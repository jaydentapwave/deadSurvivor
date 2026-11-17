#!/usr/bin/env python3
"""Shape templates: the angle, size and slots of a prop drawn from numbers, as a second generator input and as the thing
a generated prop is judged against (2d-art-produce "형태 규격 틀"). Style comes from the style sample, shape from here.

  shape.py template <spec.json> --out <dir> [--key #ff00ff] [--gen-size 1536x1024]
  shape.py fit <keyed png>... --shape <dir>/<id>.shape.json [--tol 0.05] [--iou-min 0]
  shape.py selftest

spec.json (cells; origin = the pivot, bottom centre; x right, d = depth into the screen, z = height):
  {"schema": "2d-shape/1", "unit_px": 512,
   "projection": {"depth": 0.77, "height": 0.64},        screen y = d*depth + z*height. A side view is {"depth": 0, "height": 1}
   "height_bar": {"label": "주인공 1H", "cells": 1.0},     optional: drawn on the people sheet only
   "shapes": {"<id>": {"name": "...", "footprint": [2.1, 1.0], "item_cells": 0.5,
     "parts": [{"type": "box",  "x": [-1.05, 1.05], "d": [0, 1], "z": [0, 0.35]},       front face + top face
               {"type": "slab", "x": [-1.05, 1.05], "d": [0, 1], "z": [0.35, 0.95]},    a sloped top from (d0, z0) to (d1, z1)
               {"type": "cyl",  "cx": 0, "r": 0.5, "d": 0.5, "z": [0, 0.45]},           open barrel / basket
               {"type": "rect", "x": [-0.1, 0.1], "y": [0, 0.6]},                       screen-space block (stem, post)
               {"type": "blob", "cx": 0, "y": [0.25, 1.0], "w": 1.03, "wavy": true}],   screen-space ellipse; wavy = leafy edge
     "slots": [{"x": -0.71, "d": 0.6, "z": 0.71}, {"x": 0.2, "y": 0.8}]}}}              where an item's bottom centre sits

template writes, per shape:
  gen_<id>.png        generator input: flat key background, grey faces and dark lines only. No slots, bar or text - the
                      generator copies whatever is in the picture (a smooth dome was drawn as an arch: use "wavy" for foliage).
  tmpl_<id>.png       the same shape for people and for the reader of the check: footprint, numbered slots with item
                      boxes, the height bar, a 0.1-cell ruler. Give this to a reviewer BEFORE asking about the angle.
  mask_<id>.png       silhouette at unit_px
  <id>.shape.json     width/height in cells and px, aspect, slots (cells and px from the pivot), the px size of the shape
                      on the generator canvas (write THAT number in the prompt; do not ask for "wide"), paths
fit judges a generated picture BEFORE it is scaled to the template (after scaling, "height = template" passes by itself):
  aspect   width/height of the opaque figure against the template's, within --tol (default 5%)
  iou      overlap with the template silhouette when the figure is set on it by height, bottom centre (shown; FAIL only
           below --iou-min, default off - thin shapes overlap poorly even when right)
Too wide: take words like "wide" out of the prompt and give the px width; do not pre-narrow the template (that over-corrects).
Inside a kit project fit writes work_dir/measure/<sha256>.shape.json. Needs no art spec or identity.
Exit: 0 PASS (template: written) / 1 FAIL / 2 input problem. Needs numpy and Pillow."""
import argparse
import json
import math
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402

SCHEMA, OUT_SCHEMA = '2d-shape/1', '2d-shape-out/1'
FACE = {'top': (222, 222, 222), 'front': (160, 160, 160), 'inner': (120, 120, 120)}
LINE = (70, 70, 70)
SLOT, FOOT, BAR = (230, 120, 30), (70, 140, 220), (205, 140, 90)
PAPER = (255, 249, 231)


def _pair(p, key, shape_id):
    v = p.get(key)
    if not (isinstance(v, list) and len(v) == 2 and all(isinstance(n, (int, float)) for n in v)):
        raise K.KitError('BAD_SHAPE', f'{shape_id}: part {p.get("type")} needs "{key}": [a, b]')
    return float(v[0]), float(v[1])


def _num(p, key, shape_id):
    v = p.get(key)
    if not isinstance(v, (int, float)):
        raise K.KitError('BAD_SHAPE', f'{shape_id}: part {p.get("type")} needs a number "{key}"')
    return float(v)


def resolve(shape_id, shape, kd, kh):
    """parts in screen cells, back to front: ('rect', x0, y0, x1, y1, face) / ('cyl', cx, r, y_base, y_top, ry) /
    ('blob', cx, y0, y1, w, wavy)"""
    def sy(d, z):
        return d * kd + z * kh
    out = []
    parts = shape.get('parts')
    if not isinstance(parts, list) or not parts:
        raise K.KitError('BAD_SHAPE', f'{shape_id}: "parts" is empty')
    for p in parts:
        t = p.get('type')
        if t == 'box':
            (x0, x1), (d0, d1), (z0, z1) = (_pair(p, k, shape_id) for k in 'xdz')
            out.append(('rect', x0, sy(d0, z0), x1, sy(d0, z1), 'front'))
            if kd > 0 and d1 > d0:
                out.append(('rect', x0, sy(d0, z1), x1, sy(d1, z1), 'top'))
        elif t == 'slab':
            (x0, x1), (d0, d1), (z0, z1) = (_pair(p, k, shape_id) for k in 'xdz')
            out.append(('rect', x0, sy(d0, z0), x1, sy(d1, z1), 'top'))
        elif t == 'cyl':
            z0, z1 = _pair(p, 'z', shape_id)
            r, d = _num(p, 'r', shape_id), _num(p, 'd', shape_id)
            out.append(('cyl', _num(p, 'cx', shape_id), r, sy(d, z0), sy(d, z1), r * kd))
        elif t == 'rect':
            (x0, x1), (y0, y1) = _pair(p, 'x', shape_id), _pair(p, 'y', shape_id)
            out.append(('rect', x0, y0, x1, y1, p.get('face', 'front')))
        elif t == 'blob':
            y0, y1 = _pair(p, 'y', shape_id)
            out.append(('blob', _num(p, 'cx', shape_id), y0, y1, _num(p, 'w', shape_id), bool(p.get('wavy'))))
        else:
            raise K.KitError('BAD_SHAPE', f'{shape_id}: unknown part type {t!r} (box, slab, cyl, rect, blob)')
    slots = []
    for s in shape.get('slots') or []:
        if 'y' in s:
            slots.append((float(s['x']), float(s['y'])))
        else:
            slots.append((float(s['x']), sy(float(s.get('d', 0)), float(s.get('z', 0)))))
    return out, slots


def extent(parts):
    xs, ys = [], []
    for p in parts:
        if p[0] == 'rect':
            xs += [p[1], p[3]]
            ys += [p[2], p[4]]
        elif p[0] == 'cyl':
            xs += [p[1] - p[2], p[1] + p[2]]
            ys += [p[3] - p[5], p[4] + p[5]]
        else:
            xs += [p[1] - p[4] / 2, p[1] + p[4] / 2]
            ys += [p[2], p[3]]
    return min(xs), max(xs), min(ys), max(ys)


def _blob(d, X, Y, p, fill, outline, lw):
    _, cx, y0, y1, w, wavy = p
    box = [X(cx - w / 2), Y(y1), X(cx + w / 2), Y(y0)]
    if not wavy:
        d.ellipse(box, fill=fill, outline=outline, width=lw)
        return
    # leafy edge: bumps along a slightly smaller ellipse, all inside the box
    rx, ry, cy = (box[2] - box[0]) / 2, (box[3] - box[1]) / 2, (box[1] + box[3]) / 2
    bump = max(3.0, min(rx, ry) * 0.16)
    n = max(10, int(2 * math.pi * max(rx, ry) / (bump * 1.5)))
    pts = [((box[0] + box[2]) / 2 + (rx - bump) * math.cos(2 * math.pi * i / n), cy + (ry - bump) * math.sin(2 * math.pi * i / n)) for i in range(n)]
    if outline is not None:
        for x, y in pts:
            d.ellipse([x - bump, y - bump, x + bump, y + bump], fill=outline)
        for x, y in pts:
            d.ellipse([x - bump + lw, y - bump + lw, x + bump - lw, y + bump - lw], fill=fill)
        d.ellipse([box[0] + bump, box[1] + bump, box[2] - bump, box[3] - bump], fill=fill)
    else:
        for x, y in pts:
            d.ellipse([x - bump, y - bump, x + bump, y + bump], fill=fill)
        d.ellipse([box[0] + bump * 0.7, box[1] + bump * 0.7, box[2] - bump * 0.7, box[3] - bump * 0.7], fill=fill)


def paint(d, parts, X, Y, lw, mask=False):
    """faces (grey + lines) or, with mask, the white silhouette"""
    for p in parts:
        if p[0] == 'rect':
            box = [X(p[1]), Y(p[4]), X(p[3]), Y(p[2])]
            if mask:
                d.rectangle(box, fill=255)
            else:
                d.rectangle(box, fill=FACE.get(p[5], FACE['front']), outline=LINE, width=lw)
        elif p[0] == 'cyl':
            _, cx, r, b, t, ry = p
            base, top = [X(cx - r), Y(b + ry), X(cx + r), Y(b - ry)], [X(cx - r), Y(t + ry), X(cx + r), Y(t - ry)]
            body = [X(cx - r), Y(t), X(cx + r), Y(b)]
            if mask:
                d.ellipse(base, fill=255)
                d.rectangle(body, fill=255)
                d.ellipse(top, fill=255)
                continue
            d.ellipse(base, fill=FACE['front'], outline=LINE, width=lw)
            d.rectangle(body, fill=FACE['front'])
            d.line([X(cx - r), Y(t), X(cx - r), Y(b)], fill=LINE, width=lw)
            d.line([X(cx + r), Y(t), X(cx + r), Y(b)], fill=LINE, width=lw)
            d.ellipse(top, fill=FACE['top'], outline=LINE, width=lw)
            q = 0.82
            d.ellipse([X(cx - r * q), Y(t + ry * q), X(cx + r * q), Y(t - ry * q)], fill=FACE['inner'], outline=LINE, width=max(1, lw // 2))
        else:
            _blob(d, X, Y, p, 255 if mask else FACE['top'], None if mask else LINE, lw)


def load_spec(path):
    try:
        spec = json.loads(Path(path).read_text(encoding='utf-8'))
    except (OSError, ValueError) as e:
        raise K.KitError('BAD_SPEC', f'{path}: {e}')
    if spec.get('schema') != SCHEMA:
        raise K.KitError('BAD_SPEC', f'{path}: schema {spec.get("schema")!r}, want {SCHEMA}')
    pr = spec.get('projection') or {}
    if not all(isinstance(pr.get(k), (int, float)) for k in ('depth', 'height')) or pr['height'] <= 0 or pr['depth'] < 0:
        raise K.KitError('BAD_SPEC', f'{path}: "projection" needs depth >= 0 and height > 0 (screen y = d*depth + z*height)')
    if not isinstance(spec.get('unit_px'), (int, float)) or spec['unit_px'] < 8:
        raise K.KitError('BAD_SPEC', f'{path}: "unit_px" (px per cell in the game) is missing')
    if not isinstance(spec.get('shapes'), dict) or not spec['shapes']:
        raise K.KitError('BAD_SPEC', f'{path}: "shapes" is empty')
    return spec


def cmd_template(spec_path, out_dir, key='#ff00ff', gen_size=(1536, 1024), quiet=False):
    spec = load_spec(spec_path)
    kd, kh, unit = float(spec['projection']['depth']), float(spec['projection']['height']), float(spec['unit_px'])
    keyc = tuple(int(v) for v in K.hex_rgb(key))
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    bar = spec.get('height_bar')
    for sid, shape in spec['shapes'].items():
        parts, slots = resolve(sid, shape, kd, kh)
        x0, x1, y0, y1 = extent(parts)
        wc, hc = x1 - x0, y1 - y0
        if wc <= 0 or hc <= 0:
            raise K.KitError('BAD_SHAPE', f'{sid}: the shape has no size')
        # silhouette at game scale
        mw, mh = max(1, round(wc * unit)), max(1, round(hc * unit))
        m = Image.new('L', (mw, mh), 0)
        paint(ImageDraw.Draw(m), parts, lambda x: (x - x0) * unit, lambda y: (y1 - y) * unit, 1, mask=True)
        m.save(out_dir / f'mask_{sid}.png')
        # generator input
        gw, gh = gen_size
        k = min(gw * 0.78 / wc, gh * 0.82 / hc)
        g = Image.new('RGB', (gw, gh), keyc)
        gx, gy = gw / 2 - (x0 + x1) / 2 * k, gh / 2 + (y0 + y1) / 2 * k
        paint(ImageDraw.Draw(g), parts, lambda x: gx + x * k, lambda y: gy - y * k, max(3, round(k * 0.01)))
        g.save(out_dir / f'gen_{sid}.png')
        # people sheet (1 cell = 256 px)
        s, pad = 256, 90
        top = max(y1, (bar or {}).get('cells', 0)) + 0.15
        W, H = int(wc * s + 2 * pad + 240), int((top - min(y0, 0)) * s + 2 * pad + 40)
        im = Image.new('RGB', (W, H), PAPER)
        d = ImageDraw.Draw(im)
        ox, oy = pad - x0 * s, H - pad + min(y0, 0) * s
        X, Y = (lambda x: ox + x * s), (lambda y: oy - y * s)
        for i in range(0, int(top * 10) + 1):
            d.line([X(x0) - 30, Y(i / 10), X(x0) - (18 if i % 5 else 8), Y(i / 10)], fill=(150, 120, 90), width=1)
        fp = shape.get('footprint')
        if fp and kd > 0:
            fx, fd = fp[0] / 2, fp[1] * kd
            for a, b in (((-fx, 0), (fx, 0)), ((-fx, fd), (fx, fd)), ((-fx, 0), (-fx, fd)), ((fx, 0), (fx, fd))):
                for i in range(0, 30, 2):
                    p0 = (a[0] + (b[0] - a[0]) * i / 30, a[1] + (b[1] - a[1]) * i / 30)
                    p1 = (a[0] + (b[0] - a[0]) * (i + 1) / 30, a[1] + (b[1] - a[1]) * (i + 1) / 30)
                    d.line([X(p0[0]), Y(p0[1]), X(p1[0]), Y(p1[1])], fill=FOOT, width=2)
        paint(d, parts, X, Y, 3)
        item = float(shape.get('item_cells', 0) or 0)
        for i, (sx, sy_) in enumerate(slots):
            if item:
                d.rectangle([X(sx - item / 2), Y(sy_ + item), X(sx + item / 2), Y(sy_)], outline=SLOT, width=2)
            d.ellipse([X(sx) - 6, Y(sy_) - 6, X(sx) + 6, Y(sy_) + 6], fill=SLOT)
            d.text((X(sx) + 8, Y(sy_) - 22), str(i + 1), fill=SLOT, font=K.font(20))
        d.line([X(x0) - 50, Y(0), X(x1) + 200, Y(0)], fill=(90, 60, 40), width=2)
        if bar:
            hx = X(x1) + 110
            d.rectangle([hx, Y(float(bar['cells'])), hx + 26, Y(0)], fill=BAR, outline=LINE)
            d.text((hx - 20, Y(float(bar['cells'])) - 28), str(bar.get('label', '1H')), fill=LINE, font=K.font(18))
        d.text((pad, 10), f"{shape.get('name', sid)} — 형태 규격 틀 (표시 1칸 = {s}px, 게임 {unit:g}px)", fill=LINE, font=K.font(22))
        d.text((pad, 40), f"폭 {wc:.3f}칸 · 높이 {hc:.3f}칸 · 투영 깊이×{kd:g} 높이×{kh:g}"
               + (f" · 파란 점선 = 바닥 자리 {fp[0]:g}×{fp[1]:g}칸" if fp and kd > 0 else '')
               + (f" · 주황 = 칸 {len(slots)}개" if slots else ''), fill=(110, 90, 70), font=K.font(15))
        im.save(out_dir / f'tmpl_{sid}.png')
        meta = {'schema': OUT_SCHEMA, 'id': sid, 'name': shape.get('name', sid), 'unit_px': unit, 'projection': {'depth': kd, 'height': kh},
                'width_cells': round(wc, 4), 'height_cells': round(hc, 4), 'aspect': round(wc / hc, 5),
                'size_px': [mw, mh], 'pivot_px': [round(-x0 * unit, 1), round(y1 * unit, 1)],
                'gen_px': [round(wc * k), round(hc * k)], 'gen_canvas': [gw, gh], 'key': key,
                'slots_cells': [[round(a, 4), round(b, 4)] for a, b in slots],
                'slots_px': [[round(a * unit, 1), round(b * unit, 1)] for a, b in slots],
                'mask': f'mask_{sid}.png', 'gen': f'gen_{sid}.png', 'sheet': f'tmpl_{sid}.png'}
        (out_dir / f'{sid}.shape.json').write_text(json.dumps(meta, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
        if not quiet:
            print(f"TEMPLATE {sid}: {wc:.3f} x {hc:.3f} cells ({mw} x {mh} px in the game), on the generator canvas {meta['gen_px'][0]} x {meta['gen_px'][1]} px "
                  f"-> {out_dir / f'gen_{sid}.png'}")
    return 0


def load_shape(path):
    try:
        meta = json.loads(Path(path).read_text(encoding='utf-8'))
    except (OSError, ValueError) as e:
        raise K.KitError('BAD_SHAPE', f'{path}: {e}')
    if meta.get('schema') != OUT_SCHEMA:
        raise K.KitError('BAD_SHAPE', f'{path}: schema {meta.get("schema")!r}, want {OUT_SCHEMA} (written by shape.py template)')
    mask = Path(path).parent / meta['mask']
    if not mask.is_file():
        raise K.KitError('NO_MASK', str(mask))
    return meta, np.asarray(Image.open(mask).convert('L')) > 127


def measure_fit(img, meta, mask):
    solid = img[..., 3] >= K.ALPHA_SOLID
    bb = K.bbox(solid)
    if bb is None:
        raise K.KitError('EMPTY_IMAGE', 'no opaque pixels (remove the background first: matte.py key)')
    w, h = bb[2] - bb[0], bb[3] - bb[1]
    off = (w / h) / meta['aspect'] - 1
    # set the figure on the template by height, bottom centre
    mh, mw = mask.shape
    fig = Image.fromarray((solid[bb[1]:bb[3], bb[0]:bb[2]] * 255).astype(np.uint8), 'L')
    fw = max(1, round(w * mh / h))
    fig = np.asarray(fig.resize((fw, mh), Image.LANCZOS)) > 127
    W = max(mw, fw)
    a, b = np.zeros((mh, W), bool), np.zeros((mh, W), bool)
    a[:, (W - mw) // 2:(W - mw) // 2 + mw] = mask
    b[:, (W - fw) // 2:(W - fw) // 2 + fw] = fig
    iou = float((a & b).sum()) / max(1, int((a | b).sum()))
    return {'figure_px': [w, h], 'aspect': round(w / h, 5), 'aspect_off': round(off, 4), 'iou': round(iou, 4),
            'width_cells_at_template_height': round(meta['height_cells'] * w / h, 4)}


def cmd_fit(paths, shape_path, tol=0.05, iou_min=0.0, quiet=False):
    meta, mask = load_shape(shape_path)
    bad = 0
    for p in paths:
        r = measure_fit(K.load_rgba(p), meta, mask)
        why = []
        if abs(r['aspect_off']) > tol:
            why.append(f"aspect {r['aspect_off']:+.1%} (limit ±{tol:.0%})")
        if r['iou'] < iou_min:
            why.append(f"iou {r['iou']:.3f} < {iou_min}")
        fail = bool(why)
        bad += fail
        try:
            kit = K.load_kit(Path(p).resolve().parent)
            K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': 'shape', 'image': str(Path(p).resolve()), 'sha256': K.sha256(p),
                                 'verdict': 'FAIL' if fail else 'PASS', 'shape': meta['id'], 'tol': tol, 'iou_min': iou_min, **r})
        except K.KitError:
            pass
        if not quiet:
            hint = ''
            if r['aspect_off'] > tol:
                hint = ' -> too wide: drop words like "wide" and give the px width; do not pre-narrow the template'
            elif r['aspect_off'] < -tol:
                hint = ' -> too narrow/tall: give the px width and height of the shape on the generator canvas'
            print(f"{'FAIL' if fail else 'PASS'} {Path(p).name}: {r['figure_px'][0]}x{r['figure_px'][1]} px, aspect {r['aspect']:.3f} vs template "
                  f"{meta['aspect']:.3f} ({r['aspect_off']:+.1%}), at template height {r['width_cells_at_template_height']:.3f} cells wide "
                  f"(template {meta['width_cells']:.3f}), iou {r['iou']:.3f}{hint}")
    if not quiet:
        print(f"{'FAIL' if bad else 'PASS'} fit to {meta['id']}: {len(paths)} pictures, {bad} off the template (measured before scaling to it)")
    return 1 if bad else 0


# ---------- selftest ----------

def selftest():
    td = Path(tempfile.mkdtemp(prefix='shape-selftest-'))
    cases = []

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    spec = {'schema': SCHEMA, 'unit_px': 256, 'projection': {'depth': 0.77, 'height': 0.64}, 'height_bar': {'label': 'hero 1H', 'cells': 1.0},
            'shapes': {'crate': {'name': 'demo crate', 'footprint': [2.0, 1.0], 'item_cells': 0.5,
                                 'parts': [{'type': 'box', 'x': [-1, 1], 'd': [0, 1], 'z': [0, 0.5]}],
                                 'slots': [{'x': -0.5, 'd': 0.5, 'z': 0.5}, {'x': 0.5, 'd': 0.5, 'z': 0.5}]},
                       'barrel': {'parts': [{'type': 'cyl', 'cx': 0, 'r': 0.5, 'd': 0.5, 'z': [0, 0.45]}]},
                       'bush': {'parts': [{'type': 'rect', 'x': [-0.1, 0.1], 'y': [0, 0.4]}, {'type': 'blob', 'cx': 0, 'y': [0.25, 1.0], 'w': 1.0, 'wavy': True}]},
                       'dome': {'parts': [{'type': 'rect', 'x': [-0.1, 0.1], 'y': [0, 0.4]}, {'type': 'blob', 'cx': 0, 'y': [0.25, 1.0], 'w': 1.0}]}}}
    sp = td / 'spec.json'
    sp.write_text(json.dumps(spec))
    out = td / 'out'
    case('template written for every shape', cmd_template(sp, out, quiet=True) == 0 and all((out / f'{n}_{s}.png').is_file() for n in ('gen', 'tmpl', 'mask') for s in spec['shapes']))
    meta = json.loads((out / 'crate.shape.json').read_text())
    case('box 2 x 1 x 0.5 at depth 0.77 / height 0.64: 2.0 wide, 1.09 high', abs(meta['width_cells'] - 2.0) < 1e-6 and abs(meta['height_cells'] - 1.09) < 1e-6,
         f"{meta['width_cells']} x {meta['height_cells']}")
    case('slots projected: y = 0.5*0.77 + 0.5*0.64 = 0.705 cells = 180.5 px', meta['slots_cells'] == [[-0.5, 0.705], [0.5, 0.705]] and meta['slots_px'][0] == [-128.0, 180.5], str(meta['slots_px']))
    g = np.asarray(Image.open(out / 'gen_crate.png').convert('RGB')).astype(int)
    t = np.asarray(Image.open(out / 'tmpl_crate.png').convert('RGB')).astype(int)

    def has(img, col):
        return bool((np.abs(img - np.array(col)).sum(-1) < 12).any())
    case('generator input: key background, grey faces, and none of the slot / footprint / bar colours',
         tuple(g[0, 0]) == (255, 0, 255) and has(g, FACE['front']) and has(g, FACE['top']) and not has(g, SLOT) and not has(g, FOOT) and not has(g, BAR))
    case('people sheet carries slots, footprint and the height bar', has(t, SLOT) and has(t, FOOT) and has(t, BAR))
    gp = meta['gen_px']
    gb = K.bbox(np.abs(g - np.array([255, 0, 255])).sum(-1) > 40)
    case('gen_px is the size of the shape on the generator canvas', abs((gb[2] - gb[0]) - gp[0]) <= 4 and abs((gb[3] - gb[1]) - gp[1]) <= 4, f'{gb} vs {gp}')
    # fit: the template's own silhouette, then widened and narrowed
    mask = np.asarray(Image.open(out / 'mask_crate.png')) > 127

    def fig(scale_x, name):
        h, w = mask.shape
        m = Image.fromarray((mask * 255).astype(np.uint8)).resize((round(w * scale_x * 0.6), round(h * 0.6)), Image.LANCZOS)
        im = Image.new('RGBA', (m.width + 40, m.height + 40), (0, 0, 0, 0))
        im.paste(Image.new('RGBA', m.size, (200, 150, 90, 255)), (20, 20), m)
        im.save(td / name)
        return td / name
    case('fit PASS for the same proportions at another size', cmd_fit([fig(1.0, 'same.png')], out / 'crate.shape.json', quiet=True) == 0)
    case('counter: 12% wider FAIL', cmd_fit([fig(1.12, 'wide.png')], out / 'crate.shape.json', quiet=True) == 1)
    case('counter: 12% narrower FAIL', cmd_fit([fig(0.88, 'narrow.png')], out / 'crate.shape.json', quiet=True) == 1)
    case('3% wider is inside the default 5%', cmd_fit([fig(1.03, 'near.png')], out / 'crate.shape.json', quiet=True) == 0)
    m2, mk = load_shape(out / 'crate.shape.json')
    case('iou of the same shape is near 1, and --iou-min can gate it', measure_fit(K.load_rgba(td / 'same.png'), m2, mk)['iou'] > 0.97
         and cmd_fit([td / 'wide.png'], out / 'crate.shape.json', tol=0.5, iou_min=0.99, quiet=True) == 1)
    wavy, dome = (np.asarray(Image.open(out / f'mask_{n}.png')) > 127 for n in ('bush', 'dome'))
    case('a wavy blob is not the smooth dome (the generator copies a smooth arch as drawn)', wavy.shape == dome.shape and int((wavy != dome).sum()) > 200, str(int((wavy != dome).sum())))
    bm = json.loads((out / 'barrel.shape.json').read_text())
    case('cylinder: 1.0 wide, height = 0.45*0.64 + 2 * 0.5*0.77', abs(bm['width_cells'] - 1.0) < 1e-6 and abs(bm['height_cells'] - (0.288 + 0.77)) < 1e-6, str(bm['height_cells']))
    side = dict(spec, projection={'depth': 0, 'height': 1}, shapes={'post': {'parts': [{'type': 'box', 'x': [-0.2, 0.2], 'd': [0, 1], 'z': [0, 1.5]}]}})
    sp2 = td / 'side.json'
    sp2.write_text(json.dumps(side))
    cmd_template(sp2, td / 'side', quiet=True)
    case('side view (depth 0): a box is its front face only', json.loads((td / 'side/post.shape.json').read_text())['height_cells'] == 1.5)

    def bad_spec(change):
        b = json.loads(json.dumps(spec))
        change(b)
        f = td / 'bad.json'
        f.write_text(json.dumps(b))
        return main(['template', str(f), '--out', str(td / 'bad')])
    case('bad spec -> exit 2 (unknown part, no projection, wrong schema)',
         bad_spec(lambda b: b['shapes']['crate']['parts'].append({'type': 'cone'})) == 2
         and bad_spec(lambda b: b.pop('projection')) == 2 and bad_spec(lambda b: b.update(schema='x')) == 2)
    empty = td / 'empty.png'
    Image.new('RGBA', (32, 32), (0, 0, 0, 0)).save(empty)
    case('an empty picture is an input problem, not a FAIL', main(['fit', str(empty), '--shape', str(out / 'crate.shape.json')]) == 2)
    root = td / 'proj'
    (root / '.ai/2d-art').mkdir(parents=True)
    (root / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'work_dir': 'out'}))
    Image.open(td / 'wide.png').save(root / 'wide.png')
    cmd_fit([root / 'wide.png'], out / 'crate.shape.json', quiet=True)
    rec = K.read_record(K.load_kit(root), K.sha256(root / 'wide.png'), 'shape')
    case('kit project: shape record written (FAIL, aspect_off about +12%)', rec and rec['verdict'] == 'FAIL' and 0.09 < rec['aspect_off'] < 0.15, str(rec and rec.get('aspect_off')))
    bad = cases.count(False)
    print(f"shape selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    t = sub.add_parser('template')
    t.add_argument('spec', type=Path)
    t.add_argument('--out', type=Path, required=True)
    t.add_argument('--key', default='#ff00ff')
    t.add_argument('--gen-size', default='1536x1024')
    f = sub.add_parser('fit')
    f.add_argument('paths', nargs='+')
    f.add_argument('--shape', type=Path, required=True)
    f.add_argument('--tol', type=float, default=0.05)
    f.add_argument('--iou-min', type=float, default=0.0)
    sub.add_parser('selftest')
    a = ap.parse_args(argv)
    if a.cmd == 'selftest':
        return selftest()
    try:
        if a.cmd == 'template':
            try:
                gw, gh = (int(v) for v in a.gen_size.lower().split('x'))
            except ValueError:
                raise K.KitError('BAD_SIZE', f'--gen-size wants WxH, got {a.gen_size}')
            return cmd_template(a.spec, a.out, a.key, (gw, gh))
        missing = [str(p) for p in a.paths if not Path(p).is_file()]
        if missing:
            raise K.KitError('NO_IMAGE', ', '.join(missing))
        return cmd_fit(a.paths, a.shape, a.tol, a.iou_min)
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
