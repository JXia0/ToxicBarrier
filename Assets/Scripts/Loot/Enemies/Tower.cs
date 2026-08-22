using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 塔 / 净化建筑。血量由 HealthSystem 统一管理；敌人仇恨优先级：玩家高于塔（GDD）。
/// 中央塔：可放置在地图中心，攻击范围 = Map 1 半径的一半；攻击逻辑由战斗/怪物 Tab 实现。
/// 等级 0–3 对应表格数值（ATK, ATS, HP）；DPS = ATK * ATS。
/// </summary>
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(CircleCollider2D))]
public class Tower : MonoBehaviour
{
    public const int InitialLevel = 0;
    public const int MaxLevel = 3;

    private const int FirstUpgradeLevel = InitialLevel + 1;

    private static readonly LootManager.ResourceType[] UpgradeResourceTypes =
    {
        LootManager.ResourceType.Wood,
        LootManager.ResourceType.Stone,
        LootManager.ResourceType.Gel,
        LootManager.ResourceType.Teeth
    };

    [Serializable]
    public struct TowerLevelStats
    {
        public float atk;
        public float ats;
        public float hp;
        public float Dps => atk * ats;
    }

    [Serializable]
    public struct TowerUpgradeCost
    {
        public int wood;
        public int stone;
        public int gel;
        public int teeth;

        public int GetAmount(LootManager.ResourceType resourceType)
        {
            switch (resourceType)
            {
                case LootManager.ResourceType.Wood: return wood;
                case LootManager.ResourceType.Stone: return stone;
                case LootManager.ResourceType.Gel: return gel;
                case LootManager.ResourceType.Teeth: return teeth;
                default: return 0;
            }
        }
    }

    [Header("Tower level stats (Level 0–3)")]
    [SerializeField] private TowerLevelStats[] levelStats = new TowerLevelStats[]
    {
        new TowerLevelStats { atk = 50f, ats = 2f, hp = 1000f },   // L0: DPS 100
        new TowerLevelStats { atk = 150f, ats = 4f, hp = 2500f },  // L1: DPS 600
        new TowerLevelStats { atk = 500f, ats = 8f, hp = 5000f },  // L2: DPS 4000
        new TowerLevelStats { atk = 750f, ats = 12f, hp = 8000f } // L3: DPS 9000
    };

    [SerializeField] private int currentLevel = InitialLevel;

    [Header("Upgrade cost (to reach Level 1 / 2 / 3)")]
    [Tooltip("Array index 0/1/2 is the cost to reach Tower Level 1/2/3.")]
    [SerializeField] private TowerUpgradeCost[] upgradeCosts = new TowerUpgradeCost[]
    {
        new TowerUpgradeCost { wood = 30, stone = 0, gel = 20, teeth = 0 },    // → Level 1
        new TowerUpgradeCost { wood = 10, stone = 30, gel = 40, teeth = 10 },  // → Level 2
        new TowerUpgradeCost { wood = 15, stone = 30, gel = 95, teeth = 50 }  // → Level 3
    };

    [Header("Upgrade interaction")]
    [Tooltip("World-space distance that shows the upgrade button.")]
    [SerializeField] private float upgradeTriggerRadius = 2f;

    [Header("Tower sprites by level")]
    [Tooltip("Level 1 sprite (tower).")]
    [SerializeField] private Sprite level1Sprite;
    [Tooltip("Level 2 sprite (tower2).")]
    [SerializeField] private Sprite level2Sprite;
    [Tooltip("Level 3 sprite (tower3).")]
    [SerializeField] private Sprite level3Sprite;

    [Header("Upgrade presentation")]
    [SerializeField, Min(1f)] private float upgradePulseScale = 1.2f;
    [SerializeField, Min(0.05f)] private float upgradePulseDuration = 0.28f;
    [SerializeField, Min(0f)] private float cameraShakeDuration = 0.16f;
    [SerializeField, Min(0f)] private float cameraShakeMagnitude = 0.18f;

    [Header("Map (central tower)")]
    [Tooltip("If true, position at map center on Start.")]
    [SerializeField] private bool placeAtMapCenter = true;
    [Tooltip("If true, attack range = Map 1 radius * 0.5 (read from MapController).")]
    [SerializeField] private bool attackRangeFromMap1Half = true;
    [Tooltip("Used when Attack Range From Map1 Half is false.")]
    [SerializeField] private float manualAttackRange = 2.5f;

    private HealthSystem _health;
    private SpriteRenderer _spriteRenderer;
    private CircleCollider2D _upgradeTrigger;
    private bool _playerInUpgradeRange;
    private Vector3 _restingScale;
    private Coroutine _upgradePulseRoutine;

    public event Action<bool> OnUpgradeRangeChanged;
    public event Action OnLevelChanged;

    /// <summary> 攻击范围（Map 1 半径一半或 manualAttackRange）. 供攻击逻辑使用。 </summary>
    public float AttackRange { get; private set; }

    /// <summary> 当前塔等级 (0–3). </summary>
    public int CurrentLevel => currentLevel;
    public int NextLevel => Mathf.Min(currentLevel + 1, MaxLevel);
    public bool IsMaxLevel => currentLevel >= MaxLevel;
    public bool IsPlayerInUpgradeRange => _playerInUpgradeRange;

    /// <summary> 当前等级数值（ATK, ATS, HP）. 供攻击逻辑使用。 </summary>
    public TowerLevelStats CurrentStats => GetLevelStats(currentLevel);

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _restingScale = transform.localScale;

        _upgradeTrigger = GetComponent<CircleCollider2D>();
        if (_upgradeTrigger == null)
            _upgradeTrigger = gameObject.AddComponent<CircleCollider2D>();

        _upgradeTrigger.isTrigger = true;
        _upgradeTrigger.radius = upgradeTriggerRadius / Mathf.Abs(transform.lossyScale.x);
    }

    private void Start()
    {
        ApplyLevelStats();
        ApplyLevelSprite();

        if (MapController.Instance != null)
        {
            if (placeAtMapCenter)
            {
                Vector2 center = MapController.Instance.GetMapCenter();
                transform.position = new Vector3(center.x, center.y, transform.position.z);
            }
            if (attackRangeFromMap1Half)
                AttackRange = MapController.Instance.GetMap1Radius() * 0.5f;
        }
        if (!attackRangeFromMap1Half || AttackRange <= 0f)
            AttackRange = manualAttackRange;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
            SetPlayerInUpgradeRange(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
            SetPlayerInUpgradeRange(false);
    }

    private void SetPlayerInUpgradeRange(bool inRange)
    {
        if (_playerInUpgradeRange == inRange) return;
        _playerInUpgradeRange = inRange;
        OnUpgradeRangeChanged?.Invoke(inRange);
    }

    /// <summary>Checks and consumes materials only when the player presses the upgrade button.</summary>
    public bool TryUpgrade(PlayerInventory inventory)
    {
        if (!CanUpgrade(inventory))
            return false;

        int targetLevel = NextLevel;
        TowerUpgradeCost cost = GetUpgradeCostForNextLevel();
        if (!TryPayUpgradeCost(inventory, cost))
            return false;

        SetLevel(targetLevel);
        return true;
    }

    /// <summary>Checks range, level, and materials without consuming anything.</summary>
    public bool CanUpgrade(PlayerInventory inventory)
    {
        if (!_playerInUpgradeRange || IsMaxLevel || inventory == null)
            return false;

        TowerUpgradeCost cost = GetUpgradeCostForNextLevel();
        foreach (LootManager.ResourceType resourceType in UpgradeResourceTypes)
        {
            if (inventory.GetCount(resourceType) < cost.GetAmount(resourceType))
                return false;
        }

        return true;
    }

    private static bool TryPayUpgradeCost(PlayerInventory inventory, TowerUpgradeCost cost)
    {
        foreach (LootManager.ResourceType resourceType in UpgradeResourceTypes)
        {
            if (inventory.GetCount(resourceType) < cost.GetAmount(resourceType))
                return false;
        }

        foreach (LootManager.ResourceType resourceType in UpgradeResourceTypes)
            inventory.TryConsume(resourceType, cost.GetAmount(resourceType));

        return true;
    }

    private void ApplyLevelStats()
    {
        TowerLevelStats s = GetLevelStats(currentLevel);
        if (_health != null)
        {
            _health.SetStats(s.atk, s.ats, s.hp);
            _health.RestoreFullHealth();
        }

        if (MapController.Instance != null && MapController.Instance.PlayerHealth != null)
            MapController.Instance.PlayerHealth.RestoreFullHealth();
    }

    private void ApplyLevelSprite()
    {
        if (_spriteRenderer == null) return;

        Sprite target = null;
        switch (currentLevel)
        {
            case 0:
            case 1:
                target = level1Sprite;
                break;
            case 2:
                target = level2Sprite;
                break;
            case 3:
                target = level3Sprite;
                break;
        }

        if (target != null)
            _spriteRenderer.sprite = target;
    }

    private TowerLevelStats GetLevelStats(int level)
    {
        int index = Mathf.Clamp(level, InitialLevel, MaxLevel);
        if (levelStats != null && levelStats.Length > index)
            return levelStats[index];
        return new TowerLevelStats { atk = 50f, ats = 2f, hp = 1000f };
    }

    /// <summary> 升级到指定等级并刷新 ATK/ATS/HP. </summary>
    public void SetLevel(int level)
    {
        int nextLevel = Mathf.Clamp(level, InitialLevel, MaxLevel);
        if (nextLevel == currentLevel) return;

        currentLevel = nextLevel;
        ApplyLevelStats();
        ApplyLevelSprite();
        PlayUpgradeFeedback();
        OnLevelChanged?.Invoke();
    }

    private void PlayUpgradeFeedback()
    {
        if (_upgradePulseRoutine != null)
            StopCoroutine(_upgradePulseRoutine);

        transform.localScale = _restingScale;
        _upgradePulseRoutine = StartCoroutine(UpgradePulse());
        CameraShake2D.Play(UnityEngine.Camera.main, cameraShakeDuration, cameraShakeMagnitude);
    }

    private IEnumerator UpgradePulse()
    {
        float halfDuration = Mathf.Max(0.025f, upgradePulseDuration * 0.5f);
        Vector3 peakScale = new Vector3(
            _restingScale.x * upgradePulseScale,
            _restingScale.y * upgradePulseScale,
            _restingScale.z);

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / halfDuration);
            transform.localScale = Vector3.LerpUnclamped(_restingScale, peakScale, t);
            yield return null;
        }

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / halfDuration);
            transform.localScale = Vector3.LerpUnclamped(peakScale, _restingScale, t);
            yield return null;
        }

        transform.localScale = _restingScale;
        _upgradePulseRoutine = null;
    }

    /// <summary>升级费用的统一查询入口。targetLevel 1/2/3 分别读取 upgradeCosts 0/1/2。</summary>
    public TowerUpgradeCost GetUpgradeCostToLevel(int targetLevel)
    {
        int clampedTargetLevel = Mathf.Clamp(targetLevel, FirstUpgradeLevel, MaxLevel);
        int index = clampedTargetLevel - FirstUpgradeLevel;
        if (upgradeCosts != null && upgradeCosts.Length > index)
            return upgradeCosts[index];
        return default;
    }

    /// <summary> 下一级所需资源；若已是满级则返回全 0。 </summary>
    public TowerUpgradeCost GetUpgradeCostForNextLevel()
    {
        return IsMaxLevel ? default : GetUpgradeCostToLevel(NextLevel);
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnDeath.AddListener(OnDeath);
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnDeath.RemoveListener(OnDeath);
        if (_upgradePulseRoutine != null)
        {
            StopCoroutine(_upgradePulseRoutine);
            _upgradePulseRoutine = null;
        }
        transform.localScale = _restingScale;
        SetPlayerInUpgradeRange(false);
    }

    private void OnDeath()
    {
        // 塔被摧毁：可在此挂载特效、音效或通知 MapController 等
        gameObject.SetActive(false);
    }

    public HealthSystem Health => _health;

    private void OnDrawGizmosSelected()
    {
        Vector2 center = transform.position;
        float r = Application.isPlaying ? AttackRange : manualAttackRange;
        if (r > 0f)
        {
            const int segments = 24;
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Gizmos.DrawLine(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r);
            }
        }
        if (upgradeTriggerRadius > 0f)
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Gizmos.DrawLine(center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * upgradeTriggerRadius, center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * upgradeTriggerRadius);
            }
        }
    }
}

/// <summary>Short render-time camera offset that does not fight Cinemachine's transform updates.</summary>
[DisallowMultipleComponent]
internal sealed class CameraShake2D : MonoBehaviour
{
    private float _endTime;
    private float _duration;
    private float _magnitude;
    private Vector3 _appliedOffset;

    public static void Play(UnityEngine.Camera targetCamera, float duration, float magnitude)
    {
        if (targetCamera == null || duration <= 0f || magnitude <= 0f) return;

        CameraShake2D shake = targetCamera.GetComponent<CameraShake2D>();
        if (shake == null)
            shake = targetCamera.gameObject.AddComponent<CameraShake2D>();

        shake._duration = duration;
        shake._magnitude = magnitude;
        shake._endTime = Time.unscaledTime + duration;
    }

    private void OnPreCull()
    {
        RestoreCameraPosition();
        if (Time.unscaledTime >= _endTime) return;

        float strength = Mathf.Clamp01((_endTime - Time.unscaledTime) / Mathf.Max(0.001f, _duration));
        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * (_magnitude * strength);
        _appliedOffset = new Vector3(randomOffset.x, randomOffset.y, 0f);
        transform.position += _appliedOffset;
    }

    private void OnPostRender()
    {
        RestoreCameraPosition();
    }

    private void OnDisable()
    {
        RestoreCameraPosition();
    }

    private void RestoreCameraPosition()
    {
        if (_appliedOffset == Vector3.zero) return;
        transform.position -= _appliedOffset;
        _appliedOffset = Vector3.zero;
    }
}
