using TMPro;
using UnityEngine;

/// <summary>Scene-authored survival, wave and poison readouts. Does not own gameplay rules.</summary>
public class GameHudUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI regionText;
    [SerializeField] private TextMeshProUGUI threatText;
    [SerializeField] private TextMeshProUGUI towerText;
    [SerializeField] private Tower tower;
    [SerializeField] private EnemySpawner[] spawners;
    private float _refreshAt;
    private GameState _lastState;
    private PlayerInventory _inventory;

    private void Update()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null) return;
        if (_lastState == flow.CurrentState && Time.unscaledTime < _refreshAt) return;
        _lastState = flow.CurrentState;
        _refreshAt = Time.unscaledTime + 0.2f;
        if (timeText != null)
            timeText.text = GameText.Translate($"{Mathf.FloorToInt(flow.SurvivalSeconds / 60):00}:{Mathf.FloorToInt(flow.SurvivalSeconds % 60):00}  |  击败 {flow.DefeatedEnemies}");
        if (towerText != null && tower != null) towerText.text = GameText.Translate($"净化塔  {tower.CurrentLevel} 级");
        if (objectiveText != null && tower != null)
            objectiveText.text = GameText.Translate(Objective(flow));
        if (regionText != null && spawners != null)
        {
            string value = string.Empty;
            for (int i = 0; i < spawners.Length; i++)
            {
                EnemySpawner wave = spawners[i];
                if (wave == null) continue;
                string state = !wave.IsUnlocked ? "未解锁" : wave.IsCleared ? "已净化" :
                    wave.HasFinishedSpawning ? $"清场中 · 剩余 {wave.AliveEnemies}" :
                    wave.IsWaitingForNextWave ? $"{wave.CurrentWaveNumber}/{wave.TotalWaves} 波 · {Mathf.CeilToInt(wave.NextWaveSeconds)}秒后" :
                    $"{wave.CurrentWaveNumber}/{wave.TotalWaves} 波 · {wave.AliveEnemies} 敌人";
                value += $"第 {wave.RegionLevel} 区  {state}" + (i < spawners.Length - 1 ? "\n" : "");
            }
            regionText.text = GameText.Translate(value);
        }
        MapController map = MapController.Instance;
        if (threatText != null && map != null)
        {
            bool safe = map.IsPlayerInMap1();
            float distance = tower != null && map.Player != null ? Vector2.Distance(tower.transform.position, map.Player.transform.position) : 0;
            threatText.text = GameText.Translate(map.PlayerHealth != null && map.PlayerHealth.IsRecovering ? "返塔恢复中  |  每秒恢复 8 生命" :
                safe ? "安全区  |  塔自动攻击；返塔停留可回血，WASD 移动 / ESC 暂停" : $"毒气区 · 每秒 -{map.PlayerHealth.ToxicDamagePerSecond:0.#} 生命  |  净化塔 {distance:0}米 {TowerDirection(map)}");
            threatText.color = safe ? new Color(0.73f, 0.9f, 0.69f) : new Color(1f, 0.58f, 0.4f);
        }
    }

    private string Objective(GameFlowManager flow)
    {
        if (_inventory == null && MapController.Instance != null && MapController.Instance.Player != null)
            _inventory = MapController.Instance.Player.GetComponent<PlayerInventory>();
        RegionSupplyCache cache = flow.ActiveSupplyCache;
        if (cache != null && MapController.Instance.Player != null)
        {
            Vector2 delta = cache.transform.position - MapController.Instance.Player.transform.position;
            return $"清区补给 · 金色标记 {Direction(delta)} {delta.magnitude:0}米  |  靠近领取升级材料和 25 生命";
        }
        if (!tower.IsMaxLevel && _inventory != null)
        {
            var cost = tower.GetUpgradeCostForNextLevel();
            bool ready = _inventory.GetCount(LootManager.ResourceType.Wood) >= cost.wood &&
                _inventory.GetCount(LootManager.ResourceType.Stone) >= cost.stone &&
                _inventory.GetCount(LootManager.ResourceType.Gel) >= cost.gel &&
                _inventory.GetCount(LootManager.ResourceType.Teeth) >= cost.teeth;
            if (ready) return tower.IsPlayerInUpgradeRange ? "材料已备齐  |  按 E 或点击按钮升级净化塔" : "材料已备齐  |  返回净化塔 " + TowerDirection(MapController.Instance);
        }
        if (flow.ShowSupplyNotice) return flow.LatestSupplyNotice;
        if (flow.SurvivalSeconds < 18) return "WASD 移动  |  采集、躲避，把敌人引向塔；清区后领取金色补给";
        if (tower.IsMaxLevel) return "最终任务  |  保护净化塔，清空三区全部波次和剩余敌人";
        return "采集可提前升级  |  清区后领取补给兜底，返塔按 E 升级";
    }

    private string TowerDirection(MapController map)
    {
        if (map == null || map.Player == null || tower == null) return string.Empty;
        Vector2 delta = tower.transform.position - map.Player.transform.position;
        return Direction(delta);
    }
    private static string Direction(Vector2 delta)
    {
        if (delta.magnitude < 2) return "（附近）";
        string vertical = Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * .5f ? (delta.y > 0 ? "北" : "南") : "";
        string horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * .5f ? (delta.x > 0 ? "东" : "西") : "";
        return "（" + horizontal + vertical + "）";
    }
}
