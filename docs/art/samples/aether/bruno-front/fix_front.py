"""Aether→도트 프레임 손질(크레딧 0). 손질 기준: docs/art/samples/aether/손질-기준.md

usage: fix_front.py <params.json>
params.json이 기준 그림, 무기·방패 조각 좌표, 프레임마다의 원본 파일과 손질 값을 정한다.
순서(프레임마다): 64×64로 자르기(피벗 x32, 맨 아래 줄 63) → 옛 무기·궤적 지우기 → 머리 맞춤 이동 찾기 →
머리 둘레 지우기 → 기준 머리 덮기 → 방패 덮기 → 기준 손·칼(정수 변환 8가지 중 하나) 붙이기 → 저장.
정수 픽셀 이동·뒤집기·전치만 쓴다(재표본 없음). 새 색을 만들지 않는다(외곽선은 기준 그림의 #2f1108).
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[5]
OUTLINE = (0x2f, 0x11, 0x08)
# 정수 변환 8가지: (u, v) -> 새 (u, v)
D4 = {
    'id': lambda u, v: (u, v), 'flipx': lambda u, v: (-u, v), 'flipy': lambda u, v: (u, -v), 'rot180': lambda u, v: (-u, -v),
    'transpose': lambda u, v: (v, u), 'rot90': lambda u, v: (-v, u), 'rot270': lambda u, v: (v, -u), 'antitranspose': lambda u, v: (-v, -u),
}


def load(path):
    return np.array(Image.open(ROOT / path).convert('RGBA'))


def to64(src):
    """피벗 x32 그대로(왼쪽 맞춤), 아래 64줄. 잘려 나간 불투명 픽셀 수를 돌려준다."""
    h, w = src.shape[:2]
    out = np.zeros((64, 64, 4), np.uint8)
    oy = h - 64
    out[:, :min(w, 64)] = src[oy:, :64]
    lost = int((src[..., 3] > 0).sum() - (out[..., 3] > 0).sum())
    return out, oy, lost


def head_mask(base, p):
    m = np.zeros(base.shape[:2], bool)
    top, bottom, xcut, full = p['top'], p['bottom'], p['x_min_low'], p['full_rows_to']
    for y in range(top, bottom + 1):
        for x in range(64):
            if base[y, x, 3] and (y <= full or x >= xcut):
                m[y, x] = True
    return m


def find_shift(frame, base, mask, rng=6):
    ys, xs = np.nonzero(mask)
    best = None
    for dy in range(-rng, rng + 1):
        for dx in range(-rng, rng + 1):
            ty, tx = ys + dy, xs + dx
            ok = (ty >= 0) & (ty < 64) & (tx >= 0) & (tx < 64)
            f = frame[ty[ok], tx[ok]]
            b = base[ys[ok], xs[ok]]
            dist = np.abs(f[:, :3].astype(int) - b[:, :3].astype(int)).sum(1)
            score = int(((f[:, 3] > 0) & (dist < 90)).sum())
            if best is None or score > best[0]:
                best = (score, dx, dy)
    return best[1], best[2]


def seg_erase(frame, a, b, r):
    (x0, y0), (x1, y1) = a, b
    vx, vy = x1 - x0, y1 - y0
    ll = vx * vx + vy * vy
    n = 0
    for y in range(64):
        for x in range(64):
            if not frame[y, x, 3]:
                continue
            t = max(0.0, min(1.0, ((x - x0) * vx + (y - y0) * vy) / ll))
            if (x - x0 - t * vx) ** 2 + (y - y0 - t * vy) ** 2 <= r * r:
                frame[y, x] = 0
                n += 1
    return n


def in_boxes(x, y, boxes):
    return any(b[0] <= x <= b[2] and b[1] <= y <= b[3] for b in boxes)


def paste(frame, pix, keep_under=False):
    """pix: [(x, y, rgba)]. keep_under=True면 빈 칸에만 찍는다(뒤쪽에 있는 것)."""
    for x, y, c in pix:
        if 0 <= x < 64 and 0 <= y < 64:
            if keep_under and frame[y, x, 3]:
                continue
            frame[y, x] = c


def patch_pixels(base, mask, dx, dy):
    ys, xs = np.nonzero(mask)
    return [(int(x) + dx, int(y) + dy, base[y, x].copy()) for y, x in zip(ys, xs)]


def weapon_pixels(weapon, pivot, tf, at):
    ys, xs = np.nonzero(weapon[..., 3])
    out = []
    for y, x in zip(ys, xs):
        u, v = D4[tf](int(x) - pivot[0], int(y) - pivot[1])
        out.append((at[0] + u, at[1] + v, weapon[y, x].copy()))
    return out


def outline_around(frame, pix):
    """붙인 무기 둘레의 빈 칸(8방향)에만 기준 외곽선 색을 찍는다. 무기 픽셀 자체는 바꾸지 않는다."""
    own = {(x, y) for x, y, _ in pix}
    for x, y, _ in pix:
        for ddx in (-1, 0, 1):
            for ddy in (-1, 0, 1):
                nx, ny = x + ddx, y + ddy
                if (nx, ny) in own or not (0 <= nx < 64 and 0 <= ny < 64):
                    continue
                if frame[ny, nx, 3] == 0:
                    frame[ny, nx] = (*OUTLINE, 255)


def main():
    params = json.loads(Path(sys.argv[1]).read_text())
    base = load(params['base'])
    weapon = load(params['weapon'])
    hm = head_mask(base, params['head'])
    sh = params['shield']
    shield = np.zeros(hm.shape, bool)
    for y in range(sh['rows'][0], sh['rows'][1] + 1):
        for x in range(sh['x_min'], 64):
            if base[y, x, 3]:
                shield[y, x] = True
    wpivot = params['weapon_pivot']
    out_dir = ROOT / params['out_dir']
    out_dir.mkdir(parents=True, exist_ok=True)
    for old in out_dir.glob('*.png'):
        old.unlink()
        meta = old.with_suffix('.png.meta')
        if meta.exists():
            meta.unlink()
    log = []
    for i, fr in enumerate(params['frames']):
        if fr.get('src') == 'base':
            frame = base.copy()  # 대기 그림 그대로(칼·방패·머리가 이미 기준)
            lost = 0
        else:
            src = load(fr['src'])
            frame, _, lost = to64(src)
        rec = {'out': f"{params['prefix']}_{i:02d}.png", 'src': fr.get('src'), 'cell': fr.get('cell'), 'lost_by_crop': lost}
        erased = 0
        for seg in fr.get('erase_segments', []):
            erased += seg_erase(frame, seg[0], seg[1], seg[2])
        for b in fr.get('erase_boxes', []):
            sub = frame[b[1]:b[3] + 1, b[0]:b[2] + 1]
            erased += int((sub[..., 3] > 0).sum())
            sub[:] = 0
        rec['erased_px'] = erased
        if fr.get('src') == 'base':
            dx, dy = 0, 0
        elif 'head_shift' in fr:
            dx, dy = fr['head_shift']
        else:
            dx, dy = find_shift(frame, base, hm)
        rec['head_shift'] = [dx, dy]
        if fr.get('src') != 'base':
            # 머리 둘레(기준 머리 모양을 clear_pad만큼 넓힌 곳) 지우기, keep 상자는 남김(팔·무기)
            pad = params['head']['clear_pad']
            ys, xs = np.nonzero(hm)
            area = np.zeros(hm.shape, bool)
            for y, x in zip(ys, xs):
                y0, y1 = max(0, y + dy - pad), min(63, y + dy + pad)
                x0, x1 = max(0, x + dx - pad), min(63, x + dx + pad)
                area[y0:y1 + 1, x0:x1 + 1] = True
            area[min(63, params['head']['bottom'] + dy + 1):] = False
            keep = fr.get('keep_boxes', []) + fr.get('front_boxes', [])
            # front_boxes: 머리 앞을 지나는 팔 — 지우지 않고, 머리·방패를 덮은 뒤 다시 위에 찍는다
            front = [(int(x), int(y), frame[y, x].copy()) for y, x in zip(*np.nonzero(frame[..., 3] > 0))
                     if in_boxes(x, y, fr.get('front_boxes', []))]
            cleared = 0
            for y, x in zip(*np.nonzero(area & (frame[..., 3] > 0))):
                if not in_boxes(x, y, keep):
                    frame[y, x] = 0
                    cleared += 1
            rec['head_area_cleared_px'] = cleared
            paste(frame, patch_pixels(base, hm, dx, dy))
            sdx, sdy = fr.get('shield_shift', [dx, dy])
            paste(frame, patch_pixels(base, shield, sdx, sdy), keep_under=fr.get('shield_behind', False))
            rec['shield_shift'] = [sdx, sdy]
            paste(frame, front)
            if front:
                rec['front_px'] = len(front)
        w = fr.get('weapon')
        if w is None and 'hand_dx' in fr:
            # 걷기: 기준 손 자리 + 몸(머리) 이동 + 팔 흔들림(가까운 다리와 반대)
            w = {'tf': 'id', 'at': [wpivot[0] + dx + fr['hand_dx'], wpivot[1] + dy]}
        if w:
            pix = weapon_pixels(weapon, wpivot, w['tf'], w['at'])
            outside = [(x, y) for x, y, _ in pix if not (0 <= x < 64 and 0 <= y < 64)]
            if outside:
                rec['weapon_outside_px'] = len(outside)
            paste(frame, pix)
            outline_around(frame, pix)
            rec['weapon'] = w
        Image.fromarray(frame).save(out_dir / rec['out'])
        log.append(rec)
    print(json.dumps(log, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()
