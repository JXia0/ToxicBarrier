# Map Control 範圍說明

**此 Tab 負責：所有 Map Control 相關邏輯。**

其他 Tab 分別負責：掉落、怪物刷新、玩家、血量管理等；後續可能再新增其他模組。

---

## 本 Tab 負責的腳本與職責

| 腳本 | 路徑 | 職責 |
|------|------|------|
| **MapController** | `Core/MapController.cs` | 中央地圖控制：Map ID、玩家引用、Loot/MapScale 引用、毒氣週期、**逐環解鎖**（Tower 等級）、GetMaxAllowedRadius |
| **MapZone** | `Map/MapZone.cs` | 三環地圖區域：Map ID、環半徑、玩家進入切換 Map（僅解鎖時）、**戰爭迷霧**（Map 2/3 鎖定時） |
| **MapRevealMask** | `Map/MapRevealMask.cs` | 掛在 Map 1：提供 Sprite Mask，使鎖定環的迷霧只顯示「Map 1 外」的一小部分 |
| **PlayerMapBounds** | `Player/PlayerMapBounds.cs` | 掛在玩家：將玩家位置限制在當前解鎖半徑內，無法進入未解鎖環 |
| **EnvironmentalResourceSpawner** | `Map/EnvironmentalResourceSpawner.cs` | 環境資源 (Wood/Stone) 在環內/矩形內生成、間隔與類型控制 |
| **MapScale** | `Map/MapScale.cs` | 地圖階級對應的玩家移動速度 (Tier 1–4) |

## 本 Tab 可動的邊界

- 修改、擴充上述腳本。
- 與 **Map ID、環區域、環境生成位置/時機** 相關的邏輯。
- MapController 對外 API（例如 `SetMapId`, `SpawnEnvironmentalResource`, `ApplyMoveSpeedTier`）的簽名與行為。
- 僅在「需要與地圖/環綁定」時，才最小幅度讀取或呼叫其他模組（例如只讀 `MapController.CurrentMapId`），不改動其他模組內部邏輯。

## 本 Tab 不負責（由其他 Tab 處理）

- **掉落**：LootManager 的掉落表、RollEnemyLoot、SpawnResource 內部邏輯、ResourcePickup。
- **怪物刷新**：敵人生成、波次、生成點。
- **玩家**：PlayerController、PlayerMovement、輸入、面向滑鼠。
- **血量管理**：HealthSystem、毒氣傷害公式、治療、死亡流程。
- **塔 / 其他戰鬥**：Tower、戰鬥與 Aggro。

若需要與上述模組「接線」（例如 MapController 要讀當前 Map ID 給 LootManager 用），只做最小必要介面，不重寫對方邏輯。

---

## 其他 Tab 與 Map 的接點（僅供參考）

- **掉落**：可呼叫 `MapController.Instance.CurrentMapId` 或 `LootManager.SpawnEnvironmentalResource(mapId, pos)`，不應改 MapZone/環邏輯。
- **怪物刷新**：可依 `MapController.CurrentMapId` 決定生成表或難度，不應改 MapController/MapZone。
- **玩家**：可被 MapController 引用並呼叫 `SetPlayerMoveSpeed`、`SetPlayerControlEnabled`，玩家邏輯由玩家 Tab 維護。
- **血量**：MapController 可驅動毒氣週期並呼叫 `playerHealth.SetToxicGasPeriodsElapsed`，血量與毒氣公式由血量 Tab 維護。

---

---

## 逐環解鎖與戰爭迷霧

- **Map 1**：始終解鎖。
- **Map 2**：塔等級 ≥ 2 時解鎖。
- **Map 3**：塔滿級（等級 3）時解鎖。
- 未解鎖環：顯示迷霧（Fog of War），僅在與 Map 1 邊界處露出一小部分；玩家無法進入（PlayerMapBounds 限制半徑）；進入 trigger 不會切換 Map ID（MapZone 檢查 IsMapUnlocked）。

**場景設定**：在 MapController 上指定 **Tower**、**Map 2 Zone**、**Map 3 Zone**；在 **Map 1** 上掛 **MapRevealMask**（用於迷霧裁切）。玩家 Prefab 已含 **PlayerMapBounds**。

---

*最後更新：依專案分 Tab 分工時建立；新增逐環解鎖與戰爭迷霧。*
