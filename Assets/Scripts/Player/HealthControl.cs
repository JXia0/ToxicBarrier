using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the scene-authored player health bar synchronized with HealthSystem.
/// HealthSystem remains the only source of truth for current and maximum HP.
/// The HealthCanvas and fill Image are assigned as serialized scene references.
/// </summary>
public class HealthControl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthSystem playerHealth;
    [SerializeField] private Image healthBarFill;

    public HealthSystem PlayerHealth => playerHealth;
    public Image HealthBarFill => healthBarFill;

    private bool _isSubscribed;

    private void OnEnable()
    {
        BindPlayerHealth();
    }

    private void Start()
    {
        // Start is a second initialization point in case this UI enabled before
        // another scene object's Awake assigned its initial health value.
        BindPlayerHealth();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindPlayerHealth();
    }

    public void Refresh()
    {
        if (playerHealth == null) return;
        SetHealth(playerHealth.CurrentHp, playerHealth.MaxHp);
    }

    /// <summary>UnityEvent-compatible entry point for displaying health.</summary>
    public void SetHealth(float currentHp, float maxHp)
    {
        if (healthBarFill == null) return;

        float percentage = maxHp > 0f ? currentHp / maxHp : 0f;
        healthBarFill.fillAmount = Mathf.Clamp01(percentage);
    }

    private void BindPlayerHealth()
    {
        if (playerHealth == null)
            playerHealth = FindPlayerHealth();

        if (playerHealth == null)
        {
            Debug.LogWarning("HealthControl could not find the player's HealthSystem.", this);
            return;
        }

        if (!_isSubscribed)
        {
            playerHealth.OnHpChanged.AddListener(SetHealth);
            _isSubscribed = true;
        }

        Refresh();
    }

    private void UnbindPlayerHealth()
    {
        if (!_isSubscribed || playerHealth == null) return;

        playerHealth.OnHpChanged.RemoveListener(SetHealth);
        _isSubscribed = false;
    }

    private static HealthSystem FindPlayerHealth()
    {
        if (MapController.Instance != null && MapController.Instance.PlayerHealth != null)
            return MapController.Instance.PlayerHealth;

        HealthSystem[] healthSystems = FindObjectsOfType<HealthSystem>();
        foreach (HealthSystem health in healthSystems)
        {
            if (health != null && health.IsPlayer)
                return health;
        }

        return null;
    }
}
