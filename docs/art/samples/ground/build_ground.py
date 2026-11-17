"""바닥 그림체 견본 조립 — 풀 마름모 타일, 흙길 직선 ↗, 들판 합성, 이음·색 측정.

입력: <work>/<s>/g128.png, d128.png (pixelize.py one 결과, 128x128, 팔레트 3.5 r2)
출력: docs/art/samples/ground/<s>/grass.png, path_ne.png, field.png(합성 640x360), field_x3.png, measure.json

타일은 이음이 없도록 만든다: 마름모 격자 L = {(24,12),(24,-12)}에 대해 주기적인 그림 f를
  f(p) = 원천 텍스처를 L의 이웃 사본들에서 창 가중치에 비례하는 확률로 골라 찍는다(픽셀마다, 씨앗 고정).
같은 원천 장의 이어진 영역에서만 가져오므로 변 양쪽이 같은 그림에서 온다. 새 색을 만들지 않는다(팔레트 그대로).
흙길은 길 방향 좌표 alpha에 대해 주기 1인 가장자리 굴곡을 써서 ↗ 다음 칸과 이어지고, 옆 변(beta=±0.5)은 풀 타일과 같다.
사용: /usr/bin/python3 build_ground.py <work_dir> <out_dir> <s> <strength>
"""
import json
import math
import sys

import numpy as np
from PIL import Image

W, H = 48, 24
CAP = "/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/.director/evidence/game/check-2/02-G2-1920x1080.png"
CHARS = ["bruno", "lena", "mira", "bolt"]
MONS = ["slime", "mushroom", "boar", "puffer", "rock"]
ART = "Assets/Game/03.Resources/Art/AIGenerated"


def ab(px, py):
    dx, dy = px + 0.5 - 24, py + 0.5 - 12
    return (dx / 24 - dy / 12) / 2, (dx / 24 + dy / 12) / 2


def lattice_tile(src, origin, seed):
    rng = np.random.default_rng(seed)
    out = np.zeros((H, W, 4), np.uint8)
    ox, oy = (src.shape[1] - W) // 2, (src.shape[0] - H) // 2  # 원천 가운데(창 범위 x -6..54, y -3..27이 64px 안에 든다)
    for y in range(H):
        for x in range(W):
            a, b = ab(x, y)
            if max(abs(a), abs(b)) >= 0.5:
                continue
            cands, wts = [], []
            for i in (-1, 0, 1):
                for j in (-1, 0, 1):
                    # 사본 (i,j): 이 픽셀을 이웃 칸 자리로 옮긴 위치에서 원천을 읽는다
                    qa, qb = a + j, b + i
                    r = 2 * max(abs(qa), abs(qb))
                    w = max(0.0, 1.25 - r)
                    if w <= 0:
                        continue
                    sx = ox + x + 24 * (i + j)
                    sy = oy + y + 12 * (i - j)
                    cands.append(src[sy % src.shape[0], sx % src.shape[1]])
                    wts.append(w ** 3)
            wts = np.array(wts) / sum(wts)
            out[y, x, :3] = cands[rng.choice(len(cands), p=wts)][:3]
            out[y, x, 3] = 255
    return out


def path_tile(grass, dirt, edge_rgb, strength, seed):
    out = grass.copy()
    rng = np.random.default_rng(seed)
    k = [rng.uniform(0, 2 * math.pi) for _ in range(3)]
    amp = {1: 0.0, 2: 0.03, 3: 0.05}[strength]
    for y in range(H):
        for x in range(W):
            if grass[y, x, 3] == 0:
                continue
            a, b = ab(x, y)
            t = 2 * math.pi * (a + 0.5)
            half = 0.34 + amp * (math.sin(t + k[0]) + 0.6 * math.sin(2 * t + k[1]))
            centre = amp * 0.5 * math.sin(t + k[2])
            d = abs(b - centre)
            if d < half:
                out[y, x] = dirt[y, x]
            elif edge_rgb is not None and d < half + 0.045:
                out[y, x, :3] = edge_rgb
    return out


def render_field(grass, path, path_row):
    fw, fh = 640, 360
    img = np.zeros((fh, fw, 4), np.uint8)
    for Y in range(fh):
        for X in range(fw):
            # 원점 칸 중심 (24,12) 기준 alpha,beta → 칸 번호와 칸 안 좌표
            dx, dy = X + 0.5 - 24, Y + 0.5 - 12
            a, b = (dx / 24 - dy / 12) / 2, (dx / 24 + dy / 12) / 2
            j, i = math.floor(a + 0.5), math.floor(b + 0.5)
            lx = X - 24 * (i + j)
            ly = Y - 12 * (i - j)
            tile = path if i == path_row else grass
            img[Y, X] = tile[ly, lx]
    return img


def paste(dst, spr, cx, by):
    h, w = spr.shape[:2]
    x0, y0 = cx - w // 2, by - h + 1
    for y in range(h):
        for x in range(w):
            if spr[y, x, 3] > 0:
                X, Y = x0 + x, y0 + y
                if 0 <= X < dst.shape[1] and 0 <= Y < dst.shape[0]:
                    dst[Y, X] = spr[y, x]


def lab(rgb):
    c = np.asarray(rgb, float) / 255
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ M.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def hexrgb(h):
    return [int(h[i:i + 2], 16) for i in (1, 3, 5)]


def hue(rgb):
    import colorsys
    r, g, b = [v / 255 for v in rgb]
    return colorsys.rgb_to_hsv(r, g, b)[0] * 360


def rep_color(pix):
    """대표색 = Lab 중앙값(L, a, b 각각)을 RGB 대신 Lab으로 돌려준다."""
    return np.median(lab(pix), 0)


def seam_measure(field, mask_fn):
    """C5: 칸 경계를 가로지르는 이웃 픽셀 쌍의 색 차이와 칸 안 이웃 쌍의 색 차이 비, 그리고 경계 띠 어긋남(px).
    어긋남: 경계 양쪽 4px 띠의 색 줄을 경계 방향으로 -3..3px 밀어 가장 잘 맞는 이동량. 0이면 띠가 이어진다."""
    L = lab(field[..., :3].astype(float))
    fh, fw = L.shape[:2]
    cross, inner = [], []
    for Y in range(fh - 1):
        for X in range(fw):
            if not mask_fn(X, Y) or not mask_fn(X, Y + 1):
                continue
            d = np.linalg.norm(L[Y, X] - L[Y + 1, X])
            if tile_id(X, Y) != tile_id(X, Y + 1):
                cross.append(d)
            else:
                inner.append(d)
    ratio = float(np.mean(cross) / np.mean(inner))
    Lb = L.copy()
    Lb[1:-1, 1:-1] = sum(L[1 + dy:fh - 1 + dy, 1 + dx:fw - 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1)) / 9
    # 띠 어긋남: 칸 변마다 양쪽 약 3px 띠의 색 줄(변 방향 1px 칸으로 묶은 Lab 평균)을 -3..3px 밀어 가장 잘 맞는 이동량
    prof = {}
    for Y in range(fh):
        for X in range(fw):
            if not mask_fn(X, Y):
                continue
            dx, dy = X + 0.5 - 24, Y + 0.5 - 12
            a, b = (dx / 24 - dy / 12) / 2, (dx / 24 + dy / 12) / 2
            for axis, u, v in (("ne", a, b), ("se", b, a)):
                k = math.floor(u + 0.5)
                for edge in (k - 0.5, k + 0.5, k + 0.0):  # k+0.0 = 칸 가운데 가짜 변(같은 측정의 바탕값)
                    off = u - edge
                    if abs(off) < 0.07:
                        side = 0 if off < 0 else 1
                        key = (axis, edge, math.floor(v + 0.5))
                        bin_ = int(math.floor((v - math.floor(v + 0.5) + 0.5) * 27))
                        prof.setdefault(key, [{}, {}])[side].setdefault(bin_, []).append(Lb[Y, X])
    shifts, pseudo = [], []
    for key, (p0, p1) in prof.items():
        bins = sorted(set(p0) & set(p1))
        if len(bins) < 16:
            continue
        a0 = np.array([np.mean(p0[q], 0) for q in bins])
        a1 = np.array([np.mean(p1[q], 0) for q in bins])
        def cost(sh):
            if sh >= 0:
                return np.mean(np.linalg.norm(a0[sh:] - a1[:len(a1) - sh], axis=1))
            return np.mean(np.linalg.norm(a0[:sh] - a1[-sh:], axis=1))
        best = min(range(-3, 4), key=lambda sh: (cost(sh), abs(sh)))
        (pseudo if key[1] == math.floor(key[1]) else shifts).append(abs(best))
    return ratio, int(max(shifts)), float(np.median(shifts)), len(shifts), float(np.median(pseudo)), float(np.mean(shifts)), float(np.mean(pseudo))


def tile_id(X, Y):
    dx, dy = X + 0.5 - 24, Y + 0.5 - 12
    a, b = (dx / 24 - dy / 12) / 2, (dx / 24 + dy / 12) / 2
    return (math.floor(a + 0.5), math.floor(b + 0.5))


def main():
    work, outd, s, strength = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
    g = np.array(Image.open(f"{work}/{s}/g128.png").convert("RGBA"))
    d = np.array(Image.open(f"{work}/{s}/d128.png").convert("RGBA"))
    grass = lattice_tile(g, (40, 52), seed=11)
    dirt = lattice_tile(d, (40, 52), seed=12)
    edge = None if strength == 1 else hexrgb("#6f6c46") if strength == 3 else hexrgb("#a1985d")
    path = path_tile(grass, dirt, edge, strength, seed=13)
    Image.fromarray(grass).save(f"{outd}/grass.png")
    Image.fromarray(path).save(f"{outd}/path_ne.png")

    path_row = 13  # 중심선 x+2y=672: 왼쪽 아래(0,336) → 오른쪽 위(640,16), 원정대 자리를 지난다
    field = render_field(grass, path, path_row)
    Image.fromarray(field).save(f"{outd}/field_ground_only.png")

    # 측정: 바닥만 깐 들판
    floor_rows = (22, 283)  # HUD 사이(캡처 66px/3 ~ 850px/3)
    inside = lambda X, Y: floor_rows[0] <= Y < floor_rows[1]
    ratio, smax, smed, nseg, spseudo, smean, spmean = seam_measure(field, inside)
    # 대조군: 같은 원천에서 마름모를 그냥 잘라 깐 들판(이음 처리 없음)
    naive = np.zeros_like(grass)
    for y in range(H):
        for x in range(W):
            a, b = ab(x, y)
            if max(abs(a), abs(b)) < 0.5:
                naive[y, x] = g[(g.shape[0] - H) // 2 + y, (g.shape[1] - W) // 2 + x]
    nfield = render_field(naive, naive, -999)
    Image.fromarray(nfield).save(f"{outd}/control_naive_crop_field.png")
    nratio, nsmax, nsmed, _, _, nsmean, _ = seam_measure(nfield, inside)
    gfield = render_field(grass, grass, -999)
    gratio, gsmax, gsmed, _, _, gsmean, _ = seam_measure(gfield, inside)
    gpix = grass[grass[..., 3] > 0][:, :3].astype(float)
    dpix = path[(path[..., 3] > 0) & np.any(path[..., :3] != grass[..., :3], -1)][:, :3].astype(float)
    k7g = lab(np.array([hexrgb(h) for h in ("#c2ad5f", "#a1985d", "#d6c074")], float))
    k7d = lab(np.array([hexrgb(h) for h in ("#e4b87d", "#cfa36c")], float))
    gl, dl = rep_color(gpix), rep_color(dpix)
    de_g = float(min(np.linalg.norm(k7g - gl, axis=1)))
    gmean, dmean = lab(gpix).mean(0), lab(dpix).mean(0)
    de_gm = float(min(np.linalg.norm(k7g - gmean, axis=1)))
    de_dm = float(min(np.linalg.norm(k7d - dmean, axis=1)))
    de_d = float(min(np.linalg.norm(k7d - dl, axis=1)))
    floor_hue = hue(np.median(gpix, 0))
    mons = {}
    for m in MONS:
        spr = np.array(Image.open(f"{ART}/Monsters/{m}/{m}.png").convert("RGBA"))
        px = spr[spr[..., 3] > 0][:, :3].astype(float)
        lum = (px * np.array([0.299, 0.587, 0.114])).sum(1)
        px = px[lum > 60]  # 외곽선(어두운 갈색) 제외
        mh = hue(np.median(px, 0))
        dh = abs(mh - floor_hue)
        mons[m] = {"hue": round(mh, 1), "dhue": round(min(dh, 360 - dh), 1)}

    # 합성: 최신 게임 캡처(3배)를 640x360으로 내려 HUD는 그대로 두고 바닥 자리만 바꾼다
    cap = np.array(Image.open(CAP).convert("RGBA").resize((640, 360), Image.NEAREST))
    comp = cap.copy()
    comp[floor_rows[0]:floor_rows[1]] = field[floor_rows[0]:floor_rows[1]]
    sprites = []
    for n, (cx, by) in zip(CHARS, [(300, 165), (330, 172), (298, 188), (328, 195)]):
        sprites.append((by, f"{ART}/Characters/{n}/front/{n}_front.png", cx))
    for (m, cx, by) in [("slime", 205, 120), ("mushroom", 450, 140), ("boar", 420, 250), ("slime", 180, 230), ("mushroom", 120, 160), ("slime", 520, 215)]:
        sprites.append((by, f"{ART}/Monsters/{m}/{m}.png", cx))
    for by, p, cx in sorted(sprites):  # 겹 순서 3.2: 화면 Y로 앞뒤
        spr = np.array(Image.open(p).convert("RGBA"))
        # 캔버스 64x64, 피벗 = 바닥 가운데(32,63)
        paste(comp, spr, cx, by)
    Image.fromarray(comp).save(f"{outd}/field.png")

    json.dump({
        "strength": strength,
        "C5_seam_contrast_ratio": round(ratio, 3), "C5_grass_only_ratio": round(gratio, 3), "C5_grass_only_shift_max": gsmax, "C5_grass_only_shift_median": gsmed, "C5_control_naive_ratio": round(nratio, 3), "C5_control_naive_shift_max": nsmax, "C5_control_naive_shift_median": nsmed,
        "C5_band_shift_px_max": smax, "C5_band_shift_px_median": smed, "C5_edges_measured": nseg, "C5_band_shift_px_mean": round(smean, 2), "C5_pseudo_edge_shift_median": spseudo, "C5_pseudo_edge_shift_mean": round(spmean, 2), "C5_grass_only_shift_mean": round(gsmean, 2), "C5_control_naive_shift_mean": round(nsmean, 2),
        "C3_grass_lab": [round(v, 1) for v in gl], "C3_grass_dE_to_K7": round(de_g, 1),
        "C3_dirt_lab": [round(v, 1) for v in dl], "C3_dirt_dE_to_K7": round(de_d, 1), "C3_grass_mean_dE_to_K7": round(de_gm, 1), "C3_dirt_mean_dE_to_K7": round(de_dm, 1),
        "floor_hue": round(floor_hue, 1), "C4_monsters": mons,
    }, open(f"{outd}/measure.json", "w"), ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
