using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 玩家背包：订阅场景中的资源拾取，按类型累计数量。
/// 挂到 Player 上，玩家走过带 ResourcePickup 的物体时会自动收入背包。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Optional capacity (0 = unlimited)")]
    [SerializeField] private int maxSlotsPerResource = 0;

    /// <summary> (ResourceType, currentCount). 背包数量变化时触发，便于 UI 刷新。 </summary>
    public UnityEvent<LootManager.ResourceType, int> OnResourceCountChanged;

    private readonly Dictionary<LootManager.ResourceType, int> _counts = new Dictionary<LootManager.ResourceType, int>();

    private void OnEnable()
    {
        ResourcePickup.OnResourceCollected += OnResourceCollected;
    }

    private void OnDisable()
    {
        ResourcePickup.OnResourceCollected -= OnResourceCollected;
    }

    private void OnResourceCollected(LootManager.ResourceType type, int amount)
    {
        Add(type, amount);
    }

    /// <summary> 增加资源数量，受容量限制时只加到上限。 </summary>
    public void Add(LootManager.ResourceType type, int amount)
    {
        if (amount <= 0) return;

        int current = GetCount(type);
        int cap = maxSlotsPerResource > 0 ? maxSlotsPerResource : int.MaxValue;
        int add = Mathf.Min(amount, cap - current);
        if (add <= 0) return;

        _counts[type] = current + add;
        OnResourceCountChanged?.Invoke(type, _counts[type]);
    }

    /// <summary> 当前该类型资源数量。 </summary>
    public int GetCount(LootManager.ResourceType type)
    {
        return _counts.TryGetValue(type, out int n) ? n : 0;
    }

    /// <summary> 尝试消耗指定数量；足够则扣除并返回 true，否则返回 false。 </summary>
    public bool TryConsume(LootManager.ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        int current = GetCount(type);
        if (current < amount) return false;
        _counts[type] = current - amount;
        OnResourceCountChanged?.Invoke(type, _counts[type]);
        return true;
    }

    /// <summary> 获取所有非零资源（用于 UI 或存档）。 </summary>
    public IReadOnlyDictionary<LootManager.ResourceType, int> GetAllCounts()
    {
        return _counts;
    }
}
