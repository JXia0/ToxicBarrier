using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    private void Start()
    {
        StartSpawning();
    }

    public void StartSpawning()
    {
        StopSpawning();
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
        if (mapZone == null || levelWaves == null)
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
                yield return SpawnWave(levelWaves.waves[waveIndex], waveIndex);
        }
        while (loopWaves);

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
        while (elapsed < wave.maxDelayBeforeNext)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= wave.minDelayBeforeNext && _aliveEnemies <= wave.minRemainingEnemiesToSpeedUp)
                break;
            yield return null;
        }
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
    }

    private List<Vector2> BuildSpawnPositions(int count)
    {
        mapZone.GetRingParams(out Vector2 center, out float innerRadius, out float outerRadius);
        var positions = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            Vector2 position = center;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                position = RandomPointInRing(center, innerRadius, outerRadius);
                if (IsOutsideTowerRange(position) && IsFarEnough(position, positions))
                    break;
            }
            positions.Add(position);
        }

        return positions;
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
