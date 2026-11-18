"""바닥 풀 마름모 타일 변형 4장 조립·합성·측정 (대기열 20261117-art-prod 항목 ground-grass).

견본 조립(docs/art/samples/ground/build_ground.py)의 마름모 격자 주기 조립(lattice_tile)을 그대로 쓴다.
변형 4장은 테두리 띠를 같은 바탕 타일에서 가져오고(그래서 어떤 두 장이 맞닿아도 변 픽셀이 견본처럼 이어진다),
안쪽만 원천의 다른 위치에서 가져온다(견본 보고 '남긴 것': 원천 위치를 바꿔 변형을 만든다). 바탕↔안쪽 넘어감은
마름모 거리 r에 따른 픽셀별 확률 선택(디더)이라 안쪽에 새 경계선이 생기지 않는다. 새 색을 만들지 않는다.

사용: /usr/bin/python3 docs/art/sheets/ground-grass-build.py <원천 128.png> <타일 출력 폴더> <작업 폴더> [<버림 원천 128.png> ...]
"""
import json
import math
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, "docs/art/samples/ground")
import build_ground as bg  # noqa: E402

W, H = bg.W, bg.H
CAP = "/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/.director/evidence/20261117-screen-fixes/2/1/art-flows/10-MON-1920x1080.png"
ART = "Assets/Game/03.Resources/Art/AIGenerated"
FLOOR = (22, 307)  # 캡처 HUD 사이(66px/3 ~ 923px/3)
CROP = 64  # 원천에서 잘라 쓰는 창(조립 창 x -6..54, y -3..27이 이 안에 든다 — 감싸 읽기 없음)
CORNERS = [(0, 0, True), (1, 1, False), (1, 0, True)]  # 변형 2~4의 안쪽 창 자리(원천 모서리 쪽, 좌우 뒤집기 여부), 변형 1은 바탕 그대로
K7_GRASS = ("#c2ad5f", "#a1985d", "#d6c074", "#b3a954", "#918c4b", "#70683e")


def radius(x, y):
    a, b = bg.ab(x, y)
    return 2 * max(abs(a), abs(b))


def crop(src, fx, fy, flip=False):
    if flip:
        src = src[:, ::-1]
    x0 = int(round((src.shape[1] - CROP) * fx))
    y0 = int(round((src.shape[0] - CROP) * fy))
    return src[y0:y0 + CROP, x0:x0 + CROP]


def variant(src, base, corner, seed):
    if corner is None:
        return base.copy()
    inner = bg.lattice_tile(crop(src, *corner), None, seed)
    rng = np.random.default_rng(seed + 100)
    out = base.copy()
    for y in range(H):
        for x in range(W):
            if base[y, x, 3] == 0:
                continue
            w = min(1.0, max(0.0, (0.85 - radius(x, y)) / 0.12))
            if rng.random() < w:
                out[y, x] = inner[y, x]
    return out


def make_tiles(src):
    base = bg.lattice_tile(crop(src, 0.5, 0.5), None, 11)
    return [variant(src, base, c, 20 + k) for k, c in enumerate([None] + CORNERS)]


def render_mixed(tiles, seed):
    rng = np.random.default_rng(seed)
    choice = {}
    fw, fh = 640, 360
    img = np.zeros((fh, fw, 4), np.uint8)
    for Y in range(fh):
        for X in range(fw):
            dx, dy = X + 0.5 - 24, Y + 0.5 - 12
            a, b = (dx / 24 - dy / 12) / 2, (dx / 24 + dy / 12) / 2
            j, i = math.floor(a + 0.5), math.floor(b + 0.5)
            if (i, j) not in choice:
                choice[(i, j)] = int(rng.integers(len(tiles)))
            img[Y, X] = tiles[choice[(i, j)]][Y - 12 * (i - j), X - 24 * (i + j)]
    return img


def measure(field, tiles):
    inside = lambda X, Y: FLOOR[0] <= Y < FLOOR[1]
    ratio, smax, smed, nseg, spseudo, smean, spmean = bg.seam_measure(field, inside)
    pix = np.concatenate([t[t[..., 3] > 0][:, :3] for t in tiles]).astype(float)
    rep = bg.rep_color(pix)
    k7 = bg.lab(np.array([bg.hexrgb(h) for h in K7_GRASS], float))
    de = np.linalg.norm(k7 - rep, axis=1)
    fpix = field[FLOOR[0]:FLOOR[1]].reshape(-1, 4)[:, :3].astype(float)
    frep = bg.rep_color(fpix)
    fde = np.linalg.norm(k7 - frep, axis=1)
    return {
        "C5_band_shift_px_max": smax, "C5_band_shift_px_median": smed, "C5_band_shift_px_mean": round(smean, 2),
        "C5_edges_measured": nseg, "C5_pseudo_edge_shift_median": spseudo, "C5_pseudo_edge_shift_mean": round(spmean, 2),
        "C5_seam_contrast_ratio": round(ratio, 3),
        "C3_tiles_lab": [round(v, 1) for v in rep], "C3_tiles_dE_to_nearest_K7": round(float(de.min()), 1),
        "C3_tiles_nearest_K7": K7_GRASS[int(de.argmin())],
        "C3_field_lab": [round(v, 1) for v in frep], "C3_field_dE_to_nearest_K7": round(float(fde.min()), 1),
        "share_by_colour": {h: round(float(np.mean(np.all(pix == bg.hexrgb(h), 1))), 3)
                            for h in sorted({"#%02x%02x%02x" % tuple(int(v) for v in p) for p in pix})},
    }


def main():
    srcp, outd, work = sys.argv[1], sys.argv[2], sys.argv[3]
    src = np.array(Image.open(srcp).convert("RGBA"))
    tiles = make_tiles(src)
    for k, t in enumerate(tiles):
        Image.fromarray(t).save(f"{outd}/grass_{k + 1:02d}.png")
    field = render_mixed(tiles, 7)
    Image.fromarray(field).save(f"{work}/field_ground_only.png")
    result = {"source": srcp, "crop": CROP, "corners": CORNERS, "adopted": measure(field, tiles)}
    # 대조: 한 장만 깐 들판(견본 방식)
    one = render_mixed(tiles[:1], 7)
    Image.fromarray(one).save(f"{work}/field_one_tile.png")
    result["control_one_tile"] = {k: v for k, v in measure(one, tiles[:1]).items() if k.startswith("C5")}
    # 버린 원천으로 만든 변형(시트용)
    for n, rp in enumerate(sys.argv[4:]):
        rt = make_tiles(np.array(Image.open(rp).convert("RGBA")))
        for k, t in enumerate(rt):
            Image.fromarray(t).save(f"{work}/rejected_{n + 1}_{k + 1:02d}.png")
        Image.fromarray(render_mixed(rt, 7)).save(f"{work}/rejected_{n + 1}_field.png")
    # 합성: 최신 전투 캡처를 640x360으로 내려 HUD는 두고 바닥 자리만 바꾼 뒤 캐릭터 4·몬스터 3
    cap = np.array(Image.open(CAP).convert("RGBA").resize((640, 360), Image.NEAREST))
    comp = cap.copy()
    comp[FLOOR[0]:FLOOR[1]] = field[FLOOR[0]:FLOOR[1]]
    sprites = []
    for n, (cx, by) in zip(["bruno", "lena", "mira", "bolt"], [(305, 165), (335, 165), (305, 198), (335, 198)]):
        sprites.append((by, f"{ART}/Characters/{n}/front/{n}_front.png", cx))
    for m, cx, by in [("slime", 228, 88), ("boar", 430, 256), ("mushroom", 65, 290)]:
        sprites.append((by, f"{ART}/Monsters/{m}/{m}.png", cx))
    for by, p, cx in sorted(sprites):
        bg.paste(comp, np.array(Image.open(p).convert("RGBA")), cx, by)
    Image.fromarray(comp).save(f"{work}/composite_640.png")
    json.dump(result, open(f"{work}/measure.json", "w"), ensure_ascii=False, indent=1)
    print(json.dumps(result["adopted"], ensure_ascii=False)[:600])
    print("control_one_tile", result["control_one_tile"])


if __name__ == "__main__":
    main()
