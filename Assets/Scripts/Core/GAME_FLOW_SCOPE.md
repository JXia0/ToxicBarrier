# Game Flow 职责与状态整理

本文用于下一阶段实现 `GameFlowManager`。当前阶段只确定游戏流程、状态边界和各入口需要执行的动作，不在地图、玩家或 UI 脚本中提前实现流程控制。

## 当前项目现状

- Build Settings 目前只有 `Assets/Scenes/SampleScene.unity`，尚无 Main Menu 或 Death Scene。
- 进入 `SampleScene` 后游戏立即开始：
  - `EnemySpawner.Start()` 自动开始波次。
  - `EnvironmentalResourceSpawner.Start()` 自动生成环境资源。
  - `MapController.Update()` 持续推进毒气周期。
  - `PlayerController.Update()` 立即接收移动和物品栏输入。
  - Inventory、Tower Upgrade 和 Parasite Warning UI 各自创建或刷新自己的界面。
- 当前没有暂停输入、`Time.timeScale` 管理、重新加载场景或返回主界面的逻辑。
- 玩家死亡目前只由 `HealthSystem.OnDeath` 发出事件，没有任何对象订阅它来结束游戏。因此玩家死亡后：
  - 玩家控制不会被明确关闭；
  - 波次、敌人、毒气和其他系统仍会继续运行；
  - 不会显示 Death UI，也不能重新开始或返回主界面。
- `MapController.SetPlayerControlEnabled()` 目前只是一个转发入口，没有调用者。游戏流程不应继续放进 `MapController` 或 `PlayerMapBounds`。

## 目标状态

建议由 `GameFlowManager` 统一维护以下状态：

| 状态 | 含义 | 游戏时间 | 玩家控制 | 可见的流程 UI |
| --- | --- | --- | --- | --- |
| `MainMenu` | 位于主界面，尚未开始本局游戏 | 正常（1） | 无 | Main Menu |
| `Playing` | 正常游戏中 | 正常（1） | 开启 | 游戏 HUD |
| `Paused` | 玩家主动暂停 | 停止（0） | 关闭并清空移动速度 | Pause Menu |
| `Dead` | 玩家已经死亡 | 停止（0） | 关闭并清空移动速度 | Death UI |
| `Transitioning` | 正在加载或切换场景，防止重复点击 | 正常（1） | 关闭 | 可选 Loading UI |

合法的主要状态转换：

```text
MainMenu -> Transitioning -> Playing
Playing <-> Paused
Playing -> Dead
Paused -> Transitioning -> Playing       (Restart)
Dead -> Transitioning -> Playing         (Restart)
Paused -> Transitioning -> MainMenu
Dead -> Transitioning -> MainMenu
```

`Dead` 不能通过 Escape 进入 Pause，也不能直接恢复为 `Playing`。重复收到玩家死亡事件时必须保持幂等，只处理第一次。

## 各流程入口需要发生什么

### 1. 游戏开始 / Start New Game

入口：Main Menu 的 Start 按钮。

执行顺序：

1. 将状态设为 `Transitioning`，临时禁止重复点击。
2. 明确恢复 `Time.timeScale = 1`，避免从暂停或死亡状态带入新场景。
3. 加载 Gameplay Scene。当前可继续使用 `SampleScene`，后续可再重命名；不要在多个 UI 脚本中散落场景名或 Build Index。
4. Gameplay Scene 加载完成后找到并订阅玩家 `HealthSystem.OnDeath`。
5. 隐藏 Pause Menu 和 Death UI，显示游戏 HUD。
6. 开启玩家控制，将状态设为 `Playing`。
7. 波次、资源、毒气、塔、背包和地图均以新场景的初始值开始。

当前这些系统依赖各自的 `Start()` 自动启动。下一阶段可以保留这种领域内自启动方式，由 `GameFlowManager` 只负责允许游戏进入 `Playing`；如果以后需要开场倒计时，再将 Spawner 改为响应状态进入 `Playing`，不要让 Manager 直接持有每一种生成器的细节。

### 2. 暂停 / Pause

入口：仅在 `Playing` 状态按 Escape，或点击游戏内 Pause 按钮。

执行顺序：

1. 状态切换为 `Paused`。
2. 关闭玩家输入，并将 Rigidbody2D 速度清零，避免恢复时继续沿旧方向移动。
3. 设置 `Time.timeScale = 0`，统一冻结：
   - Rigidbody2D 物理；
   - 敌人和塔的攻击、移动与冷却；
   - `WaitForSeconds` 驱动的波次与资源生成；
   - 毒气计时与持续伤害；
   - 伪装怪伤害间隔。
4. 显示 Pause Menu，并把 UI 输入焦点交给菜单。
5. 游戏 HUD 可以保留为背景，但 Tower Upgrade 等可交互 Gameplay UI 必须不可点击。
6. Parasite Warning 当前使用 unscaled time，暂停时仍会闪烁且 Sorting Order 很高；下一阶段必须在非 `Playing` 状态隐藏，或保证 Pause/Death UI 显示在它上方。

Pause Menu 中的按钮只负责调用 `GameFlowManager.ResumeGame()`、`RestartGame()`、`ReturnToMainMenu()`，不直接修改 `Time.timeScale` 或加载场景。

### 3. 继续 / Resume

入口：仅在 `Paused` 状态按 Escape，或点击 Resume。

执行顺序：

1. 隐藏 Pause Menu。
2. 设置 `Time.timeScale = 1`。
3. 恢复玩家控制。
4. 状态切换为 `Playing`。

恢复操作只继续当前局，不重置 HP、背包、塔等级、波次、地图解锁或毒气周期。

### 4. 玩家死亡 / Player Death

入口：Gameplay Scene 中玩家 `HealthSystem.OnDeath`。

执行顺序：

1. 若当前状态不是 `Playing`，或死亡已经处理过，则忽略重复事件。
2. 状态切换为 `Dead`。
3. 关闭玩家控制并清空 Rigidbody2D 速度。
4. 设置 `Time.timeScale = 0`，冻结敌人、波次、毒气、掉落物和陷阱，保留死亡瞬间作为背景。
5. 关闭 Pause Menu、Tower Upgrade 等 Gameplay 交互界面。
6. 显示 Death UI；淡入动画如有需要必须使用 unscaled time。
7. Death UI 提供 Restart 和 Return to Main Menu，不允许 Resume。

当前项目更适合使用 Gameplay Scene 内的 Death UI，而不是立即切换到独立 Death Scene：可以保留死亡现场作为背景，Restart 也可以统一为重新加载 Gameplay Scene。如果以后确实采用 Death Scene，死亡状态和按钮仍由 `GameFlowManager` 管理，只有表现层改为加载场景。

### 5. 重新开始 / Restart

入口：Pause Menu 或 Death UI 的 Restart 按钮。

执行顺序：

1. 状态切换为 `Transitioning`，阻止重复点击和 Escape 切换。
2. 关闭玩家控制和流程菜单。
3. 在加载前明确设置 `Time.timeScale = 1`。
4. 解除旧玩家的死亡事件订阅。
5. 重新加载 Gameplay Scene，而不是逐个手工重置对象。
6. 场景加载后重新绑定玩家死亡事件，并进入 `Playing`。

重新加载场景会自然重置当前所有本局数据：

- 玩家位置、HP 和背包；
- 塔等级、HP、精灵和地图解锁；
- 毒气计时和累计周期；
- 波次进度、存活敌人和攻击冷却；
- 环境资源、敌人掉落和伪装怪；
- 所有运行时创建的 HUD 与提示 UI。

项目目前没有存档或跨局保留规则，因此 Restart 默认是完整新局。未来若加入设置、成就等跨局数据，应由独立持久化系统保存，不放入场景内的 Gameplay 对象。

### 6. 返回主界面 / Return to Main Menu

入口：Pause Menu 或 Death UI 的 Main Menu 按钮。

执行顺序：

1. 状态切换为 `Transitioning`。
2. 关闭玩家控制和当前流程菜单。
3. 在卸载 Gameplay Scene 前设置 `Time.timeScale = 1`。
4. 解除玩家死亡事件和场景级 UI 的绑定。
5. 加载 Main Menu Scene，卸载本局所有 Gameplay 对象。
6. 场景加载完成后状态切换为 `MainMenu`，只显示 Main Menu。

Main Menu Scene 尚未存在。下一阶段需要创建并加入 Build Settings；建议放在 Gameplay Scene 前，但场景切换仍使用集中配置的场景名，不依赖散落的数字 Build Index。

## `GameFlowManager` 应拥有的职责

- 保存只读公开的当前 `GameState`，并发出状态变化事件。
- 校验状态转换，防止 Pause、Death 和场景加载互相覆盖。
- 统一处理 Escape/Pause 输入。
- 统一设置和恢复 `Time.timeScale`。
- 订阅玩家 `HealthSystem.OnDeath` 并进入 `Dead`。
- 统一执行 Start、Resume、Restart、Return to Main Menu 的场景与状态转换。
- 统一启停玩家控制，确保停用时速度清零。
- 协调 Main Menu、Pause Menu、Death UI 和 Gameplay HUD 的可见性与交互性。
- 在 Manager 销毁、退出 Play Mode 或切换场景前确保 `Time.timeScale` 恢复为 1。

建议 `GameFlowManager` 作为持久 Singleton 跨场景存在。场景内 UI 通过轻量 View/Presenter 在 `OnEnable` 注册、`OnDisable` 注销，避免持久 Manager 长期持有已经卸载的 Scene Object 引用。

## 不属于 `GameFlowManager` 的职责

- `HealthSystem` 只计算 HP 并发出死亡事件，不显示 Death UI、不加载场景。
- `PlayerController` 只处理角色移动和角色输入，不处理 Pause/Menu/Restart。
- `PlayerMapBounds` 只限制地图边界，不判断游戏是否开始或结束。
- `MapController` 只管理地图解锁与毒气规则，不作为玩家控制或流程 UI 的中转站。
- `EnemySpawner` 和 `EnvironmentalResourceSpawner` 管理各自的生成过程，不加载场景或显示菜单。
- Main/Pause/Death UI 按钮只转发意图给 `GameFlowManager`，不自行修改全局状态。

## 下一阶段实施清单

1. 创建 `GameState` 与 `GameFlowManager`，实现受控状态转换。
2. 创建 Main Menu Scene，并把 Gameplay Scene 名集中配置到 Manager。
3. 创建 Main Menu、Pause Menu、Death UI 的场景级 View。
4. 将 Escape 输入、`Time.timeScale` 和 SceneManager 调用全部收口到 Manager。
5. 为 `PlayerController` 增加明确的控制开关/停止移动接口，并移除 `MapController.SetPlayerControlEnabled()` 这一流程转发职责。
6. 绑定和解绑玩家 `HealthSystem.OnDeath`。
7. 让 Tower Upgrade、Parasite Warning 等 Gameplay UI 根据状态禁用或隐藏。
8. 验证 Start、Pause/Resume、死亡、Restart、Return Main Menu 的完整闭环以及重复点击防护。

