using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Connects the existing scene-authored HealthCanvas to the player's HealthSystem.</summary>
public static class PlayerHealthUISetupTool
{
    private const string GameplayScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Toxic Barrier/Setup Player Health UI")]
    public static void SetupPlayerHealthUI()
    {
        // Keep the old menu command compatible with the finished, scene-authored HUD.
        if (AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Core/GameHudUI.cs") != null)
        {
            ReleaseUISetupTool.SetupAllScenes();
            return;
        }
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

        RectTransform healthBar = healthBarObject.GetComponent<RectTransform>();
        SerializedObject serializedControl = new SerializedObject(healthControl);
        serializedControl.FindProperty("healthBar").objectReferenceValue = healthBar;
        serializedControl.ApplyModifiedPropertiesWithoutUndo();

        for (int i = playerHealth.OnHpChanged.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (playerHealth.OnHpChanged.GetPersistentTarget(i) == healthControl)
                UnityEventTools.RemovePersistentListener(playerHealth.OnHpChanged, i);
        }
        UnityEventTools.AddPersistentListener(playerHealth.OnHpChanged, healthControl.UpdateHealthBar);

        healthBarFill.type = Image.Type.Simple;
        healthBarFill.raycastTarget = false;

        RectTransform healthCanvasRect = healthCanvasObject.GetComponent<RectTransform>();
        if (healthCanvasRect != null)
            healthCanvasRect.localScale = Vector3.one;

        RectTransform fillRect = healthBar;
        RectTransform backgroundRect = backgroundObject != null ? backgroundObject.GetComponent<RectTransform>() : null;
        if (fillRect != null && backgroundRect != null)
            AlignHealthBarToBackground(backgroundRect, fillRect);

        Image background = backgroundObject != null ? backgroundObject.GetComponent<Image>() : null;
        if (background != null)
            background.raycastTarget = false;

        EditorUtility.SetDirty(healthControl);
        EditorUtility.SetDirty(playerHealth);
        PrefabUtility.RecordPrefabInstancePropertyModifications(playerHealth);
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
        Debug.Log("Player Health UI setup complete. OnHpChanged now drives the HealthBar RectTransform width.");
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

    private static void AlignHealthBarToBackground(RectTransform background, RectTransform healthBar)
    {
        healthBar.anchorMin = background.anchorMin;
        healthBar.anchorMax = background.anchorMax;
        healthBar.pivot = new Vector2(0f, background.pivot.y);
        healthBar.anchoredPosition = new Vector2(
            background.anchoredPosition.x - background.rect.width * background.pivot.x,
            background.anchoredPosition.y);
        healthBar.sizeDelta = background.sizeDelta;
        healthBar.localRotation = background.localRotation;
        healthBar.localScale = background.localScale;
    }
}
