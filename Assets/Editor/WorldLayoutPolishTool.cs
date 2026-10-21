using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit, repeatable layout pass using the already selected building art.</summary>
[InitializeOnLoad]
public static class WorldLayoutPolishTool
{
    private const string Root = "Assets/PeterRadiant/Tiny Swords/Buildings/Black Buildings/";
    static WorldLayoutPolishTool() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists("Temp/ToxicBarrierWorld.polish")) return;
        File.Delete("Temp/ToxicBarrierWorld.polish");
        try { Apply(); }
        catch (Exception e) { File.WriteAllText("Temp/ToxicBarrierWorld.result", "FAIL\n" + e); Debug.LogException(e); }
    }

    [MenuItem("Tools/Toxic Barrier/Polish Routes and Building Footprints")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        EditorSceneManager.SaveOpenScenes();
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        SceneManager.SetActiveScene(scene);
        Directory.CreateDirectory(".utmp/layout-backup");
        File.Copy("Assets/Scenes/GameScene.unity", ".utmp/layout-backup/GameScene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", false);
        MapZone[] zones = Objects<MapZone>(scene).OrderBy(z => z.MapLevel).ToArray();
        // Offset pairs create choices around each block, while the cardinal approaches remain broad.
        Add(scene, "Route01_Northwest", "House3", new Vector2(-7, 6), 1);
        Add(scene, "Route01_Southeast", "House1", new Vector2(6, -5), 1);
        Add(scene, "Route02_WestA", "Archery", RingPoint(zones[1], .40f, 150), 2);
        Add(scene, "Route02_WestB", "House1", RingPoint(zones[1], .58f, 159), 2);
        Add(scene, "Route02_EastA", "House3", RingPoint(zones[1], .40f, -35), 2);
        Add(scene, "Route02_EastB", "House1", RingPoint(zones[1], .62f, -43), 2);
        Add(scene, "Route03_SouthwestA", "House3", RingPoint(zones[2], .38f, 200), 3, BuildingRegionVisibility.SupplyKind.TowerRepair);
        Add(scene, "Route03_SouthwestB", "Archery", RingPoint(zones[2], .58f, 207), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_NortheastA", "House1", RingPoint(zones[2], .38f, 35), 3, BuildingRegionVisibility.SupplyKind.TowerRepair);
        Add(scene, "Route03_NortheastB", "House3", RingPoint(zones[2], .60f, 29), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route02_NorthA", "Archery", RingPoint(zones[1], .38f, 70), 2, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route02_NorthB", "House1", RingPoint(zones[1], .57f, 77), 2, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route02_SouthA", "House3", RingPoint(zones[1], .43f, 235), 2, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route02_SouthB", "House1", RingPoint(zones[1], .62f, 244), 2, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route02_GatewayA", "House1", RingPoint(zones[1], .25f, 8), 2, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route02_GatewayB", "House3", RingPoint(zones[1], .43f, 17), 2, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_NorthwestA", "Archery", RingPoint(zones[2], .20f, 130), 3, BuildingRegionVisibility.SupplyKind.TowerRepair);
        Add(scene, "Route03_NorthwestB", "House3", RingPoint(zones[2], .37f, 138), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_SouthA", "House3", RingPoint(zones[2], .26f, 275), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_SouthB", "House1", RingPoint(zones[2], .44f, 282), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_WestA", "House1", RingPoint(zones[2], .65f, 170), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_WestB", "House3", RingPoint(zones[2], .80f, 176), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_NorthA", "House3", RingPoint(zones[2], .68f, 80), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_NorthB", "House1", RingPoint(zones[2], .82f, 86), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_SoutheastA", "Archery", RingPoint(zones[2], .30f, 320), 3, BuildingRegionVisibility.SupplyKind.None);
        Add(scene, "Route03_SoutheastB", "House1", RingPoint(zones[2], .46f, 328), 3, BuildingRegionVisibility.SupplyKind.None);
        var buildings = Objects<SpriteRenderer>(scene).Where(s => s.sprite != null && AssetDatabase.GetAssetPath(s.sprite).StartsWith(Root)).ToArray();
        foreach (var renderer in buildings)
        {
            Fit(renderer);
            BuildingNavigationGeometry.Sync(renderer.gameObject, renderer.GetComponent<PolygonCollider2D>());
            SetDepthAnchor(renderer);
        }
        foreach (var camera in Objects<Camera>(scene))
        {
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = Vector3.up;
        }
        Tower tower = Objects<Tower>(scene).Single();
        var towerData = new SerializedObject(tower);
        towerData.FindProperty("attackRangeFromMap1Half").boolValue = false;
        towerData.FindProperty("manualAttackRange").floatValue = 4.75f;
        towerData.ApplyModifiedPropertiesWithoutUndo();
        if (tower.GetComponent<TowerNavigationFootprint>() == null) tower.gameObject.AddComponent<TowerNavigationFootprint>();
        StyleTimer(scene);
        NavMeshSurface surface = Objects<NavMeshSurface>(scene).Single();
        zones.Last().GetRingParams(out Vector2 mapCenter, out _, out float mapRadius);
        var ground = Objects<BoxCollider>(scene).Single(c => c.name == "NavigationGround");
        ground.transform.position = new Vector3(mapCenter.x, mapCenter.y, 0);
        ground.size = new Vector3((mapRadius + 2) * 2, (mapRadius + 2) * 2, .1f);
        surface.overrideVoxelSize = true;
        surface.voxelSize = .04f;
        NavMesh2DSetupTool.BakeNavigation(surface);
        Physics2D.SyncTransforms();
        var report = Validate(buildings, zones);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/Review/2026-09-20");
        File.WriteAllText("output/Review/2026-09-20/expanded-navigation.txt", report);
        File.WriteAllText("Temp/ToxicBarrierWorld.result", "PASS\n" + report);
    }

    private static Vector2 RingPoint(MapZone zone, float fraction, float angle)
    {
        zone.GetRingParams(out Vector2 center, out float inner, out float outer);
        return center + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * Mathf.Lerp(inner, outer, fraction);
    }

    private static void Add(Scene scene, string name, string art, Vector2 position, int region,
        BuildingRegionVisibility.SupplyKind supplies = BuildingRegionVisibility.SupplyKind.Materials)
    {
        var found = Objects<SpriteRenderer>(scene).FirstOrDefault(s => s.name == name);
        GameObject obj = found != null ? found.gameObject : new GameObject(name);
        if (found == null)
        {
            SceneManager.MoveGameObjectToScene(obj, scene);
            obj.transform.position = position;
            obj.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + art + ".png");
        }
        var component = obj.GetComponent<BuildingRegionVisibility>();
        if (component == null) component = obj.AddComponent<BuildingRegionVisibility>();
        var visibility = new SerializedObject(component);
        visibility.FindProperty("region").intValue = region;
        visibility.FindProperty("supplyKind").enumValueIndex = (int)supplies;
        visibility.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Fit(SpriteRenderer renderer)
    {
        var obj = renderer.gameObject;
        obj.layer = LayerMask.NameToLayer("Obstacle");
        foreach (BoxCollider2D old in obj.GetComponents<BoxCollider2D>()) UnityEngine.Object.DestroyImmediate(old);
        var footprint = obj.GetComponent<PolygonCollider2D>();
        if (footprint == null) footprint = obj.AddComponent<PolygonCollider2D>();
        // Pixel coordinates measured against the ground-contact masonry, excluding roof overhangs.
        string name = renderer.sprite.name;
        Vector2[][] paths;
        if (name.StartsWith("Archery"))
            paths = new[] {
                Points(10,176, 24,176, 24,216, 10,216),
                Points(10,218, 44,218, 44,235, 10,235),
                Points(81,219, 113,219, 113,235, 81,235),
                Points(117,216, 165,216, 173,228, 165,236, 116,236, 110,228) };
        else if (name.StartsWith("House1"))
            paths = new[] { Points(23,153, 107,153, 110,163, 101,171, 25,171, 18,164) };
        else if (name.StartsWith("House3"))
            paths = new[] { Points(19,151, 110,151, 115,161, 106,171, 21,171, 13,162) };
        else if (name.StartsWith("Castle"))
            paths = new[] { Points(28,187, 288,187, 302,204, 281,224, 186,228, 165,242, 141,242, 123,228, 36,225, 18,207) };
        else throw new InvalidOperationException("A ground footprint must be authored for " + name);
        // Castle has a shallow doorway protrusion; split it into convex paths for the mesh extrusion.
        if (name.StartsWith("Castle")) paths = new[] {
            Points(28,209, 289,209, 299,218, 280,231, 39,231, 22,221),
            Points(127,229, 184,229, 176,242, 141,245, 129,238) };
        footprint.pathCount = paths.Length;
        footprint.offset = Vector2.zero;
        footprint.isTrigger = false;
        for (int p = 0; p < paths.Length; p++)
        {
            Sprite sprite = renderer.sprite;
            Vector2[] local = paths[p].Select(point => Vector2.Scale(new Vector2(
                point.x - sprite.rect.x - sprite.pivot.x,
                sprite.texture.height - point.y - sprite.rect.y - sprite.pivot.y) / sprite.pixelsPerUnit,
                new Vector2(renderer.flipX ? -1 : 1, renderer.flipY ? -1 : 1))).ToArray();
            Array.Reverse(local); // Input is clockwise in screen coordinates; store CCW in world XY.
            footprint.SetPath(p, local);
        }
        renderer.sortingLayerName = SortingLayerNames.Environment;
        renderer.sortingOrder = 0;
    }

    private static Vector2[] Points(params float[] xy)
    {
        var points = new Vector2[xy.Length / 2];
        for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        return points;
    }

    private static void SetDepthAnchor(SpriteRenderer renderer)
    {
        var polygon = renderer.GetComponent<PolygonCollider2D>();
        var group = renderer.GetComponentInParent<SortingGroup>();
        if (group != null) return;
        GameObject anchor = new GameObject(renderer.name + "_GroundSort");
        anchor.transform.SetParent(renderer.transform.parent, false);
        Vector2[] points = Enumerable.Range(0, polygon.pathCount).SelectMany(p => polygon.GetPath(p)).ToArray();
        float footY = (points.Min(p => p.y) + points.Max(p => p.y)) * .5f;
        anchor.transform.position = renderer.transform.TransformPoint(new Vector3(0, footY, 0));
        renderer.transform.SetParent(anchor.transform, true);
        group = anchor.AddComponent<SortingGroup>();
        group.sortingLayerName = SortingLayerNames.Characters;
        group.sortingOrder = 0;
    }

    public static void StyleTimer(Scene scene)
    {
        var timer = Objects<TextMeshProUGUI>(scene).FirstOrDefault(t => t.name == "RunTimer");
        if (timer == null) return;
        RectTransform hud = Objects<Canvas>(scene).Single(c => c.name == "HUDCanvas").transform as RectTransform;
        var found = hud.Find("RunStatusBar") as RectTransform;
        RectTransform bar = found;
        if (bar == null)
        {
            GameObject obj = new GameObject("RunStatusBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bar = obj.GetComponent<RectTransform>(); bar.SetParent(hud, false);
            var border = obj.GetComponent<Image>(); border.color = new Color(.48f, .4f, .25f, .8f); border.raycastTarget = false;
            GameObject insetObj = new GameObject("Inset", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var inset = insetObj.GetComponent<RectTransform>(); inset.SetParent(bar, false);
            inset.anchorMin = Vector2.zero; inset.anchorMax = Vector2.one;
            inset.offsetMin = Vector2.one * 2; inset.offsetMax = -Vector2.one * 2;
            insetObj.GetComponent<Image>().color = new Color(.075f, .085f, .075f, .94f);
            insetObj.GetComponent<Image>().raycastTarget = false;
        }
        bar.anchorMin = bar.anchorMax = new Vector2(.48f, 1);
        bar.pivot = new Vector2(.5f, 1); bar.anchoredPosition = new Vector2(0, -24);
        bar.sizeDelta = new Vector2(335, 48);
        timer.rectTransform.SetParent(bar, false);
        timer.rectTransform.anchorMin = Vector2.zero; timer.rectTransform.anchorMax = Vector2.one;
        timer.rectTransform.offsetMin = new Vector2(12, 4); timer.rectTransform.offsetMax = new Vector2(-12, -4);
        timer.fontSize = 20; timer.enableAutoSizing = true; timer.fontSizeMin = 16; timer.fontSizeMax = 20;
        timer.color = new Color(.86f, .81f, .65f); timer.alignment = TextAlignmentOptions.Center; timer.raycastTarget = false;
    }

    private static string Validate(SpriteRenderer[] buildings, MapZone[] zones)
    {
        var lines = new List<string> { "Buildings: " + buildings.Length, "NavMesh voxel: 0.04; agent radius: 0.25; tower range: 4.75" };
        foreach (var building in buildings)
        {
            var polygon = building.GetComponent<PolygonCollider2D>();
            var proxy = building.transform.Find("NavigationObstacle").GetComponent<MeshCollider>();
            if (proxy == null || proxy.sharedMesh == null || building.GetComponent<BoxCollider2D>() != null)
                throw new Exception("Unsynchronized footprint: " + building.name);
            // A walkable circuit outside each footprint must connect on all four sides.
            Bounds b = polygon.bounds;
            Vector3[] corners = { new Vector3(b.min.x - .6f, b.min.y - .6f), new Vector3(b.max.x + .6f, b.min.y - .6f),
                new Vector3(b.max.x + .6f, b.max.y + .6f), new Vector3(b.min.x - .6f, b.max.y + .6f) };
            for (int i = 0; i < 4; i++) RequirePath(corners[i], corners[(i + 1) % 4], building.name + " side " + i);
            lines.Add("OK " + building.name + ": matching polygon/proxy, four connected sides; footprint " + b.size);
        }
        for (int ring = 1; ring <= 3; ring++)
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                zones[ring - 1].GetRingParams(out Vector2 center, out float inner, out float outer);
                float radius = Mathf.Lerp(inner, outer, .85f);
                RequirePath(new Vector3(0, -.5f), center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius), "Ring " + ring + " approach " + i);
            }
        lines.Add("OK 36 routes between the central area and all three rings");
        return string.Join("\n", lines);
    }

    private static void RequirePath(Vector3 from, Vector3 to, string label)
    {
        var route = new NavMeshPath();
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, .45f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(to, out NavMeshHit end, .45f, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, route) || route.status != NavMeshPathStatus.PathComplete)
            throw new Exception("No complete navigation route: " + label + " from " + from + " to " + to);
    }
    private static T[] Objects<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
}
