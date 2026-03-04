using UnityEngine;

/// <summary>
/// 敌人攻击逻辑。开局默认攻击塔；若角色在仇恨范围内则优先攻击角色（可随时切换仇恨）。
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
    [Tooltip("角色在此范围内时，敌人优先攻击角色；否则默认攻击塔。")]
    [SerializeField] private float agroRange = 5f;

    [Header("Debug Target Line")]
    [Tooltip("是否用 LineRenderer 显示当期攻击目标（从怪物连到目标）。")]
    [SerializeField] private bool showTargetLine = true;
    [Tooltip("可选：指定用于绘制目标连线的 LineRenderer；留空则在运行时自动创建一个简单的。")]
    [SerializeField] private LineRenderer lineRenderer;

    private HealthSystem _health;
    private Rigidbody2D _rb;
    private Transform _target;
    private float _attackCooldown;

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _rb = GetComponent<Rigidbody2D>();

        if (showTargetLine)
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 0;
                lineRenderer.startWidth = 0.03f;
                lineRenderer.endWidth = 0.03f;
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = Color.red;
            }
        }
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

        UpdateTargetLine();
    }

    private void UpdateTargetLine()
    {
        if (!showTargetLine || lineRenderer == null)
            return;

        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            if (lineRenderer.enabled)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.enabled = false;
            }
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, _target.position);
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

        // 默认攻塔；若角色在仇恨范围内则优先攻击角色（每帧可重新判定目标）。
        Transform playerInRange = GetPlayerInAgroRange(pos);
        if (playerInRange != null)
        {
            _target = playerInRange;
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

    /// <summary> 若角色在仇恨范围内且存活，返回玩家 Transform；否则 null。 </summary>
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
