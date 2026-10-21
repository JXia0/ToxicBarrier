using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

internal static class GameplayBalancePlayChecks
{
    private static object Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Place(HealthSystem player, Vector2 point)
    { player.transform.position = point; player.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms(); }

    public static IEnumerator Run(HealthSystem player, Tower tower, Action<bool, string> check)
    {
        Vector3 original = player.transform.position;
        var attack = tower.GetComponent<TowerAttackBehavior>();
        attack.enabled = false;
        UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
        var spawners = UnityEngine.Object.FindObjectsOfType<EnvironmentalResourceSpawner>();
        int tested = 0;
        foreach (int seed in new[] { 19, 713, 20260919 })
        {
            UnityEngine.Random.InitState(seed);
            foreach (var spawner in spawners)
            {
                var positions = (List<Vector2>)Invoke(spawner, "BuildSpawnPositions");
                check(positions.Count > 0, "Reachable resource candidates remain available: " + seed + "/" + spawner.name);
                foreach (var position in positions)
                {
                    if (!ReachableGround.IsOpen(position) || !ReachableGround.TryFind(position, .05f, out _))
                        throw new Exception("Inaccessible resource at " + position);
                    tested++;
                }
            }
        }
        UnityEngine.Random.state = originalRandom;
        check(tested > 0, "Resource positions avoid walls and retain complete paths across three random seeds (" + tested + ")");

        var wall = new GameObject("Temporary melee cover fixture"); wall.layer = LayerMask.NameToLayer("Obstacle");
        wall.transform.position = new Vector2(3, 3);
        var box = wall.AddComponent<BoxCollider2D>(); box.size = new Vector2(.12f, 2f);
        var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Bug.prefab"), new Vector3(2.7f, 3), Quaternion.identity);
        var behavior = enemy.GetComponent<EnemyAttackBehavior>(); behavior.enabled = false;
        enemy.GetComponent<NavMeshAgent>().enabled = false;
        Place(player, new Vector2(3.3f, 3));
        for (int i = 0; i < 2; i++) yield return null;
        player.RestoreFullHealth();
        typeof(EnemyAttackBehavior).GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(behavior, player.transform);
        Invoke(behavior, "DealDamage");
        check(player.CurrentHp == player.MaxHp, "A thin wall blocks actual enemy melee damage at close range");
        box.enabled = false; Physics2D.SyncTransforms();
        Invoke(behavior, "DealDamage");
        check(player.CurrentHp < player.MaxHp, "The same melee strike damages the player when cover is removed");
        enemy.transform.position = new Vector2(26, 0);
        Place(player, Vector2.zero);
        float speedA = (float)Invoke(behavior, "GetMoveSpeed");
        Place(player, new Vector2(35, 0));
        float speedB = (float)Invoke(behavior, "GetMoveSpeed");
        check(Mathf.Approximately(speedA, speedB) && Mathf.Approximately(speedA, .8f), "Enemy speed uses its own region and is unaffected by the player's region");
        UnityEngine.Object.Destroy(enemy); UnityEngine.Object.Destroy(wall);

        Place(player, original); player.RestoreFullHealth();
        check(player.ToxicDamagePerSecond >= 1.2f && player.ToxicDamagePerSecond <= 2f, "Scene poison budget is 1.2-2 HP per second");
        int oldPeriods = player.GetToxicGasPeriodsElapsed(); player.SetToxicGasPeriodsElapsed(100);
        check(Mathf.Approximately(player.ToxicDamagePerSecond, 2), "Late-run poison is capped at two HP per second");
        player.SetToxicGasPeriodsElapsed(oldPeriods);
        check(ExplorationSupply.Available.Count >= 2, "Side-route buildings expose finite exploration supplies");
        var cache = ExplorationSupply.Available.First();
        Vector2 cachePosition = cache.transform.position;
        check(ReachableGround.TryFind(cachePosition, .1f, out _), "Exploration supplies have a complete navigable route");
        player.TakeDamage(50, null);
        var inventory = player.GetComponent<PlayerInventory>(); int wood = inventory.GetCount(LootManager.ResourceType.Wood);
        Place(player, cachePosition);
        float timeout = Time.realtimeSinceStartup + 3;
        while (cache != null && Time.realtimeSinceStartup < timeout) yield return null;
        check(cache == null && inventory.GetCount(LootManager.ResourceType.Wood) == wood + 10, "Physical exploration pickup awards its finite material reward");
        check(player.CurrentHp >= 85 && player.ToxicProtectionSeconds > 16, "Exploration pickup heals 35 and grants gas protection");
        Place(player, new Vector2(16, 0));
        float protectedHp = player.CurrentHp;
        Invoke(player, "ApplyToxicGasDamage", 1f);
        check(player.CurrentHp == protectedHp, "Gas protection actually prevents poison damage");
        GameFlowManager.Instance.PauseGame();
        float remaining = player.ToxicProtectionSeconds;
        for (int i = 0; i < 6; i++) yield return null;
        check(Mathf.Approximately(remaining, player.ToxicProtectionSeconds), "Pause preserves temporary gas protection duration");
        GameFlowManager.Instance.ResumeGame();
        typeof(HealthSystem).GetField("_toxicProtectionUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, 0f);
        Place(player, original); player.RestoreFullHealth();

        var bossObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Boss.prefab"), new Vector3(4, 3), Quaternion.identity);
        bossObject.GetComponent<EnemyAttackBehavior>().enabled = false;
        bossObject.GetComponent<NavMeshAgent>().enabled = false;
        var boss = bossObject.AddComponent<FinalSiegeBoss>();
        for (int i = 0; i < 3; i++) yield return null;
        check(boss.Health.MaxHp == 108000 && boss.Health.CurrentHp == 108000, "Final boss custom health survives Start and cannot be one-shot by the tower");
        bossObject.transform.position = tower.transform.position + Vector3.right * 2;
        var escort = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Bug.prefab"), new Vector3(5, 5), Quaternion.identity);
        escort.GetComponent<EnemyAttackBehavior>().enabled = false;
        escort.GetComponent<NavMeshAgent>().enabled = false;
        escort.transform.position = tower.transform.position + Vector3.right * 3;
        check(Invoke(attack, "GetTargetEnemy") == escort.GetComponent<Enemy>(), "Tower prioritizes an escort over the high-health final boss");
        UnityEngine.Object.Destroy(escort);
        bossObject.transform.position = new Vector3(4, 3);
        Place(player, new Vector2(4, 4));
        Invoke(boss, "BeginStrike", (Vector2)player.transform.position);
        Vector2 strike = boss.StrikePosition;
        Place(player, new Vector2(7, 4));
        float end = Time.time + 1.6f;
        while (Time.time < end) yield return null;
        check(player.CurrentHp == player.MaxHp && boss.StrikePosition == strike, "Boss telegraph locks to the floor and moving out avoids damage");
        Invoke(boss, "BeginStrike", (Vector2)player.transform.position);
        GameFlowManager.Instance.PauseGame();
        for (int i = 0; i < 6; i++) yield return null;
        check(boss.IsWindingUp && player.CurrentHp == player.MaxHp, "Boss telegraph and damage freeze during pause");
        GameFlowManager.Instance.ResumeGame();
        ScreenCapture.CaptureScreenshot(".utmp/ui-checks/12-boss-telegraph.png");
        end = Time.time + 1.6f;
        while (Time.time < end) yield return null;
        check(Mathf.Approximately(player.CurrentHp, 70), "Remaining inside the telegraph takes one 30 HP slam");
        UnityEngine.Object.Destroy(bossObject);
        for (int i = 0; i < 3; i++) yield return null;
        check(FinalSiegeBoss.Active == null, "Boss HUD reference is cleared on despawn");
        Place(player, original); player.RestoreFullHealth(); attack.enabled = true;
        check(Resources.Load<Shader>("Presentation/SurvivorRim") != null, "Player readability shader is included as a runtime resource");
        var font = Resources.Load<TMPro.TMP_FontAsset>("Presentation/Chinese");
        check("骸骨领主橙色预警圈侧路毒气防护隐藏靠近可疑掉落物".All(c => font.HasCharacter(c)), "Chinese font has baked glyphs for all new gameplay notices");
        check(GameText.Translate("隐藏怪靠近  |  红色边缘越强越危险，绕开可疑掉落物").StartsWith("HIDDEN ENEMY"), "Hidden-enemy guidance has an English translation");
    }

    public static IEnumerator CheckOuterRegions(HealthSystem player, Action<bool, string> check)
    {
        Vector3 original = player.transform.position;
        for (int i = 0; i < 10; i++) yield return null;
        check(ExplorationSupply.Available.Count(s => s.Region == 2) == 4 && ExplorationSupply.Available.Count(s => s.Region == 3 && s.IsTowerRepair) == 3,
            "Expanded buildings retain four second-region supplies and three finite third-region repairs");
        foreach (var supply in ExplorationSupply.Available)
            check(ReachableGround.TryFind(supply.transform.position, .05f, out _), "Side-route supply is navigable: " + supply.name);
        Place(player, new Vector2(20, 0));
        check(MapController.Instance.IsSafeAt(player.transform.position), "Clearing region two actually removes its toxic damage");
        float hp = player.CurrentHp;
        Invoke(player, "ApplyToxicGasDamage", 1f);
        check(player.CurrentHp == hp, "Cleared region immunity is enforced by HealthSystem");
        Place(player, new Vector2(35, 0));
        check(!MapController.Instance.IsSafeAt(player.transform.position), "Uncleared third region remains toxic");
        Invoke(player, "ApplyToxicGasDamage", 1f);
        check(player.CurrentHp < hp, "Uncleared region still damages the player");
        var rim = player.transform.Find("Survivor silhouette rim").GetComponent<SpriteRenderer>();
        float settle = Time.time + 1.2f;
        while (Time.time < settle) yield return null;
        check(Vector3.Dot(Camera.main.transform.forward, Vector3.forward) > .999f,
            "Camera remains perpendicular to the 2D world in the outer region");
        check(rim.enabled && rim.sprite == player.GetComponent<SpriteRenderer>().sprite, "Outer-region silhouette rim follows the current animated player sprite");
        ScreenCapture.CaptureScreenshot(".utmp/ui-checks/13-outer-readability.png");
        for (int i = 0; i < 4; i++) yield return null;
        Place(player, original); player.RestoreFullHealth();
        foreach (var zone in UnityEngine.Object.FindObjectsOfType<MapZone>())
        {
            var texture = zone.GetComponent<SpriteRenderer>().sprite.texture;
            check(texture.filterMode == FilterMode.Point, "Map retains crisp sprite filtering: " + zone.name);
        }
    }
}
