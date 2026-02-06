using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles enemy loot drops (Gel, Teeth) and map-based environmental resource spawn rates.
/// Rates are modular and editable in the Inspector. Hook enemy death to RollEnemyLoot.
/// </summary>
public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Enemy drop rates (0–1). GDD: Bug 70% Gel 10% Teeth, Leader 40% Gel 30% Teeth, Boss 10% Gel 60% Teeth")]
    [SerializeField] private EnemyLootConfig bugLoot = new EnemyLootConfig { gelChance = 0.7f, teethChance = 0.1f };
    [SerializeField] private EnemyLootConfig leaderLoot = new EnemyLootConfig { gelChance = 0.4f, teethChance = 0.3f };
    [SerializeField] private EnemyLootConfig bossLoot = new EnemyLootConfig { gelChance = 0.1f, teethChance = 0.6f };

    [Header("Map environmental spawn rates (0–1). Map 1: 0.8 Wood; Map 2: 0.5 Wood 0.3 Stone; Map 3: 0.2 Wood 0.6 Stone")]
    [SerializeField] private List<MapSpawnConfig> mapSpawnConfigs = new List<MapSpawnConfig>
    {
        new MapSpawnConfig { mapId = 1, woodRate = 0.8f, stoneRate = 0f },
        new MapSpawnConfig { mapId = 2, woodRate = 0.5f, stoneRate = 0.3f },
        new MapSpawnConfig { mapId = 3, woodRate = 0.2f, stoneRate = 0.6f }
    };

    [Header("Drop prefabs (assign in Inspector)")]
    [SerializeField] private GameObject gelPrefab;
    [SerializeField] private GameObject teethPrefab;
    [SerializeField] private GameObject woodPrefab;
    [SerializeField] private GameObject stonePrefab;

    [Header("Spawn")]
    [SerializeField] private float dropSpawnRadius = 0.5f;

    [Serializable]
    public struct EnemyLootConfig
    {
        [Range(0f, 1f)] public float gelChance;
        [Range(0f, 1f)] public float teethChance;
    }

    [Serializable]
    public struct MapSpawnConfig
    {
        public int mapId;
        [Range(0f, 1f)] public float woodRate;
        [Range(0f, 1f)] public float stoneRate;
    }

    public enum ResourceType { Gel, Teeth, Wood, Stone }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Roll and spawn enemy drops at position. Call from enemy death (e.g. HealthSystem.OnDeath).
    /// </summary>
    public void RollEnemyLoot(HealthSystem.EnemyType enemyType, Vector2 position)
    {
        EnemyLootConfig config = GetEnemyLootConfig(enemyType);

        if (UnityEngine.Random.value <= config.gelChance)
            SpawnResource(ResourceType.Gel, position);

        if (UnityEngine.Random.value <= config.teethChance)
            SpawnResource(ResourceType.Teeth, position);
    }

    /// <summary>
    /// Get drop config for an enemy type (for custom roll logic).
    /// </summary>
    public EnemyLootConfig GetEnemyLootConfig(HealthSystem.EnemyType enemyType)
    {
        switch (enemyType)
        {
            case HealthSystem.EnemyType.Bug: return bugLoot;
            case HealthSystem.EnemyType.Leader: return leaderLoot;
            case HealthSystem.EnemyType.Boss: return bossLoot;
            default: return bugLoot;
        }
    }

    /// <summary>
    /// Spawn a single resource at position (with small random offset).
    /// </summary>
    public void SpawnResource(ResourceType type, Vector2 position)
    {
        GameObject prefab = GetPrefabForResource(type);
        if (prefab == null) return;

        Vector2 offset = UnityEngine.Random.insideUnitCircle * dropSpawnRadius;
        GameObject go = Instantiate(prefab, position + offset, Quaternion.identity);
        go.SetActive(true);
    }

    private GameObject GetPrefabForResource(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Gel: return gelPrefab;
            case ResourceType.Teeth: return teethPrefab;
            case ResourceType.Wood: return woodPrefab;
            case ResourceType.Stone: return stonePrefab;
            default: return null;
        }
    }

    /// <summary>
    /// Get environmental spawn rates for a map. Use for spawning resources on the map by ID.
    /// Map 1: 0.8 Wood; Map 2: 0.5 Wood, 0.3 Stone; Map 3: 0.2 Wood, 0.6 Stone.
    /// </summary>
    public void GetMapSpawnRates(int mapId, out float woodRate, out float stoneRate)
    {
        woodRate = 0f;
        stoneRate = 0f;

        foreach (var config in mapSpawnConfigs)
        {
            if (config.mapId == mapId)
            {
                woodRate = config.woodRate;
                stoneRate = config.stoneRate;
                return;
            }
        }
    }

    /// <summary>
    /// Spawn one random environmental resource at position based on map rates.
    /// Rolls once: wood vs stone vs nothing by map rates.
    /// </summary>
    public void SpawnEnvironmentalResource(int mapId, Vector2 position)
    {
        GetMapSpawnRates(mapId, out float woodRate, out float stoneRate);

        float r = UnityEngine.Random.value;
        if (r < woodRate)
            SpawnResource(ResourceType.Wood, position);
        else if (r < woodRate + stoneRate)
            SpawnResource(ResourceType.Stone, position);
    }

    /// <summary>
    /// Get full config for a map (e.g. for level generation). Returns null if mapId not found.
    /// </summary>
    public MapSpawnConfig? GetMapConfig(int mapId)
    {
        foreach (var config in mapSpawnConfigs)
            if (config.mapId == mapId) return config;
        return null;
    }
}
