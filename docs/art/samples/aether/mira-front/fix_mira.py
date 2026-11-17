"""미라 앞 Aether→도트 프레임 손질(크레딧 0). 손질 기준: docs/art/samples/aether/손질-기준.md
usage: fix_mira.py <params.json>
레나 fix_lena.py와 같은 흐름(브루노 fix_front.py의 머리 맞춤·덮기를 불러 씀). 다른 점:
- 지팡이는 기준 조각으로 바꾸지 않는다(영상 지팡이 길이가 기준과 거의 같고, 공격의 기울기를 8방향 변환으로는 못 맞춘다).
- 머리 덮개에서 head-exclude.json의 mira_front 물건 자리(지팡이 윗부분·구슬)를 뺀다. check_motion이 빼는 자리와 같다.
  그 자리(머리 이동만큼 옮긴 곳)의 영상 픽셀은 지우지 않는다.
- front_boxes: 머리 위를 지나는 영상 지팡이 픽셀을 덮은 뒤 다시 찍는다(지팡이가 머리 앞).
- erase_px: 지울 낱 픽셀 [[x,y],...] (떨어진 점).
- src "base"는 기준 그림 그대로(대기).
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'docs/art/samples/aether/bruno-front'))
sys.path.insert(0, str(ROOT / 'docs/art/samples/aether/lena-front'))
import fix_front as ff  # noqa: E402
from fix_lena import fix_key_residue  # noqa: E402


def main():
    p = json.loads(Path(sys.argv[1]).read_text())
    base = ff.load(p['base'])
    hm = ff.head_mask(base, p['head'])
    held = json.loads((ROOT / 'docs/art/samples/aether/head-exclude.json').read_text())[Path(p['base']).stem]
    for x0, y0, x1, y1 in held:
        hm[y0:y1 + 1, x0:x1 + 1] = False
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
            for x, y in fr.get('erase_px', []):
                frame[y, x] = 0
            dx, dy = fr['head_shift'] if 'head_shift' in fr else ff.find_shift(frame, base, hm)
            rec['head_shift'] = [int(dx), int(dy)]
            pad = p['head']['clear_pad']
            area = np.zeros(hm.shape, bool)
            for y, x in zip(*np.nonzero(hm)):
                area[max(0, y + dy - pad):min(63, y + dy + pad) + 1, max(0, x + dx - pad):min(63, x + dx + pad) + 1] = True
            area[min(63, p['head']['bottom'] + dy + 1):] = False
            keep = [[b[0] + dx, b[1] + dy, b[2] + dx, b[3] + dy] for b in held] + fr.get('keep_boxes', []) + fr.get('front_boxes', [])
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
