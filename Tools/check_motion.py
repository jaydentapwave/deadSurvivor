#!/usr/bin/env python3
"""캐릭터 동작 프레임(걷기·공격) 검사. 손질 기준: docs/art/samples/aether/손질-기준.md

usage:
  check_motion.py --base <기준 그림.png> --kind walk|attack <프레임 폴더> [--head-rows N] [--head-xmin X]
  check_motion.py --selftest

판정(하나라도 어기면 실패, exit 1 / 모두 통과 exit 0):
  - 프레임 수: walk 6~8, attack 4~6 (폴더의 *.png)
  - 모든 장 64×64, RGBA
  - 발(맨 아래 불투명 줄) 63±1 (62~63)
  - 불투명 픽셀(alpha>0)이 모두 alpha 255이고 기준서 3.5 팔레트 안
  - 머리 패치: 기준 그림의 맨 위 줄부터 head-rows줄. 위 70% 줄은 전부, 아래 30% 줄은 x >= head-xmin만
    (배낭·어깨 제외). 프레임마다 정수 이동(±6px) 중 가장 잘 맞는 곳에서 같은 픽셀 95% 이상.
    가린 픽셀(그 자리 색이 기준 머리에 없는 색 = 무기·팔)은 빼고 세되, 가린 픽셀이 머리의 25%를 넘으면 실패.
    손에 든 물건 자리(지팡이·구슬·등불 등)는 docs/art/samples/aether/head-exclude.json에서 --base 파일 이름
    (확장자 뺀 것)으로 찾은 사각형 [[x0,y0,x1,y1], ...](기준 그림 좌표, 끝 포함)을 머리 패치에서 뺀다. 없는 이름은 뺄 것 없음.
  기본값: head-rows = round(0.42 × 기준 그림 키), head-xmin = 위 70% 줄의 가장 왼쪽 x + 3.
"""
import argparse
import json
import re
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SPEC = ROOT / 'docs/art/아트-기준서.md'
HEAD_EXCLUDE = ROOT / 'docs/art/samples/aether/head-exclude.json'
COUNTS = {'walk': (6, 8), 'attack': (4, 6)}
SHIFT = 6
MATCH_MIN = 0.95
OCCLUDE_MAX = 0.25
FOOT_ROW = 63


def spec_palette(spec=SPEC):
    sec = spec.read_text().split('### 3.5')[1].split('### 3.6')[0]
    hexes = json.loads(re.search(r'```json\s*(\[.*?\])\s*```', sec, re.S).group(1))
    return {tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) for h in hexes}


def head_mask(base, rows=None, xmin=None):
    alpha = base[..., 3] > 0
    ys = np.nonzero(alpha.any(1))[0]
    top, height = int(ys.min()), int(ys.max() - ys.min() + 1)
    rows = rows or int(round(0.42 * height))
    full = top + int(round(rows * 0.7)) - 1
    if xmin is None:
        xmin = int(np.nonzero(alpha[top:full + 1].any(0))[0].min()) + 3
    m = np.zeros(alpha.shape, bool)
    m[top:full + 1] = alpha[top:full + 1]
    m[full + 1:top + rows, xmin:] = alpha[full + 1:top + rows, xmin:]
    return m


def held_boxes(base_path, table=HEAD_EXCLUDE):
    """기준 그림 이름으로 head-exclude.json의 물건 사각형 목록을 읽는다(없으면 빈 목록)."""
    if not Path(table).exists():
        return []
    boxes = json.loads(Path(table).read_text()).get(Path(base_path).stem, [])
    for b in boxes:
        if len(b) != 4 or b[0] > b[2] or b[1] > b[3]:
            raise ValueError(f'{table}: {Path(base_path).stem}의 사각형 {b}가 [x0,y0,x1,y1](x0<=x1, y0<=y1)이 아니다')
    return boxes


def exclude_boxes(mask, boxes):
    m = mask.copy()
    for x0, y0, x1, y1 in boxes:
        m[y0:y1 + 1, x0:x1 + 1] = False
    return m


def head_score(frame, base, mask):
    """가장 잘 맞는 정수 이동에서 (같은 비율, 가린 비율, dx, dy)."""
    ys, xs = np.nonzero(mask)
    head_colors = {tuple(c) for c in base[ys, xs, :3]}
    total = len(ys)
    best = None
    h, w = frame.shape[:2]
    for dy in range(-SHIFT, SHIFT + 1):
        for dx in range(-SHIFT, SHIFT + 1):
            ty, tx = ys + dy, xs + dx
            inside = (ty >= 0) & (ty < h) & (tx >= 0) & (tx < w)
            f = np.zeros((total, 4), np.uint8)
            f[inside] = frame[ty[inside], tx[inside]]
            b = base[ys, xs]
            same = (f[:, 3] > 0) & (f[:, :3] == b[:, :3]).all(1)
            occl = (f[:, 3] > 0) & ~same & np.array([tuple(c) not in head_colors for c in f[:, :3]])
            n_same, n_occl = int(same.sum()), int(occl.sum())
            ratio = n_same / max(1, total - n_occl)
            key = (n_same, -n_occl)
            if best is None or key > best[0]:
                best = (key, ratio, n_occl / total, dx, dy)
    return best[1:]


def check(base_path, kind, folder, palette, head_rows=None, head_xmin=None, held=None):
    """실패 줄 목록(빈 목록 = 통과). held = 물건 사각형 목록(None이면 head-exclude.json에서 읽는다)."""
    fails = []
    base = np.array(Image.open(base_path).convert('RGBA'))
    if held is None:
        held = held_boxes(base_path)
    mask = exclude_boxes(head_mask(base, head_rows, head_xmin), held)
    files = sorted(Path(folder).glob('*.png'))
    lo, hi = COUNTS[kind]
    if not lo <= len(files) <= hi:
        fails.append(f'{folder}: 프레임 {len(files)}장 (기준 {kind} {lo}~{hi})')
    for f in files:
        im = Image.open(f)
        if im.size != (64, 64):
            fails.append(f'{f.name}: 크기 {im.size[0]}x{im.size[1]} (기준 64x64)')
            continue
        if im.mode != 'RGBA':
            fails.append(f'{f.name}: 모드 {im.mode} (기준 RGBA)')
            continue
        a = np.array(im)
        opaque = a[..., 3] > 0
        if not opaque.any():
            fails.append(f'{f.name}: 빈 그림')
            continue
        foot = int(np.nonzero(opaque.any(1))[0].max())
        if abs(foot - FOOT_ROW) > 1:
            fails.append(f'{f.name}: 발 맨 아래 줄 {foot} (기준 {FOOT_ROW}±1)')
        semi = int((opaque & (a[..., 3] < 255)).sum())
        if semi:
            fails.append(f'{f.name}: 반투명 픽셀 {semi}개')
        outside = {tuple(c) for c in a[opaque][:, :3]} - palette
        if outside:
            sample = ', '.join('#%02x%02x%02x' % c for c in sorted(outside)[:3])
            fails.append(f'{f.name}: 팔레트 밖 색 {len(outside)}개 ({sample})')
        ratio, occl, dx, dy = head_score(a, base, mask)
        if occl > OCCLUDE_MAX:
            fails.append(f'{f.name}: 머리가 {occl:.0%} 가려짐 (상한 {OCCLUDE_MAX:.0%}, 이동 {dx},{dy})')
        elif ratio < MATCH_MIN:
            fails.append(f'{f.name}: 머리 같은 픽셀 {ratio:.1%} (기준 {MATCH_MIN:.0%}, 이동 {dx},{dy})')
    return fails, len(files)


def selftest():
    """합성 그림으로 통과 1건과 반례들을 실제로 돌린다."""
    pal_list = [(0x2f, 0x11, 0x08), (0xc4, 0x43, 0x34), (0x77, 0x21, 0x1d), (0xf8, 0xda, 0xac), (0xff, 0xff, 0xff),
                (0xa8, 0x98, 0x98), (0x40, 0x54, 0x8b)]
    pal = set(pal_list)
    rng = np.random.default_rng(7)
    base = np.zeros((64, 64, 4), np.uint8)
    base[16:36, 20:44] = (*pal_list[1], 255)                       # 머리(머리카락)
    noise = rng.random((20, 24)) < 0.3
    base[16:36, 20:44][noise] = (*pal_list[2], 255)                # 머리 음영(점 무늬)
    base[26:34, 26:40] = (*pal_list[3], 255)                       # 얼굴
    base[29, 29] = base[29, 36] = (*pal_list[4], 255)              # 눈
    base[36:56, 18:46] = (*pal_list[6], 255)                       # 몸
    base[56:64, 22:28] = base[56:64, 34:40] = (*pal_list[0], 255)  # 다리
    cases = []
    tmp = Path(tempfile.mkdtemp(prefix='check_motion_'))
    Image.fromarray(base).save(tmp / 'base.png')

    def write(name, frames):
        d = tmp / name
        d.mkdir()
        for i, fr in enumerate(frames):
            Image.fromarray(fr).save(d / f'f_{i:02d}.png')
        return d

    def shifted(dx, dy_head=0):
        fr = np.roll(base, dx, axis=1)
        if dy_head < 0:
            head = fr[16:36].copy()
            fr[16:36] = 0
            fr[16 + dy_head:36 + dy_head] = head
            fr[36 + dy_head:36] = (*pal_list[0], 255)  # 머리가 올라간 자리는 목(외곽선 색)
        return fr

    good = [shifted(dx, dy) for dx, dy in [(0, 0), (1, -1), (2, -2), (1, -1), (0, 0), (-1, -1)]]
    cases.append(('통과: 걷기 6장(머리 정수 이동)', write('pass', good), 'walk', True))
    occl = [g.copy() for g in good]
    occl[2][14:30, 30:33] = (*pal_list[5], 255)                    # 칼날이 머리 앞을 지남(48px, 약 10%)
    cases.append(('통과: 칼이 머리를 가린 장', write('occluded', occl), 'walk', True))
    bad_head = [g.copy() for g in good]
    m = (bad_head[3][16:26, 21:43, :3] == pal_list[1]).all(-1)
    bad_head[3][16:26, 21:43][m] = (*pal_list[2], 255)             # 머리 도트가 바뀐 장(기준 머리에 있는 색으로)
    cases.append(('반례: 머리 도트가 바뀐 장', write('head', bad_head), 'walk', False))
    wide = [g.copy() for g in good]
    wide[1] = np.zeros((64, 65, 4), np.uint8)
    wide[1][:, :64] = good[1]
    cases.append(('반례: 65px 장', write('wide', wide), 'walk', False))
    off = [g.copy() for g in good]
    off[4][40, 30] = (1, 2, 3, 255)
    cases.append(('반례: 팔레트 밖 색', write('palette', off), 'walk', False))
    lifted = [g.copy() for g in good]
    lifted[0] = np.roll(lifted[0], -3, axis=0)
    cases.append(('반례: 발이 3px 뜬 장', write('foot', lifted), 'walk', False))
    cases.append(('반례: 공격인데 6장 넘음(걷기 7장)', write('count', good + [good[0]]), 'attack', False))
    covered = [g.copy() for g in good]
    covered[5][16:36, 20:44] = (*pal_list[6], 255)                 # 머리를 몸 색으로 통째로 덮음
    cases.append(('반례: 머리를 다른 색으로 통째로 덮은 장', write('cover', covered), 'walk', False))

    # 손에 든 물건: 머리 오른쪽에 지팡이(머리 줄 안, 구슬 색). 물건 자리를 뺀 기준과 빼지 않은 기준을 함께 본다.
    orb = (0x7f, 0xd0, 0xc8)
    pal.add(orb)
    held_base = base.copy()
    held_base[18:40, 46:50] = (*pal_list[0], 255)                  # 지팡이 자루(외곽선 색)
    held_base[18:23, 45:51] = (*orb, 255)                           # 구슬
    Image.fromarray(held_base).save(tmp / 'held_base.png')
    held_box = [[45, 18, 50, 39]]

    def with_staff(fr, dx, dy):
        fr = fr.copy()
        fr[18 + dy:40 + dy, 46 + dx:50 + dx] = (*pal_list[0], 255)
        fr[18 + dy:23 + dy, 45 + dx:51 + dx] = (*orb, 255)
        return fr

    moves = [(0, 0), (2, 3), (4, 5), (2, 3), (0, 0), (-1, 4)]       # 걸을 때 지팡이만 머리와 따로 움직임
    staff_moved = [with_staff(g, dx, dy) for g, (dx, dy) in zip(good, moves)]
    held_dir = write('held_moved', staff_moved)
    face = [f.copy() for f in staff_moved]
    face[2][26:31, 26:40] = (*pal_list[2], 255)                     # 얼굴 도트를 바꾼 장(기준 머리에 있는 색, 70px)
    face_dir = write('held_face', face)
    held_cases = [
        ('통과: 물건 자리를 뺀 기준에서 물건만 옮긴 장', held_dir, held_box, True),
        ('반례: 물건 자리를 빼지 않으면 물건만 옮긴 장도 실패', held_dir, [], False),
        ('반례: 물건 자리를 뺀 기준에서 얼굴 도트를 바꾼 장', face_dir, held_box, False),
    ]

    ok = True
    for name, d, held, expect in held_cases:
        fails, _ = check(tmp / 'held_base.png', 'walk', d, pal, held=held)
        got = not fails
        mark = 'OK ' if got == expect else 'BAD'
        ok &= got == expect
        print(f'{mark} {name}: {"통과" if got else "실패"}' + (f' — {fails[0]}' if fails else ''))
    tbl = tmp / 'exclude.json'
    tbl.write_text(json.dumps({'held_base': held_box}))
    got = held_boxes(tmp / 'held_base.png', tbl) == held_box and held_boxes(tmp / 'base.png', tbl) == []
    ok &= got
    print(f'{"OK " if got else "BAD"} 표 읽기: 기준 그림 이름으로 찾고, 없는 이름은 빈 목록')
    for name, d, kind, expect in cases:
        fails, _ = check(tmp / 'base.png', kind, d, pal, held=[])
        got = not fails
        mark = 'OK ' if got == expect else 'BAD'
        ok &= got == expect
        print(f'{mark} {name}: {"통과" if got else "실패"}' + (f' — {fails[0]}' if fails else ''))
    print('selftest', 'PASS' if ok else 'FAIL')
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--base')
    ap.add_argument('--kind', choices=sorted(COUNTS))
    ap.add_argument('--head-rows', type=int)
    ap.add_argument('--head-xmin', type=int)
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('folder', nargs='?')
    args = ap.parse_args()
    if args.selftest:
        return selftest()
    if not (args.base and args.kind and args.folder):
        ap.error('--base, --kind, 프레임 폴더가 필요하다')
    fails, n = check(args.base, args.kind, args.folder, spec_palette(), args.head_rows, args.head_xmin)
    for line in fails:
        print('FAIL', line)
    print(f'{"PASS" if not fails else "FAIL"} {args.kind} {n}장 {args.folder}')
    return 1 if fails else 0


if __name__ == '__main__':
    sys.exit(main())
