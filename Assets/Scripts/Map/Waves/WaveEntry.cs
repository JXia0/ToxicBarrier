using UnityEngine;

[System.Serializable]
public class WaveEntry
{
    [Header("Budget")]
    [Min(0)]
    public int budget = 200;

    [Header("Timing")]
    [Tooltip("Minimum time before the next wave may start (even if enemies are nearly cleared).")]
    [Min(0f)]
    public float minDelayBeforeNext = 5f;

    [Tooltip("Maximum time before the next wave is forced to start (even if enemies remain).")]
    [Min(0f)]
    public float maxDelayBeforeNext = 15f;

    [Header("Speed-up trigger")]
    [Tooltip("When alive enemies are <= this threshold AND minDelayBeforeNext has elapsed, the next wave may start early.")]
    [Min(0)]
    public int minRemainingEnemiesToSpeedUp = 2;

    [Header("Lane balancing (optional hints)")]
    [Tooltip("If your spawner supports lanes, this nudges distribution away from single-lane stacking.")]
    [Range(0f, 1f)]
    public float laneBalanceStrength = 0.5f;
}

