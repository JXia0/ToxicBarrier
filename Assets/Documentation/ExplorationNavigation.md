# Exploration and navigation

The three concentric regions and tower upgrade gates remain the main progression:
region 1 is open initially, tower level 2 opens region 2, and level 3 opens region 3.
The win condition remains a level-3 tower plus all three regions' waves and surviving enemies cleared.

## Navigation

- Click the bottom-left minimap, or press M / Tab, to open the full map.
- The minimap uses a 264 x 292 reference-pixel frame with larger player/tower markers and brighter terrain.
- The compact minimap hides while the tower upgrade panel is visible, and moves above the bottom
  journey-status panel if a narrow window would otherwise cause an overlap.
- The expanded map uses the actual three scene sprites with locked regions darkened. Wood frames, dark insets and gold accents match the existing HUD, with separate position, region-status and legend sections.
- Press M / Tab / Escape or click Close to return. Viewing the full map pauses the simulation.
- The minimap fits the unlocked area. The full map shows all three rings with locked regions darkened.
- Ring dimensions come from the scene's actual MapZone collider bounds, including later scale changes.
- White diamond: player; gold square: tower; gold diamond: clear reward; teal diamond: discovered materials;
  blue cross: repair or defensive supplies; gray footprints: buildings in unlocked regions.
- Side supplies are discovered within 8 world units; clear rewards are always marked.
- The player-to-tower line is a bearing, not a calculated walkable path. Navigate around the building footprints.
- Labels follow the existing English / Chinese setting.

## Exploration rewards

- Ordinary side supplies: 10 wood and 6 gel; region 2+ also grants 12 stone and 5 teeth.
- Existing 35 player HP and 18 seconds of gas protection are retained.
- At tower level 3, remaining ordinary supplies become defensive cargo. One parcel can be carried at once.
- Returning within two units of a damaged tower, with clear line of sight, restores up to 15% of its maximum HP.
  Defensive cargo also grants 12 seconds of 35% damage reduction. Time is frozen while paused.
- A full-health tower preserves the carried parcel. Existing dedicated repair parcels retain their 15% repair behavior.
- Region 2's guaranteed clear reward is now 65% through its annulus, with reachable NavMesh placement;
  it still grants the missing upgrade materials. A failed map bake retains the safe fallback to avoid blocking progression.

## Ground rendering

The selected circular artwork, GUIDs and scene scale are preserved. Regions 2 and 3 now use
4096 x 4096 PNGs derived from 5016 x 5016 Real-ESRGAN x4plus masters. The result retains 25% of the
nearest-resized original RGB to avoid excessive smoothing. Alpha comes from the original mask,
not the model. These are enhanced versions of the selected art, not newly authored terrain details.
Region 1 is unchanged. PPU is 4096, retaining the original one-unit sprite and all scene radii.

Desktop import uses BC7, Point sampling, mipmaps for the map UI, and no CPU-readable copy.
The 4K power-of-two output avoids the uncompressed RGBA32 fallback observed with the 5016px masters.
GroundDetailLayer bypasses its old corner-reconstruction shader for HD sources.
The minimap and expanded map reference the same sprite textures, without additional full-size copies.
The opt-in `Temp/ToxicBarrierTerrain.prepare` marker reapplies and validates these import settings.
Current originals and original metadata are backed up in `.utmp/super-resolution/originals`.
Local inference used the official portable tool: https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan .

## Collision alignment

Buildings use source-image ground-contact coordinates, transformed through the actual sprite rectangle,
pivot, pixels-per-unit and renderer flips. Archery uses separate foundation/wall paths, leaving its courtyard
and gate open. House and castle footprints sit at their masonry bases, not at the middle of their facades.
The navigation extrusion uses the same paths, with stable per-instance assets so one edited building cannot
overwrite another building's navigation mesh. Sorting anchors move to corrected feet without moving art.
Tower collision and carving follow the dimensions of the selected tier's foundation.
Player animation pivots now share the authored foot anchor (51, 5 pixels in each 126 x 62 frame),
instead of the empty upper-right portion of the sprite. Goblin and skeleton locomotion/attack frames use
their common ground anchor (75, 54 pixels in 150 x 150 frames). Sprite IDs and PNG pixels are unchanged.
Player/enemy depth sorting uses these pivots. Flying-eye art retains its existing airborne anchor.

Run Tools > Toxic Barrier > Repair Building Ground Alignment (or create Temp/ToxicBarrierNavigation.repair)
only outside Play Mode with a saved scene. This preserves building placements, backs up the scene, syncs the
navigation ground to the current map size, bakes at 0.025-unit voxels and checks four routes per building.

## Verification

`Temp/ToxicBarrierMap.prepare` bakes new Chinese map/HUD glyphs.
The opt-in ReleaseUIPlayModeChecks runner includes ExplorationMapChecks, covering map mesh submission,
unlock gates, resized ring bounds, discovery, localization, pause, cargo delivery, actual damage reduction,
and ward expiry. Screenshots are captured alongside the existing game-flow regression report in `.utmp/ui-checks`.

No enemy combat statistics, authored wave budgets, or tower attack range were changed in this pass.
