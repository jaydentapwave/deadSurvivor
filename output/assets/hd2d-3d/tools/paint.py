# 도트 알베도 칠(크레딧 0): mesh.json의 UV(48텍셀/m)에 팔레트 색만 칠한다. 칠한 외곽선·그라데이션·점 노이즈 없음.
# 시범(hd2d-3d-pilot/tools/paint.py)을 이어 씀. 바뀐 것:
#  - 잎: 연두 잎(L0)은 하늘을 보는 덩어리 윗부분에서만(시범의 0.17m 무리 무작위 구역 = 판독 "위장 무늬 노란 얼룩" → 뺌)
#  - 바위 r3: 바탕 한 색(R0) + 금(R1, 긴 금 가운데 한 점 R2). 칠 단계를 알베도에서 나누지 않아 셰이더 3단 띠만 보인다.
#  - 돌담·풀포기·꽃 새로.
# /usr/bin/python3 paint.py <dir>   (kind는 mesh.json) → <dir>/albedo.png, <dir>/palette.json
import sys, json
from common import *

D = sys.argv[1]
m = json.load(open(D + '/mesh.json'))
kind = m['kind']
SH, SW = m['SH'], m['S']
tid, pos = raster_full(m, (SH, SW))
cov = tid >= 0
tris = m['tris']
part = np.array([t['part'] for t in tris])
grp = np.array([t['grp'] for t in tris])
FN = np.array([t['n'] for t in tris])
VN = np.array([t['vn'] for t in tris]).mean(1)
VN /= np.linalg.norm(VN, axis=-1, keepdims=True)
T = np.maximum(tid, 0)
P = part[T]; G = grp[T]
nz = np.where(P == 'leaf', VN[T][..., 2], FN[T][..., 2])
up = cov & (FN[T][..., 2] > 0.55)            # 위를 향한 면: 2×2 묶음
H = lambda c: np.array([int(c[i:i + 2], 16) for i in (1, 3, 5)])
ys, xs = np.mgrid[0:SH, 0:SW]
gx = np.where(up, xs // 2, xs); gy = np.where(up, ys // 2, ys)
zmax = m['bounds'][1][2]
# 조각(grp)별 3D 경계 상자
PV = np.array([t['p'] for t in tris])
gmin = {}; gmax = {}
for g in np.unique(grp):
    q = PV[grp == g].reshape(-1, 3); gmin[g] = q.min(0); gmax[g] = q.max(0)
GMIN = np.zeros((SH, SW, 3)); GMAX = np.zeros((SH, SW, 3))
for g in gmin:
    sel = cov & (G == g); GMIN[sel] = gmin[g]; GMAX[sel] = gmax[g]

if kind.startswith(('tree', 'bush')):
    PAL = {'L0': '#b3ca7b', 'L1': '#5c8d73', 'L2': '#49746b', 'W0': '#774c3b', 'W1': '#5c493c'}
    if kind.startswith('bush'):
        PAL = {k: v for k, v in PAL.items() if k[0] == 'L'}
elif kind.startswith(('boulder', 'stone', 'wall')):
    PAL = {'R0': '#d8d3a9', 'R1': '#9d9778', 'R2': '#403d2a'}
elif kind == 'grass_v1':
    PAL = {'G0': '#3a3721', 'G1': '#4f7a2e', 'G2': '#7fa040', 'G3': '#a8c828'}
elif kind == 'grass_v2':
    PAL = {'G0': '#3a3721', 'G1': '#4f7a2e', 'G2': '#7fa040', 'G3': '#b3a954'}
elif kind == 'flower_white':
    PAL = {'S0': '#4f7a2e', 'S1': '#7fa040', 'F0': '#fff9ed', 'F1': '#f8b828'}
elif kind == 'flower_yellow':
    PAL = {'S0': '#4f7a2e', 'S1': '#7fa040', 'F0': '#ffe08a', 'F1': '#f8b828', 'F2': '#d5833a'}
elif kind == 'flower_purple':
    PAL = {'S0': '#4f7a2e', 'S1': '#7fa040', 'F0': '#b07fd0', 'F1': '#7a4a9e', 'F2': '#f8b828'}
idx = np.full((SH, SW), -1)
names = list(PAL)
def put(mask, name): idx[mask] = names.index(name)

def leaf_blobs(cell, rx, ry, seed):
    """세포마다 잎 한 장(타원, 가로로 조금 눕힘). 반환: 잎 안인가, 잎 세포 id, 잎 안 세로 위치."""
    best = np.zeros((SH, SW), bool); cid = np.zeros((SH, SW)); eyo = np.zeros((SH, SW))
    cx0 = gx // cell; cy0 = gy // cell
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cx = cx0 + dx; cy = cy0 + dy
            px = cx * cell + h32(cx, cy, seed) * cell; py = cy * cell + h32(cx, cy, seed + 1) * cell
            ex = (gx + .5 - px) / rx; ey = (gy + .5 - py) / ry
            ex2 = ex + 0.45 * ey   # 비스듬한 잎
            ins = ex2 ** 2 + ey ** 2 <= 1
            cid = np.where(ins & ~best, h32(cx, cy, seed + 2), cid); eyo = np.where(ins & ~best, ey, eyo); best |= ins
    return best, cid, eyo

def cracks(density, seed):
    """짧은 금 줄(4~9텍셀), 한 단 어둡게. 긴 금 가운데 한 점만 R2."""
    rng = np.random.default_rng(seed)
    for k in range(int(m['A3'] * density)):
        t = rng.choice(len(tris)); bc = rng.dirichlet((1, 1, 1))
        uv = (np.array(tris[t]['uv']) * [SW, SH]).T @ bc
        if FN[t][2] < -0.2: continue
        ang = rng.choice([0, 0.35, -0.35, 1.2]); L_ = rng.integers(4, 10)
        x, y = uv
        for s in range(L_):
            xi, yi = int(x + s * np.cos(ang)), int(y + s * np.sin(ang))
            if 0 <= xi < SW and 0 <= yi < SH and tid[yi, xi] >= 0 and tid[yi, xi] // 1 >= 0:
                if grp[tid[yi, xi]] != grp[t]: break
                idx[yi, xi] = names.index('R1')
                if s == L_ // 2 and L_ >= 8: idx[yi, xi] = names.index('R2')

if kind.startswith(('tree', 'bush')):
    L = cov & (P == 'leaf'); Wd = cov & (P == 'wood')
    put(L, 'L1')
    leaf, cid, ey = leaf_blobs(4, 2.6, 1.7, 7)
    # 잎 비늘: 잎마다 아랫테두리 = 그늘 잎(L2). 아래를 보는 쪽은 그늘 잎을 더.
    put(L & leaf & (ey < -0.25), 'L2')
    put(L & (nz < -0.35) & leaf & (cid < 0.6), 'L2')
    # 연두 잎(L0): 하늘을 보는 덩어리 윗부분에서만, 작은 잎 덩어리 통째(아랫줄은 바탕색). 무작위 구역 없음.
    leaf2, cid2, ey2 = leaf_blobs(4, 1.9, 1.5, 17)
    top = nz > 0.50
    put(L & leaf2 & top & (cid2 > 0.30) & (ey2 > -0.45), 'L0')
    put(L & leaf2 & top & (cid2 > 0.30) & (ey2 <= -0.45), 'L1')
    if 'W0' in PAL:
        put(Wd, 'W0')
        run = h32(xs, ys // 6, 31) < 0.14
        put(Wd & run & (h32(xs, 0, 33) < 0.5), 'W1')
elif kind.startswith(('boulder', 'stone')):
    put(cov, 'R0')
    cracks({'boulder_v1': 14, 'stone_v1': 10}.get(kind, 5 if kind.startswith('boulder') else 4), 20261118 + len(tris))
    if kind in ('boulder_v1', 'stone_v1'):   # r4: 땅에 닿는 2텍셀 그늘 띠(R1) — 2D 바위 견본의 아래 그늘
        put(cov & (pos[..., 2] < 2 / 48) & (FN[T][..., 2] < 0.3), 'R1')
    # 이웃 없는 한 점 지우기(점 노이즈 금지)
    r1 = idx == names.index('R1')
    nb = np.zeros_like(r1, int)
    for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
        nb += np.roll(np.roll(r1, dy, 0), dx, 1)
    idx[r1 & (nb == 0)] = names.index('R0')
elif kind.startswith('wall'):
    # r3: broad rock-colour bands and sparse coarse mortar, not a wire grid.
    put(cov, 'R0')
    side = cov & (np.abs(FN[T][..., 2]) < .5)
    put(side, 'R1')
    # Bottom shade is a two-texel mass. Two courses only; no dense crack hatch.
    put(side & (pos[..., 2] < 2 / 48), 'R2')
    joint = side & (pos[..., 2] - GMIN[..., 2] < 1 / 48)
    front = side & (np.abs(FN[T][..., 1]) > .7)
    joint |= front & (pos[..., 0] - GMIN[..., 0] < 1 / 48)
    put(joint, 'R2')
    if kind == 'wall_b':
        # One coarse chipped light patch per stone, with a 2-texel staircase edge.
        # Break broad uniform fill without returning to a fine mortar/hatch grid.
        localx = pos[..., 0] - GMIN[..., 0]
        localz = pos[..., 2] - GMIN[..., 2]
        cellx = np.floor(localx * 24); cellz = np.floor(localz * 24)
        width = GMAX[..., 0] - GMIN[..., 0]
        patch = front & (localx > width * .18) & (localx < width * .78)
        patch &= (cellz >= 3) & (cellz <= 4 + (cellx % 3 == 0))
        put(patch & ~joint, 'R0')
        # A short connected crack, two texels thick, per course.
        scar = front & (cellx >= 2) & (cellx <= 3) & (cellz == 2)
        put(scar & ~joint, 'R2')
elif kind.startswith('grass'):
    # 잎마다 높이 비율: 밑 1텍셀 G0, ~45% G1, ~80% G2, 끝 G3
    f = pos[..., 2] / np.maximum(GMAX[..., 2], 1e-6)
    put(cov, 'G2'); put(cov & (f < 0.45), 'G1'); put(cov & (f >= 0.80), 'G3'); put(cov & (pos[..., 2] < 1.5 / 48), 'G0')
elif kind.startswith('flower'):
    S = cov & (P == 'stem'); F = cov & (P == 'petal')
    put(S, 'S1'); put(S & (pos[..., 2] < 0.06), 'S0')
    put(F, 'F0')
    c = (GMIN + GMAX) / 2
    rxy = np.hypot(pos[..., 0] - c[..., 0], pos[..., 1] - c[..., 1])
    if kind == 'flower_white':
        put(F & up & (rxy < 0.022), 'F1')
    elif kind == 'flower_yellow':
        put(F & (FN[T][..., 2] < 0.3), 'F1'); put(F & up & (rxy < 0.022), 'F2')
    else:
        put(F & (FN[T][..., 2] < 0.3), 'F1'); put(F & up & (rxy < 0.022), 'F2')

# 위를 향한 면 2×2 묶음(왼아래 텍셀 색을 넷에)
yy = ys & ~1; xx = xs & ~1
idx = np.where(up & cov & (tid[yy, xx] >= 0) & (idx[yy, xx] >= 0), idx[yy, xx], idx)

# 빈 텍셀: 섬 둘레 12텍셀까지 이웃 색, 나머지 첫 색
filled = idx >= 0
for _ in range(12):
    if filled.all(): break
    src = idx.copy()
    for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
        sh = np.roll(np.roll(src, dy, 0), dx, 1)
        take = (idx < 0) & (sh >= 0); idx[take] = sh[take]
    filled = idx >= 0
idx[idx < 0] = 0
cols = np.array([H(PAL[n]) for n in names], np.uint8)
img = cols[idx][::-1]
Image.fromarray(img).save(D + '/albedo.png')
counts = {n: int(((idx == i) & cov).sum()) for i, n in enumerate(names)}
A_uv = cov.sum()
json.dump({'palette': PAL, 'texels': counts, 'tex': [SW, SH], 'fill': float(cov.mean()),
           'density_texel_per_m': float(np.sqrt(A_uv / m['A3']))}, open(D + '/palette.json', 'w'), ensure_ascii=False, indent=1)
print(kind, counts, 'dens', round(float(np.sqrt(A_uv / m['A3'])), 1), 'fill', round(float(cov.mean()), 2))
