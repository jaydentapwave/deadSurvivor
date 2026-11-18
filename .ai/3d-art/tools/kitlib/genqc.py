"""Generation quality checks: numbers from the concept images (before paying) and from the generated GLB (right after fetch).
No Blender, no Unity, no network: numpy + Pillow only. A check that needs a declared expectation only runs when the catalog
gives it (tripo.expect: {"feet": 4, "top_round": true}). Thresholds were set on archived good and bad assets (docs/plan/005)."""
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image

VIEWS = ('front', 'left', 'back', 'right')


# ---------------- input images ----------------
def fg_mask(rgb):
    """foreground = differs from the background colour taken at the four corners"""
    h, w, _ = rgb.shape
    k = max(4, min(h, w) // 40)
    bg = np.median(np.concatenate([rgb[:k, :k].reshape(-1, 3), rgb[:k, -k:].reshape(-1, 3), rgb[-k:, :k].reshape(-1, 3), rgb[-k:, -k:].reshape(-1, 3)]), axis=0)
    return np.abs(rgb.astype(float) - bg).max(axis=2) > 24


def _load(path, size=256):
    return np.asarray(Image.open(path).convert('RGB').resize((size, size), Image.BILINEAR))


def _iou(a, b):
    u = (a | b).sum()
    return float((a & b).sum() / u) if u else 0.0


def foot_blobs(mask):
    """feet = bumps of the silhouette's bottom contour. For each column the lowest foreground row; a bump is a run that reaches
    lower than both its surroundings by at least 3% of the height. Rear feet drawn higher in a perspective view count too.
    Returns [(x0, x1, lowest_row)]."""
    ys, xs = np.nonzero(mask)
    top, bot = ys.min(), ys.max()
    prom = max(3, int(0.03 * (bot - top)))
    cols = np.nonzero(mask.any(axis=0))[0]
    low = np.array([np.nonzero(mask[:, x])[0].max() for x in cols])
    out = []
    i = 0
    n = len(cols)
    while i < n - 1:
        j = i
        while j + 1 < n and low[j + 1] >= low[j]:      # go down to a local lowest point
            j += 1
        k = j
        while k + 1 < n and low[k + 1] == low[j]:
            k += 1
        left = low[max(0, i):j + 1].min() if j > i else low[j] - prom - 1   # the silhouette edge counts as open air
        e = k
        while e + 1 < n and low[e + 1] <= low[e]:
            e += 1
        right = low[k:e + 1].min() if e > k else low[k] - prom - 1
        if low[j] - left >= prom and low[j] - right >= prom:
            out.append((int(cols[j]), int(cols[k]), int(low[j])))
        i = max(e, j + 1)
    return out


def check_input(paths):
    """paths in view order front, left, back, right. Returns dict(views, measures, fails). Single image: nothing to compare."""
    paths = [Path(p) for p in paths]
    m = dict(views=[VIEWS[i] for i in range(len(paths))])
    fails = []
    if len(paths) < 2:
        return dict(measures=m, fails=fails, note='single image: no view comparison')
    rgbs = [_load(p) for p in paths]
    masks = [fg_mask(r) for r in rgbs]
    empty = [v for v, mk in zip(m['views'], masks) if not mk.any()]
    if empty:
        return dict(measures=m, fails=[f'INPUT_EMPTY_VIEW {",".join(empty)} (nothing differs from the corner background colour)'])
    box = []
    for mk in masks:
        ys, xs = np.nonzero(mk)
        box.append((int(ys.min()), int(ys.max())))
    hs = [b - t for t, b in box]
    h0 = max(hs)
    m['height_px'] = hs
    m['top_row'] = [t for t, _ in box]
    m['bottom_row'] = [b for _, b in box]
    spread = (max(hs) - min(hs)) / h0
    m['height_spread'] = round(spread, 3)
    if spread > 0.05:
        fails.append(f'INPUT_HEIGHT_DIFFERS {spread:.3f} (views are not the same size)')
    base = (max(b for _, b in box) - min(b for _, b in box)) / h0
    m['ground_line_spread'] = round(base, 3)
    if base > 0.04:
        fails.append(f'INPUT_GROUND_LINE_DIFFERS {base:.3f}')
    # only bumps in the lowest 22% can be feet: a belly or hanging hands sit higher. Tight: drawn rear feet 0.21, hero hands 0.24
    blobs = [[x for x in foot_blobs(mk) if x[2] >= bx[1] - 0.22 * h] for mk, bx, h in zip(masks, box, hs)]
    m['foot_blobs'] = [len(b) for b in blobs]
    for name, b, h in zip(m['views'], blobs, hs):
        if len(b) >= 2:
            s = (max(x[2] for x in b) - min(x[2] for x in b)) / h
            m.setdefault('foot_level_spread', {})[name] = round(s, 3)
            if s > 0.03:
                fails.append(f'INPUT_FEET_NOT_LEVEL {name} {s:.3f} (perspective view: nearer feet lower; Tripo reads extra legs)')
    pairs = [(0, 2, 'front_back')] + ([(1, 3, 'left_right')] if len(paths) > 3 else [])
    for a, b, name in pairs:
        if b >= len(masks):
            continue
        iou = _iou(masks[a], masks[b][:, ::-1])
        m[f'{name}_mirror_iou'] = round(iou, 3)
        if iou < 0.86:
            fails.append(f'INPUT_MIRROR_MISMATCH {name} {iou:.3f} (back must be the mirrored front silhouette)')
    return dict(measures=m, fails=fails)


# ---------------- generated model ----------------
def load_glb(path):
    """positions and triangle indices of every primitive, in one array each"""
    with open(path, 'rb') as f:
        f.read(12)
        n = struct.unpack('<I', f.read(4))[0]
        f.read(4)
        g = json.loads(f.read(n))
        bn = struct.unpack('<I', f.read(4))[0]
        f.read(4)
        blob = f.read(bn)

    def acc(i):
        a = g['accessors'][i]
        v = g['bufferViews'][a['bufferView']]
        comp = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a['componentType']]
        cnt = {'SCALAR': 1, 'VEC3': 3}[a['type']]
        o = v.get('byteOffset', 0) + a.get('byteOffset', 0)
        return np.frombuffer(blob, comp, a['count'] * cnt, o).reshape(-1, cnt).astype(float if comp is np.float32 else int)

    P, I = [], []
    base = 0
    for mesh in g['meshes']:
        for pr in mesh['primitives']:
            p = acc(pr['attributes']['POSITION'])
            P.append(p)
            I.append(acc(pr['indices']).reshape(-1) + base)
            base += len(p)
    return np.concatenate(P), np.concatenate(I).reshape(-1, 3)


def _weld(P, I):
    key = np.round(P / 1e-4).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    return inv.reshape(-1)[I], inv.max() + 1


def islands(P, I):
    """share of surface area in each welded component, largest first"""
    W, n = _weld(P, I)
    parent = np.arange(n)

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for a, b, c in W:
        ra, rb, rc = find(a), find(b), find(c)
        parent[rb] = ra
        parent[find(rc)] = ra
    roots = np.array([find(x) for x in W[:, 0]])
    area = 0.5 * np.linalg.norm(np.cross(P[I[:, 1]] - P[I[:, 0]], P[I[:, 2]] - P[I[:, 0]]), axis=1)
    tot = area.sum()
    return sorted((float(area[roots == r].sum() / tot) for r in np.unique(roots)), reverse=True)


def surface_samples(P, I, n=3000, seed=1):
    rng = np.random.default_rng(seed)
    a, b, c = P[I[:, 0]], P[I[:, 1]], P[I[:, 2]]
    area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
    k = rng.choice(len(I), n, p=area / area.sum())
    r1, r2 = np.sqrt(rng.random(n))[:, None], rng.random(n)[:, None]
    return a[k] * (1 - r1) + b[k] * r1 * (1 - r2) + c[k] * r1 * r2, k


def feet_clusters(P, I, up=1, contact_frac=0.08, grid=64, min_area_share=0.006):
    """leg ends: the surface inside the lowest band, rasterised from above, split into connected areas. Each with its own lowest point.
    Returns [(area share of the footprint square, a, b, lowest as a share of height)]."""
    axes = [i for i in range(3) if i != up]
    lo, H = P[:, up].min(), np.ptp(P[:, up])
    S, _ = surface_samples(P, I, 60000)
    q = S[S[:, up] < lo + contact_frac * H]
    if not len(q):
        return []
    mn = P[:, axes].min(axis=0)
    ext = float(np.ptp(P[:, axes], axis=0).max())
    ij = np.clip(((q[:, axes] - mn) / ext * (grid - 1)).astype(int), 0, grid - 1)
    occ = np.zeros((grid, grid), bool)
    occ[ij[:, 0], ij[:, 1]] = True
    lab = -np.ones((grid, grid), int)
    k = 0
    for a, b in zip(*np.nonzero(occ)):
        if lab[a, b] >= 0:
            continue
        st = [(a, b)]
        lab[a, b] = k
        while st:
            x, y = st.pop()
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    u, v = x + dx, y + dy
                    if 0 <= u < grid and 0 <= v < grid and occ[u, v] and lab[u, v] < 0:
                        lab[u, v] = k
                        st.append((u, v))
        k += 1
    pts = lab[ij[:, 0], ij[:, 1]]
    out = []
    for c in range(k):
        share = float((lab == c).sum() / grid ** 2)
        if share >= min_area_share:
            m = pts == c
            out.append((round(share, 4), round(float(q[m, axes[0]].mean()), 3), round(float(q[m, axes[1]].mean()), 3), round(float((q[m, up].min() - lo) / H), 4)))
    return out


def silhouette(P, I, axis_u, axis_v, size=128, flip_u=False):
    """orthographic silhouette, normalised to the mesh bounding box height"""
    u, v = P[:, axis_u], P[:, axis_v]
    H = np.ptp(v)
    cx = (u.max() + u.min()) / 2
    px = (u - cx) / H * (size * 0.8) * (-1 if flip_u else 1) + size / 2
    py = (v.max() - v) / H * (size * 0.8) + size * 0.1
    out = np.zeros((size, size), bool)
    yy, xx = np.mgrid[0:size, 0:size]
    for tri in I:
        x, y = px[tri], py[tri]
        x0, x1, y0, y1 = int(max(x.min(), 0)), int(min(x.max() + 1, size - 1)), int(max(y.min(), 0)), int(min(y.max() + 1, size - 1))
        if x1 < x0 or y1 < y0:
            continue
        X, Y = xx[y0:y1 + 1, x0:x1 + 1] + .5, yy[y0:y1 + 1, x0:x1 + 1] + .5
        d = (y[1] - y[2]) * (x[0] - x[2]) + (x[2] - x[1]) * (y[0] - y[2])
        if abs(d) < 1e-9:
            continue
        a = ((y[1] - y[2]) * (X - x[2]) + (x[2] - x[1]) * (Y - y[2])) / d
        b = ((y[2] - y[0]) * (X - x[2]) + (x[0] - x[2]) * (Y - y[2])) / d
        out[y0:y1 + 1, x0:x1 + 1] |= (a >= -.02) & (b >= -.02) & (a + b <= 1.02)
    return out


def concept_silhouette(path, size=128):
    mk = fg_mask(_load(path, 512))
    ys, xs = np.nonzero(mk)
    t, b = ys.min(), ys.max()
    H = b - t
    cx = (xs.min() + xs.max()) / 2
    yy, xx = np.mgrid[0:size, 0:size]
    X = np.clip(((xx + .5 - size / 2) / (size * 0.8) * H + cx).astype(int), 0, mk.shape[1] - 1)
    Y = np.clip(((yy + .5 - size * 0.1) / (size * 0.8) * H + t).astype(int), 0, mk.shape[0] - 1)
    return mk[Y, X]


def top_roundness(P, up=1, top_frac=0.15):
    """outline of the top part seen from above: area of its convex hull over the circle through its farthest point. Circle 1.0, square 0.64"""
    axes = [i for i in range(3) if i != up]
    q = P[P[:, up] > P[:, up].max() - top_frac * np.ptp(P[:, up])][:, axes]
    pts = sorted(map(tuple, q))

    def half(seq):
        h = []
        for p in seq:
            while len(h) >= 2 and (h[-1][0] - h[-2][0]) * (p[1] - h[-2][1]) - (h[-1][1] - h[-2][1]) * (p[0] - h[-2][0]) <= 0:
                h.pop()
            h.append(p)
        return h[:-1]

    hull = np.array(half(pts) + half(pts[::-1]))
    x, y = hull[:, 0], hull[:, 1]
    area = 0.5 * abs(np.dot(x, np.roll(y, 1)) - np.dot(y, np.roll(x, 1)))
    c = (hull.max(axis=0) + hull.min(axis=0)) / 2
    r = np.linalg.norm(hull - c, axis=1).max()
    return float(area / (np.pi * r * r))


def parts_joined(island_count):
    """catalog tripo.expect.separate_parts: the model has a part that must move on its own, so one connected piece needs a human look (T12)"""
    return island_count < 2


def check_result(glb, concepts=None, expect=None):
    """numbers + fails for a generated GLB (colour is tripo.quality's COLOR_LOST). concepts: input image paths in view order.
    expect: catalog tripo.expect."""
    P, I = load_glb(glb)
    expect = expect or {}
    concepts = [Path(c) for c in concepts or []]
    m = dict(tris=int(len(I)))
    fails = []
    isl = islands(P, I)
    m['islands'] = len(isl)
    m['largest_island_share'] = round(isl[0], 3)
    if expect.get('separate_parts') and parts_joined(len(isl)):
        fails.append(f'RESULT_PARTS_JOINED {len(isl)} island: a cape or other part is joined to the body; look at where it joins before rigging')
    feet = feet_clusters(P, I)
    m['feet'] = feet
    if 'feet' in expect:
        if len(feet) != expect['feet']:
            fails.append(f"RESULT_FEET_COUNT {len(feet)} expected {expect['feet']}")
        if feet and max(f[3] for f in feet) - min(f[3] for f in feet) > 0.03:
            fails.append(f'RESULT_FEET_NOT_LEVEL lowest {[f[3] for f in feet]}')
    if expect.get('top_round'):
        tr = top_roundness(P)
        m['top_roundness'] = round(tr, 3)
        if tr < 0.88:      # set on two archived pads only: round 0.92, rounded square 0.84
            fails.append(f'RESULT_TOP_NOT_ROUND {tr:.3f} (round 1.0, square 0.64)')
    if len(concepts) >= 2:
        # the front image is matched to whichever horizontal direction fits best; the side image to the other axis
        best = {}
        cs = {v: concept_silhouette(c) for v, c in zip(VIEWS, concepts)}
        for a, v in ((0, 'front'), (2, 'front'), (0, 'left'), (2, 'left')):
            if v not in cs:
                continue
            s = silhouette(P, I, a, 1)
            best[v] = max(best.get(v, 0), max(_iou(s, cs[v]), _iou(s[:, ::-1], cs[v])))
        m['silhouette_iou'] = {k: round(x, 3) for k, x in best.items()}
        if best and min(best.values()) < 0.80:
            fails.append(f'RESULT_SILHOUETTE_DIFFERS {m["silhouette_iou"]}')
    return dict(measures=m, fails=fails)


def _box(c, s):
    x, y, z = c
    a, b, h = s
    v = np.array([[x - a, y, z - b], [x + a, y, z - b], [x + a, y, z + b], [x - a, y, z + b],
                  [x - a, y + h, z - b], [x + a, y + h, z - b], [x + a, y + h, z + b], [x - a, y + h, z + b]], float)
    f = np.array([[0, 1, 2], [0, 2, 3], [4, 6, 5], [4, 7, 6], [0, 4, 5], [0, 5, 1], [1, 5, 6], [1, 6, 2], [2, 6, 7], [2, 7, 3], [3, 7, 4], [3, 4, 0]])
    return v, f

def test_creature(legs, lift=None):
    parts = [_box((0, 0, 0), (.4, .4, 1))]
    parts[0][0][:, 1] += .4
    for i, (x, z) in enumerate(legs):
        parts.append(_box((x, (lift or {}).get(i, 0), z), (.08, .08, .4)))
    P, I, n = [], [], 0
    for v, f in parts:
        P.append(v)
        I.append(f + n)
        n += len(v)
    return np.concatenate(P), np.concatenate(I)


def selftest():
    assert parts_joined(1) and not parts_joined(2), 'one island means the part is joined'
    creature = test_creature
    four = [(-.25, -.25), (.25, -.25), (-.25, .25), (.25, .25)]
    P, I = creature(four)
    f = feet_clusters(P, I)
    assert len(f) == 4 and max(x[3] for x in f) < .01, f
    P, I = creature(four + [(0, .36)])
    assert len(feet_clusters(P, I)) == 5, 'an extra leg counts'
    P, I = creature(four, {0: .05})
    f = feet_clusters(P, I)
    assert len(f) == 4 and max(x[3] for x in f) - min(x[3] for x in f) > .03, f
    th = np.linspace(0, 2 * np.pi, 40, endpoint=False)
    disc = np.stack([np.cos(th), np.ones(40), np.sin(th)], 1)
    sq = np.array([[1, 1, 1], [-1, 1, 1], [-1, 1, -1], [1, 1, -1], [0, 0, 0]], float)
    assert top_roundness(np.vstack([disc, [[0, 0, 0]]])) > .95 and top_roundness(sq) < .7

    def sil(feet):
        m = np.zeros((200, 200), bool)
        m[40:120, 50:150] = True
        for x0, x1, y1 in feet:
            m[120:y1, x0:x1] = True
        return m
    assert len(foot_blobs(sil([(60, 90, 170), (110, 140, 170)]))) == 2
    assert sorted(b[2] for b in foot_blobs(sil([(50, 70, 140), (72, 95, 165), (105, 128, 165), (130, 150, 140)])))[0] < 145, 'rear feet drawn higher are found'
    return 'genqc selftest PASS'


if __name__ == '__main__':
    import sys
    a = sys.argv[1:]
    if a == ['selftest']:
        print(selftest())
    elif a[:1] == ['input']:
        print(json.dumps(check_input(a[1:]), ensure_ascii=False, indent=1))
    elif a[:1] == ['result']:
        i = a.index('--expect') if '--expect' in a else len(a)
        print(json.dumps(check_result(a[1], a[2:i], json.loads(a[i + 1]) if i < len(a) else None), ensure_ascii=False, indent=1))
    else:
        raise SystemExit('USAGE: genqc.py selftest | input <front> <left> <back> [right] | result <model.glb> [concepts...] [--expect JSON]')
