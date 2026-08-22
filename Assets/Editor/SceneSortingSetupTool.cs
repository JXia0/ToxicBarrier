using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Applies the project's fixed 2D sorting rules to scenes and gameplay prefabs.</summary>
public static class SceneSortingSetupTool
{
    private const string GameplayScenePath = "Assets/Scenes/GameScene.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string SetupRequestPath = "Assets/Editor/SceneSortingSetup.request";
    private const string ToxicFogTileAssetPath = "Assets/kenney_1-bit-pack/Tilemap/Untitled_Artwork(1).asset";

    private static readonly LayerDefinition[] LayerDefinitions =
    {
        new LayerDefinition(SortingLayerNames.Background, 1907311999),
        new LayerDefinition(SortingLayerNames.Ground, 1381532601),
        new LayerDefinition(SortingLayerNames.Environment, 1347790203),
        new LayerDefinition(SortingLayerNames.Characters, 1922954461),
        new LayerDefinition(SortingLayerNames.Foreground, 2053025017),
        new LayerDefinition(SortingLayerNames.UI, 1066930667)
    };

    private static readonly PrefabRule[] PrefabRules =
    {
        new PrefabRule("Assets/Prefab/Player.prefab", SortingLayerNames.Characters, 10),
        new PrefabRule("Assets/Prefab/Bug.prefab", SortingLayerNames.Characters, 0),
        new PrefabRule("Assets/Prefab/Boss.prefab", SortingLayerNames.Characters, 0),
        new PrefabRule("Assets/Prefab/Leader.prefab", SortingLayerNames.Characters, 0),
        new PrefabRule("Assets/Prefab/Wood.prefab", SortingLayerNames.Environment, 0),
        new PrefabRule("Assets/Prefab/Stone.prefab", SortingLayerNames.Environment, 0),
        new PrefabRule("Assets/Prefab/Gel.prefab", SortingLayerNames.Environment, 0),
        new PrefabRule("Assets/Prefab/Teeth.prefab", SortingLayerNames.Environment, 0)
    };

    [InitializeOnLoadMethod]
    private static void RunRequestedSetupAfterCompile()
    {
        string absoluteRequestPath = Path.Combine(Application.dataPath, "Editor/SceneSortingSetup.request");
        if (!File.Exists(absoluteRequestPath)) return;

        EditorApplication.delayCall += () =>
        {
            try
            {
                SetupSorting();
                AssetDatabase.DeleteAsset(SetupRequestPath);
                Debug.Log("Scene sorting setup request completed and removed.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Scene sorting setup is waiting: {exception.Message} " +
                    "The request remains available and will retry after the next script refresh.");
            }
        };
    }

    [MenuItem("Tools/Toxic Barrier/Setup Scene Sorting")]
    public static void SetupSorting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before setting up scene sorting.");

        if (AssetDatabase.LoadAssetAtPath<TileBase>(ToxicFogTileAssetPath) == null)
            throw new InvalidOperationException($"Missing Toxic Fog tile asset at '{ToxicFogTileAssetPath}'.");

        EnsureSortingLayers();

        foreach (PrefabRule rule in PrefabRules)
            ConfigurePrefab(rule);

        ConfigureScene(GameplayScenePath, true);
        ConfigureScene(MainMenuScenePath, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Scene sorting setup complete. Gameplay renderers now use the fixed sorting-layer rules.");
    }

    private static void EnsureSortingLayers()
    {
        UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets.Length == 0)
            throw new InvalidOperationException("Could not load ProjectSettings/TagManager.asset.");

        SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");

        foreach (LayerDefinition definition in LayerDefinitions)
        {
            if (ContainsLayer(layers, definition.Name)) continue;

            int index = layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            layer.FindPropertyRelative("name").stringValue = definition.Name;
            layer.FindPropertyRelative("uniqueID").intValue = definition.UniqueId;
            layer.FindPropertyRelative("locked").boolValue = false;
        }

        tagManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tagManagerAssets[0]);
    }

    private static bool ContainsLayer(SerializedProperty layers, string layerName)
    {
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layerName)
                return true;
        }

        return false;
    }

    private static void ConfigurePrefab(PrefabRule rule)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(rule.Path);
        try
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                ApplyRendererRule(renderer, rule.SortingLayer, rule.SortingOrder);

            NormalizeRenderableZ(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, rule.Path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureScene(string scenePath, bool configureGameplay)
    {
        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene targetScene = SceneManager.GetSceneByPath(scenePath);
        bool wasAlreadyLoaded = targetScene.IsValid() && targetScene.isLoaded;

        if (wasAlreadyLoaded && targetScene.isDirty && !EditorSceneManager.SaveScene(targetScene, scenePath))
            throw new InvalidOperationException($"Could not save existing changes in '{scenePath}' before sorting setup.");

        if (!wasAlreadyLoaded)
            targetScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        SceneManager.SetActiveScene(targetScene);

        if (configureGameplay)
            ConfigureGameplayScene(targetScene);

        ConfigureCanvases(targetScene);
        EditorSceneManager.MarkSceneDirty(targetScene);

        if (!EditorSceneManager.SaveScene(targetScene, scenePath))
            throw new InvalidOperationException($"Failed to save sorting changes to '{scenePath}'.");

        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);

        if (!wasAlreadyLoaded)
            EditorSceneManager.CloseScene(targetScene, true);
    }

    private static void ConfigureGameplayScene(Scene scene)
    {
        ApplyNamedSpriteRule(scene, "Map3", SortingLayerNames.Ground, -30, true);
        ApplyNamedSpriteRule(scene, "Map2", SortingLayerNames.Ground, -20, true);
        ApplyNamedSpriteRule(scene, "Map1", SortingLayerNames.Ground, 30, true);
        ApplyNamedSpriteRule(scene, "Tower", SortingLayerNames.Environment, 10, true);
        ApplyGlobalFogRule(scene);
        ApplyMapZoneOrders(scene, "Map1", 30, 30);
        ApplyMapZoneOrders(scene, "Map2", -20, 20);
        ApplyMapZoneOrders(scene, "Map3", -30, 10);
    }

    private static void ApplyGlobalFogRule(Scene scene)
    {
        GameObject fog = FindGameObject(scene, "ToxicFog");
        if (fog == null)
            fog = FindGameObject(scene, "Toxic Fog");
        if (fog == null) return;

        TilemapRenderer tilemapRenderer = fog.GetComponent<TilemapRenderer>();
        if (tilemapRenderer != null)
            ApplyRendererRule(tilemapRenderer, SortingLayerNames.Ground, 0);

        SetLocalZToZero(fog.transform);
    }

    private static void ApplyMapZoneOrders(
        Scene scene,
        string mapName,
        int lockedSortingOrder,
        int unlockedSortingOrder)
    {
        GameObject map = FindGameObject(scene, mapName);
        if (map == null) return;
        MapZone mapZone = map.GetComponent<MapZone>();
        if (mapZone == null) return;

        SerializedObject serializedMapZone = new SerializedObject(mapZone);
        serializedMapZone.FindProperty("lockedSortingOrder").intValue = lockedSortingOrder;
        serializedMapZone.FindProperty("unlockedSortingOrder").intValue = unlockedSortingOrder;
        serializedMapZone.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mapZone);
    }

    private static void ApplyNamedSpriteRule(
        Scene scene,
        string objectName,
        string sortingLayer,
        int sortingOrder,
        bool normalizeZ)
    {
        GameObject target = FindGameObject(scene, objectName);
        if (target == null) return;

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer != null)
            ApplyRendererRule(renderer, sortingLayer, sortingOrder);

        if (normalizeZ)
            SetLocalZToZero(target.transform);
    }

    private static GameObject FindGameObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    return transform.gameObject;
            }
        }

        return null;
    }

    private static void ConfigureCanvases(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.sortingLayerName = SortingLayerNames.UI;
                EditorUtility.SetDirty(canvas);
            }
        }
    }

    private static void ApplyRendererRule(Renderer renderer, string sortingLayer, int sortingOrder)
    {
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        EditorUtility.SetDirty(renderer);
    }

    private static void NormalizeRenderableZ(Transform root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            SetLocalZToZero(renderer.transform);
    }

    private static void SetLocalZToZero(Transform transform)
    {
        Vector3 localPosition = transform.localPosition;
        if (Mathf.Approximately(localPosition.z, 0f)) return;

        localPosition.z = 0f;
        transform.localPosition = localPosition;
        EditorUtility.SetDirty(transform);
    }

    private readonly struct LayerDefinition
    {
        public LayerDefinition(string name, int uniqueId)
        {
            Name = name;
            UniqueId = uniqueId;
        }

        public string Name { get; }
        public int UniqueId { get; }
    }

    private readonly struct PrefabRule
    {
        public PrefabRule(string path, string sortingLayer, int sortingOrder)
        {
            Path = path;
            SortingLayer = sortingLayer;
            SortingOrder = sortingOrder;
        }

        public string Path { get; }
        public string SortingLayer { get; }
        public int SortingOrder { get; }
    }
}
