using UnityEngine;

/// <summary>
/// Attach to each of the three circle objects representing Map 1, Map 2, and Map 3.
/// When the player enters the trigger, MapController's current map ID is set to this zone's ID.
/// Use with EnvironmentalResourceSpawner on the same (or child) object so resources spawn inside this zone with this map's rates.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MapZone : MonoBehaviour
{
    [Header("Zone identity")]
    [Tooltip("Map 1, 2, or 3. Drives spawn rates: Map 1 = 0.8 Wood; Map 2 = 0.5 Wood, 0.3 Stone; Map 3 = 0.2 Wood, 0.6 Stone.")]
    [SerializeField] private int mapId = 1;

    [Header("Player detection")]
    [SerializeField] private string playerTag = "Player";

    public int MapId => Mathf.Clamp(mapId, 1, 3);

    /// <summary> World-space bounds of this zone (from Collider2D). Used by spawner to place resources inside the circle. </summary>
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

        if (MapController.Instance != null)
            MapController.Instance.SetMapId(MapId);
    }

    private bool IsPlayer(Collider2D other)
    {
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            return true;
        if (other.GetComponent<PlayerController>() != null)
            return true;
        return false;
    }

    /// <summary> Get spawn area as Min/Max for EnvironmentalResourceSpawner. </summary>
    public void GetSpawnArea(out Vector2 min, out Vector2 max)
    {
        Bounds b = Bounds;
        min = new Vector2(b.min.x, b.min.y);
        max = new Vector2(b.max.x, b.max.y);
    }

    private void OnDrawGizmosSelected()
    {
        Bounds b = Bounds;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawCube(b.center, b.size);
    }
}
