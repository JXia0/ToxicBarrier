using UnityEngine;
using System.Collections.Generic;
using TMPro;

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
    private bool _collected;
    private static readonly HashSet<ResourcePickup> Available = new HashSet<ResourcePickup>();
    private TextMeshPro _countLabel;
    public int Amount => amount;

    private void OnEnable() { Available.Add(this); }
    private void OnDisable() { Available.Remove(this); }

    // Keep disguised parasites out of this registry by disabling their pickup component.
    public static bool TryMerge(LootManager.ResourceType type, Vector2 position, int quantity)
    {
        ResourcePickup nearest = null;
        float best = .85f * .85f;
        foreach (var pickup in Available)
        {
            if (pickup == null || !pickup.isActiveAndEnabled || pickup._collected || pickup.resourceType != type) continue;
            float distance = ((Vector2)pickup.transform.position - position).sqrMagnitude;
            if (distance > best || !ReachableGround.HasLineOfSight(position, pickup.transform.position)) continue;
            nearest = pickup; best = distance;
        }
        if (nearest == null || quantity <= 0) return false;
        nearest.amount += quantity;
        nearest.UpdateCountLabel();
        return true;
    }

    private void UpdateCountLabel()
    {
        if (_countLabel == null)
        {
            var label = new GameObject("Stack count");
            label.transform.SetParent(transform, false);
            Vector3 scale = transform.lossyScale;
            label.transform.localScale = new Vector3(1f / Mathf.Max(.001f, Mathf.Abs(scale.x)), 1f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
            label.transform.position = transform.position + new Vector3(.2f, -.2f, -.01f);
            _countLabel = label.AddComponent<TextMeshPro>();
            _countLabel.fontSize = 2.4f;
            _countLabel.alignment = TextAlignmentOptions.Center;
            _countLabel.color = new Color(1f, .94f, .75f);
            _countLabel.outlineWidth = .2f;
            _countLabel.outlineColor = new Color32(20, 25, 30, 255);
            var renderer = _countLabel.GetComponent<MeshRenderer>();
            var art = GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = art != null ? art.sortingLayerID : 0;
            renderer.sortingOrder = (art != null ? art.sortingOrder : 0) + 1;
        }
        _countLabel.SetText("{0}", amount);
    }

    private void OnTriggerStay2D(Collider2D other) { OnTriggerEnter2D(other); }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Disabled pickups are used as parasite disguises until their component is destroyed.
        if (!isActiveAndEnabled || _collected || !IsPlayer(other)) return;
        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsPlaying) return;
        if (!ReachableGround.HasLineOfSight(transform.position, other.transform.position, other.transform)) return;
        _collected = true;

        OnResourceCollected?.Invoke(resourceType, amount);
        GameAudio.Play("Pickup", .12f);
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
