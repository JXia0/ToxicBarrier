# 玩家管理范围

玩家模块保留一套移动实现：

- `PlayerController`：WASD 移动、左右朝向、数字键 1–5，以及按地图环半径选择移动速度。
- `PlayerInventory`：Gel / Teeth / Wood / Stone 的数量、消耗与变更事件。
- `InventoryPanelUI`：显示背包资源数量。
- `PlayerMapBounds`：读取 `MapController` 的已解锁半径并限制玩家位置。

当前场景是以世界原点为中心的三层同心圆。移动速度直接放在 `PlayerController`，不再经过备用移动脚本、`MapScale` 或 `MapController` 转发。
