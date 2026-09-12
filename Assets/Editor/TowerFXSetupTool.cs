using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TowerFXSetupTool
{
    private const string Folder = "Assets/TowerFX/FX/FireFX_vol1/Prefabs/";
    static TowerFXSetupTool() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists("Temp/ToxicBarrierFX.setup")) return;
        File.Delete("Temp/ToxicBarrierFX.setup");
        try { Setup(); File.WriteAllText("Temp/ToxicBarrierFX.setup-result", "PASS"); }
        catch (Exception e) { File.WriteAllText("Temp/ToxicBarrierFX.setup-result", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Toxic Barrier/Configure Tower Particle Effects")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        EditorSceneManager.SaveOpenScenes();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/GameScene.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        Tower tower = UnityEngine.Object.FindObjectOfType<Tower>();
        if (tower == null) throw new InvalidOperationException("Tower missing");
        TowerParticleEffects effects = tower.GetComponent<TowerParticleEffects>();
        if (effects == null) effects = Undo.AddComponent<TowerParticleEffects>(tower.gameObject);
        SerializedObject data = new SerializedObject(effects);
        Assign(data, "upgradePrefabs", new[] { "FX_Fire_01", "FX_Fire_01_2" });
        Assign(data, "attackPrefabs", new[] { "FX_Fire_06", "FX_Fire_06 1", "FX_Fire_06_2" });
        data.FindProperty("effectRotation").vector3Value = Vector3.zero;
        data.FindProperty("footOffset").vector3Value = new Vector3(0, .1f, 0);
        data.FindProperty("upgradeScale").floatValue = .5f;
        data.ApplyModifiedProperties();
        SerializedObject attack = new SerializedObject(tower.GetComponent<TowerAttackBehavior>());
        attack.FindProperty("showLockTargetLine").boolValue = false;
        attack.FindProperty("showAttackFlashLine").boolValue = false;
        attack.FindProperty("attackEffectController").objectReferenceValue = null;
        attack.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        StringBuilder report = new StringBuilder();
        foreach (string path in Directory.GetFiles(Folder, "*.prefab"))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            report.AppendLine(prefab.name);
            foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                report.AppendLine($"  {ps.name}: size {ps.main.startSize.constantMax}, speed {ps.main.startSpeed.constantMax}, mode {r.renderMode}, mesh {(r.mesh != null ? r.mesh.bounds.size.ToString() : "none")}, scale {ps.transform.localScale}");
            }
        }
        Directory.CreateDirectory(".utmp/ui-checks");
        File.WriteAllText(".utmp/ui-checks/particle-assets.txt", report.ToString());
    }
    private static void Assign(SerializedObject data, string property, string[] names)
    {
        SerializedProperty array = data.FindProperty(property);
        array.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + names[i] + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing effect: " + names[i]);
            array.GetArrayElementAtIndex(i).objectReferenceValue = prefab;
        }
    }
}
