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
        Vector2 size = new Vector2(bounds.size.x * .78f, .12f);
        Vector2 centre = new Vector2(bounds.center.x, bounds.min.y + .085f);
        _feet.size = size;
        _feet.offset = centre;
        _navigation.size = new Vector3(size.x, size.y, 1f);
        _navigation.center = new Vector3(centre.x, centre.y, .45f);
    }
}
