using System;
using UnityEngine;

/// <summary>
/// 塔 / 净化建筑。血量由 HealthSystem 统一管理；敌人仇恨优先级：玩家高于塔（GDD）。
/// 中央塔：可放置在地图中心，攻击范围 = Map 1 半径的一半；攻击逻辑由战斗/怪物 Tab 实现。
/// 等级 0–3 对应表格数值（ATK, ATS, HP）；DPS = ATK * ATS。
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class Tower : MonoBehaviour
{
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
    }

    [Header("Tower level stats (Level 0–3)")]
    [SerializeField] private TowerLevelStats[] levelStats = new TowerLevelStats[]
    {
        new TowerLevelStats { atk = 50f, ats = 2f, hp = 1000f },   // L0: DPS 100
        new TowerLevelStats { atk = 150f, ats = 4f, hp = 2500f },  // L1: DPS 600
        new TowerLevelStats { atk = 500f, ats = 8f, hp = 5000f },  // L2: DPS 4000
        new TowerLevelStats { atk = 750f, ats = 12f, hp = 8000f } // L3: DPS 9000
    };

    [SerializeField] private int currentLevel = 0;

    [Header("Upgrade cost (to reach Level 1 / 2 / 3)")]
    [SerializeField] private TowerUpgradeCost[] upgradeCosts = new TowerUpgradeCost[]
    {
        new TowerUpgradeCost { wood = 30, stone = 0, gel = 20, teeth = 0 },    // → Level 1
        new TowerUpgradeCost { wood = 10, stone = 30, gel = 40, teeth = 10 },  // → Level 2
        new TowerUpgradeCost { wood = 15, stone = 30, gel = 95, teeth = 50 }  // → Level 3
    };

    [Header("Auto upgrade (player near)")]
    [Tooltip("When player is within this distance and has enough resources, tower upgrades once per approach.")]
    [SerializeField] private float upgradeTriggerRadius = 2f;

    [Header("Tower sprites by level")]
    [Tooltip("Level 1 sprite (tower).")]
    [SerializeField] private Sprite level1Sprite;
    [Tooltip("Level 2 sprite (tower2).")]
    [SerializeField] private Sprite level2Sprite;
    [Tooltip("Level 3 sprite (tower3).")]
    [SerializeField] private Sprite level3Sprite;

    [Header("Map (central tower)")]
    [Tooltip("If true, position at map center on Start.")]
    [SerializeField] private bool placeAtMapCenter = true;
    [Tooltip("If true, attack range = Map 1 radius * 0.5 (read from MapController).")]
    [SerializeField] private bool attackRangeFromMap1Half = true;
    [Tooltip("Used when Attack Range From Map1 Half is false.")]
    [SerializeField] private float manualAttackRange = 2.5f;

    private HealthSystem _health;
    private SpriteRenderer _spriteRenderer;
    private bool _playerWasInUpgradeRange;

    /// <summary> 攻击范围（Map 1 半径一半或 manualAttackRange）. 供攻击逻辑使用。 </summary>
    public float AttackRange { get; private set; }

    /// <summary> 当前塔等级 (0–3). </summary>
    public int CurrentLevel => currentLevel;

    /// <summary> 当前等级数值（ATK, ATS, HP）. 供攻击逻辑使用。 </summary>
    public TowerLevelStats CurrentStats => GetLevelStats(currentLevel);

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
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

    private void Update()
    {
        TryAutoUpgradeWhenPlayerNear();
    }

    private void TryAutoUpgradeWhenPlayerNear()
    {
        if (currentLevel >= 3) return;
        if (MapController.Instance == null) return;

        Transform playerTransform = MapController.Instance.Player != null ? MapController.Instance.Player.transform : null;
        if (playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        bool inRange = dist <= upgradeTriggerRadius;

        if (!inRange)
        {
            _playerWasInUpgradeRange = false;
            return;
        }

        if (_playerWasInUpgradeRange) return;
        _playerWasInUpgradeRange = true;

        PlayerInventory inventory = MapController.Instance.Player.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        TowerUpgradeCost cost = GetUpgradeCostForNextLevel();
        if (inventory.GetCount(LootManager.ResourceType.Wood) < cost.wood ||
            inventory.GetCount(LootManager.ResourceType.Stone) < cost.stone ||
            inventory.GetCount(LootManager.ResourceType.Gel) < cost.gel ||
            inventory.GetCount(LootManager.ResourceType.Teeth) < cost.teeth)
            return;

        if (!inventory.TryConsume(LootManager.ResourceType.Wood, cost.wood)) return;
        if (!inventory.TryConsume(LootManager.ResourceType.Stone, cost.stone)) return;
        if (!inventory.TryConsume(LootManager.ResourceType.Gel, cost.gel)) return;
        if (!inventory.TryConsume(LootManager.ResourceType.Teeth, cost.teeth)) return;

        SetLevel(currentLevel + 1);
    }

    private void ApplyLevelStats()
    {
        TowerLevelStats s = GetLevelStats(currentLevel);
        if (_health != null)
            _health.SetStats(s.atk, s.ats, s.hp);
    }

    private void ApplyLevelSprite()
    {
        if (_spriteRenderer == null) return;

        Sprite target = null;
        switch (currentLevel)
        {
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
        int index = Mathf.Clamp(level, 0, 3);
        if (levelStats != null && levelStats.Length > index)
            return levelStats[index];
        return new TowerLevelStats { atk = 50f, ats = 2f, hp = 1000f };
    }

    /// <summary> 升级到指定等级并刷新 ATK/ATS/HP. </summary>
    public void SetLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 0, 3);
        ApplyLevelStats();
        ApplyLevelSprite();
    }

    /// <summary> 升到某一级所需资源。targetLevel 为 1、2、3；0 为初始无升级。 </summary>
    public TowerUpgradeCost GetUpgradeCostToLevel(int targetLevel)
    {
        int index = Mathf.Clamp(targetLevel, 1, 3) - 1;
        if (upgradeCosts != null && upgradeCosts.Length > index)
            return upgradeCosts[index];
        return new TowerUpgradeCost();
    }

    /// <summary> 下一级所需资源；若已是满级则返回全 0。 </summary>
    public TowerUpgradeCost GetUpgradeCostForNextLevel()
    {
        if (currentLevel >= 3) return new TowerUpgradeCost();
        return GetUpgradeCostToLevel(currentLevel + 1);
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
