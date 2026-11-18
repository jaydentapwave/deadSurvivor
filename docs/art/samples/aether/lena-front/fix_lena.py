"""레나 앞 Aether→도트 프레임 손질(크레딧 0). 손질 기준: docs/art/samples/aether/손질-기준.md
usage: fix_lena.py <params.json>
브루노 앞 fix_front.py의 함수(머리 맞춤·덮기)를 불러 쓴다. 다른 점:
- 활은 기준 조각으로 바꾸지 않는다(기준 그림에 당긴 활 픽셀이 없음). 머리 덮개에서 활 자리(y>=bow_y, x>=bow_x)를 빼고
  그 자리의 영상 픽셀(활·시위)은 지우지 않는다.
- src "base"는 기준 그림 그대로(대기).
- 시위에 남은 키 색(자홍) 픽셀은 이웃 색으로 바꾼다(key_residue_px).
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'docs/art/samples/aether/bruno-front'))
import fix_front as ff  # noqa: E402


def fix_key_residue(frame):
    """키 색(자홍) 남은 픽셀(R·B가 G보다 30 넘게 큼)을 8방향 이웃의 가장 흔한 불투명 색으로 바꾼다. 레나에는 자홍 계열 색이 없다."""
    a = frame.astype(int)
    m = (a[..., 3] > 0) & (a[..., 0] > a[..., 1] + 30) & (a[..., 2] > a[..., 1] + 30)
    n = 0
    for y, x in zip(*np.nonzero(m)):
        cnt = {}
        for ddy in (-1, 0, 1):
            for ddx in (-1, 0, 1):
                ny, nx = y + ddy, x + ddx
                if (ddx or ddy) and 0 <= ny < 64 and 0 <= nx < 64 and frame[ny, nx, 3] and not m[ny, nx]:
                    c = tuple(int(v) for v in frame[ny, nx])
                    cnt[c] = cnt.get(c, 0) + 1
        if cnt:
            frame[y, x] = max(cnt, key=cnt.get)
            n += 1
    return n


def main():
    p = json.loads(Path(sys.argv[1]).read_text())
    base = ff.load(p['base'])
    hm = ff.head_mask(base, p['head'])
    bx, by = p['bow_x'], p['bow_y']
    hm[by:, bx:] = False
    out_dir = ROOT / p['out_dir']
    out_dir.mkdir(parents=True, exist_ok=True)
    for old in out_dir.glob('*.png'):
        old.unlink()
        meta = old.with_suffix('.png.meta')
        if meta.exists():
            meta.unlink()
    log = []
    for i, fr in enumerate(p['frames']):
        rec = {'out': f"{p['prefix']}_{i:02d}.png", 'src': fr['src'], 'cell': fr.get('cell')}
        if fr['src'] == 'base':
            frame = base.copy()
        else:
            frame, _, lost = ff.to64(ff.load(fr['src']))
            rec['lost_by_crop'] = lost
            erased = 0
            for b in fr.get('erase_boxes', []):
                sub = frame[b[1]:b[3] + 1, b[0]:b[2] + 1]
                erased += int((sub[..., 3] > 0).sum())
                sub[:] = 0
            rec['erased_px'] = erased
            dx, dy = fr['head_shift'] if 'head_shift' in fr else ff.find_shift(frame, base, hm)
            rec['head_shift'] = [int(dx), int(dy)]
            pad = p['head']['clear_pad']
            area = np.zeros(hm.shape, bool)
            for y, x in zip(*np.nonzero(hm)):
                area[max(0, y + dy - pad):min(63, y + dy + pad) + 1, max(0, x + dx - pad):min(63, x + dx + pad) + 1] = True
            area[min(63, p['head']['bottom'] + dy + 1):] = False
            keep = [[bx + dx, by + dy, 63, 63]] + fr.get('keep_boxes', []) + fr.get('front_boxes', [])
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
            if front:
                rec['front_px'] = len(front)
        rec['key_residue_px'] = fix_key_residue(frame)
        Image.fromarray(frame).save(out_dir / rec['out'])
        log.append(rec)
    print(json.dumps(log, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()
