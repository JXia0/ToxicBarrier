using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles enemy loot drops (Gel, Teeth) and map-based environmental resource spawn rates.
/// Rates are modular and editable in the Inspector. Use via LootManager.Instance.
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
        new MapSpawnConfig { mapLevel = 1, woodRate = 0.8f, stoneRate = 0f },
        new MapSpawnConfig { mapLevel = 2, woodRate = 0.5f, stoneRate = 0.3f },
        new MapSpawnConfig { mapLevel = 3, woodRate = 0.2f, stoneRate = 0.6f }
    };

    [Header("Drop prefabs (assign in Inspector)")]
    [SerializeField] private GameObject gelPrefab;
    [SerializeField] private GameObject teethPrefab;
    [SerializeField] private GameObject woodPrefab;
    [SerializeField] private GameObject stonePrefab;

    [Header("Spawn")]
    [SerializeField] private float dropSpawnRadius = 0.5f;

    [Header("Disguised parasite")]
    [Tooltip("Independent chance that any spawned Bug, Leader, or Boss carries a parasite.")]
    [Range(0f, 1f)] [SerializeField] private float parasiteInfectionChance = 0.2f;
    [Tooltip("Damage dealt whenever the player remains on the disguised parasite for one tick.")]
    [Min(0f)] [SerializeField] private float parasiteDamagePerTick = 10f;
    [Tooltip("Seconds between damage ticks while the player is standing on the disguised parasite.")]
    [Min(0.05f)] [SerializeField] private float parasiteDamageInterval = 0.7f;
    [Tooltip("Distance at which the red screen-edge warning begins to appear.")]
    [Min(0f)] [SerializeField] private float parasiteWarningRadius = 3.5f;

    [Serializable]
    public struct EnemyLootConfig
    {
        [Range(0f, 1f)] public float gelChance;
        [Range(0f, 1f)] public float teethChance;
    }

    [Serializable]
    public struct MapSpawnConfig
    {
        public int mapLevel;
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

    public void RollEnemyLoot(HealthSystem.EnemyType enemyType, Vector2 position)
    {
        EnemyLootConfig config = GetEnemyLootConfig(enemyType);
        if (UnityEngine.Random.value <= config.gelChance)
            SpawnResource(ResourceType.Gel, position);
        if (UnityEngine.Random.value <= config.teethChance)
            SpawnResource(ResourceType.Teeth, position);
    }

    public bool RollParasiteInfection()
    {
        return UnityEngine.Random.value < parasiteInfectionChance;
    }

    public void SpawnDisguisedParasite(HealthSystem.EnemyType hostType, Vector2 position)
    {
        GameObject disguisePrefab = GetDisguisePrefab(hostType);
        if (disguisePrefab == null)
        {
            Debug.LogWarning("A disguised parasite needs either the Gel or Teeth drop prefab.", this);
            return;
        }

        GameObject parasiteObject = UnityEngine.Object.Instantiate(disguisePrefab, position, Quaternion.identity);
        parasiteObject.name = $"DisguisedParasite ({disguisePrefab.name})";

        ResourcePickup pickup = parasiteObject.GetComponent<ResourcePickup>();
        if (pickup != null)
        {
            pickup.enabled = false;
            UnityEngine.Object.Destroy(pickup);
        }

        DisguisedParasite parasite = parasiteObject.AddComponent<DisguisedParasite>();
        parasite.Configure(parasiteDamagePerTick, parasiteDamageInterval, parasiteWarningRadius);
    }

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

    public void SpawnResource(ResourceType type, Vector2 position, bool addRandomOffset = true)
    {
        GameObject prefab = GetPrefabForResource(type);
        if (prefab == null) return;
        if (addRandomOffset)
            position += (Vector2)UnityEngine.Random.insideUnitCircle * dropSpawnRadius;
        UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
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

    private GameObject GetDisguisePrefab(HealthSystem.EnemyType hostType)
    {
        EnemyLootConfig config = GetEnemyLootConfig(hostType);
        float gelWeight = gelPrefab != null ? config.gelChance : 0f;
        float teethWeight = teethPrefab != null ? config.teethChance : 0f;
        float totalWeight = gelWeight + teethWeight;

        if (totalWeight <= 0f)
            return gelPrefab != null ? gelPrefab : teethPrefab;

        return UnityEngine.Random.value * totalWeight < gelWeight ? gelPrefab : teethPrefab;
    }

    public void GetMapSpawnRates(int mapLevel, out float woodRate, out float stoneRate)
    {
        woodRate = 0f;
        stoneRate = 0f;
        foreach (var config in mapSpawnConfigs)
        {
            if (config.mapLevel == mapLevel)
            {
                woodRate = config.woodRate;
                stoneRate = config.stoneRate;
                return;
            }
        }
    }

    public void SpawnEnvironmentalResource(int mapLevel, Vector2 position)
    {
        GetMapSpawnRates(mapLevel, out float woodRate, out float stoneRate);
        float r = UnityEngine.Random.value;
        if (r < woodRate)
            SpawnResource(ResourceType.Wood, position, addRandomOffset: false);
        else if (r < woodRate + stoneRate)
            SpawnResource(ResourceType.Stone, position, addRandomOffset: false);
    }

}
