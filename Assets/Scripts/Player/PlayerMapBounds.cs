using UnityEngine;

/// <summary>
/// Clamps the player to the currently allowed map radius (progressive unlock).
/// Prevents the player from entering Map 2/3 before they are unlocked.
/// Attach to the player GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMapBounds : MonoBehaviour
{
    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void LateUpdate()
    {
        if (MapController.Instance == null) return;

        Vector2 center = MapController.Instance.GetMapCenter();
        float maxR = Mathf.Max(0f, MapController.Instance.GetMaxAllowedRadius());
        Vector2 pos = transform.position;
        float dist = Vector2.Distance(pos, center);
        if (dist <= maxR) return;

        Vector2 clamped = center + (pos - center).normalized * maxR;
        transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
        if (_rb != null)
            _rb.velocity = Vector2.zero;
    }
}
