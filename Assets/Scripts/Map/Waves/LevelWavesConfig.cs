using UnityEngine;

[CreateAssetMenu(menuName = "ToxicBarrier/Waves/Level Waves", fileName = "LevelWaves_Map")]
public class LevelWavesConfig : ScriptableObject
{
    [System.Serializable]
    public struct Wave
    {
        [Min(0)] public int budget;
        [Min(0f)] public float minDelayBeforeNext;
        [Min(0f)] public float maxDelayBeforeNext;
        [Min(0)] public int minRemainingEnemiesToSpeedUp;
    }

    public Wave[] waves;
}
