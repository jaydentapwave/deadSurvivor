#!/usr/bin/env python3
"""046 full map layout.

Our own layout at the reference map's scale and density (Verdant Meadows: ~1000x1000 px, ~70 small clusters, stone
boundary, roads, dirt patches). Coordinates are not traced from the reference. The key-art start area (OB-001..006 and
the road through it) is kept from Layout045.

Writes (overwrites its own outputs):
  Assets/DeadSurvivor/Scenes/Map046/Layout046.json   runtime layout (DSBlockoutData + "kind" per object)
  Assets/DeadSurvivor/Scenes/Map046/Props046.json    visual placements for Tools/Map/build_map_046.cs
  output/map-full-046/layout046.png                  top-down preview
Usage: /usr/bin/python3 Tools/Map/layout_046.py [seed]
"""
import json, math, random, sys
from collections import deque
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'Assets/DeadSurvivor/Scenes/Proto045/Layout045.json'
OUT = ROOT / 'Assets/DeadSurvivor/Scenes/Map046'
PREVIEW = ROOT / 'output/map-full-046'

START = (280.0, 300.0)
KEYART = (95, 95, 525, 405)          # key-art start area (kept as is)
GAP = 40                              # min free gap between obstacles, px (line formation ~40 px wide)
EDGE_GAP = 52                         # min gap obstacle - boundary
ROAD_W = 20                           # road width, px
ROAD_CLEAR = ROAD_W / 2 + 14          # min distance obstacle - road centre line
CLEAR_R = 6.5                         # largest monster radius (25 logical) in px, for the connectivity check
TREE_LOW_R = 13.9                     # tree045 radius of the part <= 1.0 m, px at scale 1
BOULDER_R = 6.75                      # boulder045 radius, px at scale 1


# ---------------------------------------------------------------- geometry
def edges(p):
    return [(p[i], p[(i + 1) % len(p)]) for i in range(len(p))]


def cross(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def seg_x(a, b, c, d):
    x, y, z, w = cross(a, b, c), cross(a, b, d), cross(c, d, a), cross(c, d, b)
    return ((x < 0 < y) or (x > 0 > y)) and ((z < 0 < w) or (z > 0 > w))


def simple(p):
    E = edges(p)
    for i in range(len(E)):
        for j in range(i):
            if j == i - 1 or (j == 0 and i == len(E) - 1):
                continue
            if seg_x(*E[i], *E[j]):
                return False
    return len(set(p)) == len(p) and abs(sum(a[0] * b[1] - a[1] * b[0] for a, b in E)) > 1


def inside(pt, poly):
    r = False
    for a, b in edges(poly):
        if (a[1] > pt[1]) != (b[1] > pt[1]) and pt[0] < (b[0] - a[0]) * (pt[1] - a[1]) / (b[1] - a[1]) + a[0]:
            r = not r
    return r


def pt_seg(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    n = dx * dx + dy * dy
    t = 0 if n == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / n))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)


def edge_dist(p, poly):
    return min(pt_seg(p, a, b) for a, b in edges(poly))


def bbox(p, m=0):
    xs, ys = [q[0] for q in p], [q[1] for q in p]
    return (min(xs) - m, min(ys) - m, max(xs) + m, max(ys) + m)


def bb_hit(a, b):
    return a[0] <= b[2] and b[0] <= a[2] and a[1] <= b[3] and b[1] <= a[3]


def poly_gap(p, q):
    """0 if overlapping, else the minimum distance between the outlines."""
    if any(seg_x(*e, *f) for e in edges(p) for f in edges(q)) or inside(p[0], q) or inside(q[0], p):
        return 0.0
    return min(min(edge_dist(v, q) for v in p), min(edge_dist(v, p) for v in q))


def polyline_dist(poly, line):
    return min(min(pt_seg(v, line[i], line[i + 1]) for i in range(len(line) - 1)) for v in poly)


def centroid(p):
    return (sum(q[0] for q in p) / len(p), sum(q[1] for q in p) / len(p))


def rot(pts, c, deg):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    return [(c[0] + (x - c[0]) * ca - (y - c[1]) * sa, c[1] + (x - c[0]) * sa + (y - c[1]) * ca) for x, y in pts]


def rnd(p):
    return [(round(x, 2), round(y, 2)) for x, y in p]


# ---------------------------------------------------------------- shapes
def ellipse(c, rx, ry, n=16):
    return [(c[0] + rx * math.cos(2 * math.pi * i / n), c[1] + ry * math.sin(2 * math.pi * i / n)) for i in range(n)]


def half(c, rx, ry, n=13):
    return [(c[0] + rx * math.cos(math.pi + math.pi * i / (n - 1)), c[1] + ry * math.sin(math.pi + math.pi * i / (n - 1))) for i in range(n)]


def capsule(c, length, w, deg, k=5):
    r, h = w / 2, max(0.0, length / 2 - w / 2)
    pts = [(c[0] + h + r * math.cos(-math.pi / 2 + math.pi * i / k), c[1] + r * math.sin(-math.pi / 2 + math.pi * i / k)) for i in range(k + 1)]
    pts += [(c[0] - h + r * math.cos(math.pi / 2 + math.pi * i / k), c[1] + r * math.sin(math.pi / 2 + math.pi * i / k)) for i in range(k + 1)]
    return rot(pts, c, deg)


def blob(c, r, rng, n=12, j=0.2, sy=1.0):
    ph = [rng.uniform(0, 6.3) for _ in range(3)]
    out = []
    for i in range(n):
        a = 2 * math.pi * i / n
        k = 1 + j * (0.6 * math.sin(2 * a + ph[0]) + 0.3 * math.sin(3 * a + ph[1]) + 0.25 * math.sin(5 * a + ph[2]))
        out.append((c[0] + r * k * math.cos(a), c[1] + r * k * sy * math.sin(a)))
    return out


def thick_arc(c, R, t, a0, a1, step=7.5):
    n = max(2, int(abs(a1 - a0) / step) + 1)
    angs = [math.radians(a0 + (a1 - a0) * i / (n - 1)) for i in range(n)]
    outer = [(c[0] + (R + t / 2) * math.cos(a), c[1] + (R + t / 2) * math.sin(a)) for a in angs]
    inner = [(c[0] + (R - t / 2) * math.cos(a), c[1] + (R - t / 2) * math.sin(a)) for a in reversed(angs)]
    return outer + inner


def pillar(c, s, deg):
    k = s * 0.32
    pts = [(-s / 2 + k, -s / 2), (s / 2 - k, -s / 2), (s / 2, -s / 2 + k), (s / 2, s / 2 - k),
           (s / 2 - k, s / 2), (-s / 2 + k, s / 2), (-s / 2, s / 2 - k), (-s / 2, -s / 2 + k)]
    return rot([(c[0] + x, c[1] + y) for x, y in pts], c, deg)


# ---------------------------------------------------------------- map parts
def boundary(rng):
    cx, cy, hx, hy, ex, n = 595.0, 558.0, 508.0, 494.0, 4.6, 168
    ph = [rng.uniform(0, 2 * math.pi) for _ in range(4)]
    feats = [(0.30, -0.065, 0.13), (2.45, 0.035, 0.22), (4.95, -0.045, 0.10), (0.95, -0.03, 0.16), (3.55, 0.025, 0.18)]
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        c, s = math.cos(t), math.sin(t)
        x, y = hx * math.copysign(abs(c) ** (2 / ex), c), hy * math.copysign(abs(s) ** (2 / ex), s)
        k = 1 + 0.022 * math.sin(3 * t + ph[0]) + 0.016 * math.sin(5 * t + ph[1]) + 0.012 * math.sin(9 * t + ph[2]) + 0.008 * math.sin(17 * t + ph[3])
        for at, amp, w in feats:
            d = (t - at + math.pi) % (2 * math.pi) - math.pi
            k += amp * math.exp(-(d / w) ** 2)
        pts.append((cx + x * k, cy + y * k))
    return rnd(pts)


def catmull(pts, step=6.0):
    p = [pts[0]] + pts + [pts[-1]]
    out = []
    for i in range(1, len(p) - 2):
        a, b, c, d = p[i - 1], p[i], p[i + 1], p[i + 2]
        n = max(1, int(math.dist(b, c) / step))
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * b[j]) + (-a[j] + c[j]) * t + (2 * a[j] - 5 * b[j] + 4 * c[j] - d[j]) * t2 + (-a[j] + 3 * b[j] - 3 * c[j] + d[j]) * t3) for j in (0, 1)))
    out.append(pts[-1])
    return rnd(out)


ROADS = {
    'RD-1': [(30, 349.09), (100, 349.09), (146.67, 350.87), (413.33, 361.04), (520, 365.11), (640, 345), (760, 308), (900, 290), (1040, 300), (1170, 312)],
    'RD-2': [(606, 352), (615, 425), (588, 505), (528, 575), (505, 650), (540, 718), (640, 768), (790, 792), (930, 772), (1060, 742), (1180, 736)],
    'RD-3': [(512, 680), (452, 745), (418, 830), (408, 930), (415, 1120)],
}

REGION_KINDS = {
    'north': [('ring', .42), ('arc', .25), ('boulder', .33)],
    'center': [('ring', .42), ('arc', .23), ('boulder', .17), ('row', .18)],
    'east': [('row', .55), ('ring', .25), ('boulder', .20)],
    'south': [('boulder', .55), ('ring', .25), ('grove', .20)],
    'grove': [('grove', .85), ('boulder', .15)],
    'ruins': [('boulder', .45), ('ring', .30), ('row', .25)],
}
SPACING = {'north': 84, 'center': 88, 'east': 84, 'south': 82, 'grove': 74, 'ruins': 86}
HEIGHT = {'ring': 16, 'arc': 16, 'row': 24, 'boulder': 24, 'grove': 32, 'lake': 16, 'pond': 16, 'ruin': 24, 'pillar': 32, 'altar': 24, 'keyart': 16}
RUINS_C, RUINS_R, RUINS_T = (262.0, 796.0), 92.0, 17.0


def region(p):
    x, y = p
    if KEYART[0] <= x <= KEYART[2] and KEYART[1] <= y <= KEYART[3]:
        return 'keyart'
    if x < 420 and y > 620:
        return 'ruins'
    if x >= 745 and y >= 800:
        return 'grove'
    if 400 <= x < 745 and y >= 820:
        return 'south'
    if x >= 780 and 330 <= y < 800:
        return 'east'
    if y < 330:
        return 'north'
    return 'center'


def make(kind, c, rng, reg):
    if kind == 'ring':
        rx = rng.uniform(19, 32)
        return ellipse(c, rx, rx * rng.uniform(.68, .8))
    if kind == 'arc':
        rx = rng.uniform(32, 44)
        return rot(half(c, rx, rx * rng.uniform(.6, .7)), c, rng.choice([0, 180]) + rng.uniform(-35, 35))
    if kind == 'row':
        deg = -35 + rng.uniform(-12, 12) if reg == 'east' else rng.uniform(0, 180)
        return capsule(c, rng.uniform(56, 96), rng.uniform(17, 23), deg)
    if kind == 'boulder':
        return blob(c, rng.uniform(13, 22), rng, 10, .18)
    if kind == 'grove':
        return blob(c, rng.uniform(22, 34), rng, 14, .16)
    raise ValueError(kind)


# ---------------------------------------------------------------- raster checks
class Raster:
    def __init__(self, bnd, step=4.0):
        x0, y0, x1, y1 = bbox(bnd, 8)
        self.xs, self.ys = np.arange(x0, x1, step), np.arange(y0, y1, step)
        X, Y = np.meshgrid(self.xs, self.ys)
        self.X, self.Y, self.step = X, Y, step
        self.free = self._inside(bnd) & (self._dist(bnd) >= CLEAR_R)

    def _inside(self, poly):
        X, Y = self.X, self.Y
        r = np.zeros(X.shape, bool)
        for (ax, ay), (bx, by) in edges(poly):
            if ay == by:
                continue
            m = (ay > Y) != (by > Y)
            xi = (bx - ax) * (Y - ay) / (by - ay) + ax
            r ^= m & (X < xi)
        return r

    def _dist(self, poly, sub=None):
        X, Y = (self.X, self.Y) if sub is None else sub
        d = np.full(X.shape, np.inf)
        for (ax, ay), (bx, by) in edges(poly):
            dx, dy = bx - ax, by - ay
            n = dx * dx + dy * dy
            t = np.clip(((X - ax) * dx + (Y - ay) * dy) / n, 0, 1) if n else 0
            d = np.minimum(d, np.hypot(X - ax - t * dx, Y - ay - t * dy))
        return d

    def carve(self, poly):
        x0, y0, x1, y1 = bbox(poly, CLEAR_R + self.step)
        i0, i1 = np.searchsorted(self.ys, y0), np.searchsorted(self.ys, y1)
        j0, j1 = np.searchsorted(self.xs, x0), np.searchsorted(self.xs, x1)
        X, Y = self.X[i0:i1, j0:j1], self.Y[i0:i1, j0:j1]
        ins = np.zeros(X.shape, bool)
        for (ax, ay), (bx, by) in edges(poly):
            if ay == by:
                continue
            m = (ay > Y) != (by > Y)
            ins ^= m & (X < (bx - ax) * (Y - ay) / (by - ay) + ax)
        self.free[i0:i1, j0:j1] &= ~ins & (self._dist(poly, (X, Y)) >= CLEAR_R)

    def components(self, start):
        free = self.free
        h, w = free.shape
        lab = np.full((h, w), -1, int)
        sizes = []
        si, sj = int(np.argmin(abs(self.ys - start[1]))), int(np.argmin(abs(self.xs - start[0])))
        order = [(si, sj)] + [(i, j) for i, j in zip(*np.nonzero(free))]
        for i, j in order:
            if not free[i, j] or lab[i, j] >= 0:
                continue
            k = len(sizes)
            q = deque([(i, j)])
            lab[i, j] = k
            n = 0
            while q:
                a, b = q.popleft()
                n += 1
                for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    u, v = a + da, b + db
                    if 0 <= u < h and 0 <= v < w and free[u, v] and lab[u, v] < 0:
                        lab[u, v] = k
                        q.append((u, v))
            sizes.append(n)
        return lab, sizes


# ---------------------------------------------------------------- props
def fit_points(poly, rng, radius, count, spacing, tries=400, prefer=None):
    x0, y0, x1, y1 = bbox(poly)
    out = []
    for _ in range(tries):
        if len(out) >= count:
            break
        p = (rng.uniform(x0, x1), rng.uniform(y0, y1))
        if prefer and not prefer(p):
            continue
        if not inside(p, poly) or edge_dist(p, poly) < radius + 1:
            continue
        if any(math.dist(p, q) < spacing for q in out):
            continue
        out.append(p)
    return out


def pole(poly):
    """Interior point with the largest distance to the outline (grid search)."""
    x0, y0, x1, y1 = bbox(poly)
    best, bd = centroid(poly), -1.0
    for i in range(21):
        for j in range(21):
            p = (x0 + (x1 - x0) * i / 20, y0 + (y1 - y0) * j / 20)
            if inside(p, poly):
                d = edge_dist(p, poly)
                if d > bd:
                    best, bd = p, d
    return best, bd


def props_for(o, rng):
    kind, poly = o['kind'], o['poly']
    out = []
    if kind in ('ring', 'arc', 'keyart_ring'):
        c, d = pole(poly)
        r = min(d - 8.5, 13.5)
        if r >= 5:
            out.append({'model': 'bush', 'x': c[0], 'y': c[1], 'r': round(r * .08, 3), 'occluder': True})
    elif kind == 'row':
        c, d = pole(poly)
        if d - 7 >= BOULDER_R * .7 and rng.random() < .55:
            out.append({'model': 'boulder045', 'x': c[0], 'y': c[1], 's': round(min(1.1, (d - 7) / BOULDER_R), 3), 'yaw': rng.uniform(0, 360), 'occluder': True})
    elif kind in ('boulder', 'altar'):
        c, d = pole(poly)
        s = min(2.2, (d - 1) / BOULDER_R)
        out.append({'model': 'boulder045', 'x': c[0], 'y': c[1], 's': round(s, 3), 'yaw': rng.uniform(0, 360), 'occluder': True})
        for _ in range(rng.randint(0, 2)):
            s2 = rng.uniform(.5, .85)
            pts = fit_points(poly, rng, BOULDER_R * s2, 1, 0)
            if pts and math.dist(pts[0], c) > BOULDER_R * (s + s2) * .8:
                out.append({'model': 'boulder045', 'x': pts[0][0], 'y': pts[0][1], 's': round(s2, 3), 'yaw': rng.uniform(0, 360), 'occluder': True})
    elif kind == 'grove':
        area = abs(sum(a[0] * b[1] - a[1] * b[0] for a, b in edges(poly))) / 2
        n = max(1, min(5, int(area / 900)))
        placed = []
        for _ in range(n * 3):
            if len(placed) >= n:
                break
            s = rng.uniform(.62, .86)
            pts = fit_points(poly, rng, TREE_LOW_R * s, 1, 0)
            if pts and all(math.dist(pts[0], q) > 12 for q in placed):
                placed.append(pts[0])
                out.append({'model': 'tree045', 'x': pts[0][0], 'y': pts[0][1], 's': round(s, 3), 'yaw': rng.uniform(0, 360), 'occluder': True})
        if not placed:
            c, d = pole(poly)
            out.append({'model': 'tree045', 'x': c[0], 'y': c[1], 's': round((d - 1) / TREE_LOW_R, 3), 'yaw': rng.uniform(0, 360), 'occluder': True})
        for p in fit_points(poly, rng, 5.5, rng.randint(1, 3), 9, prefer=lambda p: edge_dist(p, poly) < 11):
            out.append({'model': 'bush', 'x': p[0], 'y': p[1], 'r': round(rng.uniform(.34, .44), 3), 'occluder': True})
    elif kind in ('lake', 'pond'):
        c = centroid(poly)
        north = lambda p: p[1] < c[1] - 4
        nb = 4 if kind == 'lake' else 2
        for p in fit_points(poly, rng, BOULDER_R * 1.1, nb, 26, prefer=lambda p: north(p) and edge_dist(p, poly) < 13):
            out.append({'model': 'boulder045', 'x': p[0], 'y': p[1], 's': round(rng.uniform(.8, 1.15), 3), 'yaw': rng.uniform(0, 360), 'occluder': True})
        nt = 2 if kind == 'lake' else 1
        for p in fit_points(poly, rng, TREE_LOW_R * .8, nt, 40, prefer=lambda p: north(p) and edge_dist(p, poly) < 15):
            out.append({'model': 'tree045', 'x': p[0], 'y': p[1], 's': .78, 'yaw': rng.uniform(0, 360), 'occluder': True})
    return out


def outside_props(bnd, rng):
    """Trees / bush domes outside the boundary. Row 1 may occlude (registered); rows 2+ are combined per chunk."""
    out = []
    E = edges(bnd)
    per = sum(math.dist(a, b) for a, b in E)
    c = centroid(bnd)

    def at(d):
        acc = 0
        for a, b in E:
            L = math.dist(a, b)
            if acc + L >= d:
                t = (d - acc) / L
                p = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
                nx, ny = -(b[1] - a[1]) / L, (b[0] - a[0]) / L
                if (p[0] + nx - c[0]) ** 2 + (p[1] + ny - c[1]) ** 2 < (p[0] - c[0]) ** 2 + (p[1] - c[1]) ** 2:
                    nx, ny = -nx, -ny
                return p, (nx, ny)
            acc += L
        return E[-1][1], (0, 1)

    for row, (off0, off1, step, tree_p) in enumerate([(4, 12, 30, 1.0), (40, 66, 26, 0.0), (80, 125, 28, 0.0)]):
        d = rng.uniform(0, step)
        while d < per:
            p, n = at(d)
            d += step * rng.uniform(.8, 1.2)
            tree = rng.random() < tree_p
            s = rng.uniform(.8, 1.08) if tree else 0
            r = rng.uniform(.9, 1.5)
            foot = TREE_LOW_R * s if tree else r / .08
            q = (p[0] + n[0] * (foot + rng.uniform(off0, off1)), p[1] + n[1] * (foot + rng.uniform(off0, off1)))
            if inside(q, bnd) or edge_dist(q, bnd) < foot + 2:
                continue
            item = {'model': 'tree045', 's': round(s, 3), 'yaw': rng.uniform(0, 360)} if tree else {'model': 'bush', 'r': round(r, 3)}
            item.update({'x': round(q[0], 2), 'y': round(q[1], 2), 'occluder': row == 0, 'outside': True})
            out.append(item)
    return out


def dirt_patches(bnd, obstacles, roads, rng, count=175):
    out = []
    x0, y0, x1, y1 = bbox(bnd)
    for _ in range(6000):
        if len(out) >= count:
            break
        c = (rng.uniform(x0, x1), rng.uniform(y0, y1))
        if not inside(c, bnd) or edge_dist(c, bnd) < 30:
            continue
        rx = rng.uniform(9, 26)
        near = min(math.dist(c, centroid(o['poly'])) for o in obstacles)
        if near > 70 and rng.random() < .55:
            continue
        if any(inside(c, o['poly']) for o in obstacles):
            continue
        if min(min(pt_seg(c, l[i], l[i + 1]) for i in range(len(l) - 1)) for l in roads.values()) < ROAD_W / 2 + rx * .6:
            continue
        if any(math.dist(c, (p['x'], p['y'])) < (rx + p['rx']) * .9 for p in out):
            continue
        out.append({'x': round(c[0], 2), 'y': round(c[1], 2), 'rx': round(rx, 2), 'ry': round(rx * rng.uniform(.55, .85), 2), 'rot': round(rng.uniform(0, 180), 1)})
    return out


# ---------------------------------------------------------------- main
def main(seed=4601):
    rng = random.Random(seed)
    src = json.loads(SRC.read_text(encoding='utf-8'))
    bnd = boundary(rng)
    assert simple(bnd), 'boundary not simple'
    roads = {k: catmull(v) for k, v in ROADS.items()}
    objs = []

    def add(kind, poly, anchor=True, keep_id=None):
        objs.append({'id': keep_id, 'kind': kind, 'poly': rnd(poly), 'anchor': anchor})

    # key-art start area (kept)
    for o in src['objects']:
        if o['id'] <= 'OB-006':
            add('pond' if o['id'] == 'OB-006' else 'keyart_ring', [(p['x'], p['y']) for p in o['points_px']], keep_id=o['id'])
    add('lake', blob((880, 218), 112, rng, 24, .09, sy=.5))
    add('pond', blob((782, 560), 50, rng, 18, .1, sy=.66))
    gaps = [-140, -60, 15, 95, 165]
    for i, g in enumerate(gaps):
        a0, a1 = g + 15, (gaps[(i + 1) % len(gaps)] + (360 if i == len(gaps) - 1 else 0)) - 15
        add('ruin', thick_arc(RUINS_C, RUINS_R, RUINS_T, a0, a1))
    add('altar', blob(RUINS_C, 16, rng, 10, .12))
    for c, deg in (((388, 690), 20), ((398, 915), -15), ((190, 664), 40)):
        add('pillar', pillar(c, 15, deg))

    problems = []
    for o in objs:
        p = o['poly']
        if not simple(p):
            problems.append(f"anchor {o['kind']} not simple")
        if not all(inside(v, bnd) for v in p) or min(edge_dist(v, bnd) for v in p) < (EDGE_GAP if o['kind'] not in ('keyart_ring', 'pond') else 20):
            problems.append(f"anchor {o['kind']} too close to boundary: {min(edge_dist(v, bnd) for v in p):.1f}")

    # fill
    for _ in range(80000):
        if len(objs) >= 90:
            break
        c = (rng.uniform(90, 1100), rng.uniform(70, 1050))
        reg = region(c)
        if reg == 'keyart' or not inside(c, bnd):
            continue
        if math.dist(c, RUINS_C) < RUINS_R + RUINS_T / 2 + GAP + 22:
            continue
        if any(math.dist(c, centroid(o['poly'])) < SPACING[reg] * (.75 if o['kind'] in ('ruin', 'pillar') else 1) for o in objs if o['kind'] not in ('lake',)):
            continue
        r_, acc = rng.random(), 0
        for kind, w in REGION_KINDS[reg]:
            acc += w
            if r_ <= acc:
                break
        p = make(kind, c, rng, reg)
        if not simple(p) or not all(inside(v, bnd) for v in p) or min(edge_dist(v, bnd) for v in p) < EDGE_GAP:
            continue
        bb = bbox(p)
        if bb_hit(bb, (KEYART[0] - 30, KEYART[1] - 30, KEYART[2] + 30, KEYART[3] + 30)):
            continue
        if any(polyline_dist(p, l) < ROAD_CLEAR for l in roads.values()):
            continue
        if any(bb_hit(bbox(p, GAP), bbox(o['poly'])) and poly_gap(p, o['poly']) < GAP for o in objs):
            continue
        add(kind, p, anchor=False)

    # connectivity (largest monster radius): remove fill objects that close off pockets
    removed = []
    for it in range(12):
        ras = Raster(bnd)
        for o in objs:
            ras.carve(o['poly'])
        lab, sizes = ras.components(START)
        pockets = [(k, n) for k, n in enumerate(sizes) if k != 0 and n * ras.step ** 2 > 60]
        if not pockets:
            break
        k, n = max(pockets, key=lambda kn: kn[1])
        ii, jj = np.nonzero(lab == k)
        pc = (float(ras.xs[jj].mean()), float(ras.ys[ii].mean()))
        cand = [o for o in objs if not o['anchor']]
        if not cand:
            problems.append(f'pocket at {pc} ({n * ras.step ** 2:.0f} px2) next to anchors only')
            break
        victim = min(cand, key=lambda o: math.dist(centroid(o['poly']), pc))
        objs.remove(victim)
        removed.append({'kind': victim['kind'], 'at': rnd([centroid(victim['poly'])])[0], 'pocket_px2': n * ras.step ** 2})
    free_area = int(ras.free.sum() * ras.step ** 2)
    reach = int(sizes[0] * ras.step ** 2) if sizes else 0

    # ids: key-art ids kept, the rest numbered from OB-007 in reading order
    rest = sorted([o for o in objs if not o['id']], key=lambda o: (round(centroid(o['poly'])[1] / 80), centroid(o['poly'])[0]))
    for i, o in enumerate(rest):
        o['id'] = f'OB-{i + 7:03d}'
    objs.sort(key=lambda o: o['id'])
    assert len(objs) <= 120, len(objs)

    props = []
    prng = random.Random(seed + 1)
    for o in objs:
        for p in props_for(o, prng):
            p['group'] = o['id']
            props.append(p)
    props += outside_props(bnd, prng)
    dirt = dirt_patches(bnd, objs, roads, prng)
    for p in props:
        for k in ('x', 'y', 'yaw'):
            if k in p:
                p[k] = round(p[k], 2)

    layout = {
        'package': 'MAP-FULL-046', 'revision': 'r001', 'document_role': 'GAME_MAP',
        'source_layout_sha256': src['source_layout_sha256'], 'source_image_sha256': src['source_image_sha256'],
        'origin_px': src['origin_px'], 'boundary_px': [{'x': x, 'y': y} for x, y in bnd],
        'objects': [{'id': o['id'], 'kind': o['kind'], 'points_px': [{'x': x, 'y': y} for x, y in o['poly']], 'height_logical': HEIGHT[o['kind'].replace('keyart_ring', 'keyart')]} for o in objs],
        'routes': [{'id': k, 'points_px': [{'x': x, 'y': y} for x, y in v]} for k, v in ROADS.items()],
        'rock_object_id': 'OB-001', 'rock_center_px': src['rock_center_px'], 'scale_profiles': [4, 6, 8],
        'world_scale': src['world_scale'], 'build_scale': src['build_scale'], 'body_radius': src['body_radius'],
        'body_height': src['body_height'], 'epsilon': src['epsilon'],
    }
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'Layout046.json').write_text(json.dumps(layout, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    props_doc = {'seed': seed, 'start_px': START, 'road_width_px': ROAD_W, 'roads': {k: [{'x': x, 'y': y} for x, y in v] for k, v in roads.items()},
                 'dirt': dirt, 'props': props}
    (OUT / 'Props046.json').write_text(json.dumps(props_doc, ensure_ascii=False) + '\n', encoding='utf-8')
    kinds = {}
    for o in objs:
        kinds[o['kind']] = kinds.get(o['kind'], 0) + 1
    report = {'objects': len(objs), 'kinds': kinds, 'props': len(props),
              'props_by_model': {m: sum(1 for p in props if p['model'] == m) for m in ('tree045', 'boulder045', 'bush')},
              'dirt': len(dirt), 'boundary_bbox': [round(v) for v in bbox(bnd)], 'free_px2': free_area, 'reachable_px2': reach,
              'unreachable_px2': free_area - reach, 'removed_for_pockets': removed, 'problems': problems}
    PREVIEW.mkdir(parents=True, exist_ok=True)
    draw(bnd, objs, roads, dirt, props, PREVIEW / 'layout046.png')
    (PREVIEW / 'layout-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))
    return 0 if not problems else 1


COLORS = {'keyart_ring': (240, 200, 70), 'ring': (150, 170, 150), 'arc': (150, 170, 150), 'row': (170, 160, 150), 'boulder': (140, 140, 150),
          'grove': (40, 110, 50), 'lake': (80, 170, 230), 'pond': (80, 170, 230), 'ruin': (200, 180, 150), 'pillar': (200, 180, 150), 'altar': (140, 140, 150)}


def draw(bnd, objs, roads, dirt, props, path):
    from PIL import Image, ImageDraw
    Z = 1
    im = Image.new('RGB', (1200, 1120), (30, 50, 32))
    d = ImageDraw.Draw(im, 'RGBA')
    d.polygon(bnd, fill=(88, 150, 70, 255), outline=(200, 200, 200, 255))
    for p in dirt:
        d.ellipse((p['x'] - p['rx'], p['y'] - p['ry'], p['x'] + p['rx'], p['y'] + p['ry']), fill=(190, 160, 110, 200))
    for l in roads.values():
        d.line(l, fill=(150, 150, 140, 255), width=ROAD_W, joint='curve')
    for o in objs:
        d.polygon(o['poly'], fill=COLORS[o['kind']] + (230,), outline=(20, 20, 20, 255))
    for p in props:
        r = TREE_LOW_R * p['s'] if p['model'] == 'tree045' else BOULDER_R * p['s'] if p['model'] == 'boulder045' else p['r'] / .08
        col = (30, 90, 40, 220) if p['model'] == 'tree045' else (110, 110, 120, 230) if p['model'] == 'boulder045' else (70, 140, 60, 230)
        d.ellipse((p['x'] - r, p['y'] - r, p['x'] + r, p['y'] + r), fill=col, outline=(0, 0, 0, 120))
    d.ellipse((START[0] - 5, START[1] - 5, START[0] + 5, START[1] + 5), fill=(255, 255, 255, 255))
    with open(path, 'wb') as f:
        im.save(f, format='PNG')


if __name__ == '__main__':
    sys.exit(main(int(sys.argv[1]) if len(sys.argv) > 1 else 4601))
