using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds editable, scene-authored UI from the project's Tiny Swords artwork.</summary>
[InitializeOnLoad]
public static class ReleaseUISetupTool
{
    private const string Art = "Assets/PeterRadiant/Tiny Swords/UI Elements/";
    private const string Request = "Temp/ToxicBarrierUI.setup";
    private static readonly Color Ink = new Color(0.055f, 0.09f, 0.085f, 0.97f);
    private static readonly Color Cream = new Color(0.96f, 0.92f, 0.8f);
    private static readonly Color Gold = new Color(0.96f, 0.77f, 0.4f);
    private static TMP_FontAsset _font;
    private static bool _busy;

    static ReleaseUISetupTool() { EditorApplication.update += ProcessRequest; }

    private static void ProcessRequest()
    {
        if (_busy || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        _busy = true;
        File.Delete(Request);
        try
        {
            SetupAllScenes();
            File.WriteAllText("Temp/ToxicBarrierUI.setup-result", "PASS: MainMenu and GameScene UI saved and validated.");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("Temp/ToxicBarrierUI.setup-result", "FAIL: " + e);
        }
        finally { _busy = false; }
    }

    [MenuItem("Tools/Toxic Barrier/Build Finished UI")]
    public static void SetupAllScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before building UI.");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/PeterRadiant/SoftTouch_UI/SoftTouch/Font/Carlito-Bold SDF.asset");
        if (_font == null) _font = TMP_Settings.defaultFontAsset;
        BuildScene("Assets/Scenes/MainMenu.unity", true);
        BuildScene("Assets/Scenes/GameScene.unity", false);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Finished UI saved: menus, health, inventory, tower upgrades, victory and defeat.");
    }

    private static void BuildScene(string path, bool menu)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool loaded = scene.IsValid() && scene.isLoaded;
        if (loaded && scene.isDirty && !EditorSceneManager.SaveScene(scene))
            throw new IOException("Could not preserve current scene changes: " + path);
        Directory.CreateDirectory(".utmp/ui-backup");
        string backup = ".utmp/ui-backup/" + Path.GetFileName(path);
        if (!File.Exists(backup) && File.Exists(path)) File.Copy(path, backup);
        if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            string[] generated = { "GameFlowManager", "GameFlowUI", "GameFlowEventSystem", "GameplayUIRoot",
                "HealthCanvas", "HelathControl", "InventoryUI", "TowerUpgradeUI" };
            foreach (string name in generated)
            {
                Transform[] matches = Objects<Transform>(scene).Where(t => t.name == name).ToArray();
                foreach (Transform match in matches) if (match != null) UnityEngine.Object.DestroyImmediate(match.gameObject);
            }
            EventSystem[] systems = Objects<EventSystem>(scene);
            if (systems.Length == 0) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            else for (int i = 1; i < systems.Length; i++) UnityEngine.Object.DestroyImmediate(systems[i].gameObject);
            new GameObject("GameFlowManager", typeof(GameFlowManager));
            GameObject root = new GameObject("GameFlowUI", typeof(GameFlowUI));
            GameFlowUI flow = root.GetComponent<GameFlowUI>();
            RectTransform canvas = CanvasRoot("GameFlowCanvas", root.transform, 200);
            if (menu) BuildMenu(canvas, flow);
            else
            {
                BuildPause(canvas, flow);
                BuildResult(canvas, flow, false);
                BuildResult(canvas, flow, true);
                Button pause = ButtonAt(canvas, "PauseButton", "PAUSE  [ESC]", new Vector2(1, 1), new Vector2(-32, -24), new Vector2(180, 72), flow.PauseGame);
                Ref(flow, "pauseButton", pause.gameObject);
                BuildGameplay(scene);
                SimpleMainMenuTool.ConfigurePause(scene);
            }
            RectTransform loading = Screen(canvas, "LoadingPanel", new Color(0.03f, 0.065f, 0.06f, 1));
            TextAt(loading, "LoadingText", "ENTERING THE BARRIER...", 38, Cream, new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 100));
            Ref(flow, "loadingPanel", loading.gameObject);
            loading.gameObject.SetActive(false);
            ValidateScene(scene, menu);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (!loaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void BuildMenu(RectTransform canvas, GameFlowUI flow)
    {
        SimpleMainMenuTool.Configure(canvas.gameObject.scene);
    }

    private static void BuildPause(RectTransform canvas, GameFlowUI flow)
    {
        RectTransform overlay = Screen(canvas, "PauseMenuPanel", new Color(.015f, .03f, .025f, .8f));
        RectTransform card = Card(overlay, "PauseCard", new Vector2(.5f, .5f), Vector2.zero, new Vector2(690, 710));
        TextAt(card, "Title", "TAKE A BREATH", 51, Cream, new Vector2(.5f, 1), new Vector2(0, -85), new Vector2(600, 80));
        TextAt(card, "Subtitle", "The world is paused.", 26, Gold, new Vector2(.5f, 1), new Vector2(0, -172), new Vector2(580, 42));
        ButtonAt(card, "ResumeButton", "RESUME", new Vector2(.5f, 1), new Vector2(0, -250), new Vector2(470, 86), flow.ResumeGame);
        ButtonAt(card, "RestartButton", "RESTART RUN", new Vector2(.5f, 1), new Vector2(0, -355), new Vector2(470, 86), flow.RestartGame);
        ButtonAt(card, "MainMenuButton", "MAIN MENU", new Vector2(.5f, 1), new Vector2(0, -460), new Vector2(470, 86), flow.ReturnToMainMenu, true);
        TextAt(card, "Hint", "WASD Move | E Upgrade near tower | ESC Resume", 22, Cream, new Vector2(.5f, 1), new Vector2(0, -595), new Vector2(590, 42));
        Ref(flow, "pausePanel", overlay.gameObject);
        overlay.gameObject.SetActive(false);
    }

    private static void BuildResult(RectTransform canvas, GameFlowUI flow, bool won)
    {
        RectTransform overlay = Screen(canvas, won ? "VictoryPanel" : "DeathPanel", new Color(.015f, .03f, .025f, .88f));
        RectTransform card = Card(overlay, "ResultCard", new Vector2(.5f, .5f), Vector2.zero, new Vector2(920, 770));
        TextAt(card, "Eyebrow", won ? "ALL THREE REGIONS RECLAIMED" : "THE WILDS CLAIM ANOTHER", 25, Gold, new Vector2(.5f, 1), new Vector2(0, -85), new Vector2(810, 40));
        TextAt(card, "Title", won ? "BARRIER RESTORED" : "DEFENSE LOST", 58, won ? Cream : new Color(1, .65f, .53f), new Vector2(.5f, 1), new Vector2(0, -145), new Vector2(820, 90));
        TextMeshProUGUI reason = TextAt(card, "Reason", won ? "All waves defeated. Your tower still stands." : "The barrier has fallen.", 29, Cream, new Vector2(.5f, 1), new Vector2(0, -260), new Vector2(790, 70));
        TextMeshProUGUI summary = TextAt(card, "RunSummary", "", 26, Gold, new Vector2(.5f, 1), new Vector2(0, -355), new Vector2(810, 55));
        ButtonAt(card, "RestartButton", "PLAY AGAIN", new Vector2(.5f, 1), new Vector2(0, -465), new Vector2(540, 88), flow.RestartGame);
        ButtonAt(card, "MainMenuButton", "MAIN MENU", new Vector2(.5f, 1), new Vector2(0, -570), new Vector2(540, 88), flow.ReturnToMainMenu, true);
        Ref(flow, won ? "victoryPanel" : "deathPanel", overlay.gameObject);
        Ref(flow, won ? "victorySummary" : "deathSummary", summary);
        if (!won) Ref(flow, "deathReason", reason);
        overlay.gameObject.SetActive(false);
    }

    private static void BuildGameplay(Scene scene)
    {
        Tower tower = Objects<Tower>(scene).Single();
        HealthSystem player = Objects<HealthSystem>(scene).Single(h => h.IsPlayer);
        PlayerInventory inventory = Objects<PlayerInventory>(scene).Single();
        for (int i = player.OnHpChanged.GetPersistentEventCount() - 1; i >= 0; i--)
            if (player.OnHpChanged.GetPersistentTarget(i) == null || player.OnHpChanged.GetPersistentTarget(i) is HealthControl)
                UnityEventTools.RemovePersistentListener(player.OnHpChanged, i);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        GameObject root = new GameObject("GameplayUIRoot", typeof(GameHudUI));
        RectTransform hud = CanvasRoot("HUDCanvas", root.transform, 10);
        RectTransform playerCard = Card(hud, "PlayerHealth", new Vector2(0, 1), new Vector2(24, -20), new Vector2(420, 165));
        TextAt(playerCard, "Label", "SURVIVOR", 24, Gold, new Vector2(.5f, 1), new Vector2(0, -30), new Vector2(330, 32));
        BuildHealthBar(playerCard, player, new Vector2(0, -78), new Vector2(325, 40));
        RectTransform towerCard = Card(hud, "TowerHealth", new Vector2(0, 1), new Vector2(24, -197), new Vector2(420, 153));
        TextMeshProUGUI towerLabel = TextAt(towerCard, "Label", "PURIFICATION TOWER  Lv.0", 22, Gold, new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(365, 30));
        BuildHealthBar(towerCard, tower.GetComponent<HealthSystem>(), new Vector2(0, -71), new Vector2(325, 40));
        RectTransform resources = Card(hud, "Resources", new Vector2(1, 1), new Vector2(-225, -20), new Vector2(515, 125));
        InventoryPanelUI bag = root.AddComponent<InventoryPanelUI>();
        Ref(bag, "inventory", inventory);
        Ref(bag, "panelRoot", resources);
        string[] names = { "Wood", "Stone", "Gel", "Teeth" };
        for (int i = 0; i < names.Length; i++)
        {
            float x = 35 + i * 112;
            ImageAt(resources, names[i] + "Icon", ResourceSprite(names[i]), new Vector2(0, 1), new Vector2(x + 35, -28), new Vector2(42, 42), false).preserveAspect = true;
            TextMeshProUGUI count = TextAt(resources, names[i] + "Count", names[i] + ": 0", 21, Cream, new Vector2(0, 1), new Vector2(x, -77), new Vector2(110, 28));
            Ref(bag, "text" + names[i], count);
        }
        RectTransform waves = Card(hud, "RegionProgress", new Vector2(1, 1), new Vector2(-24, -164), new Vector2(370, 195));
        TextMeshProUGUI waveText = TextAt(waves, "Waves", "REGION 1  0/10\nREGION 2  LOCKED\nREGION 3  LOCKED", 24, Cream, new Vector2(.5f, .5f), Vector2.zero, new Vector2(305, 120));
        waveText.enableAutoSizing = true;
        waveText.fontSizeMin = 18;
        waveText.fontSizeMax = 24;
        waveText.alignment = TextAlignmentOptions.Left;
        RectTransform footer = Card(hud, "JourneyStatus", new Vector2(.5f, 0), new Vector2(0, 18), new Vector2(1110, 137));
        TextMeshProUGUI objective = TextAt(footer, "Objective", "Gather materials and upgrade the tower to Lv.3", 25, Gold, new Vector2(.5f, 1), new Vector2(0, -25), new Vector2(1010, 36));
        TextMeshProUGUI threat = TextAt(footer, "PoisonStatus", "SAFE ZONE", 23, Cream, new Vector2(.5f, 1), new Vector2(0, -65), new Vector2(1010, 35));
        TextMeshProUGUI timer = TextAt(hud, "RunTimer", "00:00  |  0 defeated", 24, Cream, new Vector2(.42f, 1), new Vector2(0, -33), new Vector2(430, 40));
        GameHudUI view = root.GetComponent<GameHudUI>();
        Ref(view, "tower", tower); Ref(view, "towerText", towerLabel);
        Ref(view, "objectiveText", objective); Ref(view, "threatText", threat);
        Ref(view, "regionText", waveText); Ref(view, "timeText", timer);
        SerializedObject hudData = new SerializedObject(view);
        EnemySpawner[] spawners = Objects<EnemySpawner>(scene).OrderBy(s => s.name).ToArray();
        SerializedProperty array = hudData.FindProperty("spawners"); array.arraySize = spawners.Length;
        for (int i = 0; i < spawners.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = spawners[i];
        hudData.ApplyModifiedPropertiesWithoutUndo();
        BuildUpgrade(root, tower, inventory);
    }

    private static void BuildHealthBar(Transform parent, HealthSystem health, Vector2 pos, Vector2 size)
    {
        RectTransform frame = ImageAt(parent, "HealthBarFrame", SpriteAt(Art + "Bars/BigBar_Base.png"), new Vector2(.5f, 1), pos, size, true).rectTransform;
        RectTransform track = Rect(frame, "FillArea", new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(44, 16));
        Image fill = ImageAt(track, "HealthBar", SpriteAt(Art + "Bars/BigBar_Fill.png"), new Vector2(.5f, .5f), Vector2.zero, track.sizeDelta, false);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1;
        TextMeshProUGUI value = TextAt(frame, "HealthValue", "100 / 100", 23, Color.white, new Vector2(.5f, .5f), Vector2.zero, size);
        value.outlineWidth = .2f; value.outlineColor = new Color32(20, 25, 20, 255);
        HealthControl control = frame.gameObject.AddComponent<HealthControl>();
        Ref(control, "healthBar", fill.rectTransform); Ref(control, "fillImage", fill);
        Ref(control, "valueText", value); Ref(control, "health", health);
    }

    private static void BuildUpgrade(GameObject root, Tower tower, PlayerInventory inventory)
    {
        RectTransform canvas = CanvasRoot("UpgradeCanvas", root.transform, 30);
        RectTransform panel = Card(canvas, "TowerUpgradePanel", new Vector2(1, 0), new Vector2(-26, 160), new Vector2(550, 540));
        TowerUpgradePanelUI upgrade = root.AddComponent<TowerUpgradePanelUI>();
        Ref(upgrade, "tower", tower); Ref(upgrade, "inventory", inventory); Ref(upgrade, "panelRoot", panel);
        Image preview = ImageAt(panel, "TowerPreview", SpriteAt("Assets/Sprites/Sprites_tower.png"), new Vector2(.5f, 1), new Vector2(0, -38), new Vector2(82, 75), false);
        preview.preserveAspect = true; Ref(upgrade, "towerPreview", preview);
        Ref(upgrade, "textLine1", TextAt(panel, "TowerLevel", "Tower Level: 0  |  Next: Level 1", 25, Gold, new Vector2(.5f, 1), new Vector2(0, -122), new Vector2(455, 40)));
        Ref(upgrade, "textLine2", TextAt(panel, "StatsPreview", "ATK 50 > 150     HP 1000 > 2500", 23, Cream, new Vector2(.5f, 1), new Vector2(0, -166), new Vector2(460, 42)));
        Ref(upgrade, "textLine3", TextAt(panel, "Benefit", "Stronger defenses. Restores you and the tower.", 22, Cream, new Vector2(.5f, 1), new Vector2(0, -210), new Vector2(450, 60)));
        string[] names = { "Wood", "Stone", "Gel", "Teeth" };
        for (int i = 0; i < names.Length; i++)
        {
            float x = -119 + (i % 2) * 239;
            float y = -280 - (i / 2) * 45;
            ImageAt(panel, names[i] + "Icon", ResourceSprite(names[i]), new Vector2(.5f, 1), new Vector2(x - 73, y), new Vector2(34, 34), false).preserveAspect = true;
            TextMeshProUGUI label = TextAt(panel, names[i] + "Cost", names[i] + " 0 / 0", 23, Cream, new Vector2(.5f, 1), new Vector2(x + 20, y), new Vector2(185, 34));
            Ref(upgrade, char.ToLowerInvariant(names[i][0]) + names[i].Substring(1) + "CostText", label);
        }
        Button button = ButtonAt(panel, "UpgradeButton", "UPGRADE TOWER", new Vector2(.5f, 1), new Vector2(0, -385), new Vector2(415, 75), null);
        Ref(upgrade, "upgradeButton", button); Ref(upgrade, "upgradeButtonText", button.GetComponentInChildren<TextMeshProUGUI>());
        Ref(upgrade, "statusText", TextAt(panel, "Status", "Collect the missing materials", 22, Gold, new Vector2(.5f, 1), new Vector2(0, -480), new Vector2(460, 34)));
        SerializedObject data = new SerializedObject(upgrade);
        SerializedProperty sprites = data.FindProperty("towerSprites"); sprites.arraySize = 4;
        for (int i = 0; i < 4; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue =
            SpriteAt("Assets/Sprites/Sprites_tower" + (i == 1 ? "" : i.ToString()) + ".png");
        data.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
    }

    private static RectTransform CanvasRoot(string name, Transform parent, int order)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Canvas canvas = rect.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        CanvasScaler scaler = rect.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        return rect;
    }

    private static RectTransform Screen(Transform parent, string name, Color color)
    {
        RectTransform rect = Rect(parent, name, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = true;
        return rect;
    }

    private static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Image wood = ImageAt(parent, name, SpriteAt(Art + "Wood Table/WoodTable.png"), anchor, pos, size, true);
        wood.color = new Color(.74f, .8f, .7f); wood.raycastTarget = true;
        RectTransform inside = Rect(wood.transform, "DarkInset", new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(62, 60));
        Image inset = inside.gameObject.AddComponent<Image>(); inset.color = Ink; inset.raycastTarget = false;
        return wood.rectTransform;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x == 0 || anchor.x == 1 ? anchor.x : .5f,
            anchor.y == 0 || anchor.y == 1 ? anchor.y : .5f);
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        return rect;
    }

    private static Image ImageAt(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size, bool sliced)
    {
        Image image = Rect(parent, name, anchor, pos, size).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.raycastTarget = false;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 4;
        return image;
    }

    private static TextMeshProUGUI TextAt(Transform parent, string name, string value, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 dimensions)
    {
        TextMeshProUGUI text = Rect(parent, name, anchor, pos, dimensions).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.fontSize = size; text.text = value; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static Button ButtonAt(Transform parent, string name, string title, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction callback, bool danger = false)
    {
        Image image = ImageAt(parent, name, SpriteAt(Art + "Buttons/" + (danger ? "BigRedButton_Regular.png" : "BigBlueButton_Regular.png")), anchor, pos, size, true);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.15f, 1.15f, 1.05f);
        colors.pressedColor = new Color(.65f, .75f, .65f); colors.selectedColor = new Color(1.12f, 1.1f, .93f);
        colors.disabledColor = new Color(.42f, .45f, .42f, .8f); colors.fadeDuration = .08f; button.colors = colors;
        if (callback != null) UnityEventTools.AddPersistentListener(button.onClick, callback);
        TextMeshProUGUI label = TextAt(image.transform, "Text", title, size.y < 80 ? 24 : 30, Cream, new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(35, 10));
        label.outlineWidth = .15f; label.outlineColor = new Color32(25, 45, 35, 255);
        return button;
    }

    private static Sprite ResourceSprite(string name) { return SpriteAt("Assets/Sprites/Sprites_" + name.ToLowerInvariant() + ".png"); }
    private static Sprite SpriteAt(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("UI sprite missing: " + path);
        return sprite;
    }

    private static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.name + "." + field + " is missing");
        property.objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T[] Objects<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }

    public static void ValidateScene(Scene scene, bool menu)
    {
        if (Objects<EventSystem>(scene).Length != 1) throw new InvalidOperationException("Expected one EventSystem: " + scene.name);
        if (Objects<GameFlowManager>(scene).Length != 1) throw new InvalidOperationException("Expected one GameFlowManager");
        GameFlowUI flow = Objects<GameFlowUI>(scene).Single();
        string[] fields = menu ? new[] { "mainMenuPanel", "loadingPanel" } :
            new[] { "pausePanel", "deathPanel", "victoryPanel", "loadingPanel", "pauseButton", "deathReason", "deathSummary", "victorySummary" };
        SerializedObject data = new SerializedObject(flow);
        foreach (string field in fields)
            if (data.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing flow reference: " + field);
        if (!menu)
        {
            if (Objects<HealthControl>(scene).Length != 2) throw new InvalidOperationException("Expected player and tower health displays");
            if (Objects<EnemySpawner>(scene).Length != 3) throw new InvalidOperationException("Expected three region spawners");
            foreach (EnemySpawner spawner in Objects<EnemySpawner>(scene))
            {
                SerializedObject wave = new SerializedObject(spawner);
                if (wave.FindProperty("loopWaves").boolValue) throw new InvalidOperationException("Victory requires finite waves");
            }
            data = new SerializedObject(Objects<TowerUpgradePanelUI>(scene).Single());
            foreach (string field in new[] { "tower", "inventory", "panelRoot", "textLine1", "textLine2", "textLine3", "upgradeButton", "upgradeButtonText", "statusText", "woodCostText", "stoneCostText", "gelCostText", "teethCostText" })
                if (data.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing upgrade reference: " + field);
        }
    }
}
