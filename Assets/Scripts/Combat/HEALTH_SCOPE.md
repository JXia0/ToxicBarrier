# 血量管理职责范围（Player / Enemy / Tower）

本目录负责**玩家、敌人、塔**的统一血量与战斗数值管理。

## 归属模块

- **HealthSystem.cs**：唯一血量/攻击/攻速实现
  - 玩家：`isPlayer = true`，受毒气 DoT（可由 MapController 集中驱动周期）
  - 敌人：`isPlayer = false`、`isTower = false`，按 `EnemyType`（Bug/Leader/Boss）套用基础 atk/maxHp
  - 塔：`isTower = true`，使用 Inspector 上的 Combat Stats，不受毒气与敌人类型影响
- **Tower.cs**：依赖 `HealthSystem`，监听 `OnDeath` 做塔被摧毁后的处理（如禁用物体、后续可加特效/音效）

## 主要 API（HealthSystem）

| 方法/属性 | 说明 |
|-----------|------|
| `TakeDamage(float amount, GameObject source)` | 造成伤害，血量为 0 时触发 `OnDeath` |
| `RestoreFullHealth()` | 塔升级时把血量重置为满血 |
| `CurrentHp` / `MaxHp` / `Atk` / `Ats` | 当前/最大血量、攻击力、攻速 |
| `OnHpChanged(float current, float max)` | 血量变化时触发 |
| `OnDeath` | 死亡时触发（敌人掉宝在 Enemy 中监听，塔在 Tower 中监听） |
| `SetStats(float atk, float ats, float maxHp)` | 运行时重设数值（会按比例保留当前血量） |
| `SetEnemyType(EnemyType)` | 仅敌人：运行时设置类型并重算 atk/maxHp |

## 预制体配置

- **玩家**：挂 `HealthSystem`，勾选 `Is Player`，按需设置毒气相关参数。
- **敌人**：挂 `Enemy` + `HealthSystem`，不勾选 Is Player/Is Tower，`Enemy Type` 可由 EnemySpawner 运行时设置。
- **塔**：挂 `Tower` + `HealthSystem`，勾选 **Is Tower**，在 Combat Stats 中设置 atk、ats、maxHp。
- **玩家血条**：`HealthControl` 只接收 `OnHpChanged(current, max)`，按比例修改 `RectTransform.width`，不持有或查找 `HealthSystem`。

## 与其他模块的边界

- **MapController**：引用 `PlayerHealth`、驱动毒气周期（`SetToxicGasPeriodsElapsed`），不直接改其他实体血量。
- **Map 1 安全区**：玩家位于 Map 1 圆形范围内时，`HealthSystem` 跳过毒气持续伤害；毒气周期计时仍继续。
- **Loot/Enemy**：敌人死亡时由 `Enemy.OnDeath` 调用 `LootManager.RollEnemyLoot`，血量模块只负责触发 `OnDeath`。
- **伤害来源**：谁调用 `TakeDamage` 由战斗/投射物等模块负责，本模块只提供接口。

如需扩展（例如护甲、暴击、不同伤害类型），应在 `HealthSystem` 或本目录内扩展，保持单一职责。
