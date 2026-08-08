# 生成来源

当前项目只有三条生成路径：

1. `EnvironmentalResourceSpawner`
   - 每个地图环各放一个，并直接绑定对应的 `MapZone`。
   - 等待该环解锁后，在环形范围内生成 Wood / Stone。
   - 掉率由 `LootManager` 按 `MapZone.MapLevel` 查询。

2. `EnemySpawner`
   - 每个地图环各放一个，并直接绑定对应的 `MapZone` 与 `LevelWavesConfig`。
   - 等待该环解锁后执行自己的波次，不经过全局地图编号或配置选择器。

3. `Enemy` 死亡
   - 通过 `LootManager.RollEnemyLoot()` 在死亡位置生成 Gel / Teeth。

## 场景检查

- 三个资源生成器都要绑定各自的 `MapZone`。
- 三个敌人生成器都要绑定各自的 `MapZone`、敌人 Prefab 和波次配置。
- Map 1 / 2 / 3 的 `innerZone` 依次连接，避免生成区域重叠。
- `MapController` 要绑定三个 `MapZone` 和中央塔，供解锁与玩家边界使用。
