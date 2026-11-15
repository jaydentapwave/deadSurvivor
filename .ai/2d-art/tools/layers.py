#!/usr/bin/env python3
"""Side-scroller scene tools: measure layers against the hero at game size, and a composite preview of one screen.

  layers.py scene --hero <png>[,scale=<s>] <name>=<png>[,scale=<s>]... [--luma-max <n>] [--stroke-ratio <name>=<lo>..<hi> ...]
                  [--palette '#RRGGBB,#RRGGBB,...']
  layers.py preview <out.png> --size <W>x<H> --ground <y> --hero <png> --hero-height <px> [--hero-x 0.25]
                  <name>=<png>[,scroll=<f>][,scale=<s>][,surface=<row>][,tile=no]... [--prop <png>,x=<px>,height=<px> ...]
                  [--shots 3] [--shift <px>]
  layers.py selftest

scene    measure.py judges one picture against its identity; it cannot say whether the pictures of one screen belong
         together. scene prints, per layer, at game size (scale = screen px per picture px: 0.5 for a PPU 200 sprite
         on a PPU 100 screen, 1 for a background drawn at screen size; default 1):
           outline px      the outline thickness on screen (median run of dark pixels, luma < --luma-max; default: the
                           approved art spec's outline.luma_max, else 60) and its ratio to the hero's outline
           colour          the layer's most common paint (outline left out), its dE76 from the hero's, and — with
                           --palette (the spec's colour table) — its dE76 from the nearest palette colour
           lightness       L* and the contrast ratio against the hero's paint ((L1+0.05)/(L2+0.05), relative luminance)
         Everything is INFO unless a range is given: --stroke-ratio bg_near=0.8..1.6 makes that layer's outline ratio
         PASS/FAIL (the spec's per-layer line-weight ratio). Exit 0 no FAIL / 1 FAIL / 2 input problem.
         Give every picture the scale it has in the game: a hero shrunk after the mock-up halves its line while layers
         generated at mock-up scale keep theirs.
preview  one screen composed the way the game will: layers back to front, each scaled (default: its height fits the
         screen height), tiled sideways, moved by camera x * scroll (0 = fixed sky, 1 = moves with the ground), bottom
         on the screen bottom — or, with surface=<row>, that picture row (the walkable top of a ground tile) on the
         --ground line. The hero's solid height becomes --hero-height px, feet on the ground line, at --hero-x of the
         width. Props stand on the ground line, left edge at world x. --shots pictures, the camera moved --shift px each time
         (default a quarter of the width), are stacked under a header "합성 — 게임 화면 아님" that lists every layer's
         scale and scroll — check them there before judging the picture. Not a measurement, not the game screen.
Needs numpy and Pillow; no art spec or identity needed."""
import argparse
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402

MARK = '합성 — 게임 화면 아님'
RED, GREEN, GREY, INK = (200, 30, 40), (30, 140, 60), (90, 90, 90), (20, 20, 20)


def parse_item(arg, named=True, numeric=('scale', 'scroll', 'surface', 'x', 'height')):
    """'<name>=<png>,k=v,...' (or '<png>,k=v,...' when not named) -> (name, path, {k: v})"""
    head, *opts = arg.split(',')
    name, path = (head.split('=', 1) + [''])[:2] if named else (Path(head).stem, head)
    if named and not path:
        raise K.KitError('BAD_LAYER', f'{arg}: want <name>=<png>[,key=value...]')
    kv = {}
    for o in opts:
        k, sep, v = o.partition('=')
        if not sep:
            raise K.KitError('BAD_LAYER', f'{arg}: option {o!r} is not key=value')
        try:
            kv[k] = float(v) if k in numeric else v
        except ValueError:
            raise K.KitError('BAD_LAYER', f'{arg}: {k} wants a number')
    if not Path(path).is_file():
        raise K.KitError('NO_IMAGE', path)
    return name, path, kv


# ---------- scene ----------

def _runs(mask):
    """per pixel: the length of the run of True it sits in, along rows"""
    h, w = mask.shape
    ids = np.cumsum(np.diff(mask.astype(np.int8), axis=1, prepend=0) != 0, axis=1) + (np.arange(h) * (w + 1))[:, None]
    return np.bincount(ids.ravel(), minlength=h * (w + 1))[ids] * mask


def outline_px(img, luma_max):
    """outline thickness in picture px: median over dark solid pixels of the shorter of the row run and the column run
    through it. A filled dark area (a pit) has long runs both ways and does not move the median of thin lines much."""
    dark = (img[..., 3] >= 200) & (K.luma(img[..., :3]) < luma_max)
    if int(dark.sum()) < 50:
        return None
    return float(np.median(np.minimum(_runs(dark), _runs(dark.T).T)[dark]))


def main_paint(img, luma_max):
    """the most common paint of the solid, non-outline pixels (RGB)"""
    m = (img[..., 3] >= 200) & (K.luma(img[..., :3]) >= luma_max)
    if not m.any():
        return None
    px = img[..., :3][m].astype(np.int64)
    code = px // 8 @ np.array([1024, 32, 1])
    codes, counts = np.unique(code, return_counts=True)
    return np.median(px[code == codes[int(np.argmax(counts))]], axis=0).astype(np.float32)


def rel_lum(rgb):
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    return float(0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2])


def scene(hero, layers, luma_max=60.0, ranges=None, palette=(), quiet=False):
    """hero: (path, scale); layers: [(name, path, scale)]. Returns (exit code, rows)"""
    ranges = dict(ranges or {})
    unknown = [n for n in ranges if n not in {x[0] for x in layers}]
    if unknown:
        raise K.KitError('UNKNOWN_LAYER', f'--stroke-ratio names no given layer: {unknown}')
    pal = [K.rgb_to_lab(K.hex_rgb(h)) for h in palette]
    rows, bad = [], 0
    base = None
    for name, path, scale in [('hero', hero[0], hero[1])] + list(layers):
        img = K.load_rgba(path)
        w, paint = outline_px(img, luma_max), main_paint(img, luma_max)
        row = {'name': name, 'scale': scale, 'outline': None if w is None else w * scale, 'paint': paint}
        if base is None:
            base = row
            if base['outline'] is None or paint is None:
                raise K.KitError('HERO_UNREADABLE', f'{path}: no outline (luma < {luma_max:g}) or no paint found in the hero')
        row['ratio'] = None if row['outline'] is None else row['outline'] / base['outline']
        if paint is not None:
            lab = K.rgb_to_lab(paint)
            row['L'] = float(lab[0])
            row['de_hero'] = K.de76(lab, K.rgb_to_lab(base['paint']))
            la, lb = rel_lum(paint), rel_lum(base['paint'])
            row['contrast'] = (max(la, lb) + 0.05) / (min(la, lb) + 0.05)
            row['de_palette'] = min((K.de76(lab, p) for p in pal), default=None)
        row['verdict'] = 'INFO'
        if name in ranges:
            lo, hi = ranges[name]
            row['verdict'] = 'NOT_ASSESSED' if row['ratio'] is None else 'PASS' if lo <= row['ratio'] <= hi else 'FAIL'
            row['range'] = (lo, hi)
            bad += row['verdict'] != 'PASS'
        rows.append(row)
    if not quiet:
        f = lambda v, fmt: '-' if v is None else format(v, fmt)  # noqa: E731
        print(f"{'':12} {'layer':14} {'scale':>5} {'outline px':>10} {'x hero':>7} {'paint':>8} {'L*':>5} {'dE hero':>8} {'contrast':>8}"
              + (f" {'dE palette':>10}" if pal else ''))
        for r in rows:
            hx = '-' if r['paint'] is None else '#%02x%02x%02x' % tuple(int(v) for v in r['paint'])
            print(f"{r['verdict']:12} {r['name']:14} {r['scale']:>5g} {f(r['outline'], '.1f'):>10} {f(r['ratio'], '.2f'):>7} {hx:>8} {f(r.get('L'), '.0f'):>5} "
                  f"{f(r.get('de_hero'), '.1f'):>8} {f(r.get('contrast'), '.2f'):>8}" + (f" {f(r.get('de_palette'), '.1f'):>10}" if pal else '')
                  + (f"  outline ratio wanted {r['range'][0]:g}..{r['range'][1]:g}" if 'range' in r else ''))
        print(f"scene {'FAIL' if bad else 'PASS' if ranges else 'INFO (no --stroke-ratio given: nothing judged)'}: outline measured on screen, "
              f"dark = luma < {luma_max:g}")
    return (1 if bad else 0), rows


# ---------- preview ----------

def _scaled(im, s):
    return im if abs(s - 1) < 1e-6 else im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)


def _solid_box(im):
    box = K.bbox(np.asarray(im)[..., 3] >= K.ALPHA_SOLID)
    if box is None:
        raise K.KitError('EMPTY_IMAGE', 'a picture has no solid pixel')
    return box


def compose(size, ground, hero, hero_height, hero_x, layers, props, cam):
    """one shot. layers: [(name, RGBA image already scaled, scroll, top y, tile)]; hero/props scaled RGBA with feet row"""
    w, h = size
    shot = Image.new('RGBA', size, (255, 255, 255, 255))
    for _, im, scroll, top, tile in layers:
        x0 = -round(cam * scroll)
        if tile:
            x0 = x0 % im.width - im.width
            for x in range(x0, w, im.width):
                shot.alpha_composite(im, (x, top)) if 0 <= x and x + im.width <= w and 0 <= top and top + im.height <= h else _paste_clipped(shot, im, x, top)
        else:
            _paste_clipped(shot, im, x0, top)
    for im, feet, x in props:
        _paste_clipped(shot, im, round(x - cam), ground - feet)
    him, hfeet, hcx = hero
    _paste_clipped(shot, him, round(w * hero_x - hcx), ground - hfeet)
    return shot


def _paste_clipped(dst, im, x, y):
    """alpha_composite that accepts positions partly or fully outside the canvas"""
    x0, y0, x1, y1 = max(0, x), max(0, y), min(dst.width, x + im.width), min(dst.height, y + im.height)
    if x0 < x1 and y0 < y1:
        dst.alpha_composite(im.crop((x0 - x, y0 - y, x1 - x, y1 - y)), (x0, y0))


def preview(out, size, ground, hero_path, hero_height, layers, hero_x=0.25, props=(), shots=3, shift=None, quiet=False):
    w, h = size
    if not 0 < ground <= h:
        raise K.KitError('BAD_GROUND', f'--ground {ground} is outside the screen height {h}')
    notes, built = [], []
    for name, path, kv in layers:
        im = Image.open(path).convert('RGBA')
        s = float(kv.get('scale', h / im.height))
        sim = _scaled(im, s)
        top = round(ground - kv['surface'] * s) if 'surface' in kv else h - sim.height
        scroll = float(kv.get('scroll', 1.0))
        built.append((name, sim, scroll, top, kv.get('tile', 'yes') != 'no'))
        notes.append(f"{name}: {im.width}x{im.height} x{s:.3f} -> {sim.width}x{sim.height}, scroll {scroll:g}, "
                     + (f"row {kv['surface']:g} on the ground line" if 'surface' in kv else f'bottom on the screen bottom (top y={top})'))
    him = Image.open(hero_path).convert('RGBA')
    hb = _solid_box(him)
    hs = hero_height / (hb[3] - hb[1])
    hsim = _scaled(him, hs)
    hb2 = _solid_box(hsim)
    hero = (hsim, hb2[3], (hb2[0] + hb2[2]) / 2)
    notes.append(f'hero: solid height {hb[3] - hb[1]} x{hs:.3f} -> {hb2[3] - hb2[1]}px = {(hb2[3] - hb2[1]) / h:.1%} of the screen height, feet on y={ground}')
    pbuilt = []
    for _, path, kv in props:
        if 'x' not in kv or 'height' not in kv:
            raise K.KitError('BAD_PROP', f'--prop {path}: needs x=<world px> and height=<px>')
        pim = Image.open(path).convert('RGBA')
        pb = _solid_box(pim)
        ps = kv['height'] / (pb[3] - pb[1])
        psim = _scaled(pim, ps)
        pbuilt.append((psim, _solid_box(psim)[3], kv['x']))
        notes.append(f"prop {Path(path).name}: x{ps:.3f} -> {round(kv['height'])}px high at world x={kv['x']:g}")
    shift = w // 4 if shift is None else shift
    cams = [i * shift for i in range(max(1, shots))]
    font, pad, line = K.font(15), 12, 20
    head = [f'{MARK} (composite preview, not a game screen)', f'screen {w}x{h}, ground line y={ground}, camera x = ' + ', '.join(str(c) for c in cams)] + notes
    top0 = pad * 2 + line * len(head)
    sheet = Image.new('RGB', (w + pad * 2, top0 + len(cams) * (h + line + pad)), (245, 245, 245))
    d = ImageDraw.Draw(sheet)
    d.rectangle([0, 0, sheet.width, 6], fill=GREY)
    for i, t in enumerate(head):
        d.text((pad, pad + i * line), t, fill=INK if i else GREY, font=font)
    for i, cam in enumerate(cams):
        y = top0 + i * (h + line + pad)
        d.text((pad, y), f'{MARK} · camera x = {cam}', fill=GREY, font=font)
        sheet.paste(compose(size, ground, hero, hero_height, hero_x, built, pbuilt, cam).convert('RGB'), (pad, y + line))
        d.rectangle([0, y + line + ground - 1, pad - 3, y + line + ground], fill=RED)           # ground line tick in the margin
        d.rectangle([pad + w + 3, y + line + ground - 1, sheet.width, y + line + ground], fill=RED)
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    if not quiet:
        for n in notes:
            print('  ' + n)
        print(f'PREVIEW {out} ({len(cams)} shots, marked "{MARK}")')
    return 0


# ---------- selftest ----------

def _blob(size, fill, line_w, outline=(30, 14, 8)):
    im = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(im).ellipse([size * 0.1, size * 0.1, size * 0.9, size * 0.9], fill=fill + (255,), outline=outline + (255,), width=line_w)
    return im


def selftest():
    td = Path(tempfile.mkdtemp(prefix='layers-selftest-'))
    cases = []

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    hero, thick, thin = td / 'hero.png', td / 'near.png', td / 'log.png'
    _blob(400, (250, 140, 40), 6).save(hero)                 # PPU 200 sprite: 6px line -> 3px on screen
    _blob(400, (70, 150, 60), 12).save(thick)                # drawn at screen size: 12px line
    _blob(400, (150, 90, 40), 7).save(thin)                  # PPU 200 prop: 7px -> 3.5px
    rc, rows = scene((hero, 0.5), [('bg_near', thick, 1.0), ('log', thin, 0.5)], quiet=True)
    r = {x['name']: x for x in rows}
    case('outline on screen: hero 3px, layer drawn at screen size 12px (x4), prop 3.5px', rc == 0 and abs(r['hero']['outline'] - 3) <= 0.5
         and abs(r['bg_near']['ratio'] - 4) <= 0.5 and abs(r['log']['ratio'] - 1.17) <= 0.2,
         ', '.join(f"{n} {x['outline']:.1f}px x{x['ratio']:.2f}" for n, x in r.items()))
    case('paint and lightness: the layer paint is found, darker than the hero', np.abs(r['bg_near']['paint'] - (70, 150, 60)).max() <= 4
         and r['bg_near']['L'] < r['hero']['L'] and r['bg_near']['contrast'] > 1.2 and r['bg_near']['de_hero'] > 30)
    rc, rows = scene((hero, 0.5), [('bg_near', thick, 1.0), ('log', thin, 0.5)], ranges={'bg_near': (0.8, 1.6), 'log': (0.8, 1.6)}, quiet=True)
    v = {x['name']: x['verdict'] for x in rows}
    case('--stroke-ratio 0.8..1.6: the thick layer FAILs, the prop PASSes, exit 1', rc == 1 and v == {'hero': 'INFO', 'bg_near': 'FAIL', 'log': 'PASS'}, str(v))
    case('command line: exit 0 with nothing judged, 2 for a layer that is not given',
         main(['scene', '--hero', f'{hero},scale=0.5', f'near={thick}']) == 0
         and main(['scene', '--hero', str(hero), f'near={thick}', '--stroke-ratio', 'far=1..2']) == 2)
    # preview
    sky = Image.new('RGBA', (320, 180), (150, 200, 240, 255))
    far = Image.new('RGBA', (320, 180), (0, 0, 0, 0))
    ImageDraw.Draw(far).rectangle([0, 100, 319, 179], fill=(120, 170, 200, 255))
    ImageDraw.Draw(far).rectangle([40, 60, 60, 179], fill=(200, 60, 60, 255))                # a marker post
    tile = Image.new('RGBA', (200, 120), (0, 0, 0, 0))
    ImageDraw.Draw(tile).rectangle([0, 40, 199, 119], fill=(120, 80, 50, 255))               # walkable top at row 40
    ImageDraw.Draw(tile).rectangle([0, 40, 9, 119], fill=(60, 40, 25, 255))
    for n, im in (('sky', sky), ('far', far), ('tile', tile), ('crate', _blob(200, (60, 80, 200), 6))):
        im.save(td / f'{n}.png')
    out = td / 'preview.png'
    layers = [('sky', td / 'sky.png', {'scroll': 0.0}), ('far', td / 'far.png', {'scroll': 0.25}), ('ground', td / 'tile.png', {'scale': 0.5, 'surface': 40.0})]
    preview(out, (640, 360), 300, hero, 90, layers, props=[('crate', td / 'crate.png', {'x': 400.0, 'height': 50.0})], shots=2, shift=160, quiet=True)
    im = np.asarray(Image.open(out).convert('RGB')).astype(int)
    case('preview written with the grey "not a game screen" band', out.is_file() and tuple(im[3, 5]) == GREY)
    pad, line = 12, 20
    top0 = pad * 2 + line * (2 + 5)
    shot = lambda i: im[top0 + i * (360 + line + pad) + line:top0 + i * (360 + line + pad) + line + 360, pad:pad + 640]  # noqa: E731
    s0, s1 = shot(0), shot(1)
    is_hero = lambda s: (np.abs(s - (250, 140, 40)).max(-1) < 30)  # noqa: E731
    ys = np.flatnonzero(is_hero(s0).any(axis=1))
    case('hero: 90px high, feet on the ground line (y=300), at a quarter of the width', abs((ys.max() - ys.min() + 1) - 90) <= 8 and abs(ys.max() + 4 - 300) <= 5
         and abs(np.flatnonzero(is_hero(s0).any(axis=0)).mean() - 160) <= 3, f'rows {ys.min()}..{ys.max()}')
    case('ground tile: its row 40 sits on the ground line at scale 0.5 (soil at y=300, not above)', np.abs(s0[302, 350] - (120, 80, 50)).max() < 12
         and np.abs(s0[297, 350] - (120, 80, 50)).max() > 40)
    post = lambda s: int(np.flatnonzero(np.abs(s[150] - (200, 60, 60)).max(-1) < 30)[0])  # noqa: E731
    case('second shot (camera +160): the far layer (scroll 0.25) moved 40px, the sky did not move', post(s0) - post(s1) == 40
         and np.array_equal(s0[10, :200], s1[10, :200]), f'post x {post(s0)} -> {post(s1)}')
    is_log = lambda s: np.abs(s - (60, 80, 200)).max(-1) < 25  # noqa: E731
    lx0, lx1 = np.flatnonzero(is_log(s0).any(axis=0)).mean(), np.flatnonzero(is_log(s1).any(axis=0)).mean()
    case('prop stands on the ground line and moves with the world (160px)', abs((lx0 - lx1) - 160) <= 2 and abs(np.flatnonzero(is_log(s0).any(axis=1)).max() - 298) <= 4)
    case('command line preview: exit 0; ground line outside the screen: exit 2',
         main(['preview', str(td / 'p2.png'), '--size', '640x360', '--ground', '300', '--hero', str(hero), '--hero-height', '90',
               f'sky={td / "sky.png"},scroll=0', f'ground={td / "tile.png"},scale=0.5,surface=40']) == 0
         and main(['preview', str(td / 'p3.png'), '--size', '640x360', '--ground', '900', '--hero', str(hero), '--hero-height', '90', f'sky={td / "sky.png"}']) == 2)
    bad = cases.count(False)
    print(f"layers selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('scene')
    s.add_argument('--hero', required=True)
    s.add_argument('layers', nargs='+')
    s.add_argument('--luma-max', type=float)
    s.add_argument('--stroke-ratio', nargs='*', default=[], metavar='NAME=LO..HI')
    s.add_argument('--palette', default='')
    p = sub.add_parser('preview')
    p.add_argument('out')
    p.add_argument('--size', required=True)
    p.add_argument('--ground', type=int, required=True)
    p.add_argument('--hero', required=True)
    p.add_argument('--hero-height', type=float, required=True)
    p.add_argument('--hero-x', type=float, default=0.25)
    p.add_argument('layers', nargs='+')
    p.add_argument('--prop', action='append', default=[])
    p.add_argument('--shots', type=int, default=3)
    p.add_argument('--shift', type=int)
    sub.add_parser('selftest')
    a = ap.parse_args(argv)
    if a.cmd == 'selftest':
        return selftest()
    try:
        if a.cmd == 'scene':
            luma = a.luma_max
            if luma is None:
                try:
                    luma = K.approved_tolerance(K.load_kit())[1]['luma_max']
                except K.KitError:
                    luma = 60.0
            _, hpath, hkv = parse_item(a.hero, named=False)
            layers = [(n, pth, float(kv.get('scale', 1.0))) for n, pth, kv in (parse_item(x) for x in a.layers)]
            ranges = {}
            for item in a.stroke_ratio:
                name, sep, rng = item.partition('=')
                lo, sep2, hi = rng.partition('..')
                try:
                    ranges[name] = (float(lo), float(hi))
                except ValueError:
                    raise K.KitError('BAD_RANGE', f'--stroke-ratio wants <layer>=<lo>..<hi>, got {item}')
            pal = [x for x in a.palette.split(',') if x]
            return scene((hpath, float(hkv.get('scale', 1.0))), layers, luma, ranges, pal)[0]
        try:
            w, h = (int(x) for x in a.size.lower().split('x'))
        except ValueError:
            raise K.KitError('BAD_SIZE', f'--size wants <W>x<H>, got {a.size}')
        if not Path(a.hero).is_file():
            raise K.KitError('NO_IMAGE', a.hero)
        return preview(a.out, (w, h), a.ground, a.hero, a.hero_height, [parse_item(x) for x in a.layers], a.hero_x,
                       [parse_item(x, named=False) for x in a.prop], a.shots, a.shift)
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
