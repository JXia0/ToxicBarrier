using UnityEngine;

[CreateAssetMenu(menuName = "ToxicBarrier/Waves/Enemy Type", fileName = "EnemyType_")]
public class EnemyTypeConfig : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable ID for referencing this enemy type in wave generation/spawners.")]
    public string enemyId = "zombie_normal";

    [Header("Prefab (optional)")]
    [Tooltip("Optional: the enemy prefab to spawn. Leave empty if your spawn system uses enemyId instead.")]
    public GameObject prefab;

    [Header("Point system")]
    [Min(1)]
    public int cost = 50;

    [Header("Availability")]
    [Tooltip("Optional stage gate. 0 = always available; 1+ = later stages only.")]
    [Min(0)]
    public int unlockStage = 0;
}

