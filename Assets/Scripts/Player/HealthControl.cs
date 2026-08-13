using UnityEngine;

/// <summary>Displays HP by changing the health bar RectTransform width.</summary>
public class HealthControl : MonoBehaviour
{
    [SerializeField] private RectTransform healthBar;

    private float _fullWidth;

    private void Awake()
    {
        _fullWidth = healthBar.rect.width;
    }

    public void UpdateHealthBar(float currentHp, float maxHp)
    {
        float percentage = Mathf.Clamp01(currentHp / maxHp);
        healthBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _fullWidth * percentage);
    }
}
