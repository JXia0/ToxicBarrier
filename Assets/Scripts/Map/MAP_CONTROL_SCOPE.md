# Map Control 范围

地图系统只负责三件事：

- `MapZone`：保存地图层级、环形范围和迷雾显示。
- `MapController`：根据中央塔等级判断解锁，并提供当前允许的移动半径。
- `PlayerMapBounds`：把玩家限制在已解锁的最外层地图内。

生成系统采用直接绑定：

- `EnvironmentalResourceSpawner` 直接绑定所在的 `MapZone`。
- `EnemySpawner` 直接绑定所在的 `MapZone` 和 `LevelWavesConfig`。
- 不维护“玩家当前 Map ID”，也不通过全局事件动态切换波次配置。

场景固定为三层同心圆：Map 1 始终解锁，Map 2 / 3 分别在塔达到等级 2 / 3 时解锁。

游戏开始、暂停、玩家死亡、重新开始和返回主界面不属于地图控制。`MapController.SetPlayerControlEnabled()` 是待迁移的旧转发入口；下一阶段由 `GameFlowManager` 直接协调玩家控制，`MapController` 继续只负责地图解锁与毒气规则。
