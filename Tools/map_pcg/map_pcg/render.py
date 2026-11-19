"""Unity-free previews.

topdown(L)   : 10 px/m map of the whole field with zones, water, banks, path, props, rules overlay.
start_view(L): approximate in-game 30 degree ortho view of the 1920x1080 start screen around the
               origin, real sprites at true scale (PPU 48, 2 screen px per sprite px), ground drawn
               from the zone masks with dot noise. 3D props (fence/sign/flag/tent/bridge) are simple
               projected boxes in their palette (approximation, not the Unity meshes).
current_*    : same renderer fed with the committed field_layout.json and the committed masks/textures.
"""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from . import fields as F

ROOT = Path(__file__).resolve().parents[3]
DECOR = ROOT / 'Assets/Game/03.Resources/Art/AIGenerated/Decor'
TEX = ROOT / 'Assets/Game/03.Resources/Textures/HD2D'
HEROES = ROOT / 'output/assets/hd2d-8dir/heroes'

W, H = 1920, 1080
PX = 96.0           # screen px per metre (x)
PY = 48.0           # screen px per metre of floor depth (sin 30)
PH = 96.0 * math.cos(math.radians(30))  # screen px per metre of height

GRASS = np.array([[147, 143, 91], [158, 153, 98], [169, 164, 105], [179, 174, 112]], float)
SOIL = np.array([[78, 66, 44], [96, 81, 54], [116, 99, 66]], float)
WORN = np.array([[118, 104, 70], [134, 119, 82], [149, 134, 94]], float)
GRAVEL = np.array([[112, 108, 92], [132, 128, 110], [156, 151, 130]], float)
WATER = np.array([[30, 48, 42], [43, 65, 54], [57, 85, 69]], float)
FACE = np.array([[58, 50, 35], [46, 40, 28], [36, 31, 22]], float)
WOOD = [(160, 129, 103), (128, 97, 76), (94, 71, 55), (52, 20, 14)]
TENT = [(224, 210, 184), (201, 184, 157), (168, 148, 123), (102, 73, 55)]
OUTLINE = (52, 20, 14)

_sprite_cache = {}


def sprite(name):
    if name not in _sprite_cache:
        p = DECOR / (name + '.png')
        _sprite_cache[name] = Image.open(p).convert('RGBA')
    return _sprite_cache[name]


def to_screen(x, y, h=0.0, cx=0.0, cy=0.0):
    return W / 2 + (x - cx) * PX, H / 2 - (y - cy) * PY - h * PH


# ------------------------------------------------------------------ ground painters

def _texel_grid(cx=0.0, cy=0.0, extra_rows=40):
    """Texel centres (2x2 px) of the screen plus rows below the screen (for raised banks)."""
    tw, th = W // 2, H // 2 + extra_rows
    u = (np.arange(tw) + 0.5) * 2
    v = (np.arange(th) + 0.5) * 2
    U, V = np.meshgrid(u, v)
    X = (U - W / 2) / PX + cx
    Y = (H / 2 - V) / PY + cy
    return X, Y


def _grain(X, Y, seed, levels=4):
    """World-locked dot grain: 2x2-texel clumps + small value noise -> level index."""
    ix = np.floor(X * 24)  # 2-texel clumps in x (48/m)
    iy = np.floor(Y * 12)  # 2-texel clumps in depth (24/m)
    h = F.hash01(ix, iy, seed)
    n = F.value_noise(X, Y * 2, 0.35, seed + 3)
    v = 0.6 * h + 0.4 * n
    cuts = np.linspace(0, 1, levels + 1)[1:-1]
    cuts = np.quantile(v, cuts) if v.size > 1000 else cuts
    return np.digitize(v, cuts)


def _hgrain(X, Y, seed, levels=3):
    """Horizontal-grain soil (stretched in x)."""
    ix = np.floor(X * 8)
    iy = np.floor(Y * 24)
    h = F.hash01(ix, iy, seed)
    n = F.value_noise(X * 0.4, Y * 2, 0.3, seed + 5)
    v = 0.55 * h + 0.45 * n
    return np.digitize(v, np.quantile(v, np.linspace(0, 1, levels + 1)[1:-1]))


def paint_pcg(L, X, Y):
    from .export import sample_ground
    G = sample_ground(L, X, Y)
    light = np.clip(np.round((F.value_noise(X, Y, 6.0, L.seed + 9) - 0.5) * 4) * 3, -7, 7)[..., None]
    rgb = GRASS[_grain(X, Y, L.seed + 1)] + light
    t = G['tone'][..., None]
    rgb = rgb + t * np.array([-9.0, 3.0, -4.0])
    rgb = np.where(G['meadow'][..., None], rgb + np.array([5.0, 8.0, 1.0]), rgb)
    # Worn ground: soil showing through thinned grass (dot mix, not a flat smear).
    thin = F.hash01(np.floor(X * 24), np.floor(Y * 12), L.seed + 12) < 0.62
    rgb = np.where((G['worn'] & thin)[..., None], WORN[_hgrain(X, Y, L.seed + 2)], rgb)
    rgb = np.where((G['worn'] & ~thin)[..., None], rgb * 0.95, rgb)
    # Gravel: pebble clumps on worn soil.
    peb = F.hash01(np.floor(X * 24), np.floor(Y * 12), L.seed + 14) < 0.7
    rgb = np.where((G['gravel'] & peb)[..., None], GRAVEL[_grain(X, Y, L.seed + 4, 3)], rgb)
    rgb = np.where((G['gravel'] & ~peb)[..., None], WORN[1], rgb)
    rgb = np.where(G['path'][..., None], SOIL[_hgrain(X, Y, L.seed + 6)], rgb)
    ripple = F.value_noise(X * 0.5, Y * 3, 0.5, L.seed + 8)
    wl = np.digitize(ripple, [0.42, 0.72])
    rgb = np.where(G['water'][..., None], WATER[wl], rgb)
    if getattr(L, 'terrain', None) is not None:
        from .terrain import sample
        h = sample(L.terrain, X, Y)
        dz = (sample(L.terrain, X, Y+.05) - sample(L.terrain, X, Y-.05))/.1
        shade = np.where(dz > .025, 1.07, np.where(dz < -.025, .92, 1.0))
        rgb *= shade[..., None]
        contour = (np.abs(h*10-np.round(h*10)) < .012) & (np.abs(dz) > .012)
        rgb = np.where(contour[..., None], rgb*.87, rgb)
    raised = G['bank'] | G['upper']
    return np.clip(rgb, 0, 255), raised


def _tex_sample(img, u, v):
    """Point-sample a repeating texture with Unity UV (v=0 bottom)."""
    h, w = img.shape[:2]
    cols = (np.floor(u * w).astype(int)) % w
    rows = (h - 1 - np.floor(v * h).astype(int)) % h
    return img[rows, cols]


def _mask_sample(img, X, Y):
    """Sample a 3456x1728 field mask (rect -36..36, row 0 = far)."""
    cols = np.clip(((X + 36) * 48).astype(int), 0, img.shape[1] - 1)
    rows = np.clip(((36 - Y) * 24).astype(int), 0, img.shape[0] - 1)
    return img[rows, cols]


def paint_current(X, Y):
    grass = np.asarray(Image.open(TEX / 'FieldGrassOnly.png').convert('RGB')).astype(float)
    dirt = np.asarray(Image.open(TEX / 'FieldGravel.png').convert('RGB')).astype(float)
    water = np.asarray(Image.open(TEX / 'FieldWater.png').convert('RGB')).astype(float)
    pm = np.asarray(Image.open(TEX / 'FieldPathMask.png').convert('L'))
    wm = np.asarray(Image.open(TEX / 'FieldWaterMask.png').convert('L'))
    bm = np.asarray(Image.open(TEX / 'FieldBankMask.png').convert('L'))
    uv_u = X / 42.6666667 + 0.5
    uv_v = Y / 42.6666667 + 0.5
    rgb = _tex_sample(grass, uv_u, uv_v)
    path = _mask_sample(pm, X, Y) > 127
    rgb = np.where(path[..., None], _tex_sample(dirt, X / 2.666667, Y / 2.666667), rgb)
    raised = _mask_sample(bm, X, Y) > 127
    wet = (_mask_sample(wm, X, Y) > 127) & ~raised
    rgb = np.where(wet[..., None], _tex_sample(water, uv_u, uv_v), rgb)
    return rgb, raised


def _extrude(rgb, raised, lift_texels):
    """Raise `raised` ground by lift_texels rows, add lip + 3-band side faces (K21)."""
    th, tw = raised.shape
    out = rgb.copy()
    k = lift_texels
    sil = np.zeros_like(raised)
    for d in range(k + 1):
        sil[:th - d] |= raised[d:]
    top = np.zeros_like(raised)
    top[:th - k] = raised[k:]
    top_rgb = np.zeros_like(rgb)
    top_rgb[:th - k] = rgb[k:]
    face = sil & ~top
    out[top] = top_rgb[top]
    # Lip: first top row above a face -> 1.2x.
    lip = top & np.roll(face, -1, axis=0)
    out[lip] = np.clip(top_rgb[lip] * 1.2, 0, 255)
    # Face bands by distance below the top edge.
    depth = np.zeros(raised.shape, int)
    run = np.zeros(tw, int)
    for r in range(th):
        run = np.where(face[r], run + 1, 0)
        depth[r] = run
    band = np.clip(depth * 3 // (k + 1), 0, 2)
    out[face] = FACE[band[face]]
    return out


def _ground_image(rgb, raised):
    lift = int(round(0.36 * PH / 2))
    full = _extrude(rgb, raised, lift)
    img = np.repeat(np.repeat(full, 2, axis=0), 2, axis=1)[:H]
    return Image.fromarray(img.astype(np.uint8), 'RGB').convert('RGBA'), raised


# ------------------------------------------------------------------ cards and props

def _card_image(name, scale, flip, tint):
    im = sprite(name)
    w, h = im.size
    s = 2 * scale
    im = im.resize((max(1, int(round(w * s))), max(1, int(round(h * s)))), Image.NEAREST)
    if flip:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    if tint != 1.0:
        a = np.asarray(im).astype(float)
        a[..., :3] *= tint
        im = Image.fromarray(a.clip(0, 255).astype(np.uint8), 'RGBA')
    return im


def _shadow(layer, im, u, v):
    """Hard shadow: silhouette rows projected along the sun (to screen right/down), warm dark."""
    a = np.asarray(im)[..., 3] > 127
    h, w = a.shape
    sd = ImageDraw.Draw(layer)
    k = 1.6 * 0.7071
    for r in range(0, h, 2):
        cols = np.nonzero(a[r])[0]
        if not len(cols):
            continue
        hm = (h - r) / 96.0
        du = k * hm * PX
        dv = k * hm * PY
        x0 = u - w / 2 + cols[0] + du
        x1 = u - w / 2 + cols[-1] + du
        y = v + dv - 2
        sd.rectangle([x0, y, x1, y + 2], fill=(70, 52, 30, 120))


def _poly(draw, pts, fill, outline=OUTLINE):
    draw.polygon([tuple(map(float, p)) for p in pts], fill=fill, outline=outline)


def _draw_box(draw, cx, cy, yaw, lx, lz, h0, h1, colors, cam):
    """Axis box in prop-local frame (x = rail axis, z = long axis), yaw as in Unity."""
    th = math.radians(yaw)
    ax = np.array([math.cos(th), -math.sin(th)])
    az = np.array([math.sin(th), math.cos(th)])
    c = np.array([cx, cy])
    corners = [c + ax * sx * lx / 2 + az * sz * lz / 2 for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    P = lambda p, h: to_screen(p[0], p[1], h, *cam)
    top = [P(p, h1) for p in corners]
    # Front faces: those whose outward normal points toward the camera (-y).
    for i in range(4):
        a, b = corners[i], corners[(i + 1) % 4]
        mid = (a + b) / 2 - c
        if mid[1] < -1e-6:
            _poly(draw, [P(a, h0), P(b, h0), P(b, h1), P(a, h1)], colors[1])
    _poly(draw, top, colors[0])


def _draw_prop(draw, it, cam, flag_img, base):
    x, y, yaw, sc = it['x'], it['y'], it['yaw'], it['scale']
    name = it['prop']
    h = it['height']
    if name == 'fence':
        th = math.radians(yaw)
        ax = np.array([math.cos(th), -math.sin(th)])
        for off in (-0.4, 0.4):
            p = np.array([x, y]) + ax * off
            _draw_box(draw, p[0], p[1], yaw, 0.12, 0.12, h, h + 0.6, [WOOD[0], WOOD[1]], cam)
        for hh in (0.2, 0.42):
            _draw_box(draw, x, y, yaw, 0.9, 0.09, h + hh - 0.045, h + hh + 0.045, [WOOD[0], WOOD[2]], cam)
    elif name == 'sign':
        _draw_box(draw, x, y, yaw, 0.1, 0.1, h, h + 1.0, [WOOD[0], WOOD[1]], cam)
        _draw_box(draw, x, y, yaw, 0.8, 0.08, h + 0.64, h + 1.0, [WOOD[0], WOOD[1]], cam)
    elif name == 'flag':
        _draw_box(draw, x, y, 0, 0.065, 0.065, h, h + 1.6, [WOOD[1], WOOD[2]], cam)
        u, v = to_screen(x, y, h + 1.6, *cam)
        base.alpha_composite(flag_img, (int(u), int(v)))
    elif name == 'tent':
        th = math.radians(yaw)
        ax = np.array([math.cos(th), -math.sin(th)])
        az = np.array([math.sin(th), math.cos(th)])
        c = np.array([x, y])
        wx, lz, rh = 1.74 * sc / 2, 2.32 * sc / 2, 1.46 * sc
        P = lambda p, hh: to_screen(p[0], p[1], h + hh, *cam)
        fl, fr, bl, br = c - ax * wx - az * lz, c + ax * wx - az * lz, c - ax * wx + az * lz, c + ax * wx + az * lz
        rf, rb = c - az * lz, c + az * lz
        faces = [([bl, rb, rf, fl], [0, rh, rh, 0], TENT[1]), ([br, rb, rf, fr], [0, rh, rh, 0], TENT[0]),
                 ([fl, rf, fr], [0, rh, 0], TENT[2]), ([bl, rb, br], [0, rh, 0], TENT[2])]
        faces.sort(key=lambda f: -np.mean([p[1] for p in f[0]]))
        for pts, hs, col in faces:
            _poly(draw, [P(p, hh) for p, hh in zip(pts, hs)], col)
    elif name == 'bridge':
        _draw_box(draw, x, y, yaw, 1.8, 4.0, h, h + 0.3, [(194, 159, 130), (128, 98, 76)], cam)
        th = math.radians(yaw)
        ax = np.array([math.cos(th), -math.sin(th)])
        for off in (-0.82, 0.82):
            p = np.array([x, y]) + ax * off
            _draw_box(draw, p[0], p[1], yaw, 0.1, 4.0, h + 0.3, h + 0.75, [(163, 129, 103), (94, 71, 55)], cam)


def _hero_images():
    out = []
    for name in ('bruno', 'lena', 'mira', 'bolt'):
        p = HEROES / (name + '_down.png')
        if p.exists():
            im = Image.open(p).convert('RGBA')
            out.append(im.resize((im.width * 2, im.height * 2), Image.NEAREST))
    return out


def start_view(placements, ground_rgb, raised, cam=(0.0, 0.0), heroes=True, label=None):
    """Composite one 1920x1080 start screen."""
    base, _ = _ground_image(ground_rgb, raised)
    shadow_layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    lift = 0.36 * PH
    flag_img = sprite('flag_01').resize((sprite('flag_01').width * 2, sprite('flag_01').height * 2), Image.NEAREST)
    hw = W / 2 / PX + 1.5
    vis = [it for it in placements if abs(it['x'] - cam[0]) <= hw and -12.5 - 2.5 <= it['y'] - cam[1] <= 11.25 + 1.0]
    hero_list = []
    if heroes:
        imgs = _hero_images()
        spots = [(-0.55, 0.35), (0.55, 0.35), (-0.55, -0.35), (0.55, -0.35)]
        for im, (hx, hy) in zip(imgs, spots):
            hero_list.append({'hero': im, 'x': hx, 'y': hy, 'height': 0.0})
    drawables = []
    for it in vis:
        if it.get('kind') == 'card':
            tint = 0.72 if it['sprite'].startswith('rock_') or it['sprite'] == 'boulder_01' else 1.0
            im = _card_image(it['sprite'], it.get('scale', 1.0), it['isFlipped'], tint)
            u, v = to_screen(it['x'], it['y'], it['height'], *cam)
            if it['isCastingShadow']:
                _shadow(shadow_layer, im, u, v)
            drawables.append((it['y'], 0, it, im, u, v))
        else:
            drawables.append((it['y'] + (0.6 if it['prop'] == 'bridge' else 0), 1, it, None, 0, 0))
    for hd in hero_list:
        u, v = to_screen(hd['x'], hd['y'], 0, *cam)
        _shadow(shadow_layer, hd['hero'], u, v)
        drawables.append((hd['y'], 0, hd, hd['hero'], u, v))
    base.alpha_composite(shadow_layer)
    drawables.sort(key=lambda d: (-d[0], d[1]))
    draw = ImageDraw.Draw(base)
    for _, kind, it, im, u, v in drawables:
        if kind == 0:
            base.alpha_composite(im, (int(round(u - im.width / 2)), int(round(v - im.height))))
        else:
            _draw_prop(draw, it, cam, flag_img, base)
    if label:
        d = ImageDraw.Draw(base)
        d.rectangle([0, 0, 18 + 9 * len(label), 30], fill=(0, 0, 0, 160))
        d.text((8, 8), label, fill=(255, 255, 255, 255))
    return base.convert('RGB')


def pcg_start_view(L, label=None, cam=(0.0, 0.0)):
    X, Y = _texel_grid(*cam)
    placements = L.placements
    if getattr(L, 'terrain', None) is not None:
        from .terrain import sample
        screen_y = Y.copy()
        focus = float(sample(L.terrain, *cam))
        for _ in range(8):
            Y = screen_y - (sample(L.terrain, X, Y)-focus) * PH/PY
        placements = [dict(it, height=it['height']+float(sample(L.terrain,it['x'],it['y']))-focus) for it in placements]
    rgb, raised = paint_pcg(L, X, Y)
    return start_view(placements, rgb, raised, cam=cam, label=label, heroes=cam == (0.0, 0.0))


def current_start_view(layout_path, label=None, cam=(0.0, 0.0)):
    data = json.loads(Path(layout_path).read_text())
    X, Y = _texel_grid(*cam)
    rgb, raised = paint_current(X, Y)
    return start_view(data['placements'], rgb, raised, cam=cam, label=label, heroes=cam == (0.0, 0.0))


# ------------------------------------------------------------------ top-down map

GLYPH = {'tree': ((52, 92, 64), 0.75), 'rim': ((40, 70, 52), 0.75), 'bush': ((70, 120, 70), 0.45),
         'boulder': ((150, 146, 128), 0.5), 'stone': ((120, 116, 104), 0.18), 'shore': ((110, 106, 96), 0.15),
         'flowers': ((230, 200, 90), 0.08), 'fence': ((120, 80, 50), 0.2), 'sign': ((200, 120, 60), 0.3),
         'flag': ((200, 50, 50), 0.25), 'tent': ((235, 225, 200), 0.95), 'bridge': ((170, 130, 90), 0.9)}
FLOWER_COL = {'flower_01': (245, 245, 235), 'flower_02': (248, 184, 40), 'flower_03': (160, 90, 200),
              'grass_tuft_01': (90, 140, 60), 'grass_tuft_02': (110, 160, 70)}


def topdown_ground(sampler, ppm=10):
    n = int(72 * ppm)
    c = (np.arange(n) + 0.5) / ppm - 36
    X, Y = np.meshgrid(c, c[::-1])
    return X, Y


def topdown(placements, classify, title, cfg, ppm=10, extra=None):
    """classify(X, Y) -> dict with path/water/bank/worn/gravel/meadow/tone (bool/float arrays)."""
    X, Y = topdown_ground(None, ppm)
    G = classify(X, Y)
    rgb = np.zeros(X.shape + (3,))
    rgb[:] = (165, 160, 104)
    if 'tone' in G:
        rgb += G['tone'][..., None] * np.array([-18.0, 6.0, -8.0])
    for key, col in (('meadow', (176, 178, 96)), ('worn', (150, 128, 88)), ('gravel', (138, 134, 120)),
                     ('path', (110, 88, 58)), ('bank', (120, 140, 80)), ('water', (40, 70, 66))):
        if key in G:
            rgb[G[key]] = col
    outside = F.l1(X, Y) > 36
    rgb[outside] = (40, 40, 40)
    fieldout = (F.l1(X, Y) > 32) & ~outside
    rgb[fieldout] = rgb[fieldout] * 0.75
    img = Image.fromarray(rgb.clip(0, 255).astype(np.uint8), 'RGB')
    d = ImageDraw.Draw(img)
    n = X.shape[0]
    T = lambda x, y: ((x + 36) * ppm, (36 - y) * ppm)

    def diamond(r, col, w=1):
        d.polygon([T(0, r), T(r, 0), T(0, -r), T(-r, 0)], outline=col, width=w)
    diamond(32, (20, 20, 20), 2)
    for r, col in ((3.5, (255, 60, 60)), (6.0, (255, 140, 60)), (10.75, (255, 220, 120))):
        diamond(r, col)
    v = cfg['bounds']['visible']
    a, b, m = v['halfWidth'], v['halfDepth'], v['moveLimitL1']
    octo = [(a + m, b), (a, b + m), (-a, b + m), (-a - m, b), (-a - m, -b), (-a, -b - m), (a, -b - m), (a + m, -b)]
    d.polygon([T(*p) for p in octo], outline=(60, 60, 200), width=2)
    ss = cfg['bounds']['startScreen']
    d.rectangle([T(-ss['halfWidth'], ss['halfDepth']), T(ss['halfWidth'], -ss['halfDepth'])], outline=(255, 255, 255), width=2)
    counts = {}
    for it in sorted(placements, key=lambda it: GLYPH.get(it['group'] if not it['group'].startswith('wall_') else 'stone', ((0, 0, 0), .1))[1], reverse=True):
        grp = 'stone' if it['group'].startswith('wall_') else it['group']
        counts[grp] = counts.get(grp, 0) + 1
        col, r = GLYPH.get(grp, ((255, 0, 255), 0.2))
        if grp == 'flowers':
            col = FLOWER_COL.get(it['sprite'], col)
        x, y = T(it['x'], it['y'])
        rp = max(1.0, r * ppm)
        if it.get('kind') == 'prop' and it['prop'] in ('fence', 'bridge', 'tent'):
            th = math.radians(it['yaw'])
            if it['prop'] == 'fence':
                ax = np.array([math.cos(th), -math.sin(th)]) * 0.46
            else:
                ax = np.array([math.sin(th), math.cos(th)]) * (2.0 if it['prop'] == 'bridge' else 1.16)
            p0 = T(it['x'] - ax[0], it['y'] - ax[1])
            p1 = T(it['x'] + ax[0], it['y'] + ax[1])
            d.line([p0, p1], fill=col, width=max(2, int(ppm * (0.18 if it['prop'] == 'fence' else 1.6))))
        else:
            d.ellipse([x - rp, y - rp, x + rp, y + rp], fill=col, outline=(20, 30, 20) if grp in ('tree', 'rim', 'bush') else None)
    if extra:
        extra(d, T)
    legend = title + '  |  ' + '  '.join('%s %d' % kv for kv in sorted(counts.items()))
    d.rectangle([0, 0, n, 34], fill=(0, 0, 0))
    d.text((6, 4), legend[:150], fill=(255, 255, 255))
    d.text((6, 18), 'white = start screen 1920x1080, blue = visible octagon V1080, red/orange/yellow = L1 3.5 / 6 / 10.75', fill=(200, 200, 200))
    return img


def pcg_topdown(L, title):
    from .export import sample_ground

    def extra(d, T):
        if getattr(L, 'terrain', None) is not None:
            from .terrain import coordinates
            data = L.terrain
            heights = np.asarray(data['heightsMm']).reshape(data['depth'],data['width'])/1000.
            x,z = coordinates(data)
            for j in range(data['depth']-1):
                for i in range(data['width']-1):
                    for triangle in [[(j,i),(j,i+1),(j+1,i+1)],[(j,i),(j+1,i+1),(j+1,i)]]:
                        values=[heights[p] for p in triangle]
                        for level in np.arange(np.ceil(min(values)*10)/10,max(values),.1):
                            points=[]
                            for k in range(3):
                                a,b=triangle[k],triangle[(k+1)%3]
                                ha,hb=heights[a],heights[b]
                                if (ha <= level < hb) or (hb <= level < ha):
                                    t=(level-ha)/(hb-ha)
                                    points.append(T(x[a]+t*(x[b]-x[a]),z[a]+t*(z[b]-z[a])))
                            if len(points)==2:
                                d.line(points,fill=(63,90,43),width=1)
        if L.cross is not None:
            x, y = T(*L.cross)
            d.ellipse([x - 4, y - 4, x + 4, y + 4], outline=(255, 0, 0), width=2)
        if L.falls is not None:
            x, y = T(*L.falls[0])
            d.ellipse([x - 5, y - 5, x + 5, y + 5], outline=(0, 200, 255), width=2)
    return topdown(L.placements, lambda X, Y: sample_ground(L, X, Y), title, L.cfg, extra=extra)


def current_topdown(layout_path, cfg, title):
    data = json.loads(Path(layout_path).read_text())
    pm = np.asarray(Image.open(TEX / 'FieldPathMask.png').convert('L'))
    wm = np.asarray(Image.open(TEX / 'FieldWaterMask.png').convert('L'))
    bm = np.asarray(Image.open(TEX / 'FieldBankMask.png').convert('L'))

    def classify(X, Y):
        return {'path': _mask_sample(pm, X, Y) > 127, 'water': _mask_sample(wm, X, Y) > 127, 'bank': _mask_sample(bm, X, Y) > 127}
    return topdown(data['placements'], classify, title, cfg)
