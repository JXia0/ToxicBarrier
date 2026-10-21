using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in import preparation and navigation/field-supply integration checks.</summary>
[InitializeOnLoad]
internal static class ExplorationMapChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static ExplorationMapChecks() { EditorApplication.update += Prepare; }
    private static void Prepare()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Temp/ToxicBarrierMap.prepare")) return;
        File.Delete("Temp/ToxicBarrierMap.prepare");
        try
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Presentation/Chinese.asset");
            string copy = File.ReadAllText("Assets/Scripts/Core/GameText.cs") + File.ReadAllText("Assets/Scripts/Core/GameHudUI.cs") +
                File.ReadAllText("Assets/Scripts/Map/ExplorationMapUI.cs");
            string missing = new string(copy.Where(c => c > 127 && !font.HasCharacter(c)).Distinct().ToArray());
            if (missing.Length > 0 && !font.TryAddCharacters(missing, out string unavailable)) throw new Exception("Missing glyphs: " + unavailable);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            File.WriteAllText("Temp/ToxicBarrierMap.result", "PASS");
        }
        catch (Exception e) { File.WriteAllText("Temp/ToxicBarrierMap.result", "FAIL\n" + e); }
    }
    private static void Place(HealthSystem player, Vector2 point)
    { player.transform.position = point; player.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms(); }
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);

    public static IEnumerator Run(HealthSystem player, Tower tower, Action<bool,string> check)
    {
        var nav = ExplorationMapUI.Instance; var flow = GameFlowManager.Instance; var map = MapController.Instance;
        Vector3 original = player.transform.position;
        check(nav != null && nav.Zones.Length == 3, "Navigation retains all three authored map rings");
        tower.SetLevel(0);
        check(map.IsMapUnlocked(1) && !map.IsMapUnlocked(2) && !map.IsMapUnlocked(3), "Navigation does not bypass initial region locks");
        tower.SetLevel(2);
        check(map.IsMapUnlocked(2) && !map.IsMapUnlocked(3), "Tower level two still unlocks only region two");
        tower.SetLevel(3);
        check(map.IsMapUnlocked(3), "Tower level three unlocks the final ring");
        var outer = nav.Zones.Single(z => z.MapLevel == 3);
        var scale = outer.transform.localScale; float radius = nav.OuterRadius;
        outer.transform.localScale = scale * 1.1f; Physics2D.SyncTransforms();
        check(Mathf.Abs(nav.OuterRadius - radius * 1.1f) < .01f, "Map geometry follows resized collider bounds");
        outer.transform.localScale = scale; Physics2D.SyncTransforms();
        foreach (var zone in nav.Zones.Where(z => z.MapLevel > 1))
        {
            var layer = zone.GetComponent<GroundDetailLayer>();
            var renderer = zone.GetComponent<SpriteRenderer>();
            var mat = renderer.sharedMaterial;
            var sprite = renderer.sprite;
            check(layer != null && layer.UsesHighResolution && !layer.HasDetail && mat.shader.isSupported && !UnityEditor.ShaderUtil.ShaderHasError(mat.shader),
                "HD terrain uses the normal sprite material without a second reconstruction filter: " + zone.MapLevel);
            check(sprite.texture.width == 4096 && sprite.texture.height == 4096 && Mathf.Abs(sprite.pixelsPerUnit - 4096) < .01f &&
                Mathf.Abs(sprite.rect.width / sprite.pixelsPerUnit - 1) < .0001f,
                "HD terrain imports at full resolution while retaining its original local size: " + zone.MapLevel);
            check(sprite.texture.mipmapCount > 1 && !sprite.texture.isReadable && sprite.texture.format == TextureFormat.BC7,
                "HD terrain has mipmaps for the map UI and compressed GPU storage without a CPU copy: " + zone.MapLevel);
            check(!mat.HasProperty("_DetailTex") && sprite.name == "Sprites_map_" + zone.MapLevel, "Original terrain artwork retained, without replacement tiles: " + zone.MapLevel);
        }
        nav.Discovered.Clear();
        var supply = ExplorationSupply.Available.First(s => !s.IsTowerRepair && s.Region == 2);
        nav.Discover((Vector2)supply.transform.position + Vector2.one * 100);
        check(!nav.Discovered.Contains(supply), "Unvisited supplies stay hidden from navigation");
        nav.Discover(supply.transform.position);
        check(nav.Discovered.Contains(supply), "Nearby supplies become discovered map markers");
        var reward = RegionSupplyCache.Spawn(tower, 2);
        check(map.GetRegionAt(reward.transform.position) == 2 && ReachableGround.TryFind(reward.transform.position,.1f,out _),
            "Region-two clear reward requires a reachable trip into region two");
        UnityEngine.Object.Destroy(reward.gameObject);
        nav.Toggle();
        for (int i=0;i<5;i++) yield return null;
        check(nav.IsExpanded && flow.CurrentState == GameState.Paused && Time.timeScale == 0, "Expanded map pauses gameplay");
        var mapMesh = GameObject.Find("FullMapGeometry").GetComponent<CanvasRenderer>().GetMesh();
        check(mapMesh.vertexCount > 500, "Full map submits actual ring and marker geometry to the canvas");
        foreach(var terrain in UnityEngine.Object.FindObjectsOfType<MapTerrainGraphic>().Where(t=>t.Full))
        {
            var mesh=terrain.canvasRenderer.GetMesh();
            check(mesh!=null && mesh.vertexCount>0 && terrain.mainTexture.name.StartsWith("Sprites_map_"),
                "Actual terrain drawn: "+terrain.name+" / "+terrain.mainTexture.name);
            File.AppendAllText(".utmp/ui-checks/terrain-diagnostic.txt",terrain.name+" tex "+terrain.mainTexture.name+" uv "+
                (mesh==null?"no mesh":string.Join(";",mesh.uv.Take(6)))+"\n");
        }
        var pause = (GameObject)typeof(GameFlowUI).GetField("pausePanel",Private).GetValue(UnityEngine.Object.FindObjectOfType<GameFlowUI>());
        check(!pause.activeSelf, "Expanded map does not overlap the pause menu");
        foreach (bool english in new[] {true,false})
        {
            GameText.SetEnglish(english); Call(nav,"RefreshLabels"); Canvas.ForceUpdateCanvases();
            var legend = GameObject.Find("MapLegend").GetComponent<TextMeshProUGUI>(); legend.ForceMeshUpdate();
            check(!legend.isTextOverflowing && (english ? legend.text.Contains("Supplies") : legend.text.Contains("补给")),
                "Full map labels fit and switch language: " + english);
            foreach(var label in GameObject.Find("ExpandedMap").GetComponentsInChildren<TextMeshProUGUI>())
            { label.ForceMeshUpdate(); check(!label.isTextOverflowing,"Map layout fits " + label.name + " / " + english); }
            ScreenCapture.CaptureScreenshot(".utmp/ui-checks/map-" + (english ? "english" : "chinese") + ".png");
            for(int i=0;i<5;i++) yield return null;
        }
        GameText.SetEnglish(true);
        check(nav.Close() && flow.IsPlaying && Time.timeScale == 1, "Closing navigation resumes gameplay");
        Place(player,tower.transform.position+Vector3.down*1.6f);
        for(int i=0;i<8;i++) yield return null;
        check(UnityEngine.Object.FindObjectOfType<TowerUpgradePanelUI>().IsVisible && !nav.MiniVisible,
            "Upgrade panel hides the minimap instead of being covered by it");
        ScreenCapture.CaptureScreenshot(".utmp/ui-checks/map-upgrade-no-overlap.png");
        yield return null;
        Place(player,original);
        flow.PauseGame(); nav.Toggle();
        check(!nav.IsExpanded && pause.activeSelf, "Ordinary pause keeps its own simple menu");
        flow.ResumeGame();
        var cargo = player.GetComponent<TowerRepairCargo>();
        check(cargo != null && !cargo.IsCarrying, "Navigation test starts with an empty field-supply slot");
        int wood = player.GetComponent<PlayerInventory>().GetCount(LootManager.ResourceType.Wood);
        Vector2 supplyPoint = supply.transform.position;
        Place(player,supplyPoint);
        Call(supply,"TryCollect",player.GetComponent<PlayerController>(),false);
        yield return null;
        check(supply == null && cargo.IsCarrying && cargo.GrantsProtection && player.GetComponent<PlayerInventory>().GetCount(LootManager.ResourceType.Wood)==wood,
            "Max-level side supplies become finite defensive cargo instead of unused upgrade materials");
        check(!cargo.TryLoad(true), "Defensive cargo still has one-parcel capacity");
        Place(player,original); tower.Health.RestoreFullHealth(); Call(cargo,"Update");
        check(cargo.IsCarrying, "Healthy tower preserves defensive cargo for later");
        tower.Health.TakeDamage(tower.Health.MaxHp*.3f,null);
        float before=tower.Health.CurrentHp;
        Place(player,tower.transform.position + Vector3.down); Call(cargo,"Update");
        check(!cargo.IsCarrying && Mathf.Approximately(tower.Health.CurrentHp,before+tower.Health.MaxHp*.15f) && tower.Health.TowerProtectionSeconds > 11,
            "Returning defensive cargo repairs fifteen percent and grants a twelve-second ward");
        before=tower.Health.CurrentHp; tower.Health.TakeDamage(100,null);
        check(Mathf.Abs(before-tower.Health.CurrentHp-65)<.01f, "Field ward reduces actual tower damage by thirty-five percent");
        flow.PauseGame(); float ward=tower.Health.TowerProtectionSeconds;
        for(int i=0;i<5;i++) yield return null;
        check(Mathf.Approximately(ward,tower.Health.TowerProtectionSeconds), "Pause preserves tower ward duration");
        flow.ResumeGame();
        typeof(HealthSystem).GetField("_towerProtectionUntil",Private).SetValue(tower.Health,Time.time-1);
        before=tower.Health.CurrentHp; tower.Health.TakeDamage(100,null);
        check(Mathf.Approximately(before-tower.Health.CurrentHp,100), "Expired ward restores normal incoming damage");
        nav.Discover(original); check(!nav.Discovered.Contains(supply), "Collected supply marker is removed from navigation");
        tower.Health.RestoreFullHealth();
        foreach(int region in new[]{2,3})
        {
            ReachableGround.TryFind(new Vector2(region==2?22:38,0),3,out Vector2 spot);
            Place(player,spot);
            float end=Time.time+1.5f; while(Time.time<end) yield return null;
            check(nav.MiniVisible, "Minimap returns while exploring region " + region);
            var mini=GameObject.Find("MiniMap").GetComponent<RectTransform>();
            check(mini.anchorMin == Vector2.zero && !ExplorationMapUI.ScreenBounds(mini).Overlaps(ExplorationMapUI.ScreenBounds(GameObject.Find("JourneyStatus").GetComponent<RectTransform>())),
                "Bottom-left map does not overlap the journey status: region " + region);
            var renderer=nav.Zones.Single(z=>z.MapLevel==region).GetComponent<SpriteRenderer>();
            var current=renderer.sprite;
            string originalPath=$".utmp/super-resolution/originals/Sprites_map_{region}.png";
            // Optional A/B evidence uses a transient sprite, never reimports or rewrites the scene.
            if(File.Exists(originalPath))
            {
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                texture.LoadImage(File.ReadAllBytes(originalPath)); texture.filterMode=FilterMode.Point;
                var originalSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,texture.width);
                renderer.sprite=originalSprite;
                RefreshTerrainGraphics();
                for(int i=0;i<3;i++) yield return null;
                ScreenCapture.CaptureScreenshot(".utmp/ui-checks/region-"+region+"-before.png");
                for(int i=0;i<3;i++) yield return null;
                renderer.sprite=current;
                RefreshTerrainGraphics();
                UnityEngine.Object.Destroy(originalSprite); UnityEngine.Object.Destroy(texture);
            }
            for(int i=0;i<3;i++) yield return null;
            ScreenCapture.CaptureScreenshot(".utmp/ui-checks/region-"+region+"-after.png");
            for(int i=0;i<3;i++) yield return null;
        }
        Place(player,original); player.RestoreFullHealth();
        typeof(HealthSystem).GetField("_toxicProtectionUntil",Private).SetValue(player,0f);
    }

    private static void RefreshTerrainGraphics()
    {
        foreach(var graphic in UnityEngine.Object.FindObjectsOfType<MapTerrainGraphic>()) graphic.SetAllDirty();
        Canvas.ForceUpdateCanvases();
    }
}
