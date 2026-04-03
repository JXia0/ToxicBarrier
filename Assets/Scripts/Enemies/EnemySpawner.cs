using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 怪物刷新器。在 MapZone 环内或矩形区域内按波次刷新敌人，支持 Bug/Leader/Boss 类型权重。
/// 与 EnvironmentalResourceSpawner 一致：可绑 MapZone 或在无 Zone 时使用矩形范围。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    private enum SpawnMode
    {
        LegacyWeights = 0,
        WaveConfig = 1,
    }

    [Header("Spawn area")]
    [Tooltip("若设置，在环内刷新并可使用该 Zone 的 MapId（多环场景每环一个 Spawner）。")]
    [SerializeField] private MapZone mapZone;
    [Tooltip("未设置 MapZone 时使用。矩形左下角。")]
    [SerializeField] private Vector2 spawnAreaMin = new Vector2(-10f, -10f);
    [Tooltip("未设置 MapZone 时使用。矩形右上角。")]
    [SerializeField] private Vector2 spawnAreaMax = new Vector2(10f, 10f);

    [Header("Exclude tower range")]
    [Tooltip("若设置，怪物只会刷新在该塔攻击范围之外（避免出生在塔射程内）。")]
    [SerializeField] private Tower towerToExclude;

    [Header("Enemy prefabs (one per type)")]
    [Tooltip("Bug 敌人预制体（需带 Enemy + HealthSystem，Enemy Type 设为 Bug）。")]
    [SerializeField] private GameObject bugPrefab;
    [Tooltip("Leader 敌人预制体（Enemy Type 设为 Leader）。")]
    [SerializeField] private GameObject leaderPrefab;
    [Tooltip("Boss 敌人预制体（Enemy Type 设为 Boss）。")]
    [SerializeField] private GameObject bossPrefab;

    [Header("Wave config (PvZ-style point system)")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.LegacyWeights;

    [Tooltip("Optional: subscribe to MapWaveConfigSelector so map changes swap wave tables.")]
    [SerializeField] private MapWaveConfigSelector waveConfigSelector;

    [Tooltip("Optional: directly assign a wave table (used if selector is empty).")]
    [SerializeField] private LevelWavesConfig overrideLevelWaves;

    [Tooltip("If true, restart spawning when the wave config changes (e.g. on MapId change).")]
    [SerializeField] private bool restartOnWaveConfigChanged = true;

    [Tooltip("If true, loops waves after finishing the last wave entry.")]
    [SerializeField] private bool loopConfiguredWaves;

    [Tooltip("Stage gate: number of early waves considered stage 0 (easy).")]
    [Min(0)]
    [SerializeField] private int easyWaveCount = 3;

    [Tooltip("Stage gate: number of mid waves considered stage 1 (normal). Remaining waves are stage 2 (hard).")]
    [Min(0)]
    [SerializeField] private int normalWaveCount = 6;

    [Header("Type weights (normalized for spawn chance)")]
    [Tooltip("Bug 刷新权重。")]
    [Range(0f, 1f)]
    [SerializeField] private float bugWeight = 0.7f;
    [Tooltip("Leader 刷新权重。")]
    [Range(0f, 1f)]
    [SerializeField] private float leaderWeight = 0.25f;
    [Tooltip("Boss 刷新权重。")]
    [Range(0f, 1f)]
    [SerializeField] private float bossWeight = 0.05f;

    [Header("Spawn count & timing")]
    [Tooltip("本波刷新的怪物数量。")]
    [SerializeField] private int spawnCount = 10;
    [Tooltip("每只怪物刷新间隔（秒）。")]
    [SerializeField] private float spawnIntervalSeconds = 0.8f;
    [Tooltip("是否在完成一波后按间隔持续刷新（每波 spawnCount 只）。")]
    [SerializeField] private bool continuousWaves;

    [Header("Spacing")]
    [Tooltip("刷新点之间最小距离，0 表示不限制。")]
    [SerializeField] private float minDistanceBetween = 1f;

    [Header("Debug")]
    [Tooltip("在 Scene 视图中绘制刷新范围。")]
    [SerializeField] private bool drawGizmos = true;

    private Coroutine _spawnCoroutine;
    private int _aliveEnemies;
    private LevelWavesConfig _activeWaves;

    private void Start()
    {
        ResolveActiveWaves(forceFromSelector: true);
        StartSpawning();
    }

    private void OnEnable()
    {
        if (waveConfigSelector != null)
            waveConfigSelector.OnWaveConfigChanged += HandleWaveConfigChanged;
    }

    private void OnDisable()
    {
        if (waveConfigSelector != null)
            waveConfigSelector.OnWaveConfigChanged -= HandleWaveConfigChanged;
    }

    /// <summary> 开始刷新（先停止当前协程再启动新一轮）。 </summary>
    public void StartSpawning()
    {
        if (_spawnCoroutine != null)
            StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = StartCoroutine(SpawnWavesCoroutine());
    }

    /// <summary> 停止刷新。 </summary>
    public void StopSpawning()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnWavesCoroutine()
    {
        if (spawnMode == SpawnMode.WaveConfig)
        {
            if (!EnsureWaveModeReady())
                yield break;
        }
        else
        {
            if (bugPrefab == null && leaderPrefab == null && bossPrefab == null)
            {
                Debug.LogWarning("EnemySpawner: 至少需要设置一个敌人预制体 (Bug / Leader / Boss)。", this);
                yield break;
            }
        }

        do
        {
            if (spawnMode == SpawnMode.WaveConfig)
            {
                yield return SpawnConfiguredWavesOnce();
            }
            else
            {
                List<Vector2> positions = BuildSpawnPositions();
                foreach (Vector2 pos in positions)
                {
                    SpawnOneEnemy(pos);
                    yield return new WaitForSeconds(spawnIntervalSeconds);
                }

                if (continuousWaves)
                    yield return new WaitForSeconds(spawnIntervalSeconds * 2f);
                else
                    break;
            }
        }
        while (spawnMode == SpawnMode.WaveConfig ? loopConfiguredWaves : continuousWaves);

        _spawnCoroutine = null;
    }

    private void SpawnOneEnemy(Vector2 position)
    {
        GameObject prefab = RollPrefabByWeight();
        if (prefab == null)
            return;
        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        go.name = prefab.name + "(Clone)";

        TryTrackAlive(go);
    }

    /// <summary> Rolls Bug/Leader/Boss by weight; only considers types that have a prefab assigned. </summary>
    private GameObject RollPrefabByWeight()
    {
        float wBug = bugPrefab != null ? bugWeight : 0f;
        float wLeader = leaderPrefab != null ? leaderWeight : 0f;
        float wBoss = bossPrefab != null ? bossWeight : 0f;
        float total = wBug + wLeader + wBoss;
        if (total <= 0f)
            return bugPrefab != null ? bugPrefab : (leaderPrefab != null ? leaderPrefab : bossPrefab);
        float r = Random.Range(0f, total);
        if (r < wBug) return bugPrefab;
        if (r < wBug + wLeader) return leaderPrefab;
        return bossPrefab;
    }

    private void HandleWaveConfigChanged(int mapId, LevelWavesConfig cfg)
    {
        _activeWaves = cfg != null ? cfg : overrideLevelWaves;
        if (restartOnWaveConfigChanged && isActiveAndEnabled)
            StartSpawning();
    }

    private void ResolveActiveWaves(bool forceFromSelector)
    {
        if (forceFromSelector && waveConfigSelector != null && waveConfigSelector.CurrentConfig != null)
            _activeWaves = waveConfigSelector.CurrentConfig;
        else if (_activeWaves == null)
            _activeWaves = overrideLevelWaves;
    }

    private bool EnsureWaveModeReady()
    {
        ResolveActiveWaves(forceFromSelector: true);
        if (_activeWaves == null)
        {
            Debug.LogWarning("EnemySpawner(WaveConfig): 未指定 LevelWavesConfig（请设置 MapWaveConfigSelector 或 overrideLevelWaves）。", this);
            return false;
        }

        if (_activeWaves.waves == null || _activeWaves.waves.Length == 0)
        {
            Debug.LogWarning($"EnemySpawner(WaveConfig): LevelWavesConfig '{_activeWaves.name}' 没有 waves。", this);
            return false;
        }

        if (_activeWaves.enemyTypes == null || _activeWaves.enemyTypes.Length == 0)
        {
            Debug.LogWarning($"EnemySpawner(WaveConfig): LevelWavesConfig '{_activeWaves.name}' 没有 enemyTypes（可用怪名单）。", this);
            return false;
        }

        return true;
    }

    private IEnumerator SpawnConfiguredWavesOnce()
    {
        int waveCount = _activeWaves != null && _activeWaves.waves != null ? _activeWaves.waves.Length : 0;
        for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
        {
            WaveEntry wave = _activeWaves.waves[waveIndex];

            List<EnemyTypeConfig> toSpawn = BuildWaveEnemyList(_activeWaves, wave, stage: GetStageForWaveIndex(waveIndex));
            List<Vector2> positions = BuildSpawnPositionsForCount(toSpawn.Count);

            for (int i = 0; i < toSpawn.Count; i++)
            {
                EnemyTypeConfig type = toSpawn[i];
                Vector2 pos = positions != null && i < positions.Count ? positions[i] : PickSingleSpawnPosition();
                SpawnOneConfiguredEnemy(type, pos);
                yield return new WaitForSeconds(spawnIntervalSeconds);
            }

            float timer = 0f;
            while (true)
            {
                timer += Time.deltaTime;

                if (timer >= Mathf.Max(0f, wave.minDelayBeforeNext) && _aliveEnemies <= Mathf.Max(0, wave.minRemainingEnemiesToSpeedUp))
                    break;

                if (timer >= Mathf.Max(0f, wave.maxDelayBeforeNext))
                    break;

                yield return null;
            }
        }
    }

    private int GetStageForWaveIndex(int waveIndex)
    {
        if (easyWaveCount <= 0 && normalWaveCount <= 0) return 0;
        if (waveIndex < easyWaveCount) return 0;
        if (waveIndex < normalWaveCount) return 1;
        return 2;
    }

    /// <summary>
    /// PvZ-style wave build: spend budget to "buy" enemies from the allowed roster.
    /// - Filters by stage gate (EnemyTypeConfig.unlockStage <= stage)
    /// - Prefers higher cost first, then fills with cheaper types
    /// </summary>
    private static List<EnemyTypeConfig> BuildWaveEnemyList(LevelWavesConfig level, WaveEntry wave, int stage)
    {
        int budgetLeft = Mathf.Max(0, wave.budget);
        var roster = (level.enemyTypes ?? new EnemyTypeConfig[0])
            .Where(t => t != null && t.cost > 0 && t.unlockStage <= stage)
            .OrderByDescending(t => t.cost)
            .ToArray();

        var result = new List<EnemyTypeConfig>();
        if (roster.Length == 0 || budgetLeft <= 0)
            return result;

        int minCost = roster[roster.Length - 1].cost;
        int safety = 0;
        while (budgetLeft >= minCost && safety++ < 10000)
        {
            for (int i = 0; i < roster.Length; i++)
            {
                EnemyTypeConfig t = roster[i];
                if (budgetLeft >= t.cost)
                {
                    result.Add(t);
                    budgetLeft -= t.cost;
                    break;
                }
            }
        }
        return result;
    }

    private void SpawnOneConfiguredEnemy(EnemyTypeConfig type, Vector2 position)
    {
        GameObject prefab = ResolvePrefabForType(type);
        if (prefab == null)
            return;

        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        go.name = prefab.name + "(Clone)";
        TryTrackAlive(go);
    }

    private GameObject ResolvePrefabForType(EnemyTypeConfig type)
    {
        if (type == null) return null;
        if (type.prefab != null) return type.prefab;

        // Fallback: allow using built-in 3-prefab setup via enemyId conventions
        string id = string.IsNullOrEmpty(type.enemyId) ? "" : type.enemyId.ToLowerInvariant();
        if (id.Contains("boss")) return bossPrefab != null ? bossPrefab : (leaderPrefab != null ? leaderPrefab : bugPrefab);
        if (id.Contains("leader") || id.Contains("elite")) return leaderPrefab != null ? leaderPrefab : (bugPrefab != null ? bugPrefab : bossPrefab);
        return bugPrefab != null ? bugPrefab : (leaderPrefab != null ? leaderPrefab : bossPrefab);
    }

    private void TryTrackAlive(GameObject spawned)
    {
        if (spawned == null) return;
        HealthSystem hs = spawned.GetComponent<HealthSystem>();
        if (hs == null) return;

        _aliveEnemies++;
        hs.OnDeath.AddListener(HandleSpawnedEnemyDeath);
    }

    private void HandleSpawnedEnemyDeath()
    {
        _aliveEnemies = Mathf.Max(0, _aliveEnemies - 1);
    }

    private List<Vector2> BuildSpawnPositionsForCount(int count)
    {
        int old = spawnCount;
        spawnCount = Mathf.Max(0, count);
        List<Vector2> list = BuildSpawnPositions();
        spawnCount = old;
        return list;
    }

    private Vector2 PickSingleSpawnPosition()
    {
        // Single fallback point, respecting ring/rect + tower exclusion + spacing as much as possible.
        if (mapZone != null)
        {
            mapZone.GetRingParams(out Vector2 center, out float innerR, out float outerR);
            for (int i = 0; i < 200; i++)
            {
                Vector2 p = RandomPointInRing(center, innerR, outerR);
                if (IsOutsideTowerRange(p))
                    return p;
            }
            return center;
        }

        for (int i = 0; i < 200; i++)
        {
            Vector2 p = RandomPointInArea(spawnAreaMin, spawnAreaMax);
            if (IsOutsideTowerRange(p))
                return p;
        }
        return (spawnAreaMin + spawnAreaMax) * 0.5f;
    }

    private List<Vector2> BuildSpawnPositions()
    {
        if (mapZone != null)
        {
            mapZone.GetRingParams(out Vector2 center, out float innerR, out float outerR);
            return BuildPositionsInRing(center, innerR, outerR);
        }
        return BuildPositionsInRect(spawnAreaMin, spawnAreaMax);
    }

    private List<Vector2> BuildPositionsInRing(Vector2 center, float innerR, float outerR)
    {
        var list = new List<Vector2>(spawnCount);
        int attempts = 0;
        const int maxAttempts = 2000;

        while (list.Count < spawnCount && attempts < maxAttempts)
        {
            attempts++;
            Vector2 pos = RandomPointInRing(center, innerR, outerR);
            if (!IsOutsideTowerRange(pos) || (minDistanceBetween > 0f && !IsFarEnoughFromAll(pos, list)))
                continue;
            list.Add(pos);
        }
        return list;
    }

    private List<Vector2> BuildPositionsInRect(Vector2 min, Vector2 max)
    {
        var list = new List<Vector2>(spawnCount);
        int attempts = 0;
        const int maxAttempts = 2000;

        while (list.Count < spawnCount && attempts < maxAttempts)
        {
            attempts++;
            Vector2 pos = RandomPointInArea(min, max);
            if (!IsOutsideTowerRange(pos) || (minDistanceBetween > 0f && !IsFarEnoughFromAll(pos, list)))
                continue;
            list.Add(pos);
        }
        return list;
    }

    /// <summary> True if no tower to exclude, or position is outside the tower's attack range. </summary>
    private bool IsOutsideTowerRange(Vector2 pos)
    {
        if (towerToExclude == null) return true;
        Vector2 towerCenter = towerToExclude.transform.position;
        float excludeRadius = towerToExclude.AttackRange;
        return Vector2.Distance(pos, towerCenter) > excludeRadius;
    }

    private static Vector2 RandomPointInRing(Vector2 center, float innerR, float outerR)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(Random.Range(innerR * innerR, outerR * outerR));
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
    }

    private static Vector2 RandomPointInArea(Vector2 min, Vector2 max)
    {
        return new Vector2(Mathf.Lerp(min.x, max.x, Random.value), Mathf.Lerp(min.y, max.y, Random.value));
    }

    private bool IsFarEnoughFromAll(Vector2 pos, List<Vector2> existing)
    {
        foreach (Vector2 other in existing)
        {
            if (Vector2.Distance(pos, other) < minDistanceBetween)
                return false;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.35f);
        if (mapZone != null)
        {
            mapZone.GetRingParams(out Vector2 center, out float innerR, out float outerR);
            DrawGizmoRing(center, innerR, outerR);
        }
        else
        {
            Vector3 center = (Vector3)(spawnAreaMin + spawnAreaMax) * 0.5f;
            Vector3 size = new Vector3(spawnAreaMax.x - spawnAreaMin.x, spawnAreaMax.y - spawnAreaMin.y, 0.01f);
            Gizmos.DrawCube(center, size);
        }
        if (towerToExclude != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            float r = towerToExclude.AttackRange;
            DrawGizmoRing((Vector2)towerToExclude.transform.position, 0f, r);
        }
    }

    private static void DrawGizmoRing(Vector2 center, float innerR, float outerR)
    {
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            Gizmos.DrawLine(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerR, center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerR);
            Gizmos.DrawLine(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outerR, center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outerR);
        }
    }
}
