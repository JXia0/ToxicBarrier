using UnityEngine;

/// <summary>Keep baked obstacles present; reveal their art when the corresponding ring opens.</summary>
public sealed class BuildingRegionVisibility : MonoBehaviour
{
    public enum SupplyKind { Materials, None, TowerRepair }
    [SerializeField, Range(1, 3)] private int region = 1;
    [SerializeField] private SupplyKind supplyKind = SupplyKind.Materials;
    private SpriteRenderer _sprite;
    private bool _suppliesPlaced;
    private void Awake() { _sprite = GetComponent<SpriteRenderer>(); }
    private void LateUpdate()
    {
        if (_sprite != null && MapController.Instance != null)
            _sprite.enabled = MapController.Instance.IsMapUnlocked(region);
        if (!_suppliesPlaced && supplyKind != SupplyKind.None && _sprite != null && _sprite.enabled && GameFlowManager.Instance != null && GameFlowManager.Instance.IsPlaying)
        {
            _suppliesPlaced = true;
            var footprint = GetComponent<PolygonCollider2D>();
            if (footprint != null)
            {
                Bounds b = footprint.bounds;
                Vector2[] options = { new Vector2(b.max.x + .9f, b.center.y), new Vector2(b.min.x - .9f, b.center.y),
                    new Vector2(b.center.x, b.min.y - .9f), new Vector2(b.center.x, b.max.y + .9f) };
                foreach (var point in options) if (ExplorationSupply.TrySpawn(point, region, supplyKind == SupplyKind.TowerRepair)) break;
            }
        }
    }
}
