using UnityEngine;
using UnityEngine.AI;

/// <summary>Match the narrow ground base of each tower tier; the roof does not block movement.</summary>
[DefaultExecutionOrder(50)]
public sealed class TowerNavigationFootprint : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Sprite _lastSprite;
    private BoxCollider2D _feet;
    private NavMeshObstacle _navigation;

    private void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        GameObject baseObject = new GameObject("TowerGroundFootprint");
        baseObject.layer = LayerMask.NameToLayer("Obstacle");
        baseObject.transform.SetParent(transform, false);
        _feet = baseObject.AddComponent<BoxCollider2D>();
        _navigation = baseObject.AddComponent<NavMeshObstacle>();
        _navigation.shape = NavMeshObstacleShape.Box;
        _navigation.carving = true;
        _navigation.carveOnlyStationary = true;
        _navigation.carvingTimeToStationary = 0;
        Refresh();
    }

    private void LateUpdate()
    {
        if (_renderer != null && _renderer.sprite != _lastSprite) Refresh();
    }

    private void Refresh()
    {
        _lastSprite = _renderer.sprite;
        if (_lastSprite == null) return;
        Bounds bounds = _lastSprite.bounds;
        // Tight-cropped tower art: follow each tier's visible foundation instead of a fixed-height strip.
        Vector2 size = new Vector2(bounds.size.x * .94f, bounds.size.y * .105f);
        Vector2 centre = new Vector2(bounds.center.x, bounds.min.y + size.y * .55f);
        if (_renderer.flipX) centre.x = -centre.x;
        if (_renderer.flipY) centre.y = -centre.y;
        _feet.size = size;
        _feet.offset = centre;
        _navigation.size = new Vector3(size.x, size.y, 1f);
        _navigation.center = new Vector3(centre.x, centre.y, .45f);
    }
}
