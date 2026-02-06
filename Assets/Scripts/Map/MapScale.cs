using UnityEngine;

/// <summary>
/// Map-tier movement speeds. Used by MapController to apply speed to the player.
/// </summary>
public class MapScale : MonoBehaviour
{
    [Header("Speed per tier (1 = fastest)")]
    [SerializeField] private float tier1Speed = 8f;
    [SerializeField] private float tier2Speed = 5f;
    [SerializeField] private float tier3Speed = 3f;
    [SerializeField] private float tier4Speed = 1f;

    /// <summary>
    /// Apply speed for the given tier (1–4) to the player via MapController.
    /// </summary>
    public void ApplyTierToPlayer(MapController map, int tier)
    {
        if (map == null) return;
        float speed = GetSpeedForTier(tier);
        map.SetPlayerMoveSpeed(speed);
    }

    public float GetSpeedForTier(int tier)
    {
        switch (Mathf.Clamp(tier, 1, 4))
        {
            case 1: return tier1Speed;
            case 2: return tier2Speed;
            case 3: return tier3Speed;
            case 4: return tier4Speed;
            default: return tier2Speed;
        }
    }
}
