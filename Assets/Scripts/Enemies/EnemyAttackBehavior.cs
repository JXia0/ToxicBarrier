using UnityEngine;

/// <summary>
/// 敌人攻击逻辑。默认朝向塔移动；若玩家在仇恨范围内则被吸引转向玩家。
/// 靠近目标后按 ATS 造成 ATK 伤害。需要 Rigidbody2D 用于移动。
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAttackBehavior : MonoBehaviour
{
    [Header("Combat")]
    [Tooltip("攻击范围（与目标距离小于此值时造成伤害）。")]
    [SerializeField] private float attackRange = 1.5f;
    [Tooltip("移动速度 = 玩家速度 × 1/2（比玩家慢一半）。无玩家时使用此值作为后备。")]
    [SerializeField] private float baseMoveSpeed = 2.5f;  // 5 × 1/2，与默认玩家速度匹配

    [Header("仇恨 (Agro)")]
    [Tooltip("若玩家在此范围内，敌人会转向攻击玩家；否则默认攻击塔。")]
    [SerializeField] private float agroRange = 5f;

    private HealthSystem _health;
    private Rigidbody2D _rb;
    private Transform _target;
    private float _attackCooldown;

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (_health == null || _health.CurrentHp <= 0f)
        {
            _target = null;
            if (_rb != null) _rb.velocity = Vector2.zero;
            return;
        }

        UpdateTarget();
        if (_target == null)
        {
            if (_rb != null) _rb.velocity = Vector2.zero;
            return;
        }

        Vector2 pos = transform.position;
        Vector2 targetPos = _target.position;
        float dist = Vector2.Distance(pos, targetPos);

        if (dist <= attackRange)
        {
            if (_rb != null) _rb.velocity = Vector2.zero;
            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f)
            {
                DealDamage();
                _attackCooldown = 1f / Mathf.Max(0.001f, _health.Ats);
            }
        }
        else
        {
            Vector2 dir = (targetPos - pos).normalized;
            if (_rb != null)
                _rb.velocity = dir * GetMoveSpeed();
        }
    }

    /// <summary> 怪物移速 = 玩家移速 × 1/2；无玩家时用 baseMoveSpeed。 </summary>
    private float GetMoveSpeed()
    {
        if (MapController.Instance != null && MapController.Instance.Player != null)
        {
            float playerSpeed = MapController.Instance.Player.GetMoveSpeed();
            return Mathf.Max(0f, playerSpeed * 0.5f);
        }
        return baseMoveSpeed;
    }

    private void UpdateTarget()
    {
        Vector2 pos = transform.position;

        // 默认目标：塔。若玩家在仇恨范围内，则被吸引转向玩家。
        Transform playerTransform = GetPlayerInAgroRange(pos);
        if (playerTransform != null)
        {
            _target = playerTransform;
            return;
        }

        Tower tower = FindClosestTower();
        if (tower != null && tower.Health != null && tower.Health.CurrentHp > 0f)
        {
            _target = tower.transform;
            return;
        }

        _target = null;
    }

    /// <summary> 若玩家在仇恨范围内且存活，返回玩家 Transform；否则 null。 </summary>
    private Transform GetPlayerInAgroRange(Vector2 pos)
    {
        if (MapController.Instance == null || MapController.Instance.PlayerHealth == null) return null;

        HealthSystem ph = MapController.Instance.PlayerHealth;
        if (!ph.IsPlayer || ph.CurrentHp <= 0f) return null;

        float sqrDist = ((Vector2)ph.transform.position - pos).sqrMagnitude;
        if (sqrDist <= agroRange * agroRange)
            return ph.transform;

        return null;
    }

    private Tower FindClosestTower()
    {
        Tower[] towers = FindObjectsOfType<Tower>();
        Tower closest = null;
        float closestSqr = float.MaxValue;
        Vector2 pos = transform.position;

        foreach (Tower t in towers)
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (t.Health == null || t.Health.CurrentHp <= 0f) continue;

            float sqrDist = ((Vector2)t.transform.position - pos).sqrMagnitude;
            if (sqrDist < closestSqr)
            {
                closestSqr = sqrDist;
                closest = t;
            }
        }

        return closest;
    }

    private void DealDamage()
    {
        if (_target == null) return;

        HealthSystem targetHealth = _target.GetComponent<HealthSystem>();
        if (targetHealth == null || targetHealth.CurrentHp <= 0f) return;

        float damage = _health.Atk;
        targetHealth.TakeDamage(damage, gameObject);
    }
}
