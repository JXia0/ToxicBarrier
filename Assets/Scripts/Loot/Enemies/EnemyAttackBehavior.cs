using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人攻击逻辑。开局默认攻击塔；若角色在仇恨范围内则优先攻击角色（可随时切换仇恨）。
/// 靠近目标后按 ATS 造成 ATK 伤害。需要 Rigidbody2D 用于移动。
/// </summary>
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class EnemyAttackBehavior : MonoBehaviour
{
    private static readonly HashSet<EnemyAttackBehavior> ActiveEnemies =
        new HashSet<EnemyAttackBehavior>();
    private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving");
    private static readonly int AttackParameter = Animator.StringToHash("Attack");

    [Header("Combat")]
    [Tooltip("攻击范围（与目标距离小于此值时造成伤害）。")]
    [SerializeField] private float attackRange = 0.75f;
    [Tooltip("移动速度 = 玩家速度 × 1/2（比玩家慢一半）。无玩家时使用此值作为后备。")]
    [SerializeField] private float baseMoveSpeed = 2.5f;  // 5 × 1/2，与默认玩家速度匹配

    [Header("仇恨 (Agro)")]
    [Tooltip("角色在此范围内时，敌人优先攻击角色；否则默认攻击塔。")]
    [SerializeField] private float agroRange = 5f;

    [Header("Debug Target Line")]
    [Tooltip("敌人目标连线已禁用；保留字段仅为兼容已有 Prefab。")]
    [SerializeField] private bool showTargetLine = false;
    [Tooltip("敌人目标连线已禁用；保留字段仅为兼容已有 Prefab。")]
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private HealthSystem _health;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Collider2D[] _colliders;
    private Transform _target;
    private float _attackCooldown;
    private bool _playerCollisionIgnored;

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _colliders = GetComponents<Collider2D>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (_rb != null)
            _rb.freezeRotation = true;

        SetMovingAnimation(false);

        // 强制禁用敌人目标连线：不再创建也不再显示。
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }
    }

    private void OnEnable()
    {
        _playerCollisionIgnored = false;
        foreach (EnemyAttackBehavior other in ActiveEnemies)
            IgnoreCollisionsWith(other);
        ActiveEnemies.Add(this);
        IgnorePlayerCollisions();
    }

    private void Update()
    {
        if (!_playerCollisionIgnored)
            IgnorePlayerCollisions();

        if (_health == null || _health.CurrentHp <= 0f)
        {
            _target = null;
            if (_rb != null) _rb.velocity = Vector2.zero;
            SetMovingAnimation(false);
            return;
        }

        UpdateTarget();
        if (_target == null)
        {
            if (_rb != null) _rb.velocity = Vector2.zero;
            SetMovingAnimation(false);
            return;
        }

        Vector2 pos = transform.position;
        Vector2 targetPos = _target.position;
        float dist = Vector2.Distance(pos, targetPos);
        FaceTarget(targetPos);

        if (dist <= attackRange)
        {
            if (_rb != null) _rb.velocity = Vector2.zero;
            SetMovingAnimation(false);
            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f)
            {
                PlayAttackAnimation();
                DealDamage();
                _attackCooldown = 1f / Mathf.Max(0.001f, _health.Ats);
            }
        }
        else
        {
            Vector2 dir = (targetPos - pos).normalized;
            if (_rb != null)
                _rb.velocity = dir * GetMoveSpeed();
            SetMovingAnimation(true);
        }

        // 敌人目标连线功能已禁用（只保留塔的红线）。
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
        _playerCollisionIgnored = false;
        if (_rb != null)
            _rb.velocity = Vector2.zero;
        SetMovingAnimation(false);
    }

    private void IgnorePlayerCollisions()
    {
        PlayerController player = MapController.Instance != null ? MapController.Instance.Player : null;
        if (player == null)
            player = FindObjectOfType<PlayerController>();
        if (player == null) return;

        IgnoreCollisionsWith(player.GetComponents<Collider2D>());
        _playerCollisionIgnored = true;
    }

    private void IgnoreCollisionsWith(EnemyAttackBehavior other)
    {
        if (other == null || other == this) return;
        IgnoreCollisionsWith(other.GetComponents<Collider2D>());
    }

    private void IgnoreCollisionsWith(Collider2D[] otherColliders)
    {
        if (_colliders == null || otherColliders == null) return;

        foreach (Collider2D ownCollider in _colliders)
        {
            if (ownCollider == null) continue;
            foreach (Collider2D otherCollider in otherColliders)
            {
                if (otherCollider != null && otherCollider != ownCollider)
                    Physics2D.IgnoreCollision(ownCollider, otherCollider, true);
            }
        }
    }

    private void SetMovingAnimation(bool isMoving)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(IsMovingParameter, isMoving);
    }

    private void PlayAttackAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        animator.ResetTrigger(AttackParameter);
        animator.SetTrigger(AttackParameter);
    }

    private void FaceTarget(Vector2 targetPosition)
    {
        if (_spriteRenderer == null) return;

        float horizontalDirection = targetPosition.x - transform.position.x;
        if (Mathf.Abs(horizontalDirection) <= 0.01f) return;
        _spriteRenderer.flipX = horizontalDirection < 0f;
    }

    private void UpdateTargetLine()
    {
        // 已停用：保留方法签名避免影响外部引用。
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
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
