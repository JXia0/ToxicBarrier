using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Connects the existing scene-authored HealthCanvas to the player's HealthSystem.</summary>
public static class PlayerHealthUISetupTool
{
    private const string GameplayScenePath = "Assets/Scenes/GameScene.unity";
    private const string SetupRequestPath = "Assets/Editor/PlayerHealthUISetup.request";

    [InitializeOnLoadMethod]
    private static void RunRequestedSetupAfterCompile()
    {
        string requestPath = Path.Combine(Application.dataPath, "Editor/PlayerHealthUISetup.request");
        if (!File.Exists(requestPath)) return;

        EditorApplication.delayCall += () =>
        {
            try
            {
                SetupPlayerHealthUI();
                AssetDatabase.DeleteAsset(SetupRequestPath);
                Debug.Log("Player health UI setup request completed and removed.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Player health UI automatic setup is waiting: {exception.Message} " +
                    "Save GameScene and run Tools/Toxic Barrier/Setup Player Health UI if needed.");
            }
        };
    }

    [MenuItem("Tools/Toxic Barrier/Setup Player Health UI")]
    public static void SetupPlayerHealthUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up Player Health UI.");

        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
        bool wasAlreadyLoaded = gameplayScene.IsValid() && gameplayScene.isLoaded;

        if (wasAlreadyLoaded && gameplayScene.isDirty && !EditorSceneManager.SaveScene(gameplayScene, GameplayScenePath))
            throw new InvalidOperationException("Could not save existing GameScene changes before health UI setup.");

        if (!wasAlreadyLoaded)
            gameplayScene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(gameplayScene);

        HealthSystem playerHealth = FindPlayerHealth(gameplayScene);
        GameObject healthCanvasObject = FindGameObject(gameplayScene, "HealthCanvas");
        GameObject healthBarObject = FindGameObject(gameplayScene, "HealthBar");
        GameObject backgroundObject = FindGameObject(gameplayScene, "Background", healthCanvasObject?.transform);

        if (playerHealth == null)
            throw new InvalidOperationException("No HealthSystem marked Is Player was found in GameScene.");
        if (healthCanvasObject == null)
            throw new InvalidOperationException("HealthCanvas was not found in GameScene.");
        if (healthBarObject == null)
            throw new InvalidOperationException("HealthBar was not found in GameScene.");

        Image healthBarFill = healthBarObject.GetComponent<Image>();
        if (healthBarFill == null)
            throw new InvalidOperationException("HealthBar does not contain a Unity UI Image component.");

        HealthControl healthControl = healthCanvasObject.GetComponent<HealthControl>();
        if (healthControl == null)
            healthControl = healthCanvasObject.AddComponent<HealthControl>();

        SerializedObject serializedControl = new SerializedObject(healthControl);
        serializedControl.FindProperty("playerHealth").objectReferenceValue = playerHealth;
        serializedControl.FindProperty("healthBarFill").objectReferenceValue = healthBarFill;
        serializedControl.ApplyModifiedPropertiesWithoutUndo();

        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthBarFill.fillAmount = 1f;
        healthBarFill.raycastTarget = false;

        RectTransform healthCanvasRect = healthCanvasObject.GetComponent<RectTransform>();
        if (healthCanvasRect != null)
            healthCanvasRect.localScale = Vector3.one;

        RectTransform fillRect = healthBarObject.GetComponent<RectTransform>();
        RectTransform backgroundRect = backgroundObject != null ? backgroundObject.GetComponent<RectTransform>() : null;
        if (fillRect != null && backgroundRect != null)
            CopyRectTransform(backgroundRect, fillRect);

        Image background = backgroundObject != null ? backgroundObject.GetComponent<Image>() : null;
        if (background != null)
            background.raycastTarget = false;

        EditorUtility.SetDirty(healthControl);
        EditorUtility.SetDirty(healthBarFill);
        if (healthCanvasRect != null) EditorUtility.SetDirty(healthCanvasRect);
        if (fillRect != null) EditorUtility.SetDirty(fillRect);
        if (background != null) EditorUtility.SetDirty(background);

        EditorSceneManager.MarkSceneDirty(gameplayScene);
        if (!EditorSceneManager.SaveScene(gameplayScene, GameplayScenePath))
            throw new InvalidOperationException("Failed to save Player Health UI to GameScene.");

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);
        if (!wasAlreadyLoaded)
            EditorSceneManager.CloseScene(gameplayScene, true);

        AssetDatabase.SaveAssets();
        Debug.Log("Player Health UI setup complete. HealthCanvas is linked to the player's HealthSystem and HealthBar Image.");
    }

    private static HealthSystem FindPlayerHealth(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (HealthSystem health in root.GetComponentsInChildren<HealthSystem>(true))
            {
                if (health.IsPlayer)
                    return health;
            }
        }

        return null;
    }

    private static GameObject FindGameObject(Scene scene, string objectName, Transform requiredAncestor = null)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != objectName) continue;
                if (requiredAncestor != null && !child.IsChildOf(requiredAncestor)) continue;
                return child.gameObject;
            }
        }

        return null;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }
}
