"""Aether 출력 프레임(process 결과)(×8 좌표, 512 높이) -> 64 높이 도트. pixelize.py의 pixelize()(블록 최빈색 + 기준서 3.5 팔레트)를 그대로 쓴다.
바닥(가장 아래 불투명 줄)을 캔버스 맨 아래 줄(64 높이면 63)에 맞춘다(클립 전체 같은 dy가 아니라 프레임별 - 발이 땅에서 뜨지 않게; 몸 들썩임은 위쪽 윤곽으로 남는다).
usage: to_dot.py <out_dir> <prefix> <frame.png>...  (첫 프레임의 바닥 이동값을 모든 프레임에 같게 쓰려면 --same-dy)
"""
import json
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[5] / '.ai/2d-art/tools'))
import pixelize as px  # noqa: E402

SPEC = Path(__file__).resolve().parents[5] / 'docs/art/아트-기준서.md'


def palette():
    text = SPEC.read_text()
    sec = text.split('### 3.5')[1].split('### 3.6')[0]
    return [px.hex_rgb(h) for h in json.loads(re.search(r'```json\s*(\[.*?\])\s*```', sec, re.S).group(1))]


def main():
    args = sys.argv[1:]
    same = '--same-dy' in args
    args = [a for a in args if a != '--same-dy']
    out_dir, prefix, frames = Path(args[0]), args[1], args[2:]
    out_dir.mkdir(parents=True, exist_ok=True)
    pal = palette()
    bg = (255, 0, 255)
    fixed_dy = None
    report = []
    for i, f in enumerate(frames):
        im = Image.open(f).convert('RGBA')
        a = np.array(im)
        rgb = a[..., :3].copy()
        rgb[a[..., 3] < 128] = bg
        w, h = im.size[0] // 8, im.size[1] // 8
        dot = np.array(px.pixelize(rgb, w, h, pal, bg))
        rows = np.where(dot[..., 3].max(axis=1) > 0)[0]
        dy = (h - 1) - int(rows.max())
        if same:
            fixed_dy = dy if fixed_dy is None else fixed_dy
            dy = fixed_dy
        dot = np.roll(dot, dy, axis=0)
        if dy > 0:
            dot[:dy] = 0
        elif dy < 0:
            dot[dy:] = 0
        out = out_dir / f'{prefix}_{i:02d}.png'
        Image.fromarray(dot, 'RGBA').save(out)
        cols = np.where(dot[..., 3].max(axis=0) > 0)[0]
        report.append({'src': f, 'out': str(out), 'size': [w, h], 'dy': dy,
                       'bbox_x': [int(cols.min()), int(cols.max())],
                       'colors': int(len({tuple(p) for p in dot[dot[..., 3] > 0][:, :3]}))})
    print(json.dumps(report, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()
