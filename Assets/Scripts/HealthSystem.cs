using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Shared health, ATK, and attack speed. Supports toxic gas DoT for the player;
/// attach to both player and enemies. Use EnemyType for monster base stats (Bug, Leader, Boss).
/// </summary>
public class HealthSystem : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private bool isPlayer;
    [SerializeField] private EnemyType enemyType = EnemyType.Bug;

    [Header("Combat Stats")]
    [SerializeField] private float atk = 50f;
    [SerializeField] private float ats = 1f;  // attacks per second
    [SerializeField] private float maxHp = 100f;

    [Header("Toxic Gas (Player only)")]
    [SerializeField] private bool affectedByToxicGas = true;
    [SerializeField] private float toxicGasBaseDamagePerSecond = 5f;
    [SerializeField] private float toxicGasPeriodSeconds = 60f;
    [SerializeField] private float toxicGasDamageIncreasePerPeriod = 3f;

    private float _currentHp;
    private float _toxicGasTimer;
    private int _toxicGasPeriodsElapsed;
    private bool _hasDied;

    public float CurrentHp => _currentHp;
    public float MaxHp => maxHp;
    public float Atk => atk;
    public float Ats => ats;
    public bool IsPlayer => isPlayer;
    public EnemyType EnemyType => enemyType;

    public UnityEvent<float, float> OnHpChanged;
    public UnityEvent OnDeath;

    public enum EnemyType
    {
        Bug,    // 100 ATK, 50 HP (GDD); use for scaling
        Leader, // 50 ATK, 150 HP
        Boss    // 3000 ATK, 500 HP
    }

    private void Awake()
    {
        _currentHp = maxHp;
        if (isPlayer && affectedByToxicGas)
            _toxicGasTimer = 0f;
    }

    private void Start()
    {
        ApplyEnemyTypeStats();
    }

    private void Update()
    {
        if (isPlayer && affectedByToxicGas && _currentHp > 0f)
            TickToxicGas(Time.deltaTime);
    }

    /// <summary>
    /// Apply GDD base stats by enemy type. Overwrites inspector ATK/HP for enemies.
    /// </summary>
    private void ApplyEnemyTypeStats()
    {
        if (isPlayer) return;

        switch (enemyType)
        {
            case EnemyType.Bug:
                atk = 100f;
                maxHp = 50f;
                break;
            case EnemyType.Leader:
                atk = 50f;
                maxHp = 150f;
                break;
            case EnemyType.Boss:
                atk = 3000f;
                maxHp = 500f;
                break;
        }

        _currentHp = maxHp;
    }

    /// <summary>
    /// Toxic gas: damage over time that scales up every period.
    /// </summary>
    private void TickToxicGas(float deltaTime)
    {
        _toxicGasTimer += deltaTime;

        while (_toxicGasTimer >= toxicGasPeriodSeconds)
        {
            _toxicGasTimer -= toxicGasPeriodSeconds;
            _toxicGasPeriodsElapsed++;
        }

        float damagePerSecond = toxicGasBaseDamagePerSecond + _toxicGasPeriodsElapsed * toxicGasDamageIncreasePerPeriod;
        float damage = damagePerSecond * deltaTime;
        TakeDamage(damage, null);
    }

    public void TakeDamage(float amount, GameObject source)
    {
        if (amount <= 0f || _hasDied) return;

        _currentHp = Mathf.Max(0f, _currentHp - amount);
        OnHpChanged?.Invoke(_currentHp, maxHp);

        if (_currentHp <= 0f)
        {
            _hasDied = true;
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        _currentHp = Mathf.Min(maxHp, _currentHp + amount);
        OnHpChanged?.Invoke(_currentHp, maxHp);
    }

    /// <summary>
    /// Call when toxic gas intensity is driven by another system (e.g. global timer).
    /// </summary>
    public void SetToxicGasPeriodsElapsed(int periods)
    {
        _toxicGasPeriodsElapsed = Mathf.Max(0, periods);
    }

    public int GetToxicGasPeriodsElapsed() => _toxicGasPeriodsElapsed;

    /// <summary>
    /// Override stats at runtime (e.g. for difficulty scaling).
    /// </summary>
    public void SetStats(float newAtk, float newAts, float newMaxHp)
    {
        atk = Mathf.Max(0f, newAtk);
        ats = Mathf.Max(0f, newAts);
        float oldMax = maxHp;
        maxHp = Mathf.Max(1f, newMaxHp);
        _currentHp = maxHp * (_currentHp / Mathf.Max(0.001f, oldMax));
        OnHpChanged?.Invoke(_currentHp, maxHp);
    }
}
