using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>A reachable, physical clear reward; the remaining upgrade cost is calculated on pickup.</summary>
public sealed class RegionSupplyCache : MonoBehaviour
{
    public int Region { get; private set; }
    private Tower _tower;
    private bool _collected;
    private Transform _icon;
    public static RegionSupplyCache Spawn(Tower tower, int region)
    {
        Vector3 origin = tower.transform.position;
        // The guaranteed reward stays inside the region just cleared, even after map resizing.
        float radius = region == 1 ? MapController.Instance.GetMap1Radius() * .68f :
            Mathf.Lerp(MapController.Instance.GetMap1Radius(), MapController.Instance.GetMap2Radius(), .65f);
        var path = new NavMeshPath();
        Vector3 position = origin;
        bool found = false;
        if (NavMesh.SamplePosition(origin + Vector3.down, out NavMeshHit start, 3, NavMesh.AllAreas))
            for (int i = 0; i < 48; i++)
            {
                float angle = (-90 + i * 137.5f) * Mathf.Deg2Rad;
                Vector3 desired = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 1, NavMesh.AllAreas)) continue;
                float distance = Vector2.Distance(origin, hit.position);
                if (distance < radius - 1 || distance > radius + 1) continue;
                if (!NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                position = new Vector3(hit.position.x, hit.position.y, 0); found = true; break;
            }
        // A map bake failure must not silently block the entire run.
        if (!found) { position = origin + Vector3.down * 3; Debug.LogWarning("Supply cache uses the safe fallback position: no outer reachable point."); }
        var go = new GameObject("Region " + region + " Supply Cache");
        go.transform.position = position;
        var cache = go.AddComponent<RegionSupplyCache>(); cache.Region = region; cache._tower = tower;
        var collider = go.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = .75f;
        cache._icon = new GameObject("Supply icon", typeof(SpriteRenderer)).transform;
        cache._icon.SetParent(go.transform, false);
        var renderer = cache._icon.GetComponent<SpriteRenderer>();
        renderer.sprite = Resources.Load<Sprite>("Presentation/Supply"); renderer.sortingOrder = 100;
        renderer.sortingLayerName = SortingLayerNames.Characters;
        if (renderer.sprite != null) cache._icon.localScale = Vector3.one * (1.1f / renderer.sprite.bounds.size.x);
        WorldMarker.Add(go.transform, .9f, new Color(1f, .8f, .25f), 98);
        return cache;
    }

    private void Update() { if (_icon != null) _icon.localPosition = Vector3.up * (.15f + Mathf.Sin(Time.time * 3) * .08f); }
    private void OnTriggerEnter2D(Collider2D other) { Collect(other); }
    private void OnTriggerStay2D(Collider2D other) { Collect(other); }
    private void Collect(Collider2D other)
    {
        if (_collected || GameFlowManager.Instance == null || !GameFlowManager.Instance.IsPlaying) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) return;
        _collected = true;
        foreach (LootManager.ResourceType type in Enum.GetValues(typeof(LootManager.ResourceType)))
        {
            int required = 0;
            for (int level = _tower.CurrentLevel + 1; level <= Mathf.Min(Region + 1, Tower.MaxLevel); level++)
                required += _tower.GetUpgradeCostToLevel(level).GetAmount(type);
            int missing = Mathf.Max(0, required - inventory.GetCount(type));
            if (missing > 0) inventory.Add(type, missing);
        }
        player.GetComponent<HealthSystem>().Heal(25);
        GameAudio.Play("Supply");
        GameFlowManager.Instance.NotifySupplyCollected(Region);
        Destroy(gameObject);
    }
}
