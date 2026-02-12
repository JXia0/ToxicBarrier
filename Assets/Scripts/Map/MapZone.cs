using UnityEngine;

/// <summary>
/// Attach to each of the three circle objects representing Map 1, Map 2, and Map 3.
/// When the player enters the trigger, MapController's current map ID is set to this zone's ID (only if zone is unlocked).
/// Map 2/3 show a fog overlay when locked (progressive unlock by Tower level). Use with EnvironmentalResourceSpawner for spawn rates.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MapZone : MonoBehaviour
{
    [Header("Zone identity")]
    [Tooltip("Map 1, 2, or 3. Drives spawn rates: Map 1 = 0.8 Wood; Map 2 = 0.5 Wood, 0.3 Stone; Map 3 = 0.2 Wood, 0.6 Stone.")]
    [SerializeField] private int mapId = 1;

    [Header("Ring spawn area")]
    [Tooltip("If set, inner radius = this zone's outer radius. Use for concentric rings: Map 2 → Map 1, Map 3 → Map 2.")]
    [SerializeField] private MapZone innerZone;
    [Tooltip("Inner radius when Inner Zone is not set. Map 1 = 0.")]
    [SerializeField] private float innerRadius = 0f;
    [Tooltip("Outer radius (max) = this map radius. If Use Collider For Outer Radius is on, this is ignored.")]
    [SerializeField] private float radius = 5f;
    [Tooltip("Use CircleCollider2D bounds for outer radius so it matches the visual circle.")]
    [SerializeField] private bool useColliderForOuterRadius = true;

    [Header("Player detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Fog of War (Map 2 & 3 when locked)")]
    [Tooltip("Optional. When locked, this overlay is shown (fog). Assign a child with SpriteRenderer; or leave empty to auto-create from this zone's sprite.")]
    [SerializeField] private GameObject fogOverlay;
    [Tooltip("Fog color when zone is locked.")]
    [SerializeField] private Color fogColor = new Color(0.1f, 0.1f, 0.15f, 0.85f);
    [Tooltip("When true, hide this zone's map graphic (SpriteRenderer) when locked so only fog is visible. Unlocked = show map, hide fog.")]
    [SerializeField] private bool hideMapVisualWhenLocked = true;
    [Tooltip("Optional. When locked, this object (and its children) are disabled so no features (spawns, etc.) of the locked map are visible. Assign a child that contains spawners or content.")]
    [SerializeField] private GameObject contentRootToHideWhenLocked;

    public int MapId => Mathf.Clamp(mapId, 1, 3);

    /// <summary> True if this zone is unlocked (Map 1 always; Map 2/3 by Tower level). </summary>
    public bool IsUnlocked => MapController.Instance != null && MapController.Instance.IsMapUnlocked(MapId);

    private void Start()
    {
        EnsureFogOverlay();
    }

    private void Update()
    {
        RefreshFogVisibility();
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        if (MapController.Instance == null) return;
        if (!MapController.Instance.IsMapUnlocked(MapId)) return;

        MapController.Instance.SetMapId(MapId);
    }

    private void EnsureFogOverlay()
    {
        if (mapId <= 1) return;
        if (fogOverlay != null) return;

        Transform fogT = transform.Find("Fog");
        if (fogT != null) { fogOverlay = fogT.gameObject; return; }

        SpriteRenderer zoneSr = GetComponent<SpriteRenderer>();
        if (zoneSr == null || zoneSr.sprite == null) return;

        GameObject fog = new GameObject("Fog");
        fog.transform.SetParent(transform, false);
        fog.transform.localPosition = Vector3.zero;
        fog.transform.localScale = Vector3.one;
        fog.transform.localRotation = Quaternion.identity;

        SpriteRenderer sr = fog.AddComponent<SpriteRenderer>();
        sr.sprite = zoneSr.sprite;
        sr.color = fogColor;
        sr.sortingOrder = zoneSr.sortingOrder + 1;
        sr.sortingLayerID = zoneSr.sortingLayerID;
        sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

        fogOverlay = fog;
    }

    private void RefreshFogVisibility()
    {
        if (mapId <= 1) return;

        bool locked = !IsUnlocked;

        if (fogOverlay != null && fogOverlay.activeSelf != locked)
            fogOverlay.SetActive(locked);

        if (hideMapVisualWhenLocked)
        {
            SpriteRenderer zoneSr = GetComponent<SpriteRenderer>();
            if (zoneSr != null && zoneSr.enabled == locked)
                zoneSr.enabled = !locked;
        }

        if (contentRootToHideWhenLocked != null && contentRootToHideWhenLocked.activeSelf == locked)
            contentRootToHideWhenLocked.SetActive(!locked);
    }

    private bool IsPlayer(Collider2D other)
    {
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            return true;
        if (other.GetComponent<PlayerController>() != null)
            return true;
        return false;
    }

    /// <summary> Legacy rect mode. Prefer GetRingParams for ring spawning. </summary>
    public void GetSpawnArea(out Vector2 min, out Vector2 max)
    {
        Vector2 center = transform.position;
        min = new Vector2(center.x - radius, center.y - radius);
        max = new Vector2(center.x + radius, center.y + radius);
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
