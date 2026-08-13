using UnityEngine;

/// <summary>
/// Enemy behaviour. Requires HealthSystem (set Enemy Type: Bug, Leader, or Boss).
/// On death, requests loot from LootManager (via MapController or singleton).
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class Enemy : MonoBehaviour
{
    private HealthSystem _health;
    private bool _isParasiteHost;

    public bool IsParasiteHost => _isParasiteHost;

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
    }

    private void Start()
    {
        if (LootManager.Instance != null)
            _isParasiteHost = LootManager.Instance.RollParasiteInfection();
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
        Vector2 deathPosition = transform.position;
        HealthSystem.EnemyType enemyType = _health.CurrentEnemyType;
        if (LootManager.Instance != null)
        {
            LootManager.Instance.RollEnemyLoot(enemyType, deathPosition);
            if (_isParasiteHost)
                LootManager.Instance.SpawnDisguisedParasite(enemyType, deathPosition);
        }
        Destroy(gameObject);
    }
}
