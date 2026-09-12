using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns the wave config assigned to this map ring.
/// Each ring owns one spawner and one LevelWavesConfig; no global map-ID selector is needed.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Map ring")]
    [SerializeField] private MapZone mapZone;
    [SerializeField] private Tower towerToExclude;

    [Header("Enemy prefabs")]
    [SerializeField] private GameObject bugPrefab;
    [SerializeField] private GameObject leaderPrefab;
    [SerializeField] private GameObject bossPrefab;

    [Header("Waves")]
    [SerializeField] private LevelWavesConfig levelWaves;
    [SerializeField] private bool loopWaves;
    [Tooltip("Zero-based wave index where leaders start appearing.")]
    [Min(0)] [SerializeField] private int leaderStartWave = 3;
    [Tooltip("Zero-based wave index where bosses start appearing.")]
    [Min(0)] [SerializeField] private int bossStartWave = 6;
    [Min(1)] [SerializeField] private int bugCost = 50;
    [Min(1)] [SerializeField] private int leaderCost = 120;
    [Min(1)] [SerializeField] private int bossCost = 320;

    [Header("Spawn")]
    [Min(0f)] [SerializeField] private float spawnIntervalSeconds = 0.8f;
    [Min(0f)] [SerializeField] private float minDistanceBetween = 1f;
    [SerializeField] private bool drawGizmos = true;

    private Coroutine _spawnCoroutine;
    private int _aliveEnemies;
    public int CurrentWaveNumber { get; private set; }
    public int TotalWaves => levelWaves != null && levelWaves.waves != null ? levelWaves.waves.Length : 0;
    public int AliveEnemies => _aliveEnemies;
    public int DefeatedCount { get; private set; }
    public bool HasFinishedSpawning { get; private set; }
    public bool IsWaitingForNextWave { get; private set; }
    public float NextWaveSeconds { get; private set; }
    public bool IsCleared => HasFinishedSpawning && _aliveEnemies == 0;
    public bool IsUnlocked => mapZone != null && mapZone.IsUnlocked;
    public int RegionLevel => mapZone != null ? mapZone.MapLevel : 0;

    private void Start()
    {
        StartSpawning();
    }

    public void StartSpawning()
    {
        StopSpawning();
        HasFinishedSpawning = false;
        IsWaitingForNextWave = false;
        NextWaveSeconds = 0;
        CurrentWaveNumber = 0;
        _spawnCoroutine = StartCoroutine(SpawnWavesCoroutine());
    }

    public void StopSpawning()
    {
        if (_spawnCoroutine == null) return;

        StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = null;
    }

    private IEnumerator SpawnWavesCoroutine()
    {
        if (mapZone == null || levelWaves == null || levelWaves.waves == null)
        {
            Debug.LogWarning("EnemySpawner requires a MapZone and LevelWavesConfig.", this);
            _spawnCoroutine = null;
            yield break;
        }

        while (!mapZone.IsUnlocked)
            yield return null;

        do
        {
            for (int waveIndex = 0; waveIndex < levelWaves.waves.Length; waveIndex++)
            {
                CurrentWaveNumber = waveIndex + 1;
                yield return SpawnWave(levelWaves.waves[waveIndex], waveIndex);
            }
        }
        while (loopWaves);

        HasFinishedSpawning = true;
        _spawnCoroutine = null;
    }

    private IEnumerator SpawnWave(LevelWavesConfig.Wave wave, int waveIndex)
    {
        List<GameObject> enemies = BuildWave(wave.budget, waveIndex);
        List<Vector2> positions = BuildSpawnPositions(enemies.Count);

        for (int i = 0; i < enemies.Count; i++)
        {
            SpawnEnemy(enemies[i], positions[i]);
            yield return new WaitForSeconds(spawnIntervalSeconds);
        }

        float elapsed = 0f;
        // A final wave has no next-wave delay; clear status still waits for its living enemies.
        if (waveIndex == levelWaves.waves.Length - 1 && !loopWaves) yield break;
        IsWaitingForNextWave = true;
        while (elapsed < wave.maxDelayBeforeNext)
        {
            float waitUntil = _aliveEnemies <= wave.minRemainingEnemiesToSpeedUp ? wave.minDelayBeforeNext : wave.maxDelayBeforeNext;
            NextWaveSeconds = Mathf.Max(0, waitUntil - elapsed);
            elapsed += Time.deltaTime;
            if (elapsed >= wave.minDelayBeforeNext && _aliveEnemies <= wave.minRemainingEnemiesToSpeedUp)
                break;
            yield return null;
        }
        IsWaitingForNextWave = false;
        NextWaveSeconds = 0;
    }

    private List<GameObject> BuildWave(int budget, int waveIndex)
    {
        var enemies = new List<GameObject>();

        while (budget >= bugCost)
        {
            if (waveIndex >= bossStartWave && budget >= bossCost)
            {
                enemies.Add(bossPrefab);
                budget -= bossCost;
            }
            else if (waveIndex >= leaderStartWave && budget >= leaderCost)
            {
                enemies.Add(leaderPrefab);
                budget -= leaderCost;
            }
            else
            {
                enemies.Add(bugPrefab);
                budget -= bugCost;
            }
        }

        return enemies;
    }

    private void SpawnEnemy(GameObject prefab, Vector2 position)
    {
        if (prefab == null) return;

        GameObject enemy = Instantiate(prefab, position, Quaternion.identity);
        enemy.name = prefab.name + "(Clone)";

        HealthSystem health = enemy.GetComponent<HealthSystem>();
        if (health == null) return;

        _aliveEnemies++;
        health.OnDeath.AddListener(HandleEnemyDeath);
    }

    private void HandleEnemyDeath()
    {
        _aliveEnemies = Mathf.Max(0, _aliveEnemies - 1);
        DefeatedCount++;
    }

    private List<Vector2> BuildSpawnPositions(int count)
    {
        mapZone.GetRingParams(out Vector2 center, out float innerRadius, out float outerRadius);
        var positions = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            Vector2 position = center;
            for (int attempt = 0; attempt < 256; attempt++)
            {
                position = RandomPointInRing(center, innerRadius, outerRadius);
                if (TryNavigablePosition(ref position, center, innerRadius, outerRadius) &&
                    IsOutsideTowerRange(position) && IsFarEnough(position, positions))
                    break;
                if (attempt == 255)
                    throw new System.InvalidOperationException("No reachable enemy spawn position in region " + RegionLevel);
            }
            positions.Add(position);
        }

        return positions;
    }

    private bool TryNavigablePosition(ref Vector2 position, Vector2 center, float inner, float outer)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return false;
        float radius = Vector2.Distance(hit.position, center);
        if (radius < inner || radius > outer) return false;
        if (towerToExclude != null)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(hit.position, towerToExclude.transform.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
        }
        position = hit.position;
        return true;
    }

    private bool IsOutsideTowerRange(Vector2 position)
    {
        return towerToExclude == null ||
               Vector2.Distance(position, towerToExclude.transform.position) > towerToExclude.AttackRange;
    }

    private bool IsFarEnough(Vector2 position, List<Vector2> existing)
    {
        if (minDistanceBetween <= 0f) return true;

        foreach (Vector2 other in existing)
        {
            if (Vector2.Distance(position, other) < minDistanceBetween)
                return false;
        }
        return true;
    }

    private static Vector2 RandomPointInRing(Vector2 center, float innerRadius, float outerRadius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Random.Range(innerRadius * innerRadius, outerRadius * outerRadius));
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || mapZone == null) return;

        mapZone.GetRingParams(out Vector2 center, out float innerRadius, out float outerRadius);
        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.35f);
        DrawCircle(center, innerRadius);
        DrawCircle(center, outerRadius);
    }

    private static void DrawCircle(Vector2 center, float radius)
    {
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            Gizmos.DrawLine(
                center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius);
        }
    }
}
