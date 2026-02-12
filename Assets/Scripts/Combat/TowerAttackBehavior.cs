using UnityEngine;

/// <summary>
/// 塔攻击逻辑。对攻击范围内的敌人按 ATS 频率造成 ATK 伤害。
/// 攻击时用 LineRenderer 显示激光示意。需挂载在带 Tower + HealthSystem 的 GameObject 上。
/// </summary>
[RequireComponent(typeof(Tower))]
[RequireComponent(typeof(HealthSystem))]
public class TowerAttackBehavior : MonoBehaviour
{
    [Header("Laser (LineRenderer)")]
    [SerializeField] private float laserDisplayDuration = 0.08f;
    [SerializeField] private float laserStartWidth = 0.15f;
    [SerializeField] private float laserEndWidth = 0.05f;
    [SerializeField] private Color laserColor = new Color(1f, 0.3f, 0.2f, 0.9f);

    private Tower _tower;
    private HealthSystem _health;
    private float _attackCooldown;
    private LineRenderer _lineRenderer;
    private float _laserHideTime;

    private void Awake()
    {
        _tower = GetComponent<Tower>();
        _health = GetComponent<HealthSystem>();

        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
            _lineRenderer = gameObject.AddComponent<LineRenderer>();

        _lineRenderer.enabled = false;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 2;
        _lineRenderer.startWidth = laserStartWidth;
        _lineRenderer.endWidth = laserEndWidth;
        _lineRenderer.material = _lineRenderer.material != null ? _lineRenderer.material : new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor = laserColor;
        _lineRenderer.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0.4f);
    }

    private void Update()
    {
        if (_tower == null || _health == null || _health.CurrentHp <= 0f) return;

        if (_lineRenderer != null && _lineRenderer.enabled && Time.time >= _laserHideTime)
            _lineRenderer.enabled = false;

        _attackCooldown -= Time.deltaTime;
        if (_attackCooldown > 0f) return;

        Enemy target = FindNearestEnemyInRange();
        if (target == null) return;

        DealDamage(target);
        _attackCooldown = 1f / Mathf.Max(0.001f, _health.Ats);
    }

    private Enemy FindNearestEnemyInRange()
    {
        float range = _tower.AttackRange;
        if (range <= 0f) return null;

        Vector2 pos = transform.position;
        Enemy nearest = null;
        float nearestSqr = range * range;

        foreach (Enemy e in FindObjectsOfType<Enemy>())
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;

            HealthSystem h = e.GetComponent<HealthSystem>();
            if (h == null || h.CurrentHp <= 0f) continue;

            float sqrDist = ((Vector2)e.transform.position - pos).sqrMagnitude;
            if (sqrDist <= nearestSqr)
            {
                nearestSqr = sqrDist;
                nearest = e;
            }
        }

        return nearest;
    }

    private void DealDamage(Enemy target)
    {
        HealthSystem targetHealth = target.GetComponent<HealthSystem>();
        if (targetHealth == null || targetHealth.CurrentHp <= 0f) return;

        float damage = _health.Atk;
        targetHealth.TakeDamage(damage, gameObject);

        if (_lineRenderer != null)
        {
            Vector3 start = transform.position;
            Vector3 end = target.transform.position;
            _lineRenderer.SetPosition(0, start);
            _lineRenderer.SetPosition(1, end);
            _lineRenderer.enabled = true;
            _laserHideTime = Time.time + laserDisplayDuration;
        }
    }
}
