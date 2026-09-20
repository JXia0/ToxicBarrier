"""Remove connected neutral checkerboard outside circular map art, without redrawing it.

Usage: python Tools/remove_checkerboard_alpha.py
Writes review copies under .utmp/alpha-cleanup; source assets are not overwritten.
"""
from collections import deque
from pathlib import Path
import json

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.utmp' / 'alpha-cleanup'
OUT.mkdir(parents=True, exist_ok=True)


def clean(source):
    original = Image.open(source).convert('RGBA')
    rgba = np.array(original)
    rgb = rgba[:, :, :3]
    h, w = rgb.shape[:2]
    yy, xx = np.ogrid[:h, :w]
    radius = min(w, h) / 2
    distance2 = (xx - (w - 1) / 2) ** 2 + (yy - (h - 1) / 2) ** 2
    # Interior pale teeth/stone must never enter the background selection.
    protected = distance2 < (radius * .78) ** 2
    neutral = (rgb.max(axis=2).astype(int) - rgb.min(axis=2).astype(int)) <= 32
    candidate = neutral & (rgb.min(axis=2) >= 145) & ~protected
    candidate |= rgba[:, :, 3] == 0
    outside = np.zeros((h, w), dtype=bool)
    queue = deque()
    def add(y, x):
        if candidate[y, x] and not outside[y, x]:
            outside[y, x] = True
            queue.append((y, x))
    for x in range(w):
        add(0, x)
        add(h - 1, x)
    for y in range(h):
        add(y, 0)
        add(y, w - 1)
    while queue:
        y, x = queue.popleft()
        if y: add(y - 1, x)
        if y + 1 < h: add(y + 1, x)
        if x: add(y, x - 1)
        if x + 1 < w: add(y, x + 1)
    # Remove the single-pixel contaminated edge against the baked light matte.
    removed = np.array(Image.fromarray(outside.astype('uint8') * 255).filter(ImageFilter.MaxFilter(3))) > 0
    removed &= ~protected
    ratio = float(removed.mean())
    if not .12 < ratio < .35:
        raise RuntimeError(f'{source.name}: unexpected exterior area {ratio:.3f}; review before processing')
    rgba[removed, 3] = 0
    rgba[removed, :3] = 0
    # Extrude existing edge RGB under transparent pixels to prevent filtering halos.
    # This changes no visible interior pixel and never adds a visible outline.
    known = ~removed
    for _ in range(3):
        totals = np.zeros((h, w, 3), dtype=np.uint32)
        counts = np.zeros((h, w), dtype=np.uint8)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            neighbor = np.roll(known, (dy, dx), axis=(0, 1))
            colors = np.roll(rgba[:, :, :3], (dy, dx), axis=(0, 1))
            if dy == -1: neighbor[-1, :] = False
            if dy == 1: neighbor[0, :] = False
            if dx == -1: neighbor[:, -1] = False
            if dx == 1: neighbor[:, 0] = False
            totals += colors.astype(np.uint32) * neighbor[:, :, None]
            counts += neighbor
        fill = ~known & (counts > 0)
        rgba[fill, :3] = (totals[fill] // counts[fill, None]).astype(np.uint8)
        known |= fill
    result = Image.fromarray(rgba)
    destination = OUT / source.name
    result.save(destination)
    reopened = np.array(Image.open(destination))
    assert reopened.shape == rgba.shape
    assert np.all(reopened[protected] == np.array(original)[protected])
    assert np.all(reopened[~removed, :3] == rgb[~removed])
    assert all(reopened[y, x, 3] == 0 for y, x in ((0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)))
    return result, dict(file=source.name, width=w, height=h, mode='RGBA', transparent_pixels=int(removed.sum()), transparent_fraction=ratio, protected_interior_unchanged=True, retained_rgb_unchanged=True)


def main():
    results, report = [], []
    for number in (1, 2, 3):
        result, entry = clean(ROOT / 'Assets' / 'Sprites' / f'Sprites_map_{number}.png')
        results.append(result)
        report.append(entry)
    sheet = Image.new('RGB', (1440, 1020), '#25252f')
    draw = ImageDraw.Draw(sheet)
    for row, color in enumerate(('#20212d', '#b9cfda')):
        for col, result in enumerate(results):
            preview = Image.new('RGBA', result.size, color)
            preview.alpha_composite(result)
            preview = preview.convert('RGB').resize((470, 470), Image.Resampling.LANCZOS)
            sheet.paste(preview, (col * 480 + 5, row * 510 + 30))
            draw.text((col * 480 + 15, row * 510 + 8), f'Map {col + 1} | {"dark" if row == 0 else "light"} background', fill='white')
    sheet.save(OUT / 'edge-review.jpg', quality=95)
    (OUT / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
