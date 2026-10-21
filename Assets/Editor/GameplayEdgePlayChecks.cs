using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in boundary checks in the existing temporary Play Mode test scene.</summary>
[InitializeOnLoad]
internal static class GameplayEdgePlayChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Place(HealthSystem player, Vector2 point)
    { player.transform.position = point; player.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms(); }

    static GameplayEdgePlayChecks() { EditorApplication.update += Prepare; }
    private static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists("Temp/ToxicBarrierEdges.prepare")) return;
        File.Delete("Temp/ToxicBarrierEdges.prepare");
        try
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Presentation/Chinese.asset");
            string copy = File.ReadAllText("Assets/Scripts/Core/GameText.cs") + File.ReadAllText("Assets/Scripts/Core/GameHudUI.cs");
            string missing = new string(copy.Where(c => c > 127 && !font.HasCharacter(c)).Distinct().ToArray());
            if (missing.Length > 0 && !font.TryAddCharacters(missing, out string unavailable)) throw new Exception("Missing glyphs: " + unavailable);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            File.WriteAllText("Temp/ToxicBarrierEdges.result", "PASS");
        }
        catch (Exception e) { File.WriteAllText("Temp/ToxicBarrierEdges.result", "FAIL\n" + e); }
    }

    public static IEnumerator Run(HealthSystem player, Tower tower, Action<bool, string> check)
    {
        var flow = GameFlowManager.Instance;
        var hud = UnityEngine.Object.FindObjectOfType<GameHudUI>();
        var bag = player.GetComponent<PlayerInventory>();
        var controller = player.GetComponent<PlayerController>();
        var collider = player.GetComponent<Collider2D>();
        Vector3 original = player.transform.position;
        float clock = flow.SurvivalSeconds;
        var timeProperty = typeof(GameFlowManager).GetProperty("SurvivalSeconds");
        typeof(GameFlowManager).GetField("_notice", Private).SetValue(flow, null);
        ((IList)typeof(GameFlowManager).GetField("_pendingNotices", Private).GetValue(flow)).Clear();
        timeProperty.SetValue(flow, 0f);
        flow.ShowGameplayNotice("supply fixture", 3);
        check(((string)Call(hud, "Objective", flow)).StartsWith("WASD"), "Opening tutorial outranks supply feedback and nearby supplies");
        flow.ShowGameplayNotice("danger one", 2, true);
        flow.ShowGameplayNotice("danger two", 2, true);
        flow.ShowGameplayNotice("supply two", 3);
        check((string)Call(hud, "Objective", flow) == "danger one", "Danger preempts pickup feedback without replacing an active warning");
        flow.PauseGame();
        for (int i = 0; i < 4; i++) yield return null;
        check(flow.GameplayNotice == "danger one", "Pause preserves queued danger feedback");
        flow.ResumeGame();
        Call(flow, "TickNotices", 2.1f);
        check(flow.GameplayNotice == "danger two", "Second danger waits until the first warning finishes");
        Call(flow, "TickNotices", 2.1f);
        check(!flow.ShowDangerNotice && flow.GameplayNotice == "supply fixture", "Interrupted pickup feedback resumes after danger warnings");
        typeof(GameFlowManager).GetField("_notice", Private).SetValue(flow, null);
        ((IList)typeof(GameFlowManager).GetField("_pendingNotices", Private).GetValue(flow)).Clear();
        timeProperty.SetValue(flow, clock);

        var existing = UnityEngine.Object.FindObjectsOfType<ResourcePickup>().Where(p => p.enabled).ToArray();
        foreach (var pickup in existing) pickup.enabled = false;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Wood.prefab");
        Place(player, original);
        var stack = UnityEngine.Object.Instantiate(prefab, new Vector3(3, 3), Quaternion.identity).GetComponent<ResourcePickup>();
        int initial = stack.Amount;
        check(ResourcePickup.TryMerge(LootManager.ResourceType.Wood, new Vector2(3.1f, 3), 4) && stack.Amount == initial + 4,
            "Nearby ordinary drops merge without losing resource quantity");
        check(stack.GetComponentInChildren<TextMeshPro>().text == (initial + 4).ToString(), "Merged pickup displays its quantity");
        check(!ResourcePickup.TryMerge(LootManager.ResourceType.Gel, new Vector2(3, 3), 2), "Different resource types never merge");
        var wall = new GameObject("Temporary pickup cover"); wall.layer = LayerMask.NameToLayer("Obstacle");
        wall.transform.position = new Vector3(3.3f, 3);
        var box = wall.AddComponent<BoxCollider2D>(); box.size = new Vector2(.1f, 2);
        Physics2D.SyncTransforms();
        check(!ResourcePickup.TryMerge(LootManager.ResourceType.Wood, new Vector2(3.6f, 3), 2), "Same-type drops across a wall do not merge");
        int wood = bag.GetCount(LootManager.ResourceType.Wood);
        Place(player, new Vector2(3.6f, 3));
        Call(stack, "OnTriggerEnter2D", collider);
        check(bag.GetCount(LootManager.ResourceType.Wood) == wood, "A wall blocks ordinary pickup collection");
        box.enabled = false; Physics2D.SyncTransforms();
        flow.PauseGame(); Call(stack, "OnTriggerEnter2D", collider);
        check(bag.GetCount(LootManager.ResourceType.Wood) == wood, "Paused pickup cannot change inventory");
        flow.ResumeGame(); Call(stack, "OnTriggerStay2D", collider); Call(stack, "OnTriggerEnter2D", collider);
        check(bag.GetCount(LootManager.ResourceType.Wood) == wood + initial + 4, "Resume collects the entire stack exactly once");
        Place(player, original);
        yield return null;
        var disguise = UnityEngine.Object.Instantiate(prefab, new Vector3(3, 3), Quaternion.identity);
        disguise.GetComponent<ResourcePickup>().enabled = false;
        var trap = disguise.AddComponent<DisguisedParasite>(); trap.Configure(10, .7f, 0);
        check(!ResourcePickup.TryMerge(LootManager.ResourceType.Wood, new Vector2(3, 3), 1), "A disguised parasite cannot absorb normal drops");
        box.enabled = true; Physics2D.SyncTransforms(); Place(player, new Vector2(3.6f, 3)); player.RestoreFullHealth();
        var parasitesBefore = UnityEngine.Object.FindObjectsOfType<DisguisedParasite>();
        LootManager.Instance.SpawnDisguisedParasite(HealthSystem.EnemyType.Bug, wall.transform.position);
        var spawned = UnityEngine.Object.FindObjectsOfType<DisguisedParasite>().Except(parasitesBefore).ToArray();
        check(spawned.All(p => ReachableGround.IsOpen(p.transform.position)), "Parasite spawn rejects ground intersecting a building footprint");
        foreach (var created in spawned) UnityEngine.Object.Destroy(created.gameObject);
        Call(trap, "OnTriggerEnter2D", collider);
        check(player.CurrentHp == player.MaxHp, "Cover blocks parasite contact damage");
        box.enabled = false; Physics2D.SyncTransforms();
        typeof(HealthSystem).GetField("_contactProtectionUntil", Private).SetValue(player, 0f);
        Call(trap, "OnTriggerStay2D", collider);
        check(player.CurrentHp == player.MaxHp - 10, "Parasite still damages on the same side of cover");
        UnityEngine.Object.Destroy(disguise); UnityEngine.Object.Destroy(wall);
        foreach (var pickup in existing) if (pickup != null) pickup.enabled = true;
        Place(player, original); player.RestoreFullHealth();
        yield return null;

        var supply = ExplorationSupply.Available.First();
        Place(player, supply.transform.position);
        wood = bag.GetCount(LootManager.ResourceType.Wood);
        for (int i = 0; i < 6; i++) yield return null;
        check(supply != null && bag.GetCount(LootManager.ResourceType.Wood) == wood, "Walking over exploration supplies at full health preserves them");
        check(ExplorationSupply.FindInteractable(controller) == supply, "Full-health player can choose nearby supplies explicitly");
        flow.PauseGame(); Call(supply, "TryCollect", controller, true);
        check(bag.GetCount(LootManager.ResourceType.Wood) == wood, "Manual supply interaction is disabled during pause");
        flow.ResumeGame(); Call(supply, "TryCollect", controller, true);
        check(bag.GetCount(LootManager.ResourceType.Wood) == wood + 10, "Manual supply interaction grants materials at full health");
        check(ExplorationSupply.FindInteractable(controller) == null, "One keypress cannot consume several exploration supplies");
        Vector2 restorePoint = supply.transform.position;
        Place(player, original); player.RestoreFullHealth();
        yield return null;
        // Restore the finite cache used by this fixture for the existing exploration regression.
        check(ExplorationSupply.TrySpawn(restorePoint, 1), "Exploration fixture restores its cache for downstream checks");
        typeof(HealthSystem).GetField("_toxicProtectionUntil", Private).SetValue(player, 0f);
        typeof(HealthSystem).GetField("_contactProtectionUntil", Private).SetValue(player, 0f);
        typeof(GameFlowManager).GetField("_notice", Private).SetValue(flow, null);
        ((IList)typeof(GameFlowManager).GetField("_pendingNotices", Private).GetValue(flow)).Clear();
        check(GameText.Translate("侧路补给 [E]  |  主动领取材料、回血与毒气防护").StartsWith("SUPPLIES [E]"), "Manual supplies retain English localization");
        check("侧路补给主动领取材料回血与毒气防护".All(c => Resources.Load<TMP_FontAsset>("Presentation/Chinese").HasCharacter(c)), "Manual supplies have baked Chinese glyphs");
    }
}
