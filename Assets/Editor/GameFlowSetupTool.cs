using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates and saves the real GameFlow Canvas, panels, buttons, and references in both scenes.</summary>
public static class GameFlowSetupTool
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/GameScene.unity";
    private const string SetupRequestPath = "Assets/Editor/GameFlowSetup.request";
    private const int FlowSortingOrder = 32767;

    private static readonly Color ScreenBackground = new Color(0.015f, 0.025f, 0.035f, 0.98f);
    private static readonly Color OverlayBackground = new Color(0.01f, 0.01f, 0.015f, 0.82f);
    private static readonly Color CardBackground = new Color(0.07f, 0.085f, 0.1f, 0.98f);
    private static readonly Color AccentColor = new Color(0.12f, 0.62f, 0.42f, 1f);
    private static readonly Color DangerColor = new Color(0.72f, 0.12f, 0.12f, 1f);

    [InitializeOnLoadMethod]
    private static void RunRequestedSetupAfterCompile()
    {
        string absoluteRequestPath = Path.Combine(Application.dataPath, "Editor/GameFlowSetup.request");
        if (!File.Exists(absoluteRequestPath)) return;

        EditorApplication.delayCall += () =>
        {
            try
            {
                SetupAllScenes();
                AssetDatabase.DeleteAsset(SetupRequestPath);
                Debug.Log("GameFlow setup request completed and removed.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"GameFlow automatic setup is waiting: {exception.Message} " +
                    "The request remains available and will retry after the next script refresh. " +
                    "You can also save the scene and run Tools/Toxic Barrier/Setup Game Flow UI.");
            }
        };
    }

    [MenuItem("Tools/Toxic Barrier/Setup Game Flow UI")]
    public static void SetupAllScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up GameFlow UI.");

        SetupScene(MainMenuScenePath, true);
        SetupScene(GameplayScenePath, false);
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GameFlow UI setup complete. MainMenu and GameScene now contain saved Canvas and Button objects.");
    }

    private static void SetupScene(string scenePath, bool isMainMenu)
    {
        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene targetScene = SceneManager.GetSceneByPath(scenePath);
        bool wasAlreadyLoaded = targetScene.IsValid() && targetScene.isLoaded;

        if (wasAlreadyLoaded && targetScene.isDirty && !EditorSceneManager.SaveScene(targetScene, scenePath))
            throw new InvalidOperationException($"Could not save existing changes in '{scenePath}' before GameFlow setup.");

        if (!wasAlreadyLoaded)
            targetScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(targetScene);
        RemoveGeneratedRoot(targetScene, "GameFlowManager");
        RemoveGeneratedRoot(targetScene, "GameFlowUI");
        RemoveGeneratedRoot(targetScene, "GameFlowEventSystem");

        CreateManagerRoot(targetScene);
        CreateEventSystemRoot(targetScene);
        CreateUIRoot(targetScene, isMainMenu);

        EditorSceneManager.MarkSceneDirty(targetScene);
        if (!EditorSceneManager.SaveScene(targetScene, scenePath))
            throw new InvalidOperationException($"Failed to save GameFlow UI to '{scenePath}'.");

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);

        if (!wasAlreadyLoaded)
            EditorSceneManager.CloseScene(targetScene, true);
    }

    private static void CreateManagerRoot(Scene scene)
    {
        GameObject managerObject = new GameObject("GameFlowManager", typeof(GameFlowManager));
        SceneManager.MoveGameObjectToScene(managerObject, scene);
    }

    private static void CreateEventSystemRoot(Scene scene)
    {
        GameObject eventSystemObject = new GameObject(
            "GameFlowEventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
    }

    private static void CreateUIRoot(Scene scene, bool isMainMenu)
    {
        GameObject uiRoot = new GameObject("GameFlowUI", typeof(GameFlowUI));
        SceneManager.MoveGameObjectToScene(uiRoot, scene);
        GameFlowUI view = uiRoot.GetComponent<GameFlowUI>();

        GameObject canvasObject = new GameObject(
            "GameFlowCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(uiRoot.transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = FlowSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject mainMenuPanel = null;
        GameObject pausePanel = null;
        GameObject deathPanel = null;
        GameObject pauseButton = null;

        if (isMainMenu)
            mainMenuPanel = CreateMainMenu(canvasObject.transform, view);
        else
        {
            pausePanel = CreatePauseMenu(canvasObject.transform, view);
            deathPanel = CreateDeathUI(canvasObject.transform, view);
            pauseButton = CreatePauseButton(canvasObject.transform, view);
        }

        GameObject loadingPanel = CreateLoadingPanel(canvasObject.transform);
        AssignViewReferences(view, mainMenuPanel, pausePanel, deathPanel, loadingPanel, pauseButton);

        mainMenuPanel?.SetActive(isMainMenu);
        pausePanel?.SetActive(false);
        deathPanel?.SetActive(false);
        loadingPanel.SetActive(false);
        pauseButton?.SetActive(!isMainMenu);
    }

    private static GameObject CreateMainMenu(Transform canvas, GameFlowUI view)
    {
        GameObject panel = CreateScreenPanel(canvas, "MainMenuPanel", ScreenBackground);
        Transform card = CreateCard(panel.transform, new Vector2(620f, 560f));
        CreateLabel(card, "Title", "TOXIC BARRIER", 64f, new Color(0.82f, 1f, 0.9f), 110f);
        CreateLabel(card, "Subtitle", "Survive. Gather. Rebuild.", 25f, new Color(0.68f, 0.75f, 0.73f), 55f);
        CreateSpacer(card, 22f);
        CreateButton(card, "StartGameButton", "START GAME", AccentColor, view, nameof(GameFlowUI.StartNewGame));
        CreateButton(card, "QuitButton", "QUIT", new Color(0.22f, 0.25f, 0.27f), view, nameof(GameFlowUI.QuitGame));
        return panel;
    }

    private static GameObject CreatePauseMenu(Transform canvas, GameFlowUI view)
    {
        GameObject panel = CreateScreenPanel(canvas, "PauseMenuPanel", OverlayBackground);
        Transform card = CreateCard(panel.transform, new Vector2(540f, 520f));
        CreateLabel(card, "Title", "GAME PAUSED", 52f, Color.white, 100f);
        CreateButton(card, "ResumeButton", "RESUME", AccentColor, view, nameof(GameFlowUI.ResumeGame));
        CreateButton(card, "RestartButton", "RESTART", new Color(0.24f, 0.34f, 0.38f), view, nameof(GameFlowUI.RestartGame));
        CreateButton(card, "MainMenuButton", "MAIN MENU", new Color(0.24f, 0.27f, 0.3f), view, nameof(GameFlowUI.ReturnToMainMenu));
        return panel;
    }

    private static GameObject CreateDeathUI(Transform canvas, GameFlowUI view)
    {
        GameObject panel = CreateScreenPanel(canvas, "DeathPanel", new Color(0.12f, 0.005f, 0.01f, 0.86f));
        Transform card = CreateCard(panel.transform, new Vector2(570f, 550f));
        CreateLabel(card, "Title", "YOU DIED", 62f, new Color(1f, 0.28f, 0.25f), 120f);
        CreateLabel(card, "Subtitle", "The barrier has fallen.", 25f, new Color(0.86f, 0.73f, 0.72f), 55f);
        CreateSpacer(card, 16f);
        CreateButton(card, "RestartButton", "RESTART", DangerColor, view, nameof(GameFlowUI.RestartGame));
        CreateButton(card, "MainMenuButton", "MAIN MENU", new Color(0.27f, 0.22f, 0.23f), view, nameof(GameFlowUI.ReturnToMainMenu));
        return panel;
    }

    private static GameObject CreateLoadingPanel(Transform canvas)
    {
        GameObject panel = CreateScreenPanel(canvas, "LoadingPanel", ScreenBackground);
        Transform card = CreateCard(panel.transform, new Vector2(480f, 220f));
        CreateLabel(card, "LoadingText", "LOADING...", 42f, new Color(0.8f, 1f, 0.9f), 120f);
        return panel;
    }

    private static GameObject CreatePauseButton(Transform canvas, GameFlowUI view)
    {
        GameObject buttonObject = CreateButtonObject(
            canvas,
            "PauseButton",
            "PAUSE  [ESC]",
            new Color(0.045f, 0.06f, 0.07f, 0.9f),
            view,
            nameof(GameFlowUI.PauseGame),
            false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -20f);
        rect.sizeDelta = new Vector2(150f, 54f);
        return buttonObject;
    }

    private static GameObject CreateScreenPanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private static Transform CreateCard(Transform parent, Vector2 size)
    {
        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        card.GetComponent<Image>().color = CardBackground;

        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(70, 70, 50, 50);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return card.transform;
    }

    private static void CreateLabel(Transform parent, string objectName, string value, float size, Color color, float height)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.font = GetDefaultFont();
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        labelObject.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private static void CreateButton(Transform parent, string name, string label, Color color, GameFlowUI view, string methodName)
    {
        CreateButtonObject(parent, name, label, color, view, methodName, true);
    }

    private static GameObject CreateButtonObject(Transform parent, string name, string label, Color color, GameFlowUI view, string methodName, bool useLayout)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        AddButtonListener(button, view, methodName);

        if (useLayout)
        {
            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 64f;
            layout.minHeight = 64f;
        }

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.font = GetDefaultFont();
        text.fontSize = useLayout ? 28f : 20f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return buttonObject;
    }

    private static void AddButtonListener(Button button, GameFlowUI view, string methodName)
    {
        switch (methodName)
        {
            case nameof(GameFlowUI.StartNewGame): UnityEventTools.AddPersistentListener(button.onClick, view.StartNewGame); break;
            case nameof(GameFlowUI.PauseGame): UnityEventTools.AddPersistentListener(button.onClick, view.PauseGame); break;
            case nameof(GameFlowUI.ResumeGame): UnityEventTools.AddPersistentListener(button.onClick, view.ResumeGame); break;
            case nameof(GameFlowUI.RestartGame): UnityEventTools.AddPersistentListener(button.onClick, view.RestartGame); break;
            case nameof(GameFlowUI.ReturnToMainMenu): UnityEventTools.AddPersistentListener(button.onClick, view.ReturnToMainMenu); break;
            case nameof(GameFlowUI.QuitGame): UnityEventTools.AddPersistentListener(button.onClick, view.QuitGame); break;
            default: throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unknown GameFlow button callback.");
        }
        EditorUtility.SetDirty(button);
    }

    private static TMP_FontAsset GetDefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    }

    private static void AssignViewReferences(GameFlowUI view, GameObject main, GameObject pause, GameObject death, GameObject loading, GameObject pauseButton)
    {
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("mainMenuPanel").objectReferenceValue = main;
        serializedView.FindProperty("pausePanel").objectReferenceValue = pause;
        serializedView.FindProperty("deathPanel").objectReferenceValue = death;
        serializedView.FindProperty("loadingPanel").objectReferenceValue = loading;
        serializedView.FindProperty("pauseButton").objectReferenceValue = pauseButton;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static void RemoveGeneratedRoot(Scene scene, string rootName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == rootName)
            {
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }
        }
    }

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(GameplayScenePath, true)
        };
    }
}
