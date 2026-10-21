# Map presentation and ground alignment — 22 September 2026

- Actual selected terrain now appears in both map views. Three region gates remain unchanged.
- Map panels reuse the HUD wood frame, dark inset and restrained gold accents. English and Chinese supported.
- Bottom-left minimap hides during tower upgrade and avoids the journey footer.
- Removed the replacement paving shader. Original terrain colors, drawings and alpha retained; corner reconstruction only.
- 30 building footprints corrected; the archery courtyard/gate is open. Art world placements preserved.
- Collision paths and baked meshes synchronized per instance; 120 complete perimeter paths verified.
- Tower base collision scales with its selected tier.
- Player, goblin and skeleton art anchors aligned to feet; animation IDs preserved; depth sorting uses pivots.

Validation:
- ui-regression.txt: PASS, 288 assertions, latest run 11:23:52, including physical courtyard walk, around-house movement, language/layout and upgrade overlap.
- navigation-routes.txt: PASS, 30 unchanged art placements and 120 complete routes.
- full-playthrough.txt: PASS after navigation rebake, seed 20260922, 08:09 game time, 165 enemies, one final boss, regions 6/7/8 waves all cleared. Clock x4; no resource/damage injection. This run preceded the purely visual actor-pivot correction; the 288-assertion run includes that correction.
- Screenshot layout checked at 2531 x 1262. Other aspect ratios have not been visually certified.
- No player build or remote push performed.

Known limit:
All original map PNGs are 1254 x 1254. Edge reconstruction is not an HD repaint and cannot add missing close-up detail. Enlarged outer-ring scenery still needs higher-density source artwork for a complete clarity upgrade.

The adjacent 2026-09-22 review files predate this revision; use this Alignment folder for the corrected map and collision review.
