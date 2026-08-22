using UnityEngine;

/// <summary>
/// 塔攻击逻辑。对攻击范围内的敌人按 ATS 频率造成 ATK 伤害。
/// 攻击时用 LineRenderer 显示激光示意。需挂载在带 Tower + HealthSystem 的 GameObject 上。
/// </summary>
[RequireComponent(typeof(Tower))]
[RequireComponent(typeof(HealthSystem))]
public class TowerAttackBehavior : MonoBehaviour
{
    [Header("Attack Flash Line (Black)")]
    [SerializeField] private float attackLineDisplayDuration = 0.08f;
    [SerializeField] private float attackLineStartWidth = 0.15f;
    [SerializeField] private float attackLineEndWidth = 0.05f;
    [SerializeField] private Color attackLineColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField] private int attackLineSortingOrder = 5;
    [SerializeField] private LineRenderer attackLineRenderer;

    [Header("Lock Target Line (Red)")]
    [SerializeField] private bool showLockTargetLine = true;
    [SerializeField] private float lockLineStartWidth = 0.03f;
    [SerializeField] private float lockLineEndWidth = 0.015f;
    [SerializeField] private Color lockLineColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private int lockLineSortingOrder = 10;
    [SerializeField] private LineRenderer lockLineRenderer;

    private Tower _tower;
    private HealthSystem _health;
    private float _attackCooldown;
    private float _attackLineHideTime;

    [Header("Targeting")]
    [Tooltip("若为 true，塔会锁定一个目标敌人，直到其死亡或离开攻击范围才重新选择。")]
    [SerializeField] private bool lockTarget = true;

    private Enemy _currentTarget;

    private void Awake()
    {
        _tower = GetComponent<Tower>();
        _health = GetComponent<HealthSystem>();

        if (attackLineRenderer == null)
            attackLineRenderer = GetComponent<LineRenderer>();
        if (attackLineRenderer == null)
            attackLineRenderer = gameObject.AddComponent<LineRenderer>();

        if (showLockTargetLine && lockLineRenderer == null)
        {
            GameObject lockLineObject = new GameObject("TowerLockTargetLine");
            lockLineObject.transform.SetParent(transform, false);
            lockLineRenderer = lockLineObject.AddComponent<LineRenderer>();
        }
        else if (lockLineRenderer == attackLineRenderer)
        {
            // 防止两条线意外引用同一个 LineRenderer，导致颜色/宽度完全一致。
            GameObject lockLineObject = new GameObject("TowerLockTargetLine");
            lockLineObject.transform.SetParent(transform, false);
            lockLineRenderer = lockLineObject.AddComponent<LineRenderer>();
        }

        Shader lineShader = Shader.Find("Sprites/Default");
        SetupLineRenderer(attackLineRenderer, attackLineStartWidth, attackLineEndWidth, attackLineColor, lineShader, attackLineSortingOrder);
        if (lockLineRenderer != null)
            SetupLineRenderer(lockLineRenderer, lockLineStartWidth, lockLineEndWidth, lockLineColor, lineShader, lockLineSortingOrder);
    }

    private void Update()
    {
        if (_tower == null || _health == null || _health.CurrentHp <= 0f)
        {
            DisableAllLines();
            return;
        }

        Enemy target = GetTargetEnemy();
        UpdateLockTargetLine(target);
        HideAttackLineIfNeeded();

        _attackCooldown -= Time.deltaTime;
        if (_attackCooldown > 0f) return;
        if (target == null) return;

        DealDamage(target);
        ShowAttackFlashLine(target);
        _attackCooldown = 1f / Mathf.Max(0.001f, _health.Ats);
    }

    private Enemy GetTargetEnemy()
    {
        float range = _tower.AttackRange;
        if (range <= 0f) return null;

        float rangeSqr = range * range;

        // 若启用锁定并且当前目标仍在范围内且存活，则继续攻击同一目标
        if (lockTarget && IsEnemyValidInRange(_currentTarget, rangeSqr))
            return _currentTarget;

        Vector2 pos = transform.position;
        Enemy nearest = null;
        float nearestSqr = rangeSqr;

        foreach (Enemy e in FindObjectsOfType<Enemy>())
        {
            if (!IsEnemyValidInRange(e, rangeSqr)) continue;

            float sqrDist = ((Vector2)e.transform.position - pos).sqrMagnitude;
            if (sqrDist <= nearestSqr)
            {
                nearestSqr = sqrDist;
                nearest = e;
            }
        }

        _currentTarget = lockTarget ? nearest : null;
        return nearest;
    }

    private bool IsEnemyValidInRange(Enemy e, float rangeSqr)
    {
        if (e == null || !e.gameObject.activeInHierarchy) return false;

        HealthSystem h = e.GetComponent<HealthSystem>();
        if (h == null || h.CurrentHp <= 0f) return false;

        float sqrDist = ((Vector2)e.transform.position - (Vector2)transform.position).sqrMagnitude;
        return sqrDist <= rangeSqr;
    }

    private void DealDamage(Enemy target)
    {
        if (target == null) return;
        HealthSystem targetHealth = target.GetComponent<HealthSystem>();
        if (targetHealth == null || targetHealth.CurrentHp <= 0f) return;

        float damage = _health.Atk;
        targetHealth.TakeDamage(damage, gameObject);
    }

    private void SetupLineRenderer(LineRenderer lr, float startWidth, float endWidth, Color color, Shader lineShader, int sortingOrder)
    {
        if (lr == null) return;
        lr.enabled = false;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        if (lineShader != null)
            lr.material = new Material(lineShader);
        if (lr.material != null)
            lr.material.color = Color.white;
        lr.sortingLayerName = SortingLayerNames.Characters;
        lr.sortingOrder = sortingOrder;
        lr.startColor = color;
        lr.endColor = new Color(color.r, color.g, color.b, 0.4f);
    }

    private void ShowAttackFlashLine(Enemy target)
    {
        if (attackLineRenderer == null || target == null) return;
        attackLineRenderer.SetPosition(0, transform.position);
        attackLineRenderer.SetPosition(1, target.transform.position);
        attackLineRenderer.enabled = true;
        _attackLineHideTime = Time.time + attackLineDisplayDuration;
    }

    private void HideAttackLineIfNeeded()
    {
        if (attackLineRenderer != null && attackLineRenderer.enabled && Time.time >= _attackLineHideTime)
            attackLineRenderer.enabled = false;
    }

    private void UpdateLockTargetLine(Enemy target)
    {
        if (!showLockTargetLine || lockLineRenderer == null) return;

        bool hasTarget = target != null;
        lockLineRenderer.enabled = hasTarget;
        if (!hasTarget) return;

        lockLineRenderer.SetPosition(0, transform.position);
        lockLineRenderer.SetPosition(1, target.transform.position);
    }

    private void DisableAllLines()
    {
        if (attackLineRenderer != null)
            attackLineRenderer.enabled = false;
        if (lockLineRenderer != null)
            lockLineRenderer.enabled = false;
    }
}
