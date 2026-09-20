using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>English is the default. Both languages ship together and can be changed in Settings.</summary>
public static class GameText
{
    public static bool English => PlayerPrefs.GetInt("English", 1) == 1;
    public static event Action Changed;
    public static void SetEnglish(bool english) { PlayerPrefs.SetInt("English", english ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
    private static readonly Dictionary<string, string> Copy = new Dictionary<string, string> {
        {"修塔补给已送达  |  净化塔恢复 15% 生命", "REPAIR DELIVERED  |  Restored up to 15% tower HP"},
        {"已取得修塔补给  |  返回塔旁，修复 15% 塔生命", "REPAIR COLLECTED  |  Return to the tower to restore up to 15% HP"},
        {"修塔补给已保留  |  塔受损时在塔旁自动修复", "REPAIR SAVED  |  Repairs automatically near the tower when damaged"},
        {"携带修塔补给  |  返回净化塔", "CARRYING REPAIR  |  Return to tower"},
        {"修塔补给  |  带回塔旁修复 15% 生命，每次携带一份", "TOWER REPAIR  |  Carry one home to restore up to 15% HP"},
        {"侧路补给 [E]  |  主动领取材料、回血与毒气防护", "SUPPLIES [E]  |  Collect materials, healing and gas protection"},
        {"骸骨领主来袭  |  离开橙色预警圈，保护净化塔", "BONE WARDEN APPROACHES  |  Dodge orange circles. Protect the tower."},
        {"侧路补给  |  材料、35 生命、18 秒毒气防护", "SUPPLIES  |  Materials, +35 HP, 18s gas protection"},
        {"隐藏怪靠近  |  红色边缘越强越危险，绕开可疑掉落物", "HIDDEN ENEMY NEARBY  |  Stronger red edges mean danger. Avoid suspicious drops."},
        {"采集  /  守护  /  净化", "GATHER  /  DEFEND  /  RESTORE"}, {"毒雾之中，守住最后的庇护所", "A LAST REFUGE IN THE TOXIC WILDS"},
        {"毒雾\n屏障", "TOXIC\nBARRIER"}, {"升级净化塔，夺回被毒雾吞没的三区。", "Build the last defense. Reclaim all three regions."},
        {"开始游戏", "START GAME"}, {"退出", "QUIT"}, {"退出游戏", "QUIT GAME"}, {"暂停", "PAUSED"}, {"设置", "SETTINGS"}, {"暂歇片刻", "TAKE A BREATH"},
        {"游戏已暂停", "The world is paused."}, {"继续游戏", "RESUME"}, {"重新开始", "RESTART RUN"}, {"返回主菜单", "MAIN MENU"}, {"再玩一次", "PLAY AGAIN"}, {"暂停 [ESC]", "PAUSE [ESC]"},
        {"三区全部净化", "ALL THREE REGIONS RECLAIMED"}, {"防线未能守住", "THE WILDS CLAIM ANOTHER"}, {"屏障重生", "BARRIER RESTORED"}, {"守护失败", "DEFENSE LOST"},
        {"所有敌人已消灭，净化塔依然矗立。", "All waves defeated. Your tower still stands."}, {"屏障已经倒下。", "The barrier has fallen."}, {"正在进入屏障…", "ENTERING THE BARRIER..."}, {"幸存者", "SURVIVOR"},
        {"WASD 移动  |  返塔按 E 升级  |  ESC 继续", "WASD Move | E Upgrade near tower | ESC Resume"},
        {"WASD / 方向键移动，ESC 暂停\n玩家采集和躲避，净化塔自动攻击\n清区后前往金色标记领取升级补给\n返塔按 E 升级；塔旁脱战后持续回血", "WASD / ARROWS Move | ESC Pause\nGather and dodge; the tower auto-attacks\nClear regions, then collect the golden supplies\nE near tower: upgrade. Rest near tower to heal"},
        {"音乐音量", "MUSIC VOLUME"}, {"音效音量", "SOUND EFFECTS"}, {"切换分辨率", "RESOLUTION"}, {"切换全屏 / 窗口", "FULLSCREEN / WINDOW"},
        {"应用显示设置", "APPLY DISPLAY"}, {"保存并返回 [ESC]", "SAVE & BACK [ESC]"}, {"显示设置", "DISPLAY"},
        {"首次任务：把塔升至 3 级，清空三区敌人", "Upgrade to Lv.3 and clear all three regions"},
        {"点击应用显示设置以生效", "Click APPLY DISPLAY to confirm"}, {"显示设置已保存（编辑器中不改变窗口）", "Saved (Editor window stays unchanged)"},
        {"净化塔已满级", "Tower is already max level"}, {"材料不足", "Not enough materials"}, {"正在升级…", "UPGRADING..."}, {"升级成功", "Upgrade successful"}, {"升级已取消", "Upgrade cancelled"},
        {"守住净化塔，清空三区所有波次和敌人。", "Defend the tower and clear all three regions."},
        {"强化防御，恢复玩家与塔的全部生命。", "Stronger defenses. Fully restores you and the tower."},
        {"已达满级", "MAX LEVEL"}, {"升级净化塔 [E]", "UPGRADE TOWER [E]"}, {"最终防线已就绪", "The final defense is ready."}, {"材料已备齐", "Materials ready"},
        {"采集材料，或清区后领取补给", "Gather materials or collect clear rewards"},
        {"返塔恢复中  |  每秒恢复 8 生命", "RECOVERING  |  Restoring 8 HP per second"},
        {"安全区  |  塔自动攻击；返塔停留可回血，WASD 移动 / ESC 暂停", "SAFE ZONE  |  Rest near tower to heal. WASD Move / ESC Pause"},
        {"材料已备齐  |  按 E 或点击按钮升级净化塔", "UPGRADE READY  |  Press E or click Upgrade Tower"},
        {"材料已备齐  |  返回净化塔", "UPGRADE READY  |  Return to tower"},
        {"WASD 移动  |  采集、躲避，把敌人引向塔；清区后领取金色补给", "WASD Move  |  Gather, dodge and lure enemies to the tower"},
        {"最终任务  |  保护净化塔，清空三区全部波次和剩余敌人", "FINAL OBJECTIVE  |  Defend the tower and clear all remaining enemies"},
        {"采集可提前升级  |  清区后领取补给兜底，返塔按 E 升级", "Gather to upgrade early  |  Collect clear rewards; E near tower"},
        {"幸存者倒下了。留意血量，及时返塔恢复。", "You fell beyond the barrier. Return to the tower to recover."},
        {"净化塔已被摧毁。升级可以恢复塔的生命。", "The tower was destroyed. Upgrading restores its health."},
        {"三区已净化，屏障重新亮起。", "All three regions are clear. The barrier holds."}
    };
    private static readonly KeyValuePair<string, string>[] Replacements = Copy.OrderByDescending(p => p.Key.Length).ToArray();
    public static string Translate(string value)
    {
        if (!English || string.IsNullOrEmpty(value)) return value;
        if (Copy.TryGetValue(value, out string translated)) return translated;
        value = Regex.Replace(value, @"净化塔 (\d+) 级\s*→\s*(\d+) 级", "Tower Lv.$1 → Lv.$2");
        value = Regex.Replace(value, @"净化塔\s+(\d+)\s*级", "TOWER Lv.$1");
        value = Regex.Replace(value, @"塔 (\d+) 级", "TOWER Lv.$1");
        value = Regex.Replace(value, @"解锁第 (\d+) 区，恢复玩家与塔的全部生命。", "Unlocks REGION $1. Fully restores you and the tower.");
        value = Regex.Replace(value, @"第 (\d+) 区", "REGION $1");
        foreach (var pair in Replacements) value = value.Replace(pair.Key, pair.Value);
        return value.Replace("清区补给 · 金色标记", "SUPPLY CACHE").Replace("靠近领取升级材料和 25 生命", "Collect upgrade materials + 25 HP")
            .Replace("已领取", "Collected ").Replace("补给并恢复 25 生命，返塔升级！", " supplies + 25 HP. Return to upgrade!")
            .Replace("已清空！前往金色标记领取补给。", " CLEAR! Collect the golden supplies.")
            .Replace("毒气区 · 每秒 -", "TOXIC GAS · -").Replace(" 生命  |  净化塔", " HP/s  |  Tower")
            .Replace("材料已备齐  |  返回净化塔", "UPGRADE READY  |  Return to tower")
            .Replace("已满级", "MAX").Replace("未解锁", "LOCKED").Replace("已净化", "CLEAR").Replace("清场中 · 剩余", "FINAL · remaining")
            .Replace("秒后", "s next").Replace(" 敌人", " alive").Replace(" 波", " waves")
            .Replace("木材", "Wood").Replace("石料", "Stone").Replace("凝胶", "Gel").Replace("尖牙", "Teeth")
            .Replace("攻击", "ATK").Replace("生命", "HP").Replace("用时", "TIME").Replace("击败", "DEFEATED")
            .Replace("最快净化", "BEST CLEAR").Replace("通关", "WINS").Replace(" 次", "")
            .Replace("全屏", "Fullscreen").Replace("窗口", "Windowed")
            .Replace("（附近）", "(nearby)").Replace("东北", "NE").Replace("东南", "SE").Replace("西北", "NW").Replace("西南", "SW")
            .Replace("北", "N").Replace("南", "S").Replace("东", "E").Replace("西", "W").Replace("（", "(").Replace("）", ")").Replace("米", "m");
    }
}
