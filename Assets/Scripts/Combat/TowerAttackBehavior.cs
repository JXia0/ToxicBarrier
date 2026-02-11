using UnityEngine;

/// <summary>
/// 塔攻击逻辑。对攻击范围内的敌人按 ATS 频率造成 ATK 伤害。
/// 目标选择：范围内最近的敌人。需挂载在带 Tower + HealthSystem 的 GameObject 上。
/// </summary>
[RequireComponent(typeof(Tower))]
[RequireComponent(typeof(HealthSystem))]
public class TowerAttackBehavior : MonoBehaviour
{
    private Tower _tower;
    private HealthSystem _health;
    private float _attackCooldown;

    private void Awake()
    {
        _tower = GetComponent<Tower>();
        _health = GetComponent<HealthSystem>();
    }

    private void Update()
    {
        if (_tower == null || _health == null || _health.CurrentHp <= 0f) return;

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
    }
}
