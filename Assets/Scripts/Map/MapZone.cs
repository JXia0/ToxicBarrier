using UnityEngine;

/// <summary>
/// Attach to each of the three circle objects representing Map 1, Map 2, and Map 3.
/// Map 2/3 move above the global ToxicFog Tilemap when unlocked by Tower level.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MapZone : MonoBehaviour
{
    [Header("Map level")]
    [Tooltip("1, 2, or 3. This also matches the Tower level required to unlock the ring.")]
    [SerializeField] private int mapLevel = 1;

    [Header("Ring spawn area")]
    [Tooltip("If set, inner radius = this zone's outer radius. Use for concentric rings: Map 2 → Map 1, Map 3 → Map 2.")]
    [SerializeField] private MapZone innerZone;
    [Tooltip("Inner radius when Inner Zone is not set. Map 1 = 0.")]
    [SerializeField] private float innerRadius = 0f;
    [Tooltip("Outer radius (max) = this map radius. If Use Collider For Outer Radius is on, this is ignored.")]
    [SerializeField] private float radius = 5f;
    [Tooltip("Use CircleCollider2D bounds for outer radius so it matches the visual circle.")]
    [SerializeField] private bool useColliderForOuterRadius = true;

    [Header("Rendering around global ToxicFog (Ground order 0)")]
    [Tooltip("Sorting order while this map is locked and must stay below ToxicFog.")]
    [SerializeField] private int lockedSortingOrder = -20;
    [Tooltip("Sorting order after unlock, when this map must render above ToxicFog.")]
    [SerializeField] private int unlockedSortingOrder = 20;
    [Tooltip("Optional. When locked, this object (and its children) are disabled so no features (spawns, etc.) of the locked map are visible. Assign a child that contains spawners or content.")]
    [SerializeField] private GameObject contentRootToHideWhenLocked;

    private SpriteRenderer _zoneRenderer;
    private bool? _lastUnlockedState;

    public int MapLevel => mapLevel;

    /// <summary> True if this zone is unlocked (Map 1 always; Map 2/3 by Tower level). </summary>
    public bool IsUnlocked => MapController.Instance.IsMapUnlocked(mapLevel);

    private void Start()
    {
        _zoneRenderer = GetComponent<SpriteRenderer>();
        RefreshUnlockVisuals();
    }

    private void Update()
    {
        RefreshUnlockVisuals();
    }

    /// <summary> Ring params: center, inner radius (min), outer radius (max). Resources spawn only inside this ring. </summary>
    public void GetRingParams(out Vector2 center, out float minRadius, out float maxRadius)
    {
        if (innerZone != null)
        {
            innerZone.GetRingParams(out _, out _, out float innerOuter);
            minRadius = Mathf.Max(0f, innerOuter);
        }
        else
        {
            minRadius = Mathf.Max(0f, innerRadius);
        }

        Collider2D c = GetComponent<Collider2D>();
        if (useColliderForOuterRadius && c != null)
        {
            Bounds b = c.bounds;
            center = b.center;
            maxRadius = Mathf.Min(b.extents.x, b.extents.y);
        }
        else
        {
            center = transform.position;
            maxRadius = Mathf.Max(minRadius, radius);
        }
        maxRadius = Mathf.Max(minRadius, maxRadius);
    }

    /// <summary> World-space bounds (for collider). </summary>
    public Bounds Bounds
    {
        get
        {
            Collider2D c = GetComponent<Collider2D>();
            return c != null ? c.bounds : new Bounds(transform.position, Vector3.one * 5f);
        }
    }

    private void RefreshUnlockVisuals()
    {
        bool unlocked = mapLevel <= 1 || IsUnlocked;
        if (_lastUnlockedState == unlocked) return;

        _lastUnlockedState = unlocked;
        if (_zoneRenderer != null)
            _zoneRenderer.sortingOrder = unlocked ? unlockedSortingOrder : lockedSortingOrder;

        if (contentRootToHideWhenLocked != null && contentRootToHideWhenLocked.activeSelf != unlocked)
            contentRootToHideWhenLocked.SetActive(unlocked);
    }

    private void OnDrawGizmosSelected()
    {
        GetRingParams(out Vector2 center, out float innerR, out float outerR);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        DrawCircle(center, innerR);
        DrawCircle(center, outerR);
    }

    private void DrawCircle(Vector2 center, float r)
    {
        if (r <= 0f) return;
        const int segments = 32;
        float step = 360f / segments * Mathf.Deg2Rad;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * step;
            float a1 = (i + 1) * step;
            Gizmos.DrawLine(
                center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r,
                center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r);
        }
    }
}
