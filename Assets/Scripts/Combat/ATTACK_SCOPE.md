# 攻击系统职责范围

本目录负责**所有攻击逻辑**：塔攻击敌人、敌人攻击玩家/塔，以及未来玩家攻击扩展。

## 归属模块

- **HealthSystem.cs**：提供 `TakeDamage(amount, source)` 接口，攻击方调用此接口造成伤害
- **TowerAttackBehavior.cs**：塔攻击逻辑，按 ATS 频率对范围内敌人造成 ATK 伤害
- **EnemyAttackBehavior.cs**：敌人攻击逻辑，默认攻塔；角色在仇恨范围内则优先攻击角色

## 攻击流程

| 攻击方 | 目标 | 触发条件 | 伤害来源 |
|--------|------|----------|----------|
| 塔 | 范围内最近敌人 | 每 1/ATS 秒 | Tower.Health.Atk |
| 敌人 | 塔（默认）；角色在仇恨范围内时优先攻击角色 | 进入攻击范围后每 1/ATS 秒 | Enemy.Health.Atk |

## 主要 API

### TowerAttackBehavior

- 挂载在 Tower 同一 GameObject 上
- 自动从 Tower 读取 AttackRange、CurrentStats
- 从 HealthSystem 读取 Atk、Ats（与 Tower 等级同步）
- 目标选择：范围内最近的敌人

### EnemyAttackBehavior

- 挂载在 Enemy 同一 GameObject 上
- 需 Rigidbody2D 用于移动
- 目标选择：默认塔；角色在仇恨范围内则优先角色
- 攻击范围：可配置，默认 1.5 单位
- 仇恨范围（agroRange）：角色在此范围内时优先攻击角色，否则攻塔
- 移动速度：可配置，朝目标移动

## 与其他模块的边界

- **HealthSystem**：只调用 `TakeDamage`，不修改内部逻辑
- **Tower**：读取 AttackRange、CurrentStats、Health
- **MapController**：通过 Instance 获取 PlayerHealth、Player 引用（供 Enemy 寻敌）
- **Enemy**：依赖 HealthSystem 的 Atk、Ats

## 预制体配置

- **塔**：需挂 `Tower` + `HealthSystem` + `TowerAttackBehavior`。TowerAttackBehavior 会自动从 Tower 读取攻击范围与数值。
- **敌人**（Bug / Leader / Boss）：需挂 `Enemy` + `HealthSystem` + `EnemyAttackBehavior` + `Rigidbody2D`。EnemyAttackBehavior 控制寻敌与移动。

## 扩展方向

- 玩家攻击（近战/远程）
- 投射物系统
- 暴击、护甲、伤害类型
