# Emulates the K18 background pass: low-res target (1 px = 1/48 m = 1 character dot), ortho 30 deg pitch,
# 3-band toon (F8 FieldSurface thresholds .25/.68 -> x0.60/0.82/1.0), hard shadow (x(.44,.43,.39)),
# silhouette-only depth outline #34140e, then x2 nearest (1080p, n=2).
import sys, json, math
from common import *

def render(name, albedo_path, yaw_deg, light=(-0.5, -0.45, 0.75), pad=6, outline=True, flat=False, smooth=('leaf', 'rock'), ground=None, mesh=None):
    m = mesh if mesh is not None else json.load(open(name + '/mesh.json'))
    # (pilot) smooth: parts shaded with interpolated vertex normals (leaf clumps); others flat face normals
    tex = np.asarray(Image.open(albedo_path).convert('RGB'))[::-1]   # row = v
    SH, SW = tex.shape[:2]
    tris = m['tris']
    P = np.array([t['p'] for t in tris]); UV = np.array([t['uv'] for t in tris])
    a = math.radians(yaw_deg); R = np.array([[math.cos(a), -math.sin(a), 0], [math.sin(a), math.cos(a), 0], [0, 0, 1]])
    P = P @ R.T
    pitch = math.radians(30)
    right = np.array([1., 0, 0]); up = np.array([0, math.sin(pitch), math.cos(pitch)]); fwd = np.array([0, math.cos(pitch), -math.sin(pitch)])
    sx = P @ right * 48; sy = P @ up * 48; dz = P @ fwd
    pr = pt = 0
    if ground is not None:
        zt = max(max(p[2] for p in t['p']) for t in m['tris'])
        pr, pt = int(zt * 0.7 * 48) + 4, int(zt * 0.62 * 48 * 0.5) + 4   # 그림자가 오른쪽 뒤로 떨어질 자리(높이에 맞춤)
    x0 = math.floor(sx.min()) - pad; y0 = math.floor(sy.min()) - pad
    W = math.ceil(sx.max()) + pad + pr - x0; H = math.ceil(sy.max()) + pad + pt - y0
    depth = np.full((H, W), np.inf); tid = -np.ones((H, W), int); bary = np.zeros((H, W, 3))
    for i in range(len(tris)):
        X = sx[i] - x0; Y = sy[i] - y0; Z = dz[i]
        bx0, bx1 = max(int(math.floor(X.min())), 0), min(int(math.ceil(X.max())), W - 1)
        by0, by1 = max(int(math.floor(Y.min())), 0), min(int(math.ceil(Y.max())), H - 1)
        xs, ys = np.meshgrid(np.arange(bx0, bx1 + 1) + .5, np.arange(by0, by1 + 1) + .5)
        d = (Y[1] - Y[2]) * (X[0] - X[2]) + (X[2] - X[1]) * (Y[0] - Y[2])
        if abs(d) < 1e-12: continue
        l1 = ((Y[1] - Y[2]) * (xs - X[2]) + (X[2] - X[1]) * (ys - Y[2])) / d
        l2 = ((Y[2] - Y[0]) * (xs - X[2]) + (X[0] - X[2]) * (ys - Y[2])) / d
        l3 = 1 - l1 - l2
        ins = (l1 >= 0) & (l2 >= 0) & (l3 >= 0)
        z = l1 * Z[0] + l2 * Z[1] + l3 * Z[2]
        iy = (ys - .5).astype(int); ix = (xs - .5).astype(int)
        upd = ins & (z < depth[iy, ix])
        depth[iy[upd], ix[upd]] = z[upd]; tid[iy[upd], ix[upd]] = i
        bary[iy[upd], ix[upd]] = np.stack([l1[upd], l2[upd], l3[upd]], -1)
    cov = tid >= 0
    T = np.maximum(tid, 0)
    uv = (bary[..., :, None] * UV[T]).sum(-2)
    tx = np.clip((uv[..., 0] * SW).astype(int), 0, SW - 1); ty = np.clip((uv[..., 1] * SH).astype(int), 0, SH - 1)
    col = tex[ty, tx].astype(float)
    if flat: col[:] = 180
    Lw = np.array(light) / np.linalg.norm(light)
    Lr = R @ Lw * 0 + Lw  # light fixed in world after model yaw (model rotates under the sun)
    N = np.array([t['n'] for t in tris]) @ R.T
    Npx = N[T]
    if smooth and 'vn' in tris[0]:
        VN = np.array([t['vn'] for t in tris]) @ R.T            # (tri, 3 corners, 3)
        sm = np.array([t.get('part') in smooth for t in tris])
        nI = (bary[..., :, None] * VN[T]).sum(-2); nI /= np.linalg.norm(nI, axis=-1, keepdims=True) + 1e-9
        Npx = np.where(sm[T][..., None], nI, Npx)
    dd = np.clip((Npx * Lr).sum(-1), 0, 1)
    band = np.where(dd < .25, .60, np.where(dd < .68, .82, 1.0))
    # hard shadow: ray from pixel world pos toward the light
    wp = (bary[..., :, None] * P[T]).sum(-2)
    def lit_of(o):
      lit = np.ones(len(o), bool)
      for i in range(len(tris)):
        v0, v1, v2 = P[i]; e1 = v1 - v0; e2 = v2 - v0
        h = np.cross(Lr, e2); a_ = e1 @ h
        if abs(a_) < 1e-9: continue
        f = 1 / a_; s_ = o - v0; u = f * (s_ @ h)
        q = np.cross(s_, e1); v = f * (q @ Lr); t = f * (q @ e2)
        lit &= ~((u >= 0) & (v >= 0) & (u + v <= 1) & (t > 1e-4))
      return lit
    sh = np.ones((H, W)); sh[cov] = lit_of(wp[cov] + Lr * 0.01)
    shade = band[..., None] * np.where(sh[..., None] > .5, 1, np.array([.44, .43, .39]))
    img = np.zeros((H, W, 4)); img[..., :3] = col * shade; img[..., 3] = cov * 255
    if ground is not None:   # 땅(z=0) 칸: 풀 질감 1:1 + 하드 땅 그림자(자기 물체만)
        gys, gxs = np.mgrid[0:H, 0:W]
        gsx = (gxs + x0 + .5) / 48; gsy = (gys + y0 + .5) / 48
        gp = np.stack([gsx, gsy / math.sin(pitch), np.zeros_like(gsx)], -1)
        g = ~cov
        gl = lit_of(gp[g] + Lr * 0.001)
        th, tw = ground.shape[:2]
        gc = ground[(H - 1 - gys[g]) % th, gxs[g] % tw].astype(float)
        gc = gc * np.where(gl[:, None], 1.0, np.array([.44, .43, .39]))
        img[g, :3] = gc; img[g, 3] = 255
    if outline:
        D = np.where(cov, depth, np.inf); Dp = np.pad(D, 1, constant_values=np.inf)
        edge = np.zeros_like(cov)
        for dy, dx in ((0, 1), (2, 1), (1, 0), (1, 2)):
            edge |= cov & (Dp[dy:dy + H, dx:dx + W] - D > 0.25)
        img[edge, :3] = (0x34, 0x14, 0x0e)
    img = np.clip(img, 0, 255).astype(np.uint8)[::-1]   # screen y up -> image rows down
    return img

if __name__ == '__main__':
    pass
