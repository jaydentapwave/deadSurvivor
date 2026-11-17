# 미라 뒤 공격 끝 그림: 기준 뒤 그림에서 지팡이(구슬·손 포함)를 떼어 오른쪽 위로 내민 자세. 손으로 옮긴 픽셀, 생성 아님.
# 앞 판 mira-front(keep/samples/make_end.py)와 같은 방식. 뒤 그림의 지팡이 자리:
#  y16~41: x>=41(y27~33은 x>=42 — x41은 모자·머리 외곽선), y49~63: 로브 아래로 나온 지팡이 줄 x40~42(갈색·외곽선).
# 옮긴 뒤 빈 자리는 몸에 붙은 칸만 외곽선 #34140e로 닫고, 원래 손 옆 소매에서 옮긴 손까지 파란 소매 2px.
# usage: make_end.py <기준> <출력> [dx dy]
import sys
import numpy as np
from PIL import Image
base, out = sys.argv[1], sys.argv[2]
dx, dy = (int(sys.argv[3]), int(sys.argv[4])) if len(sys.argv) > 4 else (6, -6)
a = np.array(Image.open(base).convert('RGBA'))
OUTL = np.array([52, 20, 14, 255], np.uint8)
SLEEVE = np.array([64, 84, 139, 255], np.uint8)
SHADE = np.array([43, 53, 102, 255], np.uint8)
BROWN = {(175, 100, 46), (119, 76, 59)}
DARK = {(52, 20, 14), (47, 17, 8)}
m = np.zeros((64, 64), bool)
for y in range(16, 42):
    x0 = 42 if 27 <= y <= 33 else 41
    m[y, x0:] = a[y, x0:, 3] > 0
for y in range(49, 64):
    for x in range(39, 44):
        if a[y, x, 3] and tuple(a[y, x, :3]) in BROWN:
            m[y, x] = True
            for xx in (x - 1, x + 1):
                if a[y, xx, 3] and tuple(a[y, xx, :3]) in DARK:
                    m[y, xx] = True
staff = a.copy()
staff[~m] = 0
body = a.copy()
body[m] = 0
for y, x in zip(*np.nonzero(m)):
    if any(0 <= y + j < 64 and 0 <= x + i < 64 and body[y + j, x + i, 3] and not m[y + j, x + i]
           for i, j in ((1, 0), (-1, 0), (0, 1), (0, -1))):
        body[y, x] = OUTL
res = body.copy()
moved = np.roll(np.roll(staff, dy, 0), dx, 1)
sel = moved[..., 3] > 0
res[sel] = moved[sel]
print('staff_px', int(m.sum()), 'shift', dx, dy)
(x0, y0), (x1, y1) = (40, 41), (41 + dx - 1, 41 + dy)
n = max(abs(x1 - x0), abs(y1 - y0))
pts = set()
for i in range(n + 1):
    x = round(x0 + (x1 - x0) * i / n)
    y = round(y0 + (y1 - y0) * i / n)
    pts.add((x, y))
    pts.add((x, y + 1))
for x, y in pts:
    if not sel[y, x]:
        res[y, x] = SHADE if (x, y - 1) in pts else SLEEVE
for x, y in pts:
    for i, j in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        if res[y + j, x + i, 3] == 0:
            res[y + j, x + i] = OUTL
Image.fromarray(res).save(out)
print('sleeve_px', len(pts))
# 손 아래~로브 뒤에서 나오던 줄 사이(원래 소매에 가려졌던 y42~48 → 옮긴 뒤 y36~42) 지팡이 줄 잇기, 떨어진 작은 덩어리(옛 지팡이 옆 장식 점) 지우기
res = np.array(Image.open(out))
BR = np.array([175, 100, 46, 255], np.uint8)
sx = 41 + dx
for y in range(42 + dy, 49 + dy):
    if res[y, sx, 3] == 0 or tuple(res[y, sx, :3]) in DARK:
        res[y, sx] = BR
    for xx in (sx - 1, sx + 1):
        if res[y, xx, 3] == 0:
            res[y, xx] = OUTL
from collections import deque
seen = np.zeros((64, 64), bool)
removed = 0
for sy, sx0 in zip(*np.nonzero(res[..., 3] > 0)):
    if seen[sy, sx0]:
        continue
    comp = []
    q = deque([(sy, sx0)])
    seen[sy, sx0] = True
    while q:
        y, x = q.popleft()
        comp.append((y, x))
        for j, i in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            yy, xx = y + j, x + i
            if 0 <= yy < 64 and 0 <= xx < 64 and not seen[yy, xx] and res[yy, xx, 3] > 0:
                seen[yy, xx] = True
                q.append((yy, xx))
    if len(comp) < 20:
        for y, x in comp:
            res[y, x] = 0
        removed += len(comp)
Image.fromarray(res).save(out)
print('removed_small_px', removed)
