"""Resolution-independent fields: hash, value noise, splines, distances.

Every mask in the generator is a function of floor coordinates (x, y in metres),
so the same layout can be sampled for the top-down map, the 3456x1728 shader
masks and the 30 degree preview without resampling artefacts.
"""
import math
import numpy as np


def hash01(ix, iy, seed):
    """Deterministic integer hash -> [0, 1). Vectorised over integer arrays."""
    seed = int(seed) & 0x7FFFFFFF
    h = (np.asarray(ix, dtype=np.int64) * 374761393 + np.asarray(iy, dtype=np.int64) * 668265263
         + seed * 2147483647) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    h = h ^ (h >> 16)
    return (h & 0xFFFFFF) / float(0x1000000)


def value_noise(x, y, scale, seed):
    """Smooth value noise in [0, 1] with lattice spacing `scale` metres."""
    fx = np.asarray(x, dtype=np.float64) / scale
    fy = np.asarray(y, dtype=np.float64) / scale
    x0 = np.floor(fx)
    y0 = np.floor(fy)
    tx = fx - x0
    ty = fy - y0
    tx = tx * tx * (3 - 2 * tx)
    ty = ty * ty * (3 - 2 * ty)
    x0 = x0.astype(np.int64)
    y0 = y0.astype(np.int64)
    a = hash01(x0, y0, seed)
    b = hash01(x0 + 1, y0, seed)
    c = hash01(x0, y0 + 1, seed)
    d = hash01(x0 + 1, y0 + 1, seed)
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def fbm(x, y, scale, seed, octaves=3, gain=0.5):
    """Fractal value noise normalised to roughly [0, 1]."""
    total = 0.0
    norm = 0.0
    amp = 1.0
    for o in range(octaves):
        total = total + amp * value_noise(x, y, scale / (2 ** o), (int(seed) * 31 + o * 977) & 0x7FFFFFFF)
        norm += amp
        amp *= gain
    return total / norm


def catmull_rom(points, samples_per_metre=8.0):
    """Centripetal-free uniform Catmull-Rom through control points -> dense polyline (N, 2)."""
    pts = np.asarray(points, dtype=np.float64)
    ext = np.vstack([2 * pts[0] - pts[1], pts, 2 * pts[-1] - pts[-2]])
    out = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        n = max(2, int(np.linalg.norm(p2 - p1) * samples_per_metre))
        t = np.linspace(0, 1, n, endpoint=False)[:, None]
        t2 = t * t
        t3 = t2 * t
        seg = 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
        out.append(seg)
    out.append(pts[-1:])
    return np.vstack(out)


def arc_length(poly):
    seg = np.linalg.norm(np.diff(poly, axis=0), axis=1)
    return np.concatenate([[0.0], np.cumsum(seg)])


def max_turn_per_metre(poly, window=1.0):
    """Largest heading change (degrees) over any `window` metres of the polyline."""
    s = arc_length(poly)
    d = np.diff(poly, axis=0)
    heading = np.unwrap(np.arctan2(d[:, 1], d[:, 0]))
    smid = (s[:-1] + s[1:]) / 2
    worst = 0.0
    j = 0
    for i in range(len(heading)):
        while smid[i] - smid[j] > window:
            j += 1
        worst = max(worst, abs(heading[i] - heading[j]))
    return math.degrees(worst)


def polyline_distance(px, py, poly, chunk=200000):
    """Euclidean distance from points to a polyline, plus arc-length parameter of the closest point."""
    px = np.asarray(px, dtype=np.float64)
    py = np.asarray(py, dtype=np.float64)
    shape = px.shape
    fx = px.ravel()
    fy = py.ravel()
    a = poly[:-1]
    b = poly[1:]
    ab = b - a
    ab2 = np.maximum((ab ** 2).sum(1), 1e-12)
    s = arc_length(poly)
    best = np.full(fx.shape, np.inf)
    best_s = np.zeros(fx.shape)
    # Coarse prefilter by segment bounding boxes keeps this fast for dense grids.
    for start in range(0, len(fx), chunk):
        qx = fx[start:start + chunk]
        qy = fy[start:start + chunk]
        bd = np.full(qx.shape, np.inf)
        bs = np.zeros(qx.shape)
        for k in range(len(a)):
            t = ((qx - a[k, 0]) * ab[k, 0] + (qy - a[k, 1]) * ab[k, 1]) / ab2[k]
            t = np.clip(t, 0, 1)
            cx = a[k, 0] + t * ab[k, 0]
            cy = a[k, 1] + t * ab[k, 1]
            d = (qx - cx) ** 2 + (qy - cy) ** 2
            m = d < bd
            bd[m] = d[m]
            bs[m] = s[k] + t[m] * math.sqrt(ab2[k])
        best[start:start + chunk] = bd
        best_s[start:start + chunk] = bs
    return np.sqrt(best).reshape(shape), best_s.reshape(shape)


def resample(poly, step):
    s = arc_length(poly)
    t = np.arange(0, s[-1], step)
    return np.stack([np.interp(t, s, poly[:, 0]), np.interp(t, s, poly[:, 1])], axis=1), t


def l1(x, y):
    return np.abs(x) + np.abs(y)


class Grid:
    """Square raster over [-half, half]^2 with `res` cells per metre; row 0 = far (y = +half)."""

    def __init__(self, half=36.0, res=8):
        self.half = half
        self.res = res
        self.n = int(round(2 * half * res))
        c = (np.arange(self.n) + 0.5) / res - half
        self.x, self.y = np.meshgrid(c, c[::-1])

    def index(self, x, y):
        col = np.clip(((np.asarray(x) + self.half) * self.res).astype(int), 0, self.n - 1)
        row = np.clip(((self.half - np.asarray(y)) * self.res).astype(int), 0, self.n - 1)
        return row, col

    def sample(self, mask, x, y):
        r, c = self.index(x, y)
        return mask[r, c]


def label_components(mask):
    """4-connected component labels for a boolean raster (pure numpy/python BFS)."""
    from collections import deque
    lab = np.zeros(mask.shape, dtype=np.int32)
    cur = 0
    h, w = mask.shape
    for r0, c0 in zip(*np.nonzero(mask)):
        if lab[r0, c0]:
            continue
        cur += 1
        q = deque([(r0, c0)])
        lab[r0, c0] = cur
        while q:
            r, c = q.popleft()
            for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                rr, cc = r + dr, c + dc
                if 0 <= rr < h and 0 <= cc < w and mask[rr, cc] and not lab[rr, cc]:
                    lab[rr, cc] = cur
                    q.append((rr, cc))
    return lab, cur


def grid_polyline_distance(grid, poly, maxd=8.0):
    """Distance (clipped at maxd) and arc parameter from every grid cell to a polyline.

    Only cells inside each segment's padded bounding box are touched, so this is fast
    on the 8 cells/m generation grid. Use `bilinear` to sample it at other resolutions.
    """
    dist = np.full(grid.x.shape, maxd)
    par = np.zeros(grid.x.shape)
    s = arc_length(poly)
    for k in range(len(poly) - 1):
        a = poly[k]
        b = poly[k + 1]
        lo = np.minimum(a, b) - maxd
        hi = np.maximum(a, b) + maxd
        r0, c0 = grid.index(lo[0], hi[1])
        r1, c1 = grid.index(hi[0], lo[1])
        sx = grid.x[r0:r1 + 1, c0:c1 + 1]
        sy = grid.y[r0:r1 + 1, c0:c1 + 1]
        ab = b - a
        L2 = max(float(ab @ ab), 1e-12)
        t = np.clip(((sx - a[0]) * ab[0] + (sy - a[1]) * ab[1]) / L2, 0, 1)
        d = np.hypot(sx - (a[0] + t * ab[0]), sy - (a[1] + t * ab[1]))
        sub = dist[r0:r1 + 1, c0:c1 + 1]
        subp = par[r0:r1 + 1, c0:c1 + 1]
        m = d < sub
        sub[m] = d[m]
        subp[m] = s[k] + t[m] * math.sqrt(L2)
    return dist, par


def bilinear(grid, field, x, y):
    """Sample a grid field at arbitrary floor points (clamped)."""
    fx = (np.asarray(x) + grid.half) * grid.res - 0.5
    fy = (grid.half - np.asarray(y)) * grid.res - 0.5
    fx = np.clip(fx, 0, grid.n - 1.001)
    fy = np.clip(fy, 0, grid.n - 1.001)
    c0 = np.floor(fx).astype(int)
    r0 = np.floor(fy).astype(int)
    tx = fx - c0
    ty = fy - r0
    a = field[r0, c0]
    b = field[r0, c0 + 1]
    c = field[r0 + 1, c0]
    d = field[r0 + 1, c0 + 1]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def erode(mask, r):
    m = mask.copy()
    for _ in range(r):
        n = m.copy()
        n[1:] &= m[:-1]
        n[:-1] &= m[1:]
        n[:, 1:] &= m[:, :-1]
        n[:, :-1] &= m[:, 1:]
        n[0, :] = n[-1, :] = n[:, 0] = n[:, -1] = False
        m = n
    return m


def dilate(mask, r):
    m = mask.copy()
    for _ in range(r):
        n = m.copy()
        n[1:] |= m[:-1]
        n[:-1] |= m[1:]
        n[:, 1:] |= m[:, :-1]
        n[:, :-1] |= m[:, 1:]
        m = n
    return m
