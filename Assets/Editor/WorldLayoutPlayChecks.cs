using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

internal static class WorldLayoutPlayChecks
{
    public static IEnumerator Run(HealthSystem health, Tower tower, Action<bool, string> check)
    {
        var player = health.GetComponent<PlayerController>();
        var body = player.GetComponent<Rigidbody2D>();
        Vector3 original = player.transform.position;
        var playerArt=player.GetComponent<SpriteRenderer>();
        check(Mathf.Abs(playerArt.sprite.pivot.x-51)<.01f && Mathf.Abs(playerArt.sprite.pivot.y-5)<.01f &&
            playerArt.spriteSortPoint==SpriteSortPoint.Pivot,
            "Player animation origin and depth sort align with the feet instead of the top-right of the body");
        var buildings = UnityEngine.Object.FindObjectsOfType<PolygonCollider2D>().Where(p => p.transform.Find("NavigationObstacle") != null).ToArray();
        check(buildings.Length == 30, "Thirty buildings have ground footprints");
        check(buildings.Count(b => MapController.Instance.GetRegionAt(b.transform.position) == 1) == 6 &&
            buildings.Count(b => MapController.Instance.GetRegionAt(b.transform.position) == 2) == 10 &&
            buildings.Count(b => MapController.Instance.GetRegionAt(b.transform.position) == 3) == 14,
            "Buildings are distributed 6 / 10 / 14 across the three regions");
        foreach (var polygon in buildings)
        {
            var proxy = polygon.transform.Find("NavigationObstacle").GetComponent<MeshCollider>();
            var actualPoints = Enumerable.Range(0, polygon.pathCount).SelectMany(i => polygon.GetPath(i)).ToArray();
            check(actualPoints.All(p => proxy.sharedMesh.vertices.Any(v => Vector2.Distance(p + polygon.offset, new Vector2(v.x, v.y)) < .001f)),
                polygon.name + " navigation uses the actual collision outline");
        }
        var feet = tower.transform.Find("TowerGroundFootprint");
        check(feet != null && !feet.GetComponent<BoxCollider2D>().isTrigger && feet.GetComponent<NavMeshObstacle>().carving,
            "Central tower base has solid collision and dynamic navigation carving");
        check(UnityEngine.Object.FindObjectsOfType<BuildingRegionVisibility>().Where(v => v.name.StartsWith("Route03")).All(v => !v.GetComponent<SpriteRenderer>().enabled),
            "Locked outer-ring buildings remain hidden until unlocked");
        player.transform.position = new Vector3(26, 0, 0);
        check(Mathf.Approximately(player.GetMoveSpeed(), 1.6f), "Expanded second region keeps its correct movement speed at 26 metres");
        player.transform.position = original;
        var house = buildings.Single(p => p.name == "House1");
        Bounds bounds = house.bounds;
        Vector3 start = new Vector3(bounds.min.x - .6f, bounds.center.y, 0);
        Vector3 goal = new Vector3(bounds.max.x + .6f, bounds.center.y, 0);
        var path = new NavMeshPath();
        check(NavMesh.CalculatePath(start, goal, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 2,
            "Navigation bends around the house instead of crossing the wall");
        body.position = start; player.transform.position = start; Physics2D.SyncTransforms();
        int corner = 1;
        float deadline = Time.realtimeSinceStartup + 15;
        while (Vector2.Distance(body.position, goal) > .16f)
        {
            if (!GameFlowManager.Instance.IsPlaying) GameFlowManager.Instance.ResumeGame();
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Player could not physically walk around House1.");
            while (corner < path.corners.Length && Vector2.Distance(body.position, path.corners[corner]) < .08f) corner++;
            Vector2 waypoint = corner < path.corners.Length ? (Vector2)path.corners[corner] : (Vector2)goal;
            player.SetReviewMovement((waypoint - body.position).normalized);
            if (house.OverlapPoint(body.position)) throw new Exception("Player crossed the house collision outline.");
            yield return null;
        }
        player.SetReviewMovement(null); player.StopMovement();
        check(Vector2.Distance(body.position, goal) < .16f, "Actual Rigidbody movement reaches the other side of the house");
        var archery=buildings.First(p=>p.name=="Archery");
        var sprite=archery.GetComponent<SpriteRenderer>().sprite;
        Vector2 courtyardLocal=new Vector2(63-sprite.pivot.x, sprite.rect.height-200-sprite.pivot.y)/sprite.pixelsPerUnit;
        Vector2 courtyard=archery.transform.TransformPoint(courtyardLocal);
        check(!archery.OverlapPoint(courtyard) && ReachableGround.IsOpen(courtyard,.225f),
            "Archery courtyard is open at the actual source-art courtyard, not an invisible horizontal wall");
        Vector2 entrance=archery.transform.TransformPoint(courtyardLocal+Vector2.down*.82f);
        body.position=entrance; player.transform.position=entrance; Physics2D.SyncTransforms();
        deadline=Time.realtimeSinceStartup+8;
        while(Vector2.Distance(body.position,courtyard)>.08f)
        {
            if(Time.realtimeSinceStartup>deadline) throw new Exception("Player blocked at the visible archery entrance.");
            player.SetReviewMovement((courtyard-body.position).normalized);
            yield return null;
        }
        player.SetReviewMovement(null); player.StopMovement();
        check(true,"Actual player walks through the visible archery gate into the courtyard");
        ScreenCapture.CaptureScreenshot(".utmp/ui-checks/11-building-route.png");
        for (int i = 0; i < 4; i++) yield return null;
        body.position = original; player.transform.position = original; Physics2D.SyncTransforms();
        health.RestoreFullHealth();
        for (int i = 0; i < 12; i++) yield return null;
    }
}
