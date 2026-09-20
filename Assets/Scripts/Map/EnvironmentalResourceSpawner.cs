using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns environmental resources inside one map ring.
/// The ring supplies its map level, and LootManager supplies the matching rates.
/// </summary>
public class EnvironmentalResourceSpawner : MonoBehaviour
{
    public enum ResourceFilter
    {
        Both,
        WoodOnly,
        StoneOnly
    }

    [Header("Map ring")]
    [SerializeField] private MapZone mapZone;

    [Header("Spawn")]
    [SerializeField] private int spawnCount = 40;
    [SerializeField] private float spawnIntervalSeconds = 0.5f;
    [SerializeField] private ResourceFilter resourceFilter = ResourceFilter.Both;
    [SerializeField] private float minDistanceBetween = 0.5f;
    [SerializeField] private LootManager lootManager;

    private Coroutine _spawnCoroutine;

    private void Start()
    {
        if (lootManager == null)
            lootManager = LootManager.Instance;
        StartSpawning();
    }

    public void StartSpawning()
    {
        StopSpawning();
        _spawnCoroutine = StartCoroutine(SpawnOverTime());
    }

    public void StopSpawning()
    {
        if (_spawnCoroutine == null) return;

        StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = null;
    }

    private IEnumerator SpawnOverTime()
    {
        if (mapZone == null || lootManager == null)
        {
            Debug.LogWarning("EnvironmentalResourceSpawner requires a MapZone and LootManager.", this);
            _spawnCoroutine = null;
            yield break;
        }

        while (!mapZone.IsUnlocked)
            yield return null;

        foreach (Vector2 position in BuildSpawnPositions())
        {
            SpawnOne(position);
            yield return new WaitForSeconds(spawnIntervalSeconds);
        }

        _spawnCoroutine = null;
    }

    public void SpawnAll()
    {
        StopSpawning();
        foreach (Vector2 position in BuildSpawnPositions())
            SpawnOne(position);
    }

    private void SpawnOne(Vector2 position)
    {
        if (resourceFilter == ResourceFilter.WoodOnly)
            lootManager.SpawnResource(LootManager.ResourceType.Wood, position, addRandomOffset: false);
        else if (resourceFilter == ResourceFilter.StoneOnly)
            lootManager.SpawnResource(LootManager.ResourceType.Stone, position, addRandomOffset: false);
        else
            lootManager.SpawnEnvironmentalResource(mapZone.MapLevel, position);
    }

    private List<Vector2> BuildSpawnPositions()
    {
        mapZone.GetRingParams(out Vector2 center, out float innerRadius, out float outerRadius);
        var positions = new List<Vector2>(spawnCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 position = center;
            bool found = false;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                position = RandomPointInRing(center, innerRadius, outerRadius);
                if (!ReachableGround.TryFind(position, .6f, out position)) continue;
                float radius = Vector2.Distance(position, center);
                if (radius >= innerRadius && radius <= outerRadius && IsFarEnough(position, positions))
                { found = true; break; }
            }
            if (found) positions.Add(position);
        }

        return positions;
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
        if (mapZone == null) return;

        mapZone.GetRingParams(out Vector2 center, out float innerRadius, out float outerRadius);
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
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
