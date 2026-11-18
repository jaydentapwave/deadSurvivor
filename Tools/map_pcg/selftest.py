#!/usr/bin/python3
"""Self-test: every check must pass on generated maps AND fail on a planted counterexample.

  /usr/bin/python3 Tools/map_pcg/selftest.py            (exit 0 = all PASS)
"""
import copy
import hashlib
import json
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import numpy as np  # noqa: E402
from map_pcg import generate as G, constraints as C, export as E, fields as F  # noqa: E402

CFG = json.loads((HERE / 'config/deadsurvivor.json').read_text())
results = []


def case(name, ok, detail=''):
    results.append((name, ok, detail))
    print('%s  %s %s' % ('PASS' if ok else 'FAIL', name, detail))


def item(name, x, y, **kw):
    cat = CFG['catalog'][name]
    d = {'kind': cat['kind'], 'group': kw.pop('group', cat.get('group', name)), 'sprite': name if cat['kind'] == 'card' else '',
         'prop': name if cat['kind'] == 'prop' else '', 'x': x, 'y': y, 'height': 0.0, 'yaw': 0.0, 'scale': 1.0,
         'isFlipped': False, 'isCastingShadow': False, 'isRoadside': False, 'tall': bool(cat.get('tall')), 'camp': False}
    d.update(kw)
    return d


def clone(L):
    M = copy.copy(L)
    M.placements = [dict(it) for it in L.placements]
    return M


def main():
    seeds = [1, 2, 3, 4, 5, 6]
    base = {}
    # 1. Generated maps pass every check.
    for s in seeds:
        L = G.generate(s, CFG)
        v = C.check(L)
        base[s] = L
        case('seed %d all checks' % s, not v, '; '.join(v[:3]))

    L = base[3]
    # 2. Determinism: same seed -> identical layout json and masks; different seed differs.
    with tempfile.TemporaryDirectory() as tmp:
        hashes = []
        for k in range(2):
            Lk = G.generate(3, CFG)
            d = Path(tmp) / str(k)
            E.write_all(Lk, d, masks=True)
            h = hashlib.sha256()
            for f in sorted(d.iterdir()):
                h.update(f.read_bytes())
            hashes.append(h.hexdigest())
        d = Path(tmp) / 'other'
        E.write_all(G.generate(4, CFG), d, masks=False)
        other = hashlib.sha256((d / 'field_layout.json').read_bytes()).hexdigest()
        same = hashlib.sha256((Path(tmp) / '0' / 'field_layout.json').read_bytes()).hexdigest()
        case('determinism same seed -> same bytes (json+5 masks)', hashes[0] == hashes[1], hashes[0][:12])
        case('determinism different seed -> different json', other != same)

    # 3. Path continuity: cut the path mask across its width -> must fail.
    M = clone(L)
    M.path_mask = L.path_mask.copy()
    mid = L.path[len(L.path) // 2]
    cut = np.hypot(L.grid.x - mid[0], L.grid.y - mid[1]) < 2.5
    M.path_mask[cut] = False
    v = C.check_path_continuity(M)
    case('counterexample: cut path detected', any('not continuous' in x for x in v), v[:1])
    # Sharp V corner (the current ground_path kind of corner) -> must fail.
    M = clone(L)
    M.path = np.vstack([np.linspace([-20, -10], [0, 0], 80), np.linspace([0, 0], [20, -10], 80)[1:]])
    v = C.check_path_continuity(M)
    case('counterexample: V corner detected', any('sharp corner' in x for x in v), v[-1:])
    # The committed ground_path.json itself (straight runs + right-angle turns).
    root = HERE.parents[1]
    cells = json.loads((root / 'Assets/Game/03.Resources/Resources/Data/Field/ground_path.json').read_text())['cells']
    poly = np.array([[(c['x'] - c['y']) / 2.0, (c['x'] + c['y']) / 2.0] for c in cells])
    turn = F.max_turn_per_metre(poly, 1.0)
    case('counterexample: current ground_path.json fails the corner rule', turn > CFG['path']['maxTurnDegPerMetre'], '%.0f deg/m' % turn)

    # 4. Clear centre: tall tree at L1 4, any card at L1 2, yellow flower at L1 5 -> each must fail.
    for name, x, y, key in (('tree_02', 2.0, 2.0, 'tall'), ('rock_01', 1.0, 1.0, 'center'), ('flower_02', 2.5, 2.5, 'yellow')):
        M = clone(L)
        M.placements.append(item(name, x, y))
        v = C.check_clear_center(M)
        case('counterexample: %s at (%.1f,%.1f) in centre' % (name, x, y), any(key in x_ or 'center' in x_ for x_ in v) and len(v) > 0, v[:1])
    # Too many tall props in the move band.
    M = clone(L)
    for k in range(CFG['gameplay']['maxTallInMoveBand'] + 1):
        M.placements.append(item('boulder_01', 7.0 + 0.01 * k, 2.0))
    v = C.check_clear_center(M)
    case('counterexample: move band over-filled', any('move band' in x for x in v), v[-1:])

    # 5. Min spacing: duplicate a tree on top of an existing tree.
    M = clone(L)
    t = next(it for it in M.placements if it['group'] == 'tree')
    M.placements.append(dict(t, x=t['x'] + 0.3))
    v = C.check_spacing(M)
    case('counterexample: overlapping trees', len(v) > 0, v[:1])

    # 6. Bounds: card in the invisible corner, card outside the field, rim tree on the near side.
    for name, x, y, grp in (('bush_01', 26.0, 2.0, 'bush'), ('rock_02', 20.0, 13.0, 'stone'), ('tree_02', -20.0, -13.5, 'rim')):
        M = clone(L)
        M.placements.append(item(name, x, y, group=grp))
        v = C.check_bounds(M)
        case('counterexample: %s %s at (%.1f,%.1f) out of bounds' % (grp, name, x, y), len(v) > 0, v[:1])

    # 7. Water/path: tree in the pond, tree on the path, extra bridge.
    M = clone(L)
    k = np.unravel_index(np.argmin(L.water_sdf), L.water_sdf.shape)  # deepest water cell (pond centre may be an island)
    M.placements.append(item('tree_02', float(L.grid.x[k]), float(L.grid.y[k])))
    v = C.check_water_and_path(M)
    case('counterexample: tree in pond', any('in water' in x for x in v), v[:1])
    M = clone(L)
    p = L.path[len(L.path) // 3]
    M.placements.append(item('tree_02', float(p[0]), float(p[1])))
    v = C.check_water_and_path(M)
    case('counterexample: tree on path', any('from path edge' in x for x in v), v[:1])
    M = clone(L)
    M.placements.append(item('bridge', 0.0, 20.0))
    v = C.check_water_and_path(M)
    case('counterexample: second bridge', any('bridge' in x for x in v), v[:1])

    # 8. Landmarks: move everything far away -> start screen without landmarks must fail.
    M = clone(L)
    M.groves = []
    M.outcrops = []
    M.camp = None
    M.pond_sdf = np.full(L.grid.x.shape, 5.0)
    v = C.check_landmarks(M)
    case('counterexample: empty start screen', any('landmarks' in x for x in v), v[:1])

    # 9. Export formats.
    with tempfile.TemporaryDirectory() as tmp:
        data, cells, info = E.write_all(L, tmp, masks=True)
        from PIL import Image
        sizes = {f: Image.open(Path(tmp) / f).size for f in ('FieldPathMask.png', 'FieldWaterMask.png', 'FieldBankMask.png', 'FieldZoneMask.png')}
        case('export mask sizes 3456x1728', all(s == (3456, 1728) for s in sizes.values()), str(set(sizes.values())))
        keys = {'seed', 'groundHalfDiagonal', 'pathClearance', 'roadsideClearance', 'startClearance', 'yellowClearance', 'path', 'banks', 'water', 'placements'}
        case('export field_layout keys superset of FieldLayoutData', keys <= set(data), sorted(keys - set(data)))
        four = all(abs(a[0] - b[0]) + abs(a[1] - b[1]) == 1 for a, b in zip(cells, cells[1:]))
        case('export ground_path cells 4-connected', four and len(cells) > 40, '%d cells' % len(cells))
        runs = data['banks'][0]['runs']
        n_cells = sum(r['xMax'] - r['xMin'] for r in runs)
        case('export bank runs == bank cells', n_cells == int(L.bank.sum()), '%d cells' % n_cells)

    bad = [r for r in results if not r[1]]
    print('\n%d/%d PASS' % (len(results) - len(bad), len(results)))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
