using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A parasite disguised as a normal resource drop. It cannot be collected and damages the
/// player at a fixed interval while their collider remains over it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DisguisedParasite : MonoBehaviour
{
    private static readonly HashSet<DisguisedParasite> ActiveParasites =
        new HashSet<DisguisedParasite>();

    [Header("Trap damage")]
    [Min(0f)] [SerializeField] private float damagePerTick = 10f;
    [Min(0.05f)] [SerializeField] private float damageInterval = 0.7f;

    [Header("Proximity warning")]
    [Min(0f)] [SerializeField] private float warningRadius = 3.5f;

    private HealthSystem _playerInside;
    private float _nextDamageTime;

    public void Configure(float newDamagePerTick, float newDamageInterval, float newWarningRadius)
    {
        damagePerTick = Mathf.Max(0f, newDamagePerTick);
        damageInterval = Mathf.Max(0.05f, newDamageInterval);
        warningRadius = Mathf.Max(0f, newWarningRadius);

        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnEnable()
    {
        ActiveParasites.Add(this);
        ParasiteWarningUI.EnsureExists();
    }

    private void OnDisable()
    {
        ActiveParasites.Remove(this);
        _playerInside = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HealthSystem playerHealth = FindPlayerHealth(other);
        if (playerHealth == null) return;

        _playerInside = playerHealth;
        _nextDamageTime = Time.time;
        TryDealDamage();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HealthSystem playerHealth = FindPlayerHealth(other);
        if (playerHealth == null) return;

        if (_playerInside != playerHealth)
        {
            _playerInside = playerHealth;
            _nextDamageTime = Time.time;
        }

        TryDealDamage();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HealthSystem playerHealth = FindPlayerHealth(other);
        if (playerHealth != null && playerHealth == _playerInside)
            _playerInside = null;
    }

    private void TryDealDamage()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f ||
            (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsPlaying)) return;
        if (_playerInside == null || _playerInside.CurrentHp <= 0f || Time.time < _nextDamageTime)
            return;

        if (!ReachableGround.HasLineOfSight(transform.position, _playerInside.transform.position, _playerInside.transform)) return;

        _playerInside.TakeDamage(damagePerTick, gameObject);
        _nextDamageTime = Time.time + damageInterval;
    }

    private static HealthSystem FindPlayerHealth(Collider2D other)
    {
        HealthSystem health = other.GetComponentInParent<HealthSystem>();
        return health != null && health.IsPlayer ? health : null;
    }

    public static float GetStrongestWarning(Vector2 playerPosition)
    {
        float strongestWarning = 0f;
        foreach (DisguisedParasite parasite in ActiveParasites)
        {
            if (parasite == null || parasite.warningRadius <= 0f) continue;

            float distance = Vector2.Distance(playerPosition, parasite.transform.position);
            float intensity = 1f - distance / parasite.warningRadius;
            strongestWarning = Mathf.Max(strongestWarning, Mathf.Clamp01(intensity));
        }
        return strongestWarning;
    }
}
