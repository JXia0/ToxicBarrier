using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameplayBalancePolishTool
{
    static GameplayBalancePolishTool() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists("Temp/ToxicBarrierBalance.polish")) return;
        File.Delete("Temp/ToxicBarrierBalance.polish");
        try { Apply(); File.WriteAllText("Temp/ToxicBarrierBalance.result", "PASS"); }
        catch (Exception e) { File.WriteAllText("Temp/ToxicBarrierBalance.result", "FAIL\n" + e); Debug.LogException(e); }
    }
    [MenuItem("Tools/Toxic Barrier/Apply Exploration and Combat Balance")]
    public static void Apply()
    {
        EditorSceneManager.SaveOpenScenes();
        string original = SceneManager.GetActiveScene().path;
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        Directory.CreateDirectory(".utmp/balance-backup");
        File.Copy(scene.path, ".utmp/balance-backup/GameScene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity");
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HealthSystem>(true)).Single(h => h.IsPlayer);
        var data = new SerializedObject(player);
        data.FindProperty("toxicGasBaseDamagePerSecond").floatValue = 1.2f;
        data.FindProperty("toxicGasDamageIncreasePerPeriod").floatValue = .1f;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Cinemachine.CinemachineVirtualCamera>(true)).ToArray())
        {
            camera.transform.SetParent(null, true);
            camera.transform.rotation = Quaternion.identity;
            camera.LookAt = null;
            var composer = camera.GetCinemachineComponent<Cinemachine.CinemachineComposer>();
            if (composer != null) composer.enabled = false;
            var body = camera.GetCinemachineComponent<Cinemachine.CinemachineTransposer>();
            if (body != null)
            {
                body.m_BindingMode = Cinemachine.CinemachineTransposer.BindingMode.WorldSpace;
                body.m_FollowOffset = new Vector3(0, 0, -10);
                body.m_XDamping = body.m_YDamping = .35f;
            }
            EditorUtility.SetDirty(camera);
            if (body != null) EditorUtility.SetDirty(body);
            if (composer != null) EditorUtility.SetDirty(composer);
        }
        foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            camera.transform.rotation = Quaternion.identity;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        for (int i = 1; i <= 3; i++)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath($"Assets/Sprites/Sprites_map_{i}.png");
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var platform = importer.GetPlatformTextureSettings("Standalone");
            if (platform.overridden) { platform.format = TextureImporterFormat.RGBA32; platform.textureCompression = TextureImporterCompression.Uncompressed; importer.SetPlatformTextureSettings(platform); }
            importer.SaveAndReimport();
        }
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Resources/Presentation/Chinese.asset");
        string copy = File.ReadAllText("Assets/Scripts/Core/GameText.cs") + File.ReadAllText("Assets/Scripts/Core/GameHudUI.cs");
        string missing = new string(copy.Where(c => c > 127 && !font.HasCharacter(c)).Distinct().ToArray());
        if (missing.Length > 0 && !font.TryAddCharacters(missing, out string unavailable))
            throw new Exception("Could not bake new Chinese glyphs: " + unavailable);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
        if (!string.IsNullOrEmpty(original) && original != scene.path) EditorSceneManager.OpenScene(original);
    }
}
