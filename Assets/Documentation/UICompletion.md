# Toxic Barrier 界面与流程收尾

## 本轮实现

- MainMenu：开始游戏、设置、退出、操作说明和最快通关纪录；地图与原版塔作背景。
- GameScene：暂停/继续/重开/返回主菜单；玩家死亡、塔毁失败；胜利结算。
- HUD：玩家与塔的独立血条、当前/最大血量、四种资源、三区波次与剩余敌人、局内时间、击败数、毒气/安全区提示和当前目标。
- 升级面板：靠近塔出现、离开隐藏；预览下级塔、攻击力与生命值变化、区域解锁、恢复血量；四种材料的持有量/需求量；缺材料禁用按钮；升级过程中防重复扣款；暂停和结算时隐藏。
- 胜利条件：塔达到 3 级，且三个 EnemySpawner 的所有波次生成结束、剩余敌人均为零。
- 通关补给：第一区清完时补足达到塔 2 级所缺的累计升级材料，第二区清完时补足达到塔 3 级所缺的材料。只发放差额，每区一局仅一次，HUD 显示补给通知。修复第一区不产石头、有限怪物掉落不足造成的进度死锁；清区前仍可正常捡材料提前升级。
- 失败条件：玩家血量归零，或中央净化塔血量归零。结算显示不同原因。
- 重开重新加载 GameScene，恢复时间、血量、材料、塔等级和地图/波次初始状态。

## Unity 中从哪里修改

所有主要 UI 均已保存为场景对象，退出 Play Mode 后也能选中编辑。

MainMenu 场景：

```text
GameFlowUI (GameFlowUI)
  GameFlowCanvas
    MainMenuPanel
      MapBackdrop / TowerArtwork / WorldCaption
      MenuCard
        Title / Subtitle / StartGameButton / QuitButton / HowToPlay
    LoadingPanel
```

GameScene 场景：

```text
GameFlowUI (GameFlowUI)
  GameFlowCanvas (排序 200)
    PauseButton
    PauseMenuPanel
    DeathPanel
    VictoryPanel
    LoadingPanel
GameplayUIRoot (GameHudUI / InventoryPanelUI / TowerUpgradePanelUI)
  HUDCanvas (排序 10)
    PlayerHealth / TowerHealth
    Resources / RegionProgress / RunTimer / JourneyStatus
  UpgradeCanvas (排序 30)
    TowerUpgradePanel
```

每个场景只保留一个 EventSystem。不要把素材包 Demo 的 EventSystem 或场景切换脚本拖进来。

修改静态文案：直接选中对应 TextMeshProUGUI。动态文案由 GameHudUI、TowerUpgradePanelUI、InventoryPanelUI、HealthControl 更新，需在对应脚本中调整。

按钮 On Click 保留 GameFlowUI 上的回调：StartNewGame、PauseGame、ResumeGame、RestartGame、ReturnToMainMenu、QuitGame。升级按钮由 TowerUpgradePanelUI 在运行时订阅，Inspector 不需要重复绑升级回调。

## 使用的素材

- 面板边框：`Assets/PeterRadiant/Tiny Swords/UI Elements/Wood Table/WoodTable.png`。
- 普通/返回按钮：同目录 UI Elements 下的 `Buttons/BigBlueButton_Regular.png`、`BigRedButton_Regular.png`。
- 血条：`Bars/BigBar_Base.png`、`BigBar_Fill.png`。
- 塔保持用户指定的三张 `Assets/Sprites/Sprites_tower*.png`，场景、主菜单与升级预览使用同一组素材。石料、凝胶、尖牙保留现有素材；木材复用 Tiny Swords 的原木图标。
- 中文字体：随项目分发的 Noto Sans CJK SC Regular（`Assets/Fonts/`，附 OFL 许可）；TMP 字库为 `Assets/Resources/Presentation/Chinese.asset`。

面板和按钮使用 Image Sliced；素材已有九宫格 Border。血条边框固定，内层 Image 使用 Filled / Horizontal / Left，通过 fillAmount 更新；HealthControl 同时订阅 HealthSystem.OnHpChanged 并更新数字。

Canvas 使用 Screen Space Overlay，参考分辨率 1920×1080，Scale With Screen Size，以高度匹配。主界面与 HUD 面向横屏桌面窗口设计；升级面板位于右下，给右上波次留出空间。

## 重建与验证

### 塔粒子特效

GameScene 中选中塔的 `TowerParticleEffects` 组件即可调整特效引用、大小和塔脚偏移。原始素材位于 `Assets/TowerFX/FX/FireFX_vol1/Prefabs/`，未修改原始预制体和材质：

- 升级：`FX_Fire_01` 与 `FX_Fire_01_2` 同时在塔脚播放，位于塔后方。
- 0／1 级攻击：`FX_Fire_06`。
- 2 级攻击：`FX_Fire_06 1`（名称中有空格）。
- 3 级攻击：`FX_Fire_06_2`。

攻击特效从塔身上部发出并朝目标敌人方向喷射；`Muzzle Height` 可调整发射口高度。伤害仍由原有塔攻击逻辑即时结算。攻击粒子最多保留 8 个实例，升级粒子复用两个实例；短暂发射后自动隐藏，随游戏暂停冻结，重开或切换场景时销毁。旧的红色锁定线、黑色攻击线和旧爆炸动画已关闭。

`Tools > Toxic Barrier > Configure Tower Particle Effects` 可重新绑定上述素材。

- `Tools > Toxic Barrier > Build Finished UI`：用生成器重建两个场景的 UI。会替换已生成的同名 UI 树，因此日常美术微调直接编辑场景即可；再次运行前应保存或提交想保留的手调内容。
- `ReleaseUISetupTool` 含完整布局、素材引用、按钮绑定和场景引用校验。
- 第一次重建前的场景备份在 `.utmp/ui-backup/`。
- 编辑器运行回归检查：在 `Temp/ToxicBarrierUI.test` 写入任意内容，Unity 完成编译后自动进入 Play Mode 检查，结果在 `.utmp/ui-checks/report.txt`，截图在同目录。
- 回归检查使用临时 Play Mode 材料和短波次，验证实际扣款、动画、生成器/死亡计数和场景切换，不修改正式波次配置；不等于完整游戏的难度与通关耗时测试。
- `Tools > Toxic Barrier > Build Windows Game`：生成 `Builds/ToxicBarrier/ToxicBarrier.exe`，默认 1280×720 窗口运行。
- `Tools > Toxic Barrier > Apply Release Polish`：在现有 UI 上应用中文字体、设置页与采集/毒气参数；保留用户指定的中央塔三套素材。重新执行旧的 Build Finished UI 后需再执行此项。

## 后续验收重点

本轮追加复查结果见 [FlowReview.md](FlowReview.md)：已修复建筑旁卡怪，正式 6/7/8 波防守路线成功进入胜利；新增 E 升级、自动暂停和分阶段提示。攻击特效已改为从塔发射。该路线通过不代表所有走位与难度均已验证。

本轮继续补入中文、音乐/音效、音量/分辨率/全屏设置、个人最快成绩、实体清区补给和返塔恢复。完整流程与验证边界见 FlowReview.md。

设置从主菜单或暂停菜单打开；ESC 先关闭设置，在暂停状态下不会直接恢复战斗。默认英文，通过 Language 按钮切换简体中文，两个版本同时保留。音量实时生效并在返回时保存；显示设置需要点击“应用显示设置”。PlayerPrefs 保存语言、设置、通关次数和最快用时，不保存局中进度。玩家蓝圈按用户要求取消，清区补给的金色标记保留。

音乐与反馈音效在 `Assets/Resources/Audio/`。按钮声音复用 Bloodlines UI；其他八段音频为项目原创合成，生成脚本为 `Tools/generate_audio.py`。攻击声音限制频率，避免满级每秒十二次攻击过于嘈杂。
