using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Updates existing saved UI in place; adds settings and clean presentation assets.</summary>
[InitializeOnLoad]
public static class ReleasePolishTool
{
    private static TMP_FontAsset _font;
    private const string Presentation = "Assets/Resources/Presentation/";
    static ReleasePolishTool() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/ToxicBarrierRelease.polish")) return;
        File.Delete("Temp/ToxicBarrierRelease.polish");
        try { Apply(); File.WriteAllText("Temp/ToxicBarrierRelease.polish-result", "PASS"); }
        catch (Exception e) { File.WriteAllText("Temp/ToxicBarrierRelease.polish-result", "FAIL\n" + e); Debug.LogException(e); }
    }

    [MenuItem("Tools/Toxic Barrier/Apply Release Polish")]
    public static void Apply()
    {
        EditorSceneManager.SaveOpenScenes();
        string original = SceneManager.GetActiveScene().path;
        Directory.CreateDirectory(Presentation);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Presentation + "Chinese.asset");
        if (_font == null)
        {
            AssetDatabase.ImportAsset("Assets/Fonts/NotoSansCJKsc-Regular.otf", ImportAssetOptions.ForceUpdate);
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
            if (source == null) throw new Exception("Chinese source font must finish downloading/importing first.");
            _font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (_font == null) throw new Exception("TMP could not read the imported font. Reimport the complete source font and retry.");
            _font.name = "Noto Sans Chinese";
            AssetDatabase.CreateAsset(_font, Presentation + "Chinese.asset");
            AssetDatabase.AddObjectToAsset(_font.material, _font);
            foreach (Texture2D atlas in _font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, _font);
        }
        string characters = string.Concat(Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText)) + File.ReadAllText("Assets/Editor/ReleasePolishTool.cs");
        _font.TryAddCharacters(new string(characters.Distinct().ToArray()));
        EditorUtility.SetDirty(_font);
        // Preserve the user's three original central tower designs in the world and UI.
        Sprite[] towerSprites = Enumerable.Range(0, 4).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/Sprites_tower" + (i < 2 ? "" : i.ToString()) + ".png")).ToArray();
        Sprite supply = CleanSprite("Assets/PeterRadiant/Tiny Swords/UI Elements/Icons/Icon_02.png", "Supply", 64);
        var wood = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PeterRadiant/Tiny Swords/UI Elements/Icons/Icon_02.png");
        foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/GameScene.unity" })
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            if (scene.name == "MainMenu" && Objects<Camera>(scene).Length == 0)
            {
                var camera = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .06f, .05f);
                camera.cullingMask = 0; camera.transform.position = new Vector3(0, 0, -10);
            }
            foreach (var text in Objects<TextMeshProUGUI>(scene))
            {
                text.font = _font;
                text.text = Translate(text.text);
                text.enableAutoSizing = true; text.fontSizeMax = text.fontSize; text.fontSizeMin = Mathf.Min(18, text.fontSize);
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
            foreach (var image in Objects<Image>(scene))
            {
                if (image.name == "TowerArtwork") image.sprite = towerSprites[3];
                if (image.name == "TowerPreview") image.sprite = towerSprites[1];
                if (image.name == "WoodIcon") image.sprite = wood;
            }
            if (scene.name == "GameScene")
            {
                var tower = Objects<Tower>(scene).Single();
                // Keep the original tower transform, trigger and physics.
                for (int i = 1; i <= 3; i++) Ref(tower, "level" + i + "Sprite", towerSprites[i]);
                tower.GetComponent<SpriteRenderer>().sprite = towerSprites[0];
                var upgrade = new SerializedObject(Objects<TowerUpgradePanelUI>(scene).Single());
                var sprites = upgrade.FindProperty("towerSprites"); sprites.arraySize = 4;
                for (int i = 0; i < 4; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = towerSprites[i];
                upgrade.ApplyModifiedPropertiesWithoutUndo();
                var player = Objects<PlayerController>(scene).Single();
                Float(player, "map2MoveSpeed", 1.6f); Float(player, "map3MoveSpeed", 1.4f);
                Float(player.GetComponent<HealthSystem>(), "toxicGasBaseDamagePerSecond", 4f);
                Float(player.GetComponent<HealthSystem>(), "toxicGasDamageIncreasePerPeriod", .5f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<HealthSystem>());
            }
            SetupSettings(scene);
            AddLanguageSwitch(scene);
            BindStaticLanguage(scene);
            foreach (var button in Objects<Button>(scene)) if (button.GetComponent<ButtonSound>() == null) button.gameObject.AddComponent<ButtonSound>();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllLines("Temp/" + scene.name + "-polished-text.txt", Objects<TextMeshProUGUI>(scene).Select(t => t.name + ": " + t.text));
        }
        // Ordinary collection is more valuable, allowing upgrades before the guaranteed clear reward.
        foreach (string name in new[] { "Wood", "Stone", "Gel", "Teeth" })
        {
            string path = "Assets/Prefab/" + name + ".prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            var pickup = prefab.GetComponent<ResourcePickup>();
            var data = new SerializedObject(pickup); data.FindProperty("amount").intValue = 2; data.ApplyModifiedPropertiesWithoutUndo();
            if (name == "Wood")
            {
                var renderer = prefab.GetComponent<SpriteRenderer>();
                renderer.sprite = CleanSprite("Assets/PeterRadiant/Tiny Swords/UI Elements/Icons/Icon_02.png", "WoodPickup", 128);
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, path); PrefabUtility.UnloadPrefabContents(prefab);
        }
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
    }

    private static Sprite CleanSprite(string path, string name, float units)
    {
        var saved = AssetDatabase.LoadAssetAtPath<Sprite>(Presentation + name + ".asset");
        if (saved != null) return saved;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), units, 0, SpriteMeshType.FullRect);
        sprite.name = name; AssetDatabase.CreateAsset(sprite, Presentation + name + ".asset"); return sprite;
    }

    private static void AddLanguageSwitch(Scene scene)
    {
        var settings = Objects<GameSettingsUI>(scene).Single();
        Transform card = Objects<Transform>(scene).First(t => t.name == "SettingsCard");
        if (card.Find("LanguageButton") != null) return;
        var button = Button(card, "LanguageButton", "Language: English", new Vector2(260, 330), new Vector2(245, 50), settings.ToggleLanguage);
        var label = button.GetComponentInChildren<TextMeshProUGUI>(); label.fontSize = 19;
        Ref(settings, "languageValue", label);
    }

    private static void BindStaticLanguage(Scene scene)
    {
        var dynamicNames = new HashSet<string> { "HealthValue", "WoodCount", "StoneCount", "GelCount", "TeethCount", "Waves", "Objective", "PoisonStatus", "RunTimer", "TowerLevel", "StatsPreview", "Benefit", "WoodCost", "StoneCost", "GelCost", "TeethCost", "Status", "BestRecord", "RunSummary" };
        var settingsData = new SerializedObject(Objects<GameSettingsUI>(scene).Single());
        var settingsDynamic = new[] { "musicValue", "effectsValue", "displayValue", "languageValue" }.Select(f => settingsData.FindProperty(f).objectReferenceValue).ToArray();
        foreach (var label in Objects<TextMeshProUGUI>(scene))
        {
            if (string.IsNullOrEmpty(label.text) || dynamicNames.Contains(label.name) || settingsDynamic.Contains(label) ||
                label.transform.parent.name == "TowerHealth" || label.transform.parent.name == "UpgradeButton") continue;
            var binding = label.GetComponent<LocalizedText>();
            if (binding == null) { binding = label.gameObject.AddComponent<LocalizedText>(); binding.chinese = label.text; }
        }
    }

    private static readonly Dictionary<string, string> Copy = new Dictionary<string, string> {
        {"GATHER  /  DEFEND  /  RESTORE", "采集  /  守护  /  净化"}, {"A LAST REFUGE IN THE TOXIC WILDS", "毒雾之中，守住最后的庇护所"},
        {"TOXIC\nBARRIER", "毒雾\n屏障"}, {"Build the last defense. Reclaim all three regions.", "升级净化塔，夺回被毒雾吞没的三区。"},
        {"START GAME", "开始游戏"}, {"QUIT GAME", "退出游戏"}, {"TAKE A BREATH", "暂歇片刻"}, {"The world is paused.", "游戏已暂停"},
        {"RESUME", "继续游戏"}, {"RESTART RUN", "重新开始"}, {"MAIN MENU", "返回主菜单"}, {"PLAY AGAIN", "再玩一次"}, {"PAUSE  [ESC]", "暂停 [ESC]"},
        {"ALL THREE REGIONS RECLAIMED", "三区全部净化"}, {"THE WILDS CLAIM ANOTHER", "防线未能守住"}, {"BARRIER RESTORED", "屏障重生"}, {"DEFENSE LOST", "守护失败"},
        {"All waves defeated. Your tower still stands.", "所有敌人已消灭，净化塔依然矗立。"}, {"The barrier has fallen.", "屏障已经倒下。"},
        {"ENTERING THE BARRIER...", "正在进入屏障…"}, {"SURVIVOR", "幸存者"}, {"WASD Move | E Upgrade near tower | ESC Resume", "WASD 移动  |  返塔按 E 升级  |  ESC 继续"},
        {"WASD / ARROWS Move | ESC Pause\nYou gather and dodge; the tower auto-attacks\nClear regions for upgrade supplies\nE near tower: upgrade and restore health", "WASD / 方向键移动，ESC 暂停\n玩家采集和躲避，净化塔自动攻击\n清区后前往金色标记领取升级补给\n返塔按 E 升级；塔旁脱战后持续回血"}
    };
    private static string Translate(string value)
    {
        if (Copy.TryGetValue(value, out string result)) return result;
        if (value.StartsWith("REGION 1")) return "第 1 区 0/6 波\n第 2 区 未解锁\n第 3 区 未解锁";
        return value.Replace("PURIFICATION TOWER  Lv.0", "净化塔 0 级").Replace("Wood", "木材").Replace("Stone", "石料").Replace("Gel", "凝胶").Replace("Teeth", "尖牙")
            .Replace("Gather materials and upgrade the tower to Lv.3", "采集材料，把净化塔升至 3 级").Replace("SAFE ZONE", "安全区")
            .Replace("0 defeated", "击败 0").Replace("Tower Level: 0  |  Next: Level 1", "净化塔 0 级 → 1 级")
            .Replace("ATK 50 > 150     HP 1000 > 2500", "攻击 50 → 150    生命 1000 → 2500")
            .Replace("Stronger defenses. Restores you and the tower.", "强化防御，恢复玩家与塔的全部生命。")
            .Replace("UPGRADE TOWER", "升级净化塔 [E]").Replace("Collect the missing materials", "采集材料，或清区后领取补给");
    }

    private static void SetupSettings(Scene scene)
    {
        var flow = Objects<GameFlowUI>(scene).Single();
        if (flow.GetComponent<GameSettingsUI>() != null) return;
        var settings = flow.gameObject.AddComponent<GameSettingsUI>();
        Transform canvas = flow.transform.Find("GameFlowCanvas");
        var panel = Rect(canvas, "SettingsPanel", Vector2.zero, new Vector2(1920, 1080));
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = new Color(.015f, .03f, .025f, .98f);
        var card = Rect(panel, "SettingsCard", Vector2.zero, new Vector2(800, 830));
        Label(card, "设置", new Vector2(0, 330), new Vector2(650, 70), 46);
        Ref(settings, "panel", panel.gameObject);
        MakeSlider(card, settings, "music", "音乐音量", 210);
        MakeSlider(card, settings, "effects", "音效音量", 100);
        Ref(settings, "displayValue", Label(card, "显示设置", new Vector2(0, -35), new Vector2(680, 65), 24));
        Button(card, "ResolutionButton", "切换分辨率", new Vector2(-165, -130), new Vector2(290, 64), settings.NextResolution);
        Button(card, "FullscreenButton", "切换全屏 / 窗口", new Vector2(165, -130), new Vector2(290, 64), settings.ToggleFullscreen);
        Button(card, "ApplyDisplayButton", "应用显示设置", new Vector2(0, -222), new Vector2(420, 64), settings.ApplyDisplay);
        Button(card, "CloseSettingsButton", "保存并返回 [ESC]", new Vector2(0, -330), new Vector2(420, 72), settings.Close);
        Transform parent = Objects<Transform>(scene).First(t => t.name == (scene.name == "MainMenu" ? "MenuCard" : "PauseCard"));
        var settingsButton = Button(parent, "SettingsButton", "设置", Vector2.zero, new Vector2(248, 70), settings.Open);
        var rect = settingsButton.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(-140, scene.name == "MainMenu" ? -583 : -539);
        if (scene.name == "MainMenu")
        {
            var quit = parent.Find("QuitButton").GetComponent<RectTransform>(); quit.sizeDelta = new Vector2(248, 70); quit.pivot = new Vector2(.5f, .5f); quit.anchoredPosition = new Vector2(140, -583);
            var best = Label(parent, "", new Vector2(0, -385), new Vector2(650, 35), 20); best.name = "BestRecord"; Ref(settings, "bestRecord", best);
        }
        else
        {
            rect.anchoredPosition = new Vector2(0, -565); rect.sizeDelta = new Vector2(470, 70);
            parent.Find("Hint").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -655);
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(690, 770);
            foreach (Transform child in parent) if (child.name == "DarkInset") child.GetComponent<RectTransform>().sizeDelta = new Vector2(628, 710);
        }
        panel.gameObject.SetActive(false);
    }
    private static void MakeSlider(Transform parent, GameSettingsUI settings, string field, string title, float y)
    {
        Label(parent, title, new Vector2(-240, y + 36), new Vector2(220, 44), 27);
        Ref(settings, field + "Value", Label(parent, "", new Vector2(280, y + 36), new Vector2(130, 44), 25));
        var rect = Rect(parent, field + "Slider", new Vector2(0, y - 12), new Vector2(640, 32));
        rect.gameObject.AddComponent<Image>().color = new Color(.18f, .3f, .28f);
        var slider = rect.gameObject.AddComponent<Slider>();
        var handle = Rect(rect, "Handle", Vector2.zero, new Vector2(28, 44));
        var image = handle.gameObject.AddComponent<Image>(); image.color = new Color(.95f, .77f, .4f);
        slider.handleRect = handle; slider.targetGraphic = image; slider.minValue = 0; slider.maxValue = 1;
        Ref(settings, field + "Slider", slider);
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; return r;
    }
    private static TextMeshProUGUI Label(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize)
    {
        var t = Rect(parent, "Label", pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = _font; t.fontSize = fontSize; t.text = text; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.color = new Color(.96f, .92f, .8f); return t;
    }
    private static Button Button(Transform parent, string name, string text, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(parent, name, pos, size); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.2f, .38f, .35f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; UnityEventTools.AddPersistentListener(button.onClick, action);
        Label(rect, text, Vector2.zero, size - new Vector2(16, 4), 25); return button;
    }
    private static T[] Objects<T>(Scene scene) where T : Component { return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray(); }
    private static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value) { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Float(UnityEngine.Object target, string field, float value) { var data = new SerializedObject(target); data.FindProperty(field).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
}
