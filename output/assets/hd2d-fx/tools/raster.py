# HD-2D 효과 — 크기·방향이 게임 데이터로 바뀌는 도트 그림을 "화면 픽셀 공간"에서 그리는 기준 구현.
# Unity 쪽 순수 함수(설계 docs/features/hd2d-fx/design.md 4절 GroundMarkRaster·ChainBoltRaster)가 이 결과와 같아야 한다.
# 규칙: 1 텍셀 = 화면 1 도트(세운 판 1/48 m, 눕힌 판 가로 1/48 m × 깊이 1/24 m). 변환 회전·소수 배율 없음. 알파는 0/255.
# 사용: /usr/bin/python3 raster.py all <out_dir>   |   /usr/bin/python3 raster.py selftest
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

PPU = 48
SIN_TILT = 0.5  # 30°


def hex_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def new(w, h):
    return np.zeros((h, w, 4), dtype=np.uint8)


def put(img, x, y, col):
    h, w = img.shape[:2]
    if 0 <= x < w and 0 <= y < h:
        img[y, x] = (*hex_rgb(col), 255)


def save(img, path):
    Image.fromarray(img, 'RGBA').save(path)


# ---------- 바닥 타원 고리 (눕힌 판) ----------

def ellipse_axes(radius_m):
    """바닥 반지름(m) -> 화면 반축(px). 가로 = r·48, 세로 = r·48·sinθ."""
    return radius_m * PPU, radius_m * PPU * SIN_TILT


def ring_mask(a, b, thick, size=None):
    """반축 a·b(px)의 타원 고리. 픽셀 중심 판정, 안쪽 타원은 (a-thick, b-thick) → 위아래도 thick 도트 두께."""
    w = size[0] if size else int(math.ceil(a)) * 2 + 2
    h = size[1] if size else int(math.ceil(b)) * 2 + 2
    cx, cy = w / 2, h / 2
    ys, xs = np.mgrid[0:h, 0:w]
    dx, dy = xs + 0.5 - cx, ys + 0.5 - cy
    outer = (dx / a) ** 2 + (dy / b) ** 2 <= 1.0
    ai, bi = max(a - thick, 0.01), max(b - thick, 0.01)
    inner = (dx / ai) ** 2 + (dy / bi) ** 2 <= 1.0
    ang = (np.degrees(np.arctan2(dy / b, dx / a)) + 360) % 360
    return outer & ~inner, outer, ang, (w, h)


def ground_ring_frames(kind, radius_m):
    """r003: 3도트 띠(어두운 테/몸/밝은 심), 종류별 모양. 픽셀 중심·고정 캔버스."""
    if not math.isfinite(radius_m) or radius_m <= 0:
        raise ValueError('radius_m must be finite and positive')
    a, b = ellipse_axes(radius_m)
    size = (int(math.ceil(a)) * 2 + 4, int(math.ceil(b)) * 2 + 4)
    frames = []
    if kind not in ('heal', 'pulse', 'boom', 'blast'):
        raise ValueError(kind)
    steps = {'heal': (.55, .8, 1., 1., 1.), 'pulse': (.3, .55, .8, 1., 1.),
             'boom': (.5, .8, 1., 1.), 'blast': (1., 1.)}[kind]
    colors = {'heal': ('#7fa040', '#a8c828', '#ffe08a'),
              'pulse': ('#7a4a9e', '#b07fd0', '#ffffff'),
              'boom': ('#77211d', '#c44334', '#e4b67e'),
              'blast': ('#77211d', '#c44334', '#e4b67e')}
    for k, scale in enumerate(steps):
        img = new(*size)
        aa, bb = a * scale, b * scale
        _, outer, ang, _ = ring_mask(aa, bb, 3, size)
        ys, xs = np.mgrid[0:size[1], 0:size[0]]
        # 종류마다 모양도 바꾼다. 색만 다른 고리를 만들지 않는다.
        if kind == 'heal':
            keep = (ang % 90) >= (2 if k < 3 else (18 if k == 3 else 58))
        elif kind == 'pulse':
            keep = (ang % 45) >= (3 if k < 4 else 22)
        else:
            keep = (ang % 24) >= (5 if kind == 'blast' or k < 2 else 12)
        for band in range(3):
            ring, _, _, _ = ring_mask(max(.1, aa - band), max(.1, bb - band), 1, size)
            col = colors[kind][band]
            if kind == 'pulse' and k == 3 and band == 2:
                col = '#7fd0c8'
            if kind == 'boom' and k == 3:
                col = ('#77211d', '#774c3b', '#c44334')[band]
            img[ring & keep] = (*hex_rgb(col), 255)
        if kind == 'heal':
            # 네 잎 + 십자 반짝임. 파동·폭발과 실루엣이 다르다.
            for i, deg in enumerate((25, 115, 205, 295)):
                if k == 4 and i % 2:
                    continue
                r = math.radians(deg + k * 4)
                px = int(size[0] / 2 + math.cos(r) * max(1, aa - 4))
                py = int(size[1] / 2 + math.sin(r) * max(1, bb - 4))
                for dy in range(-2, 3):
                    for dx in range(-2, 3):
                        if abs(dx + dy) <= 1 and abs(dx - dy) <= 3:
                            put(img, px + dx, py + dy, '#a8c828' if dx + dy else '#7fa040')
                if k < 4:
                    for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                        put(img, px + dx + 4, py + dy - 2, '#ffffff')
        elif kind == 'pulse':
            # 뒤따르는 두 번째 파동. 첫 장부터 겹 고리로 읽힌다.
            ring, _, ang2, _ = ring_mask(max(.1, aa - 5), max(.1, bb - 5), 2, size)
            keep2 = (ang2 % 45) >= (7 if k < 4 else 25)
            img[ring & keep2] = (*hex_rgb('#7fd0c8' if k < 4 else '#7a4a9e'), 255)
        else:
            # 각 섹터의 바깥 테에 삼각 톱니 + 안쪽 점선.
            tooth, _, _, _ = ring_mask(aa, bb, 5, size)
            tooth &= ((ang + k * 2) % 24) < 5
            img[tooth] = (*hex_rgb('#c44334' if k % 2 == 0 else '#77211d'), 255)
            dash, _, ang2, _ = ring_mask(max(.1, aa - 6), max(.1, bb - 6), 1, size)
            img[dash & ((ang2 % 24) < 7)] = (*hex_rgb('#e4b67e' if k < 3 else '#774c3b'), 255)
            if kind == 'blast':
                fill = outer & (img[..., 3] == 0) & ((xs + 2 * ys + k * 2) % 8 == 0) & (ys % 2 == 0)
                img[fill] = (*hex_rgb('#77211d'), 255)
        frames.append(img)
    return frames


# ---------- 돌진 선 (눕힌 판, 방향은 그림에 굽는다) ----------

def dash_line_frames(length_m, floor_dir_deg, frames=3, half_width=5):
    """바닥 방향 각도(0 = +x, 반시계)와 길이(m) -> 화면 공간 '돌진 길' 예고. 판은 회전하지 않고 시작점(몬스터 발)이 피벗.
    r002(판독 r001 #12 "화염 투사체로 읽힘" 고침): 꼬리 같은 한 줄 대신 양옆 테 두 줄(길 폭) + 안쪽 갈매기가 앞으로 흐르고 끝에 큰 화살촉."""
    if not math.isfinite(length_m) or length_m <= 0 or not math.isfinite(floor_dir_deg):
        raise ValueError('dash length must be finite and positive; angle must be finite')
    r = math.radians(floor_dir_deg)
    sx, sy = math.cos(r) * length_m * PPU, math.sin(r) * length_m * PPU * SIN_TILT  # 화면 px(위 = +y)
    L = math.hypot(sx, sy)
    ux, uy = sx / L, sy / L
    pad = half_width + 8
    w = int(math.ceil(abs(sx))) + pad * 2
    h = int(math.ceil(abs(sy))) + pad * 2
    ox = pad if sx >= 0 else w - pad
    oy = pad if sy >= 0 else h - pad
    head = 9
    out = []
    for k in range(frames):
        img = new(w, h)
        for y in range(h):
            for x in range(w):
                px, py = x + 0.5 - ox, (h - 1 - y) + 0.5 - oy
                along = px * ux + py * uy
                perp = -px * uy + py * ux
                if along < 0 or along > L - head:
                    continue
                ap = abs(perp)
                if half_width - 1 <= ap <= half_width:
                    # 테: 6 도트 칠하고 2 도트 비우는 점선이 앞으로 흐른다
                    if (along - k * 3) % 8 < 6:
                        put(img, x, y, '#77211d')
                elif ap < half_width - 2:
                    phase = (along + ap * 1.4 - k * 3) % 9
                    if phase < 2.2:
                        put(img, x, y, '#c44334' if ap < 1.5 else '#c44334')
        for y in range(h):  # 화살촉(빈틈 없는 삼각형): 길 폭보다 넓다
            for x in range(w):
                px, py = x + 0.5 - ox, (h - 1 - y) + 0.5 - oy
                along = px * ux + py * uy
                perp = -px * uy + py * ux
                lim = (L + 2 - along) * 0.8
                if L - head - 2 <= along <= L + 2 and abs(perp) <= lim:
                    edge = abs(perp) >= lim - 1.2 or along < L - head - 0.8
                    put(img, x, y, '#77211d' if edge else ('#e4b67e' if k == 1 else '#c44334'))
        out.append(img)
    return out, (ox / w, oy / h)


# ---------- 번개 연결 (세운 판, 화면 공간 꺾은선) ----------

def chain_bolt_frames(dx_px, dy_px, seed=7, frames=3):
    """r003: 2도트 흰 심 + 색 테 + 두 갈래와 끝 불꽃. xorshift32는 Unity에서 그대로 옮길 수 있다."""
    if dx_px == 0 and dy_px == 0:
        raise ValueError('chain endpoints must differ')
    state = (int(seed) & 0xffffffff) or 1
    def jitter():
        nonlocal state
        state ^= (state << 13) & 0xffffffff
        state ^= state >> 17
        state ^= (state << 5) & 0xffffffff
        state &= 0xffffffff
        return state % 9 - 4
    length = math.hypot(dx_px, dy_px)
    n = max(2, int(length // 10))
    pad = 10
    w, h = int(abs(dx_px)) + pad * 2, int(abs(dy_px)) + pad * 2
    ox = pad if dx_px >= 0 else w - pad
    oy = pad if dy_px >= 0 else h - pad
    nx, ny = -dy_px / length, dx_px / length
    out = []
    def line(core, start, end, thick):
        x0, y0 = start
        x1, y1 = end
        steps = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for step in range(steps + 1):
            x = int(round(x0 + (x1 - x0) * step / steps))
            y = int(round(y0 + (y1 - y0) * step / steps))
            core.add((x, y))
            if thick:
                core.add((x, y + 1) if abs(dx_px) >= abs(dy_px) else (x + 1, y))
    for k in range(frames):
        points = []
        for i in range(n + 1):
            t = i / n
            j = 0 if i in (0, n) else jitter()
            points.append((ox + dx_px * t + nx * j, oy + dy_px * t + ny * j))
        core = set()
        for start, end in zip(points, points[1:]):
            line(core, start, end, True)
        for i, side in ((n // 3, 1), (2 * n // 3, -1)):
            x, y = points[i]
            tip = (x + dx_px / length * 5 + nx * 6 * side,
                   y + dy_px / length * 5 + ny * 6 * side)
            line(core, (x, y), tip, False)
        # Two compact endpoint sparks stay within 1 texel of the requested anchors.
        for x, y in (points[0], points[-1]):
            cx, cy = int(round(x)), int(round(y))
            core.update((cx + ddx, cy + ddy) for ddx, ddy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)))
        img = new(w, h)
        for x, y in sorted(core):
            for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if (x + ddx, y + ddy) not in core:
                    put(img, x + ddx, h - 1 - y - ddy, '#b07fd0' if k != 1 else '#7fd0c8')
        for x, y in sorted(core):
            put(img, x, h - 1 - y, '#ffffff')
        if k == frames - 1:
            ys, xs = np.nonzero(img[..., 3])
            # Break the middle only, preserving both endpoint sparks.
            away = (np.hypot(xs - ox, h - 1 - ys - oy) > 3) & (np.hypot(xs - ox - dx_px, h - 1 - ys - oy - dy_px) > 3)
            img[ys[away & ((xs // 3 + ys // 3) % 3 == 0)], xs[away & ((xs // 3 + ys // 3) % 3 == 0)]] = 0
        out.append(img)
    return out


# ---------- 투사체 방향 굽기 (세운 판, 0/45/90°) ----------

def bake_directions(src_path):
    """오른쪽을 보는 원본에서 45°·90° 그림을 최근접으로 굽고 외톨이 도트를 지운다(실행 중 Z 회전을 대신)."""
    im = Image.open(src_path).convert('RGBA')
    outs = {'right': im}
    for name, deg in (('upright', 45), ('up', 90)):
        big = im.resize((im.width * 4, im.height * 4), Image.NEAREST)
        rot = big.rotate(deg, resample=Image.NEAREST, expand=True)
        w = max(1, round(rot.width / 4))
        h = max(1, round(rot.height / 4))
        small = rot.resize((w, h), Image.NEAREST)
        a = np.array(small)
        a[..., 3] = np.where(a[..., 3] >= 128, 255, 0)
        al = a[..., 3] > 0
        nb = np.zeros_like(al, dtype=int)
        for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            nb += np.roll(np.roll(al, ddy, 0), ddx, 1)
        a[(al) & (nb == 0)] = 0
        outs[name] = Image.fromarray(a, 'RGBA')  # 자르기는 부르는 쪽이 프레임 합집합으로(피벗 고정)
    return outs


def shadow_dot(w=8, h=4):
    """투사체·적 탄 아래 눕힌 그림자(화면 공간 8×4). 색은 몬스터 그림자 판(shadow.png)과 같은 #34140e."""
    _, outer, _, _ = ring_mask(w / 2, h / 2, 99, (w, h))
    img = new(w, h)
    img[outer] = (*hex_rgb('#34140e'), 255)
    return img


def hit_star_frames():
    """r003: 20x20 캔버스, 정점 실루엣 16도트(48도트 인물 키의 1/3). 단발 3장."""
    out = []
    for k, reach in enumerate((4, 8, 8)):
        img = new(20, 20)
        for y in range(20):
            for x in range(20):
                dx, dy = abs(x - 9.5), abs(y - 9.5)
                if k < 2:
                    # 긴 십자 끝과 가운데 다이아몬드. 불투명 도트만 사용.
                    inside = max(dx, dy) < reach and (min(dx, dy) < 1 or dx + dy < reach * .7)
                    if inside:
                        edge = (dx + dy >= reach * .7 - 1) and min(dx, dy) >= 1
                        put(img, x, y, '#ffe08a' if edge else '#ffffff')
                    if k == 1 and 4 <= dx < 6 and 4 <= dy < 6 and dx == dy:
                        put(img, x, y, '#f8c838')
                elif 4 <= max(dx, dy) < 8 and min(dx, dy) < 1:
                    put(img, x, y, '#fff9ed' if max(dx, dy) < 6 else '#ffe08a')
                elif k == 2 and 4 <= dx < 6 and 4 <= dy < 6 and dx == dy:
                    put(img, x, y, '#f8c838')
        out.append(img)
    return out


# ---------- 실행 ----------

def selftest():
    ok = True

    def case(label, cond):
        nonlocal ok
        ok &= bool(cond)
        print(('PASS ' if cond else 'FAIL ') + label)

    a, b = ellipse_axes(1.0)
    case('반지름 1 m -> 반축 48×24 px', (a, b) == (48, 24))
    ring, outer, _, size = ring_mask(48, 24, 2)
    ys, xs = np.nonzero(ring)
    case('고리 폭 96 px, 높이 48 px', xs.max() - xs.min() + 1 == 96 and ys.max() - ys.min() + 1 == 48)
    mid = ring[size[1] // 2]
    case('가로 테 두께 2 도트', int(mid[:size[0] // 2].sum()) == 2)
    col = ring[:, size[0] // 2]
    case('세로 테 두께 2 도트(눌림 없음)', int(col[:size[1] // 2].sum()) == 2)
    fr = ground_ring_frames('heal', 1.0)
    case('회복 고리 5장 같은 캔버스', len(fr) == 5 and len({f.shape for f in fr}) == 1)
    case('알파 0/255만', all(set(np.unique(f[..., 3])) <= {0, 255} for f in fr))
    lines, piv = dash_line_frames(3.6, 30)
    case('돌진 선 3장, 피벗 왼쪽 아래', len(lines) == 3 and piv[0] < 0.2 and piv[1] < 0.3)
    bolt = chain_bolt_frames(120, 30)
    case('번개 3장 흰 심 있음', len(bolt) == 3 and (bolt[0][..., 0] == 255).any())
    star = hit_star_frames()
    from PIL import Image
    box = Image.fromarray(star[1]).getbbox()
    case('피격 정점 16×16 = 인물 48도트 키의 1/3', box[2] - box[0] == 16 and box[3] - box[1] == 16)
    case('피격 시작도 8도트, 끝 장도 비지 않음', Image.fromarray(star[0]).getbbox() == (6, 6, 14, 14) and np.count_nonzero(star[2][..., 3]) > 0)
    for kind in ('heal', 'pulse', 'boom', 'blast'):
        pictures = ground_ring_frames(kind, 1.0)
        case(kind + ' 알파 이진·3색 이상·프레임마다 차이',
             all(set(np.unique(f[..., 3])) <= {0, 255} and len(np.unique(f[f[..., 3] > 0, :3], axis=0)) >= 3 for f in pictures)
             and all(not np.array_equal(a, b) for a, b in zip(pictures, pictures[1:])))
    for dx, dy in ((110, 28), (-110, 28), (110, -28), (-110, -28), (0, 80), (80, 0)):
        bolt = chain_bolt_frames(dx, dy)
        w, h = bolt[0].shape[1], bolt[0].shape[0]
        ox, oy = (10 if dx >= 0 else w - 10), (10 if dy >= 0 else h - 10)
        case('번개 끝점 보존 ' + str((dx, dy)), all(tuple(f[h - 1 - y, x]) == (255, 255, 255, 255)
             for f in bolt for x, y in ((ox, oy), (ox + dx, oy + dy))))
    case('번개 같은 시드의 픽셀 결정성', all(np.array_equal(a, b) for a, b in zip(chain_bolt_frames(110, 28, 7), chain_bolt_frames(110, 28, 7))))
    dash, _ = dash_line_frames(3.6, 35)
    dash_colors = set(tuple(c) for f in dash for c in f[f[..., 3] > 0, :3])
    case('돌진 길 원색 노랑·빨강 0', not dash_colors.intersection({hex_rgb('#f8d818'), hex_rgb('#d81808'), hex_rgb('#f88808')}))
    print('selftest', 'PASS' if ok else 'FAIL')
    return 0 if ok else 1


def run_all(out):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    meta = {}
    # 예시 반지름: 게임 데이터 값 × unitScale 0.0113 m (combat_rules rangeUnit 42, censer range 4 -> 168, orb 100, puffer 90, explosion 75)
    for kind, r in (('heal', 168 * 0.0113), ('pulse', 100 * 0.0113), ('blast', 90 * 0.0113), ('boom', 75 * 0.0113)):
        fr = ground_ring_frames(kind, r)
        for i, f in enumerate(fr):
            save(f, out / f'ground_{kind}_{i + 1}.png')
        meta[f'ground_{kind}'] = (len(fr), fr[0].shape[1], fr[0].shape[0], round(r, 3))
    lines, piv = dash_line_frames(460 * 0.7 * 0.0113, 35)
    for i, f in enumerate(lines):
        save(f, out / f'dash_line_{i + 1}.png')
    meta['dash_line'] = (len(lines), lines[0].shape[1], lines[0].shape[0], piv)
    bolt = chain_bolt_frames(110, 28)
    for i, f in enumerate(bolt):
        save(f, out / f'chain_bolt_{i + 1}.png')
    meta['chain_bolt'] = (len(bolt), bolt[0].shape[1], bolt[0].shape[0])
    save(shadow_dot(), out / 'shadow_dot.png')
    for k, v in meta.items():
        print(k, v)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'selftest':
        sys.exit(selftest())
    if len(sys.argv) > 2 and sys.argv[1] == 'all':
        run_all(sys.argv[2])
        sys.exit(0)
    if len(sys.argv) > 2 and sys.argv[1] == 'star':
        dest = Path(sys.argv[2]); dest.mkdir(parents=True, exist_ok=True)
        for i, frame in enumerate(hit_star_frames(), 1):
            save(frame, dest / f'hit_star_{i}.png')
        sys.exit(0)
    print(__doc__ or 'usage: raster.py all <out> | star <out> | selftest')
    sys.exit(2)
