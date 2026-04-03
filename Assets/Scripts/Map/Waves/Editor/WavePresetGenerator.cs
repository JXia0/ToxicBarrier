using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates ready-to-use PvZ-style wave preset assets:
/// - EnemyTypeConfig: Bug/Leader/Boss
/// - LevelWavesConfig: Map1/Map2/Map3
/// </summary>
public static class WavePresetGenerator
{
    private const string RootFolder = "Assets/Configs/Waves";
    private const string EnemyFolder = RootFolder + "/EnemyTypes";
    private const string LevelFolder = RootFolder + "/Levels";

    [MenuItem("Tools/ToxicBarrier/Waves/Generate Default Wave Presets")]
    public static void GenerateDefaultWavePresets()
    {
        EnsureFolders();

        EnemyTypeConfig bug = CreateEnemyType("EnemyType_Bug", "bug", 50, 0);
        EnemyTypeConfig leader = CreateEnemyType("EnemyType_Leader", "leader", 120, 1);
        EnemyTypeConfig boss = CreateEnemyType("EnemyType_Boss", "boss", 320, 2);

        EnemyTypeConfig[] roster = { bug, leader, boss };

        CreateOrUpdateLevel("LevelWaves_Map1", 1, roster, new[]
        {
            Wave(200, 4f, 12f, 2),
            Wave(260, 4f, 12f, 2),
            Wave(320, 4f, 13f, 2),
            Wave(420, 5f, 14f, 2),
            Wave(520, 5f, 15f, 2),
            Wave(640, 6f, 16f, 3),
        });

        CreateOrUpdateLevel("LevelWaves_Map2", 2, roster, new[]
        {
            Wave(320, 4f, 11f, 2),
            Wave(420, 4f, 12f, 2),
            Wave(540, 4.5f, 13f, 2),
            Wave(680, 5f, 14f, 2),
            Wave(840, 5f, 15f, 3),
            Wave(1040, 5.5f, 16f, 3),
            Wave(1280, 6f, 17f, 3),
        });

        CreateOrUpdateLevel("LevelWaves_Map3", 3, roster, new[]
        {
            Wave(500, 4f, 10f, 2),
            Wave(680, 4f, 11f, 2),
            Wave(860, 4.5f, 12f, 2),
            Wave(1080, 5f, 13f, 2),
            Wave(1320, 5f, 14f, 3),
            Wave(1620, 5.5f, 15f, 3),
            Wave(1960, 5.5f, 16f, 3),
            Wave(2360, 6f, 17f, 4),
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Wave presets generated at Assets/Configs/Waves", Selection.activeObject);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Configs"))
            AssetDatabase.CreateFolder("Assets", "Configs");
        if (!AssetDatabase.IsValidFolder(RootFolder))
            AssetDatabase.CreateFolder("Assets/Configs", "Waves");
        if (!AssetDatabase.IsValidFolder(EnemyFolder))
            AssetDatabase.CreateFolder(RootFolder, "EnemyTypes");
        if (!AssetDatabase.IsValidFolder(LevelFolder))
            AssetDatabase.CreateFolder(RootFolder, "Levels");
    }

    private static EnemyTypeConfig CreateEnemyType(string fileName, string enemyId, int cost, int unlockStage)
    {
        string path = EnemyFolder + "/" + fileName + ".asset";
        EnemyTypeConfig cfg = AssetDatabase.LoadAssetAtPath<EnemyTypeConfig>(path);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<EnemyTypeConfig>();
            AssetDatabase.CreateAsset(cfg, path);
        }

        cfg.enemyId = enemyId;
        cfg.cost = Mathf.Max(1, cost);
        cfg.unlockStage = Mathf.Max(0, unlockStage);
        EditorUtility.SetDirty(cfg);
        return cfg;
    }

    private static void CreateOrUpdateLevel(string fileName, int mapId, EnemyTypeConfig[] roster, WaveEntry[] waves)
    {
        string path = LevelFolder + "/" + fileName + ".asset";
        LevelWavesConfig cfg = AssetDatabase.LoadAssetAtPath<LevelWavesConfig>(path);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<LevelWavesConfig>();
            AssetDatabase.CreateAsset(cfg, path);
        }

        cfg.mapId = Mathf.Max(1, mapId);
        cfg.enemyTypes = roster;
        cfg.waves = waves;
        EditorUtility.SetDirty(cfg);
    }

    private static WaveEntry Wave(int budget, float minDelay, float maxDelay, int speedUpThreshold)
    {
        return new WaveEntry
        {
            budget = Mathf.Max(0, budget),
            minDelayBeforeNext = Mathf.Max(0f, minDelay),
            maxDelayBeforeNext = Mathf.Max(minDelay, maxDelay),
            minRemainingEnemiesToSpeedUp = Mathf.Max(0, speedUpThreshold),
            laneBalanceStrength = 0.5f
        };
    }
}
