"""3dkit texture stage (system Python with numpy + Pillow: /usr/bin/python3).

Methods (picked by the profile's standards[kind].color, see kitlib/pipeline.py):
- palette (lowpoly, from Deadsurvivor 050, code unchanged so outputs stay byte-identical):
  - build_char_palette: 8x4 palette. Fixed identity colours are row 0; the other cells are an OKLab k-means over texels
    pooled from every source texture with equal weight per texture (fixed seed).
  - quantize: texture -> spec size (box filter) -> every texel to the nearest palette colour in OKLab (optionally only
    some palette rows, optionally after moving the texture's median onto a base cell and compressing the deviation, as
    the 043 face repaint does) -> 3x3 majority filter (removes single-texel specks) -> RGB PNG. Alpha is not written:
    the Unity importer drops alpha so the palette shader reads A = 1 (not emissive).
- passthrough (midpoly): each map -> spec size (box filter); normal maps are renormalised after the resize. No colour
  change. Alpha is not written.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

COLS, ROWS, CELL = 8, 4, 8
IDENTITY_RADIUS = 0.06   # OKLab distance: texels this close to a fixed identity colour are not used for the free colours
np.seterr(all='ignore')   # macOS Accelerate matmul raises spurious FP warnings (as in palette_extract_043.py); results are checked finite


def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def rgb_to_oklab(rgb):
    lin = srgb_to_linear(rgb)
    m1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929], [0.2119034982, 0.6806995451, 0.1073969566],
                   [0.0883024619, 0.2817188376, 0.6299787005]])
    m2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468], [1.9779984951, -2.4285922050, 0.4505937099],
                   [0.0259040371, 0.7827717662, -0.8086757660]])
    lab = np.cbrt(lin @ m1.T) @ m2.T
    if not np.isfinite(lab).all():
        raise SystemExit('NON_FINITE_OKLAB')
    return lab


def oklab_to_rgb(lab):
    m2i = np.array([[1.0, 0.3963377774, 0.2158037573], [1.0, -0.1055613458, -0.0638541728], [1.0, -0.0894841775, -1.2914855480]])
    m1i = np.array([[4.0767416621, -3.3077115913, 0.2309699292], [-1.2684380046, 2.6097574011, -0.3413193965],
                    [-0.0041960863, -0.7034186147, 1.7076147010]])
    return linear_to_srgb(((lab @ m2i.T) ** 3) @ m1i.T)


def hex_rgb(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float) / 255


def rgb_hex(rgb):
    return '#%02x%02x%02x' % tuple(int(round(float(x) * 255)) for x in np.clip(rgb, 0, 1))


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def load_rgb(path, size=None):
    im = Image.open(path).convert('RGB')
    if size and im.size != (size, size):
        im = im.resize((size, size), Image.Resampling.BOX)
    return np.asarray(im, dtype=np.float64) / 255


def nearest(lab, centers):
    """index of the nearest centre per row (chunked: no N x K x 3 blow-up on big inputs)"""
    out = np.empty(len(lab), int)
    for i in range(0, len(lab), 65536):
        x = lab[i:i + 65536]
        out[i:i + 65536] = np.argmin(((x[:, None, :] - centers[None]) ** 2).sum(-1), axis=1)
    return out


def kmeans_fixed(x, fixed, k_free, seed, iters=50):
    """k-means where the first len(fixed) centres never move (k-means++ start for the free ones)."""
    rng = np.random.default_rng(seed)
    c = [f for f in fixed]
    for _ in range(k_free):
        d = np.min(((x[:, None, :] - np.array(c)[None]) ** 2).sum(-1), axis=1)
        c.append(x[rng.choice(len(x), p=d / d.sum())] if d.sum() > 0 else x[rng.integers(len(x))])
    c = np.array(c)
    nf = len(fixed)
    for _ in range(iters):
        lab = nearest(x, c)
        nc = c.copy()
        for i in range(nf, len(c)):
            if np.any(lab == i):
                nc[i] = x[lab == i].mean(0)
        if np.allclose(nc, c, atol=1e-6):
            break
        c = nc
    return c, nearest(x, c)


def build_char_palette(textures, fixed, out_png, out_json, seed, per_texture=6000, identity_textures=None):
    """textures: {name: path}; fixed: {name: '#hex'} (row 0, in the given order); identity_textures: names whose texels near
    an identity colour are kept out of the free clustering (default: all)."""
    if len(fixed) > COLS:
        raise SystemExit('TOO_MANY_FIXED')
    rng = np.random.default_rng(seed)
    pools, owner = [], []
    for name, p in textures.items():
        lab = rgb_to_oklab(load_rgb(p, 128).reshape(-1, 3))
        pools.append(lab[rng.choice(len(lab), size=min(per_texture, len(lab)), replace=False)])
        owner += [identity_textures is None or name in identity_textures] * len(pools[-1])
    x = np.concatenate(pools)
    flab = rgb_to_oklab(np.array([hex_rgb(h) for h in fixed.values()]))
    # texels close to an identity colour belong to it: the free colours are clustered from the rest only, so no free cell
    # can sit next to an identity cell and take its shades (050: bruno's texture is a shade darker than #d79b68)
    near = (np.min(((x[:, None, :] - flab[None]) ** 2).sum(-1), axis=1) < IDENTITY_RADIUS ** 2) & np.array(owner)
    centers, _ = kmeans_fixed(x[~near], flab, COLS * ROWS - len(flab), seed)
    lab = nearest(x, centers)
    share = np.bincount(lab, minlength=len(centers)) / len(lab)
    nf = len(flab)
    free = list(range(nf, len(centers)))
    # row 0: fixed identity colours, then the two most used free colours; rows 1-3: the rest by hue group, then lightness
    free.sort(key=lambda i: -share[i])
    row0_extra, rest = free[:COLS - nf], free[COLS - nf:]

    def key(i):
        L, a, b = centers[i]
        C = np.hypot(a, b)
        h = (np.degrees(np.arctan2(b, a)) + 360) % 360
        return (1 if C < 0.03 else 0, round(h / 30) if C >= 0.03 else 0, L)
    rest.sort(key=key)
    order = list(range(nf)) + row0_extra + rest
    names = list(fixed.keys())
    img = np.zeros((ROWS * CELL, COLS * CELL, 3), np.uint8)
    cells = []
    for idx, ci in enumerate(order):
        r, col = divmod(idx, COLS)
        rgb = np.clip(oklab_to_rgb(centers[ci][None])[0], 0, 1)
        img[r * CELL:(r + 1) * CELL, col * CELL:(col + 1) * CELL] = np.round(rgb * 255).astype(np.uint8)
        cells.append(dict(index=idx, row=r, col=col, hex=rgb_hex(rgb), oklab=[round(float(v), 4) for v in centers[ci]],
                          fixed=names[ci] if ci < nf else None, pixel_share=round(float(share[ci]), 5)))
    Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(img).save(out_png)
    rec = dict(status='050 character palette (user asked for palette characters, 2026-11-03)',
               method=f'row 0 = fixed identity colours + 2 most used; hero texels within OKLab {IDENTITY_RADIUS} of an identity colour excluded from the free clustering; OKLab k-means seed {seed}, {per_texture} texels per texture at 128 px, equal weight',
               layout=dict(cols=COLS, rows=ROWS, cell_px=CELL, png_row0='top', alpha='none (importer drops alpha: A = 1)'),
               sources={n: dict(path=str(p), sha256=sha(p)) for n, p in textures.items()}, fixed=fixed,
               palette_sha256=sha(out_png), cells=cells)
    Path(out_json).write_text(json.dumps(rec, ensure_ascii=False, indent=1) + '\n')
    return rec


def palette_colors(palette_json, rows=None):
    p = json.loads(Path(palette_json).read_text())
    cells = [c for c in p['cells'] if rows is None or c['row'] in rows]
    return np.array([hex_rgb(c['hex']) for c in cells]), [c['index'] for c in cells], p


def majority3(idx, k):
    """3x3 majority of palette indices; the centre wins ties (keeps thin features such as eye lines when they hold)."""
    h, w = idx.shape
    pad = np.pad(idx, 1, mode='edge')
    counts = np.zeros((k, h, w), np.int16)
    for dy in range(3):
        for dx in range(3):
            win = pad[dy:dy + h, dx:dx + w]
            for v in np.unique(win):
                counts[v] += (win == v)
    best = counts.argmax(0)
    centre = counts[idx, np.arange(h)[:, None], np.arange(w)[None]]
    return np.where(centre >= counts.max(0), idx, best)


def quantize(src, palette_json, size, out_png, rows=None, base_cell=None, compress=1.0, snap_cell=None):
    """snap_cell: palette cell index of a hero's identity colour; texels within IDENTITY_RADIUS of it always take it
    (the identity region keeps the data colour even when a free cell is a little closer to the texture's shade)"""
    rgb = load_rgb(src, size)
    pal_rgb, pal_index, _ = palette_colors(palette_json, rows)
    pal_lab = rgb_to_oklab(pal_rgb)
    lab = rgb_to_oklab(rgb.reshape(-1, 3))
    orig = lab.copy()
    if base_cell is not None:
        all_rgb, all_idx, _ = palette_colors(palette_json)
        target = rgb_to_oklab(all_rgb[all_idx.index(base_cell)][None])[0]
        med = np.median(lab, axis=0)
        lab = target + (lab - med) * compress
    n = nearest(lab, pal_lab)
    if snap_cell is not None:
        si = pal_index.index(snap_cell)
        n[((lab - pal_lab[si]) ** 2).sum(-1) < IDENTITY_RADIUS ** 2] = si
    n = majority3(n.reshape(size, size), len(pal_lab)).reshape(-1)
    out = pal_rgb[n].reshape(size, size, 3)
    Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.round(out * 255).astype(np.uint8)).save(out_png)
    used = np.bincount(n, minlength=len(pal_lab)) / len(n)
    de = np.sqrt(((pal_lab[n] - orig) ** 2).sum(-1))
    return dict(source=str(src), source_sha256=sha(src), size=size, palette=str(palette_json), rows=rows, base_cell=base_cell, compress=compress, snap_cell=snap_cell,
                out=str(out_png), out_sha256=sha(out_png),
                cells_used={str(pal_index[i]): round(float(u), 4) for i, u in enumerate(used) if u > 0.002},
                oklab_error=dict(median=round(float(np.median(de)), 4), p95=round(float(np.percentile(de, 95)), 4)))


def identity_share(png, palette_json, cell_index):
    """share of texels that are exactly the given palette cell"""
    rgb = np.round(load_rgb(png) * 255).astype(int).reshape(-1, 3)
    p = json.loads(Path(palette_json).read_text())
    c = np.round(hex_rgb(p['cells'][cell_index]['hex']) * 255).astype(int)
    return float((rgb == c).all(1).mean())


def passthrough(src, size, out_png, map_name='albedo'):
    """midpoly texture stage: resize to the spec size (box filter), no colour change. A normal map is renormalised after
    the resize (box-averaged unit vectors get shorter). The source size is recorded; a smaller source is upscaled and
    flagged so the check report shows it."""
    with Image.open(src) as im:
        src_size = im.size
    rgb = load_rgb(src, size)
    if map_name == 'normal':
        v = rgb * 2 - 1
        v = v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-6)
        rgb = (v + 1) / 2
    Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.round(np.clip(rgb, 0, 1) * 255).astype(np.uint8)).save(out_png)
    return dict(map=map_name, source=str(src), source_sha256=sha(src), source_size=list(src_size), size=size,
                upscaled=max(src_size) < size, out=str(out_png), out_sha256=sha(out_png))


def selftest():
    import tempfile
    d = Path(tempfile.mkdtemp(prefix='asset-texture-'))
    # passthrough: size, normal renormalisation, upscale flag
    n = np.zeros((64, 64, 3), np.uint8); n[:] = (128, 128, 255); n[:, :32] = (255, 128, 128)
    Image.fromarray(n).save(d / 'n.png')
    r = passthrough(d / 'n.png', 16, d / 'n16.png', 'normal')
    v = np.asarray(Image.open(d / 'n16.png'), float) / 255 * 2 - 1
    assert r['size'] == 16 and not r['upscaled'] and v.shape == (16, 16, 3), r
    assert np.allclose(np.linalg.norm(v, axis=-1), 1, atol=0.02), 'normal not renormalised'
    assert passthrough(d / 'n.png', 128, d / 'n128.png', 'albedo')['upscaled'] is True
    a = np.zeros((32, 32, 3), np.uint8); a[:, :16] = (215, 155, 104); a[:, 16:] = (40, 40, 40); a[5, 5] = (40, 40, 40)
    Image.fromarray(a).save(d / 'a.png')
    rec = build_char_palette({'a': d / 'a.png'}, {'bruno': '#d79b68'}, d / 'p.png', d / 'p.json', 1, per_texture=500)
    assert rec['cells'][0]['hex'] == '#d79b68' and len(rec['cells']) == 32, rec['cells'][0]
    q = quantize(d / 'a.png', d / 'p.json', 32, d / 'q.png')
    out = np.asarray(Image.open(d / 'q.png'))
    assert tuple(out[5, 5]) == (215, 155, 104), 'speck not removed'
    assert identity_share(d / 'q.png', d / 'p.json', 0) > 0.45
    assert q['oklab_error']['median'] < 0.02, q
    return 'texture selftest PASS (palette + passthrough)'


if __name__ == '__main__':
    print(selftest())
