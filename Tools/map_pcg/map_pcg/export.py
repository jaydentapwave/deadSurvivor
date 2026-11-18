"""Outputs in the current stage-5 formats (written under output/, never Assets/).

- field_layout.json   FieldLayoutData schema + additive fields (path.points, banks[].runs, water[].points as outlines)
- ground_path.json    4-connected iso cells along the new path (GroundPathData schema)
- FieldPathMask.png   L8 3456x1728, rect -36..36, 48 texel/m x, 24 texel/m z, row 0 = far (same as Tools/hd2d_field_textures.py)
- FieldWaterMask.png  RGBA white = water opening (same as MainSceneHD2DBackgroundMigration.BakeWaterOpening)
- FieldBankMask.png   RGBA white = raised ledge cells
- FieldZoneMask.png   RGBA new: R worn dirt, G gravel, B grass tone (128 = base), A meadow
"""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

from . import fields as F

TEX_W, TEX_H = 3456, 1728


def sample_ground(L, x, y):
    """Classify floor points. Returns dict of arrays (bool or float). Used by export and preview."""
    g = L.grid
    cfg = L.cfg
    pc = cfg['path']
    pd = F.bilinear(g, L.path_dist_grid, x, y)
    par = F.bilinear(g, L.path_par_grid, x, y)
    s = F.arc_length(L.path)
    hw = np.interp(par, s, L.path_hw)
    jitter = pc['edgeJitter'] * (2 * F.fbm(x, y, pc['edgeBiteScale'], L.seed + 61, 2) - 1)
    path = pd < hw + jitter
    if L.branch is not None:
        bd = F.bilinear(g, L.branch_dist_grid, x, y)
        path |= bd < pc['branchHalfWidth'] + jitter * 0.8
    water = F.bilinear(g, L.water_sdf, x, y) < 0
    bank = F.bilinear(g, L.bank.astype(float), x, y) > 0.5
    upper = F.bilinear(g, L.stream_upper_mask.astype(float), x, y) > 0.5
    fine = 0.05 * (2 * F.hash01(np.floor(x * 24), np.floor(y * 12), L.seed + 71) - 1)
    worn = (F.bilinear(g, L.wear_f, x, y) + fine > 0) & ~path & ~water & ~bank
    gravel = (F.bilinear(g, L.gravel_f, x, y) + fine * 2 > 0) & ~path & ~water & ~bank
    mead_ok = F.bilinear(g, L.meadow.astype(float), x, y) > 0.2
    meadow = (F.bilinear(g, L.mead_f, x, y) + fine > 0) & mead_ok & ~worn & ~path & ~water
    tone = F.bilinear(g, L.tone, x, y)
    return {'path': path & ~water, 'water': water, 'bank': bank, 'upper': upper, 'worn': worn,
            'gravel': gravel, 'meadow': meadow, 'tone': tone}


def texture_coords():
    c = np.arange(TEX_W)
    r = np.arange(TEX_H)
    x = (c + 0.5) / 48.0 - 36.0
    z = 36.0 - (r + 0.5) / 24.0
    return np.meshgrid(x, z)


def write_masks(L, out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    X, Y = texture_coords()
    G = sample_ground(L, X, Y)
    Image.fromarray((G['path'] * 255).astype(np.uint8), 'L').save(out / 'FieldPathMask.png')
    white = lambda m: np.repeat((m * 255).astype(np.uint8)[..., None], 4, axis=2)
    Image.fromarray(white(G['water']), 'RGBA').save(out / 'FieldWaterMask.png')
    Image.fromarray(white(G['bank']), 'RGBA').save(out / 'FieldBankMask.png')
    zone = np.zeros((TEX_H, TEX_W, 4), np.uint8)
    zone[..., 0] = G['worn'] * 255
    zone[..., 1] = G['gravel'] * 255
    zone[..., 2] = np.clip(128 + G['tone'] * 100, 0, 255).astype(np.uint8)
    zone[..., 3] = G['meadow'] * 255
    Image.fromarray(zone, 'RGBA').save(out / 'FieldZoneMask.png')
    return {k: int(np.sum(G[k])) for k in ('path', 'water', 'bank', 'worn', 'gravel', 'meadow')}


def bank_runs(L):
    """Bank cells as row runs in BankMeshMath cell units (cell = 0.125 m)."""
    g = L.grid
    step = L.cfg['banks']['cell']
    assert abs(1.0 / g.res - step) < 1e-9, 'grid resolution must equal bank cell'
    off = int(round(g.half / step))
    runs = []
    for row in range(g.n):
        cols = np.nonzero(L.bank[row])[0]
        if not len(cols):
            continue
        iy = off - row - 1
        start = cols[0]
        prev = cols[0]
        for c in list(cols[1:]) + [None]:
            if c is None or c != prev + 1:
                runs.append({'y': int(iy), 'xMin': int(start - off), 'xMax': int(prev - off + 1)})
                if c is not None:
                    start = c
            if c is not None:
                prev = c
    return runs


def _outline(L, sdf_fn_mask):
    """Coarse outline polygon (for the water quad bounds / sketch); counter-clockwise points."""
    pts = []
    c = L.pond_center
    for a in np.arange(0, 2 * math.pi, math.pi / 24):
        d = np.array([math.cos(a), math.sin(a)])
        last = None
        for rr in np.arange(0.2, L.pond_R * 2.5, 0.05):
            q = c + d * rr
            if F.bilinear(L.grid, L.pond_sdf, q[0], q[1]) >= 0:
                last = q
                break
        if last is not None:
            pts.append(last)
    return pts


def _r(v):
    return round(float(v), 3)


def iso_cells(poly):
    """Floor polyline -> 4-connected iso cells (cx = x + y, cy = y - x; IsoCellToFloor inverse)."""
    dense, _ = F.resample(poly, 0.1)
    cells = []
    for x, y in dense:
        c = (int(round(x + y)), int(round(y - x)))
        if cells and c == cells[-1]:
            continue
        if cells:
            px, py = cells[-1]
            # Fill diagonal steps to keep 4-connectivity.
            while abs(c[0] - px) + abs(c[1] - py) > 1:
                if abs(c[0] - px) >= abs(c[1] - py):
                    px += 1 if c[0] > px else -1
                else:
                    py += 1 if c[1] > py else -1
                if (px, py) not in cells[-3:]:
                    cells.append((px, py))
        if c not in cells[-3:]:
            cells.append(c)
    # Keep only cells inside the field (|x| + |y| <= 32 in floor metres).
    out = []
    for cx, cy in cells:
        fx, fy = (cx - cy) / 2.0, (cx + cy) / 2.0
        if abs(fx) + abs(fy) <= 32.0 and (not out or out[-1] != (cx, cy)):
            out.append((cx, cy))
    return out


def layout_json(L):
    cfg = L.cfg
    pc = cfg['path']
    banks = [{'name': 'pcg-bank', 'seed': int(L.seed), 'height': cfg['banks']['height'], 'cell': cfg['banks']['cell'],
              'rectangles': [], 'runs': bank_runs(L)}]
    water = [{'name': 'pond', 'mesh': '', 'points': [{'x': _r(p[0]), 'y': _r(p[1])} for p in _outline(L, None)],
              'width': 0.0, 'height': -0.015}]
    if L.stream is not None:
        sp, _ = F.resample(L.stream, 0.5)
        if L.falls is not None:
            s = F.arc_length(sp)
            up = sp[s <= L.s_fall]
            lo = sp[s >= L.s_fall]
            water.append({'name': 'stream-upper', 'mesh': '', 'points': [{'x': _r(p[0]), 'y': _r(p[1])} for p in up],
                          'width': _r(2 * L.stream_hw), 'height': _r(cfg['banks']['height'] - 0.03)})
            water.append({'name': 'stream', 'mesh': '', 'points': [{'x': _r(p[0]), 'y': _r(p[1])} for p in lo],
                          'width': _r(2 * L.stream_hw), 'height': -0.03})
            a, b = L.falls
            water.append({'name': 'waterfall', 'mesh': '', 'points': [{'x': _r(a[0]), 'y': _r(a[1])}, {'x': _r(b[0]), 'y': _r(b[1])}],
                          'width': _r(2 * L.stream_hw), 'height': cfg['banks']['height']})
        else:
            water.append({'name': 'stream', 'mesh': '', 'points': [{'x': _r(p[0]), 'y': _r(p[1])} for p in sp],
                          'width': _r(2 * L.stream_hw), 'height': -0.03})
    keys = ('kind', 'group', 'sprite', 'prop', 'x', 'y', 'height', 'yaw', 'scale', 'isFlipped', 'isCastingShadow', 'isRoadside')
    placements = []
    for it in L.placements:
        d = {k: it[k] for k in keys}
        for k in ('x', 'y', 'height', 'yaw', 'scale'):
            d[k] = _r(d[k])
        placements.append(d)
    pp, _ = F.resample(L.path, 0.5)
    out = {
        'seed': int(L.seed),
        'groundHalfDiagonal': cfg['bounds']['groundL1'],
        'pathClearance': cfg['placement']['pathClearanceTall'],
        'roadsideClearance': 0.9,
        'startClearance': cfg['gameplay']['clearAnyL1'],
        'yellowClearance': cfg['gameplay']['yellowFlowerClearL1'],
        'path': {'halfWidth': pc['halfWidth'], 'widthVariation': pc['widthVariation'], 'edgeVariation': pc['edgeJitter'],
                 'points': [{'x': _r(p[0]), 'y': _r(p[1])} for p in pp]},
        'banks': banks,
        'water': water,
        'placements': placements,
        'generator': {'tool': 'Tools/map_pcg', 'config': 'Tools/map_pcg/config/deadsurvivor.json'},
    }
    if L.branch is not None:
        bp, _ = F.resample(L.branch, 0.5)
        out['path']['branch'] = [{'x': _r(p[0]), 'y': _r(p[1])} for p in bp]
    return out


def write_all(L, out_dir, metrics=None, masks=True):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    data = layout_json(L)
    (out / 'field_layout.json').write_text(json.dumps(data, indent=1, ensure_ascii=False) + '\n')
    cells = iso_cells(L.path)
    (out / 'ground_path.json').write_text(json.dumps({'cells': [{'x': c[0], 'y': c[1]} for c in cells]}, indent=1) + '\n')
    info = {}
    if masks:
        info['maskTexels'] = write_masks(L, out)
    if metrics is not None:
        (out / 'metrics.json').write_text(json.dumps(metrics, indent=1, ensure_ascii=False) + '\n')
    return data, cells, info
