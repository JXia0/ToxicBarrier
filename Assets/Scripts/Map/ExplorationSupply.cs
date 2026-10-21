using System.Collections.Generic;
using UnityEngine;

/// <summary>Finite side-route rewards beside buildings; no respawn or replacement of clear guarantees.</summary>
public sealed class ExplorationSupply : MonoBehaviour
{
    public static readonly HashSet<ExplorationSupply> Available = new HashSet<ExplorationSupply>();
    public int Region { get; private set; }
    public bool IsTowerRepair { get; private set; }
    private bool _collected;
    private float _spawnAt;
    private SpriteRenderer _icon;
    private static int _lastManualFrame = -1;

    public static ExplorationSupply FindInteractable(PlayerController player)
    {
        if (player == null || _lastManualFrame == Time.frameCount) return null;
        Tower tower = Tower.FindClosestLivingTower(player.transform.position);
        if (tower != null && tower.IsPlayerInUpgradeRange) return null;
        ExplorationSupply nearest = null;
        float closest = .85f * .85f;
        foreach (var supply in Available)
        {
            if (supply == null || supply._collected) continue;
            float distance = ((Vector2)(supply.transform.position - player.transform.position)).sqrMagnitude;
            if (distance > closest || !ReachableGround.HasLineOfSight(supply.transform.position, player.transform.position, player.transform)) continue;
            closest = distance; nearest = supply;
        }
        return nearest;
    }

    public static bool TrySpawn(Vector2 desired, int region, bool towerRepair = false)
    {
        if (!ReachableGround.TryFind(desired, 1.25f, out Vector2 point)) return false;
        var map = MapController.Instance;
        if (map == null || map.GetRegionAt(point) != region) return false;
        var go = new GameObject("Side-route supplies " + region);
        go.transform.position = point;
        var supply = go.AddComponent<ExplorationSupply>(); supply.Region = region; supply._spawnAt = Time.time;
        supply.IsTowerRepair = towerRepair;
        if (towerRepair) go.name = "Tower repair supplies " + region;
        var trigger = go.AddComponent<CircleCollider2D>(); trigger.isTrigger = true; trigger.radius = .6f;
        var icon = new GameObject("Supply"); icon.transform.SetParent(go.transform, false);
        supply._icon = icon.AddComponent<SpriteRenderer>();
        supply._icon.sprite = Resources.Load<Sprite>("Presentation/Supply");
        supply._icon.color = towerRepair ? new Color(.55f, .72f, 1f) : new Color(.68f, .94f, .91f);
        supply._icon.sortingLayerName = SortingLayerNames.Characters;
        if (supply._icon.sprite != null) icon.transform.localScale = Vector3.one * (.7f / supply._icon.sprite.bounds.size.x);
        return true;
    }
    private void OnEnable() { Available.Add(this); }
    private void OnDisable() { Available.Remove(this); }
    private void Update()
    {
        if (_icon != null) _icon.transform.localPosition = Vector3.up * (.09f + Mathf.Sin((Time.time - _spawnAt) * 2f) * .06f);
        if (!Input.GetKeyDown(KeyCode.E) || GameFlowManager.Instance == null || !GameFlowManager.Instance.IsPlaying) return;
        var player = MapController.Instance != null ? MapController.Instance.Player : null;
        if (FindInteractable(player) == this) TryCollect(player, true);
    }
    private void OnTriggerEnter2D(Collider2D other) { Collect(other); }
    private void OnTriggerStay2D(Collider2D other) { Collect(other); }
    private void Collect(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        TryCollect(player, false);
    }
    private void TryCollect(PlayerController player, bool manual)
    {
        if (_collected || player == null || GameFlowManager.Instance == null || !GameFlowManager.Instance.IsPlaying) return;
        if (manual && FindInteractable(player) != this) return;
        if (!ReachableGround.HasLineOfSight(transform.position, player.transform.position, player.transform)) return;
        PlayerInventory bag = player.GetComponent<PlayerInventory>();
        HealthSystem health = player.GetComponent<HealthSystem>();
        if (bag == null || health == null || health.CurrentHp <= 0) return;
        Tower tower = Tower.FindClosestLivingTower(transform.position);
        bool fieldSupplies = !IsTowerRepair && tower != null && tower.IsMaxLevel;
        if (IsTowerRepair || fieldSupplies)
        {
            var cargo = player.GetComponent<TowerRepairCargo>();
            if (cargo == null) cargo = player.gameObject.AddComponent<TowerRepairCargo>();
            if (!cargo.TryLoad(fieldSupplies)) return;
        }
        else if (!manual && health.CurrentHp >= health.MaxHp - .01f) return;
        _collected = true;
        if (manual) _lastManualFrame = Time.frameCount;
        // Exploration accelerates upgrades, while finite clear rewards still guarantee progression.
        if (!IsTowerRepair && !fieldSupplies)
        {
            bag.Add(LootManager.ResourceType.Wood, 10);
            bag.Add(LootManager.ResourceType.Gel, 6);
            if (Region >= 2) { bag.Add(LootManager.ResourceType.Stone, 12); bag.Add(LootManager.ResourceType.Teeth, 5); }
        }
        health.Heal(35); health.GrantToxicProtection(18);
        GameAudio.Play("Supply", .5f);
        GameFlowManager.Instance.ShowGameplayNotice(fieldSupplies ? "已取得防御补给  |  返塔修复并获得短暂防护" :
            IsTowerRepair ? "已取得修塔补给  |  返回塔旁，修复 15% 塔生命" : "侧路补给  |  材料、35 生命、18 秒毒气防护", 6);
        Destroy(gameObject);
    }
}
