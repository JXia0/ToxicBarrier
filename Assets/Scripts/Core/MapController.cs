using UnityEngine;

/// <summary>
/// Central map/game controller. References and coordinates Player, Combat, Loot, and Map systems.
/// Scripts keep their individuality; MapController is the single place that wires and drives them.
/// </summary>
public class MapController : MonoBehaviour
{
    public static MapController Instance { get; private set; }

    [Header("Player")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private HealthSystem playerHealth;

    [Header("Loot & Map")]
    [SerializeField] private LootManager lootManager;
    [SerializeField] private MapScale mapScale;
    [SerializeField] private int currentMapId = 1;
    [Tooltip("Map 1 zone: used for map center and Map 1 radius (e.g. central tower attack range = radius * 0.5).")]
    [SerializeField] private MapZone map1Zone;

    [Header("Toxic Gas (central drive)")]
    [Tooltip("If true, MapController drives toxic gas periods; otherwise each HealthSystem uses its own timer.")]
    [SerializeField] private bool driveToxicGasCentrally = true;
    [SerializeField] private float toxicGasPeriodSeconds = 60f;
    private float _toxicGasTimer;
    private int _toxicGasPeriodsElapsed;

    public int CurrentMapId => currentMapId;
    public int ToxicGasPeriodsElapsed => _toxicGasPeriodsElapsed;
    public PlayerController Player => playerController;
    public HealthSystem PlayerHealth => playerHealth;
    public LootManager Loot => lootManager != null ? lootManager : LootManager.Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (lootManager == null)
            lootManager = LootManager.Instance;

        if (driveToxicGasCentrally && playerHealth != null)
            playerHealth.SetToxicGasDrivenCentrally(true);
    }

    private void Update()
    {
        if (driveToxicGasCentrally && playerHealth != null && playerHealth.IsPlayer)
            TickCentralToxicGas(Time.deltaTime);
    }

    private void TickCentralToxicGas(float deltaTime)
    {
        _toxicGasTimer += deltaTime;
        while (_toxicGasTimer >= toxicGasPeriodSeconds)
        {
            _toxicGasTimer -= toxicGasPeriodSeconds;
            _toxicGasPeriodsElapsed++;
        }
        playerHealth.SetToxicGasPeriodsElapsed(_toxicGasPeriodsElapsed);
    }

    // —— Map ID (for LootManager environmental spawns) ——
    public void SetMapId(int mapId)
    {
        currentMapId = Mathf.Max(1, mapId);
    }

    public int GetMapId() => currentMapId;

    /// <summary> World position of map center (from Map 1 zone). </summary>
    public Vector2 GetMapCenter()
    {
        if (map1Zone != null)
        {
            map1Zone.GetRingParams(out Vector2 center, out _, out _);
            return center;
        }
        return transform.position;
    }

    /// <summary> Map 1 outer radius. e.g. central tower attack range = this * 0.5. </summary>
    public float GetMap1Radius()
    {
        if (map1Zone != null)
        {
            map1Zone.GetRingParams(out _, out _, out float maxRadius);
            return maxRadius;
        }
        return 5f;
    }

    // —— Player (delegate to subsystems, keep individuality there) ——
    public void SetPlayerControlEnabled(bool enabled)
    {
        if (playerController != null)
            playerController.enabled = enabled;
    }

    public void SetPlayerMoveSpeed(float speed)
    {
        if (playerController != null)
            playerController.SetMoveSpeed(speed);
    }

    /// <summary> Apply move speed from MapScale tier (1–4). </summary>
    public void ApplyMoveSpeedTier(int tier)
    {
        if (mapScale != null)
            mapScale.ApplyTierToPlayer(this, tier);
        else if (playerController != null)
            playerController.SetMoveSpeed(5f);
    }

    // —— Loot (delegate to LootManager) ——
    public void SpawnEnvironmentalResource(Vector2 position)
    {
        if (Loot != null)
            Loot.SpawnEnvironmentalResource(currentMapId, position);
    }

    public void GetMapSpawnRates(out float woodRate, out float stoneRate)
    {
        woodRate = 0f;
        stoneRate = 0f;
        if (Loot != null)
            Loot.GetMapSpawnRates(currentMapId, out woodRate, out stoneRate);
    }

    // —— Toxic gas (central override) ——
    public void SetToxicGasPeriods(int periods)
    {
        _toxicGasPeriodsElapsed = Mathf.Max(0, periods);
        if (playerHealth != null && playerHealth.IsPlayer)
            playerHealth.SetToxicGasPeriodsElapsed(_toxicGasPeriodsElapsed);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
