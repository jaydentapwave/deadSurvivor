#!/usr/bin/env python3
"""Solid-background removal and the fringe check that follows it (2d-art-produce step 3, before a picture goes into the game).

  matte.py key <png>... --out <dir> [--key auto|#RRGGBB] [--inner 48] [--feather 3] [--border-only] [--trim [PAD]]
  matte.py fringe <png>... [--key #RRGGBB] [--tol 0.10] [--edge-px 2] [--allow 0]
  matte.py fix <png>... --out <dir> [--key #RRGGBB] [--tol 0.10] [--edge-px 2]
  matte.py selftest

key     removes a one-colour background. --key auto (default) reads the colour from the picture border (generated
        backgrounds are rarely the exact colour asked for); a border that is not one colour is an input problem.
        Background = pixels within --inner (RGB distance) of the key. Only the --feather px next to the background get
        a soft alpha, measured against the paint just inside (how far the pixel sits between that paint and the key),
        and their colour is un-mixed from the key. Everything further in stays opaque whatever its hue: green leaves on
        a green key are not eaten. --border-only keeps key-coloured areas the background does not reach (use it when a
        prop really is the key colour; holes between limbs are then kept too). --trim [PAD] crops to the figure plus
        PAD px (default 8). The key colour is written into the PNG (text chunk kit2d:key) for fringe/fix.
        Still pick a key colour the subject does not use: a mix of the key and a neighbouring hue cannot be told apart.
fringe  judges what keying leaves on the rim; seam and measure checks do not see it:
          spill   semi-transparent pixels that still carry the key colour (more than --tol above the paint inside)
          edge    opaque pixels in the outer --edge-px whose colour is pulled toward the key the same way
          stray   faint pixels (alpha < 128) further than 2px from the solid figure
        PASS when each count <= --allow. The key comes from --key, else from the PNG text chunk matte.py key wrote.
fix     repairs those without regenerating: stray pixels dropped, key colour taken out of the rim, pixels over --tol
        repainted with the paint just inside (alpha kept). Run fringe on the result.
Inside a kit project fringe also writes work_dir/measure/<sha256>.fringe.json; sheet.py leaves out a picture whose
fringe record is FAIL. Needs no art spec or identity.
Exit: 0 PASS (key/fix: written) / 1 FAIL / 2 input problem. Needs numpy and Pillow."""
import argparse
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image
from PIL.PngImagePlugin import PngInfo

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402

KEY_CHUNK = 'kit2d:key'
OPAQUE = 250            # soft hand-drawn bodies rarely reach exactly 255
REACH = 6               # how far the inside paint is carried outward past the edge band
LINE_RES, LINE_SLOPE = 8.0, 0.3   # "paint mixed with the key" = this close to the paint->key line (RGB): 8 + 30% of the way travelled
FAINT = 8               # alpha below this is not counted (invisible); fix still cleans it
PURE = np.array([[255, 0, 255], [0, 255, 0], [0, 0, 255], [0, 255, 255], [255, 0, 0], [255, 255, 0]], dtype=np.float32)
KEY_HUED = 0.005        # more than this share of the inside paint carrying the key's hue = the art itself uses that hue


def grow(mask, r):
    """square (chessboard) dilation by r px, no wrap-around"""
    if r <= 0:
        return mask.copy()
    h, w = mask.shape
    p = np.pad(mask, r)
    rows = np.zeros_like(p)
    for d in range(2 * r + 1):
        rows[:, r:r + w] |= p[:, d:d + w]
    out = np.zeros_like(mask)
    for d in range(2 * r + 1):
        out |= rows[d:d + h, r:r + w]
    return out


def spread(val, known, steps, target=None):
    """carry known colours outward: each unknown (target) pixel takes the mean of its known 8 neighbours, `steps` times"""
    val, known = val.copy(), known.copy()
    h, w = known.shape
    for _ in range(int(steps)):
        todo = ~known if target is None else target & ~known
        if not todo.any():
            break
        pv = np.pad(val * known[..., None], ((1, 1), (1, 1), (0, 0)))
        pk = np.pad(known, 1).astype(np.float32)
        acc = np.zeros_like(val)
        cnt = np.zeros((h, w), dtype=np.float32)
        for dy in (0, 1, 2):
            for dx in (0, 1, 2):
                if dy == 1 and dx == 1:
                    continue
                acc += pv[dy:dy + h, dx:dx + w]
                cnt += pk[dy:dy + h, dx:dx + w]
        fill = todo & (cnt > 0)
        val[fill] = acc[fill] / cnt[fill][:, None]
        known |= fill
    return val, known


def key_channels(key):
    """bool[3]: channels the key is strong in; None for a grey/white/black key (no hue to tell spill by)"""
    if float(key.max() - key.min()) < 60:
        return None
    return key > (float(key.max()) + float(key.min())) / 2


def spill(rgb, hi):
    return np.clip(rgb[..., hi].min(axis=-1) - rgb[..., ~hi].max(axis=-1), 0, None)


def keyness(rgb, inside, known, key, absolute=True):
    """0..1 per pixel: how far the colour is pulled from the paint inside toward the key.
    Two readings, the larger wins: the key's own channel excess over the paint's (colour-difference keying; an outline
    or any other paint has none), and the position between paint and key when the pixel sits on that line (a paint
    that is merely lighter or another hue is off the line). Where no inside paint is known (features too thin to have
    an inside), the channel excess counts as it is when `absolute` (the art uses no key-hued paint), else nothing does."""
    k = np.zeros(rgb.shape[:2], dtype=np.float32)
    hi = key_channels(key)
    if hi is not None:
        s = spill(rgb, hi)
        s_in = np.where(known, spill(inside, hi), 0 if absolute else s)
        k = np.clip((s - s_in) / np.maximum(float(spill(key, hi)) - s_in, 1), 0, 1)
    kf, cf = key - inside, rgb - inside
    den = (kf ** 2).sum(axis=-1)
    t = np.where(known & (den > 60 ** 2), (cf * kf).sum(axis=-1) / np.maximum(den, 1), 0)
    t = np.clip(t, 0, 1)
    off = np.linalg.norm(cf - t[..., None] * kf, axis=-1)
    on_line = off <= LINE_RES + LINE_SLOPE * t * np.sqrt(den)
    return np.maximum(k, np.where(on_line, t, 0)).astype(np.float32)


def inside_paint(rgb, opaque, visible, edge_px, key):
    """(colour map, known, edge band, absolute): the paint edge_px+ inside the figure, carried out over the edge band
    and the soft rim. Features too thin to have an inside stay unknown (map = their own colour). absolute: the inside
    paint has no key-hued colour, so key hue on those thin features is spill too."""
    band = opaque & grow(~opaque, edge_px)
    deep = opaque & ~band
    val, known = spread(rgb, deep, edge_px, target=opaque)
    val, known = spread(val, known, edge_px + REACH, target=visible)
    val[~known] = rgb[~known]
    hi = key_channels(key)
    absolute = hi is not None and (not deep.any() or float((spill(rgb[deep], hi) > 0.1 * float(spill(key, hi))).mean()) < KEY_HUED)
    return val, known, band, absolute


def analyse(img, key, tol=0.10, edge_px=2):
    """masks of the three fringe kinds on an RGBA float image, plus what fix needs"""
    rgb, a = img[..., :3], img[..., 3]
    visible, opaque = a > 0, a >= OPAQUE
    semi = visible & ~opaque
    val, known, band, absolute = inside_paint(rgb, opaque, visible, edge_px, key)
    k = keyness(rgb, val, known, key, absolute)
    stray = visible & (a < K.ALPHA_SOLID) & ~grow(a >= K.ALPHA_SOLID, 2)
    seen = a >= FAINT
    return {'spill': semi & (k > tol) & ~stray & seen, 'edge': band & (k > tol), 'stray': stray & seen, 'k': k, 'inside': val,
            'known': known, 'band': band, 'semi': semi, 'all_stray': stray}


def read_key(path, key=None):
    if key and key != 'auto':
        if not __import__('re').fullmatch(r'#?[0-9A-Fa-f]{6}', key):
            raise K.KitError('BAD_KEY', f'--key wants #RRGGBB, got {key}')
        return K.hex_rgb(key)
    try:
        text = getattr(Image.open(path), 'text', {}) or {}
    except (OSError, ValueError) as e:
        raise K.KitError('BAD_IMAGE', f'{path}: {e}')
    if KEY_CHUNK not in text:
        raise K.KitError('NO_KEY', f'{Path(path).name}: no key colour recorded in the file (not written by matte.py key, or resized since) -> --key #RRGGBB')
    return K.hex_rgb(text[KEY_CHUNK])


def border_key(rgb):
    """the background colour read from the outermost pixels. A layer that fills the bottom and the sides (ground, tree
    line) leaves little background on the border, and flat cel-shaded paint is as flat as a generated background, so:
    a colour close to a pure chroma key (#ff00ff, #00ff00, ...) that covers 5% of the border or more wins; otherwise
    one colour must cover 60% of it. The colour found is printed — look at it."""
    edge = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    code = (edge // 32).astype(np.int64) @ np.array([64, 8, 1])
    codes, counts = np.unique(code, return_counts=True)
    pure, plain, share = None, None, 0.0
    for i in np.argsort(counts)[::-1][:6]:
        seed = np.median(edge[code == codes[i]], axis=0)
        near = np.linalg.norm(edge - seed, axis=-1) <= 40
        cover = float(near.mean())
        share = max(share, cover)
        key = np.median(edge[near], axis=0)
        if cover >= 0.05 and float(np.linalg.norm(PURE - key, axis=-1).min()) <= 100 and (pure is None or cover > pure[0]):
            pure = (cover, key)
        if cover >= 0.6 and plain is None:
            plain = (cover, key)
    best = pure or plain
    if best is None:
        raise K.KitError('KEY_NOT_FOUND', f'no background colour found on the border (the most common colour covers {share:.0%}, '
                         'and none is close to a pure key colour) -> --key #RRGGBB')
    return best[1].astype(np.float32)


def hexs(key):
    return '#%02x%02x%02x' % tuple(int(round(float(v))) for v in key)


def save(arr, path, key):
    info = PngInfo()
    info.add_text(KEY_CHUNK, hexs(key))
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(path, pnginfo=info)


def fix_array(img, key, tol=0.10, edge_px=2):
    """(fixed image, counts). Alpha changes only where stray pixels are dropped."""
    out = img.copy()
    an = analyse(img, key, tol, edge_px)
    out[an['all_stray']] = 0
    rim = (an['semi'] | an['band']) & ~an['all_stray']
    k, inside, known = an['k'], an['inside'], an['known']
    over = rim & (k > tol) & known
    part = rim & (k > 0) & ~over & known
    lone = rim & (k > 0) & ~known                                        # no inside paint to copy: take the key's hue out
    rgb = out[..., :3]
    kk = np.minimum(k[part], 0.6)[:, None]
    rgb[part] = np.clip((rgb[part] - kk * key) / (1 - kk), 0, 255)       # un-mix the small share of key colour
    rgb[over] = inside[over]
    hi = key_channels(key)
    if hi is not None and lone.any():
        px = rgb[lone]
        px[:, hi] -= spill(px, hi)[:, None]
        rgb[lone] = px
    return out, {'spill': int(an['spill'].sum()), 'edge': int(an['edge'].sum()), 'stray': int(an['all_stray'].sum()),
                 'repainted': int(over.sum())}


def key_array(rgb, key, inner=48.0, feather=3, border_only=False):
    """(RGBA float image, info) from an opaque RGB picture on a one-colour background"""
    import frames as F
    core = np.linalg.norm(rgb - key, axis=-1) <= inner
    border = np.zeros_like(core)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    reached = F._flood(border, core)
    enclosed = int((core & ~reached).sum())
    if border_only:
        core = reached
    band = grow(core, feather) & ~core
    solid = ~core & ~band
    inside, known = spread(rgb, solid, feather + 2, target=band)
    k = keyness(rgb, inside, known, key)
    # a thin feature with no paint further in: nothing to measure against, so its distance from the key decides
    far = np.linalg.norm(rgb - key, axis=-1)
    k = np.where(band & ~known, np.clip(1 - (far - inner) / max(1.0, 140.0 - inner), 0, 1), k)
    alpha = np.ones(core.shape, dtype=np.float32)
    alpha[band] = 1 - k[band]
    alpha[core] = 0
    out_rgb = rgb.copy()
    mixed = band & (k > 0)
    low = mixed & (k < 0.6)
    kk = k[low][:, None]
    out_rgb[low] = np.clip((rgb[low] - kk * key) / (1 - kk), 0, 255)
    high = mixed & ~low & known
    out_rgb[high] = inside[high]
    alpha[alpha < 8 / 255] = 0
    out = np.dstack([out_rgb, alpha * 255]).astype(np.float32)
    out[out[..., 3] == 0] = 0
    out, counts = fix_array(out, key)                                    # stray specks and whatever the un-mix left
    return out, {'enclosed': enclosed, 'kept_near_key': int((solid & (far < 140)).sum()), 'stray': counts['stray']}


def cmd_key(paths, out_dir, key='auto', inner=48.0, feather=3, border_only=False, trim=None, quiet=False):
    for p in paths:
        try:
            src = Image.open(p)
            rgb = np.asarray(src.convert('RGB'), dtype=np.float32)
        except (OSError, ValueError) as e:
            raise K.KitError('BAD_IMAGE', f'{p}: {e}')
        kc = border_key(rgb) if key in (None, 'auto') else read_key(p, key)
        out, info = key_array(rgb, kc, inner, feather, border_only)
        box = K.bbox(out[..., 3] >= 16)
        if box is None:
            raise K.KitError('EMPTY_IMAGE', f'{p}: nothing left after keying {hexs(kc)} (the whole picture is the key colour)')
        if trim is not None:
            x0, y0, x1, y1 = box
            out = out[max(0, y0 - trim):y1 + trim, max(0, x0 - trim):x1 + trim]
        dst = Path(out_dir) / (Path(p).stem + '.png')
        save(out, dst, kc)
        if not quiet:
            notes = [f'key {hexs(kc)}', f'{out.shape[1]}x{out.shape[0]}', f"opaque {float((out[..., 3] >= OPAQUE).mean()):.3f}"]
            if info['kept_near_key']:
                notes.append(f"kept {info['kept_near_key']} px of near-key colour inside the figure")
            if info['enclosed']:
                notes.append(f"INFO {info['enclosed']} px of key colour not reached from the border "
                             + ('kept (--border-only)' if border_only else 'removed (holes; if a prop was eaten: --border-only or another key colour)'))
            print(f'KEY {dst}: ' + ', '.join(notes))
    return 0


def record(path, verdict, counts, key, tol, edge_px, allow):
    try:
        kit = K.load_kit(Path(path).resolve().parent)
    except K.KitError:
        return
    K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': 'fringe', 'image': str(Path(path).resolve()), 'sha256': K.sha256(path),
                         'verdict': verdict, 'key': hexs(key), 'tol': tol, 'edge_px': edge_px, 'allow': allow, **counts})


def cmd_fringe(paths, key=None, tol=0.10, edge_px=2, allow=0, quiet=False):
    bad = 0
    for p in paths:
        kc = read_key(p, key)
        an = analyse(K.load_rgba(p), kc, tol, edge_px)
        counts = {n: int(an[n].sum()) for n in ('spill', 'edge', 'stray')}
        fail = any(v > allow for v in counts.values())
        bad += fail
        record(p, 'FAIL' if fail else 'PASS', counts, kc, tol, edge_px, allow)
        if not quiet:
            where = ''
            if fail:
                worst = max(counts, key=counts.get)
                ys, xs = np.nonzero(an[worst])
                where = f'  (look at x={int(xs[0])}, y={int(ys[0])}: {worst})'
            print(f"{'FAIL' if fail else 'PASS'} {Path(p).name}: key {hexs(kc)}, spill {counts['spill']} px, edge {counts['edge']} px, "
                  f"stray {counts['stray']} px (allow {allow}){where}")
    if not quiet:
        print(f"{'FAIL' if bad else 'PASS'} fringe: {len(paths)} pictures, {bad} with key colour left on the rim" + (' -> matte.py fix' if bad else ''))
    return 1 if bad else 0


def cmd_fix(paths, out_dir, key=None, tol=0.10, edge_px=2, quiet=False):
    for p in paths:
        kc = read_key(p, key)
        out, c = fix_array(K.load_rgba(p), kc, tol, edge_px)
        dst = Path(out_dir) / Path(p).name
        save(out, dst, kc)
        if not quiet:
            print(f"FIX {dst}: key {hexs(kc)}, spill {c['spill']} px, edge {c['edge']} px, {c['repainted']} px repainted from inside, "
                  f"{c['stray']} stray px dropped")
    return 0


# ---------- selftest ----------

def _scene(key, body=(240, 140, 40), leaf=None, outline=(30, 12, 8), size=160, noise=0):
    """antialiased outlined disc (and an un-outlined leaf blob) on a flat key background; drawn 4x and scaled down"""
    from PIL import ImageDraw
    s = size * 4
    im = Image.new('RGB', (s, s), tuple(key))
    d = ImageDraw.Draw(im)
    d.ellipse([s * 0.2, s * 0.25, s * 0.7, s * 0.75], fill=body, outline=outline, width=20)
    if leaf:
        d.ellipse([s * 0.6, s * 0.15, s * 0.9, s * 0.4], fill=leaf)
    im = im.resize((size, size), Image.LANCZOS)
    if noise:
        a = np.asarray(im).astype(np.int16)
        rng = np.random.default_rng(3)
        a += rng.integers(-noise, noise + 1, a.shape)
        im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGB')
    return im


def _naive(im, key, cut=140):
    """what a quick distance threshold leaves: soft alpha, colours untouched"""
    a = np.asarray(im.convert('RGB'), dtype=np.float32)
    d = np.linalg.norm(a - np.asarray(key, dtype=np.float32), axis=-1)
    alpha = np.clip((d - 60) / (cut - 60), 0, 1) * 255
    return Image.fromarray(np.dstack([a, alpha]).astype(np.uint8), 'RGBA')


def selftest():
    td = Path(tempfile.mkdtemp(prefix='matte-selftest-'))
    cases = []

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    def counts(path, key=None, **kw):
        an = analyse(K.load_rgba(path), read_key(path, key), **kw)
        return {n: int(an[n].sum()) for n in ('spill', 'edge', 'stray')}

    mag, grn = (255, 0, 255), (0, 255, 0)
    src = td / 'disc.png'
    _scene(mag).save(src)
    naive = td / 'naive.png'
    _naive(Image.open(src), mag).save(naive)
    c = counts(naive, '#ff00ff')
    case('a distance-threshold cut-out leaves key colour on the rim: fringe FAIL', cmd_fringe([naive], '#ff00ff', quiet=True) == 1 and c['spill'] > 50, str(c))
    cmd_key([src], td / 'keyed', quiet=True)
    keyed = td / 'keyed/disc.png'
    c = counts(keyed)
    a = np.asarray(Image.open(keyed))
    case('matte.py key (key read from the border, written into the PNG): fringe PASS', cmd_fringe([keyed], quiet=True) == 0 and sum(c.values()) == 0
         and Image.open(keyed).text.get(KEY_CHUNK) == '#ff00ff', str(c))
    semi = (a[..., 3] > 0) & (a[..., 3] < 250)
    case('the rim is soft (semi-transparent pixels) and the background is gone', semi.sum() > 100 and a[2, 2, 3] == 0 and a[80, 70, 3] == 255,
         f'{int(semi.sum())} soft px')
    cmd_fix([naive], td / 'fixed', '#ff00ff', quiet=True)
    c = counts(td / 'fixed/naive.png')
    case('matte.py fix on the naive cut-out: fringe PASS, alpha untouched', sum(c.values()) == 0
         and np.array_equal(np.asarray(Image.open(td / 'fixed/naive.png'))[..., 3], np.asarray(Image.open(naive))[..., 3]), str(c))
    # the reported failure: green leaves on a green key
    leafy = td / 'leafy.png'
    _scene(grn, leaf=(60, 160, 60)).save(leafy)
    leaf_px = lambda im: int(((np.asarray(im)[..., 3] >= 250) & (np.abs(np.asarray(im)[..., :3].astype(int) - (60, 160, 60)).max(-1) < 12)).sum())  # noqa: E731
    eaten = leaf_px(_naive(Image.open(leafy), grn))
    cmd_key([leafy], td / 'keyed', quiet=True)
    kept = leaf_px(Image.open(td / 'keyed/leafy.png'))
    want = int((np.abs(np.asarray(Image.open(leafy)).astype(int) - (60, 160, 60)).max(-1) < 12).sum())
    case('green leaf on a green key: the leaf stays opaque (a distance cut-out makes all of it see-through)', eaten < 0.05 * want and kept >= 0.97 * want,
         f'leaf px {want}: naive {eaten}, key {kept}')
    case('... and its rim passes fringe', cmd_fringe([td / 'keyed/leafy.png'], quiet=True) == 0, str(counts(td / 'keyed/leafy.png')))
    # generated backgrounds are not the colour asked for
    off = td / 'off.png'
    _scene((13, 167, 42), noise=4).save(off)
    cmd_key([off], td / 'keyed', quiet=True)
    got = Image.open(td / 'keyed/off.png').text.get(KEY_CHUNK)
    case('an off-colour noisy background (asked #00ff00, got about #0da72a) is found from the border', got is not None
         and np.abs(K.hex_rgb(got) - (13, 167, 42)).max() <= 3 and cmd_fringe([td / 'keyed/off.png'], quiet=True) == 0, f'{got} {counts(td / "keyed/off.png")}')
    # a figure that really is purple must not be read as spill
    purple = td / 'purple.png'
    _scene(mag, body=(150, 60, 190)).save(purple)
    cmd_key([purple], td / 'keyed', quiet=True)
    pa = np.asarray(Image.open(td / 'keyed/purple.png'))
    inside_ok = np.abs(pa[80, 70, :3].astype(int) - (150, 60, 190)).max() <= 2
    case('purple figure on a magenta key: paint kept, fringe PASS (judged against the paint inside)', inside_ok
         and cmd_fringe([td / 'keyed/purple.png'], quiet=True) == 0, str(counts(td / 'keyed/purple.png')))
    # stray faint pixels
    dusty = np.asarray(Image.open(keyed)).copy()
    dusty[5:8, 100:104] = (255, 0, 255, 40)
    Image.fromarray(dusty, 'RGBA').save(td / 'dusty.png')
    c = counts(td / 'dusty.png', '#ff00ff')
    case('faint pixels away from the figure: counted as stray, FAIL', c['stray'] == 12 and cmd_fringe([td / 'dusty.png'], '#ff00ff', quiet=True) == 1, str(c))
    cmd_fix([td / 'dusty.png'], td / 'fixed', '#ff00ff', quiet=True)
    case('fix drops them', sum(counts(td / 'fixed/dusty.png').values()) == 0)
    # opaque edge mixed with the key (no outline, hard alpha): the 'edge' count
    flat = np.asarray(_scene(mag, outline=(240, 140, 40)).convert('RGB'), dtype=np.float32)
    hard = np.dstack([flat, np.where(np.linalg.norm(flat - mag, axis=-1) > 60, 255, 0)]).astype(np.uint8)
    Image.fromarray(hard, 'RGBA').save(td / 'hard.png')
    c = counts(td / 'hard.png', '#ff00ff')
    case('hard-alpha cut-out of an un-outlined shape: opaque edge pixels pulled toward the key are counted', c['edge'] > 50 and c['spill'] == 0, str(c))
    cmd_fix([td / 'hard.png'], td / 'fixed', '#ff00ff', quiet=True)
    case('fix repaints them from the paint inside', sum(counts(td / 'fixed/hard.png').values()) == 0, str(counts(td / 'fixed/hard.png')))
    # trim, border-only, input problems
    cmd_key([src], td / 'trim', trim=8, quiet=True)
    t = Image.open(td / 'trim/disc.png')
    case('--trim crops to the figure plus the pad', t.size[0] < 110 and t.size[1] < 110 and K.bbox(np.asarray(t)[..., 3] >= 16)[0] == 8, str(t.size))
    ring = _scene(mag)
    from PIL import ImageDraw
    ImageDraw.Draw(ring).ellipse([60, 70, 80, 90], fill=mag)
    ring.save(td / 'ring.png')
    cmd_key([td / 'ring.png'], td / 'keyed', quiet=True)
    cmd_key([td / 'ring.png'], td / 'bo', border_only=True, quiet=True)
    case('a key-coloured area inside the figure: removed by default, kept with --border-only',
         np.asarray(Image.open(td / 'keyed/ring.png'))[80, 70, 3] == 0 and np.asarray(Image.open(td / 'bo/ring.png'))[80, 70, 3] == 255)
    layer = _scene(mag)
    ground = np.random.default_rng(7).integers(-14, 15, (90, 160, 3)) + (60, 120, 70)   # painted ground: most of the border, not flat
    layer.paste(Image.fromarray(ground.astype(np.uint8), 'RGB'), (0, 70))
    layer.save(td / 'layer.png')
    cmd_key([td / 'layer.png'], td / 'keyed', quiet=True)
    case('a layer whose painted ground is the most common colour on the border: the chroma colour is still the key',
         Image.open(td / 'keyed/layer.png').text.get(KEY_CHUNK) == '#ff00ff' and np.asarray(Image.open(td / 'keyed/layer.png'))[150, 80, 3] == 255)
    rng = np.random.default_rng(5)
    Image.fromarray(rng.integers(0, 256, (64, 64, 3), dtype=np.uint8), 'RGB').save(td / 'busy.png')
    case('a border with no common colour: exit 2 (KEY_NOT_FOUND)', main(['key', str(td / 'busy.png'), '--out', str(td / 'x')]) == 2)
    plain = td / 'plain.png'
    Image.fromarray(np.asarray(Image.open(keyed)), 'RGBA').save(plain)
    case('fringe on a file with no recorded key and no --key: exit 2', main(['fringe', str(plain)]) == 2)
    case('exit codes through the command line: FAIL 1, PASS 0', main(['fringe', str(naive), '--key', '#ff00ff']) == 1 and main(['fringe', str(keyed)]) == 0)
    # kit project: the verdict is recorded for sheet.py
    import json
    root = td / 'proj'
    (root / '.ai/2d-art').mkdir(parents=True)
    (root / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'work_dir': 'out'}))
    Image.open(naive).save(root / 'naive.png')
    cmd_fringe([root / 'naive.png'], '#ff00ff', quiet=True)
    rec = K.read_record(K.load_kit(root), K.sha256(root / 'naive.png'), 'fringe')
    case('kit project: fringe record written (FAIL with the counts)', rec and rec['verdict'] == 'FAIL' and rec['spill'] > 0)
    bad = cases.count(False)
    print(f"matte selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    k = sub.add_parser('key')
    k.add_argument('paths', nargs='+')
    k.add_argument('--out', type=Path, required=True)
    k.add_argument('--key', default='auto')
    k.add_argument('--inner', type=float, default=48.0)
    k.add_argument('--feather', type=int, default=3)
    k.add_argument('--border-only', action='store_true')
    k.add_argument('--trim', type=int, nargs='?', const=8)
    f = sub.add_parser('fringe')
    f.add_argument('paths', nargs='+')
    f.add_argument('--allow', type=int, default=0)
    x = sub.add_parser('fix')
    x.add_argument('paths', nargs='+')
    x.add_argument('--out', type=Path, required=True)
    for s in (f, x):
        s.add_argument('--key')
        s.add_argument('--tol', type=float, default=0.10)
        s.add_argument('--edge-px', type=int, default=2)
    sub.add_parser('selftest')
    a = ap.parse_args(argv)
    if a.cmd == 'selftest':
        return selftest()
    try:
        missing = [str(p) for p in a.paths if not Path(p).is_file()]
        if missing:
            raise K.KitError('NO_IMAGE', ', '.join(missing))
        if a.cmd == 'key':
            return cmd_key(a.paths, a.out, a.key, a.inner, max(1, a.feather), a.border_only, a.trim)
        if a.cmd == 'fringe':
            return cmd_fringe(a.paths, a.key, a.tol, max(1, a.edge_px), a.allow)
        return cmd_fix(a.paths, a.out, a.key, a.tol, max(1, a.edge_px))
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
