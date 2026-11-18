"""레나 뒤 Aether→도트 프레임 손질(크레딧 0). 손질 기준: docs/art/samples/aether/손질-기준.md
usage: fix_lena_back.py <params.json>

브루노 앞 fix_front.py의 함수(64칸 자르기·머리 맞춤·덮기)를 불러 쓴다. 앞 판(lena-front/fix_lena.py)과 다른 점:
- 뒤 그림은 활 윗날개가 머리 줄(y24~35) 안에 있다. 영상 활은 걷기에서 조각나고 공격에서 기준보다 길게 그려져서,
  활을 기준 그림의 활 조각(bow_boxes 안 기준 픽셀, 활 쥔 손 포함)으로 바꾼다. 놓는 곳은 머리 이동과 같은 정수 이동
  (손질 기준 "무기는 기준 조각, 길이 그대로"; 걷기는 손 자리 = 기준 손 + 머리 이동, hand_dx 0).
- 영상의 활·활 쥔 팔 끝은 erase_box(기준 좌표, 머리 이동만큼 옮김) 안에서 지운다.
- 공격: draw = 당긴 손 자리(기준 좌표)면 끝 그림(make_end.py)과 같은 방식으로 시위(흰 1px)를 활 두 끝에서 그 점까지,
  메긴 화살(갈색 1px + 쇠 촉 + 깃)을 그 점에서 오른쪽으로 찍는다. 새 색 없음(기준서 3.5 팔레트 색).
- src "base"는 기준 그림 그대로(대기).
정수 이동만 쓴다.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'docs/art/samples/aether/bruno-front'))
import fix_front as ff  # noqa: E402

STR = (255, 249, 237, 255)
SHAFT = (175, 100, 46, 255)
TIP = (200, 184, 184, 255)
FLETCH = (255, 224, 138, 255)
OUT = (47, 17, 8, 255)


def put(frame, x, y, c, only_empty=False):
    if 0 <= x < 64 and 0 <= y < 64:
        if only_empty and frame[y, x, 3]:
            return
        frame[y, x] = c


def line(frame, a, b, c):
    (x0, y0), (x1, y1) = a, b
    n = max(abs(x1 - x0), abs(y1 - y0), 1)
    for i in range(n + 1):
        put(frame, round(x0 + (x1 - x0) * i / n), round(y0 + (y1 - y0) * i / n), c)


def draw_string_arrow(frame, p, tips, arrow_to):
    """끝 그림(make_end.py)과 같은 모양. p = 당긴 손, tips = 활 두 끝, arrow_to = 화살 촉 x."""
    px, py = p
    line(frame, tips[0], p, STR)
    line(frame, tips[1], p, STR)
    for x in range(px + 1, arrow_to):
        put(frame, x, py, SHAFT)
    for x in range(px, arrow_to + 1):
        put(frame, x, py - 1, OUT, True)
        put(frame, x, py + 1, OUT, True)
    put(frame, arrow_to, py, TIP)
    put(frame, arrow_to - 1, py - 1, TIP)
    put(frame, arrow_to - 1, py + 1, TIP)
    for q in [(arrow_to + 1, py), (arrow_to, py - 1), (arrow_to, py + 1), (arrow_to - 1, py - 2), (arrow_to - 1, py + 2)]:
        put(frame, q[0], q[1], OUT, True)
    put(frame, px - 1, py, FLETCH)


def box_pixels(base, box, dx, dy):
    x0, y0, x1, y1 = box
    out = []
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if base[y, x, 3]:
                out.append((x + dx, y + dy, base[y, x].copy()))
    return out


def main():
    p = json.loads(Path(sys.argv[1]).read_text())
    base = ff.load(p['base'])
    hm = ff.head_mask(base, p['head'])
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
            dx, dy = fr['head_shift'] if 'head_shift' in fr else ff.find_shift(frame, base, hm)
            rec['head_shift'] = [int(dx), int(dy)]
            ex = p['erase_box']
            erased = 0
            for y in range(max(0, ex[1] + dy), min(63, ex[3] + dy) + 1):
                for x in range(max(0, ex[0] + dx), 64):
                    if frame[y, x, 3]:
                        frame[y, x] = 0
                        erased += 1
            rec['bow_area_erased_px'] = erased
            pad = p['head']['clear_pad']
            area = np.zeros(hm.shape, bool)
            for y, x in zip(*np.nonzero(hm)):
                area[max(0, y + dy - pad):min(63, y + dy + pad) + 1, max(0, x + dx - pad):min(63, x + dx + pad) + 1] = True
            area[min(63, p['head']['bottom'] + dy + 1):] = False
            keep = fr.get('keep_boxes', [])
            cleared = 0
            for y, x in zip(*np.nonzero(area & (frame[..., 3] > 0))):
                if not ff.in_boxes(x, y, keep):
                    frame[y, x] = 0
                    cleared += 1
            rec['head_area_cleared_px'] = cleared
            ff.paste(frame, ff.patch_pixels(base, hm, dx, dy))
            for b in p['bow_boxes']:
                ff.paste(frame, box_pixels(base, b, dx, dy))
            if 'draw' in fr:
                d = fr['draw']
                tips = [(t[0] + dx, t[1] + dy) for t in p['bow_tips']]
                draw_string_arrow(frame, (d[0] + dx, d[1] + dy), tips, p['arrow_to'] + dx)
                rec['draw'] = [d[0] + dx, d[1] + dy]
        Image.fromarray(frame).save(out_dir / rec['out'])
        log.append(rec)
    print(json.dumps(log, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()
