"""043 evidence bundle: comparison sheets, pixel diffs and a SHA-256 list built ONLY from the
current captures and current sources (043-V8). Previous bundles are kept under history/.

Usage (project root): /usr/bin/python3 Tools/Art/evidence_043.py <bundle-name>
Diff threshold "gt12" means sum over RGB of |a-b| > 12 (same rule the reviewer reproduced).
"""
import hashlib
import json
import re
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/lowpoly-shader-043/r001'
CAP = OUT / 'captures'
BUNDLE = sys.argv[1]
SOURCES = [
    'Assets/DeadSurvivor/Art/LowPoly043/Shaders/DSLowPolyPalette.shader',
    'Assets/DeadSurvivor/Art/LowPoly043/Palette/palette-r001.png',
    'Assets/DeadSurvivor/Art/LowPoly043/Palette/palette-r001.json',
    *[f'Assets/DeadSurvivor/Art/LowPoly043/Materials/LowPoly043_{p}.mat' for p in ('Ground', 'Soil', 'Path', 'Stone', 'Foliage')],
    'Assets/DeadSurvivor/Art/LowPoly043/Meshes/Meshes043.asset',
    'Assets/DeadSurvivor/Art/LowPoly043/Props/stone/model.fbx', 'Assets/DeadSurvivor/Art/LowPoly043/Props/shrub/model.fbx',
    'Assets/DeadSurvivor/Art/LowPoly043/Props/stone/stone.prefab', 'Assets/DeadSurvivor/Art/LowPoly043/Props/shrub/shrub.prefab',
    'Assets/DeadSurvivor/Scenes/Map043_LowPolyShader_r001/Map043_LowPolyShader.unity',
    'Tools/Art/build_lowpoly_043.cs', 'Tools/Art/palette_extract_043.py', 'Tools/Art/repaint_tripo_043.py',
    'Tools/Art/preview_043.py', 'Tools/Art/evidence_043.py',
]


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def rgb(p):
    return np.asarray(Image.open(p).convert('RGB')).astype(int)


def diff(a, b):
    d = np.abs(rgb(a) - rgb(b)).sum(2)
    return d


W = 760


def load(p, asp=None):
    im = Image.open(p).convert('RGB')
    if asp:
        w, h = im.size
        th = int(w / asp)
        if th < h:
            im = im.crop((0, (h - th) // 2, w, (h - th) // 2 + th))
    return im.resize((W, int(im.size[1] * W / im.size[0])), Image.Resampling.BICUBIC)


def sheet(cells, out):
    ims = [(t, load(p, a)) for t, p, a in cells]
    H = max(i.size[1] for _, i in ims)
    rows = (len(ims) + 1) // 2
    s = Image.new('RGB', (2 * W + 30, rows * (H + 34) + 10), (22, 22, 22))
    d = ImageDraw.Draw(s)
    for i, (t, im) in enumerate(ims):
        x, y = 10 + (i % 2) * (W + 10), 10 + (i // 2) * (H + 34)
        d.text((x, y), t, fill=(240, 240, 240))
        s.paste(im, (x, y + 18))
    s.save(out)
    return out


def main():
    hist = OUT / 'history'
    hist.mkdir(exist_ok=True)
    for old in ['evidence.sha256', 'compare-angle-stages.png', 'compare-night.png', 'compare-shadow.png',
                'compare-shadow-crop.png', 'compare-shadowpair.png']:
        if (OUT / old).exists():
            target = hist / f'pre-{BUNDLE}-{old}'
            if target.exists():
                raise SystemExit(f'HISTORY_EXISTS:{target}')
            shutil.move(str(OUT / old), str(target))
    bdir = OUT / BUNDLE
    if bdir.exists():
        raise SystemExit(f'BUNDLE_EXISTS:{bdir}')
    bdir.mkdir()
    asp = 1560 / 960
    keyday, keynight = OUT / 'inputs/day.png', ROOT / 'output/keyart-041/r002/result/night.png'
    sheets = [
        sheet([('key art day.png (target)', keyday, asp), ('038 original (same camera)', CAP / '038-base-angle.png', None),
               ('043 stage1: palette + light', CAP / '043-stage1-angle.png', None), ('043 stage2: + height gradient + top moss', CAP / '043-stage2-angle.png', None),
               ('043 stage3 day: + cloud shadow + ground noise + shadow tint', CAP / '043-stage3-day-angle.png', None), ('043 stage3 night: + grade + shadow tint', CAP / '043-stage3-night-angle.png', None)],
              bdir / 'compare-angle-stages.png'),
        sheet([('key art night.png (041 r002, mood only)', keynight, asp), ('043 stage3 night', CAP / '043-stage3-night-angle.png', None),
               ('043 night, grade/tint off', CAP / '043-stage3-night-notint-angle.png', None), ('043 overview day', CAP / '043-stage3-day-overview.png', asp)],
              bdir / 'compare-night.png'),
        sheet([('038 shadows on (distance 25)', CAP / '038-shadowpair-on.png', None), ('038 shadows off', CAP / '038-shadowpair-off.png', None),
               ('043 shadows on (distance 25)', CAP / '043-shadowpair-on.png', None), ('043 shadows off', CAP / '043-shadowpair-off.png', None)],
              bdir / 'compare-shadowpair.png'),
        sheet([('043 night + temporary point light', CAP / '043-pointlight-on.png', None), ('043 night, point light off', CAP / '043-pointlight-off.png', None)],
              bdir / 'compare-pointlight.png'),
    ]
    metrics = {}
    for k in ('038', '043'):
        d = diff(CAP / f'{k}-shadowpair-on.png', CAP / f'{k}-shadowpair-off.png')
        darker = rgb(CAP / f'{k}-shadowpair-on.png').sum(2) < rgb(CAP / f'{k}-shadowpair-off.png').sum(2)
        metrics[f'shadowpair_{k}'] = {'any': int((d > 0).sum()), 'gt12': int((d > 12).sum()),
                                      'darker_gt12': int(((d > 12) & darker).sum()), 'brighter_gt12': int(((d > 12) & ~darker).sum())}
    d = diff(CAP / '043-pointlight-on.png', CAP / '043-pointlight-off.png')
    ys, xs = np.nonzero(d > 12)
    brighter = rgb(CAP / '043-pointlight-on.png').sum(2) > rgb(CAP / '043-pointlight-off.png').sum(2)
    metrics['pointlight_043'] = {'gt12': int((d > 12).sum()), 'brighter_gt12': int(((d > 12) & brighter).sum()),
                                 'bbox': [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())] if len(ys) else None}
    wp = json.loads((CAP / '043-windproof.json').read_text())
    d = diff(CAP / '043-windproof-w000.png', CAP / '043-windproof-w020.png')
    H, Wd = d.shape
    mask = np.zeros_like(d, bool)
    for r in wp['shots']['shrubRectsTopLeftOrigin']:
        x0, x1 = int(np.floor(r['x0'])), int(np.ceil(r['x1']))
        y0, y1 = int(np.floor(r['y0'])), int(np.ceil(r['y1']))
        mask[max(0, y0):min(H, y1 + 1), max(0, x0):min(Wd, x1 + 1)] = True
    metrics['wind'] = {'any': int((d > 0).sum()), 'any_outside_rects': int((d[~mask] > 0).sum()), 'gt12': int((d > 12).sum()),
                       'gt12_outside_rects': int((d[~mask] > 12).sum())}
    (bdir / 'metrics.json').write_text(json.dumps(metrics, indent=1))
    # 043-V9: only files written by the current capture run belong to the bundle. Any other file
    # in captures/ (e.g. an older ad-hoc analysis) stops the bundle instead of being included.
    current = sorted(p for p in CAP.iterdir() if p.is_file() and re.fullmatch(r'(03[89]|04[0-9])-[a-z0-9-]+\.(png|json)|call-[A-Za-z0-9]+\.json', p.name))
    stray = sorted(p.name for p in CAP.iterdir() if p.is_file() and p not in current)
    if stray:
        raise SystemExit('STRAY_FILES_IN_CAPTURES:' + ','.join(stray))
    files = [ROOT / s for s in SOURCES] + current + sheets + [bdir / 'metrics.json']
    missing = [str(f) for f in files if not Path(f).exists()]
    if missing:
        raise SystemExit('MISSING:' + ','.join(missing))
    lines = [f'{sha(f)}  {Path(f).relative_to(ROOT)}' for f in files]
    (bdir / 'evidence.sha256').write_text('\n'.join(lines) + '\n')
    print(json.dumps({'bundle': str(bdir.relative_to(ROOT)), 'files': len(lines), 'shader': sha(ROOT / SOURCES[0])[:12], 'metrics': metrics}))


if __name__ == '__main__':
    main()
