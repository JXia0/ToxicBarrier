# R01 art integration — 2026-09-19

Replaced stone, gel, fang and all three tower PNGs in `Assets/Sprites`, preserving their existing Unity GUIDs. Existing world pickups, HUD icons, upgrade previews and the main menu tower image now resolve to the new artwork. Main menu layout is unchanged.

The art-only task's source PNGs are untouched. Imported copies crop excess transparent margins to the visible alpha bounds plus four pixels; no painting, recoloring or resampling was applied. `import-report.json` records source hashes, crop rectangles, importer settings and the local backup directory.

Sprites use Point filtering, no mipmaps and uncompressed textures. Pickups have a visible maximum dimension of 0.60–0.65 world units. All tower tiers use 1000 PPU with custom pivots: the visible base remains at local Y = -0.4, while their visible heights grow from 0.812 to 0.996 to 1.093 units (scene scale is 3).

Wood, maps, characters, other buildings, saved scene tier and gameplay configuration are unchanged. The existing Play Mode check now explicitly initializes its temporary upgrade fixture and expects restart to restore the scene-authored tier. It additionally verifies sprite base alignment through all three upgrades.

Validation: Unity Play Mode smoke checks passed all 78 assertions, including three tower base-alignment checks, tier-specific attacks, upgrade effects, resource costs, language switching, pause, victory, defeat and restart. See play-mode-report.txt and the four runtime screenshots. No standalone player build was regenerated.
