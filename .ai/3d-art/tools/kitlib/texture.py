"""3dkit texture stage (system Python with numpy + Pillow: /usr/bin/python3).

Methods (picked by the profile's standards[kind].color, see kitlib/pipeline.py):
- palette (lowpoly, from an earlier project (050), code unchanged so outputs stay byte-identical):
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
    if map_name == 'smoothness':    # Unity layout RGBA (R metallic, A smoothness): keep the alpha, resize all four channels
        with Image.open(src) as im:
            if 'A' not in im.getbands():
                raise SystemExit(f'SMOOTHNESS_MAP_NEEDS_ALPHA:{src} (make it with assets.py orm; A = smoothness)')
            im = im.convert('RGBA')
            if im.size != (size, size):
                im = im.resize((size, size), Image.Resampling.BOX)
            Path(out_png).parent.mkdir(parents=True, exist_ok=True)
            im.save(out_png)
        return dict(map=map_name, source=str(src), source_sha256=sha(src), source_size=list(src_size), size=size,
                    upscaled=max(src_size) < size, out=str(out_png), out_sha256=sha(out_png))
    rgb = load_rgb(src, size)
    if map_name == 'normal':
        v = rgb * 2 - 1
        v = v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-6)
        rgb = (v + 1) / 2
    Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.round(np.clip(rgb, 0, 1) * 255).astype(np.uint8)).save(out_png)
    return dict(map=map_name, source=str(src), source_sha256=sha(src), source_size=list(src_size), size=size,
                upscaled=max(src_size) < size, out=str(out_png), out_sha256=sha(out_png))


def glb_orm_image(glb):
    """the metallicRoughness image of a Tripo GLB (pbr=true; Tripo names it ORM: G = roughness, B = metallic) as PIL
    Image, or None when the GLB has none. Reads the glTF JSON + buffer only (no Blender)."""
    import io
    import struct
    b = Path(glb).read_bytes()
    if b[:4] != b'glTF':
        raise SystemExit(f'NOT_A_GLB:{glb}')
    jl = struct.unpack('<I', b[12:16])[0]
    j = json.loads(b[20:20 + jl])
    base = 20 + jl + 8      # BIN chunk data
    for m in j.get('materials') or []:
        t = (m.get('pbrMetallicRoughness') or {}).get('metallicRoughnessTexture')
        if t is None:
            continue
        img = j['images'][j['textures'][t['index']]['source']]
        bv = j['bufferViews'][img['bufferView']]
        o = base + bv.get('byteOffset', 0)
        return Image.open(io.BytesIO(b[o:o + bv['byteLength']]))
    return None


def _glb(glb):
    import struct
    b = Path(glb).read_bytes()
    if b[:4] != b'glTF':
        raise SystemExit(f'NOT_A_GLB:{glb}')
    jl = struct.unpack('<I', b[12:16])[0]
    return b, json.loads(b[20:20 + jl]), 20 + jl + 8


def _saturation_p90(rgb, alpha=None):
    """90th percentile of HSV saturation over the foreground (alpha > 0.5 and not the corner background colour)"""
    mx, mn = rgb.max(2), rgb.min(2)
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    corner = np.median(np.concatenate([rgb[:8, :8].reshape(-1, 3), rgb[-8:, -8:].reshape(-1, 3)]), 0)
    fg = np.abs(rgb - corner).max(2) > 0.08
    if alpha is not None:
        fg &= alpha > 0.5
    v = s[fg] if fg.sum() > 500 else s.ravel()
    return float(np.percentile(v, 90))


def image_saturation(path):
    """colourfulness of a concept image (foreground only): 90th percentile saturation, 0..1"""
    a = np.asarray(Image.open(path).convert('RGBA').resize((256, 256))).astype(float) / 255
    return _saturation_p90(a[..., :3], a[..., 3])


def glb_stats(glb):
    """triangles of a GLB and the colourfulness of its base colour texture (None when it has none). Calibrated on 57 Tripo
    results (2026-11-05): the colourless S11 frog had p90 0.006 from a 0.87 concept; every good result of a colourful concept
    (concept p90 >= 0.5) kept p90 >= 0.27; grey rocks come from grey concepts (p90 < 0.07)."""
    import io
    b, j, base = _glb(glb)
    tris = 0
    for m in j.get('meshes') or []:
        for prim in m.get('primitives') or []:
            if prim.get('mode', 4) != 4:
                continue
            acc = j['accessors'][prim['indices']] if 'indices' in prim else j['accessors'][prim['attributes']['POSITION']]
            tris += acc['count'] // 3
    sat = None
    for mt in j.get('materials') or []:
        t = (mt.get('pbrMetallicRoughness') or {}).get('baseColorTexture')
        if t is None:
            continue
        img = j['images'][j['textures'][t['index']]['source']]
        bv = j['bufferViews'][img['bufferView']]
        o = base + bv.get('byteOffset', 0)
        a = np.asarray(Image.open(io.BytesIO(b[o:o + bv['byteLength']])).convert('RGB').resize((256, 256))).astype(float) / 255
        mx, mn = a.max(2), a.min(2)
        sat = float(np.percentile(np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0), 90))   # texture atlas: no background
        break
    return dict(tris=tris, sat_p90=None if sat is None else round(sat, 3))


def smoothness_from_orm(orm, out_png):
    """glTF metallicRoughness (G = roughness) -> Unity URP Lit _MetallicGlossMap layout: A = smoothness = 1 - roughness,
    R = metallic = 0 (the midpoly presets use Metallic 0; Tripo's B channel is not trusted for metal), G/B = 0.
    The Lit shader multiplies A by _Smoothness, so the material sets _Smoothness = 1 when this map is present
    (profile material.map_floats). Returns summary stats of the smoothness."""
    with orm if isinstance(orm, Image.Image) else Image.open(orm) as im:
        rough = np.asarray(im.convert('RGB'), np.float64)[..., 1] / 255
    out = np.zeros(rough.shape + (4,), np.uint8)
    out[..., 3] = np.round((1 - rough) * 255).astype(np.uint8)
    Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out, 'RGBA').save(out_png)
    sm = out[..., 3] / 255
    return dict(out=str(out_png), size=list(rough.shape[::-1]), smoothness=dict(min=round(float(sm.min()), 3), mean=round(float(sm.mean()), 3), max=round(float(sm.max()), 3)))


# ---------------- colour fit (midpoly terrain: albedo -> the art standard's region colours) ----------------
FIT_SOFT_SIGMA = 0.04      # OKLab feature distance over which a pixel's region weight blends into the next region
FIT_STD_RANGE = (0.5, 1.5)  # a region's lightness spread is scaled by target/source std, kept inside this range (the pattern stays)


def _fit_features(lab):
    return np.stack([lab[..., 0], lab[..., 1] * 0.5, lab[..., 2] * 0.5], -1)


def _blur_lab(rgb, radius):
    """Gaussian blur in sRGB (PIL) then OKLab: thin cracks join the block they sit in, so a pixel's region follows its block"""
    from PIL import ImageFilter
    im = Image.fromarray(np.round(np.clip(rgb, 0, 1) * 255).astype(np.uint8))
    return rgb_to_oklab(np.asarray(im.filter(ImageFilter.GaussianBlur(radius)), float) / 255)


def fit_regions(rgb, k, blur_px=5.0, iters=30):
    """split an albedo (H, W, 3 in 0..1) into k colour regions by k-means on the blurred image (OKLab L, a/2, b/2),
    centres ordered dark -> bright. Returns (centres, soft weights (H, W, k), hard labels (H, W))."""
    lab = rgb_to_oklab(rgb)
    f = _fit_features(_blur_lab(rgb, blur_px))
    flat = f.reshape(-1, 3)
    sub = flat[:: max(1, len(flat) // 40000)]
    centres = np.stack([[np.percentile(sub[:, 0], 100 * (i + 0.5) / k), 0.0, 0.0] for i in range(k)])
    for c in range(k):   # start each centre at the mean colour of the pixels near its lightness quantile
        near = sub[np.abs(sub[:, 0] - centres[c, 0]) < 0.04]
        if len(near):
            centres[c] = near.mean(0)
    for _ in range(iters):
        lab_id = ((sub[:, None, :] - centres[None]) ** 2).sum(-1).argmin(1)
        new = np.stack([sub[lab_id == c].mean(0) if (lab_id == c).any() else centres[c] for c in range(k)])
        if np.allclose(new, centres, atol=1e-5):
            break
        centres = new
    order = np.argsort(centres[:, 0])
    centres = centres[order]
    d2 = ((f[..., None, :] - centres) ** 2).sum(-1)
    w = np.exp(-(d2 - d2.min(-1, keepdims=True)) / (2 * FIT_SOFT_SIGMA ** 2))
    w /= w.sum(-1, keepdims=True)
    return centres, w, d2.argmin(-1)


def region_report(rgb, k, blur_px=5.0):
    """mean colour (hex), share and lightness std of each region (dark -> bright) + the mean colour of the whole image"""
    _, _, lab_id = fit_regions(rgb, k, blur_px)
    lab = rgb_to_oklab(rgb)
    out = []
    for c in range(k):
        m = lab_id == c
        out.append(dict(share=round(float(m.mean()), 4), mean_hex=rgb_hex(oklab_to_rgb(lab[m].mean(0))) if m.any() else None,
                        mean_lab=lab[m].mean(0) if m.any() else None, l_std=round(float(lab[m][:, 0].std()), 4) if m.any() else None))
    return out, lab.reshape(-1, 3).mean(0)


def delta_e_ok(a, b):
    """OKLab distance x 100 (ΔE_ok; about 1 = just noticeable)"""
    return float(np.linalg.norm(np.asarray(a, float) - np.asarray(b, float)) * 100)


def color_fit(src_png, out_png, regions, blur_px=5.0, split=None):
    """move an albedo onto the art standard's colours, keeping its pattern. regions: list dark -> bright of
    {name, hex, l_std (optional)}. Pixels are split into len(regions) colour regions (fit_regions; soft edges); each region's
    mean (OKLab) moves to the target hex and its lightness deviation is scaled by l_std / source std (clamped to
    FIT_STD_RANGE; a/b deviation kept), so block shapes and cracks stay. The output is checked finite and written as RGB PNG.
    Returns a report with per-region share, before/target/after mean colours and the ΔE_ok of after vs target.
    split: a geometry split from geometry_split (texels placed by where they sit on the model, not by colour); then every
    group (one side x one height band) moves its own mean to its region's target and texels off the model keep their colour."""
    if split is not None:
        return _color_fit_split(src_png, out_png, regions, split)
    rgb = load_rgb(src_png)
    k = len(regions)
    centres, w, lab_id = fit_regions(rgb, k, blur_px)
    lab = rgb_to_oklab(rgb)
    out = np.zeros_like(lab)
    rep = []
    for c, r in enumerate(regions):
        m = lab_id == c
        mu = lab[m].mean(0) if m.any() else lab.reshape(-1, 3).mean(0)
        tgt = rgb_to_oklab(hex_rgb(r['hex']))
        dev = lab - mu
        sc = 1.0
        if r.get('l_std') and m.any() and lab[m][:, 0].std() > 1e-6:
            sc = float(np.clip(r['l_std'] / lab[m][:, 0].std(), *FIT_STD_RANGE))
            dev[..., 0] *= sc
        out += w[..., c:c + 1] * (tgt + dev)
        rep.append(dict(name=r.get('name', str(c)), share=round(float(m.mean()), 4), before=rgb_hex(oklab_to_rgb(mu)),
                        target=r['hex'], l_scale=round(sc, 3)))
    res = oklab_to_rgb(out)
    if not np.isfinite(res).all():
        raise SystemExit('NON_FINITE_COLOR_FIT')
    Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.round(np.clip(res, 0, 1) * 255).astype(np.uint8)).save(out_png)
    after, whole = region_report(load_rgb(out_png), k, blur_px)
    for r, a in zip(rep, after):
        r['after'] = a['mean_hex']
        r['after_share'] = a['share']
    tl = [rgb_to_oklab(hex_rgb(r['hex'])) for r in regions]
    for r, a, t in zip(rep, after, tl):
        r['delta_e_to_target'] = round(delta_e_ok(a['mean_lab'], t), 2) if a['mean_lab'] is not None else None
    return dict(regions=rep, out=str(out_png), out_sha256=sha(out_png))


# ---------------- geometry split (where a texel sits on the model) ----------------
SPLIT_SOFT_M = 0.03        # metres over which a texel's height band blends into the next band
SPLIT_DILATE = 32          # texel passes that spread the split into UV gutters (bilinear / mip bleed stays in its group)
SIDES = ((0, 1.0), (0, -1.0), (2, 1.0), (2, -1.0))   # +X, -X, +Z, -Z (Unity axes)
SPLIT_SIDE_SHARP = 25.0    # 1/m: a texel near the module's diagonal blends between the two sides over about 0.1 m


def uv_geometry_maps(npz, size):
    """rasterise uv_geometry.py output into texel maps: position P (size, size, 3) and face normal N (size, size, 3),
    Unity space, NaN where no triangle covers the texel. Image row 0 = v 1 (PNG top)."""
    d = np.load(npz)
    uv, pos, nrm = d['uv'].astype(np.float64), d['pos'].astype(np.float64), d['normal'].astype(np.float64)
    P = np.full((size, size, 3), np.nan)
    N = np.full((size, size, 3), np.nan)
    for t in range(len(uv)):
        x = uv[t, :, 0] * size - 0.5
        y = (1 - uv[t, :, 1]) * size - 0.5
        x0, x1 = max(int(np.floor(x.min())), 0), min(int(np.ceil(x.max())), size - 1)
        y0, y1 = max(int(np.floor(y.min())), 0), min(int(np.ceil(y.max())), size - 1)
        den = (y[1] - y[2]) * (x[0] - x[2]) + (x[2] - x[1]) * (y[0] - y[2])
        if x1 < x0 or y1 < y0 or abs(den) < 1e-12:
            continue
        X, Y = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        a = ((y[1] - y[2]) * (X - x[2]) + (x[2] - x[1]) * (Y - y[2])) / den
        b = ((y[2] - y[0]) * (X - x[2]) + (x[0] - x[2]) * (Y - y[2])) / den
        c = 1 - a - b
        m = (a >= -1e-6) & (b >= -1e-6) & (c >= -1e-6)
        P[Y[m], X[m]] = a[m, None] * pos[t, 0] + b[m, None] * pos[t, 1] + c[m, None] * pos[t, 2]
        N[Y[m], X[m]] = nrm[t]
    return P, N


def _shift(a, dy, dx):
    """a moved by (dy, dx) texels along axes 0 and 1, zero-filled (np.roll would wrap UV islands across the image border)"""
    out = np.zeros_like(a)
    H, W = a.shape[:2]
    out[max(dy, 0):H + min(dy, 0), max(dx, 0):W + min(dx, 0)] = a[max(-dy, 0):H + min(-dy, 0), max(-dx, 0):W + min(-dx, 0)]
    return out


def _ramp(x, lo, hi):
    return np.clip((x - lo) / (hi - lo), 0, 1)


def geometry_split(P, N, n_regions, height, top_band):
    """texel groups from geometry for a terrain module (top face y = 0, bottom y = -height, cell centre at x = z = 0).
    Regions are height bands bottom -> top in the catalog order (dark -> bright for cliffs): the last region is the top band
    (y > -top_band: one group for the up-facing top face, one for the cap's sides); the other regions share the rest of the height evenly and are split
    by the side of the module the texel sits on (+X, -X, +Z, -Z, from its position, not its triangle normal, so the split is
    smooth across triangles). Each side's rows are fitted on their own, so a straight module's hidden back face does not
    pull its front face. Returns dict(weights (H, W, G) soft, summing to 1 on covered texels and spread SPLIT_DILATE texels
    into UV gutters, 0 elsewhere; region (G,) region index; names (G,); covered share). N (face normals) only separates the top face from the cap's sides."""
    if not 0 < top_band < height:
        raise SystemExit(f'BAD_SPLIT_BANDS:top_band {top_band} must be inside (0, height {height})')
    cov = ~np.isnan(P[..., 0])
    y = np.where(cov, P[..., 1], 0.0)
    xz = np.where(cov[..., None], P[..., [0, 2]], 0.0)
    near = np.stack([xz[..., 0], -xz[..., 0], xz[..., 1], -xz[..., 1]], -1)
    face = np.exp(SPLIT_SIDE_SHARP * (near - near.max(-1, keepdims=True)))
    face = face / face.sum(-1, keepdims=True)
    k_rest = n_regions - 1
    edges = [-height + i * (height - top_band) / max(k_rest, 1) for i in range(k_rest)] + [-top_band]
    band = []
    for r in range(n_regions):
        lo_w = 1.0 if r == 0 else _ramp(y, edges[r] - SPLIT_SOFT_M, edges[r] + SPLIT_SOFT_M)
        hi_w = 1.0 if r == n_regions - 1 else 1 - _ramp(y, edges[r + 1] - SPLIT_SOFT_M, edges[r + 1] + SPLIT_SOFT_M)
        band.append(lo_w * hi_w * np.ones_like(y))
    band = np.stack(band, -1)
    band = band / np.maximum(band.sum(-1, keepdims=True), 1e-9)
    up = _ramp(np.where(cov, N[..., 1], 0.0), 0.35, 0.65)    # the module's top face apart from the cap's sides
    groups, region, names = [band[..., n_regions - 1] * up, band[..., n_regions - 1] * (1 - up)], [n_regions - 1] * 2, ['top', 'top_side']
    for si, (ax, s) in enumerate(SIDES):
        for r in range(k_rest):
            groups.append(face[..., si] * band[..., r])
            region.append(r)
            names.append(f"{'+-'[s < 0]}{'XYZ'[ax]}:{r}")
    w = np.stack(groups, -1) * cov[..., None]
    have = cov.copy()
    for _ in range(SPLIT_DILATE):   # gutters take the mean weights of their covered 4-neighbours
        acc = np.zeros_like(w)
        cnt = np.zeros(have.shape)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            hs = _shift(have, dy, dx)
            acc += _shift(w, dy, dx) * hs[..., None]
            cnt += hs
        new = (~have) & (cnt > 0)
        w[new] = acc[new] / cnt[new][:, None]
        have = have | new
    return dict(weights=w, region=np.array(region), names=names, covered=round(float(cov.mean()), 4))


def _color_fit_split(src_png, out_png, regions, split):
    rgb = load_rgb(src_png)
    lab = rgb_to_oklab(rgb)
    w, reg = split['weights'], split['region']
    if w.shape[:2] != lab.shape[:2]:
        raise SystemExit(f'SPLIT_SIZE_MISMATCH:{w.shape[:2]} vs {lab.shape[:2]}')
    keep = np.clip(1 - w.sum(-1), 0, 1)
    out = lab * keep[..., None]
    tl = [rgb_to_oklab(hex_rgb(r['hex'])) for r in regions]
    groups = []
    for g in range(w.shape[-1]):
        wg = w[..., g]
        sw = wg.sum()
        if sw < 1.0:     # under one texel of weight: nothing to fit, the texels keep their colour
            out += wg[..., None] * lab
            continue
        mu = (lab * wg[..., None]).reshape(-1, 3).sum(0) / sw
        dev = lab - mu
        r = regions[reg[g]]
        sc = 1.0
        if r.get('l_std'):
            sd = float(np.sqrt((wg * (lab[..., 0] - mu[0]) ** 2).sum() / sw))
            if sd > 1e-6:
                sc = float(np.clip(r['l_std'] / sd, *FIT_STD_RANGE))
                dev[..., 0] *= sc
        out += wg[..., None] * (tl[reg[g]] + dev)
        groups.append(dict(group=split['names'][g], region=r.get('name'), weight=round(float(sw / w[..., 0].size), 4),
                           before=rgb_hex(oklab_to_rgb(mu)), l_scale=round(sc, 3)))
    res = oklab_to_rgb(out)
    if not np.isfinite(res).all():
        raise SystemExit('NON_FINITE_COLOR_FIT')
    Path(out_png).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.round(np.clip(res, 0, 1) * 255).astype(np.uint8)).save(out_png)
    after = rgb_to_oklab(load_rgb(out_png))
    for gr, g in zip(groups, [i for i in range(w.shape[-1]) if w[..., i].sum() >= 1.0]):
        wg = w[..., g]
        a = (after * wg[..., None]).reshape(-1, 3).sum(0) / wg.sum()
        gr['after'] = rgb_hex(oklab_to_rgb(a))
        gr['delta_e_to_target'] = round(delta_e_ok(a, tl[reg[g]]), 2)
    rep = []
    for i, r in enumerate(regions):
        wr = w[..., reg == i].sum(-1)
        if wr.sum() < 1.0:      # no texel sits in this band: nothing was fitted, do not report black
            rep.append(dict(name=r.get('name', str(i)), share=0.0, empty=True, target=r['hex'], before=None, after=None,
                            delta_e_to_target=None, group_delta_e_max=None))
            continue
        mu_b = (lab * wr[..., None]).reshape(-1, 3).sum(0) / wr.sum()
        mu_a = (after * wr[..., None]).reshape(-1, 3).sum(0) / wr.sum()
        gde = [g['delta_e_to_target'] for g in groups if g['region'] == r.get('name')]
        rep.append(dict(name=r.get('name', str(i)), share=round(float(wr.sum() / wr.size), 4), before=rgb_hex(oklab_to_rgb(mu_b)),
                        target=r['hex'], after=rgb_hex(oklab_to_rgb(mu_a)), delta_e_to_target=round(delta_e_ok(mu_a, tl[i]), 2),
                        group_delta_e_max=max(gde) if gde else None))
    return dict(split='height', covered=split['covered'], regions=rep, groups=groups, out=str(out_png), out_sha256=sha(out_png))


SMOOTH_MISS_WARN = 0.02    # region mean further than this from its target -> warning in the PREP line


def _fit_clipped(dev, wg, sw, t):
    """clip(t + dev + o, 0, 1) with the offset o chosen so its weighted mean is t (the mean rises monotonically with o, so
    bisection finds it). Without this, values clipped at 0 or 1 pull the region mean off its target."""
    lo, hi = -1.0, 1.0
    for _ in range(40):
        mid = (lo + hi) / 2
        if (wg * np.clip(t + dev + mid, 0, 1)).sum() / sw < t:
            lo = mid
        else:
            hi = mid
    return np.clip(t + dev + (lo + hi) / 2, 0, 1)


def smoothness_fit(sm_png, regions, weights, region_of_group):
    """move a smoothness map (RGBA, A = smoothness) onto the art standard's per-region values (regions[i]['smoothness'],
    art standard 3.5): each group's mean smoothness moves to its region's value, the variation inside the group is kept,
    texels outside every group keep theirs. weights (H, W, G) from geometry_split or colour regions (fit_regions).
    Rewrites the PNG in place; returns before/after means per region and for the whole map."""
    im = np.asarray(Image.open(sm_png).convert('RGBA')).copy()
    s = im[..., 3].astype(np.float64) / 255
    if weights.shape[:2] != s.shape:
        raise SystemExit(f'SPLIT_SIZE_MISMATCH:{weights.shape[:2]} vs {s.shape}')
    keep = np.clip(1 - weights.sum(-1), 0, 1)
    out = s * keep
    for g in range(weights.shape[-1]):
        wg = weights[..., g]
        sw = wg.sum()
        if sw < 1.0:
            out += wg * s
            continue
        out += wg * _fit_clipped(s - (wg * s).sum() / sw, wg, sw, float(regions[region_of_group[g]]['smoothness']))
    out = np.clip(out, 0, 1)
    im[..., 3] = np.round(out * 255).astype(np.uint8)
    Image.fromarray(im).save(sm_png)
    s2 = im[..., 3] / 255
    rep = []
    for i, r in enumerate(regions):
        wr = weights[..., np.asarray(region_of_group) == i].sum(-1)
        sw = wr.sum()
        if sw < 1.0:
            rep.append(dict(name=r.get('name', str(i)), target=r['smoothness'], empty=True, before=None, after=None))
            continue
        after = float((wr * s2).sum() / sw)
        edge = float((wr * ((s2 <= 0.5 / 255) | (s2 >= 254.5 / 255))).sum() / sw)
        rep.append(dict(name=r.get('name', str(i)), target=r['smoothness'], before=round(float((wr * s).sum() / sw), 3),
                        after=round(after, 3), clipped_share=round(edge, 3), miss=round(after - float(r['smoothness']), 3)))
    misses = [x['name'] for x in rep if not x.get('empty') and abs(x['miss']) > SMOOTH_MISS_WARN]
    return dict(regions=rep, warn=('SMOOTHNESS_TARGET_MISSED:' + ','.join(misses)) if misses else None, mean_before=round(float(s.mean()), 3), mean_after=round(float(s2.mean()), 3), out_sha256=sha(sm_png))


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
    # smoothness: glTF roughness (G) -> A = 1 - roughness, R = 0; passthrough keeps the alpha through the resize
    orm = np.zeros((32, 32, 3), np.uint8); orm[..., 0] = 255; orm[..., 2] = 200; orm[:, :16, 1] = 204; orm[:, 16:, 1] = 51
    Image.fromarray(orm).save(d / 'orm.png')
    st = smoothness_from_orm(d / 'orm.png', d / 'sm.png')
    sm = np.asarray(Image.open(d / 'sm.png'))
    assert sm.shape == (32, 32, 4) and sm[0, 0, 3] == 51 and sm[0, 31, 3] == 204 and not sm[..., :3].any(), st
    passthrough(d / 'sm.png', 8, d / 'sm8.png', 'smoothness')
    sm8 = np.asarray(Image.open(d / 'sm8.png'))
    assert sm8.shape == (8, 8, 4) and sm8[0, 0, 3] == 51 and sm8[0, 7, 3] == 204, 'smoothness alpha lost in resize'
    try:
        passthrough(d / 'a.png' if (d / 'a.png').exists() else d / 'n.png', 8, d / 'x.png', 'smoothness'); raise AssertionError('RGB smoothness accepted')
    except SystemExit as e:
        assert 'SMOOTHNESS_MAP_NEEDS_ALPHA' in str(e)
    a = np.zeros((32, 32, 3), np.uint8); a[:, :16] = (215, 155, 104); a[:, 16:] = (40, 40, 40); a[5, 5] = (40, 40, 40)
    Image.fromarray(a).save(d / 'a.png')
    rec = build_char_palette({'a': d / 'a.png'}, {'bruno': '#d79b68'}, d / 'p.png', d / 'p.json', 1, per_texture=500)
    assert rec['cells'][0]['hex'] == '#d79b68' and len(rec['cells']) == 32, rec['cells'][0]
    q = quantize(d / 'a.png', d / 'p.json', 32, d / 'q.png')
    out = np.asarray(Image.open(d / 'q.png'))
    assert tuple(out[5, 5]) == (215, 155, 104), 'speck not removed'
    assert identity_share(d / 'q.png', d / 'p.json', 0) > 0.45
    assert q['oklab_error']['median'] < 0.02, q
    # colour fit: two patterned blocks (light + dark, cracks) land on the target colours; the pattern (a crack) survives
    rng = np.random.RandomState(3)
    cf = np.zeros((128, 128, 3)); cf[:64] = hex_rgb('#e8c890'); cf[64:] = hex_rgb('#a05030')
    cf += rng.normal(0, 0.01, cf.shape); cf[100:102, :] *= 0.6   # a dark crack in the lower block
    Image.fromarray(np.round(np.clip(cf, 0, 1) * 255).astype(np.uint8)).save(d / 'cf.png')
    regs = [dict(name='dark', hex='#4c3018'), dict(name='bright', hex='#cdb880')]
    fr = color_fit(d / 'cf.png', d / 'cf_out.png', regs)
    assert all(r['delta_e_to_target'] < 3 for r in fr['regions']), fr
    o = load_rgb(d / 'cf_out.png')
    assert o[10:50, 10:50].mean() > o[70:95, 10:50].mean() + 0.2, 'regions swapped'
    assert o[100:102, 10:50].mean() < o[70:95, 10:50].mean() - 0.02, 'crack lost'
    # a second texture with a different look lands within 3 dE_ok of the first (the point of the tool)
    cf2 = np.zeros((128, 128, 3)); cf2[:40] = hex_rgb('#f4e0b0'); cf2[40:] = hex_rgb('#7a4020')
    Image.fromarray(np.round(cf2 * 255).astype(np.uint8)).save(d / 'cf2.png')
    color_fit(d / 'cf2.png', d / 'cf2_out.png', regs)
    r1, _ = region_report(load_rgb(d / 'cf_out.png'), 2); r2, _ = region_report(load_rgb(d / 'cf2_out.png'), 2)
    assert all(delta_e_ok(a['mean_lab'], b['mean_lab']) < 3 for a, b in zip(r1, r2)), (r1, r2)
    # l_std shrinks a wide lightness spread
    wide = np.zeros((128, 128, 3)); wide[:] = hex_rgb('#808080'); wide[::2] = hex_rgb('#a0a0a0'); wide[1::2] = hex_rgb('#606060')
    Image.fromarray(np.round(wide * 255).astype(np.uint8)).save(d / 'wide.png')
    ws = color_fit(d / 'wide.png', d / 'wide_out.png', [dict(name='a', hex='#808080', l_std=0.02)])
    assert ws['regions'][0]['l_scale'] == 0.5, ws
    # geometry split: a module of two quads - top face (uv left half, y 0, up) and front face (uv right half, +Z, y -1.3..0).
    # The texture paints both one dark brown with a crack; a colour split cannot find rows, the height split must.
    def q(u0, u1, p00, p10, p11, p01):
        return [[(u0, 0), (u1, 0), (u1, 1)], [(u0, 0), (u1, 1), (u0, 1)]], [[p00, p10, p11], [p00, p11, p01]]
    tu, tp = q(0, 0.5, (-0.5, 0, 0.5), (0.5, 0, 0.5), (0.5, 0, -0.5), (-0.5, 0, -0.5))
    fu, fp = q(0.5, 1.0, (-0.5, -1.3, 0.5), (0.5, -1.3, 0.5), (0.5, 0, 0.5), (-0.5, 0, 0.5))
    np.savez(d / 'g.npz', uv=np.array(tu + fu, np.float32), pos=np.array(tp + fp, np.float32),
             normal=np.array([(0, 1, 0)] * 2 + [(0, 0, 1)] * 2, np.float32))
    P, N = uv_geometry_maps(d / 'g.npz', 64)
    assert not np.isnan(P[..., 1]).any() and abs(P[0, 40, 1]) < 0.03 and abs(P[63, 40, 1] + 1.3) < 0.03, 'uv raster: v up = image top'
    sp = geometry_split(P, N, 3, 1.3, 0.35)
    assert np.allclose(sp['weights'].sum(-1), 1, atol=1e-6), 'split weights must sum to 1 on the model'
    g = np.zeros((64, 64, 3)); g[:] = hex_rgb('#5a3a1c'); g[:, 40:42] *= 0.6
    Image.fromarray(np.round(g * 255).astype(np.uint8)).save(d / 'g.png')
    cliff = [dict(name='dark', hex='#3e2812', smoothness=0.3), dict(name='light', hex='#9a6a34', smoothness=0.3),
             dict(name='cap', hex='#cdb880', smoothness=0.35)]
    gr = color_fit(d / 'g.png', d / 'g_out.png', cliff, split=sp)
    assert all(r['delta_e_to_target'] < 2 for r in gr['regions']), gr['regions']
    o = rgb_to_oklab(load_rgb(d / 'g_out.png'))
    assert delta_e_ok(o[10:50, 5:25].reshape(-1, 3).mean(0), rgb_to_oklab(hex_rgb('#cdb880'))) < 2, 'top face not cap colour'
    assert o[22, 50, 0] > o[55, 50, 0] + 0.15, 'front face: upper row (light) must be brighter than lower row (dark)'
    assert o[55, 41, 0] < o[55, 50, 0] - 0.01, 'crack lost'
    sm = np.zeros((64, 64, 4), np.uint8); sm[..., 3] = 20; sm[::2, :, 3] = 40
    Image.fromarray(sm).save(d / 'sm.png')
    sf = smoothness_fit(d / 'sm.png', cliff, sp['weights'], sp['region'])
    assert all(abs(r['after'] - r['target']) < 0.02 for r in sf['regions']), sf
    s2 = np.asarray(Image.open(d / 'sm.png'))[..., 3].astype(int)
    assert (s2[::2] - s2[1::2]).mean() > 15, 'smoothness variation lost'
    return 'texture selftest PASS (palette + passthrough + colour fit + geometry split + smoothness fit)'


if __name__ == '__main__':
    print(selftest())
