# Terrain clarity and minimap review — 22 September 2026

## Delivered

- Region 2 and 3 sprite PNGs: 1254² to 4096², under the existing asset GUIDs.
- PPU 4096 retains the original one-unit local sprite and existing world map/collider dimensions.
- BC7 desktop textures, mipmaps, Point filtering, no CPU-readable duplicate.
- Original alpha geometry retained. Region 1, scene placements and tower upgrade gates unchanged.
- HD art bypasses the previous runtime edge-reconstruction shader.
- Minimap frame enlarged from 196×224 to 264×292 reference pixels. Reduced internal margins make
  the actual terrain disc about 45% wider. Player/tower markers, heading and boundaries are clearer.
- Existing bottom-left placement, footer avoidance, upgrade-panel hiding, M/Tab expansion,
  pause behavior and Chinese/English support retained.

## Art processing

Official Real-ESRGAN ncnn Vulkan portable release (20220424), `realesrgan-x4plus`, scale 4, tile 192.
Source: https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan

The 5016² master blends 75% SR output with 25% nearest-resized original RGB. Its alpha is the original
mask enlarged by nearest sampling. The game PNG is a Lanczos reduction of this master to 4096²,
which allows BC7 with mipmaps. The 5016² trial was observed loading as uncompressed RGBA32.
The final masks, GUIDs and dimensions were validated by script; see asset-validation.json.

This improves edge clarity but cannot recover true authored detail absent from the original artwork.
Large scenery painted into the terrain remains large; these assets are not a tile-by-tile redraw.

Original PNGs and metadata: `.utmp/super-resolution/originals/`.
5016² masters and reproducible preparation/validation scripts: `.utmp/super-resolution/`.
Detail-Comparison images compare original crops with the 5016² master, not a Unity screenshot.
The region gameplay images show the final imported 4K result.

## Verification

- `terrain-import.txt`: actual Unity dimensions, PPU and compression format.
- `ui-regression.txt`: final Play Mode regression report, including unlocks, bilingual labels,
  map geometry, panel overlap, source resolution, compression and game flow.
- `Region-2.png`, `Region-3.png`: final game view with enlarged minimap.
- `Map-Chinese.png`, `Map-English.png`: full map using all three selected terrain images.
- `Upgrade-No-Overlap.png`: upgrade panel with minimap hidden.

No build or packaging performed.
