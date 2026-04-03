using UnityEngine;

[CreateAssetMenu(menuName = "ToxicBarrier/Waves/Level Waves", fileName = "LevelWaves_Map")]
public class LevelWavesConfig : ScriptableObject
{
    [Header("Map binding")]
    [Tooltip("Which MapId this wave table is for (matches MapController.CurrentMapId / MapZone.MapId).")]
    [Min(1)]
    public int mapId = 1;

    [Header("Enemy roster (allowed list)")]
    [Tooltip("Enemy types that may appear in this level. The actual spawner can further filter by stage, etc.")]
    public EnemyTypeConfig[] enemyTypes;

    [Header("Waves")]
    public WaveEntry[] waves;
}

