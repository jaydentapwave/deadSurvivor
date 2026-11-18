"""Layered battlefield generator (deterministic by seed).

Layers, in order (each only reads layers above it):
  1 path      smoothed spline edge-to-edge past the start area, width noise
  2 water     organic pond (+ island), stream from the far rim crossing the path once -> bridge
  3 banks     low ledge arc on the pond's far shore, island, stream gully above the falls
  4 camp      landmark site + branch trail from the main path
  5 zones     grass tone (hue only), worn dirt, meadow, gravel (grid masks)
  6 placement groves + forest edge, rim trees, outcrops, shore runs, meadows, path tufts,
              fence runs, stone wall runs, signs, fill
  7 checks    see constraints.py; scoring in score.py
"""
import math
import numpy as np

from . import fields as F


class Layout:
    pass


def _diamond_edge_point(edge, u, l1):
    """Point on the diamond |x|+|y| = l1. edge in {'NE','NW','SW','SE'}, u in [0,1] along the edge."""
    sx, sy = {'NE': (1, 1), 'NW': (-1, 1), 'SW': (-1, -1), 'SE': (1, -1)}[edge]
    x = u * l1
    y = l1 - x
    return np.array([sx * x, sy * y])


def _opposite(edge):
    return {'NE': 'SW', 'SW': 'NE', 'NW': 'SE', 'SE': 'NW'}[edge]


def _visible(x, y, cfg):
    v = cfg['bounds']['visible']
    return (np.maximum(0, np.abs(x) - v['halfWidth']) + np.maximum(0, np.abs(y) - v['halfDepth'])) <= v['moveLimitL1']


def _in_corner_box(x, y, cfg):
    bx, by = cfg['bounds']['cornerBox']
    return (np.abs(x) <= bx) & (np.abs(y) <= by)


# ---------------------------------------------------------------- 1 path

def _make_path(rng, cfg):
    pc = cfg['path']
    for attempt in range(400):
        edge = ['SW', 'NW'][int(rng.integers(2))]
        a = _diamond_edge_point(edge, rng.uniform(0.3, 0.7), 33.5)
        b = _diamond_edge_point(_opposite(edge), rng.uniform(0.3, 0.7), 33.5)
        ab = b - a
        n = np.array([-ab[1], ab[0]]) / np.linalg.norm(ab)
        k = pc['controlPoints']
        ts = np.linspace(0.2, 0.8, k) + rng.uniform(-0.05, 0.05, k)
        sign = 1 if rng.random() < 0.5 else -1
        ctrl = [a]
        for i, t in enumerate(ts):
            off = sign * rng.uniform(2.0, 6.5) * (1 if i % 2 == 0 else -0.8)
            ctrl.append(a + ab * t + n * off)
        ctrl.append(b)
        ctrl = np.array(ctrl)
        poly = F.catmull_rom(ctrl, 8)
        # Shift the whole curve sideways so it passes the start area at the wanted distance.
        d0 = np.min(np.hypot(poly[:, 0], poly[:, 1]))
        want = rng.uniform(pc['minStartDistance'], pc['maxStartDistance'])
        idx = int(np.argmin(np.hypot(poly[:, 0], poly[:, 1])))
        near = poly[idx]
        side = np.sign(near @ n) or 1.0
        shift = n * side * (want - d0)
        ctrl[1:-1] += shift
        poly = F.catmull_rom(ctrl, 8)
        dmin = np.min(np.hypot(poly[:, 0], poly[:, 1]))
        if not (pc['minStartDistance'] <= dmin <= pc['maxStartDistance'] + 0.5):
            continue
        if F.max_turn_per_metre(poly, 1.0) > pc['maxTurnDegPerMetre']:
            continue
        if np.max(F.l1(poly[:, 0], poly[:, 1])) > 35.0:
            continue
        return poly, ctrl, attempt
    raise RuntimeError('[map_pcg] no valid path in 400 attempts')


def _half_width(s, base, var, seed):
    return base * (1 + var * (2 * F.value_noise(s, 0.0, 6.0, seed) - 1))


# ---------------------------------------------------------------- 2 water

def _radius_fn(rng, R, lobes, amp):
    ks = np.arange(2, 2 + lobes)
    a = rng.uniform(-amp, amp, lobes) / np.sqrt(ks - 1)
    ph = rng.uniform(0, 2 * math.pi, lobes)

    def r(theta):
        theta = np.asarray(theta)
        out = np.ones_like(theta, dtype=float)
        for k, ak, pk in zip(ks, a, ph):
            out = out + ak * np.cos(k * theta + pk)
        return R * out
    return r


def _blob_sdf(x, y, c, rfn, seed, rough=0.12):
    dx = x - c[0]
    dy = y - c[1]
    th = np.arctan2(dy, dx)
    return np.hypot(dx, dy) - rfn(th) + rough * (2 * F.fbm(x, y, 0.6, seed, 2) - 1)


def _path_side(px, py, poly, grid, dist, par):
    """+1/-1 side of the path for points; uses the tangent at the closest path point."""
    s = F.arc_length(poly)
    q = F.bilinear(grid, par, px, py)
    i = np.clip(np.searchsorted(s, q) - 1, 0, len(poly) - 2)
    t = poly[i + 1] - poly[i]
    rel = np.stack([px, py], -1) - poly[i]
    return np.sign(t[..., 0] * rel[..., 1] - t[..., 1] * rel[..., 0])


def _make_water(rng, cfg, L, grid):
    wc = cfg['water']
    pd = L.path_dist_grid
    for attempt in range(600):
        R = rng.uniform(*wc['pondRadius'])
        c = np.array([rng.uniform(-14, 14), rng.uniform(-6, 14)])
        l1 = F.l1(*c)
        if not (wc['pondL1'][0] <= l1 <= wc['pondL1'][1]):
            continue
        # Landmark rule: pond must reach into the start screen.
        ss = cfg['bounds']['startScreen']
        if abs(c[0]) - R * 0.8 > ss['halfWidth'] or abs(c[1]) - R * 0.6 > ss['halfDepth'] - 1.0:
            continue
        if np.hypot(*c) - R * 1.25 < 6.0:
            continue
        if F.bilinear(grid, pd, c[0], c[1]) < R * 1.3 + wc['pathClearance']:
            continue
        break
    else:
        raise RuntimeError('[map_pcg] no pond site')
    L.pond_center = c
    L.pond_R = R
    L.pond_rfn = _radius_fn(rng, R, wc['pondLobes'], wc['pondLobeAmp'])
    L.pond_seed = int(rng.integers(1 << 30))
    L.island = None
    if rng.random() < wc['islandChance']:
        ang = rng.uniform(0, 2 * math.pi)
        ic = c + np.array([math.cos(ang), math.sin(ang)]) * R * rng.uniform(0.15, 0.35)
        L.island = (ic, _radius_fn(rng, rng.uniform(*wc['islandRadius']), 3, 0.25), int(rng.integers(1 << 30)))

    # Stream: from the far rim to the pond, must cross the main path exactly once.
    pond_side = _path_side(np.array([c[0]]), np.array([c[1]]), L.path, grid, pd, L.path_par_grid)[0]
    L.stream = None
    for attempt in range(500):
        # Far rim first (the stream comes from beyond the top of the screen), then any rim.
        edges = ['NW', 'NE'] if attempt < 250 else ['NW', 'NE', 'SW', 'SE']
        edge = edges[int(rng.integers(len(edges)))]
        src = _diamond_edge_point(edge, rng.uniform(0.25, 0.75), 34.0)
        src_side = _path_side(np.array([src[0]]), np.array([src[1]]), L.path, grid, pd, L.path_par_grid)[0]
        if src_side == pond_side:
            continue
        to = c - src
        dist = np.linalg.norm(to)
        n = np.array([-to[1], to[0]]) / dist
        end = c - to / dist * (R * 0.6)
        mids = [src + to * t + n * rng.uniform(-4, 4) for t in (0.3, 0.62)]
        poly = F.catmull_rom(np.array([src] + mids + [end]), 8)
        if np.min(np.hypot(poly[:, 0], poly[:, 1])) < 8.0:
            continue
        if F.max_turn_per_metre(poly, 1.0) > 30:
            continue
        sd = F.bilinear(grid, pd, poly[:, 0], poly[:, 1])
        side = _path_side(poly[:, 0], poly[:, 1], L.path, grid, pd, L.path_par_grid)
        near = sd < 2.5
        flips = np.nonzero(np.diff(side) != 0)[0]
        flips = [f for f in flips if near[f] or near[f + 1]]
        crossings = int(np.sum(sd[1:] < 0.4) > 0)
        if len(flips) != 1 or not crossings:
            continue
        # Crossing point and its distance to the start/landmarks.
        k = int(np.argmin(sd))
        cross = poly[k]
        if not _visible(cross[0], cross[1], cfg) or F.l1(*cross) < 9:
            continue
        L.stream = poly
        L.stream_hw = rng.uniform(*wc['streamHalfWidth'])
        L.cross = cross
        break
    if L.stream is None:
        L.cross = None


# ---------------------------------------------------------------- 3 masks on the grid

def _water_fields(L, grid, cfg):
    x, y = grid.x, grid.y
    pond_sdf = _blob_sdf(x, y, L.pond_center, L.pond_rfn, L.pond_seed)
    if L.island is not None:
        isdf = _blob_sdf(x, y, L.island[0], L.island[1], L.island[2], 0.06)
        pond_sdf = np.maximum(pond_sdf, -isdf)
        L.island_mask = isdf < 0
    else:
        L.island_mask = np.zeros(x.shape, bool)
    L.pond_sdf = pond_sdf
    if L.stream is not None:
        sd, sp = F.grid_polyline_distance(grid, L.stream, 10.0)
        wob = 0.12 * (2 * F.fbm(x, y, 1.2, 77, 2) - 1)
        L.stream_dist = sd - (L.stream_hw + wob)
        L.stream_par = sp
    else:
        L.stream_dist = np.full(x.shape, 99.0)
        L.stream_par = np.zeros(x.shape)
    L.water_sdf = np.minimum(pond_sdf, L.stream_dist)
    L.water = L.water_sdf < 0


def _bank_fields(rng, L, grid, cfg):
    bc = cfg['banks']
    x, y = grid.x, grid.y
    th = np.arctan2(y - L.pond_center[1], x - L.pond_center[0])
    centre = math.pi / 2 + rng.uniform(-0.5, 0.5)
    half = math.radians(bc['shoreArcDeg']) / 2
    dth = np.abs((th - centre + math.pi) % (2 * math.pi) - math.pi)
    w = bc['shoreWidth'][0] + (bc['shoreWidth'][1] - bc['shoreWidth'][0]) * F.fbm(x, y, 2.0, 91, 2)
    taper = np.clip((half - dth) / 0.5, 0, 1)
    shore = (L.pond_sdf >= 0) & (L.pond_sdf < np.maximum(w * taper, 0)) & (dth < half) & ~L.island_mask
    # Opening (radius 3 cells = 0.375 m) removes hair-thin ledge ribbons (K21: no floating slivers).
    shore = F.dilate(F.erode(shore, 3), 3) & (L.pond_sdf >= 0)
    bank = shore | L.island_mask
    L.falls = None
    L.stream_upper = None
    if bc.get('falls') and L.stream is not None:
        s = F.arc_length(L.stream)
        vis = _visible(L.stream[:, 0], L.stream[:, 1], cfg)
        first = int(np.argmax(vis))
        k = min(len(s) - 1, first + 14)
        s_fall = s[k]
        # Keep the bridge on the lower part.
        cross_s = s[int(np.argmin(np.hypot(*(L.stream - L.cross).T)))] if L.cross is not None else 1e9
        if s_fall < cross_s - 3.0:
            gully = (L.stream_par < s_fall) & (L.stream_dist >= 0) & (L.stream_dist < 1.1 + 0.5 * F.fbm(x, y, 1.5, 93, 2))
            upper_water = (L.stream_par < s_fall) & (L.stream_dist < 0)
            bank = bank | gully
            L.falls = (L.stream[k], L.stream[min(k + 2, len(s) - 1)])
            L.stream_upper_mask = upper_water
            L.s_fall = s_fall
    if L.falls is None:
        L.stream_upper_mask = np.zeros(x.shape, bool)
    # Far-edge terrace (K21 "들판 가장자리 띠"): low ledge band along the two far edges, noisy inner line.
    et = bc.get('edgeTerrace')
    if et:
        inner = et['l1'] + et['wobble'] * (2 * F.fbm(x, y, 3.0, L.seed + 95, 3) - 1)
        terrace = (F.l1(x, y) >= inner) & (y > et['minY']) & (F.l1(x, y) <= 36.0)
        terrace = F.dilate(F.erode(terrace, 2), 2)
        bank = bank | (terrace & ~L.water)
    # Banks are never on the path (the path cuts a ramp gap through ledges).
    bank &= ~F.dilate(L.path_mask, 2)
    L.bank = bank & ~L.water | (L.island_mask & ~L.water)


# ---------------------------------------------------------------- 4 camp + branch

def _make_camp(rng, cfg, L, grid):
    cc = cfg['placement']['camp']
    best = None
    for attempt in range(800):
        c = np.array([rng.uniform(-22, 22), rng.uniform(-22, 22)])
        l1 = F.l1(*c)
        if not (cc['l1'][0] <= l1 <= cc['l1'][1]) or not _visible(c[0], c[1], cfg) or not _in_corner_box(c[0], c[1], cfg):
            continue
        # Whole camp (radius ~3 m) inside the visible octagon.
        if not all(_visible(c[0] + dx, c[1] + dy, cfg) for dx, dy in ((3, 0), (-3, 0), (0, 3), (0, -3))):
            continue
        pdist = F.bilinear(grid, L.path_dist_grid, c[0], c[1])
        if not (cc['pathDistance'][0] <= pdist <= cc['pathDistance'][1]):
            continue
        if F.bilinear(grid, L.water_sdf, c[0], c[1]) < 4.0 or F.bilinear(grid, L.bank.astype(float), c[0], c[1]) > 0:
            continue
        if L.cross is not None and np.hypot(*(c - L.cross)) < 7:
            continue
        best = c
        break
    L.camp = best
    L.branch = None
    if best is None or not cfg['path'].get('branchToCamp'):
        return
    # Branch from the closest main-path point, bending once.
    i = int(np.argmin(np.hypot(*(L.path - best).T)))
    start = L.path[i]
    to = best - start
    n = np.array([-to[1], to[0]]) / max(np.linalg.norm(to), 1e-6)
    mid = start + to * 0.5 + n * rng.uniform(-1.2, 1.2)
    end = best - to / np.linalg.norm(to) * 1.2
    L.branch = F.catmull_rom(np.array([start, mid, end]), 8)
    L.branch_s0 = F.arc_length(L.path)[i]


# ---------------------------------------------------------------- 5 zones

def _zones(rng, cfg, L, grid):
    z = cfg['zones']
    x, y = grid.x, grid.y
    seed = L.seed
    tone = 2 * F.fbm(x, y, z['toneScale'], seed + 11, 3) - 1
    near_water = np.exp(-np.maximum(L.water_sdf, 0) / 2.5)
    tone = np.clip(tone * 0.8 + 0.6 * near_water, -1, 1)
    L.tone = tone
    wear = F.fbm(x, y, z['wornScale'], seed + 21, 3)
    pd = L.path_dist_grid - L.path_hw_grid
    wear = wear + 0.14 * np.exp(-np.maximum(pd, 0) / z['wornNearPath'])
    if L.camp is not None:
        wear = wear + 0.45 * np.exp(-(np.hypot(x - L.camp[0], y - L.camp[1]) / 3.0) ** 2)
    # Party start area is the most walked ground.
    wear = wear + 0.12 * np.exp(-(np.hypot(x, y) / 4.0) ** 2)
    L.wear_f = wear - z['wornThreshold']
    L.worn = (L.wear_f > 0) & ~L.water & ~L.path_mask & ~L.bank
    mead = F.fbm(x, y, z['meadowScale'], seed + 31, 3)
    L.mead_f = mead - z['meadowThreshold']
    L.meadow = (L.mead_f > 0) & (pd > 1.5) & (L.water_sdf > 1.0) & ~L.worn & ~L.bank
    # Gravel spots on path shoulders (bends wear first). gravel_f > 0 = gravel.
    gspot = F.fbm(x, y, 1.6, seed + 41, 2)
    L.gravel_f = np.where((pd > -0.2) & (pd < 0.9), gspot - 0.74, -1.0)
    L.gravel = (L.gravel_f > 0) & ~L.water & ~L.bank


# ---------------------------------------------------------------- 6 placement

class Placer:
    def __init__(self, cfg, L, grid, rng):
        self.cfg = cfg
        self.L = L
        self.grid = grid
        self.rng = rng
        self.items = []
        self.pts = np.zeros((0, 2))
        self.fps = np.zeros(0)

    def path_clear(self, x, y, camp=False):
        d = F.bilinear(self.grid, self.L.path_dist_grid, x, y) - F.bilinear(self.grid, self.L.path_hw_grid, x, y)
        if self.L.branch is not None and not camp:
            d = min(d, F.bilinear(self.grid, self.L.branch_dist_grid, x, y) - self.cfg['path']['branchHalfWidth'])
        return d

    def ok(self, name, x, y, spacing=None, need_visible=True, allow_water=False, allow_path=False, rim=False):
        cfg = self.cfg
        cat = cfg['catalog'][name]
        g = cfg['gameplay']
        pc = cfg['placement']
        l1 = abs(x) + abs(y)
        if not rim:
            if l1 > cfg['bounds']['fieldL1'] or not _in_corner_box(x, y, cfg):
                return False
            if need_visible and not _visible(x, y, cfg):
                return False
        if l1 <= g['clearAnyL1']:
            return False
        if cat.get('tall') and l1 <= g['clearTallL1']:
            return False
        if name == g['yellowSprite'] and l1 <= g['yellowFlowerClearL1']:
            return False
        if not allow_water and F.bilinear(self.grid, self.L.water_sdf, x, y) < pc['waterClearance'] * (1 if cat.get('tall') else 0.4):
            return False
        if not allow_path:
            clear = pc['pathClearanceTall'] if cat.get('tall') or name.startswith('bush') else pc['pathClearanceLow']
            if cat.get('roadside'):
                clear = 0.9
            if self.path_clear(x, y) < clear:
                return False
        fp = cat.get('footprint', 0.3)
        if len(self.pts):
            d = np.hypot(self.pts[:, 0] - x, self.pts[:, 1] - y)
            need = (self.fps + fp) if spacing is None else np.maximum(spacing, (self.fps + fp) * 0.6)
            if np.any(d < need):
                return False
        if cat.get('tall') and l1 <= g['moveBandL1']:
            n_tall = sum(1 for it in self.items if it['tall'] and abs(it['x']) + abs(it['y']) <= g['moveBandL1'])
            if n_tall >= g['maxTallInMoveBand']:
                return False
        return True

    def add(self, name, x, y, group=None, yaw=0.0, scale=1.0, flip=None, roadside=False, spacing_fp=None, camp=False):
        cfg = self.cfg
        cat = cfg['catalog'][name]
        on_bank = F.bilinear(self.grid, self.L.bank.astype(float), x, y) > 0.5
        it = {
            'kind': cat['kind'],
            'group': group or cat.get('group', name),
            'sprite': name if cat['kind'] == 'card' else '',
            'prop': name if cat['kind'] == 'prop' else '',
            'x': float(x), 'y': float(y),
            'height': cfg['banks']['height'] if on_bank else 0.0,
            'yaw': float(yaw), 'scale': float(scale),
            'isFlipped': bool(self.rng.random() < 0.5) if flip is None else bool(flip),
            'isCastingShadow': bool(cat.get('shadow', False)),
            'isRoadside': bool(roadside),
            'tall': bool(cat.get('tall', False)),
            'camp': bool(camp),
        }
        self.items.append(it)
        self.pts = np.vstack([self.pts, [x, y]])
        self.fps = np.append(self.fps, cat.get('footprint', 0.3) if spacing_fp is None else spacing_fp)
        return it

    def try_add(self, name, x, y, **kw):
        place_kw = {k: kw.pop(k) for k in list(kw) if k in ('spacing', 'need_visible', 'allow_water', 'allow_path', 'rim')}
        if self.ok(name, x, y, **place_kw):
            return self.add(name, x, y, **kw)
        return None


def _tangent(poly, i):
    j = min(i + 1, len(poly) - 1)
    k = max(i - 1, 0)
    t = poly[j] - poly[k]
    return t / max(np.linalg.norm(t), 1e-9)


def _rail_yaw(t):
    """Unity yaw (deg) that lays a local-X rail along floor tangent t."""
    return math.degrees(math.atan2(-t[1], t[0]))


def _long_yaw(t):
    """Unity yaw (deg) that lays a local-Z long axis along floor tangent t."""
    return math.degrees(math.atan2(t[0], t[1]))


def _place(rng, cfg, L, grid):
    P = Placer(cfg, L, grid, rng)
    pc = cfg['placement']
    path_s = F.arc_length(L.path)

    # Landmarks first: bridge, camp, signs.
    if L.cross is not None:
        i = int(np.argmin(np.hypot(*(L.path - L.cross).T)))
        t = _tangent(L.path, i)
        P.add('bridge', L.path[i][0], L.path[i][1], group='bridge', yaw=_long_yaw(t), flip=False)
        L.bridge_s = path_s[i]
    else:
        L.bridge_s = None
    if L.camp is not None:
        c = L.camp
        to_path = L.branch[0] - c if L.branch is not None else -c
        base = math.atan2(to_path[1], to_path[0])
        for k, da in enumerate((-0.9, 0.9)):
            a = base + da
            p = c + np.array([math.cos(a), math.sin(a)]) * 1.7
            # Door (+Z) faces the camp centre.
            face = c - p
            P.add('tent', p[0], p[1], group='tent', yaw=_long_yaw(face / np.linalg.norm(face)), scale=1.0 if k == 0 else 0.95, flip=False, camp=True)
        for k in range(cfg['placement']['camp']['flags']):
            a = base + math.pi + (k - 1) * 0.9
            p = c + np.array([math.cos(a), math.sin(a)]) * 2.4
            P.add('flag', p[0], p[1], group='flag', flip=False, camp=True)
        # Two fence pieces closing the back of the camp.
        for k in (-1, 1):
            a = base + math.pi + k * 0.35
            p = c + np.array([math.cos(a), math.sin(a)]) * 3.3
            t = np.array([-math.sin(a), math.cos(a)])
            P.add('fence', p[0], p[1], group='fence', yaw=_rail_yaw(t), flip=False, camp=True)
        for k in range(3):
            a = rng.uniform(0, 2 * math.pi)
            p = c + np.array([math.cos(a), math.sin(a)]) * rng.uniform(2.6, 3.6)
            P.try_add('rock_02', p[0], p[1], group='stone')

    # Signs: path entry into the visible octagon, and the branch fork (or bridge).
    vis = _visible(L.path[:, 0], L.path[:, 1], cfg)
    i_entry = min(len(L.path) - 1, int(np.argmax(vis)) + 12)
    sign_spots = [i_entry]
    if L.branch is not None:
        sign_spots.append(int(np.argmin(np.abs(path_s - L.branch_s0 - 1.5))))
    elif L.bridge_s is not None:
        sign_spots.append(int(np.argmin(np.abs(path_s - L.bridge_s + 3.2))))
    for i0 in sign_spots[:pc['signs']]:
        done = False
        for di in (0, 8, -8, 16, -16, 24, -24, 32, -32):
            i = int(np.clip(i0 + di, 1, len(L.path) - 2))
            t = _tangent(L.path, i)
            n = np.array([-t[1], t[0]])
            hw = L.path_hw[i]
            for side in (1, -1):
                for extra in (0.95, 1.3):
                    p = L.path[i] + n * side * (hw + extra)
                    if P.try_add('sign', p[0], p[1], group='sign', yaw=_rail_yaw(t), roadside=True, flip=False):
                        done = True
                        break
                if done:
                    break
            if done:
                break

    # Fence runs along the path (visible, outside the move band, away from bridge/fork).
    camp_fences = sum(1 for it in P.items if it['prop'] == 'fence')
    remaining = pc['fenceTotal'] - camp_fences
    runs = pc['fenceRuns']
    for r in range(runs):
        want = remaining // (runs - r)
        for attempt in range(1200):
            if attempt and attempt % 400 == 0 and want > 3:
                want -= 1
            i0 = int(rng.integers(len(L.path)))
            side = 1 if rng.random() < 0.5 else -1
            off = rng.uniform(*pc['fenceOffset'])
            spots = []
            for k in range(want):
                s = path_s[i0] + k * 1.2
                i = int(np.searchsorted(path_s, s))
                if i >= len(L.path):
                    break
                spots.append(i)
            if len(spots) < want:
                continue
            good = True
            cand = []
            for i in spots:
                p = L.path[i]
                if F.l1(*p) < 9.0 or not _visible(p[0], p[1], cfg):
                    good = False
                    break
                if L.bridge_s is not None and abs(path_s[i] - L.bridge_s) < 4.0:
                    good = False
                    break
                if L.branch is not None and abs(path_s[i] - L.branch_s0) < 2.5:
                    good = False
                    break
                t = _tangent(L.path, i)
                n = np.array([-t[1], t[0]])
                q = p + n * side * (L.path_hw[i] + off)
                if not P.ok('fence', q[0], q[1], spacing=0.95) or any(np.hypot(*(q - c0)) < 0.95 for c0, _ in cand):
                    good = False
                    break
                cand.append((q, t))
            if not good:
                continue
            for q, t in cand:
                P.add('fence', q[0], q[1], group='fence', yaw=_rail_yaw(t), roadside=True, flip=False)
            remaining -= want
            break

    # Groves with forest edges.
    groves = []
    n_groves = int(rng.integers(pc['groves'][0], pc['groves'][1] + 1))
    ss = cfg['bounds']['startScreen']
    for g in range(n_groves):
        for attempt in range(500):
            R = rng.uniform(*pc['groveRadius'])
            if g == 0:
                # Frame the start screen on one side.
                c = np.array([rng.choice([-1, 1]) * rng.uniform(7.5, 11.5), rng.uniform(-6, 8)])
            elif g == 1 and groves:
                c = np.array([-np.sign(groves[0][0][0]) * rng.uniform(8, 14), rng.uniform(-11, 12)])
            else:
                c = np.array([rng.uniform(-23, 23), rng.uniform(-24, 24)])
            l1 = F.l1(*c)
            if not (pc['groveL1'][0] <= l1 <= pc['groveL1'][1]) or not _in_corner_box(c[0], c[1], cfg):
                continue
            if P.path_clear(c[0], c[1]) < R * 0.6 + 1.5:
                continue
            if F.bilinear(grid, L.water_sdf, c[0], c[1]) < R * 0.6 + 1.0:
                continue
            if L.camp is not None and np.hypot(*(c - L.camp)) < R + 3.5:
                continue
            if any(np.hypot(*(c - gc)) < R + gr + 2.5 for gc, gr in groves):
                continue
            groves.append((c, R))
            break
    L.groves = groves
    for gi, (c, R) in enumerate(groves):
        rfn = _radius_fn(rng, R, 3, 0.25)
        # Dense core -> sparse edge, Poisson by dart throwing.
        for k in range(int(R * R * 9)):
            a = rng.uniform(0, 2 * math.pi)
            rr = R * math.sqrt(rng.random())
            p = c + np.array([math.cos(a), math.sin(a)]) * rr
            edge = rr / rfn(a)
            if edge > 1:
                continue
            if rng.random() > 0.95 - 0.45 * edge:
                continue
            P.try_add('tree_02', p[0], p[1], group='tree', spacing=pc['treeSpacing'], scale=float(rng.uniform(0.92, 1.08)))
        # Forest edge: bushes and tufts in a ring just outside the grove boundary.
        for k in range(int(R * 20)):
            a = rng.uniform(0, 2 * math.pi)
            rr = rfn(a) * rng.uniform(0.85, 1.25)
            p = c + np.array([math.cos(a), math.sin(a)]) * rr
            if rng.random() < 0.45:
                P.try_add('bush_01' if rng.random() < 0.5 else 'bush_02', p[0], p[1], group='bush')
            else:
                P.try_add('grass_tuft_02' if rng.random() < 0.6 else 'grass_tuft_01', p[0], p[1], group='flowers')

    # Rim trees beyond the far edges (only tops show).
    rim = pc['rimTrees']
    for sx in (-1, 1):
        x = rim['absX'][0]
        row = 0
        while x <= rim['absX'][1]:
            for depth in (33.0, 34.1):
                if depth > 33.5 and rng.random() < 0.45:
                    continue
                xx = sx * (x + (0.7 if depth > 33.5 else 0))
                yy = depth - abs(xx) + rng.uniform(-0.3, 0.3)
                P.try_add('tree_02', xx, yy, group='rim', rim=True, need_visible=False)
            x += rim['spacing'] * rng.uniform(0.8, 1.2)
            row += 1

    # Rock outcrops with gravel.
    outcrops = []
    n_out = int(rng.integers(pc['outcrops'][0], pc['outcrops'][1] + 1))
    for o in range(n_out):
        for attempt in range(400):
            if o == 0:
                c = np.array([rng.uniform(-10, 10), rng.uniform(-9, 9)])
            else:
                c = np.array([rng.uniform(-22, 22), rng.uniform(-22, 22)])
            l1 = F.l1(*c)
            if not (pc['outcropL1'][0] <= l1 <= pc['outcropL1'][1]):
                continue
            if not P.ok('boulder_01', c[0], c[1]):
                continue
            if any(np.hypot(*(c - gc)) < gr + 1.0 for gc, gr in groves):
                continue
            if any(np.hypot(*(c - oc)) < 6 for oc in outcrops):
                continue
            outcrops.append(c)
            P.add('boulder_01', c[0], c[1], group='boulder', scale=float(rng.uniform(1.0, 1.35)))
            if rng.random() < 0.6:
                a = rng.uniform(0, 2 * math.pi)
                q = c + np.array([math.cos(a), math.sin(a)]) * rng.uniform(0.9, 1.3)
                P.try_add('boulder_01', q[0], q[1], group='boulder', scale=float(rng.uniform(0.75, 0.9)))
            for k in range(int(rng.integers(4, 9))):
                a = rng.uniform(0, 2 * math.pi)
                q = c + np.array([math.cos(a), math.sin(a)]) * rng.uniform(0.7, 2.2)
                P.try_add('rock_02' if rng.random() < 0.55 else 'rock_01', q[0], q[1], group='stone')
            for k in range(int(rng.integers(3, 7))):
                a = rng.uniform(0, 2 * math.pi)
                q = c + np.array([math.cos(a), math.sin(a)]) * rng.uniform(0.6, 1.8)
                P.try_add('grass_tuft_01', q[0], q[1], group='flowers')
            break
    L.outcrops = outcrops
    x, y = grid.x, grid.y
    gr = pc and cfg['zones']['gravelRadius']
    for c in outcrops:
        rr = rng.uniform(*gr)
        d = np.hypot(x - c[0], y - c[1]) / rr + 0.35 * (2 * F.fbm(x, y, 0.8, int(c[0] * 7 + c[1] * 13) + L.seed, 2) - 1)
        L.gravel_f = np.maximum(L.gravel_f, np.where(L.path_mask, -1.0, 1 - d))
        L.gravel = (L.gravel_f > 0) & ~L.water & ~L.bank

    # Shore stone runs around the pond and the lower stream.
    def shore_runs(points, normals, group='shore'):
        i = 0
        while i < len(points):
            run = int(rng.integers(3, 9))
            for k in range(run):
                if i + k >= len(points):
                    break
                p = points[i + k] + normals[i + k] * rng.uniform(-0.12, 0.05)
                name = 'rock_02' if rng.random() < 0.6 else 'rock_01'
                P.try_add(name, p[0], p[1], group=group, allow_water=True, spacing=0.26)
            i += run + int(rng.integers(2, 9))
            # Tuft in the gap, on land.
            if i < len(points):
                q = points[i - 1] + normals[i - 1] * rng.uniform(0.25, 0.55)
                P.try_add('grass_tuft_02', q[0], q[1], group='flowers')
    angs = np.arange(0, 2 * math.pi, 0.11)
    pts = []
    nrm = []
    for a in angs:
        # Walk outwards to the shore line of this sdf.
        d = np.array([math.cos(a), math.sin(a)])
        for rr in np.arange(0.5, L.pond_R * 2.2, 0.06):
            q = L.pond_center + d * rr
            if F.bilinear(grid, L.pond_sdf, q[0], q[1]) >= 0:
                pts.append(q)
                nrm.append(d)
                break
    if pts:
        shore_runs(np.array(pts), np.array(nrm))
    if L.stream is not None:
        sp, _ = F.resample(L.stream, 0.45)
        for side in (1, -1):
            pts = []
            nrm = []
            for i in range(1, len(sp) - 1):
                t = sp[i + 1] - sp[i - 1]
                t = t / np.linalg.norm(t)
                n = np.array([-t[1], t[0]]) * side
                pts.append(sp[i] + n * (L.stream_hw + 0.05))
                nrm.append(n)
            shore_runs(np.array(pts), np.array(nrm))

    # Stone wall runs (rock cards in short segments, wall_i groups like the baker).
    wall_i = 0
    for counts in pc['wallRuns']:
        for seg_count in counts:
            for attempt in range(300):
                c = np.array([rng.uniform(-22, 22), rng.uniform(-22, 22)])
                if not (12 <= F.l1(*c) <= 27):
                    continue
                a = rng.uniform(0, math.pi)
                t = np.array([math.cos(a), math.sin(a)])
                bend = rng.uniform(-0.08, 0.08)
                segs = []
                for k in range(seg_count + 1):
                    ang = a + bend * k
                    segs.append(c + np.array([math.cos(ang), math.sin(ang)]) * (k - seg_count / 2) * 1.1)
                gap = int(rng.integers(1, seg_count))
                if not all(P.ok('rock_02', s[0], s[1], spacing=0.6) and P.path_clear(s[0], s[1]) > 2.0 for s in segs):
                    continue
                for k in range(seg_count):
                    if k == gap and seg_count > 3:
                        wall_i += 1
                        continue
                    a0, a1 = segs[k], segs[k + 1]
                    for j in range(4):
                        q = a0 + (a1 - a0) * (j / 4.0) + rng.uniform(-0.03, 0.03, 2)
                        P.try_add('rock_02' if j % 2 else 'rock_01', q[0], q[1], group='wall_%d' % wall_i, spacing=0.23)
                    wall_i += 1
                break

    # Meadows: clustered flower patches, one dominant colour per patch.
    meadow_pts = np.argwhere(L.meadow)
    rng.shuffle(meadow_pts)
    patches = []
    for r, ccol in meadow_pts[:4000]:
        p = np.array([grid.x[r, ccol], grid.y[r, ccol]])
        if not _visible(p[0], p[1], cfg):
            continue
        if any(np.hypot(*(p - q)) < pc['meadowPatchSpacing'] for q in patches):
            continue
        patches.append(p)
    flowers = ['flower_01', 'flower_02', 'flower_03']
    for p in patches:
        dom = flowers[int(rng.integers(3))]
        for k in range(int(rng.integers(*pc['flowersPerPatch']))):
            q = p + rng.normal(0, 0.55, 2)
            u = rng.random()
            name = dom if u < 0.55 else (flowers[int(rng.integers(3))] if u < 0.68 else ('grass_tuft_01' if u < 0.85 else 'grass_tuft_02'))
            P.try_add(name, q[0], q[1], group='flowers')
    L.meadow_patches = patches

    # Grass tufts along the path borders (gaps, both sides, never on the bridge).
    s = 0.0
    total = path_s[-1]
    while s < total:
        s += rng.uniform(*pc['pathTuftSpacing'])
        i = int(np.searchsorted(path_s, s))
        if i >= len(L.path):
            break
        if L.bridge_s is not None and abs(s - L.bridge_s) < 2.5:
            continue
        t = _tangent(L.path, i)
        n = np.array([-t[1], t[0]]) * (1 if rng.random() < 0.5 else -1)
        q = L.path[i] + n * (L.path_hw[i] + rng.uniform(0.18, 0.4))
        P.try_add('grass_tuft_01' if rng.random() < 0.5 else 'grass_tuft_02', q[0], q[1], group='flowers', allow_path=False)
        if rng.random() < 0.25:
            q2 = q + n * rng.uniform(0.25, 0.5) + t * rng.uniform(-0.3, 0.3)
            P.try_add('grass_tuft_02', q2[0], q2[1], group='flowers')

    # Fill: lone stones, tufts (few, clumped in pairs), keeps open grass open.
    for k in range(pc['fillStones'] * 20):
        if sum(1 for it in P.items if it['group'] == 'stone') >= pc['fillStones'] + 12:
            break
        q = np.array([rng.uniform(-24, 24), rng.uniform(-25, 25)])
        P.try_add('rock_01' if rng.random() < 0.6 else 'rock_02', q[0], q[1], group='stone', spacing=2.5)
    for k in range(pc['fillTufts'] * 6):
        q = np.array([rng.uniform(-24, 24), rng.uniform(-25, 25)])
        if F.value_noise(q[0], q[1], 5.0, L.seed + 51) < 0.55:
            continue
        P.try_add('grass_tuft_01', q[0], q[1], group='flowers', spacing=1.6)
    _prune_density(P, cfg, rng)
    _edge_clusters(P, cfg, L)
    return P.items


def _edge_clusters(P, cfg, L):
    """Append outer-screen clusters without moving or pruning the adopted start composition."""
    settings = cfg['placement'].get('edgeClusters')
    if not settings:
        return
    rng = np.random.default_rng(L.seed + 180003)
    start = cfg['bounds']['startScreen']
    margin_x, margin_y = settings['startMargin']
    w, h = cfg['placement']['densityWindow']

    def add(name, q, group, spacing=None):
        # Includes canopy height and shadow reach, not just the placement's anchor.
        if (abs(q[0] - start['center'][0]) <= start['halfWidth'] + margin_x
                and abs(q[1] - start['center'][1]) <= start['halfDepth'] + margin_y):
            return False
        if not P.ok(name, *q, spacing=spacing):
            return False
        cap = cfg['placement']['densityCap'].get(group)
        if cap is not None:
            pts = np.array([[it['x'], it['y']] for it in P.items if it['group'] == group] + [list(q)])
            # Existing windows already satisfy the cap. Only windows containing q
            # can change; sorted y searches avoid rescanning every existing window.
            lefts = pts[(pts[:, 0] >= q[0] - w) & (pts[:, 0] <= q[0]), 0]
            for left in lefts:
                ys = np.sort(pts[(pts[:, 0] >= left) & (pts[:, 0] <= left + w), 1])
                bottoms = ys[(ys >= q[1] - h) & (ys <= q[1])]
                counts = np.searchsorted(ys, bottoms + h, side='right') - np.searchsorted(ys, bottoms)
                if np.any(counts > cap):
                    return False
        P.add(name, *q, group=group)
        return True

    for index, center in enumerate(settings['centers']):
        center = np.array(center, dtype=float)
        for name, group, count in (('tree_02', 'tree', settings['trees']),
                                   ('boulder_01', 'boulder', settings['boulders']),
                                   ('bush_02', 'bush', settings['bushes']),
                                   ('flower_01' if index % 2 else 'flower_03', 'flowers', settings['flowers'])):
            added = 0
            for attempt in range(count * 30):
                angle = rng.uniform(0, 2 * math.pi)
                radius = settings['radius'] * math.sqrt(rng.random())
                q = center + np.array([math.cos(angle), math.sin(angle)]) * radius
                spacing = cfg['placement']['treeSpacing'] if group == 'tree' else None
                if add(name, q, group, spacing):
                    added += 1
                if added == count:
                    break
        # A short broken stone arc gives each clearing a small recognizable ruin.
        for k in range(7):
            angle = math.pi * (.15 + k / 8)
            q = center + np.array([math.cos(angle), math.sin(angle)]) * (settings['radius'] + .7)
            add('rock_02' if k % 2 else 'rock_01', q, 'wall_edge_%d' % index, .3)


def _window_members(pts, w, h):
    """Indices inside the densest w x h window (window corners anchored at points)."""
    best = []
    for left in pts[:, 0]:
        sel = np.nonzero((pts[:, 0] >= left) & (pts[:, 0] <= left + w))[0]
        order = sel[np.argsort(pts[sel, 1])]
        ys = pts[order, 1]
        j = 0
        for i in range(len(ys)):
            while ys[i] - ys[j] > h:
                j += 1
            if i - j + 1 > len(best):
                best = list(order[j:i + 1])
    return best


def _prune_density(P, cfg, rng):
    w, h = cfg['placement']['densityWindow']
    for group, cap in cfg['placement']['densityCap'].items():
        while True:
            idx = [k for k, it in enumerate(P.items) if it['group'] == group]
            if len(idx) <= cap:
                break
            pts = np.array([[P.items[k]['x'], P.items[k]['y']] for k in idx])
            members = _window_members(pts, w, h)
            if len(members) <= cap:
                break
            drop = sorted((idx[m] for m in rng.choice(members, len(members) - cap, replace=False)), reverse=True)
            for k in drop:
                del P.items[k]
                P.pts = np.delete(P.pts, k, axis=0)
                P.fps = np.delete(P.fps, k)


# ---------------------------------------------------------------- entry

def generate(seed, cfg, grid=None):
    grid = grid or F.Grid(36.0, 8)
    rng = np.random.default_rng(seed)
    L = Layout()
    L.seed = int(seed)
    L.cfg = cfg
    L.grid = grid
    L.path, L.path_ctrl, L.path_attempts = _make_path(rng, cfg)
    s = F.arc_length(L.path)
    pc = cfg['path']
    L.path_hw = _half_width(s, pc['halfWidth'], pc['widthVariation'], seed)
    L.path_dist_grid, L.path_par_grid = F.grid_polyline_distance(grid, L.path, 12.0)
    L.path_hw_grid = np.interp(L.path_par_grid, s, L.path_hw)
    L.path_mask = L.path_dist_grid < L.path_hw_grid
    _make_water(rng, cfg, L, grid)
    _water_fields(L, grid, cfg)
    _bank_fields(rng, L, grid, cfg)
    _make_camp(rng, cfg, L, grid)
    if L.branch is not None:
        L.branch_dist_grid, _ = F.grid_polyline_distance(grid, L.branch, 12.0)
        L.branch_mask = L.branch_dist_grid < pc['branchHalfWidth']
    else:
        L.branch_dist_grid = np.full(grid.x.shape, 99.0)
        L.branch_mask = np.zeros(grid.x.shape, bool)
    _zones(rng, cfg, L, grid)
    L.placements = _place(rng, cfg, L, grid)
    return L
