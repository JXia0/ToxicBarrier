using UnityEngine;

/// <summary>
/// 塔 / 净化建筑。血量由 HealthSystem 统一管理；敌人仇恨优先级：玩家高于塔（GDD）。
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class Tower : MonoBehaviour
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
        // 塔被摧毁：可在此挂载特效、音效或通知 MapController 等
        gameObject.SetActive(false);
    }

    public HealthSystem Health => _health;
}
