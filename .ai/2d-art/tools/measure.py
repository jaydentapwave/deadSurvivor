#!/usr/bin/env python3
"""Measure 2D candidates against an approved identity reference and record PASS/FAIL (2d-art-produce step 4).

  measure.py compare --id <identity> --img <candidate>... [--changed part,part] [--changed-box x0,y0,x1,y1] [--edit|--new] [--pose]
  measure.py identity --id <identity>  # right after registering: part masks, their overlaps, and the reference against itself
  measure.py calibrate --labels <labels.json> [--out <json>]   # how far the judgement agrees with past human decisions
  measure.py check                     # re-judge the last compare (work_dir/measure/last.json): 0 all PASS, 1 not, 2 none
  measure.py selftest

compare exit: 0 every candidate PASS / 1 some FAIL / 3 none FAIL but some NOT_ASSESSED / 2 input, config or spec problem.
The art spec must be approved (approve.py record) and hold a ```json 2d-measure block; the identity file
(<identities_dir>/<id>.json) names the approved reference image and its parts. Judged against the reference's measured
values; the parts' hex is only the seed that finds them: the reference's own mean colour of each part is then the seed
for the reference and the candidate alike, so the reference compared with itself always passes.
A part without its own "lab_tol" gets the default (tolerance lab_tol, 30) capped at half the dE to the nearest other
part colour — on cel-shaded art with similar paints (fur, hat, cream) the default alone lets the masks run into each
other. `identity` prints what each part got, how much the masks overlap and what to lower; exit 0 only if the
reference passes against itself and no two part masks share more than 5% of the smaller one.

Items (per candidate):
  same_outside   edit result only (--edit, the default when --changed/--changed-box is given): pixels outside the changed
                 region (changed parts + box, dilated) equal the reference within same_tol; differing / visible pixels
                 outside the region <= same_max_px. A different canvas is aligned by the figure bbox first.
  part:<name>    (the changed box is left out of every part, in both pictures) mean colour dE76 <= de76; unless the part is in --changed: area, width, height within part_area/part_dim,
                 offset from the anchor part <= part_offset (fraction of the figure diagonal). Empty or tiny mask = NOT_ASSESSED
  outline        mean colour dE76 <= de76 and stroke width / anchor width within stroke_ratio of the reference
  --pose         the pose changed (sitting, holding, a newly drawn pose): part area/width/height/offset become INFO
                 (shown, not judged). The area of parts marked "fixed": true in the identity (head, bow: they do not
                 grow with a pose) is shown too and FAILs only beyond the wide tolerance pose_area (default 0.30 = +-30%;
                 2d-measure tolerance, or "pose_area" on one identity part): a redrawn pose moves a fixed part's area by
                 5-30% in pictures people accept, so the narrow part_area would block nearly all of them, while a head
                 half as big again is still caught. A part marked "occludable": true in the identity (a buckle the arm
                 can cover) that is not found in a pose is shown as INFO instead of NOT_ASSESSED; give such a part a
                 wide "pose_area" too, it is half covered more often than fully. Colour, outline and stroke still gate;
                 with no fixed part found the result is NOT_ASSESSED. Without --pose fixed parts are judged like any part (part_area).
                 On one past set (51 pictures, some labels inferred) shape items blocked 10 of 12 accepted pose variants
                 and colour/stroke none; on another all 22 accepted-looking redrawn frames failed on fixed area alone.
                 Re-measure with `calibrate`. PASS never means "natural" — that is the part check and the user.
  haze           semi-transparent pixels (0 < alpha < 48) / visible pixels, at most haze_max above the reference's own
Scale: candidates are compared after scaling by the anchor part (identity width_part), so a larger canvas passes. Generate at
the reference resolution or above: below it the stroke width (a few pixels) quantizes past stroke_ratio.
Records: work_dir/measure/<sha256>.compare.json per candidate, last.json for `check`; sheet.py shows only PASS records."""
import argparse
import json
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402

HAZE_ALPHA = 48


def part_seeds(ident, seeds=None):
    """{name: Lab seed}: the given seed (reference mean) where there is one, else the identity hex"""
    return {n: (seeds[n] if seeds and n in seeds else K.rgb_to_lab(K.hex_rgb(p['hex']))) for n, p in (ident.get('parts') or {}).items()}


def part_lab_tols(ident, tol, used):
    """{name: lab_tol}. A part's own "lab_tol" is taken as written. The default is capped at half the dE to the nearest
    other part colour, so two similar paints do not claim the same pixels. Parts of one colour (dE < 2: left/right leg)
    and pairs both limited by a box_rel are told apart by position, not colour, and do not cap each other."""
    parts = ident.get('parts') or {}
    out = {}
    for n, p in parts.items():
        if 'lab_tol' in p:
            out[n] = float(p['lab_tol'])
            continue
        near = [K.de76(used[n], used[m]) for m, q in parts.items() if m != n and not (p.get('box_rel') and q.get('box_rel'))]
        near = [d for d in near if d >= 2]
        out[n] = min(float(tol['lab_tol']), max(4.0, min(near) / 2)) if near else float(tol['lab_tol'])
    return out


def part_masks(img, ident, tol, seeds=None, exclude=None):
    """{name: mask} for the identity parts. seeds: {name: Lab} (reference means) else the hex colours.
    exclude: pixels left out of every part (the changed box — what is drawn there is the change, not the part)."""
    a = img[..., 3]
    solid = a >= K.ALPHA_SOLID
    lab = K.rgb_to_lab(img[..., :3])
    box = K.bbox(solid if exclude is None else solid & ~exclude)    # box_rel is of the figure without the changed box
    out = {}
    used = part_seeds(ident, seeds)
    lab_tols = part_lab_tols(ident, tol, used)
    for name, p in (ident.get('parts') or {}).items():
        m = solid & (np.linalg.norm(lab - used[name], axis=-1) <= lab_tols[name])
        if box and p.get('box_rel'):
            x0, y0, x1, y1 = box
            w, h = x1 - x0, y1 - y0
            r = p['box_rel']
            lim = np.zeros_like(m)
            lim[int(y0 + r[1] * h):int(np.ceil(y0 + r[3] * h)), int(x0 + r[0] * w):int(np.ceil(x0 + r[2] * w))] = True
            m &= lim
        if exclude is not None:
            m &= ~exclude
        out[name] = m
    return out


def stats(img, ident, tol, luma_max, seeds=None, exclude=None):
    masks = part_masks(img, ident, tol, seeds, exclude)
    lab = K.rgb_to_lab(img[..., :3])
    solid = img[..., 3] >= K.ALPHA_SOLID
    parts = {}
    for name, m in masks.items():
        n = int(m.sum())
        b = K.bbox(K.dilate(K.erode(m), 1)) or K.bbox(m)      # opened: stray same-colour specks do not stretch the box
        ys, xs = np.nonzero(m)
        parts[name] = {'px': n, 'lab': lab[m].mean(axis=0) if n else None, 'w': (b[2] - b[0]) if b else 0,
                       'h': (b[3] - b[1]) if b else 0, 'c': (float(xs.mean()), float(ys.mean())) if n else None}
    excl = np.zeros_like(solid)
    for name in ident.get('outline_exclude') or []:
        if name in masks:
            excl |= masks[name]
    outline = solid & (K.luma(img[..., :3]) < luma_max) & ~excl
    anchor = ident.get('width_part')
    aw = parts.get(anchor, {}).get('w') or 0
    sw = K.stroke_width(outline) if outline.any() else None
    fb = K.bbox(solid)
    vis = img[..., 3] > 0
    haze = img[..., 3]
    return {'parts': parts, 'masks': masks, 'anchor_w': aw, 'anchor_c': parts.get(anchor, {}).get('c'),
            'outline_lab': lab[outline].mean(axis=0) if outline.any() else None,
            'stroke': (sw / aw) if (sw and aw) else None,
            'diag': float(np.hypot(fb[2] - fb[0], fb[3] - fb[1])) if fb else 0.0,
            'haze': float(((haze > 0) & (haze < HAZE_ALPHA)).sum()) / max(1, int(vis.sum()))}


def item(name, ref, value, tol, verdict, note=''):
    r = lambda v: round(v, 4) if isinstance(v, float) else v  # noqa: E731
    return {'item': name, 'ref': r(ref), 'value': r(value), 'tol': r(tol), 'verdict': verdict, **({'note': note} if note else {})}


def align_to(ref, cand):
    """cand placed on the reference canvas so its figure bbox matches the reference's; None if the figure aspect differs > 2%"""
    rb, cb = K.bbox(ref[..., 3] > 0), K.bbox(cand[..., 3] > 0)
    if not rb or not cb:
        return None
    rw, rh, cw, ch = rb[2] - rb[0], rb[3] - rb[1], cb[2] - cb[0], cb[3] - cb[1]
    if abs(rw / rh - cw / ch) > 0.02 * (rw / rh):
        return None
    crop = Image.fromarray(cand[cb[1]:cb[3], cb[0]:cb[2]].astype(np.uint8), 'RGBA')
    if crop.size != (rw, rh):
        crop = crop.resize((rw, rh), Image.LANCZOS)
    out = np.zeros_like(ref)
    out[rb[1]:rb[3], rb[0]:rb[2]] = np.asarray(crop, dtype=np.float32)
    return out


def same_outside(ref, cand, rs, cs, changed, box_rel, tol, ident=None, seeds=None):
    if ref.shape != cand.shape:             # same canvas: compare in place (an edit may legitimately grow the figure)
        aligned = align_to(ref, cand)
        if aligned is None:
            return item('same_outside', 0, None, tol['same_max_px'], 'NOT_ASSESSED', 'figure bbox aspect differs from the reference (> 2%)')
        cand = aligned                      # the candidate's part masks are measured again on the aligned picture
        cs = {'masks': part_masks(cand, ident, tol, seeds)} if ident else None
    region = np.zeros(ref.shape[:2], dtype=bool)
    for name in changed:
        region |= rs['masks'].get(name, False)
        if cs is not None:
            region |= cs['masks'].get(name, False)
    if box_rel:
        region |= box_mask(ref.shape, box_rel)
    stroke_px = (rs['stroke'] or 0) * (rs['anchor_w'] or 0)       # a changed part takes its outline with it
    region = K.dilate(region, tol['dilate_px'] + int(np.ceil(stroke_px)))
    diff = np.abs(ref - cand).max(axis=-1) > tol['same_tol']
    either = ((ref[..., 3] > 0) | (cand[..., 3] > 0)) & ~region
    bad = int((diff & either).sum())
    ratio = bad / max(1, int(either.sum()))          # of the visible pixels outside the changed region
    return item('same_outside', 0, ratio, tol['same_max_px'], 'PASS' if ratio <= tol['same_max_px'] else 'FAIL', f'{bad} px differ')


def box_mask(shape, box_rel, grow=0):
    if not box_rel:
        return None
    h, w = shape[:2]
    m = np.zeros((h, w), dtype=bool)
    m[int(box_rel[1] * h):int(np.ceil(box_rel[3] * h)), int(box_rel[0] * w):int(np.ceil(box_rel[2] * w))] = True
    return K.dilate(m, grow) if grow else m


SHAPE_KEYS = ('area', 'w', 'h', 'offset')


def ref_seeds(ref_img, ident, tol, luma_max, box_rel=None):
    """{name: Lab}: the reference's own mean colour of each part, found from the identity hex. Every later measurement
    — of the reference too — starts from these, so reference and candidate masks are cut by the same rule."""
    first = stats(ref_img, ident, tol, luma_max, exclude=box_mask(ref_img.shape, box_rel, tol['dilate_px']))
    return {n: p['lab'] for n, p in first['parts'].items() if p['lab'] is not None}


def judge(ref_img, cand_img, ident, tol, luma_max, changed=(), box_rel=None, edit=False, pose=False):
    seeds = ref_seeds(ref_img, ident, tol, luma_max, box_rel)
    rs = stats(ref_img, ident, tol, luma_max, seeds, exclude=box_mask(ref_img.shape, box_rel, tol['dilate_px']))
    cs = stats(cand_img, ident, tol, luma_max, seeds, exclude=box_mask(cand_img.shape, box_rel, tol['dilate_px']))
    items = []
    if edit:
        items.append(same_outside(ref_img, cand_img, rs, cs, changed, box_rel, tol, ident, seeds))
    anchor_ok = ident.get('width_part') not in changed and rs['anchor_w'] and cs['anchor_w']
    scale = (rs['anchor_w'] / cs['anchor_w']) if anchor_ok else None
    for name, rp in rs['parts'].items():
        cp = cs['parts'][name]
        if not rp['px']:
            items.append(item(f'part:{name}', 0, None, None, 'NOT_ASSESSED', 'not found in the reference'))
            continue
        if cp['px'] < tol['min_part_px'] * rp['px'] * ((1 / scale ** 2) if scale else 1) or cp['lab'] is None:
            items.append(item(f'part:{name}', rp['px'], cp['px'], None, 'NOT_ASSESSED', 'part not identified in the candidate'))
            continue
        d = K.de76(rp['lab'], cp['lab'])
        items.append(item(f'part:{name}:color', 0.0, d, tol['de76'], 'PASS' if d <= tol['de76'] else 'FAIL'))
        if name in changed:
            continue
        if scale is None:
            items.append(item(f'part:{name}:shape', None, None, None, 'NOT_ASSESSED', 'anchor part missing or changed'))
            continue
        for key, rv, cv, t in (('area', rp['px'], cp['px'] * scale ** 2, tol['part_area']),
                               ('w', rp['w'], cp['w'] * scale, tol['part_dim']), ('h', rp['h'], cp['h'] * scale, tol['part_dim'])):
            rel = abs(cv - rv) / max(rv, 1)
            items.append(item(f'part:{name}:{key}', float(rv), float(cv), t, 'PASS' if rel <= t else 'FAIL', f'{rel:+.1%}'.replace('+', '±')))
        if name != ident.get('width_part') and rs['anchor_c'] and cs['anchor_c'] and rs['diag']:
            ro = np.subtract(rp['c'], rs['anchor_c'])
            co = np.subtract(cp['c'], cs['anchor_c']) * scale
            off = float(np.linalg.norm(ro - co)) / rs['diag']
            items.append(item(f'part:{name}:offset', 0.0, off, tol['part_offset'], 'PASS' if off <= tol['part_offset'] else 'FAIL'))
    if rs['outline_lab'] is not None:
        if cs['outline_lab'] is None:
            items.append(item('outline', None, None, None, 'NOT_ASSESSED', 'no outline pixels in the candidate'))
        else:
            d = K.de76(rs['outline_lab'], cs['outline_lab'])
            items.append(item('outline:color', 0.0, d, tol['de76'], 'PASS' if d <= tol['de76'] else 'FAIL'))
            if rs['stroke'] and cs['stroke']:
                diff = abs(cs['stroke'] - rs['stroke'])
                items.append(item('outline:stroke_ratio', rs['stroke'], cs['stroke'], tol['stroke_ratio'],
                                  'PASS' if diff <= tol['stroke_ratio'] else 'FAIL'))
            else:
                items.append(item('outline:stroke_ratio', rs['stroke'], cs['stroke'], tol['stroke_ratio'], 'NOT_ASSESSED', 'anchor part or stroke missing'))
    if pose:
        # calibration (51 past pictures): with the pose changed, part size/offset blocked 10 of 12 accepted pictures while
        # colour and stroke blocked none. So for a pose change they are shown, not judged; colour/outline/stroke still gate.
        fixed = {n for n, v in (ident.get('parts') or {}).items() if v.get('fixed')}
        seen_fixed = False
        for i in items:
            name, key = (i['item'].split(':') + ['', ''])[1:3]
            if i['verdict'] == 'NOT_ASSESSED' and not key and (ident['parts'].get(name) or {}).get('occludable') and i['value'] is not None:
                i['verdict'] = 'INFO'       # a small part another limb covers in this pose (bag buckle behind the arm)
                i['note'] = 'not seen in this pose (identity: occludable): shown only'
                continue
            if not (i['item'].startswith('part:') and key in SHAPE_KEYS and i['verdict'] in ('PASS', 'FAIL')):
                continue
            if name in fixed and key == 'area':
                # an identity part (head, bow) does not grow with a pose, but a redrawn pose moves its area by 5-30% in
                # accepted pictures (calibration): shown, and blocked only beyond the wide pose tolerance
                wide = float((ident['parts'][name] or {}).get('pose_area', tol['pose_area']))
                rel = abs(i['value'] - i['ref']) / max(i['ref'], 1)
                seen_fixed = True
                i['tol'] = wide
                if rel > wide:
                    i['verdict'] = 'FAIL'
                    i['note'] = (i.get('note', '') + f' fixed part beyond the pose tolerance').strip()
                    continue
                i['note'] = (i.get('note', '') + ' fixed part within the pose tolerance: shown only').strip()
            else:
                i['note'] = (i.get('note', '') + ' pose change: shown only').strip()
            i['verdict'] = 'INFO'
        if not seen_fixed:
            # without a fixed part found a pose change would be colour-only: say so instead of passing
            items.append(item('parts', None, None, None, 'NOT_ASSESSED', 'pose change needs a "fixed" identity part that is found'))
    elif not any(i['item'].startswith('part:') and i['item'].rsplit(':', 1)[-1] in ('area', 'w', 'h') and i['verdict'] != 'NOT_ASSESSED'
                 for i in items):
        items.append(item('parts', None, None, None, 'NOT_ASSESSED', 'no part shape could be judged (none found, or all changed)'))
    extra = cs['haze'] - rs['haze']                     # hand-drawn edges are soft: judge only haze the reference does not have
    items.append(item('haze', rs['haze'], cs['haze'], tol['haze_max'], 'PASS' if extra <= tol['haze_max'] else 'FAIL'))
    verdicts = {i['verdict'] for i in items}
    overall = 'FAIL' if 'FAIL' in verdicts else 'NOT_ASSESSED' if 'NOT_ASSESSED' in verdicts else 'PASS'
    return overall, items


def parse_box(s):
    if not s:
        return None
    v = [float(x) for x in s.split(',')]
    if len(v) != 4 or not all(0 <= x <= 1 for x in v):
        raise K.KitError('BAD_BOX', f'--changed-box wants x0,y0,x1,y1 in 0..1 of the canvas, got {s}')
    return v


def compare(kit, ident_id, imgs, changed=(), box_rel=None, edit=None, quiet=False, pose=False):
    last = kit.measure_dir / 'last.json'
    last.unlink(missing_ok=True)                    # any aborted compare (bad id, part, image) must not leave the previous batch
    tol, out, spec = K.approved_tolerance(kit)
    ident = K.load_identity(kit, ident_id)
    unknown = [c for c in changed if c not in (ident.get('parts') or {})]
    if unknown:
        raise K.KitError('UNKNOWN_PART', f'{unknown} not in {ident["_path"]} parts')
    clash = [c for c in changed if (ident['parts'].get(c) or {}).get('fixed')]
    if clash:
        raise K.KitError('FIXED_PART_CHANGED', f'{clash} are "fixed" identity parts and cannot be --changed; change the identity (re-approve) instead')
    edit = bool(changed or box_rel) if edit is None else edit
    missing = [str(p) for p in imgs if not Path(p).is_file()]
    if missing:
        raise K.KitError('NO_IMAGE', ', '.join(missing))
    ref = K.load_rgba(ident['_ref_path'])
    results = []
    for p in imgs:
        p = Path(p)
        overall, items = judge(ref, K.load_rgba(p), ident, tol, out['luma_max'], changed, box_rel, edit, pose)
        rec = K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': 'compare', 'image': str(p.resolve()), 'sha256': K.sha256(p),
                                   'identity': ident_id, 'ref': str(ident['_ref_path']), 'ref_sha256': ident['ref_sha256'], 'identity_sha256': ident['_sha256'],
                                   'spec_sha256': K.sha256(spec), 'changed': list(changed), 'changed_box': box_rel, 'edit': edit, 'pose': pose,
                                   'verdict': overall, 'items': items})
        results.append(rec)
        if not quiet:
            print(f'{overall:12} {p.name}')
            for i in items:
                if i['verdict'] != 'PASS' or not quiet:
                    print(f"  {i['verdict']:12} {i['item']:24} ref {i['ref']} value {i['value']} tol {i['tol']}" + (f"  {i['note']}" if i.get('note') else ''))
    kit.measure_dir.mkdir(parents=True, exist_ok=True)
    last.write_text(json.dumps([r['sha256'] for r in results]) + '\n', encoding='utf-8')
    verdicts = {r['verdict'] for r in results}
    return 1 if 'FAIL' in verdicts else 3 if 'NOT_ASSESSED' in verdicts else 0


def identity(kit, ident_id, quiet=False):
    """what the identity's parts catch on its own reference, and the reference judged against itself. 0 ok / 1 look"""
    tol, out, _ = K.approved_tolerance(kit)
    ident = K.load_identity(kit, ident_id)
    ref = K.load_rgba(ident['_ref_path'])
    seeds = ref_seeds(ref, ident, tol, out['luma_max'])
    rs = stats(ref, ident, tol, out['luma_max'], seeds)
    tols = part_lab_tols(ident, tol, part_seeds(ident, seeds))
    names = list(ident['parts'])
    bad = 0
    lines = []
    for n in names:
        p, own = rs['parts'][n], 'lab_tol' in ident['parts'][n]
        mean = '#%02x%02x%02x' % tuple(int(v) for v in ref[..., :3][rs['masks'][n]].mean(axis=0)) if p['px'] else '-'
        note = 'own lab_tol' if own else 'default' if tols[n] >= float(tol['lab_tol']) else f"default {tol['lab_tol']:g} capped: a similar part colour is {tols[n] * 2:.1f} away"
        lines.append(f"  part {n:14} {p['px']:>8} px  hex {ident['parts'][n]['hex']} measured {mean}  lab_tol {tols[n]:.1f} ({note})"
                     + ('' if p['px'] else '  NOT FOUND'))
        bad += not p['px']
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            both = int((rs['masks'][a] & rs['masks'][b]).sum())
            small = min(rs['parts'][a]['px'], rs['parts'][b]['px'])
            if both and small:
                share = both / small
                d = K.de76(seeds[a], seeds[b])
                over = share > 0.05
                bad += over
                lines.append(f"  {'OVERLAP' if over else 'overlap'} {a} / {b}: {both} px = {share:.1%} of the smaller mask (colours dE {d:.1f} apart)"
                             + (f' -> set "lab_tol" on both to {max(2.0, d / 2):.0f} or less, or separate them with "box_rel"' if over else ''))
    verdict, items = judge(ref, ref, ident, tol, out['luma_max'])
    if not quiet:
        print(f"identity {ident_id}: reference {ident['_ref_path'].name}")
        for ln in lines:
            print(ln)
        print(f'  reference against itself: {verdict}')
        for i in items:
            if i['verdict'] not in ('PASS', 'INFO'):
                print(f"    {i['verdict']:12} {i['item']:24} ref {i['ref']} value {i['value']}" + (f"  {i['note']}" if i.get('note') else ''))
        print(f"identity check {'PASS' if verdict == 'PASS' and not bad else 'FAIL'}")
    return 0 if verdict == 'PASS' and not bad else 1


def check(kit):
    last = kit.measure_dir / 'last.json'
    if not last.is_file():
        print(f'NO_MEASURE {last} -> run measure.py compare first')
        return 2
    try:
        shas = json.loads(last.read_text())
    except ValueError:
        shas = None
    if not isinstance(shas, list) or not shas:
        print(f'NO_MEASURE {last} is empty -> run measure.py compare first')
        return 2
    bad = 0
    for sha in shas:
        rec = K.read_record(kit, sha)
        why = 'no record' if not rec else K.stale_reason(kit, rec)
        if not why and not (Path(rec['image']).is_file() and K.sha256(rec['image']) == sha):
            why = 'image changed or gone'
        v = rec['verdict'] if rec else 'MISSING'
        if v != 'PASS' or why:
            bad += 1
        print(f"{v:12} {Path(rec['image']).name if rec else sha}" + (f'  ({why} -> re-measure)' if why else ''))
    print(f"measure check {'PASS' if not bad else 'FAIL'}")
    return 1 if bad else 0


FAMILIES = {'color': lambda n: n.endswith(':color'), 'stroke': lambda n: n == 'outline:stroke_ratio',
            'shape': lambda n: n.split(':')[-1] in SHAPE_KEYS, 'same_outside': lambda n: n == 'same_outside',
            'haze': lambda n: n == 'haze'}


LABELS = {'accept': True, '받음': True, '받아들임': True, 'reject': False, '거절': False}


def calibrate(kit, labels_path, out=None, quiet=False):
    """labels.json: [{"img", "identity", "label": accept|reject (받음|받아들임|거절), "changed"?: [..], "pose"?: bool, "edit"?: bool}].
    `img` is relative to the labels file. Judges every picture exactly as compare would (same edit default, same fixed-part
    rules) but writes no measure record and leaves last.json alone. Prints, overall and per check family, how many
    accepted pictures were wrongly blocked (overall: anything but PASS; families: a FAIL) and how many rejected ones were
    caught. Use pictures NOT used to tune the tool or the identity; keep the result in the project's calibration log
    (config `calibration_log`)."""
    base = Path(labels_path).resolve().parent
    rows = json.loads(Path(labels_path).read_text(encoding='utf-8'))
    if not isinstance(rows, list):
        raise K.KitError('BAD_LABELS', 'labels.json must be a list')
    bad = [f"{i}: label {r.get('label')!r}" for i, r in enumerate(rows) if r.get('label') not in LABELS]
    bad += [f"{i}: identity missing" for i, r in enumerate(rows) if not r.get('identity')]
    bad += [f"{i}: changed must be a list" for i, r in enumerate(rows) if not isinstance(r.get('changed', []), list)]
    bad += [f"{i}: {k} must be true/false" for i, r in enumerate(rows) for k in ('pose', 'edit') if k in r and not isinstance(r[k], bool)]
    bad += [f"{i}: no file {r.get('img')}" for i, r in enumerate(rows) if not (base / str(r.get('img', ''))).is_file()]
    if bad:
        raise K.KitError('BAD_LABELS', '; '.join(bad[:5]) + f' (labels: {sorted(LABELS)})')
    tol, outline, _ = K.approved_tolerance(kit)
    idents, refs = {}, {}
    tally = {k: {'acc_blocked': 0, 'acc': 0, 'rej_caught': 0, 'rej': 0} for k in ('overall', *FAMILIES)}
    detail = []
    for r in rows:
        iid = r['identity']
        if iid not in idents:
            idents[iid] = K.load_identity(kit, iid)
            refs[iid] = K.load_rgba(idents[iid]['_ref_path'])
        changed = tuple(r.get('changed') or ())
        unknown = [c for c in changed if c not in idents[iid]['parts']]
        if unknown:
            raise K.KitError('UNKNOWN_PART', f'{r["img"]}: {unknown} not in {idents[iid]["_path"]} parts')
        clash = [c for c in changed if (idents[iid]['parts'].get(c) or {}).get('fixed')]
        if clash:
            raise K.KitError('FIXED_PART_CHANGED', f'{r["img"]}: {clash} are fixed parts')
        edit = r['edit'] if 'edit' in r else bool(changed)
        verdict, items = judge(refs[iid], K.load_rgba(base / r['img']), idents[iid], tol, outline['luma_max'], changed, None, edit,
                               r.get('pose', False))
        is_acc = LABELS[r['label']]
        for fam in ('overall', *FAMILIES):
            its = items if fam == 'overall' else [i for i in items if FAMILIES[fam](i['item'])]
            blocked = verdict != 'PASS' if fam == 'overall' else any(i['verdict'] == 'FAIL' for i in its)
            t = tally[fam]
            t['acc' if is_acc else 'rej'] += 1
            if blocked:
                t['acc_blocked' if is_acc else 'rej_caught'] += 1
        detail.append({'img': r['img'], 'label': r['label'], 'verdict': verdict,
                       'blocking': [i['item'] for i in items if i['verdict'] in ('FAIL', 'NOT_ASSESSED')][:4]})
    if not quiet:
        for fam, t in tally.items():
            print(f"{fam:13} accepted wrongly blocked {t['acc_blocked']}/{t['acc']}   rejected caught {t['rej_caught']}/{t['rej']}")
    result = {'tally': tally, 'detail': detail}
    if out:
        Path(out).write_text(json.dumps(result, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    return result


# ---------- selftest ----------

def _figure(size=1200, hat='#0047BB', shift=(0, 0), leg_w=1.0, line=12, haze=False, blob=False, outline='#331616'):
    """white body, blue hat, two legs, dark outline on a transparent canvas"""
    from PIL import ImageDraw
    im = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    s = size / 1200
    ox, oy = shift
    body = [380 * s + ox, 380 * s + oy, 820 * s + ox, 920 * s + oy]
    d.ellipse(body, fill=(250, 250, 250, 255), outline=outline, width=int(line * s))
    d.rectangle([430 * s + ox, 260 * s + oy, 770 * s + ox, 430 * s + oy], fill=hat, outline=outline, width=int(line * s))
    lw = 90 * s * leg_w
    for cx in (520, 680):
        d.rectangle([cx * s - lw / 2 + ox, 910 * s + oy, cx * s + lw / 2 + ox, 1060 * s + oy], fill=(240, 150, 60, 255),
                    outline=outline, width=int(line * s))
    if blob:
        d.ellipse([100 * s, 100 * s, 260 * s, 260 * s], fill=(200, 30, 30, 255))
    if haze:
        d.rectangle([900 * s, 900 * s, 1150 * s, 1150 * s], fill=(255, 255, 255, 20))
    return im


def _cel(size=600):
    """cel-shaded figure in similar warm paints, each with a darker shade tone: fur, hat, cream belly, small buckle"""
    from PIL import ImageDraw
    im = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    line = '#2A1208'
    d.ellipse([150, 200, 450, 540], fill=(250, 140, 34, 255), outline=line, width=8)             # fur
    d.pieslice([150, 200, 450, 540], 20, 120, fill=(222, 108, 22, 255), outline=line, width=8)   # fur shade
    d.ellipse([230, 330, 370, 500], fill=(255, 228, 170, 255), outline=line, width=6)            # cream
    d.chord([230, 330, 370, 500], 20, 160, fill=(240, 204, 150, 255), outline=line, width=6)     # cream shade
    d.rectangle([190, 120, 410, 230], fill=(236, 172, 100, 255), outline=line, width=8)          # hat
    d.rectangle([300, 128, 402, 222], fill=(214, 146, 78, 255))                                  # hat shade
    d.rectangle([280, 400, 320, 430], fill=(240, 192, 64, 255), outline=line, width=4)           # buckle
    return im


def selftest():
    td = Path(tempfile.mkdtemp(prefix='measure-selftest-'))
    root = td / 'proj'
    (root / '.ai/2d-art').mkdir(parents=True)
    (root / 'docs/art/identities').mkdir(parents=True)
    (root / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'spec': 'docs/art/spec.md',
                                                             'identities_dir': 'docs/art/identities', 'work_dir': 'out'}))
    spec = root / 'docs/art/spec.md'
    spec.write_text('# s\n- 상태: 승인\n- 승인: 2026-01-01 — "ok"\n\n```json 2d-measure\n{"outline": {"luma_max": 60}, "tolerance": {}}\n```\n',
                    encoding='utf-8')
    spec.with_suffix('.approval.json').write_text(json.dumps({'schema': 'art-spec-approval/1', 'revision': '', 'sha256': K.sha256(spec)}))
    ref = root / 'ref.png'
    _figure().save(ref)
    (root / 'docs/art/identities/hero.json').write_text(json.dumps({
        'schema': K.IDENTITY_SCHEMA, 'id': 'hero', 'ref': 'ref.png', 'ref_sha256': K.sha256(ref), 'approved': '2026-01-01 — "ok"', 'width_part': 'hat',
        'outline_exclude': ['hat'],
        'parts': {'hat': {'hex': '#0047BB'}, 'body': {'hex': '#FAFAFA', 'lab_tol': 6},
                  'leg_l': {'hex': '#F0963C', 'box_rel': [0, 0.5, 0.5, 1]}, 'leg_r': {'hex': '#F0963C', 'box_rel': [0.5, 0.5, 1, 1]}}}))
    kit = K.load_kit(root)
    cases = []

    def run(label, img, want, **kw):
        p = td / f'{label}.png'
        img.save(p)
        try:
            rc = compare(kit, 'hero', [p], quiet=True, **kw)
        except K.KitError as e:
            rc = 2
            got = e.code
        else:
            got = K.read_record(kit, K.sha256(p))['verdict']
        ok = (got == want) if isinstance(want, str) else (rc == want)
        cases.append(ok)
        print(f"{'PASS' if ok else 'FAIL'} {label}: got {got if isinstance(want, str) else rc}, want {want}")

    run('same', _figure(), 'PASS')
    run('1.5x resolution', _figure(1800), 'PASS')
    pad = Image.new('RGBA', (1400, 1400), (0, 0, 0, 0))
    pad.paste(_figure(), (100, 100))
    run('edit on a padded canvas (aligned by figure bbox)', pad, 'PASS', changed=('leg_l',))
    run('moved figure', _figure(shift=(-120, -60)), 'PASS')
    run('hat colour drift (navy)', _figure(hat='#1E2C6E'), 'FAIL')
    run('outline black and 2x stroke', _figure(line=24, outline='#000000'), 'FAIL')
    run('legs 30% thinner (colour same)', _figure(leg_w=0.7), 'FAIL')
    run('legs 30% thinner but changed', _figure(leg_w=0.7), 'PASS', changed=('leg_l', 'leg_r'))
    run('edit: blob outside changed region', _figure(blob=True), 'FAIL', changed=('leg_l',))
    run('edit: change only inside region', _figure(leg_w=0.85), 'PASS', changed=('leg_l', 'leg_r'))
    run('haze left by background removal', _figure(haze=True), 'FAIL')
    from PIL import ImageDraw as _D
    held = _figure()
    _D.Draw(held).rectangle([60, 520, 260, 700], fill='#0047BB')       # something hat-coloured drawn inside the changed box
    run('edit: hat-coloured thing inside the changed box only', held, 'PASS', box_rel=(0.04, 0.42, 0.23, 0.6))
    wide = Image.new('RGBA', (1400, 1400), (0, 0, 0, 0))
    wide.paste(_figure(leg_w=1.2), (100, 100))
    run('padded canvas + wider legs declared changed', wide, 'PASS', changed=('leg_l', 'leg_r'))
    run('pose change without any fixed part: NOT_ASSESSED, not colour-only PASS', _figure(leg_w=0.7), 'NOT_ASSESSED', pose=True, edit=False)
    run('pose change: hat colour drift still FAIL', _figure(hat='#2A5FC8'), 'FAIL', pose=True, edit=False)
    fx = json.loads((root / 'docs/art/identities/hero.json').read_text())
    fx['parts']['hat']['fixed'] = True
    big_hat = _figure()
    from PIL import ImageDraw as _D2
    _D2.Draw(big_hat).rectangle([400, 200, 800, 430], fill='#0047BB', outline='#331616', width=12)
    ov, _ = judge(K.load_rgba(ref), np.asarray(big_hat, dtype=np.float32), fx, dict(K.DEFAULT_TOL), 60, pose=True)
    cases.append(ov == 'FAIL')
    print(f"{'PASS' if cases[-1] else 'FAIL'} pose change: a fixed part (hat) that grew still FAILs: got {ov}")
    ov, _ = judge(K.load_rgba(ref), np.asarray(_figure(leg_w=0.7), dtype=np.float32), fx, dict(K.DEFAULT_TOL), 60, pose=True)
    cases.append(ov == 'PASS')
    print(f"{'PASS' if cases[-1] else 'FAIL'} pose change: non-fixed legs thinner still PASS: got {ov}")
    tall_hat = _figure()
    _D2.Draw(tall_hat).rectangle([430, 236, 770, 430], fill='#0047BB', outline='#331616', width=12)      # hat area +15%
    th = np.asarray(tall_hat, dtype=np.float32)
    ov, its = judge(K.load_rgba(ref), th, fx, dict(K.DEFAULT_TOL), 60, pose=True)
    hat_area = next(i for i in its if i['item'] == 'part:hat:area')
    cases.append(ov == 'PASS' and hat_area['verdict'] == 'INFO' and hat_area['tol'] == 0.30)
    print(f"{'PASS' if cases[-1] else 'FAIL'} pose change: a fixed part redrawn 15% larger is shown (INFO), not blocked: got {ov}, hat area {hat_area['verdict']} {hat_area.get('note')}")
    ov, _ = judge(K.load_rgba(ref), th, fx, dict(K.DEFAULT_TOL), 60)
    cases.append(ov == 'FAIL')
    print(f"{'PASS' if cases[-1] else 'FAIL'} without --pose the same picture FAILs (fixed parts keep part_area): got {ov}")
    ov, _ = judge(K.load_rgba(ref), th, fx, dict(K.DEFAULT_TOL, pose_area=0.10), 60, pose=True)
    fx2 = json.loads(json.dumps(fx))
    fx2['parts']['hat']['pose_area'] = 0.10
    ov2, _ = judge(K.load_rgba(ref), th, fx2, dict(K.DEFAULT_TOL), 60, pose=True)
    cases.append(ov == 'FAIL' and ov2 == 'FAIL')
    print(f"{'PASS' if cases[-1] else 'FAIL'} pose_area 0.10 (spec tolerance, or on the identity part) blocks it again: got {ov}, {ov2}")
    hid = json.loads(json.dumps(fx))
    hid['parts']['leg_l']['occludable'] = hid['parts']['leg_r']['occludable'] = True
    legless = _figure()
    _D2.Draw(legless).rectangle([400, 925, 800, 1100], fill=(0, 0, 0, 0))
    la = np.asarray(legless, dtype=np.float32)
    ov, ov_hid = judge(K.load_rgba(ref), la, fx, dict(K.DEFAULT_TOL), 60, pose=True)[0], judge(K.load_rgba(ref), la, hid, dict(K.DEFAULT_TOL), 60, pose=True)[0]
    cases.append(ov == 'NOT_ASSESSED' and ov_hid == 'PASS' and judge(K.load_rgba(ref), la, hid, dict(K.DEFAULT_TOL), 60)[0] != 'PASS')
    print(f"{'PASS' if cases[-1] else 'FAIL'} pose change: parts not seen are NOT_ASSESSED, INFO only if the identity marks them occludable (and only with --pose): got {ov}, {ov_hid}")
    # cel-shaded art with similar paints: the reference must pass against itself, whatever the hex seeds say
    cel = root / 'cel.png'
    _cel().save(cel)
    cel_id = {'schema': K.IDENTITY_SCHEMA, 'id': 'cel', 'ref': 'cel.png', 'ref_sha256': K.sha256(cel), 'approved': '2026-01-01 — "ok"',
              'width_part': 'hat', 'parts': {'fur': {'hex': '#F58A24'}, 'hat': {'hex': '#E8A860'}, 'cream': {'hex': '#FFE2A8'},
                                             'buckle': {'hex': '#F0C040', 'fixed': True}}}
    ca = K.load_rgba(cel)
    tol0 = dict(K.DEFAULT_TOL)
    loose = json.loads(json.dumps(cel_id))
    for v in loose['parts'].values():
        v['lab_tol'] = 30                                                   # what every part got before the cap
    by_hex = stats(ca, loose, tol0, 60)
    by_mean = stats(ca, loose, tol0, 60, {n: v['lab'] for n, v in by_hex['parts'].items() if v['lab'] is not None})
    moved = max(abs(by_hex['parts'][n]['px'] - by_mean['parts'][n]['px']) / max(by_hex['parts'][n]['px'], 1) for n in cel_id['parts'])
    ov, ov_loose = judge(ca, ca, cel_id, tol0, 60)[0], judge(ca, ca, loose, tol0, 60)[0]
    cases.append(ov == 'PASS' and ov_loose == 'PASS' and moved > 0.05)
    print(f"{'PASS' if cases[-1] else 'FAIL'} cel-shaded reference against itself PASSes, also with lab_tol 30 on every part, where masks cut from the hex "
          f"and from the measured mean differ by {moved:.0%} (reference and candidate are cut by one rule now): got {ov}, {ov_loose}")
    lt = part_lab_tols(cel_id, tol0, part_seeds(cel_id))
    cases.append(all(v < 30 for v in lt.values()) and lt['fur'] >= 4)
    print(f"{'PASS' if cases[-1] else 'FAIL'} default lab_tol capped at half the dE to the nearest part colour: {({k: round(v, 1) for k, v in lt.items()})}")
    idd = root / 'docs/art/identities'
    (idd / 'cel.json').write_text(json.dumps(cel_id))
    cases.append(identity(kit, 'cel', quiet=True) == 0)
    print(f"{'PASS' if cases[-1] else 'FAIL'} identity check of it: 0 (reference passes, masks apart)")
    wide = json.loads(json.dumps(cel_id))
    for n in ('fur', 'hat'):
        wide['parts'][n]['lab_tol'] = 40
    (idd / 'cel.json').write_text(json.dumps(wide))
    cases.append(identity(kit, 'cel', quiet=True) == 1)
    print(f"{'PASS' if cases[-1] else 'FAIL'} identity check with lab_tol 40 written on two similar parts: 1 (their masks overlap)")
    idf_ = root / 'docs/art/identities/hero.json'
    keep = idf_.read_text()
    idf_.write_text(json.dumps(fx))
    try:
        fp = td / 'fixed_probe.png'
        _figure().save(fp)
        compare(kit, 'hero', [fp], changed=('hat',), quiet=True, pose=True)
        ok_ = False
    except K.KitError as e:
        ok_ = e.code == 'FIXED_PART_CHANGED'
    idf_.write_text(keep)
    cases.append(ok_)
    print(f"{'PASS' if ok_ else 'FAIL'} a fixed part cannot be declared --changed")
    lab = td / 'labels.json'
    a1, r1 = td / 'cal_accept.png', td / 'cal_reject.png'
    _figure(shift=(0, -8)).save(a1)
    _figure(hat='#1E2C6E').save(r1)
    lab.write_text(json.dumps([{'img': str(a1), 'identity': 'hero', 'label': 'accept'}, {'img': str(r1), 'identity': 'hero', 'label': '거절'}]))
    before = sorted(p.name for p in kit.measure_dir.glob('*'))
    last_before = (kit.measure_dir / 'last.json').read_text() if (kit.measure_dir / 'last.json').is_file() else None
    t = calibrate(kit, lab, quiet=True)['tally']['overall']
    cases.append(t == {'acc_blocked': 0, 'acc': 1, 'rej_caught': 1, 'rej': 1})
    print(f"{'PASS' if cases[-1] else 'FAIL'} calibrate tallies accepted/rejected: {t}")
    after_last = (kit.measure_dir / 'last.json').read_text() if (kit.measure_dir / 'last.json').is_file() else None
    cases.append(sorted(p.name for p in kit.measure_dir.glob('*')) == before and after_last == last_before)
    print(f"{'PASS' if cases[-1] else 'FAIL'} calibrate writes no measure record and leaves last.json alone")
    for bad_row, code in (({'img': str(a1), 'identity': 'hero', 'label': 'Accept'}, 'BAD_LABELS'),
                          ({'img': str(a1), 'label': 'accept'}, 'BAD_LABELS'),
                          ({'img': str(a1), 'identity': 'hero', 'label': 'accept', 'pose': 'false'}, 'BAD_LABELS'),
                          ({'img': str(a1), 'identity': 'hero', 'label': 'accept', 'changed': ['bely']}, 'UNKNOWN_PART')):
        lab.write_text(json.dumps([bad_row]))
        try:
            calibrate(kit, lab, quiet=True)
            got = 'no error'
        except K.KitError as e:
            got = e.code
        cases.append(got == code)
        print(f"{'PASS' if got == code else 'FAIL'} calibrate refuses {list(bad_row)[-1]}={bad_row[list(bad_row)[-1]]!r}: {got}")
    run('every part changed: no shape judged', _figure(leg_w=0.6), 'NOT_ASSESSED', changed=('hat', 'body', 'leg_l', 'leg_r'), edit=False)
    soft = _figure()
    sa = np.asarray(soft).copy()
    edge = (sa[..., 3] == 255) & (np.roll(sa[..., 3], 1, axis=1) == 0)
    sa[edge, 3] = 30                                                    # soft hand-drawn edge in the reference itself
    Image.fromarray(sa, 'RGBA').save(ref)
    (root / 'docs/art/identities/hero.json').write_text((root / 'docs/art/identities/hero.json').read_text().replace(
        json.loads((root / 'docs/art/identities/hero.json').read_text())['ref_sha256'], K.sha256(ref)))
    run('reference with soft edges against itself', Image.fromarray(sa, 'RGBA'), 'PASS')
    blank = Image.new('RGBA', (1200, 1200), (0, 0, 0, 0))
    from PIL import ImageDraw
    ImageDraw.Draw(blank).ellipse([300, 300, 900, 900], fill=(250, 250, 250, 255), outline='#331616', width=12)
    run('no hat at all', blank, 'NOT_ASSESSED')
    # check: PASS-only batch passes, then goes stale when the identity changes
    p = td / 'fresh.png'
    _figure(shift=(0, -20)).save(p)
    compare(kit, 'hero', [p], quiet=True)
    cases.append(check(kit) == 0)
    print(f"{'PASS' if cases[-1] else 'FAIL'} check after a PASS-only compare: 0")
    idf = root / 'docs/art/identities/hero.json'
    ident_text = idf.read_text()
    idf.write_text(ident_text.replace('"lab_tol": 6', '"lab_tol": 7'))
    cases.append(check(kit) == 1)
    print(f"{'PASS' if cases[-1] else 'FAIL'} check after the identity changed: 1 (stale)")
    idf.write_text(json.dumps(dict(json.loads(ident_text), parts={})))
    run('identity without parts', _figure(), 2)
    idf.write_text(ident_text)
    good_spec = spec.read_text(encoding='utf-8')
    spec.write_text(good_spec.replace('- 승인: 2026-01-01 — "ok"\n', ''), encoding='utf-8')
    spec.with_suffix('.approval.json').write_text(json.dumps({'schema': 'art-spec-approval/1', 'revision': '', 'sha256': K.sha256(spec)}))
    run('status line but no dated approval line (approve.py rule)', _figure(), 2)
    spec.write_text(good_spec.replace('상태: 승인', '상태: 초안'), encoding='utf-8')
    run('spec not approved', _figure(), 2)
    try:
        compare(kit, 'no_such_id', [p], quiet=True)
    except K.KitError:
        pass
    cases.append(check(kit) == 2)
    print(f"{'PASS' if cases[-1] else 'FAIL'} check after a compare aborted on a bad id: 2")
    spec.write_text(good_spec, encoding='utf-8')
    spec.with_suffix('.approval.json').write_text(json.dumps({'schema': 'art-spec-approval/1', 'revision': '', 'sha256': K.sha256(spec)}))
    (kit.measure_dir / 'last.json').write_text('[]')
    cases.append(check(kit) == 2)
    print(f"{'PASS' if cases[-1] else 'FAIL'} check with an empty batch: 2")
    bad = cases.count(False)
    print(f"measure selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    c = sub.add_parser('compare')
    c.add_argument('--id', required=True)
    c.add_argument('--img', nargs='+', required=True)
    c.add_argument('--changed', default='')
    c.add_argument('--changed-box')
    c.add_argument('--pose', action='store_true', help='the pose changed: part size/offset are shown, not judged; fixed-part area fails only beyond pose_area')
    g = c.add_mutually_exclusive_group()
    g.add_argument('--edit', action='store_true', default=None)
    g.add_argument('--new', dest='edit', action='store_false')
    cb = sub.add_parser('calibrate')
    cb.add_argument('--labels', required=True)
    cb.add_argument('--out')
    sub.add_parser('check')
    idp = sub.add_parser('identity')
    idp.add_argument('--id', required=True)
    sub.add_parser('selftest')
    a = ap.parse_args()
    if a.cmd == 'selftest':
        return selftest()
    try:
        kit = K.load_kit()
        if a.cmd == 'check':
            return check(kit)
        if a.cmd == 'identity':
            return identity(kit, a.id)
        if a.cmd == 'calibrate':
            calibrate(kit, a.labels, a.out)
            return 0
        changed = tuple(x for x in a.changed.split(',') if x)
        return compare(kit, a.id, a.img, changed, parse_box(a.changed_box), a.edit, pose=a.pose)
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
