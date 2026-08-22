# Toxic Barrier 2D Sorting Rules

All gameplay sprites use `Sorting Layer + Order in Layer`. Do not move a sprite on the Z axis to change its draw order.

| Sorting Layer | Purpose | Order rule |
| --- | --- | --- |
| `Background` | Distant, non-interactive backdrop | `0` by default |
| `Ground` | Map, walkable ground, and global ToxicFog | ToxicFog `0`; Map1 `30`; Map2 `-20` locked / `20` unlocked; Map3 `-30` locked / `10` unlocked |
| `Environment` | Resources, buildings, static world props | Resources `0`, Tower `10` |
| `Characters` | Enemies, player, combat effects | Enemies `0`, Player `10`, attack/lock lines use their existing positive order |
| `Foreground` | Visual overlays that cover gameplay | `0` by default |
| `UI` | World-space UI and UI-layer reservation | Screen-space overlay canvases use Canvas order; the full-screen game-flow canvas remains `32767` |

## Fixed conventions

- Renderable gameplay objects normally stay at local Z `0`.
- The single global `ToxicFog` Tilemap uses `Ground / Order 0`. Locked maps stay below it; `MapZone` moves each unlocked map above it.
- Use another Sorting Layer for a different visual category.
- Use `Order in Layer` only for small variations inside one category.
- `Default` is reserved for temporary/unclassified content and should not be used by final gameplay renderers.
- Screen-space overlay Canvas ignores Sprite Sorting Layers and remains above scene renderers by design.
- Do not add per-frame sorting scans. Configure renderers in scenes/prefabs; runtime-created fog copies its map renderer's sorting settings.
