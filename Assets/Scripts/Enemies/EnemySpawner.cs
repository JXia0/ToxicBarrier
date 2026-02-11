using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物刷新器。在 MapZone 环内或矩形区域内按波次刷新敌人，支持 Bug/Leader/Boss 类型权重。
/// 与 EnvironmentalResourceSpawner 一致：可绑 MapZone 或在无 Zone 时使用矩形范围。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
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

    private void Start()
    {
        StartSpawning();
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
        if (bugPrefab == null && leaderPrefab == null && bossPrefab == null)
        {
            Debug.LogWarning("EnemySpawner: 至少需要设置一个敌人预制体 (Bug / Leader / Boss)。", this);
            yield break;
        }

        do
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
        while (continuousWaves);

        _spawnCoroutine = null;
    }

    private void SpawnOneEnemy(Vector2 position)
    {
        GameObject prefab = RollPrefabByWeight();
        if (prefab == null)
            return;
        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        go.name = prefab.name + "(Clone)";
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
