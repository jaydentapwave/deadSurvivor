"""흙길 마름모 타일 9장 조립·지도 미리보기·합성·이음 측정 (대기열 20261117-art-prod 항목 ground-path).

견본 조립(docs/art/samples/ground/build_ground.py)의 흙길 방식을 그대로 쓴다: 풀 타일 위에 마름모 격자 주기 흙(lattice_tile)을
길 모양 띠로 얹고, 띠 바깥 1겹은 짙은 풀 #6f6c46 가장자리(견본 s3 강도 3).
- 풀 부분 = grass_01.png의 같은 자리 픽셀(그래서 풀 타일과 맞닿는 변이 이어진다).
- 흙 = 격자 주기 흙 타일(그래서 흙길 칸끼리 맞닿는 변이 이어진다).
- 길이 지나는 변에서는 띠 모양이 모든 타일에서 같다(변 가운데를 중심으로 반폭 HALF). 굴곡은 변에서 0이 되는 함수만 써서
  직선·꺾임·끝이 어떤 조합으로 맞닿아도 띠가 어긋나지 않는다.
마름모 좌표: a = ↗ 방향, b = ↘ 방향, 칸은 |a|,|b| < 0.5. 변: ↗ a=+0.5, ↘ b=+0.5, ↙ a=-0.5, ↖ b=-0.5.
꺾임 이름 = 길이 지나는 두 변이 만나는 꼭짓점(n = ↗+↖, e = ↗+↘, s = ↘+↙, w = ↙+↖). 띠 중심선은 그 꼭짓점을 중심으로 한 반지름 0.5의 호.
끝 이름 = 길이 이어지는 변. 띠는 그 변에서 칸 가운데(END_AT)까지 오고 둥글게 닫힌다.

사용: /usr/bin/python3 docs/art/sheets/ground-path-build.py <흙 원천.png> <타일 출력 폴더> <작업 폴더> [<버림 흙 원천.png> ...]
"""
import json
import math
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, "docs/art/samples/ground")
import build_ground as bg  # noqa: E402

W, H = bg.W, bg.H
ART = "Assets/Game/03.Resources/Art/AIGenerated"
GRASS = f"{ART}/Ground/grass"
CAP = "/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/.director/evidence/20261117-screen-fixes/2/1/art-flows/10-MON-1920x1080.png"
FLOOR = (22, 307)
HALF = 0.34       # 견본 s3와 같은 반폭
AMP = 0.05        # 견본 s3 굴곡 세기
EDGE = 0.045      # 가장자리 짙은 풀 두께(견본 s3)
EDGE_RGB = bg.hexrgb("#6f6c46")
END_AT = 0.0      # 끝 타일 띠가 멈추는 자리(칸 가운데)
VERTS = {"n": (0.5, -0.5), "e": (0.5, 0.5), "s": (-0.5, 0.5), "w": (-0.5, -0.5)}
# 끝: (축, 부호) — 축 0 = a, 1 = b
ENDS = {"ne": (0, 1), "se": (1, 1), "sw": (0, -1), "nw": (1, -1)}
NAMES = ["path_ne", "path_corner_n", "path_corner_e", "path_corner_s", "path_corner_w",
         "path_end_ne", "path_end_se", "path_end_sw", "path_end_nw"]


def wobble(s, k):
    """변(s=0, 1)에서 0이 되는 굴곡: 반폭 더하기, 중심 옮기기."""
    t = 2 * math.pi * s
    dh = AMP * (k[0] * math.sin(t) + 0.6 * k[1] * math.sin(2 * t))
    dc = AMP * 0.5 * k[2] * math.sin(t)
    return dh, dc


def band(name, a, b, k):
    """(중심선에서의 거리, 그 자리 반폭)."""
    if name == "path_ne":
        dh, dc = wobble(a + 0.5, k)
        return abs(b - dc), HALF + dh
    if name.startswith("path_corner_"):
        va, vb = VERTS[name[-1]]
        r = math.hypot(a - va, b - vb)
        ang = math.atan2(abs(b - vb), abs(a - va))  # 0..pi/2, 두 변 사이
        dh, dc = wobble(ang / (math.pi / 2), k)
        return abs(r - 0.5 - dc), HALF + dh
    ax, sg = ENDS[name[-2:]]
    u = sg * (a if ax == 0 else b)    # 이어지는 변이 u=+0.5
    v = b if ax == 0 else a
    s = 0.5 - u                       # 변에서 0
    if u >= END_AT:
        dh, dc = wobble(s / (2 * (0.5 - END_AT)), k)  # 변 0 → 멈추는 자리 0.5(반 주기)
        return abs(v - dc), HALF + dh
    return math.hypot(u - END_AT, v), HALF  # 둥근 끝(멈추는 자리에서 굴곡 0이 아닐 수 있어 반폭 기준값)


def make_tile(name, grass, dirt, k):
    out = grass.copy()
    for y in range(H):
        for x in range(W):
            if grass[y, x, 3] == 0:
                continue
            a, b = bg.ab(x, y)
            d, half = band(name, a, b, k)
            if d < half:
                out[y, x] = dirt[y, x]
            elif d < half + EDGE:
                out[y, x, :3] = EDGE_RGB
    return out


def make_tiles(dirt_src, grass):
    c = (dirt_src.shape[0] - 64) // 2
    dirt = bg.lattice_tile(dirt_src[c:c + 64, c:c + 64], None, 12)
    rng = np.random.default_rng(13)
    tiles = {}
    for n in NAMES:
        k = [rng.choice([-1, 1]) * rng.uniform(0.5, 1.0) for _ in range(3)]
        tiles[n] = make_tile(n, grass, dirt, k)
    return tiles


def pick(cells, i, j):
    """이웃 네 칸 중 길인 칸의 조합 → (타일 이름, 좌우 반전)."""
    ne, se, sw, nw = ((i, j + 1) in cells, (i + 1, j) in cells, (i, j - 1) in cells, (i - 1, j) in cells)
    key = (ne, se, sw, nw)
    table = {
        (1, 0, 1, 0): ("path_ne", False), (0, 1, 0, 1): ("path_ne", True),
        (1, 0, 0, 1): ("path_corner_n", False), (1, 1, 0, 0): ("path_corner_e", False),
        (0, 1, 1, 0): ("path_corner_s", False), (0, 0, 1, 1): ("path_corner_w", False),
        (1, 0, 0, 0): ("path_end_ne", False), (0, 1, 0, 0): ("path_end_se", False),
        (0, 0, 1, 0): ("path_end_sw", False), (0, 0, 0, 1): ("path_end_nw", False),
    }
    return table.get(tuple(int(v) for v in key))


def render(tiles, grass_tiles, cells, seed=7, fw=640, fh=360):
    rng = np.random.default_rng(seed)
    choice = {}
    img = np.zeros((fh, fw, 4), np.uint8)
    for Y in range(fh):
        for X in range(fw):
            dx, dy = X + 0.5 - 24, Y + 0.5 - 12
            a, b = (dx / 24 - dy / 12) / 2, (dx / 24 + dy / 12) / 2
            j, i = math.floor(a + 0.5), math.floor(b + 0.5)
            if (i, j) not in choice:
                if (i, j) in cells:
                    p = pick(cells, i, j)
                    if p is None:
                        raise SystemExit(f"[ground-path-build] no tile for cell {(i, j)}")
                    t = tiles[p[0]]
                    choice[(i, j)] = t[:, ::-1] if p[1] else t
                else:
                    choice[(i, j)] = grass_tiles[int(rng.integers(len(grass_tiles)))]
            img[Y, X] = choice[(i, j)][Y - 12 * (i - j), X - 24 * (i + j)]
    return img


def line(start, steps):
    cells = [start]
    for d, n in steps:
        for _ in range(n):
            i, j = cells[-1]
            cells.append((i + d[0], j + d[1]))
    return cells


NE, SE, SW, NW = (0, 1), (1, 0), (0, -1), (-1, 0)
# 합성: 왼쪽 아래 끝 → ↗ 4칸 → 꺾임 → ↘ 3칸 → 꺾임 → ↗ 화면 밖까지 (두 번 꺾이고 한쪽이 끝난다)
FIELD_PATH = line((12, -8), [(NE, 5), (SE, 4), (NE, 16)])
# 시험 지도: 고리(꺾임 4·직선 양 방향) + 짧은 막힌 길 둘(끝 4)
TEST_PATH = line((10, -4), [(NE, 4), (SE, 4), (SW, 4), (NW, 3)]) + [(9, 3), (9, 4), (16, -1), (17, -1)]


def seam_report(field, tiles_used_mask):
    inside = lambda X, Y: FLOOR[0] <= Y < FLOOR[1]
    ratio, smax, smed, nseg, spseudo, smean, spmean = bg.seam_measure(field, inside)
    return {"C5_seam_contrast_ratio": round(ratio, 3), "C5_band_shift_px_max": smax,
            "C5_band_shift_px_median": smed, "C5_band_shift_px_mean": round(smean, 2),
            "C5_edges_measured": nseg, "C5_pseudo_edge_shift_mean": round(spmean, 2)}


def edge_check(tiles, grass01):
    """변 대조: 맞닿는 두 칸의 변 픽셀이 같은 그림(격자 주기 원천)에서 왔는지 — 변 바로 안쪽 1px 줄끼리 같은 원천 위치인지 대신,
    길 타일이 grass_01과 다른 픽셀이 길 띠(+가장자리) 안에만 있는지, 길이 지나는 변의 띠 폭이 모든 타일에서 같은지 센다."""
    res = {}
    for n, t in tiles.items():
        diff = np.any(t != grass01, -1)
        res[n] = int(diff.sum())
    # 변 위 띠 위치: 각 변에서 흙/가장자리 픽셀 열 집합이 타일마다 같은가
    def edge_px(t, side):
        pts = set()
        for y in range(H):
            for x in range(W):
                if t[y, x, 3] == 0:
                    continue
                a, b = bg.ab(x, y)
                near = {"ne": a > 0.47, "se": b > 0.47, "sw": a < -0.47, "nw": b < -0.47}[side]
                if near and np.any(t[y, x] != grass01[y, x]):
                    pts.add((x, y))
        return pts
    ref = {"ne": edge_px(tiles["path_ne"], "ne"), "sw": edge_px(tiles["path_ne"], "sw")}
    flip = tiles["path_ne"][:, ::-1]
    ref["se"] = edge_px(flip, "se")
    ref["nw"] = edge_px(flip, "nw")
    users = {"ne": ["path_corner_n", "path_corner_e", "path_end_ne"], "se": ["path_corner_e", "path_corner_s", "path_end_se"],
             "sw": ["path_corner_s", "path_corner_w", "path_end_sw"], "nw": ["path_corner_w", "path_corner_n", "path_end_nw"]}
    mism = {}
    for side, ns in users.items():
        for n in ns:
            mism[f"{n}@{side}"] = len(edge_px(tiles[n], side) ^ ref[side])
    return res, mism


def main():
    srcp, outd, work = sys.argv[1], sys.argv[2], sys.argv[3]
    grass01 = np.array(Image.open(f"{GRASS}/grass_01.png").convert("RGBA"))
    grass_tiles = [np.array(Image.open(f"{GRASS}/grass_{k:02d}.png").convert("RGBA")) for k in range(1, 5)]
    tiles = make_tiles(np.array(Image.open(srcp).convert("RGBA")), grass01)
    for n, t in tiles.items():
        Image.fromarray(t).save(f"{outd}/{n}.png")
    changed, mism = edge_check(tiles, grass01)
    test = render(tiles, grass_tiles, set(TEST_PATH), fw=480, fh=270)
    Image.fromarray(test).save(f"{work}/test_map.png")
    field = render(tiles, grass_tiles, set(FIELD_PATH))
    Image.fromarray(field).save(f"{work}/field_ground_only.png")
    result = {"source": srcp, "edge_band_mismatch_px_vs_straight": mism, "px_differing_from_grass_01": changed,
              "field": seam_report(field, None), "control_grass_only": seam_report(render(tiles, grass_tiles, set()), None)}
    for n, rp in enumerate(sys.argv[4:]):
        rt = make_tiles(np.array(Image.open(rp).convert("RGBA")), grass01)
        Image.fromarray(rt["path_ne"]).save(f"{work}/rejected_{n + 1}_path_ne.png")
    cap = np.array(Image.open(CAP).convert("RGBA").resize((640, 360), Image.NEAREST))
    comp = cap.copy()
    comp[FLOOR[0]:FLOOR[1]] = field[FLOOR[0]:FLOOR[1]]
    sprites = []
    for n, (cx, by) in zip(["bruno", "lena", "mira", "bolt"], [(345, 165), (375, 165), (345, 198), (375, 198)]):
        sprites.append((by, f"{ART}/Characters/{n}/front/{n}_front.png", cx))
    for m, cx, by in [("slime", 470, 95), ("boar", 520, 270), ("mushroom", 90, 160)]:
        sprites.append((by, f"{ART}/Monsters/{m}/{m}.png", cx))
    for by, p, cx in sorted(sprites):
        bg.paste(comp, np.array(Image.open(p).convert("RGBA")), cx, by)
    Image.fromarray(comp).save(f"{work}/composite_640.png")
    json.dump(result, open(f"{work}/measure.json", "w"), ensure_ascii=False, indent=1)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
