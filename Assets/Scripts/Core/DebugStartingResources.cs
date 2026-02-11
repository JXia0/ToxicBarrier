using UnityEngine;

/// <summary>
/// Debug utility: grant player starting resources on play.
/// Attach to any scene object (e.g. MapController). Remove/disable for release.
/// </summary>
public class DebugStartingResources : MonoBehaviour
{
    [SerializeField] private bool enable = true;
    [SerializeField] private PlayerInventory inventory;

    [Header("Grant on Start")]
    [SerializeField] private int grantWood = 30;

    private void Start()
    {
        if (!enable) return;

        if (inventory == null)
            inventory = FindObjectOfType<PlayerInventory>();

        if (inventory == null) return;

        if (grantWood > 0)
            inventory.Add(LootManager.ResourceType.Wood, grantWood);
    }
}

