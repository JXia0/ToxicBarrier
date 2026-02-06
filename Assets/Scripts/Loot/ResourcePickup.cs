using UnityEngine;

/// <summary>
/// Attach to Wood, Stone, Gel, and Teeth prefabs. Requires a Collider2D set as Trigger.
/// When the player enters the trigger, fires OnResourceCollected and destroys the pickup.
/// Subscribe to OnResourceCollected from your inventory or UI to add the resource.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ResourcePickup : MonoBehaviour
{
    [Header("Resource")]
    [SerializeField] private LootManager.ResourceType resourceType = LootManager.ResourceType.Gel;
    [SerializeField] private int amount = 1;

    [Header("Filter")]
    [Tooltip("Tag used to detect the player. Leave empty to use PlayerController presence.")]
    [SerializeField] private string playerTag = "Player";

    /// <summary> (ResourceType, amount). Subscribe from inventory/UI to add resources. </summary>
    public static System.Action<LootManager.ResourceType, int> OnResourceCollected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        OnResourceCollected?.Invoke(resourceType, amount);
        Destroy(gameObject);
    }

    private bool IsPlayer(Collider2D other)
    {
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            return true;
        if (other.GetComponent<PlayerController>() != null)
            return true;
        return false;
    }
}
