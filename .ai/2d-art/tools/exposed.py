#!/usr/bin/env python3
"""Exposed fill: silhouette edge that has colour but no outline (2d-art kit judgement tool).

When a figure is split into parts and moved (rigging, cut-out), a part's hidden end - filled, never outlined - slides out
past the silhouette. The picture has no transparent gap there, so `frames.py holes` passes it (source case: a rig the user rejected
as 'cut up and awkward' had passed holes). This tool measures the silhouette edge (alpha >= 128) and asks: is there a dark outline pixel (luma < luma_max)
within `reach` pixels of each edge pixel?

  exposed.py check <frame>... [--ref <picture>] [--inner] [--out <png|dir>] [--json <file>]
                   [--luma-max N] [--band PX] [--key-bg [TOL]] [--allow-ratio F] [--allow-run F] [--allow-run-px N]
  exposed.py selftest

Per frame:
  edge      silhouette edge pixels (solid with a transparent 4-neighbour; inner holes count too)
  exposed   edge pixels with no dark pixel within reach (chessboard). ratio = exposed / edge
  run       the longest connected exposed stretch: pixels, % of the figure height, and its bbox (where to look)
  inner     (shown always, judged only with --inner) colour seams inside the figure with no outline: two pixels
            `step` apart, both farther than reach from any dark pixel and from the silhouette edge, colour dE76 >
            --inner-de. In a style with inner dividing lines (bold-outline casual, 1px-outline pixel art) an unlined seam is a part edge laid
            over another part. Shadow steps (one shade, dE about 15-21 in the specs) stay under the default 25; anything
            else drawn without a line (a gradient, a pattern) shows up here - that is why it is off by default.
Judgement (with --ref the reference's own values are the floor: a picture may have some unlined edge by design):
  FAIL if ratio - ref.ratio > allow_ratio, or run - ref.run > max(allow_run x height, allow_run_px)  (and, with --inner,
  the same rule on the inner run). Without --ref the floor is 0.
Defaults are STARTING VALUES, set from the selftest and the first real pictures, not from accepted/rejected sets:
  allow_ratio 0.02, allow_run 0.03 (of figure height), allow_run_px 1, inner_de 25.
Scale ("height"): --height, else the reference's figure height when --ref is on the same canvas (a crouching frame is
not a smaller character), else the frame's own figure height. Run % and the band are taken of it.
Outline width (band): --band, else the art spec's outline row ('키의 약 1.2%' -> 1.2% of the height, '1픽셀' -> 1),
else the measured stroke width of the reference. reach = 0 for a 1px band (the edge pixel itself must be dark), else
ceil(band/2). luma_max: --luma-max, else the approved spec's 2d-measure block (the kit config, or KIT2D_CONFIG=<path>).
--key-bg: the picture has an opaque flat background (a direction sheet): flood it away from the border first (TOL = sum
of channel differences, default 40).
Exit: 0 PASS / 1 FAIL / 2 input or config problem. Kit-owned (overwritten on update). Needs numpy and Pillow."""
import argparse
import json
import math
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

import re
import warnings

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402
import frames as KF  # noqa: E402

# numpy + Apple Accelerate report spurious divide/overflow warnings from float32 matmul in kit2d.rgb_to_lab
warnings.filterwarnings('ignore', message='.*encountered in matmul', category=RuntimeWarning)


# ---------- config: outline brightness and outline width from the art spec ----------

def try_kit():
    """the kit config (KIT2D_CONFIG or found upward) or None"""
    try:
        return K.load_kit()
    except K.KitError:
        return None


def outline_luma(kit, override=None):
    """(luma_max, source). --luma-max wins; else the approved spec's 2d-measure outline.luma_max."""
    if override is not None:
        return float(override), '--luma-max'
    if kit is None:
        raise K.KitError('NO_LUMA', 'no kit config: run /2d-art-setup, set KIT2D_CONFIG=<config path>, or pass --luma-max')
    _, out, spec = K.approved_tolerance(kit)
    return out['luma_max'], f'{spec.name} 2d-measure outline.luma_max'


K2_ROW = re.compile(r'^\|\s*K\d+\s*\|\s*외곽선\s*\|.*$', re.M)


def spec_outline_width(kit):
    """outline width from the spec's '외곽선' decision row: ('rel', 0.012) for '키의 약 1.2%', ('px', 1) for '1픽셀',
    or None. Read from the approved spec text (the 2d-measure block has no width)."""
    if kit is None:
        return None
    try:
        spec, _ = K.spec_paths(kit)
        text = spec.read_text(encoding='utf-8')
    except (OSError, K.KitError):
        return None
    m = K2_ROW.search(text)
    if not m:
        return None
    row = m[0]
    r = re.search(r'키의\s*약\s*([\d.]+)\s*%', row)
    if r:
        return ('rel', float(r[1]) / 100)
    r = re.search(r'(\d+)\s*픽셀', row)
    if r:
        return ('px', int(r[1]))
    return None


# ---------- pictures ----------

def key_background(img, tol=40):
    """opaque picture on a flat background -> background made transparent: flood from the border through pixels whose
    colour is within tol (sum of |channel| differences) of the most common border colour. Returns a copy."""
    out = img.copy()
    rgb = out[..., :3]
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]]).astype(int)
    vals, counts = np.unique(border // 4, axis=0, return_counts=True)
    bg = border[(border // 4 == vals[np.argmax(counts)]).all(axis=1)].mean(axis=0)
    near = np.abs(rgb - bg).sum(axis=-1) <= tol
    seed = np.zeros_like(near)
    seed[0, :] = seed[-1, :] = seed[:, 0] = seed[:, -1] = True
    reach = KF._flood(seed, near)
    out[reach, 3] = 0
    return out


def load(path, key_bg=None):
    img = K.load_rgba(path)
    if key_bg is not None:
        img = key_background(img, key_bg)
    if (img[..., 3] >= K.ALPHA_SOLID).all():
        raise K.KitError('NO_ALPHA', f'{path}: every pixel is opaque (no transparent background) -> pass --key-bg')
    return img


def box_dilate(mask, r):
    """chessboard dilation by r (square (2r+1) window), separable"""
    m = mask.copy()
    for axis in (0, 1):
        acc = m.copy()
        for k in range(1, int(r) + 1):
            if axis == 0:
                acc[k:] |= m[:-k]
                acc[:-k] |= m[k:]
            else:
                acc[:, k:] |= m[:, :-k]
                acc[:, :-k] |= m[:, k:]
        m = acc
    return m


def edge_pixels(solid):
    """solid pixels with a 4-neighbour that is not solid (canvas border counts as outside)"""
    p = np.pad(solid, 1, constant_values=False)
    inner = p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
    return solid & ~inner


def components(mask, link=1):
    """8-connected components of mask, pixels within chessboard distance `link` joined. [(size, (x0,y0,x1,y1))] by size"""
    ys, xs = np.nonzero(mask)
    if not len(xs):
        return []
    idx = {(int(y), int(x)): i for i, (y, x) in enumerate(zip(ys, xs))}
    seen = np.zeros(len(xs), dtype=bool)
    out = []
    offs = [(dy, dx) for dy in range(-link, link + 1) for dx in range(-link, link + 1) if dy or dx]
    for i in range(len(xs)):
        if seen[i]:
            continue
        seen[i] = True
        stack, n = [i], 0
        x0 = x1 = int(xs[i])
        y0 = y1 = int(ys[i])
        while stack:
            j = stack.pop()
            n += 1
            y, x = int(ys[j]), int(xs[j])
            x0, x1, y0, y1 = min(x0, x), max(x1, x), min(y0, y), max(y1, y)
            for dy, dx in offs:
                k = idx.get((y + dy, x + dx))
                if k is not None and not seen[k]:
                    seen[k] = True
                    stack.append(k)
        out.append((n, (x0, y0, x1 + 1, y1 + 1)))
    out.sort(key=lambda c: -c[0])
    return out


def figure_box(solid):
    b = K.bbox(solid)
    if not b:
        raise K.KitError('EMPTY_IMAGE', 'no solid pixel')
    return b


DEFAULTS = {'allow_ratio': 0.02, 'allow_run': 0.03, 'allow_run_px': 1, 'inner_de': 25.0}


def resolve_band(width_spec, height, ref_img=None, luma_max=60):
    """(band px, source)"""
    if width_spec and width_spec[0] == 'rel':
        return max(1, round(width_spec[1] * height)), f'spec {width_spec[1]:.2%} of height {height}px'
    if width_spec and width_spec[0] == 'px':
        return int(width_spec[1]), f'spec {width_spec[1]}px'
    if ref_img is not None:
        solid = ref_img[..., 3] >= K.ALPHA_SOLID
        sw = K.stroke_width(solid & (K.luma(ref_img[..., :3]) < luma_max))
        if sw:
            return max(1, round(sw)), f'measured stroke {sw:.1f}px'
    return 1, 'fallback 1px'


def measure(img, luma_max, band, inner_de=DEFAULTS['inner_de'], height=None):
    """height: the figure's standing height (the scale for band and run %); default this picture's figure height"""
    solid = img[..., 3] >= K.ALPHA_SOLID
    x0, y0, x1, y1 = figure_box(solid)
    height = height or (y1 - y0)
    reach = 0 if band <= 1 else math.ceil(band / 2)
    dark = solid & (K.luma(img[..., :3]) < luma_max)
    edge = edge_pixels(solid)
    exposed = edge & ~box_dilate(dark, reach)
    comps = components(exposed)
    run, run_box = (comps[0] if comps else (0, None))
    # inner seams
    step = 1 if band <= 2 else max(2, band // 3)
    clean = solid & ~box_dilate(dark, max(reach, 1)) & ~box_dilate(~solid, max(reach, 1))
    lab = K.rgb_to_lab(img[..., :3])
    seam = np.zeros_like(solid)
    for axis in (0, 1):
        a = [slice(None), slice(None)]
        b = [slice(None), slice(None)]
        a[axis], b[axis] = slice(0, -step), slice(step, None)
        a, b = tuple(a), tuple(b)
        both = clean[a] & clean[b]
        far = np.linalg.norm(lab[a] - lab[b], axis=-1) > inner_de
        seam[a] |= both & far
    icomps = components(seam, link=max(1, step))
    irun, irun_box = (icomps[0] if icomps else (0, None))
    n_edge = int(edge.sum())
    return {'height': int(height), 'band': int(band), 'reach': int(reach), 'edge_px': n_edge,
            'exposed_px': int(exposed.sum()), 'ratio': float(exposed.sum()) / max(1, n_edge),
            'run_px': int(run), 'run_pct': float(run) / max(1, height), 'run_box': run_box,
            'runs': len(comps), 'inner_px': int(seam.sum()), 'inner_run_px': int(irun),
            'inner_run_pct': float(irun) / max(1, height), 'inner_box': irun_box,
            '_exposed': exposed, '_seam': seam}


def verdict(st, base, allow, inner=False):
    fails = []
    lim_px = max(allow['allow_run'] * st['height'], allow['allow_run_px'])
    d_ratio = st['ratio'] - base['ratio']
    # the floor is the reference's run as a share of its height, applied to this frame's height (scale-free)
    d_run = st['run_px'] - base['run_pct'] * st['height']
    if d_ratio > allow['allow_ratio']:
        fails.append(f'ratio +{d_ratio:.3f} > {allow["allow_ratio"]}')
    if d_run > lim_px:
        fails.append(f'run +{d_run:.0f}px > {lim_px:.0f}px')
    d_inner = st['inner_run_px'] - base['inner_run_pct'] * st['height']
    if inner and d_inner > lim_px:
        fails.append(f'inner run +{d_inner:.0f}px > {lim_px:.0f}px')
    return fails, d_ratio, d_run, d_inner, lim_px


def overlay(img, st, path):
    h, w = img.shape[:2]
    bg = np.full((h, w, 3), 235, dtype=np.float32)
    yy, xx = np.mgrid[0:h, 0:w]
    cell = max(4, h // 64)
    bg[((yy // cell + xx // cell) % 2) == 0] = 215
    a = img[..., 3:4] / 255.0
    rgb = img[..., :3] * a + bg * (1 - a)
    rgb = rgb * 0.55 + 255 * 0.45                     # fade the picture so the marks read
    grow = max(0, st['height'] // 160)
    rgb[box_dilate(st['_seam'], grow)] = (40, 90, 255)
    rgb[box_dilate(st['_exposed'], grow)] = (255, 0, 0)
    im = Image.fromarray(rgb.clip(0, 255).astype(np.uint8))
    scale = 8 if h <= 96 else 1
    if scale > 1:
        im = im.resize((w * scale, h * scale), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for box, col in ((st['run_box'], (255, 200, 0)), (st['inner_box'], (0, 200, 255))):
        if box:
            pad = 3
            d.rectangle([box[0] * scale - pad, box[1] * scale - pad, box[2] * scale + pad, box[3] * scale + pad], outline=col, width=2)
    d.text((6, 6), f"exposed {st['ratio']:.3f}  run {st['run_px']}px ({st['run_pct']:.1%})  inner run {st['inner_run_px']}px",
           fill=(0, 0, 0), font=K.font(max(12, min(22, w * scale // 40))))
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    im.save(path)


def check(frames, ref=None, inner=False, out=None, json_out=None, luma_max=None, band=None, key_bg=None,
          allow=None, inner_de=DEFAULTS['inner_de'], quiet=False, height=None):
    allow = dict(DEFAULTS, **(allow or {}))
    kit = try_kit()
    luma_max, luma_src = outline_luma(kit, luma_max)
    width_spec = ('px', band) if band else spec_outline_width(kit)
    ref_img = load(ref, key_bg) if ref else None
    rows, bad = [], 0
    base = None
    if ref_img is not None:
        rh = figure_box(ref_img[..., 3] >= K.ALPHA_SOLID)
        b, bsrc = resolve_band(width_spec, rh[3] - rh[1], ref_img, luma_max)
        base = measure(ref_img, luma_max, b, inner_de, rh[3] - rh[1])
        if not quiet:
            print(f"reference {Path(ref).name}: exposed {base['ratio']:.3f}, run {base['run_px']}px ({base['run_pct']:.1%}), "
                  f"inner run {base['inner_run_px']}px ({base['inner_run_pct']:.1%}), band {b}px ({bsrc})")
    zero = {'ratio': 0.0, 'run_pct': 0.0, 'inner_run_pct': 0.0}
    if not quiet:
        print(f"luma_max {luma_max:g} ({luma_src}); allow ratio +{allow['allow_ratio']}, run +max({allow['allow_run']:.0%} of height, "
              f"{allow['allow_run_px']}px){', inner judged' if inner else ''}  [starting values]")
    for p in frames:
        img = load(p, key_bg)
        fb = figure_box(img[..., 3] >= K.ALPHA_SOLID)
        # scale: --height, else the reference's standing height when it is on the same canvas (a crouch is not a
        # smaller character), else this frame's figure height
        hh = height or (rh[3] - rh[1] if ref_img is not None and ref_img.shape == img.shape else fb[3] - fb[1])
        b, bsrc = resolve_band(width_spec, hh, ref_img if ref_img is not None else img, luma_max)
        st = measure(img, luma_max, b, inner_de, hh)
        fails, d_ratio, d_run, d_inner, lim = verdict(st, base or zero, allow, inner)
        bad += bool(fails)
        row = {k: v for k, v in st.items() if not k.startswith('_')}
        row.update(image=str(Path(p).resolve()), sha256=K.sha256(p), band_source=bsrc, verdict='FAIL' if fails else 'PASS',
                   fails=fails, d_ratio=round(d_ratio, 4), d_run_px=round(d_run, 1), d_inner_run_px=round(d_inner, 1), limit_run_px=round(lim, 1))
        rows.append(row)
        if out:
            o = Path(out)
            target = o if (o.suffix.lower() == '.png' and len(frames) == 1) else o / f'{Path(p).stem}.exposed.png'
            overlay(img, st, target)
            row['overlay'] = str(target)
        if not quiet:
            where = f" at {st['run_box']}" if st['run_box'] else ''
            print(f"{row['verdict']} {Path(p).name}: exposed {st['ratio']:.3f} ({st['exposed_px']}/{st['edge_px']} edge px, "
                  f"{d_ratio:+.3f}), run {st['run_px']}px = {st['run_pct']:.1%} of {st['height']}px ({d_run:+.0f}px){where}; "
                  f"inner run {st['inner_run_px']}px ({d_inner:+.0f}px{'' if inner else ', INFO'}); band {b}px reach {st['reach']}"
                  + (f"  <- {'; '.join(fails)}" if fails else ''))
    if not quiet:
        print(f"{'FAIL' if bad else 'PASS'} {len(frames)} frames, {bad} with exposed fill beyond the allowance")
    if json_out:
        rec = {'schema': '2d-exposed/1', 'tool': 'exposed.py', 'luma_max': luma_max, 'luma_source': luma_src,
               'ref': str(Path(ref).resolve()) if ref else None,
               'ref_values': {k: v for k, v in base.items() if not k.startswith('_')} if base else None,
               'allow': allow, 'inner_judged': inner, 'inner_de': inner_de, 'frames': rows,
               'verdict': 'FAIL' if bad else 'PASS'}
        Path(json_out).parent.mkdir(parents=True, exist_ok=True)
        Path(json_out).write_text(json.dumps(rec, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    return 1 if bad else 0


# ---------- selftest ----------

DARK = (48, 12, 0, 255)


def _ring(mask, w):
    inner = mask.copy()
    for _ in range(w):
        inner = K.erode(inner)
    return mask & ~inner


def _paint(canvas, mask, fill, w, unlined=None):
    """fill the mask; outline it w px except where `unlined` (the part's hidden end) is"""
    canvas[mask] = fill
    ring = _ring(mask, w)
    if unlined is not None:
        ring &= ~unlined
    canvas[ring] = DARK


def _rect(shape, x0, y0, x1, y1):
    m = np.zeros(shape, dtype=bool)
    m[max(0, y0):y1, max(0, x0):x1] = True
    return m


def _figure(arm_dx=0, arm_on_top=False, arm_closed=False, ground=False, size=240, w=4):
    """casual-like figure: torso (cream) with an arm (orange) on its right side. The arm's left end is its hidden end:
    filled 30px into the torso, not outlined (as a rig part cut at the joint). arm_dx moves the arm right."""
    sh = (size, size)
    c = np.zeros((size, size, 4), dtype=np.uint8)
    torso = _rect(sh, 70, 40, 150, 200)
    arm_vis = _rect(sh, 150 + arm_dx, 90, 200 + arm_dx, 120)
    arm_hidden = _rect(sh, 120 + arm_dx, 90, 150 + arm_dx, 120)
    arm = arm_vis | arm_hidden
    unl = None if arm_closed else _rect(sh, 0, 0, 150 + arm_dx - w, size) & arm   # the hidden end has no outline
    if ground:
        c[200:216, 40:200] = (108, 156, 36, 255)                                   # flat strip, no outline (design)
    if arm_on_top:
        _paint(c, torso, (252, 228, 192, 255), w)
        _paint(c, arm, (240, 120, 24, 255), w, unl)
    else:
        _paint(c, arm, (240, 120, 24, 255), w, unl)
        _paint(c, torso, (252, 228, 192, 255), w)
    return Image.fromarray(c)


def _pixel(leg_dy=0, leg_closed=False):
    """48x48 pixel sprite, 1px outline: body block on one leg; the leg's top 3 rows are its hidden end (under the body,
    no outline). leg_dy moves the leg down."""
    c = np.zeros((48, 48, 4), dtype=np.uint8)
    sh = (48, 48)
    body = _rect(sh, 14, 6, 34, 30)
    leg = _rect(sh, 20, 27 + leg_dy, 26, 42 + leg_dy)
    unl = None if leg_closed else _rect(sh, 0, 0, 48, 30 + leg_dy) & leg
    _paint(c, leg, (122, 74, 42, 255), 1, unl)
    _paint(c, body, (240, 120, 24, 255), 1)
    return Image.fromarray(c)


def selftest():
    td = Path(tempfile.mkdtemp(prefix='exposed-selftest-'))
    cases = []

    def save(name, im):
        p = td / f'{name}.png'
        im.save(p)
        return p

    def run(frames, **kw):
        kw.setdefault('luma_max', 60)
        kw.setdefault('band', 4)
        return check([str(f) for f in frames], quiet=True, **kw)

    def stat(p, band=4):
        return measure(K.load_rgba(p), 60, band)

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    rest = save('rest', _figure())
    out10 = save('arm_out10', _figure(arm_dx=10))
    out14 = save('arm_out14', _figure(arm_dx=14))
    out40 = save('arm_out40', _figure(arm_dx=40))
    gap = save('gap', _figure(arm_dx=12, arm_closed=True))
    top = save('arm_on_top', _figure(arm_on_top=True))
    case('outlined figure (arm end hidden under the torso): PASS', run([rest]) == 0, f"ratio {stat(rest)['ratio']:.3f}")
    s40 = stat(out40)
    case('BAD: arm slid out 40px, its unlined end shows: FAIL', run([out40], ref=str(rest)) == 1,
         f"run {s40['run_px']}px at {s40['run_box']}")
    case('... found where the arm end is (run bbox right of the torso, x 146..190)', s40['run_box'] and 146 <= s40['run_box'][0] <= 160
         and s40['run_box'][2] <= 192, f"{s40['run_box']}")
    case('transparent gap only (closed arm moved away): PASS here (frames.py holes owns gaps)', run([gap], ref=str(rest)) == 0,
         f"ratio {stat(gap)['ratio']:.3f}")
    # near the threshold: allow_run 3% of a 160px figure = 4.8px. The arm's unlined end shows (dx - 4px arm outline) along
    # its top and bottom edges, less reach 2px at each end: dx 10 -> 2px run, dx 14 -> 6px run.
    s10, s14 = stat(out10), stat(out14)
    case('near threshold: arm out 10px (2px run) PASS at the default 3% (4.8px)', run([out10], ref=str(rest)) == 0, f"run {s10['run_px']}px")
    case('near threshold: arm out 14px (6px run) FAIL at the default', run([out14], ref=str(rest)) == 1, f"run {s14['run_px']}px")
    case('... the same 14px PASSes with allow_run 0.06 (9.6px)', run([out14], ref=str(rest), allow={'allow_run': 0.06}) == 0)
    # reference floor: a ground strip drawn without outline by design
    g_ref, g_frame = save('ground_ref', _figure(ground=True)), save('ground_frame', _figure(ground=True))
    case('BAD by absolute rule: ground strip without outline, no --ref: FAIL', run([g_frame]) == 1)
    case('the same picture with --ref holding the same strip: PASS (floor)', run([g_frame], ref=str(g_ref)) == 0)
    g_bad = save('ground_arm40', _figure(ground=True, arm_dx=40))
    case('BAD on top of the floor: ground strip + arm out 40px with --ref: FAIL', run([g_bad], ref=str(g_ref)) == 1)
    # inner seams
    st = stat(top)
    case('arm laid over the torso, its unlined end inside the figure: inner run found', st['inner_run_px'] >= 20, f"inner run {st['inner_run_px']}px")
    case('... default judgement (inner off): PASS', run([top], ref=str(rest)) == 0)
    case('BAD with --inner: FAIL', run([top], ref=str(rest), inner=True) == 1)
    shade = np.asarray(_figure()).copy()
    shade[130:190, 80:140] = (228, 192, 144, 255)                      # cream shadow (#e4c090), dE 15 from #fce4c0
    sh_p = save('shadow', Image.fromarray(shade))
    case('a one-step shadow inside a zone is not an inner seam (dE under 25): --inner PASS', run([sh_p], ref=str(rest), inner=True) == 0,
         f"inner run {stat(sh_p)['inner_run_px']}px")
    # pixel art, 1px outline, band 1 -> reach 0
    p0, p1, p3, pc = (save(f'px{n}', im) for n, im in (('0', _pixel()), ('1', _pixel(leg_dy=1)), ('3', _pixel(leg_dy=3)),
                                                         ('c', _pixel(leg_dy=3, leg_closed=True))))
    case('pixel sprite, 1px outline: PASS', run([p0], band=1) == 0, f"ratio {stat(p0, 1)['ratio']:.3f}")
    case('pixel BAD: leg down 3px shows 3 unlined rows each side: FAIL', run([p3], ref=str(p0), band=1) == 1,
         f"run {stat(p3, 1)['run_px']}px")
    case('pixel near threshold: leg down 1px (1px run each side) PASS at allow_run_px 1', run([p1], ref=str(p0), band=1) == 0,
         f"run {stat(p1, 1)['run_px']}px")
    case('pixel: closed leg moved 3px (outlined, a gap only): PASS', run([pc], ref=str(p0), band=1) == 0)
    # output files
    ov = td / 'ov.png'
    jf = td / 'rec.json'
    run([out40], ref=str(rest), out=str(ov), json_out=str(jf))
    rec = json.loads(jf.read_text())
    case('overlay png and json record written', ov.is_file() and rec['verdict'] == 'FAIL' and rec['frames'][0]['run_box'])
    # band from the spec row
    row_c = '| K2 | 외곽선 | 사용자·방법 | 색 #300c00 근처. 굵기는 키의 약 1.2%(520px 키에서 6px) |'
    row_d = '| K2 | 외곽선 | 사용자 | 바깥 윤곽은 어두운 갈색 1픽셀. 안쪽 구분선도 1픽셀 |'
    m1, m2 = K2_ROW.search(row_c), K2_ROW.search(row_d)
    case('spec outline row parsed: casual 1.2% / dot 1px', m1 and m2 and re.search(r'키의\s*약\s*([\d.]+)\s*%', m1[0])
         and re.search(r'(\d+)\s*픽셀', m2[0])[1] == '1')
    bad = cases.count(False)
    print(f"exposed selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed ({td})")
    return 1 if bad else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    c = sub.add_parser('check')
    c.add_argument('frames', nargs='+')
    c.add_argument('--ref')
    c.add_argument('--inner', action='store_true')
    c.add_argument('--out')
    c.add_argument('--json')
    c.add_argument('--luma-max', type=float)
    c.add_argument('--band', type=int)
    c.add_argument('--key-bg', type=int, nargs='?', const=40)
    c.add_argument('--allow-ratio', type=float, default=DEFAULTS['allow_ratio'])
    c.add_argument('--allow-run', type=float, default=DEFAULTS['allow_run'])
    c.add_argument('--allow-run-px', type=int, default=DEFAULTS['allow_run_px'])
    c.add_argument('--inner-de', type=float, default=DEFAULTS['inner_de'])
    c.add_argument('--height', type=int, help='standing height in px (scale for band and run %%)')
    sub.add_parser('selftest')
    a = ap.parse_args()
    if a.cmd == 'selftest':
        return selftest()
    try:
        return check(a.frames, a.ref, a.inner, a.out, a.json, a.luma_max, a.band, a.key_bg,
                     {'allow_ratio': a.allow_ratio, 'allow_run': a.allow_run, 'allow_run_px': a.allow_run_px}, a.inner_de,
                     height=a.height)
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
