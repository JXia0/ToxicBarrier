# 玩家管理範圍說明

**此 Tab 負責：所有玩家（Player）相關邏輯。**

地圖、掉落、怪物、血量等由其他 Tab 負責；本 Tab 專注於玩家控制、輸入與移動。

---

## 本 Tab 負責的腳本與職責

| 腳本 | 路徑 | 職責 |
|------|------|------|
| **PlayerController** | `Player/PlayerController.cs` | 頂視角控制：WASD 移動（Rigidbody2D）、面向滑鼠、數字鍵 1–5 裝備/材料、可被外部設定移動速度與啟用/停用控制 |
| **PlayerMovement** | `Player/PlayerMovement.cs` | 舊版/備用移動：WASD + 精靈左右翻轉，與 MapScale 無直接綁定 |
| **PlayerInventory** | `Player/PlayerInventory.cs` | 玩家背包：訂閱 ResourcePickup.OnResourceCollected，按類型累計 Gel/Teeth/Wood/Stone，可選單類上限、TryConsume、OnResourceCountChanged |
| **InventoryPanelUI** | `Player/InventoryPanelUI.cs` | 右上角背包 UI：綁定 PlayerInventory，顯示四種資源數量；可自動創建 Canvas/面板或使用 Inspector 指定已有 UI |

## 本 Tab 可動的邊界

- 修改、擴充上述腳本。
- **輸入**：水平/垂直軸、死區、裝備槽鍵位。
- **移動**：速度、剛體速度、面向（面向滑鼠或翻轉精靈）。
- **對外 API**：`SetMoveSpeed(float)`、`GetMoveSpeed()`、`OnInventorySlotPressed`；配合 MapController 的 `SetPlayerControlEnabled`、`SetPlayerMoveSpeed` 使用。
- 僅在「需要與玩家綁定」時，最小幅度讀取或呼叫其他模組（例如被 MapZone/ResourcePickup 用 Tag 或 `GetComponent<PlayerController>()` 偵測），不改動地圖/掉落/血量內部邏輯。

## 本 Tab 不負責（由其他 Tab 處理）

- **地圖**：MapController、MapZone、MapScale、環境資源生成。
- **掉落**：LootManager、ResourcePickup、掉落表。
- **怪物**：Enemy、Tower、刷新與波次。
- **血量管理**：HealthSystem、毒氣傷害、治療、死亡流程；玩家物件上可掛 HealthSystem，但公式與流程由血量 Tab 維護。

若其他模組需要「接線」玩家（例如 MapController 要設定玩家速度或開關控制），只暴露必要介面，不重寫對方邏輯。

---

## 其他 Tab 與玩家的接點（僅供參考）

- **地圖**：MapController 持有 `PlayerController`、`HealthSystem` 引用，可呼叫 `SetPlayerMoveSpeed(speed)`、`SetPlayerControlEnabled(enabled)`；MapScale 透過 MapController 對玩家套用 Tier 速度。
- **地圖區域**：MapZone 以 Tag `Player` 或 `GetComponent<PlayerController>()` 偵測玩家進入，更新 MapController 的當前 Map ID。
- **拾取**：ResourcePickup 以 Tag `Player` 或 `GetComponent<PlayerController>()` 偵測碰撞。

---

*最後更新：建立玩家管理 Tab 時新增。*
