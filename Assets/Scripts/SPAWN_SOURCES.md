# 生成來源 (Spawn Sources) 與重疊檢查

## 一、所有 Spawner 來源

| # | 來源 | 腳本 | 觸發時機 | 資源類型 | 生成位置 |
|---|------|------|----------|----------|----------|
| 1 | **EnvironmentalResourceSpawner** | `Map/EnvironmentalResourceSpawner.cs` | `Start()` 自動呼叫 `StartSpawning()` | Wood, Stone | MapZone 環內 或 手動設定的矩形 |
| 2 | **EnemySpawner** | `Enemies/EnemySpawner.cs` | `Start()` 自動呼叫 `StartSpawning()` | 怪物 (Bug/Leader/Boss) | MapZone 環內 或 手動設定的矩形 |
| 3 | **Enemy 死亡** | `Enemies/Enemy.cs` → `LootManager.RollEnemyLoot()` | HealthSystem.OnDeath | Gel, Teeth | 敵人 transform.position |
| 4 | **MapController** | `Core/MapController.cs` | 由外部呼叫 `SpawnEnvironmentalResource(position)` | Wood, Stone | 呼叫時傳入的 position |

### 詳細說明

#### 1. EnvironmentalResourceSpawner
- **數量**: 場景中可有多個（例如三環各一個）
- **位置邏輯**:
  - 有 MapZone → 在環內 (innerRadius ~ outerRadius)
  - 無 MapZone → 在矩形 (spawnAreaMin ~ spawnAreaMax，預設 -10~10)
- **啟動**: `Start()` 時自動執行，或手動呼叫 `StartSpawning()` / `SpawnAll()`

#### 2. EnemySpawner（怪物刷新）
- **數量**: 場景中可有多個（例如三環各一個）
- **位置邏輯**: 與 EnvironmentalResourceSpawner 相同
  - 有 MapZone → 在環內 (innerRadius ~ outerRadius)
  - 無 MapZone → 在矩形 (spawnAreaMin ~ spawnAreaMax)
- **敵人類型**: 三種 prefab（Bug / Leader / Boss）各一，依權重隨機選一種生成；未指定的類型不參與權重
- **啟動**: `Start()` 時自動執行，或手動呼叫 `StartSpawning()` / `StopSpawning()`
- **可選**: 連續波次 (continuousWaves)，每波 spawnCount 隻，間隔後再刷下一波

#### 3. Enemy 死亡 (RollEnemyLoot)
- **觸發**: 每個 Enemy 死亡時
- **資源**: Gel、Teeth（依敵人類型機率）
- **位置**: 敵人死亡位置 + 隨機偏移 (dropSpawnRadius)

#### 4. MapController.SpawnEnvironmentalResource
- **觸發**: 需由其他腳本或 Unity Event 主動呼叫
- **位置**: 傳入的 `Vector2 position`
- **Map ID**: 使用 MapController.CurrentMapId（由玩家所在 MapZone 更新）

---

## 二、重疊檢查 (Overlap Analysis)

### 潛在重疊狀況

| 狀況 | 是否重疊 | 說明 |
|------|----------|------|
| **多個 EnvironmentalResourceSpawner 都綁 MapZone** | 否 | 若三環的 inner/outer 正確（Map1: 0~R1, Map2: R1~R2, Map3: R2~R3），各自在獨立環內生成 |
| **EnvironmentalResourceSpawner 未綁 MapZone** | 是 | 使用預設矩形 (-10, 10)，可能涵蓋整個地圖，與環內生成區域 overlapping |
| **MapController.SpawnEnvironmentalResource 被呼叫** | 可能 | 若呼叫時傳入的位置落在環內，會在相同區域額外產生 Wood/Stone |
| **EnemySpawner vs EnvironmentalResourceSpawner** | 可同環 | 若同一 MapZone 上兩者都掛，怪物與 Wood/Stone 會在同一環內不同點生成，可設 minDistanceBetween 控制密度 |
| **Enemy 死亡 vs 環境資源** | 否 | Gel/Teeth 與 Wood/Stone 不同，且敵人位置與環/矩形無直接關聯 |

### 建議避免重疊的作法

1. **EnvironmentalResourceSpawner**
   - 三環場景：每個環的 GameObject 都掛一個 EnvironmentalResourceSpawner，且都**必須**指定對應的 MapZone
   - 不要保留「沒有 MapZone、用矩形」的 EnvironmentalResourceSpawner，除非確定不會與環重疊

2. **MapController.SpawnEnvironmentalResource**
   - 若沒有腳本或 Event 會呼叫它，可忽略
   - 若有使用，應只在「不會由 EnvironmentalResourceSpawner 生成」的區域呼叫（例如特殊事件、關卡生成）

3. **環的半徑設定**
   - Map 1: innerRadius = 0, outerRadius = R1
   - Map 2: innerRadius = R1, outerRadius = R2
   - Map 3: innerRadius = R2, outerRadius = R3
   - 確保 R1 < R2 < R3，避免環與環重疊

---

## 三、呼叫關係圖

```
EnvironmentalResourceSpawner.Start()
    └─> StartSpawning() / SpawnOverTimeCoroutine()
        └─> SpawnOneResource()
            └─> LootManager.SpawnResource() 或 SpawnEnvironmentalResource()

EnemySpawner.Start()
    └─> StartSpawning() / SpawnWavesCoroutine()
        └─> SpawnOneEnemy(position)
            └─> RollPrefabByWeight() → Instantiate(bugPrefab | leaderPrefab | bossPrefab)

Enemy.OnDeath()
    └─> LootManager.RollEnemyLoot()
        └─> SpawnResource(Gel) / SpawnResource(Teeth)

MapController.SpawnEnvironmentalResource(position)  [外部呼叫]
    └─> LootManager.SpawnEnvironmentalResource(currentMapId, position)
        └─> SpawnResource(Wood) / SpawnResource(Stone)
```

---

## 四、檢查清單

- [ ] 場景中每個 EnvironmentalResourceSpawner 都綁定了 MapZone（三環場景）
- [ ] 沒有「無 MapZone」且矩形涵蓋地圖的 EnvironmentalResourceSpawner
- [ ] 三個 MapZone 的 innerRadius / outerRadius 不重疊
- [ ] 若有腳本呼叫 MapController.SpawnEnvironmentalResource，確認不會與 EnvironmentalResourceSpawner 重複生成
- [ ] EnemySpawner 至少指定一個敵人預制體（Bug / Leader / Boss），各預制體需帶 Enemy + HealthSystem 且對應 Enemy Type
- [ ] 怪物刷新可依需求綁 MapZone 或使用矩形，與環境資源可共用同一環
