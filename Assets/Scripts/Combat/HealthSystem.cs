using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 统一血量/攻击/攻速管理，供玩家、敌人、塔共用。
/// 玩家：受毒气 DoT；敌人：按 EnemyType 套用基础数值（Bug/Leader/Boss）；塔：使用 Inspector 上的 atk/ats/maxHp，不受毒气与敌人类型影响。
/// 当 MapController 集中驱动毒气时，会通过 SetToxicGasPeriodsElapsed 设置周期。
/// </summary>
public class HealthSystem : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private bool isPlayer;
    [Tooltip("勾选表示塔：不套用敌人类型数值、不受毒气影响，使用下方 Combat Stats。")]
    [SerializeField] private bool isTower;
    [SerializeField] private EnemyType enemyType = EnemyType.Bug;

    [Header("Combat Stats")]
    [SerializeField] private float atk = 50f;
    [SerializeField] private float ats = 1f;
    [SerializeField] private float maxHp = 100f;

    [Header("Toxic Gas (Player only, used when not driven by MapController)")]
    [SerializeField] private bool affectedByToxicGas = true;
    [SerializeField] private float toxicGasBaseDamagePerSecond = 1.2f;
    [SerializeField] private float toxicGasPeriodSeconds = 60f;
    [SerializeField] private float toxicGasDamageIncreasePerPeriod = .1f;

    [Header("Debug")]
    [Tooltip("勾选后玩家不扣血、不会死亡（仅调试用，发布前请关闭）。")]
    [SerializeField] private bool playerInvincibleForDebug = false;

    private float _currentHp;
    private float _toxicGasTimer;
    private int _toxicGasPeriodsElapsed;
    private bool _hasDied;
    private bool _toxicGasDrivenCentrally;
    private float _contactProtectionUntil;
    private float _lastDamageAt = -10;
    private Tower _refuge;
    private SpriteRenderer _visual;
    private Color _baseColor;
    private float _flashUntil;
    private bool _finalBoss;
    private float _toxicProtectionUntil;
    public float ToxicProtectionSeconds => Mathf.Max(0, _toxicProtectionUntil - Time.time);
    public bool IsRecovering { get; private set; }
    public float ToxicDamagePerSecond => Mathf.Min(2f, toxicGasBaseDamagePerSecond + _toxicGasPeriodsElapsed * toxicGasDamageIncreasePerPeriod);

    public float CurrentHp => _currentHp;
    public float MaxHp => maxHp;
    public float Atk => atk;
    public float Ats => ats;
    public bool IsPlayer => isPlayer;
    public bool IsTower => isTower;
    public bool IsFinalBoss => _finalBoss;
    public EnemyType CurrentEnemyType => enemyType;

    public UnityEvent<float, float> OnHpChanged;
    public UnityEvent OnDeath;

    public enum EnemyType
    {
        Bug,
        Leader,
        Boss
    }

    private void Awake()
    {
        _currentHp = maxHp;
        _visual = GetComponent<SpriteRenderer>();
        if (_visual != null) _baseColor = _visual.color;
    }

    private void Start()
    {
        ApplyEnemyTypeStats();
        if (isPlayer)
        {
            _refuge = FindObjectOfType<Tower>();
        }
    }

    private void Update()
    {
        if (_visual != null) _visual.color = Time.time < _flashUntil ? new Color(1, .45f, .35f, 1) : _baseColor;
        IsRecovering = false;
        if (!isPlayer || _currentHp <= 0f || Time.timeScale <= 0) return;
        if (_refuge != null && Vector2.Distance(transform.position, _refuge.transform.position) <= 2f &&
            Time.time - _lastDamageAt >= 3 && _currentHp < maxHp)
        {
            IsRecovering = true;
            Heal(8f * Time.deltaTime);
        }
        if (!affectedByToxicGas) return;

        if (_toxicGasDrivenCentrally)
            ApplyToxicGasDamage(Time.deltaTime);
        else
            TickToxicGas(Time.deltaTime);
    }

    /// <summary> 按设计表套用怪物数值：Bug ATK100 ATS2 HP50 | Leader ATK50 ATS10 HP150 | Boss ATK3000 ATS1.5 HP500。 </summary>
    private void ApplyEnemyTypeStats()
    {
        if (isPlayer || isTower) return;
        if (_finalBoss) return;
        switch (enemyType)
        {
            case EnemyType.Bug:    atk = 100f;  ats = 2f;   maxHp = 50f;   break;  // DPS 200
            case EnemyType.Leader: atk = 50f;   ats = 10f;  maxHp = 150f;  break;  // DPS 500
            case EnemyType.Boss:   atk = 2000f; ats = 1f; maxHp = 500f;  break;  // DPS 2000
        }
        _currentHp = maxHp;
    }

    private void TickToxicGas(float deltaTime)
    {
        _toxicGasTimer += deltaTime;
        while (_toxicGasTimer >= toxicGasPeriodSeconds)
        {
            _toxicGasTimer -= toxicGasPeriodSeconds;
            _toxicGasPeriodsElapsed++;
        }
        ApplyToxicGasDamage(deltaTime);
    }

    private void ApplyToxicGasDamage(float deltaTime)
    {
        if (ToxicProtectionSeconds > 0) return;
        if (isPlayer && MapController.Instance != null && MapController.Instance.IsSafeAt(transform.position))
            return;

        TakeDamage(ToxicDamagePerSecond * deltaTime, null);
    }

    public void TakeDamage(float amount, GameObject source)
    {
        if (amount <= 0f || _hasDied) return;
        if (isPlayer && playerInvincibleForDebug) return;
        if (isPlayer && source != null)
        {
            if (Time.time < _contactProtectionUntil) return;
            _contactProtectionUntil = Time.time + .75f;
            GameAudio.Play("Hurt", .3f);
        }
        _lastDamageAt = Time.time;
        if (source != null) _flashUntil = Time.time + .15f;
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
        if (_hasDied || amount <= 0 || _currentHp >= maxHp) return;
        _currentHp = Mathf.Min(maxHp, _currentHp + amount);
        OnHpChanged?.Invoke(_currentHp, maxHp);
    }

    public void RestoreFullHealth()
    {
        _hasDied = false;
        _currentHp = maxHp;
        OnHpChanged?.Invoke(_currentHp, maxHp);
    }

    public void SetToxicGasPeriodsElapsed(int periods)
    {
        _toxicGasPeriodsElapsed = Mathf.Max(0, periods);
    }

    /// <summary> Call from MapController when it drives toxic gas so this system uses central periods. </summary>
    public void SetToxicGasDrivenCentrally(bool driven)
    {
        _toxicGasDrivenCentrally = driven;
    }

    public int GetToxicGasPeriodsElapsed() => _toxicGasPeriodsElapsed;

    public void SetStats(float newAtk, float newAts, float newMaxHp)
    {
        atk = Mathf.Max(0f, newAtk);
        ats = Mathf.Max(0f, newAts);
        float oldMax = maxHp;
        maxHp = Mathf.Max(1f, newMaxHp);
        _currentHp = maxHp * (_currentHp / Mathf.Max(0.001f, oldMax));
        OnHpChanged?.Invoke(_currentHp, maxHp);
    }

    public void GrantToxicProtection(float seconds)
    {
        if (isPlayer) _toxicProtectionUntil = Mathf.Max(_toxicProtectionUntil, Time.time + seconds);
    }

    public void ConfigureFinalBoss()
    {
        _finalBoss = true;
        enemyType = EnemyType.Boss;
        SetStats(350, .65f, 108000);
        RestoreFullHealth();
    }

    /// <summary> Set enemy type at runtime (e.g. from EnemySpawner). Applies stats immediately. </summary>
    public void SetEnemyType(EnemyType type)
    {
        enemyType = type;
        if (!isPlayer)
            ApplyEnemyTypeStats();
    }
}
