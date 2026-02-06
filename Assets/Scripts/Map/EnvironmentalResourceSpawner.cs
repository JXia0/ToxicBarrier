using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns Wood and Stone randomly across the map at start, using LootManager's map rates:
/// Map 1: 0.8 Wood | Map 2: 0.5 Wood, 0.3 Stone | Map 3: 0.2 Wood, 0.6 Stone.
/// With one scene and three circle zones: assign a MapZone to each circle so each zone spawns its own resources by its map ID.
/// </summary>
public class EnvironmentalResourceSpawner : MonoBehaviour
{
    [Header("Spawn area")]
    [Tooltip("If set, spawn inside this zone's bounds and use this zone's map ID (for three circles in one scene).")]
    [SerializeField] private MapZone mapZone;
    [Tooltip("Used when Map Zone is not set. Bottom-left of rectangle.")]
    [SerializeField] private Vector2 spawnAreaMin = new Vector2(-10f, -10f);
    [Tooltip("Used when Map Zone is not set. Top-right of rectangle.")]
    [SerializeField] private Vector2 spawnAreaMax = new Vector2(10f, 10f);

    [Header("Spawn count")]
    [Tooltip("Number of spawn slots. Each slot gets one roll: Wood or Stone per map rates (or nothing).")]
    [SerializeField] private int spawnCount = 40;

    [Header("Optional: spacing")]
    [Tooltip("Minimum distance between spawn positions. 0 = no spacing.")]
    [SerializeField] private float minDistanceBetween = 0.5f;

    [Header("References")]
    [SerializeField] private MapController mapController;
    [SerializeField] private LootManager lootManager;

    private void Start()
    {
        if (mapController == null) mapController = MapController.Instance;
        if (lootManager == null) lootManager = LootManager.Instance;

        SpawnAll();
    }

    /// <summary>
    /// Spawn all environmental resources now. Can be called again to respawn (e.g. new wave).
    /// </summary>
    public void SpawnAll()
    {
        if (lootManager == null)
        {
            Debug.LogWarning("EnvironmentalResourceSpawner: LootManager missing.");
            return;
        }

        Vector2 min, max;
        int mapId;
        if (mapZone != null)
        {
            mapZone.GetSpawnArea(out min, out max);
            mapId = mapZone.MapId;
        }
        else
        {
            min = spawnAreaMin;
            max = spawnAreaMax;
            mapId = mapController != null ? mapController.CurrentMapId : 1;
        }

        List<Vector2> positions = BuildSpawnPositions(min, max);

        foreach (Vector2 pos in positions)
            lootManager.SpawnEnvironmentalResource(mapId, pos);
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
        Vector2 min, max;
        if (mapZone != null)
            mapZone.GetSpawnArea(out min, out max);
        else
        { min = spawnAreaMin; max = spawnAreaMax; }

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        Vector3 center = (Vector3)(min + max) * 0.5f;
        Vector3 size = new Vector3(max.x - min.x, max.y - min.y, 0.01f);
        Gizmos.DrawCube(center, size);
    }
}
