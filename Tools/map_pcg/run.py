#!/usr/bin/python3
"""Battlefield PCG prototype CLI (no Unity, writes only under --out).

  /usr/bin/python3 Tools/map_pcg/run.py seeds 1 2 3 4 5 6 [--config ...] [--out output/map-pcg] [--best-of K] [--no-masks]
  /usr/bin/python3 Tools/map_pcg/run.py current [--out output/map-pcg]
  /usr/bin/python3 Tools/map_pcg/run.py compare 3 5 [--out output/map-pcg]
  /usr/bin/python3 Tools/map_pcg/selftest.py
"""
import argparse
import json
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
ROOT = HERE.parents[1]

from map_pcg import generate as G, constraints as C, export as E, render as R  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

FAR_CAM = (-5.4, 5.35)  # party centre at the 1920x1080 move limit L1 10.75, toward the far-left edge
CURRENT_LAYOUT = ROOT / 'Assets/Game/03.Resources/Resources/Data/Field/field_layout.json'


def load_cfg(path):
    return json.loads(Path(path).read_text())


def build(seed, cfg, best_of=1):
    """Generate `best_of` variants (sub-seeds seed*1000+i) and keep the best valid one."""
    best = None
    for i in range(best_of):
        sub = seed if best_of == 1 else seed * 1000 + i
        L = G.generate(sub, cfg)
        v = C.check(L)
        m = C.metrics(L)
        sc = C.score(m) - 10 * len(v)
        if best is None or sc > best[0]:
            best = (sc, L, v, m)
    return best


def cmd_seeds(a):
    cfg = load_cfg(a.config)
    out = Path(a.out)
    summary = []
    for seed in a.seeds:
        t = time.time()
        sc, L, v, m = build(seed, cfg, a.best_of)
        d = out / ('seed-%d' % seed)
        m['score'] = sc
        m['violations'] = v
        E.write_all(L, d / 'export', metrics=m, masks=not a.no_masks)
        R.pcg_topdown(L, 'seed %d (score %.2f, violations %d)' % (seed, sc, len(v))).save(d / 'topdown.png')
        R.pcg_start_view(L, label='PCG seed %d - start screen 1920x1080 (approx. preview)' % seed).save(d / 'start.png')
        R.pcg_start_view(L, label='PCG seed %d - party at far-left limit (-5.4,5.35)' % seed, cam=FAR_CAM).save(d / 'far-nw.png')
        summary.append({'seed': seed, 'score': sc, 'violations': len(v), 'landmarks': m['landmarks'],
                        'counts': m['counts'], 'seconds': round(time.time() - t, 1)})
        print('seed %d score %.2f violations %d %s %.1fs' % (seed, sc, len(v), m['landmarks'], time.time() - t))
    (out / 'summary.json').write_text(json.dumps(summary, indent=1, ensure_ascii=False) + '\n')


def cmd_current(a):
    cfg = load_cfg(a.config)
    d = Path(a.out) / 'current'
    d.mkdir(parents=True, exist_ok=True)
    R.current_topdown(CURRENT_LAYOUT, cfg, 'CURRENT field_layout.json').save(d / 'topdown.png')
    R.current_start_view(CURRENT_LAYOUT, label='CURRENT - start screen 1920x1080 (same preview renderer)').save(d / 'start.png')
    R.current_start_view(CURRENT_LAYOUT, label='CURRENT - party at far-left limit (-5.4,5.35)', cam=FAR_CAM).save(d / 'far-nw.png')
    print('wrote', d)


def cmd_compare(a):
    out = Path(a.out)
    tiles = [(out / 'current/start.png', 'CURRENT')] + [(out / ('seed-%d/start.png' % s), 'PCG seed %d' % s) for s in a.seeds]
    ims = [Image.open(p).convert('RGB') for p, _ in tiles]
    w, h = ims[0].size
    sheet = Image.new('RGB', (w, h * len(ims)), (0, 0, 0))
    for i, im in enumerate(ims):
        sheet.paste(im, (0, i * h))
    sheet = sheet.resize((w // 2, h * len(ims) // 2), Image.NEAREST)
    d = ImageDraw.Draw(sheet)
    for i, (_, label) in enumerate(tiles):
        d.rectangle([0, i * h // 2, 260, i * h // 2 + 26], fill=(0, 0, 0))
        d.text((8, i * h // 2 + 7), label, fill=(255, 255, 255))
    sheet.save(out / 'compare.png')
    print('wrote', out / 'compare.png')


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--config', default=str(HERE / 'config/deadsurvivor.json'))
    p.add_argument('--out', default=str(ROOT / 'output/map-pcg'))
    sub = p.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('seeds')
    s.add_argument('seeds', type=int, nargs='+')
    s.add_argument('--best-of', type=int, default=1)
    s.add_argument('--no-masks', action='store_true')
    sub.add_parser('current')
    c = sub.add_parser('compare')
    c.add_argument('seeds', type=int, nargs='+')
    a = p.parse_args()
    {'seeds': cmd_seeds, 'current': cmd_current, 'compare': cmd_compare}[a.cmd](a)


if __name__ == '__main__':
    main()
