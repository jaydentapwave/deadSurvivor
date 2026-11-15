#!/usr/bin/env python3
"""Frame tools for 2D sprite animation (2d-animation rules): clean, premultiplied transform, align, jitter and hole judgement.

  frames.py clean <png>... --out <dir> [--alpha-min 48]        drop semi-transparent haze (alpha < alpha-min -> 0)
  frames.py transform <in> <out> [--dx --dy --rot --sx --sy --pivot x,y]
                                                               move/rotate/squash in premultiplied alpha (no dark fringe)
  frames.py align --id <identity> <frames>... --out <dir> [--part <name>] [--to <frame>]
                  [--scale-by <part>[,<part>...]] [--baseline [<y>]] [--lift <frame stem>=<px> ...]
                                                               translate each frame so the part (default: identity
                                                               width_part) sits where it is in --to (default: first frame)
                                                               --scale-by: newly drawn poses come out at different
                                                               scales. Each frame is first scaled so the AREA of the
                                                               named part(s) — identity parts that do not change with
                                                               the pose (ear, hat) — equals that in --to. Area, not
                                                               width: a turned head changes the width. Per frame a part
                                                               that is not found, or whose scale disagrees with the
                                                               others by more than 10% (half covered by an arm), is
                                                               left out and printed; no usable part = the frame is
                                                               skipped and the exit is 1.
                                                               --baseline: feet on a common ground line instead of the
                                                               part at a common height: the lowest solid row goes to
                                                               row <y>, default the feet row of --to. --lift raises
                                                               single frames above it (airborne frames).
                                                               With either option every frame is written on the canvas
                                                               of --to, grown on every side a figure would not fit
                                                               (printed; all frames get the same canvas).
  frames.py jitter --id <identity> <frames>... [--moving part,part] [--fixed part,part]
                                                               judge a frame set: per part, area/width/height spread
                                                               (max-min)/mean <= jitter_max and colour spread dE76 <= de76;
                                                               small pixel art: tolerance jitter_px N also accepts a
                                                               width/height difference of N px (area: N rows/columns);
                                                               --fixed parts (eyes: identity) use jitter_max/3; --moving
                                                               parts (the swinging arm) are judged on colour only.
                                                               Writes a record per frame for this exact set
                                                               (sheet.py grid of the same frames reads it). jitter does
                                                               not compare with the reference: every frame also needs
                                                               measure.py compare (--changed <moving parts>).
  frames.py holes <frames>... [--ref <picture>] [--gaps] [--gap-max 2] [--allow 0] [--alpha-min 128]
                                                               judge each frame against the reference picture (default:
                                                               the first frame): the figure must not fall into more
                                                               pieces, and must not have more hole pixels (transparent
                                                               pixels enclosed by the figure) than the reference + allow.
                                                               This is what a piece moved by whole pixels leaves behind
                                                               (a body raised 1px off its legs); jitter does not see it
                                                               (areas and colours stay the same). Thin gaps (transparent
                                                               runs up to gap-max px between solid pixels in a row or
                                                               column) are printed as INFO with the first position:
                                                               look there enlarged. They are judged only with --gaps,
                                                               because a correct fix can add some (leg tops extended
                                                               upward lengthen the gap between the legs). Needs no
                                                               identity or spec; writes no record.
                                                               Exit 0 PASS / 1 FAIL / 2 input problem.
  frames.py selftest

jitter exit: 0 PASS / 1 FAIL / 3 NOT_ASSESSED / 2 input, config or spec problem. AI video/sprite tools redraw every frame,
so their output goes through jitter too: that is where faces and hats wander."""
import argparse
import json
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402
import measure as M  # noqa: E402


def clean(paths, out, alpha_min=48):
    out.mkdir(parents=True, exist_ok=True)
    for p in paths:
        a = np.asarray(Image.open(p).convert('RGBA')).copy()
        n = int(((a[..., 3] > 0) & (a[..., 3] < alpha_min)).sum())
        a[a[..., 3] < alpha_min] = 0
        Image.fromarray(a, 'RGBA').save(out / Path(p).name)
        print(f'{Path(p).name}: {n} haze px removed')
    return 0


def transform(src, dst, dx=0.0, dy=0.0, rot=0.0, sx=1.0, sy=1.0, pivot=None):
    im = Image.open(src).convert('RGBA').convert('RGBa')
    w, h = im.size
    px, py = pivot or (w / 2, h / 2)
    if sx != 1 or sy != 1:
        im = im.resize((max(1, round(w * sx)), max(1, round(h * sy))), Image.BICUBIC)
        canvas = Image.new('RGBa', (w, h), (0, 0, 0, 0))
        canvas.paste(im, (round(px - px * sx), round(py - py * sy)))
        im = canvas
    if rot:
        im = im.rotate(rot, resample=Image.BICUBIC, center=(px, py))
    if dx or dy:
        canvas = Image.new('RGBa', (w, h), (0, 0, 0, 0))
        canvas.paste(im, (round(dx), round(dy)))
        im = canvas
    im.convert('RGBA').save(dst)
    return 0


def _tolerance_identity(kit, ident_id):
    tol, out, spec = K.approved_tolerance(kit)
    ident = K.load_identity(kit, ident_id)
    ident['_spec_sha256'] = K.sha256(spec)
    return tol, out, ident


def set_key(shas):
    """record kind of one frame set: jitter-<12 hex of the sorted frame hashes>"""
    import hashlib
    return 'jitter-' + hashlib.sha256('\n'.join(sorted(shas)).encode()).hexdigest()[:12]


SCALE_DISAGREE = 0.10


def _scale_for(ref_parts, parts, names, tol):
    """(scale, notes): scale that brings the named parts' area to the reference's. Parts not found, or whose scale is
    more than 10% from the others (occluded, mis-read), are left out. None if nothing usable."""
    found = {}
    for n in names:
        rp, cp = ref_parts[n]['px'], parts[n]['px']
        if rp and cp and cp >= tol['min_part_px'] * rp * 0.05:           # 0.05: frames may be far smaller than the reference
            found[n] = float(np.sqrt(rp / cp))
    notes = [f'{n} not found' for n in names if n not in found]
    if not found:
        return None, notes
    if len(found) == 2:
        (a, ra), (b, rb) = found.items()
        if abs(ra - rb) / max(ra, rb) > SCALE_DISAGREE:                  # two readings disagree: the larger part is the steadier one
            drop = a if ref_parts[a]['px'] < ref_parts[b]['px'] else b
            notes.append(f'{drop} x{found.pop(drop):.3f} left out (disagrees with the larger part)')
    elif len(found) > 2:
        med = float(np.median(list(found.values())))
        for n in [n for n, r in found.items() if abs(r - med) / med > SCALE_DISAGREE]:
            notes.append(f'{n} x{found.pop(n):.3f} left out (more than 10% from the others)')
    return float(np.median(list(found.values()))), notes + [f'{n} x{r:.3f}' for n, r in found.items()]


def align(kit, ident_id, frames, out, part=None, to=None, scale_by=(), baseline=None, lift=None):
    tol, outline, ident = _tolerance_identity(kit, ident_id)
    part = part or ident.get('width_part')
    unknown = [n for n in (part, *scale_by) if n not in ident['parts']]
    if unknown:
        raise K.KitError('UNKNOWN_PART', f'{unknown} not in {ident["_path"]}')
    ref = K.load_rgba(ident['_ref_path'])
    seeds = {n: p['lab'] for n, p in M.stats(ref, ident, tol, outline['luma_max'])['parts'].items() if p['lab'] is not None}

    def parts_of(arr):
        return M.stats(arr, ident, tol, outline['luma_max'], seeds)['parts']

    def centre(p):
        c = parts_of(K.load_rgba(p))[part].get('c')
        if c is None:
            raise K.KitError('PART_NOT_FOUND', f'{part} not found in {p}')
        return c
    out.mkdir(parents=True, exist_ok=True)
    if not scale_by and baseline is None:
        target = centre(to or frames[0])
        for p in frames:
            c = centre(p)
            dx, dy = round(target[0] - c[0]), round(target[1] - c[1])
            transform(p, out / Path(p).name, dx, dy)
            res = np.subtract(target, centre(out / Path(p).name))
            print(f'{Path(p).name}: moved ({dx:+d},{dy:+d}) residual {float(np.hypot(*res)):.2f}px')
        return 0
    lift = dict(lift or {})
    stray = [k for k in lift if k not in {Path(p).stem for p in frames}]
    if stray:
        raise K.KitError('UNKNOWN_FRAME', f'--lift names no given frame: {stray}')
    goal = K.load_rgba(to or frames[0])
    goal_parts = parts_of(goal)
    if goal_parts[part]['c'] is None or (K.bbox(goal[..., 3] >= K.ALPHA_SOLID) is None):
        raise K.KitError('PART_NOT_FOUND', f'{part} not found in {to or frames[0]}')
    gh, gw = goal.shape[:2]
    ground = None if baseline is None else (K.bbox(goal[..., 3] >= K.ALPHA_SOLID)[3] if baseline < 0 else int(baseline))
    skipped, placed = 0, []
    for p in frames:
        arr = K.load_rgba(p)
        scale, notes = 1.0, []
        if scale_by:
            scale, notes = _scale_for(goal_parts, parts_of(arr), scale_by, tol)
            if scale is None:
                print(f'SKIP {Path(p).name}: no usable part to scale by ({"; ".join(notes)})')
                skipped += 1
                continue
        im = Image.open(p).convert('RGBA').convert('RGBa')
        if abs(scale - 1) > 1e-4:
            im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
        scaled = np.asarray(im.convert('RGBA'), dtype=np.float32)
        c = parts_of(scaled)[part]['c']
        box = K.bbox(scaled[..., 3] >= K.ALPHA_SOLID)
        if c is None or box is None:
            print(f'SKIP {Path(p).name}: {part} not found')
            skipped += 1
            continue
        dx = round(goal_parts[part]['c'][0] - c[0])
        dy = round(goal_parts[part]['c'][1] - c[1]) if ground is None else ground - box[3] - int(lift.get(Path(p).stem, 0))
        placed.append((p, im, dx, dy, box, scale, notes))
    if placed:
        # one canvas for all: the canvas of --to, grown where a figure would be cut off
        left = max(0, -min(b[0] + dx for _, _, dx, _, b, _, _ in placed))
        top = max(0, -min(b[1] + dy for _, _, _, dy, b, _, _ in placed))
        right = max(0, max(b[2] + dx for _, _, dx, _, b, _, _ in placed) - gw)
        bottom = max(0, max(b[3] + dy for _, _, _, dy, b, _, _ in placed) - gh)
        size = (gw + left + right, gh + top + bottom)
        if size != (gw, gh):
            print(f'CANVAS {gw}x{gh} (of --to) grown to {size[0]}x{size[1]}: left {left}, top {top}, right {right}, bottom {bottom} px — a figure did not fit')
        for p, im, dx, dy, box, scale, notes in placed:
            canvas = Image.new('RGBa', size, (0, 0, 0, 0))
            canvas.paste(im, (dx + left, dy + top))
            canvas.convert('RGBA').save(out / Path(p).name)
            print(f'{Path(p).name}: scale {scale:.4f}' + (f' ({"; ".join(notes)})' if notes else '') + f', figure height {box[3] - box[1]}px'
                  + (f', feet on row {box[3] + dy + top}' if ground is not None else ''))
    return 1 if skipped else 0


def jitter(kit, ident_id, frames, moving=(), fixed=(), quiet=False):
    tol, outline, ident = _tolerance_identity(kit, ident_id)
    names = list(ident.get('parts') or {})
    bad = [n for n in (*moving, *fixed) if n not in names]
    if bad:
        raise K.KitError('UNKNOWN_PART', f'{bad} not in {ident["_path"]}')
    if len(frames) < 2:
        raise K.KitError('TOO_FEW_FRAMES', 'jitter needs 2 or more frames')
    ref = K.load_rgba(ident['_ref_path'])
    seeds = {n: p['lab'] for n, p in M.stats(ref, ident, tol, outline['luma_max'])['parts'].items() if p['lab'] is not None}
    st = [M.stats(K.load_rgba(p), ident, tol, outline['luma_max'], seeds)['parts'] for p in frames]
    items = []
    for n in names:
        rows = [s[n] for s in st]
        if any(r['lab'] is None for r in rows):
            items.append(M.item(f'{n}', None, None, None, 'NOT_ASSESSED', 'missing in ' + ', '.join(
                Path(frames[i]).name for i, r in enumerate(rows) if r['lab'] is None)[:120]))
            continue
        labs = np.array([r['lab'] for r in rows])
        spread = max(K.de76(a, b) for a in labs for b in labs)
        items.append(M.item(f'{n}:color', 0.0, spread, tol['de76'], 'PASS' if spread <= tol['de76'] else 'FAIL'))
        if n in moving:
            continue
        lim = tol['jitter_max'] / 3 if n in fixed else tol['jitter_max']
        # Small pixel art: one pixel is a large share of a part (a 1px longer leg on a 20px figure is +3.6% area),
        # so a ratio alone judges it backwards. jitter_px (2d-measure tolerance, default 0 = off) is an absolute
        # allowance for parts that are not --fixed: width/height may differ by jitter_px pixels, area by jitter_px
        # rows or columns of the part (jitter_px x its longer side).
        slack = 0 if n in fixed else float(tol.get('jitter_px') or 0)
        side = max(float(np.median([r['w'] for r in rows])), float(np.median([r['h'] for r in rows])))
        for key in ('px', 'w', 'h'):
            v = np.array([r[key] for r in rows], dtype=float)
            diff = float(v.max() - v.min())
            sp = float(diff / max(v.mean(), 1))
            ok = sp <= lim or (slack > 0 and diff <= (slack * side if key == 'px' else slack))
            worst = Path(frames[int(np.argmax(np.abs(v - np.median(v))))]).name
            note = '' if sp <= lim else f'within jitter_px {slack:g} ({diff:g}px)' if ok else f'worst {worst}'
            items.append(M.item(f'{n}:{"area" if key == "px" else key}', float(np.median(v)), sp, lim,
                                'PASS' if ok else 'FAIL', note))
    if not any(i['verdict'] != 'NOT_ASSESSED' and not i['item'].endswith(':color') for i in items):
        items.append(M.item('parts', None, None, None, 'NOT_ASSESSED', 'no part shape judged (no parts, or all moving)'))
    verdicts = {i['verdict'] for i in items}
    overall = 'FAIL' if 'FAIL' in verdicts else 'NOT_ASSESSED' if 'NOT_ASSESSED' in verdicts else 'PASS'
    shas = [K.sha256(p) for p in frames]
    kind = set_key(shas)
    for p, sha in zip(frames, shas):
        K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': kind, 'image': str(Path(p).resolve()), 'sha256': sha,
                             'identity': ident_id, 'ref_sha256': ident['ref_sha256'], 'identity_sha256': ident['_sha256'],
                             'spec_sha256': ident['_spec_sha256'], 'set': shas, 'moving': list(moving), 'fixed': list(fixed),
                             'verdict': overall, 'items': items})
    if not quiet:
        print(f'{overall} {len(frames)} frames')
        for i in items:
            print(f"  {i['verdict']:12} {i['item']:20} value {i['value']} tol {i['tol']}" + (f"  {i['note']}" if i.get('note') else ''))
    return 1 if overall == 'FAIL' else 3 if overall == 'NOT_ASSESSED' else 0


def _spread(reach, free):
    """reach grown along rows: every free run that holds a reached pixel becomes reached"""
    h, w = free.shape
    ids = np.cumsum(np.diff(free.astype(np.int8), axis=1, prepend=0) != 0, axis=1) + (np.arange(h) * (w + 1))[:, None]
    hit = np.bincount(ids.ravel(), weights=(reach & free).ravel(), minlength=h * (w + 1)) > 0
    return hit[ids] & free


def _flood(seed, free, diagonal=False):
    """pixels of `free` connected to `seed` (4-connected; 8-connected with diagonal)"""
    reach = seed & free
    while True:
        n = int(reach.sum())
        reach = _spread(reach, free)
        reach = _spread(reach.T, free.T).T
        if diagonal:
            g = reach.copy()
            g[1:, 1:] |= reach[:-1, :-1]
            g[:-1, :-1] |= reach[1:, 1:]
            g[1:, :-1] |= reach[:-1, 1:]
            g[:-1, 1:] |= reach[1:, :-1]
            reach = g & free
        if int(reach.sum()) == n:
            return reach


def hole_stats(path, gap_max=2, alpha_min=K.ALPHA_SOLID, piece_cap=64):
    """{'holes', 'gaps', 'pieces', 'gap_at'} of one picture. holes = transparent pixels enclosed by solid ones;
    gaps = transparent runs of 1..gap_max px between solid pixels in a row or column, not enclosed (pixels counted
    once); pieces = 8-connected solid parts (counted up to piece_cap); gap_mask for locating new gaps."""
    solid = K.load_rgba(path)[..., 3] >= alpha_min
    if not solid.any():
        raise K.KitError('EMPTY_IMAGE', f'{path}: no solid pixel (alpha >= {alpha_min})')
    clear = np.pad(~solid, 1, constant_values=True)
    border = np.zeros_like(clear)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    enclosed = (clear & ~_flood(border, clear))[1:-1, 1:-1]
    gaps = np.zeros_like(solid)
    for m, g in ((solid, gaps), (solid.T, gaps.T)):              # along x, then along y (views of the same array)
        for n in range(1, int(gap_max) + 1):
            if m.shape[1] < n + 2:
                break
            run = m[:, :-n - 1] & m[:, n + 1:]
            for k in range(1, n + 1):
                run = run & ~m[:, k:m.shape[1] - n - 1 + k]
            for k in range(1, n + 1):
                g[:, k:m.shape[1] - n - 1 + k] |= run
    gaps &= ~enclosed
    left, pieces = solid.copy(), 0
    while left.any() and pieces < piece_cap:
        seed = np.zeros_like(left)
        seed[tuple(np.argwhere(left)[0])] = True
        left &= ~_flood(seed, left, diagonal=True)
        pieces += 1
    return {'holes': int(enclosed.sum()), 'gaps': int(gaps.sum()), 'pieces': pieces, 'gap_mask': gaps}


def holes(frames, ref=None, gaps=False, gap_max=2, allow=0, alpha_min=K.ALPHA_SOLID, quiet=False):
    base = hole_stats(ref or frames[0], gap_max, alpha_min)
    bad = 0
    if not quiet:
        print(f"reference {Path(ref or frames[0]).name}: pieces {base['pieces']}, holes {base['holes']}, thin gaps {base['gaps']}")
    for p in frames:
        st = hole_stats(p, gap_max, alpha_min)
        split, more, wider = st['pieces'] - base['pieces'], st['holes'] - base['holes'], st['gaps'] - base['gaps']
        fail = split > 0 or more > allow or (gaps and wider > allow)
        bad += fail
        if quiet:
            continue
        notes = [f'{split:+d} pieces (the figure came apart)' if split > 0 else '', f'{more:+d} hole px' if more > allow else '']
        if wider > 0:
            new = st['gap_mask'] & ~base['gap_mask'] if st['gap_mask'].shape == base['gap_mask'].shape else st['gap_mask']
            ys, xs = np.nonzero(new if new.any() else st['gap_mask'])
            notes.append(f"{'' if gaps and wider > allow else 'INFO '}{wider:+d} thin gap px (look at x={int(xs[0])}, y={int(ys[0])})")
        print(f"{'FAIL' if fail else 'PASS'} {Path(p).name}: pieces {st['pieces']}, holes {st['holes']}, thin gaps {st['gaps']}"
              + ''.join(f'  {n}' for n in notes if n))
    if not quiet:
        print(f"{'FAIL' if bad else 'PASS'} {len(frames)} frames, {bad} came apart or gained holes")
    return 1 if bad else 0


def _pixel_frame(body_dy=0, leg_top=12, punch=None):
    """20x20 pixel sprite: body block on two legs. body_dy moves the body, leg_top is the legs' first row"""
    a = np.zeros((20, 20, 4), dtype=np.uint8)
    a[4 + body_dy:12 + body_dy, 6:14] = (200, 60, 60, 255)
    for x0 in (7, 11):
        a[leg_top:18, x0:x0 + 2] = (60, 40, 30, 255)
    if punch:
        a[punch[1], punch[0]] = 0
    return Image.fromarray(a, 'RGBA')


def selftest():
    td = Path(tempfile.mkdtemp(prefix='frames-selftest-'))
    root = td / 'proj'
    (root / '.ai/2d-art').mkdir(parents=True)
    (root / 'ids').mkdir()
    (root / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'spec': 'spec.md', 'identities_dir': 'ids', 'work_dir': 'out'}))
    spec = root / 'spec.md'
    spec.write_text('- 상태: 승인\n- 승인: 2026-01-01 — "ok"\n```json 2d-measure\n{"tolerance": {}}\n```\n', encoding='utf-8')
    spec.with_suffix('.approval.json').write_text(json.dumps({'schema': 'art-spec-approval/1', 'revision': '', 'sha256': K.sha256(spec)}))
    ref = root / 'ref.png'
    M._figure().save(ref)
    (root / 'ids/hero.json').write_text(json.dumps({
        'schema': K.IDENTITY_SCHEMA, 'id': 'hero', 'ref': 'ref.png', 'ref_sha256': K.sha256(ref), 'approved': '2026-01-01 — "ok"',
        'width_part': 'hat', 'outline_exclude': ['hat'],
        'parts': {'hat': {'hex': '#0047BB'}, 'leg_l': {'hex': '#F0963C', 'box_rel': [0, 0.5, 0.5, 1]},
                  'leg_r': {'hex': '#F0963C', 'box_rel': [0.5, 0.5, 1, 1]}}}))
    kit = K.load_kit(root)
    cases = []

    def frameset(label, figs):
        d = td / label
        d.mkdir()
        paths = []
        for i, f in enumerate(figs):
            paths.append(d / f'f{i}.png')
            f.save(paths[-1])
        return paths

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    still = frameset('still', [M._figure(shift=(0, dy)) for dy in (0, -6, -12, -6)])
    case('bobbing frames (whole figure moves) PASS', jitter(kit, 'hero', still, quiet=True) == 0)
    one_off = frameset('one_off', [M._figure(), M._figure(), M._figure(leg_w=0.85), M._figure()])
    case('one frame with legs 15% thinner FAIL', jitter(kit, 'hero', one_off, quiet=True) == 1)
    case('same frames, legs declared moving PASS', jitter(kit, 'hero', one_off, moving=('leg_l', 'leg_r'), quiet=True) == 0)
    hat = frameset('hat', [M._figure(), M._figure(), M._figure(hat='#2A5FC8'), M._figure()])
    case('one frame with slight hat colour drift FAIL', jitter(kit, 'hero', hat, quiet=True) == 1)
    # small pixel art: a leg 1px longer is a big ratio. FAIL by ratio alone, PASS once the spec allows jitter_px 1,
    # and a 3px longer leg still FAILs with that allowance.
    from PIL import ImageDraw

    def tiny(leg_h):
        im = Image.new('RGBA', (20, 20), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rectangle([6, 2, 13, 5], fill='#0047BB')
        d.rectangle([6, 6, 13, 11], fill=(250, 250, 250, 255))
        d.rectangle([7, 12, 8, 11 + leg_h], fill='#F0963C')
        d.rectangle([11, 12, 12, 11 + 4], fill='#F0963C')
        return im
    step = frameset('tiny_step', [tiny(4), tiny(5), tiny(4)])
    far = frameset('tiny_far', [tiny(4), tiny(7), tiny(4)])
    case('tiny sprite, one leg 1px longer: FAIL by ratio (jitter_px off)', jitter(kit, 'hero', step, quiet=True) == 1)
    spec_text = spec.read_text(encoding='utf-8')

    def approve(text):
        spec.write_text(text, encoding='utf-8')
        spec.with_suffix('.approval.json').write_text(json.dumps({'schema': 'art-spec-approval/1', 'revision': '', 'sha256': K.sha256(spec)}))
    approve(spec_text.replace('{"tolerance": {}}', '{"tolerance": {"jitter_px": 1}}'))
    case('same frames with jitter_px 1 approved: PASS', jitter(kit, 'hero', step, quiet=True) == 0)
    case('leg 3px longer with jitter_px 1: still FAIL', jitter(kit, 'hero', far, quiet=True) == 1)
    approve(spec_text)
    rec = K.read_record(kit, K.sha256(still[1]), set_key([K.sha256(p) for p in still]))
    case('jitter record written per frame', rec and rec['verdict'] == 'PASS' and len(rec['set']) == 4)
    # clean and premultiplied transform: no dark fringe
    hazy = td / 'hazy.png'
    M._figure(haze=True).save(hazy)
    clean([hazy], td / 'clean')
    a = np.asarray(Image.open(td / 'clean/hazy.png'))
    case('clean removes haze', int(((a[..., 3] > 0) & (a[..., 3] < 48)).sum()) == 0)
    soft = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    soft.paste((255, 255, 255, 255), (16, 16, 48, 48))
    soft.save(td / 'w.png')
    transform(td / 'w.png', td / 'w_rot.png', rot=33)
    b = np.asarray(Image.open(td / 'w_rot.png')).astype(int)
    edge = (b[..., 3] > 0) & (b[..., 3] < 255)
    case('premultiplied rotate keeps edge colour white', edge.any() and b[edge][:, :3].min() >= 250, f'min edge rgb {b[edge][:, :3].min() if edge.any() else None}')
    al = frameset('align', [M._figure(), M._figure(shift=(14, -9))])
    align(kit, 'hero', al, td / 'aligned')
    c0 = M.stats(K.load_rgba(td / 'aligned/f0.png'), json.loads((root / 'ids/hero.json').read_text()), dict(K.DEFAULT_TOL), 60)['parts']['hat']['c']
    c1 = M.stats(K.load_rgba(td / 'aligned/f1.png'), json.loads((root / 'ids/hero.json').read_text()), dict(K.DEFAULT_TOL), 60)['parts']['hat']['c']
    case('align puts the anchor part within 1px', float(np.hypot(c0[0] - c1[0], c0[1] - c1[1])) <= 1.0)
    # newly drawn poses: another scale, another canvas, feet at another height
    big = M._figure()
    small = M._figure(900, shift=(20, -30))
    half_leg = M._figure(1500)
    from PIL import ImageDraw as _ID
    _ID.Draw(half_leg).rectangle([400 * 1.25, 980 * 1.25, 620 * 1.25, 1100 * 1.25], fill=(0, 0, 0, 0))       # left leg half hidden
    sc = frameset('scale', [big, small, half_leg])
    idj = json.loads((root / 'ids/hero.json').read_text())
    rc = align(kit, 'hero', sc, td / 'scaled', scale_by=('hat', 'leg_l'), baseline=-1)
    st_ = [M.stats(K.load_rgba(td / f'scaled/f{i}.png'), idj, dict(K.DEFAULT_TOL), 60) for i in range(3)]
    hat_px = [x['parts']['hat']['px'] for x in st_]
    feet = [K.bbox(K.load_rgba(td / f'scaled/f{i}.png')[..., 3] >= 128)[3] for i in range(3)]
    sizes = {Image.open(td / f'scaled/f{i}.png').size for i in range(3)}
    case('align --scale-by --baseline: frames drawn at 0.75x and 1.25x come out with the same hat area, feet row and canvas',
         rc == 0 and max(hat_px) / min(hat_px) < 1.02 and max(feet) - min(feet) <= 1 and sizes == {(1200, 1200)},
         f'hat px {hat_px}, feet {feet}')
    s_half, notes_half = _scale_for(st_[0]['parts'], M.stats(K.load_rgba(sc[2]), idj, dict(K.DEFAULT_TOL), 60)['parts'], ('hat', 'leg_l'), dict(K.DEFAULT_TOL))
    case('... the half-hidden leg is left out of the scale for that frame (the hat alone decides)', abs(s_half - 0.8) < 0.01
         and any('leg_l' in n and 'left out' in n for n in notes_half), '; '.join(notes_half))
    align(kit, 'hero', sc[:2], td / 'lifted', scale_by=('hat',), baseline=1000, lift={'f1': 40})
    lf = [K.bbox(K.load_rgba(td / f'lifted/f{i}.png')[..., 3] >= 128)[3] for i in range(2)]
    case('--baseline 1000 with --lift f1=40: feet on row 1000 and 960', lf == [1000, 960], str(lf))
    hatless = Image.new('RGBA', (600, 600), (0, 0, 0, 0))
    _ID.Draw(hatless).ellipse([200, 200, 400, 400], fill=(250, 250, 250, 255))
    hatless.save(td / 'hatless.png')
    case('a frame without the part to scale by: skipped, exit 1', align(kit, 'hero', [sc[0], td / 'hatless.png'], td / 'skip', scale_by=('hat',)) == 1
         and not (td / 'skip/hatless.png').is_file())
    # holes: what a piece moved by whole pixels leaves behind
    px = frameset('px', [_pixel_frame(), _pixel_frame(body_dy=-1), _pixel_frame(body_dy=-1, leg_top=11), _pixel_frame(punch=(9, 7))])
    case('pixel sprite, same frame twice: holes PASS', holes([px[0], px[0]], quiet=True) == 0)
    g0, g1, g2 = (hole_stats(p) for p in px[:3])
    case('body raised 1px off its legs (1px gap row): holes FAIL — 3 pieces, 4 new gap px', holes([px[0], px[1]], quiet=True) == 1
         and g1['pieces'] == 3 and g1['gaps'] - g0['gaps'] == 4, f"pieces {g1['pieces']}, gaps {g0['gaps']} -> {g1['gaps']}")
    case('body raised 1px with the leg tops extended: holes PASS', holes([px[0], px[2]], quiet=True) == 0 and g2['pieces'] == 1)
    case('... which --gaps would block (the gap between the legs grew 2px): why thin gaps are INFO by default',
         g2['gaps'] - g0['gaps'] == 2 and holes([px[0], px[2]], gaps=True, quiet=True) == 1)
    case('one pixel punched out of the body: holes FAIL', holes([px[0], px[3]], quiet=True) == 1 and hole_stats(px[3])['holes'] == 1)
    case('--allow 1 accepts that one pixel', holes([px[0], px[3]], allow=1, quiet=True) == 0)
    case('--ref: frames judged against another picture, not the first frame', holes([px[1], px[1]], quiet=True) == 0
         and holes([px[1], px[1]], ref=px[0], quiet=True) == 1)
    whole = np.asarray(M._figure()).copy()
    cut = whole.copy()
    cut[:913] = 0
    cut[:911] = np.maximum(cut[:911], whole[2:913])           # everything above the legs moved up 2px
    split = frameset('split', [Image.fromarray(whole, 'RGBA'), Image.fromarray(cut, 'RGBA')])
    case('counter-example: body lifted 2px off the legs passes jitter', jitter(kit, 'hero', split, quiet=True) == 0)
    case('... and fails holes (the figure came apart)', holes(split, quiet=True) == 1, f"pieces {hole_stats(split[1])['pieces']}")
    bad = cases.count(False)
    print(f"frames selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    c = sub.add_parser('clean')
    c.add_argument('paths', nargs='+')
    c.add_argument('--out', type=Path, required=True)
    c.add_argument('--alpha-min', type=int, default=48)
    t = sub.add_parser('transform')
    t.add_argument('src')
    t.add_argument('dst')
    for k, v in (('dx', 0.0), ('dy', 0.0), ('rot', 0.0), ('sx', 1.0), ('sy', 1.0)):
        t.add_argument(f'--{k}', type=float, default=v)
    t.add_argument('--pivot')
    a_ = sub.add_parser('align')
    a_.add_argument('--id', required=True)
    a_.add_argument('frames', nargs='+')
    a_.add_argument('--out', type=Path, required=True)
    a_.add_argument('--part')
    a_.add_argument('--to')
    a_.add_argument('--scale-by', default='', help='part(s) whose area sets the scale of each frame (fixed identity parts)')
    a_.add_argument('--baseline', type=int, nargs='?', const=-1, help='feet on this row (default: the feet row of --to)')
    a_.add_argument('--lift', nargs='*', default=[], metavar='STEM=PX')
    j = sub.add_parser('jitter')
    j.add_argument('--id', required=True)
    j.add_argument('frames', nargs='+')
    j.add_argument('--moving', default='')
    j.add_argument('--fixed', default='')
    h_ = sub.add_parser('holes')
    h_.add_argument('frames', nargs='+')
    h_.add_argument('--ref')
    h_.add_argument('--gaps', action='store_true')
    h_.add_argument('--gap-max', type=int, default=2)
    h_.add_argument('--allow', type=int, default=0)
    h_.add_argument('--alpha-min', type=int, default=K.ALPHA_SOLID)
    sub.add_parser('selftest')
    a = ap.parse_args()
    if a.cmd == 'selftest':
        return selftest()
    if a.cmd == 'clean':
        return clean(a.paths, a.out, a.alpha_min)
    if a.cmd == 'transform':
        piv = tuple(float(x) for x in a.pivot.split(',')) if a.pivot else None
        return transform(a.src, a.dst, a.dx, a.dy, a.rot, a.sx, a.sy, piv)
    try:
        if a.cmd == 'holes':
            return holes(a.frames, a.ref, a.gaps, a.gap_max, a.allow, a.alpha_min)
        kit = K.load_kit()
        split = lambda s: tuple(x for x in s.split(',') if x)  # noqa: E731
        if a.cmd == 'align':
            try:
                lift = {k: int(v) for k, v in (x.split('=', 1) for x in a.lift)}
            except ValueError:
                raise K.KitError('BAD_LIFT', '--lift wants <frame file name without .png>=<pixels>')
            return align(kit, a.id, a.frames, a.out, a.part, a.to, split(a.scale_by), a.baseline, lift)
        return jitter(kit, a.id, a.frames, split(a.moving), split(a.fixed))
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
