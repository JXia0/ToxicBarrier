using UnityEngine;

/// <summary>
/// Coordinates the player, map unlocks, and toxic gas.
/// Map rings own their own level/config data; this controller does not track a global map ID.
/// </summary>
public class MapController : MonoBehaviour
{
    public static MapController Instance { get; private set; }

    [Header("Player")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private HealthSystem playerHealth;

    [Header("Map")]
    [SerializeField] private MapZone map1Zone;
    [SerializeField] private MapZone map2Zone;
    [SerializeField] private MapZone map3Zone;

    [Header("Progressive unlock")]
    [SerializeField] private Tower tower;

    [Header("Toxic Gas")]
    [SerializeField] private bool driveToxicGasCentrally = true;
    [SerializeField] private float toxicGasPeriodSeconds = 60f;

    private float _toxicGasTimer;
    private int _toxicGasPeriodsElapsed;

    public int ToxicGasPeriodsElapsed => _toxicGasPeriodsElapsed;
    public PlayerController Player => playerController;
    public HealthSystem PlayerHealth => playerHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

    public bool IsMapUnlocked(int mapLevel)
    {
        if (mapLevel <= 1) return true;
        return tower != null && tower.CurrentLevel >= mapLevel;
    }

    public float GetMaxAllowedRadius()
    {
        MapZone outerZone = map1Zone;
        if (IsMapUnlocked(2)) outerZone = map2Zone;
        if (IsMapUnlocked(3)) outerZone = map3Zone;

        outerZone.GetRingParams(out _, out _, out float radius);
        return radius;
    }

    public Vector2 GetMapCenter()
    {
        map1Zone.GetRingParams(out Vector2 center, out _, out _);
        return center;
    }

    public float GetMap1Radius()
    {
        map1Zone.GetRingParams(out _, out _, out float radius);
        return radius;
    }

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
