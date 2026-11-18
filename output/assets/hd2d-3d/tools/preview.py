# 미리보기: 배경 도트화 패스 흉내(render.py) 1px = 1/48m. 영웅 정지 그림·지금 2D 판과 나란히.
# 판독 입력(judge_view.png)은 회색 올리브 땅 + 하드 땅 그림자(판독자가 "바닥·그림자 없음"을 들지 않게).
# /usr/bin/python3 tools/preview.py [kind,...]   (작업 폴더 = hd2d-3d)
import sys, json, copy
sys.path.insert(0, 'tools')
from render import *
ROOT = '/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor'
GR = (0x8e, 0x8c, 0x6a)          # 회색 올리브 땅(판독용)
SHC = np.array([.44, .43, .39])  # 하드 그림자 배율(render.py와 같음)
DEC = 'Assets/Game/03.Resources/Art/AIGenerated/Decor/'
SPR = {'tree_v1': 'tree_02', 'tree_v2': 'tree_02', 'tree_v3': 'tree_02', 'bush_v1': 'bush_01', 'bush_v2': 'bush_02',
       'boulder_v1': 'boulder_01', 'boulder_v2': 'boulder_01', 'stone_v1': 'rock_01', 'stone_v2': 'rock_02',
       'wall_a': 'rock_02', 'wall_b': 'rock_01', 'grass_v1': 'grass_tuft_01', 'grass_v2': 'grass_tuft_02',
       'flower_white': 'flower_01', 'flower_yellow': 'flower_02', 'flower_purple': 'flower_03'}
ALL = list(SPR)
SMALL = ('stone', 'grass', 'flower')
GT = np.zeros((8, 8, 3), np.uint8) + np.array(GR, np.uint8)

def trim(a):
    al = a[..., 3] > 0; ys = np.nonzero(al.any(1))[0]; xs = np.nonzero(al.any(0))[0]
    return a[ys[0]:ys[-1] + 1, xs[0]:xs[-1] + 1]
def with_shadow(a):
    """2D 그림(영웅·지금 판) 발밑에 하드 그림자 타원을 깔고 땅색 바탕으로."""
    h, w = a.shape[:2]; sw = max(6, int(w * 0.75)); sh = max(3, sw // 4)
    H_ = h + sh // 2 + 2; out = np.zeros((H_, w + 4, 4), np.uint8); out[..., :3] = GR; out[..., 3] = 255
    yy, xx = np.mgrid[0:H_, 0:w + 4]
    cx, cy = (w + 4) / 2, h - 1
    e = ((xx - cx) / (sw / 2)) ** 2 + ((yy - cy) / (sh / 2)) ** 2 <= 1
    out[e, :3] = (np.array(GR) * SHC).astype(np.uint8)
    src = a[..., 3] > 0
    out[:h, 2:w + 2][src] = a[src]
    return out
def canvas(parts, gap=12, bg=GR):
    H = max(p.shape[0] for p in parts) + 8; W = sum(p.shape[1] for p in parts) + gap * (len(parts) + 1)
    c = Image.new('RGBA', (W, H), bg + (255,)); x = gap
    for p in parts:
        c.alpha_composite(Image.fromarray(p), (x, H - 4 - p.shape[0])); x += p.shape[1] + gap
    return c
def up(c, k): return c.resize((c.width * k, c.height * k), Image.NEAREST).convert('RGB')
def tiled(k, n=3):
    """같은 토막 n개를 x로 1.0m씩 이어 붙인 mesh(UV 같음)."""
    m = json.load(open(f'{k}/mesh.json')); out = copy.deepcopy(m); out['tris'] = []
    for i in range(n):
        for t in m['tris']:
            t2 = dict(t); t2['p'] = [[p[0] + (i - (n - 1) / 2) * 1.0, p[1], p[2]] for p in t['p']]; out['tris'].append(t2)
    return out

hero = trim(np.asarray(Image.open(f'{ROOT}/output/assets/hd2d-8dir/heroes/bolt_down.png').convert('RGBA')))
heroS = with_shadow(hero)
kinds = sys.argv[1].split(',') if len(sys.argv) > 1 else ALL
rows = []
for k in kinds:
    alb = f'{k}/albedo.png'
    plain = [render(k, alb, y) for y in (0, 40, 200)]
    grd = [render(k, alb, y, ground=GT) for y in (0, 40)]
    spr = trim(np.asarray(Image.open(f'{ROOT}/{DEC}{SPR[k]}.png').convert('RGBA')))
    sc = 3 if k.startswith(SMALL) else 2
    up(canvas(plain, bg=(0xa9, 0xa4, 0x69)), sc).save(f'{k}/preview_30deg.png')
    up(canvas([heroS, grd[0], grd[1]], gap=8), sc).save(f'{k}/judge_view.png')
    if k.startswith('wall'):
        run = [render(k, alb, y, ground=GT, mesh=tiled(k)) for y in (0, 40)]
        up(canvas([heroS, run[0], run[1]], gap=8), 2).save(f'{k}/preview_run.png')
    rows.append((k, canvas([heroS, grd[0], grd[1], with_shadow(spr)], gap=10)))
if len(kinds) == len(ALL):
    W = max(r.width for _, r in rows); Ht = sum(r.height for _, r in rows)
    c = Image.new('RGBA', (W, Ht), GR + (255,)); y = 0
    from PIL import ImageDraw
    for k, r in rows:
        c.alpha_composite(r, (0, y)); y += r.height
    big = up(c, 2); d = ImageDraw.Draw(big); y = 0
    for k, r in rows:
        d.text((W * 2 - 110, y * 2 + 4), k, fill=(20, 20, 20)); y += r.height
    big.save('compare.png')
print('ok', [(k, r.size) for k, r in rows])
