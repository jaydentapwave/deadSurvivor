"""Aether→도트 프레임 손질, 뒤 3/4(크레딧 0). 손질 기준: docs/art/samples/aether/손질-기준.md, 앞 판 bruno-front/fix_front.py를 뒤 그림용으로 고친 것.

usage: fix_back.py <params.json>
앞 판과 다른 점:
- Aether 뒤 클립은 칼을 지우지 않고 길게 그린다 → 영상 칼날을 자동으로 찾아 지운다: 강철색(#fff9ed #e3d7c8 #c8b8b8 #a89898)
  8방향 연결 덩어리 중 가장 긴 것(경계 상자 대각선) + 그 둘레에서 남은 그림과 닿지 않는 외곽선 픽셀.
- 기준 칼(parts/arm_back_hand_sword.png)은 영상 칼날의 손잡이 끝(몸 가운데에 가까운 끝)에 칼날 손잡이 끝을 맞춰 붙인다.
  방향은 정수 변환 8가지 중 영상 칼날 방향과 가장 가까운 것(tf를 주면 그것). 길이·모양은 기준 그대로.
- 방패는 덮지 않는다(뒤 그림 방패는 화면 왼쪽, Aether가 팔과 함께 그려 두께가 유지됨 — source.txt에 기록).
정수 픽셀 이동·뒤집기·전치만 쓴다. 새 색을 만들지 않는다(외곽선 #2f1108은 기준 그림 색).
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'docs/art/samples/aether/bruno-front'))
import fix_front as ff  # noqa: E402

STEEL = {(0xff, 0xf9, 0xed), (0xe3, 0xd7, 0xc8), (0xc8, 0xb8, 0xb8), (0xa8, 0x98, 0x98)}
OUTL = {(0x34, 0x14, 0x0e), (0x2f, 0x11, 0x08)}
BODY_C = (32, 46)


def components(mask):
    seen = np.zeros(mask.shape, bool)
    out = []
    for y, x in zip(*np.nonzero(mask)):
        if seen[y, x]:
            continue
        stack, comp = [(y, x)], []
        seen[y, x] = True
        while stack:
            cy, cx = stack.pop()
            comp.append((cx, cy))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < mask.shape[0] and 0 <= nx < mask.shape[1] and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
        out.append(comp)
    return out


def blade_of(img, region=None):
    """가장 긴 강철색 덩어리(칼날). region=[x0,y0,x1,y1]로 찾을 곳을 좁힐 수 있다."""
    m = np.zeros(img.shape[:2], bool)
    for y, x in zip(*np.nonzero(img[..., 3])):
        if tuple(img[y, x, :3]) in STEEL and (region is None or ff.in_boxes(x, y, [region])):
            m[y, x] = True
    best = None
    for comp in components(m):
        xs, ys = [p[0] for p in comp], [p[1] for p in comp]
        diag = (max(xs) - min(xs)) ** 2 + (max(ys) - min(ys)) ** 2
        if best is None or diag > best[0]:
            best = (diag, comp)
    return best[1] if best else []


def ends(comp, hand_ref):
    """칼날의 손잡이 끝과 칼끝: 가장 먼 두 픽셀 중 hand_ref에 가까운 쪽이 손잡이 끝."""
    pts = np.array(comp)
    d = ((pts[:, None, :] - pts[None, :, :]) ** 2).sum(-1)
    i, j = np.unravel_index(int(d.argmax()), d.shape)
    a, b = tuple(pts[i]), tuple(pts[j])
    da = (a[0] - hand_ref[0]) ** 2 + (a[1] - hand_ref[1]) ** 2
    db = (b[0] - hand_ref[0]) ** 2 + (b[1] - hand_ref[1]) ** 2
    return (a, b) if da <= db else (b, a)


def erase_blade(frame, comp):
    own = set(comp)
    for x, y in comp:
        frame[y, x] = 0
    n = len(comp)
    # 칼날 둘레 외곽선 중 남은 그림(외곽선 아닌 색)과 닿지 않는 것
    ring = {(x + dx, y + dy) for x, y in own for dx in (-1, 0, 1) for dy in (-1, 0, 1)} - own
    for x, y in ring:
        if not (0 <= x < 64 and 0 <= y < 64) or not frame[y, x, 3] or tuple(frame[y, x, :3]) not in OUTL:
            continue
        touch = False
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < 64 and 0 <= ny < 64 and frame[ny, nx, 3] and tuple(frame[ny, nx, :3]) not in OUTL:
                    touch = True
        if not touch:
            frame[y, x] = 0
            n += 1
    return n


def main():
    params = json.loads(Path(sys.argv[1]).read_text())
    base = ff.load(params['base'])
    weapon = ff.load(params['weapon'])
    hm = ff.head_mask(base, params['head'])
    wblade = blade_of(weapon)
    w_handle, w_tip = ends(wblade, params['weapon_hand'])
    wvec = np.array(w_tip) - np.array(w_handle)
    out_dir = ROOT / params['out_dir']
    out_dir.mkdir(parents=True, exist_ok=True)
    for old in out_dir.glob('*.png'):
        old.unlink()
        meta = old.with_suffix('.png.meta')
        if meta.exists():
            meta.unlink()
    log = []
    for i, fr in enumerate(params['frames']):
        rec = {'out': f"{params['prefix']}_{i:02d}.png", 'src': fr.get('src'), 'cell': fr.get('cell')}
        if fr.get('src') == 'base':
            Image.fromarray(base).save(out_dir / rec['out'])
            log.append(rec)
            continue
        src = ff.load(fr['src'])
        erased = 0
        for b in fr.get('src_erase_boxes', []):
            # 자르기 전(원래 도트 캔버스 좌표)에서 지우기: 휘두름 궤적, 64칸 밖으로 뻗은 팔·칼
            sub = src[b[1]:b[3] + 1, b[0]:b[2] + 1]
            erased += int((sub[..., 3] > 0).sum())
            sub[:] = 0
        frame, _, lost = ff.to64(src)
        rec['lost_by_crop'] = lost
        if 'handle' in fr:
            # 영상 칼날이 궤적과 섞였거나 잘린 장: 칼날은 상자로 지웠고 손잡이 끝 자리와 방향을 직접 준다
            handle = tuple(fr['handle'])
            tip = None
            rec['aether_blade'] = 'erased by boxes, handle given'
        else:
            blade = blade_of(frame, fr.get('blade_region'))
            handle, tip = ends(blade, BODY_C)
            rec['aether_blade'] = {'px': len(blade), 'handle': [int(handle[0]), int(handle[1])], 'tip': [int(tip[0]), int(tip[1])]}
            erased += erase_blade(frame, blade)
        for seg in fr.get('erase_segments', []):
            erased += ff.seg_erase(frame, seg[0], seg[1], seg[2])
        for b in fr.get('erase_boxes', []):
            sub = frame[b[1]:b[3] + 1, b[0]:b[2] + 1]
            erased += int((sub[..., 3] > 0).sum())
            sub[:] = 0
        rec['erased_px'] = erased
        dx, dy = fr['head_shift'] if 'head_shift' in fr else ff.find_shift(frame, base, hm)
        rec['head_shift'] = [int(dx), int(dy)]
        pad = params['head']['clear_pad']
        area = np.zeros(hm.shape, bool)
        for y, x in zip(*np.nonzero(hm)):
            area[max(0, y + dy - pad):min(63, y + dy + pad) + 1, max(0, x + dx - pad):min(63, x + dx + pad) + 1] = True
        area[min(63, params['head']['bottom'] + dy + 1):] = False
        keep = fr.get('keep_boxes', []) + fr.get('front_boxes', [])
        front = [(int(x), int(y), frame[y, x].copy()) for y, x in zip(*np.nonzero(frame[..., 3] > 0))
                 if ff.in_boxes(x, y, fr.get('front_boxes', []))]
        cleared = 0
        for y, x in zip(*np.nonzero(area & (frame[..., 3] > 0))):
            if not ff.in_boxes(x, y, keep):
                frame[y, x] = 0
                cleared += 1
        rec['head_area_cleared_px'] = cleared
        ff.paste(frame, ff.patch_pixels(base, hm, dx, dy))
        ff.paste(frame, front)
        # 기준 칼: 방향은 영상 칼날에 가장 가까운 정수 변환, 손잡이 끝을 영상 손잡이 끝에
        tf = fr.get('tf')
        if tf is None:
            avec = np.array(tip) - np.array(handle)
            tf = max(ff.D4, key=lambda k: np.dot(ff.D4[k](*wvec), avec) / (np.linalg.norm(ff.D4[k](*wvec)) * np.linalg.norm(avec) + 1e-9))
        at = [int(handle[0]) + fr.get('nudge', [0, 0])[0], int(handle[1]) + fr.get('nudge', [0, 0])[1]]
        pix = ff.weapon_pixels(weapon, list(w_handle), tf, at)
        outside = [(x, y) for x, y, _ in pix if not (0 <= x < 64 and 0 <= y < 64)]
        if outside:
            rec['weapon_outside_px'] = len(outside)
        # weapon_behind: 뒤 3/4에서 머리 위로 든 칼은 캐릭터 앞쪽(보는 사람에게서 먼 쪽)이라 머리 뒤에 둔다(빈 칸에만 찍음)
        ff.paste(frame, pix, keep_under=fr.get('weapon_behind', False))
        ff.outline_around(frame, pix)
        rec['weapon'] = {'tf': tf, 'handle_at': at}
        # 떨어진 잔조각(12px 이하, 지운 칼의 날밑·외곽선 찌꺼기) 지우기
        stray = [c for c in components(frame[..., 3] > 0) if len(c) <= 12]
        for c in stray:
            for x, y in c:
                frame[y, x] = 0
        rec['stray_removed_px'] = sum(len(c) for c in stray)
        Image.fromarray(frame).save(out_dir / rec['out'])
        log.append(rec)
    print(json.dumps(log, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()
