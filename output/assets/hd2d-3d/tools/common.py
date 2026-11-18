import json, numpy as np
from PIL import Image
def load(n):
    m = json.load(open(n + '/mesh.json'))
    SW, SH = m['S'], m.get('SH', m['S'])
    hi = np.asarray(Image.open(n + '/bake_hi.png').convert('RGB')).astype(np.float64)[::-1]  # row0 = v0
    ss = hi.shape[1] // SW
    lo = hi.reshape(SH, ss, SW, ss, 3).mean((1, 3))
    return m, (SH, SW), lo
def raster_mask(m, S):
    """per texel: triangle id whose UV contains texel centre (-1 none). row = v."""
    tid = -np.ones((S, S), int)
    for i, t in enumerate(m['tris']):
        uv = np.array(t['uv']) * S
        x0, y0 = np.floor(uv.min(0)).astype(int); x1, y1 = np.ceil(uv.max(0)).astype(int)
        xs, ys = np.meshgrid(np.arange(max(x0, 0), min(x1 + 1, S)), np.arange(max(y0, 0), min(y1 + 1, S)))
        px = xs + .5; py = ys + .5
        a, b, c = uv
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-12: continue
        l1 = ((b[1] - c[1]) * (px - c[0]) + (c[0] - b[0]) * (py - c[1])) / d
        l2 = ((c[1] - a[1]) * (px - c[0]) + (a[0] - c[0]) * (py - c[1])) / d
        l3 = 1 - l1 - l2
        e = -1e-6
        ins = (l1 >= e) & (l2 >= e) & (l3 >= e)
        tid[ys[ins], xs[ins]] = i
    return tid
def hsv(c):
    c = np.asarray(c, float) / 255
    mx = c.max(-1); mn = c.min(-1); d = mx - mn
    s = np.where(mx > 0, d / np.maximum(mx, 1e-9), 0)
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    h = np.where(d == 0, 0, np.where(mx == r, ((g - b) / np.maximum(d, 1e-9)) % 6, np.where(mx == g, (b - r) / np.maximum(d, 1e-9) + 2, (r - g) / np.maximum(d, 1e-9) + 4))) * 60
    return h, s, mx
def from_hsv(h, s, v):
    import colorsys
    r, g, b = colorsys.hsv_to_rgb(h / 360, s, v)
    return np.array([round(r * 255), round(g * 255), round(b * 255)])
def raster_full(m, S):
    """tid, 3D position per texel (row=v). S = (SH, SW)."""
    SH, SW = S
    tid = -np.ones((SH, SW), int); pos = np.zeros((SH, SW, 3))
    for i, t in enumerate(m['tris']):
        uv = np.array(t['uv']) * [SW, SH]; P = np.array(t['p'])
        x0, y0 = np.floor(uv.min(0)).astype(int); x1, y1 = np.ceil(uv.max(0)).astype(int)
        xs, ys = np.meshgrid(np.arange(max(x0, 0), min(x1 + 1, SW)), np.arange(max(y0, 0), min(y1 + 1, SH)))
        px = xs + .5; py = ys + .5
        a, b, c = uv
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-12: continue
        l1 = ((b[1] - c[1]) * (px - c[0]) + (c[0] - b[0]) * (py - c[1])) / d
        l2 = ((c[1] - a[1]) * (px - c[0]) + (a[0] - c[0]) * (py - c[1])) / d
        l3 = 1 - l1 - l2
        ins = (l1 >= -1e-6) & (l2 >= -1e-6) & (l3 >= -1e-6)
        tid[ys[ins], xs[ins]] = i
        pos[ys[ins], xs[ins]] = (l1[ins, None] * P[0] + l2[ins, None] * P[1] + l3[ins, None] * P[2])
    # conservative pass: thin UV slivers (< 1 texel wide) get texels along their edges
    for i, t in enumerate(m['tris']):
        uv = np.array(t['uv']) * [SW, SH]; P = np.array(t['p'])
        for a_, b_ in ((0, 1), (1, 2), (2, 0)):
            n_ = int(np.ceil(np.linalg.norm(uv[b_] - uv[a_]) * 2)) + 1
            for w in np.linspace(0, 1, n_ + 1):
                q = uv[a_] * (1 - w) + uv[b_] * w
                x, y = int(q[0]), int(q[1])
                if 0 <= x < SW and 0 <= y < SH and tid[y, x] < 0:
                    tid[y, x] = i; pos[y, x] = P[a_] * (1 - w) + P[b_] * w
    return tid, pos
def uv_dir(t, axis3):
    """UV-space direction of a 3D axis on triangle t (via Jacobian)."""
    P = np.array(t['p']); U = np.array(t['uv'])
    e1, e2 = P[1] - P[0], P[2] - P[0]; f1, f2 = U[1] - U[0], U[2] - U[0]
    n = np.cross(e1, e2); n /= np.linalg.norm(n) + 1e-12
    g = axis3 - n * np.dot(axis3, n)
    if np.linalg.norm(g) < 1e-6: return None
    # solve g = a e1 + b e2
    A = np.c_[e1, e2]; ab = np.linalg.lstsq(A, g, rcond=None)[0]
    return ab[0] * f1 + ab[1] * f2
def box_mean(a, mask, r):
    from numpy.lib.stride_tricks import sliding_window_view as sw
    k = 2 * r + 1
    A = np.pad(a * mask[..., None] if a.ndim == 3 else a * mask, ((r, r), (r, r)) + (((0, 0),) if a.ndim == 3 else ()))
    M = np.pad(mask.astype(float), r)
    sA = sw(A, (k, k), axis=(0, 1)).sum((-1, -2)); sM = sw(M, (k, k)).sum((-1, -2))
    return sA / np.maximum(sM, 1e-9)[..., None] if a.ndim == 3 else sA / np.maximum(sM, 1e-9)
def h32(*a):
    x = np.uint64(0x9E3779B97F4A7C15)
    v = np.zeros(np.broadcast(*a).shape, np.uint64) + np.uint64(1469598103934665603)
    for k in a:
        v = (v ^ (np.asarray(k).astype(np.int64).astype(np.uint64) + x)) * np.uint64(1099511628211)
        v ^= v >> np.uint64(29)
    return (v % np.uint64(1 << 24)).astype(np.float64) / (1 << 24)
