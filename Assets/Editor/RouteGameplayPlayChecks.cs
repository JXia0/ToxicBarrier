using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

internal static class RouteGameplayPlayChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
    private static void Place(HealthSystem player, Vector2 point)
    { player.transform.position = point; player.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms(); }

    public static IEnumerator CheckPursuit(HealthSystem player, Tower tower, Action<bool,string> check)
    {
        Vector3 origin = player.transform.position;
        var towerAttack = tower.GetComponent<TowerAttackBehavior>();
        bool attacking = towerAttack.enabled;
        towerAttack.enabled = false;
        var wall = new GameObject("Temporary pursuit cover"); wall.layer = LayerMask.NameToLayer("Obstacle");
        wall.transform.position = new Vector3(3, 3);
        var box = wall.AddComponent<BoxCollider2D>(); box.size = new Vector2(.12f, 3);
        var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Bug.prefab"), new Vector3(2.7f, 3), Quaternion.identity);
        var behavior = enemy.GetComponent<EnemyAttackBehavior>(); behavior.enabled = false;
        enemy.GetComponent<NavMeshAgent>().enabled = false;
        Place(player, new Vector2(3.3f, 3));
        yield return null;
        Call(behavior, "UpdateTarget");
        check(Read<Transform>(behavior, "_target") == tower.transform, "Unseen player behind a wall does not attract enemies away from tower");
        box.enabled = false; Physics2D.SyncTransforms(); Call(behavior, "UpdateTarget");
        check(Read<Transform>(behavior, "_target") == player.transform, "Visible player inside aggro range attracts the enemy");
        Vector2 remembered = Read<Vector2>(behavior, "_lastSeenPosition");
        box.enabled = true; Place(player, new Vector2(3.6f, 3.5f)); Call(behavior, "UpdateTarget");
        check(Read<bool>(behavior, "_searchingLastSeen") && Read<Transform>(behavior, "_target") == null &&
            Read<Vector2>(behavior, "_lastSeenPosition") == remembered, "Lost sight searches the fixed last-seen position rather than tracking through walls");
        GameFlowManager.Instance.PauseGame();
        for (int i = 0; i < 6; i++) yield return null;
        Call(behavior, "UpdateTarget");
        check(Read<bool>(behavior, "_searchingLastSeen"), "Pause does not consume the enemy's search memory");
        GameFlowManager.Instance.ResumeGame();
        float until = Time.time + 1.7f;
        while (Time.time < until) yield return null;
        Call(behavior, "UpdateTarget");
        check(!Read<bool>(behavior, "_searchingLastSeen") && Read<Transform>(behavior, "_target") == tower.transform,
            "After 1.6 seconds without sight, the enemy resumes attacking the tower");
        box.enabled = false; Physics2D.SyncTransforms(); Call(behavior, "UpdateTarget");
        check(Read<Transform>(behavior, "_target") == player.transform, "Emerging from cover can draw enemy attention again");
        UnityEngine.Object.Destroy(enemy); UnityEngine.Object.Destroy(wall);
        Place(player, origin); player.RestoreFullHealth();
        towerAttack.enabled = attacking;
        yield return null;
    }

    public static IEnumerator CheckRepair(HealthSystem player, Tower tower, Action<bool,string> check)
    {
        Vector3 origin = player.transform.position;
        var controller = player.GetComponent<PlayerController>();
        var supplies = ExplorationSupply.Available.Where(s => s.IsTowerRepair).OrderBy(s => s.transform.position.sqrMagnitude).ToArray();
        check(supplies.Length == 3 && supplies.All(s => s.Region == 3), "Exactly three finite repair caches exist, all in region three");
        var first = supplies[0]; Vector2 location = first.transform.position;
        Place(player, location);
        int stone = player.GetComponent<PlayerInventory>().GetCount(LootManager.ResourceType.Stone);
        float timeout = Time.realtimeSinceStartup + 3;
        while (first != null && Time.realtimeSinceStartup < timeout) yield return null;
        var cargo = player.GetComponent<TowerRepairCargo>();
        check(first == null && cargo != null && cargo.IsCarrying, "Physical contact collects a repair parcel in the outer region");
        check(player.GetComponent<PlayerInventory>().GetCount(LootManager.ResourceType.Stone) == stone,
            "Repair caches do not multiply ordinary upgrade materials");
        check(!cargo.TryLoad(), "Only one repair parcel can be carried at a time");
        float full = tower.Health.MaxHp;
        Place(player, tower.transform.position + Vector3.down * 1.6f); tower.Health.RestoreFullHealth();
        Call(cargo, "Update"); check(cargo.IsCarrying, "Full-health tower preserves carried repair supply");
        tower.Health.TakeDamage(full * .3f, null);
        GameFlowManager.Instance.PauseGame(); Call(cargo, "Update");
        check(cargo.IsCarrying && Mathf.Approximately(tower.Health.CurrentHp, full * .7f), "Repair delivery is frozen during pause");
        Place(player, location); GameFlowManager.Instance.ResumeGame();
        var path = new NavMeshPath();
        Vector3 goal = tower.transform.position + Vector3.down * 1.6f;
        check(NavMesh.SamplePosition(location, out NavMeshHit start, .5f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(goal, out NavMeshHit end, .5f, NavMesh.AllAreas) &&
            NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
            "Repair cache has a complete return path through expanded building routes");
        float hpBefore = tower.Health.CurrentHp;
        ScreenCapture.CaptureScreenshot(".utmp/ui-checks/14-repair-route.png");
        int corner = 1; double deadline = EditorApplication.timeSinceStartup + 60;
        while (cargo.IsCarrying)
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Repair return route stalled at " + player.transform.position);
            if (!GameFlowManager.Instance.IsPlaying) GameFlowManager.Instance.ResumeGame();
            Time.timeScale = 3;
            while (corner < path.corners.Length && Vector2.Distance(player.transform.position, path.corners[corner]) < .06f) corner++;
            Vector2 waypoint = corner < path.corners.Length ? (Vector2)path.corners[corner] : (Vector2)goal;
            Vector2 step = waypoint - (Vector2)player.transform.position;
            float movement = controller.GetMoveSpeed() * Mathf.Max(Time.deltaTime, Time.fixedDeltaTime);
            controller.SetReviewMovement(step.normalized * Mathf.Clamp01(step.magnitude / Mathf.Max(.001f, movement)));
            yield return null;
        }
        Time.timeScale = 1; controller.SetReviewMovement(null); controller.StopMovement();
        check(Vector2.Distance(player.transform.position, tower.transform.position) <= 2.05f &&
            Mathf.Approximately(tower.Health.CurrentHp, hpBefore + full * .15f), "Actual player movement delivers the parcel and repairs exactly 15 percent tower HP");
        Call(cargo, "Update"); check(Mathf.Approximately(tower.Health.CurrentHp, hpBefore + full * .15f), "Repair parcel cannot heal the tower twice");
        check(ExplorationSupply.Available.Count(s => s.IsTowerRepair) == 2, "Collected repair cache remains consumed");
        Place(player, origin); player.RestoreFullHealth(); tower.Health.RestoreFullHealth();
        typeof(HealthSystem).GetField("_toxicProtectionUntil", Flags).SetValue(player, 0f);
        check(GameText.Translate("携带修塔补给  |  返回净化塔").StartsWith("CARRYING REPAIR"), "Repair objectives retain English localization");
    }
}
