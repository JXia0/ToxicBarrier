using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class NavMesh2DSetupTool
{
    private const string BuildingAssetRoot =
        "Assets/PeterRadiant/Tiny Swords/Buildings/";
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string NavMeshAssetPath = "Assets/Scenes/NavMesh-Navigation2D.asset";
    private const string ObstacleLayerName = "Obstacle";
    private const string NavigationLayerName = "Navigation";

    private static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Prefab/Bug.prefab",
        "Assets/Prefab/Leader.prefab",
        "Assets/Prefab/Boss.prefab"
    };

    [MenuItem("Tools/Toxic Barrier/Setup 2D NavMesh")]
    public static void SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        int obstacleLayer = EnsureLayerExists(ObstacleLayerName);
        int navigationLayer = EnsureLayerExists(NavigationLayerName);
        if (obstacleLayer < 0 || navigationLayer < 0)
        {
            Debug.LogError("No free User Layer is available for 2D navigation.");
            return;
        }

        int buildingCount = SetupBuildingObstacles(scene, obstacleLayer);
        SetupNavigationGround(scene, navigationLayer);
        NavMeshSurface surface = SetupNavigationSurface(
            scene,
            navigationLayer,
            obstacleLayer);
        SetupEnemyPrefabs();
        BakeNavigation(surface);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"2D NavMesh setup complete: {buildingCount} building obstacle(s) baked.");
    }

    private static int SetupBuildingObstacles(Scene scene, int obstacleLayer)
    {
        int count = 0;
        SpriteRenderer[] renderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer == null || spriteRenderer.gameObject.scene != scene ||
                spriteRenderer.sprite == null)
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(spriteRenderer.sprite);
            if (!assetPath.StartsWith(BuildingAssetRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            GameObject building = spriteRenderer.gameObject;
            Undo.RecordObject(building, "Setup NavMesh building obstacle");
            Undo.RecordObject(spriteRenderer, "Set building sorting layer");
            building.layer = obstacleLayer;
            spriteRenderer.sortingLayerName = "Environment";

            BoxCollider2D collider2D = building.GetComponent<BoxCollider2D>();
            if (collider2D == null)
                collider2D = Undo.AddComponent<BoxCollider2D>(building);
            else
                Undo.RecordObject(collider2D, "Fit building collider");
            FitFootprintCollider(collider2D, spriteRenderer.sprite);

            Transform proxyTransform = building.transform.Find("NavigationObstacle");
            GameObject proxy;
            if (proxyTransform == null)
            {
                proxy = new GameObject("NavigationObstacle");
                Undo.RegisterCreatedObjectUndo(proxy, "Create NavMesh obstacle proxy");
                proxy.transform.SetParent(building.transform, false);
            }
            else
            {
                proxy = proxyTransform.gameObject;
            }

            Undo.RecordObject(proxy, "Configure NavMesh obstacle proxy");
            proxy.layer = obstacleLayer;
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;

            BoxCollider navCollider = proxy.GetComponent<BoxCollider>();
            if (navCollider == null)
                navCollider = Undo.AddComponent<BoxCollider>(proxy);
            else
                Undo.RecordObject(navCollider, "Fit NavMesh obstacle proxy");

            navCollider.center = new Vector3(
                collider2D.offset.x,
                collider2D.offset.y,
                0.45f);
            navCollider.size = new Vector3(
                collider2D.size.x,
                collider2D.size.y,
                1f);
            navCollider.isTrigger = false;
            count++;
        }

        return count;
    }

    private static void SetupNavigationGround(Scene scene, int navigationLayer)
    {
        GameObject ground = FindOrCreateSceneObject(scene, "NavigationGround");
        Undo.RecordObject(ground, "Setup navigation ground");
        ground.layer = navigationLayer;
        ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ground.transform.localScale = Vector3.one;

        BoxCollider collider = ground.GetComponent<BoxCollider>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider>(ground);
        else
            Undo.RecordObject(collider, "Resize navigation ground");

        collider.center = new Vector3(0f, 0f, -0.05f);
        collider.size = new Vector3(74f, 74f, 0.1f);
        collider.isTrigger = false;
    }

    private static NavMeshSurface SetupNavigationSurface(
        Scene scene,
        int navigationLayer,
        int obstacleLayer)
    {
        GameObject navigation = FindOrCreateSceneObject(scene, "Navigation2D");
        Undo.RecordObject(navigation, "Setup 2D NavMesh surface");
        navigation.layer = navigationLayer;
        navigation.transform.SetPositionAndRotation(
            Vector3.zero,
            Quaternion.Euler(90f, 0f, 0f));
        navigation.transform.localScale = Vector3.one;

        NavMeshSurface surface = navigation.GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = Undo.AddComponent<NavMeshSurface>(navigation);
        else
            Undo.RecordObject(surface, "Configure 2D NavMesh surface");

        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = (1 << navigationLayer) | (1 << obstacleLayer);
        return surface;
    }

    private static void SetupEnemyPrefabs()
    {
        foreach (string prefabPath in EnemyPrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
                if (agent == null)
                    agent = root.AddComponent<NavMeshAgent>();

                ConfigureAgent(agent);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void ConfigureAgent(NavMeshAgent agent)
    {
        agent.agentTypeID = 0;
        agent.radius = 0.25f;
        agent.height = 0.6f;
        agent.baseOffset = 0f;
        agent.speed = 2.5f;
        agent.acceleration = 40f;
        agent.angularSpeed = 0f;
        agent.stoppingDistance = 0.75f;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    }

    private static void BakeNavigation(NavMeshSurface surface)
    {
        if (surface.navMeshData != null)
            surface.RemoveData();
        surface.navMeshData = null;

        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath) != null)
            AssetDatabase.DeleteAsset(NavMeshAssetPath);

        surface.BuildNavMesh();
        if (surface.navMeshData == null)
        {
            Debug.LogError("2D NavMesh bake produced no data.", surface);
            return;
        }

        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
        EditorUtility.SetDirty(surface);
    }

    private static GameObject FindOrCreateSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;
        }

        GameObject created = new GameObject(objectName);
        SceneManager.MoveGameObjectToScene(created, scene);
        Undo.RegisterCreatedObjectUndo(created, $"Create {objectName}");
        return created;
    }

    private static void FitFootprintCollider(BoxCollider2D boxCollider, Sprite sprite)
    {
        Bounds bounds = sprite.bounds;
        GetFootprintRatios(sprite.name, out float widthRatio, out float heightRatio);

        float colliderHeight = bounds.size.y * heightRatio;
        float bottomPadding = bounds.size.y * 0.025f;
        boxCollider.size = new Vector2(bounds.size.x * widthRatio, colliderHeight);
        boxCollider.offset = new Vector2(
            bounds.center.x,
            bounds.min.y + bottomPadding + colliderHeight * 0.5f);
        boxCollider.isTrigger = false;
    }

    private static void GetFootprintRatios(
        string buildingName,
        out float widthRatio,
        out float heightRatio)
    {
        widthRatio = 0.8f;
        heightRatio = 0.24f;

        if (buildingName.IndexOf("Castle", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            widthRatio = 0.86f;
            heightRatio = 0.28f;
        }
        else if (buildingName.IndexOf("Archery", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            widthRatio = 0.86f;
            heightRatio = 0.25f;
        }
        else if (buildingName.IndexOf("Monastery", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            widthRatio = 0.72f;
            heightRatio = 0.2f;
        }
        else if (buildingName.IndexOf("Tower", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            widthRatio = 0.72f;
            heightRatio = 0.22f;
        }
        else if (buildingName.IndexOf("House", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            widthRatio = 0.78f;
            heightRatio = 0.22f;
        }
    }

    private static int EnsureLayerExists(string layerName)
    {
        int existingLayer = LayerMask.NameToLayer(layerName);
        if (existingLayer >= 0)
            return existingLayer;

        UnityEngine.Object tagManagerAsset =
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        SerializedObject tagManager = new SerializedObject(tagManagerAsset);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue))
                continue;

            layer.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return i;
        }

        return -1;
    }
}
