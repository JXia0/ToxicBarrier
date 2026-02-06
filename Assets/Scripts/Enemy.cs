using UnityEngine;

/// <summary>
/// Enemy behaviour. Requires HealthSystem (set Enemy Type: Bug, Leader, or Boss).
/// On death, requests loot from LootManager if present.
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class Enemy : MonoBehaviour
{
    private HealthSystem _health;

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
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
        if (LootManager.Instance != null)
            LootManager.Instance.RollEnemyLoot(_health.EnemyType, transform.position);
    }
}
