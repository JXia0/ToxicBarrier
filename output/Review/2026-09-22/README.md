# Exploration and map review — 2026-09-22

Implemented:
- Three existing circular regions and tower-level unlock gates retained.
- Compact bottom-left minimap with discovered supplies, player position, tower bearing and buildings.
- Minimap hides during tower upgrades and automatically avoids the bottom journey-status panel.
- M / Tab opens a paused, opaque full map; M / Tab / Escape closes it.
- Full map separates current position, region status and the legend; English and Chinese labels verified.
- Regions 2 and 3 use tiled stone paving at a stable world scale, replacing the enlarged ground drawing in play.
  Original sprites and actual buildings/navigation remain in the project.
- Increased finite side-route material rewards; region 2 clear reward moved into region 2.
- Remaining ordinary supplies at maximum tower level become defensive repair parcels.

Validation:
- Unity 2022.3.62f3 runtime/editor compilation succeeded.
- 282 Play Mode regression assertions passed, including map geometry rendering, localization layout,
  upgrading without minimap overlap, pause/resume, finite cargo, damage reduction and expiry.
- Visually reviewed the current Game view captures (2531×1262), including both full-map languages,
  the upgrade panel with the minimap hidden, and the minimap beside the journey-status panel.
- Full authored-wave run, seed 20260922: victory at 07:58, 165 enemies defeated, tower level 3;
  regions 1/2/3 completed 6/7/8 waves, with no surviving enemies. Walked to both physical clear rewards
  and back; exactly one final boss defeated. Clock accelerated ×4; no injected resources or damage.
- No player build, commit or push performed.

Evidence: navigation-regression.txt, full-playthrough.txt, Map-English.png, Map-Chinese.png,
Upgrade-No-Overlap.png, Region-2.png and Region-3.png.

Limits: navigation is a schematic with tower bearing, not turn-by-turn pathfinding. Terrain is a new tiled
floor treatment, not higher-resolution versions of the old map paintings. Enemy combat statistics were
not retuned in this pass. Other display sizes still need visual review; the current viewport is verified.
