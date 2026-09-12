using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Displays HP by changing the health bar RectTransform width.</summary>
public class HealthControl : MonoBehaviour
{
    [SerializeField] private RectTransform healthBar;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private HealthSystem health;

    private float _fullWidth;

    private void Awake()
    {
        if (healthBar != null) _fullWidth = healthBar.rect.width;
    }

    private void Start()
    {
        if (health == null) return;
        health.OnHpChanged.AddListener(UpdateHealthBar);
        UpdateHealthBar(health.CurrentHp, health.MaxHp);
    }

    private void OnDestroy()
    {
        if (health != null) health.OnHpChanged.RemoveListener(UpdateHealthBar);
    }

    public void UpdateHealthBar(float currentHp, float maxHp)
    {
        float percentage = maxHp > 0 ? Mathf.Clamp01(currentHp / maxHp) : 0;
        if (fillImage != null)
        {
            fillImage.fillAmount = percentage;
            fillImage.color = percentage <= 0.25f ? new Color(1f, 0.45f, 0.35f) : Color.white;
        }
        else if (healthBar != null)
            healthBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _fullWidth * percentage);
        if (valueText != null) valueText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }
}
