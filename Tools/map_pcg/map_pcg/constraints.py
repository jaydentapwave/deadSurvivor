"""Gameplay / layout constraints and composition score.

check(L) returns a list of violation strings (empty = pass). The self-test feeds
counterexamples into these same functions, so a check that never fails is caught.
"""
import math
import numpy as np

from . import fields as F
from .generate import _visible, _in_corner_box


def _cat(L, it):
    return L.cfg['catalog'][it['sprite'] or it['prop']]


def check_path_continuity(L):
    out = []
    g = L.grid
    mask = L.path_mask.copy()
    lab, n = F.label_components(mask)
    a = L.path[0]
    b = L.path[-1]
    # Endpoints lie on L1 33.5 (inside the ground L1 36), sample a little inwards.
    ra, ca = g.index(a[0] * 0.98, a[1] * 0.98)
    rb, cb = g.index(b[0] * 0.98, b[1] * 0.98)
    la, lb = lab[ra, ca], lab[rb, cb]
    if la == 0 or lb == 0 or la != lb:
        out.append('path: not continuous edge to edge (labels %d,%d of %d)' % (la, lb, n))
    turn = F.max_turn_per_metre(L.path, 1.0)
    if turn > L.cfg['path']['maxTurnDegPerMetre'] + 1e-6:
        out.append('path: sharp corner %.1f deg/m' % turn)
    if np.max(F.l1(L.path[:, 0], L.path[:, 1])) > L.cfg['bounds']['groundL1']:
        out.append('path: leaves the ground mesh')
    return out


def check_clear_center(L):
    g = L.cfg['gameplay']
    out = []
    for it in L.placements:
        l1 = abs(it['x']) + abs(it['y'])
        if l1 <= g['clearAnyL1']:
            out.append('center: %s at L1 %.2f <= %.2f' % (it['sprite'] or it['prop'], l1, g['clearAnyL1']))
        elif it['tall'] and l1 <= g['clearTallL1']:
            out.append('center: tall %s at L1 %.2f <= %.2f' % (it['sprite'] or it['prop'], l1, g['clearTallL1']))
        if it['sprite'] == g['yellowSprite'] and l1 <= g['yellowFlowerClearL1']:
            out.append('center: yellow flower at L1 %.2f' % l1)
    tall_band = sum(1 for it in L.placements if it['tall'] and abs(it['x']) + abs(it['y']) <= g['moveBandL1'])
    if tall_band > g['maxTallInMoveBand']:
        out.append('center: %d tall props in move band > %d' % (tall_band, g['maxTallInMoveBand']))
    return out


def check_spacing(L):
    out = []
    items = L.placements
    pts = np.array([[it['x'], it['y']] for it in items])
    fps = np.array([_cat(L, it).get('footprint', 0.3) for it in items])
    groups = [it['group'] for it in items]
    for i in range(len(items)):
        d = np.hypot(pts[i + 1:, 0] - pts[i, 0], pts[i + 1:, 1] - pts[i, 1])
        need = (fps[i + 1:] + fps[i]) * 0.6
        bad = np.nonzero(d < need - 1e-6)[0]
        for j in bad:
            j = j + i + 1
            if groups[i].startswith('wall_') and groups[j] == groups[i]:
                continue
            out.append('spacing: %s(%.2f,%.2f) - %s(%.2f,%.2f) d=%.2f < %.2f' % (
                items[i]['sprite'] or items[i]['prop'], pts[i, 0], pts[i, 1],
                items[j]['sprite'] or items[j]['prop'], pts[j, 0], pts[j, 1], d[j - i - 1], need[j - i - 1]))
    trees = pts[[k for k, it in enumerate(items) if it['group'] == 'tree']]
    for i in range(len(trees)):
        d = np.hypot(*(trees[i + 1:] - trees[i]).T)
        if np.any(d < L.cfg['placement']['treeSpacing'] - 1e-6):
            out.append('spacing: trees closer than %.2f' % L.cfg['placement']['treeSpacing'])
    return out


def check_bounds(L):
    cfg = L.cfg
    out = []
    for it in L.placements:
        x, y = it['x'], it['y']
        name = it['sprite'] or it['prop']
        l1 = abs(x) + abs(y)
        if it['group'] == 'rim':
            lo, hi = cfg['placement']['rimTrees']['l1']
            if y <= 0 or not (lo - 0.35 <= l1 <= hi + 0.35):
                out.append('bounds: rim %s at (%.2f,%.2f) L1 %.2f' % (name, x, y, l1))
            continue
        if l1 > cfg['bounds']['fieldL1'] or not _in_corner_box(x, y, cfg):
            out.append('bounds: %s at (%.2f,%.2f) outside field/corner box' % (name, x, y))
        elif not _visible(x, y, cfg):
            out.append('bounds: %s at (%.2f,%.2f) never visible' % (name, x, y))
    return out


def check_water_and_path(L):
    cfg = L.cfg
    out = []
    g = L.grid
    for it in L.placements:
        name = it['sprite'] or it['prop']
        if it['group'] == 'rim':
            continue
        w = F.bilinear(g, L.water_sdf, it['x'], it['y'])
        if it['group'] not in ('shore',) and not it['group'].startswith('wall_') and name != 'bridge' and w < 0:
            out.append('water: %s at (%.2f,%.2f) in water' % (name, it['x'], it['y']))
        if name == 'bridge':
            continue
        pd = F.bilinear(g, L.path_dist_grid, it['x'], it['y']) - F.bilinear(g, L.path_hw_grid, it['x'], it['y'])
        if L.branch is not None and not it.get('camp'):
            pd = min(pd, F.bilinear(g, L.branch_dist_grid, it['x'], it['y']) - cfg['path']['branchHalfWidth'])
        need = 0.0
        if it['tall'] and not _cat(L, it).get('roadside'):
            need = cfg['placement']['pathClearanceTall'] - 0.05
        if pd < need:
            out.append('path: %s at (%.2f,%.2f) %.2f m from path edge < %.2f' % (name, it['x'], it['y'], pd, need))
    n_bridge = sum(1 for it in L.placements if it['prop'] == 'bridge')
    if (L.cross is not None) != (n_bridge == 1) or n_bridge > 1:
        out.append('bridge: stream crossing %s but %d bridges' % (L.cross is not None, n_bridge))
    if L.cross is not None:
        # Bridge must be where path and water overlap.
        b = [it for it in L.placements if it['prop'] == 'bridge'][0]
        if np.hypot(b['x'] - L.cross[0], b['y'] - L.cross[1]) > 0.8:
            out.append('bridge: not on the crossing')
        crossings = _count_path_water_crossings(L)
        if crossings != 1:
            out.append('bridge: path meets water %d times' % crossings)
    return out


def _count_path_water_crossings(L):
    sp, _ = F.resample(L.path, 0.1)
    w = F.bilinear(L.grid, L.water_sdf, sp[:, 0], sp[:, 1]) < 0
    return int(np.sum(np.diff(w.astype(int)) == 1) + (1 if w[0] else 0))


def start_rect(L):
    ss = L.cfg['bounds']['startScreen']
    return ss['halfWidth'], ss['halfDepth']


def check_landmarks(L):
    out = []
    hw, hd = start_rect(L)
    g = L.grid
    inside = (np.abs(g.x) <= hw) & (np.abs(g.y) <= hd)
    pond_cells = int(np.sum(inside & (L.pond_sdf < 0)))
    seen = []
    if pond_cells > 2 * 64:
        seen.append('pond')
    if any(abs(c[0]) <= hw + R * 0.3 and abs(c[1]) <= hd + R * 0.3 for c, R in L.groves):
        seen.append('grove')
    if any(abs(c[0]) <= hw and abs(c[1]) <= hd for c in L.outcrops):
        seen.append('outcrop')
    if L.camp is not None and abs(L.camp[0]) <= hw + 1 and abs(L.camp[1]) <= hd + 1:
        seen.append('camp')
    if int(np.sum(inside & L.path_mask)) < 64 * 8:
        out.append('landmarks: path barely in start screen')
    if len(seen) < 2:
        out.append('landmarks: only %s in start screen' % seen)
    L.start_landmarks = seen
    return out


def check(L):
    out = []
    for fn in (check_path_continuity, check_clear_center, check_spacing, check_bounds, check_water_and_path, check_landmarks):
        out += fn(L)
    return out


def max_window_count(points, w=20.0, h=22.5):
    best = 0
    pts = np.asarray(points)
    for left in pts[:, 0] if len(pts) else []:
        ys = np.sort(pts[(pts[:, 0] >= left) & (pts[:, 0] <= left + w), 1])
        j = 0
        for i in range(len(ys)):
            while ys[i] - ys[j] > h:
                j += 1
            best = max(best, i - j + 1)
    return best


def metrics(L):
    """Numbers for the report and the score (start screen = 1920x1080 around the origin)."""
    hw, hd = start_rect(L)
    g = L.grid
    inside = (np.abs(g.x) <= hw) & (np.abs(g.y) <= hd)
    n = inside.sum()
    frac = {
        'water': float(np.sum(inside & L.water) / n),
        'path': float(np.sum(inside & (L.path_mask | L.branch_mask)) / n),
        'worn': float(np.sum(inside & L.worn) / n),
        'gravel': float(np.sum(inside & L.gravel) / n),
        'meadow': float(np.sum(inside & L.meadow) / n),
        'bank': float(np.sum(inside & L.bank) / n),
    }
    items = [it for it in L.placements if abs(it['x']) <= hw + 0.8 and -hd - 1.5 <= it['y'] <= hd + 0.5]
    tall = [it for it in items if it['tall']]
    left = sum(1 for it in tall if it['x'] < -2)
    right = sum(1 for it in tall if it['x'] > 2)
    sides = sum(1 for it in tall if abs(it['x']) > 5)
    centre = sum(1 for it in tall if abs(it['x']) < 3.5 and abs(it['y']) < 5)
    path_len = float(np.sum(inside & L.path_mask) / 64.0 / (2 * L.cfg['path']['halfWidth']))
    occ = np.zeros(g.x.shape, bool)
    for it in items:
        occ |= np.hypot(g.x - it['x'], g.y - it['y']) < 1.5
    open_frac = float(1 - np.sum(occ & inside) / n)
    groups = {}
    for it in L.placements:
        key = 'wall' if it['group'].startswith('wall_') else it['group']
        groups[key] = groups.get(key, 0) + 1
    dens = {}
    for grp in ('tree', 'boulder', 'stone', 'bush', 'flowers'):
        pts = [[it['x'], it['y']] for it in L.placements if it['group'] == grp]
        dens[grp] = max_window_count(pts) if pts else 0
    ent = [v for v in frac.values() if v > 0.005]
    entropy = float(-sum(v / sum(ent) * math.log(v / sum(ent)) for v in ent)) if ent else 0.0
    return {
        'startScreenGround': {k: round(v, 3) for k, v in frac.items()},
        'startScreenTall': len(tall), 'tallLeft': left, 'tallRight': right, 'tallSides': sides, 'tallCentre': centre,
        'startScreenItems': len(items), 'pathLengthInStart_m': round(path_len, 1), 'openFraction': round(open_frac, 3),
        'groundEntropy': round(entropy, 3), 'landmarks': getattr(L, 'start_landmarks', []),
        'pathMaxTurnDegPerM': round(F.max_turn_per_metre(L.path, 1.0), 1),
        'counts': groups, 'densityWindowMax': dens,
        'hasStream': L.stream is not None, 'hasFalls': L.falls is not None, 'hasIsland': L.island is not None,
        'hasCamp': L.camp is not None,
    }


def score(m):
    """Composition score (higher is better). Weights are a first guess (추정) — tune against user picks."""
    s = 0.0
    s += 1.0 * len(m['landmarks'])
    balance = min(m['tallLeft'], m['tallRight']) / max(1, max(m['tallLeft'], m['tallRight']))
    s += 1.5 * balance
    s += 1.0 * min(1.0, m['tallSides'] / 8.0)
    s -= 0.5 * m['tallCentre']
    s += 1.0 * (1 - abs(m['openFraction'] - 0.62) / 0.62)
    s += 1.0 * min(1.0, m['groundEntropy'] / 1.4)
    s += 0.5 * (1 if 8 <= m['pathLengthInStart_m'] <= 28 else 0)
    s += 0.5 * (1 if m['hasStream'] else 0) + 0.3 * (1 if m['hasCamp'] else 0)
    return round(s, 3)
