using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Applies simple title/pause menus with a direct language shortcut.</summary>
[InitializeOnLoad]
public static class SimpleMainMenuTool
{
    private const string Background = "Assets/Art/UI/MenuBackground.png";
    private static readonly Color Gold = new Color(.65f, .51f, .29f);
    private static readonly Color Cream = new Color(.94f, .89f, .76f);
    static SimpleMainMenuTool() { EditorApplication.update += Tick; }

    private static void Tick()
    {
        const string request = "Temp/ToxicBarrierMenu.simple";
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || !File.Exists(request)) return;
        File.Delete(request);
        try { Apply(); File.WriteAllText(request + "-result", "PASS"); }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText(request + "-result", "FAIL\n" + e); }
    }

    [MenuItem("Tools/Toxic Barrier/Apply Simple Main Menu")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        AssetDatabase.ImportAsset(Background, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(Background) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Generate the menu background first.");
        importer.textureType = TextureImporterType.Default;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/MainMenu.unity");
        bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Additive);
        try
        {
            Configure(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!loaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
        ApplyPause();
    }

    private static void ApplyPause()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        bool loaded = scene.IsValid() && scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Additive);
        try
        {
            ConfigurePause(scene);
            var tower = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tower>(true)).Single();
            var data = new SerializedObject(tower);
            data.FindProperty("currentLevel").intValue = Tower.InitialLevel;
            data.ApplyModifiedPropertiesWithoutUndo();
            tower.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sprites_tower0.png");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!loaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public static void ConfigurePause(Scene scene)
    {
        var flow = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameFlowUI>(true)).Single();
        Transform canvas = flow.transform.Find("GameFlowCanvas");
        Transform panel = canvas.Find("PauseMenuPanel");
        for (int i = panel.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(panel.GetChild(i).gameObject);
        panel.GetComponent<Image>().color = new Color(.01f, .015f, .02f, .72f);
        var settings = flow.GetComponent<GameSettingsUI>();
        if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
        var oldSettings = canvas.Find("SettingsPanel");
        if (oldSettings != null) UnityEngine.Object.DestroyImmediate(oldSettings.gameObject);
        var card = Rect(panel, "PauseCard", Vector2.zero, new Vector2(550, 600));
        card.gameObject.AddComponent<Image>().color = Gold;
        var inset = Rect(card, "Fill", Vector2.zero, new Vector2(546, 596));
        inset.gameObject.AddComponent<Image>().color = new Color(.035f, .045f, .048f, .99f);
        var title = Label(card, "Title", "暂停", new Vector2(0, 211), new Vector2(480, 75), 46);
        var titleText = title.gameObject.AddComponent<LocalizedText>(); titleText.chinese = "暂停"; titleText.Refresh();
        var resume = Button(card, "ResumeButton", "继续游戏", 94, flow.ResumeGame);
        var restart = Button(card, "RestartButton", "重新开始", -9, flow.RestartGame);
        var menu = Button(card, "MainMenuButton", "返回主菜单", -112, flow.ReturnToMainMenu);
        var language = Button(card, "LanguageButton", "EN / 中文", -224, flow.ToggleLanguage);
        foreach (var button in new[] { resume, restart, menu, language })
        {
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(0, rect.anchoredPosition.y);
        }
        ((RectTransform)language.transform).sizeDelta = new Vector2(190, 46);
        language.transform.Find("Fill").GetComponent<RectTransform>().sizeDelta = new Vector2(186, 42);
        var label = language.GetComponentInChildren<TextMeshProUGUI>();
        label.rectTransform.sizeDelta = new Vector2(180, 42); label.fontSize = 20;
        var buttons = new[] { resume, restart, menu, language };
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnDown = buttons[(i + 1) % buttons.Length], selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length] };
        panel.gameObject.SetActive(false);
    }

    public static void Configure(Scene scene)
    {
        GameFlowUI flow = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameFlowUI>(true)).Single();
        Transform canvas = flow.transform.Find("GameFlowCanvas");
        Transform panel = canvas.Find("MainMenuPanel");
        if (panel == null) panel = Rect(canvas, "MainMenuPanel", Vector2.zero, Vector2.zero);
        var panelRect = (RectTransform)panel;
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        for (int i = panel.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(panel.GetChild(i).gameObject);
        var baseImage = panel.GetComponent<Image>();
        if (baseImage == null) baseImage = panel.gameObject.AddComponent<Image>();
        baseImage.color = new Color(.025f, .035f, .045f, 1);
        var settings = flow.GetComponent<GameSettingsUI>();
        if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
        Transform settingsPanel = canvas.Find("SettingsPanel");
        if (settingsPanel != null) UnityEngine.Object.DestroyImmediate(settingsPanel.gameObject);

        var backdrop = Rect(panel, "MenuBackground", Vector2.zero, new Vector2(1920, 1080));
        var raw = backdrop.gameObject.AddComponent<RawImage>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Background);
        if (raw.texture == null) throw new InvalidOperationException("Missing menu background texture.");
        raw.raycastTarget = false;
        var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectRatio = 16f / 9; fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        var content = Rect(panel, "MenuCard", new Vector2(0, 0), new Vector2(640, 570));
        content.anchorMin = content.anchorMax = new Vector2(.075f, .51f);
        content.pivot = new Vector2(0, .5f);
        var title = Label(content, "Title", "TOXIC\nBARRIER", new Vector2(0, 130), new Vector2(640, 260), 96);
        title.alignment = TextAlignmentOptions.Left;
        title.characterSpacing = 5;
        title.fontStyle = FontStyles.Bold;
        var line = Rect(content, "TitleRule", new Vector2(-120, -15), new Vector2(400, 2));
        line.gameObject.AddComponent<Image>().color = Gold;
        var start = Button(content, "StartGameButton", "开始游戏", -102, flow.StartNewGame);
        var quit = Button(content, "QuitButton", "退出", -202, flow.QuitGame);
        var language = Button(panel, "LanguageButton", "EN / 中文", 0, flow.ToggleLanguage);
        var languageRect = (RectTransform)language.transform;
        languageRect.anchorMin = languageRect.anchorMax = new Vector2(1, 1);
        languageRect.pivot = new Vector2(1, 1);
        languageRect.anchoredPosition = new Vector2(-38, -30);
        languageRect.sizeDelta = new Vector2(180, 48);
        language.transform.Find("Fill").GetComponent<RectTransform>().sizeDelta = new Vector2(176, 44);
        var languageLabel = language.GetComponentInChildren<TextMeshProUGUI>();
        languageLabel.rectTransform.sizeDelta = new Vector2(170, 44); languageLabel.fontSize = 20;
        start.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = quit, selectOnUp = quit, selectOnRight = language };
        quit.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = start, selectOnUp = start, selectOnRight = language };
        language.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = start, selectOnDown = start };
        var data = new SerializedObject(flow);
        data.FindProperty("mainMenuPanel").objectReferenceValue = panel.gameObject;
        data.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(true);
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = position; r.sizeDelta = size;
        return r;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Presentation/Chinese.asset");
        label.fontSize = fontSize; label.text = text; label.color = Cream;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }

    private static Button Button(Transform parent, string name, string chinese, float y, UnityEngine.Events.UnityAction action)
    {
        var r = Rect(parent, name, new Vector2(-120, y), new Vector2(400, 78));
        var edge = r.gameObject.AddComponent<Image>(); edge.color = Color.white;
        var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = edge;
        var colors = button.colors;
        colors.normalColor = Gold; colors.highlightedColor = new Color(1, .85f, .53f);
        colors.selectedColor = new Color(.92f, .75f, .46f); colors.pressedColor = new Color(.42f, .33f, .18f);
        colors.fadeDuration = .12f; button.colors = colors;
        var inset = Rect(r, "Fill", Vector2.zero, new Vector2(396, 74));
        var fill = inset.gameObject.AddComponent<Image>(); fill.color = new Color(.05f, .065f, .066f, .96f); fill.raycastTarget = false;
        var label = Label(r, "Text", chinese, Vector2.zero, new Vector2(370, 65), 30);
        label.characterSpacing = 3;
        var localized = label.gameObject.AddComponent<LocalizedText>(); localized.chinese = chinese; localized.Refresh();
        UnityEventTools.AddPersistentListener(button.onClick, action);
        r.gameObject.AddComponent<ButtonSound>();
        return button;
    }
}
