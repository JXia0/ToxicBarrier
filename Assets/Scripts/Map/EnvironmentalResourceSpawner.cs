using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns Wood and Stone one at a time via coroutine, using LootManager's map rates or a type override.
/// Map 1: 0.8 Wood | Map 2: 0.5 Wood, 0.3 Stone | Map 3: 0.2 Wood, 0.6 Stone.
/// Control spawn interval and which types to spawn (Both, Wood only, Stone only).
/// </summary>
public class EnvironmentalResourceSpawner : MonoBehaviour
{
    public enum ResourceFilter
    {
        Both,       // Use map rates (Wood/Stone per zone)
        WoodOnly,
        StoneOnly
    }

    [Header("Spawn area")]
    [Tooltip("If set, spawn inside this zone's bounds and use this zone's map ID (for three circles in one scene).")]
    [SerializeField] private MapZone mapZone;
    [Tooltip("Used when Map Zone is not set. Bottom-left of rectangle.")]
    [SerializeField] private Vector2 spawnAreaMin = new Vector2(-10f, -10f);
    [Tooltip("Used when Map Zone is not set. Top-right of rectangle.")]
    [SerializeField] private Vector2 spawnAreaMax = new Vector2(10f, 10f);

    [Header("Spawn count & timing")]
    [Tooltip("Number of spawn slots. Each slot gets one roll based on Resource Filter.")]
    [SerializeField] private int spawnCount = 40;
    [Tooltip("Seconds between each spawn.")]
    [SerializeField] private float spawnIntervalSeconds = 0.5f;
    [Tooltip("Which resources to spawn. Both = use map rates; WoodOnly/StoneOnly = force that type.")]
    [SerializeField] private ResourceFilter resourceFilter = ResourceFilter.Both;

    [Header("Optional: spacing")]
    [Tooltip("Minimum distance between spawn positions. 0 = no spacing.")]
    [SerializeField] private float minDistanceBetween = 0.5f;

    [Header("References")]
    [SerializeField] private MapController mapController;
    [SerializeField] private LootManager lootManager;

    private Coroutine _spawnCoroutine;

    private void Start()
    {
        if (mapController == null) mapController = MapController.Instance;
        if (lootManager == null) lootManager = LootManager.Instance;

        StartSpawning();
    }

    /// <summary>
    /// Start the spawn coroutine. Stops any running coroutine first.
    /// </summary>
    public void StartSpawning()
    {
        if (_spawnCoroutine != null)
            StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = StartCoroutine(SpawnOverTimeCoroutine());
    }

    /// <summary>
    /// Stop the current spawn coroutine.
    /// </summary>
    public void StopSpawning()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnOverTimeCoroutine()
    {
        if (lootManager == null)
        {
            Debug.LogWarning("EnvironmentalResourceSpawner: LootManager missing.");
            yield break;
        }

        int mapId;
        bool useRing = mapZone != null;
        Vector2 center = Vector2.zero;
        float innerR = 0f, outerR = 5f;
        Vector2 rectMin = spawnAreaMin, rectMax = spawnAreaMax;

        if (mapZone != null)
        {
            mapZone.GetRingParams(out center, out innerR, out outerR);
            mapId = mapZone.MapId;
        }
        else
        {
            rectMin = spawnAreaMin;
            rectMax = spawnAreaMax;
            mapId = mapController != null ? mapController.CurrentMapId : 1;
        }

        List<Vector2> positions = useRing ? BuildSpawnPositionsInRing(center, innerR, outerR) : BuildSpawnPositions(rectMin, rectMax);

        foreach (Vector2 pos in positions)
        {
            SpawnOneResource(mapId, pos);
            yield return new WaitForSeconds(spawnIntervalSeconds);
        }

        _spawnCoroutine = null;
    }

    private void SpawnOneResource(int mapId, Vector2 pos)
    {
        switch (resourceFilter)
        {
            case ResourceFilter.WoodOnly:
                lootManager.SpawnResource(LootManager.ResourceType.Wood, pos, addRandomOffset: false);
                break;
            case ResourceFilter.StoneOnly:
                lootManager.SpawnResource(LootManager.ResourceType.Stone, pos, addRandomOffset: false);
                break;
            default:
                lootManager.SpawnEnvironmentalResource(mapId, pos);
                break;
        }
    }

    /// <summary>
    /// Spawn all at once (legacy). Prefer StartSpawning for staggered spawns.
    /// </summary>
    public void SpawnAll()
    {
        StopSpawning();
        if (lootManager == null) return;

        int mapId;
        bool useRing = mapZone != null;
        Vector2 center = Vector2.zero;
        float innerR = 0f, outerR = 5f;
        Vector2 rectMin = spawnAreaMin, rectMax = spawnAreaMax;

        if (mapZone != null)
        {
            mapZone.GetRingParams(out center, out innerR, out outerR);
            mapId = mapZone.MapId;
        }
        else
        {
            rectMin = spawnAreaMin;
            rectMax = spawnAreaMax;
            mapId = mapController != null ? mapController.CurrentMapId : 1;
        }

        var positions = useRing ? BuildSpawnPositionsInRing(center, innerR, outerR) : BuildSpawnPositions(rectMin, rectMax);
        foreach (Vector2 pos in positions)
            SpawnOneResource(mapId, pos);
    }

    private List<Vector2> BuildSpawnPositionsInRing(Vector2 center, float innerRadius, float outerRadius)
    {
        var list = new List<Vector2>(spawnCount);
        int attempts = 0;
        const int maxAttempts = 2000;

        while (list.Count < spawnCount && attempts < maxAttempts)
        {
            attempts++;
            Vector2 pos = RandomPointInRing(center, innerRadius, outerRadius);

            if (minDistanceBetween > 0f && !IsFarEnoughFromAll(pos, list))
                continue;

            list.Add(pos);
        }

        return list;
    }

    /// <summary> Random point inside annulus (ring): distance between innerR and outerR from center. </summary>
    private Vector2 RandomPointInRing(Vector2 center, float innerR, float outerR)
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(UnityEngine.Random.Range(innerR * innerR, outerR * outerR));
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
    }

    private List<Vector2> BuildSpawnPositions(Vector2 min, Vector2 max)
    {
        var list = new List<Vector2>(spawnCount);
        int attempts = 0;
        const int maxAttempts = 2000;

        while (list.Count < spawnCount && attempts < maxAttempts)
        {
            attempts++;
            Vector2 pos = RandomPointInArea(min, max);

            if (minDistanceBetween > 0f && !IsFarEnoughFromAll(pos, list))
                continue;

            list.Add(pos);
        }

        return list;
    }

    private Vector2 RandomPointInArea(Vector2 min, Vector2 max)
    {
        float x = Mathf.Lerp(min.x, max.x, UnityEngine.Random.value);
        float y = Mathf.Lerp(min.y, max.y, UnityEngine.Random.value);
        return new Vector2(x, y);
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
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        if (mapZone != null)
        {
            mapZone.GetRingParams(out Vector2 center, out float innerR, out float outerR);
            DrawGizmoRing(center, innerR, outerR);
        }
        else
        {
            Vector2 min = spawnAreaMin, max = spawnAreaMax;
            Vector3 center = (Vector3)(min + max) * 0.5f;
            Vector3 size = new Vector3(max.x - min.x, max.y - min.y, 0.01f);
            Gizmos.DrawCube(center, size);
        }
    }

    private void DrawGizmoRing(Vector2 center, float innerR, float outerR)
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
