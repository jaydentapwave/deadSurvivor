#!/usr/bin/env python3
"""State pictures of one prop (empty / ripe, closed / open, empty shelf / stocked): build the second state from the first
plus separate pieces, and check that nothing else changed (2d-generation "상태가 있는 소품").

  states.py compose <base.png> --piece <piece.png>@<cx>,<cy>[,<scale>]... --out <state.png> [--mask <mask.png>]
  states.py verify <base.png> <state.png> --mask <mask.png> [--key #RRGGBB] [--band 3] [--allow 0]
  states.py selftest

Why: asked for "the same plant, with fruit", a generator redraws the plant. Source case: the empty and ripe tree had
different inner branches and leaves, and a silhouette IoU of 0.996 did not see it. Generating the empty plant and the
fruit as separate pieces on one sheet and pasting the pieces gave 0 differing pixels outside the fruit, on five props.
compose puts each piece on the base, centred at <cx>,<cy> (px on the base canvas, y down), optionally scaled. Pieces are
  drawn OVER the base (alpha blending): overwriting the soft rim of a piece leaves a pale 1 px line where the base
  showed through. The canvas and everything outside the pieces stay the base's, byte for byte in the pixels. The mask
  (default <state>.mask.png) is where pieces were drawn; keep it - verify needs it.
verify  same canvas; every pixel that differs between the two states lies inside the mask (PASS: 0 outside, --allow);
  with a key colour (--key, or the chunk matte.py key wrote on the state picture) also: no key-hued pixels in the --band px
  either side of the mask edge. matte.py fringe only looks where the figure meets transparency; a pasted piece's rim is inside.
Inside a kit project verify writes work_dir/measure/<sha256 of the state>.states.json. Needs no art spec or identity.
Exit: 0 PASS (compose: written) / 1 FAIL / 2 input problem. Needs numpy and Pillow."""
import argparse
import re
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402

PIECE_RE = re.compile(r'^(.+)@(-?[\d.]+),(-?[\d.]+)(?:,([\d.]+))?$')
TINT = 25               # channel excess toward the key (0..255) that counts as key-hued, same as matte.py --whole


def over(dst, src, x, y):
    """alpha-blend src (RGBA float) onto dst in place with its top-left at x, y; returns the footprint (bool, dst-sized)"""
    H, W = dst.shape[:2]
    h, w = src.shape[:2]
    x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
    foot = np.zeros((H, W), dtype=bool)
    if x0 >= x1 or y0 >= y1:
        return foot
    s = src[y0 - y:y1 - y, x0 - x:x1 - x]
    d = dst[y0:y1, x0:x1]
    sa, da = s[..., 3:4] / 255.0, d[..., 3:4] / 255.0
    oa = sa + da * (1 - sa)
    rgb = (s[..., :3] * sa + d[..., :3] * da * (1 - sa)) / np.maximum(oa, 1e-6)
    d[..., :3] = np.where(oa > 0, rgb, d[..., :3])
    d[..., 3:4] = oa * 255.0
    foot[y0:y1, x0:x1] = s[..., 3] > 0
    return foot


def cmd_compose(base, pieces, out, mask_path=None, quiet=False):
    import matte as MT
    img = K.load_rgba(base).copy()
    H, W = img.shape[:2]
    mask = np.zeros((H, W), dtype=bool)
    for spec in pieces:
        m = PIECE_RE.match(str(spec))
        if not m:
            raise K.KitError('BAD_PIECE', f'--piece wants <png>@<cx>,<cy>[,<scale>], got {spec}')
        if not Path(m[1]).is_file():
            raise K.KitError('NO_IMAGE', m[1])
        pim = Image.open(m[1]).convert('RGBA')
        sc = float(m[4]) if m[4] else 1.0
        if sc != 1.0:
            pim = pim.resize((max(1, round(pim.width * sc)), max(1, round(pim.height * sc))), Image.LANCZOS)
        cx, cy = float(m[2]), float(m[3])
        x, y = round(cx - pim.width / 2), round(cy - pim.height / 2)
        if x < 0 or y < 0 or x + pim.width > W or y + pim.height > H:
            raise K.KitError('PIECE_OUTSIDE', f'{Path(m[1]).name} at {cx:g},{cy:g} leaves the {W}x{H} canvas (the two states must share one canvas and pivot)')
        mask |= over(img, np.asarray(pim, dtype=np.float32), x, y)
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    try:
        MT.save(img, out, MT.read_key(base))
    except K.KitError:
        Image.fromarray(np.clip(img + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(out)
    mp = Path(mask_path) if mask_path else out.with_suffix('.mask.png')
    Image.fromarray((mask * 255).astype(np.uint8), 'L').save(mp)
    if not quiet:
        print(f'COMPOSE {out}: {len(pieces)} piece(s) drawn over {Path(base).name}, mask {mp} ({int(mask.sum())} px)')
    return 0


def cmd_verify(base, state, mask_path, key=None, band=3, allow=0, quiet=False):
    import matte as MT
    a, b = K.load_rgba(base), K.load_rgba(state)
    try:
        mask = np.asarray(Image.open(mask_path).convert('L')) > 0
    except (OSError, ValueError) as e:
        raise K.KitError('BAD_IMAGE', f'{mask_path}: {e}')
    why = []
    out = {'canvas_same': a.shape == b.shape, 'mask_px': int(mask.sum())}
    if a.shape != b.shape or mask.shape != a.shape[:2]:
        why.append(f'canvas differs: base {a.shape[1]}x{a.shape[0]}, state {b.shape[1]}x{b.shape[0]}, mask {mask.shape[1]}x{mask.shape[0]}')
    else:
        diff = np.abs(a - b).sum(-1) > 0
        outside = diff & ~mask
        out.update(diff_px=int(diff.sum()), outside_px=int(outside.sum()))
        if out['outside_px'] > allow:
            ys, xs = np.nonzero(outside)
            why.append(f"{out['outside_px']} px differ outside the pieces (look at x={int(xs[0])}, y={int(ys[0])})")
        if not out['diff_px']:
            why.append('the two states are the same picture')
        try:
            kc = MT.read_key(state, key)
        except K.KitError:
            kc = None
        hi = MT.key_channels(kc) if kc is not None else None
        if hi is not None:
            edge = K.dilate(mask, band) & ~_shrink(mask, band) & (b[..., 3] > 0)
            tint = edge & (MT.spill(b[..., :3], hi) > TINT)
            out.update(key=MT.hexs(kc), band_px=int(edge.sum()), tint_px=int(tint.sum()))
            if out['tint_px'] > allow:
                ys, xs = np.nonzero(tint)
                why.append(f"{out['tint_px']} key-hued px on the pieces' rim (look at x={int(xs[0])}, y={int(ys[0])})")
        else:
            out['tint_px'] = None
    verdict = 'FAIL' if why else 'PASS'
    try:
        kit = K.load_kit(Path(state).resolve().parent)
        K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': 'states', 'image': str(Path(state).resolve()), 'sha256': K.sha256(state),
                             'verdict': verdict, 'base': str(Path(base).resolve()), 'base_sha256': K.sha256(base), 'allow': allow, **out})
    except K.KitError:
        pass
    if not quiet:
        tint = 'rim tint not checked (no key colour: --key)' if out.get('tint_px') is None else f"rim tint {out['tint_px']} px"
        print(f"{verdict} {Path(state).name} vs {Path(base).name}: {out.get('diff_px', '?')} px differ, {out.get('outside_px', '?')} outside the pieces, {tint}"
              + (f"  ({'; '.join(why)})" if why else ''))
    return 1 if why else 0


def _shrink(mask, n):
    m = mask.copy()
    for _ in range(int(n)):
        m = K.erode(m)
    return m


# ---------- selftest ----------

def _disc(size, fill, outline=(60, 38, 22), soft=True, rim=None):
    from PIL import ImageDraw
    n = size * 4
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    if rim:
        d.ellipse([0, 0, n - 1, n - 1], fill=tuple(rim) + (255,))
    d.ellipse([n * 0.04, n * 0.04, n * 0.96, n * 0.96], fill=outline + (255,))
    d.ellipse([n * 0.16, n * 0.16, n * 0.84, n * 0.84], fill=tuple(fill) + (255,))
    return im.resize((size, size), Image.LANCZOS if soft else Image.NEAREST)


def selftest():
    td = Path(tempfile.mkdtemp(prefix='states-selftest-'))
    cases = []

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    base, fruit = td / 'base.png', td / 'fruit.png'
    _disc(200, (90, 170, 80)).save(base)
    _disc(40, (240, 140, 40)).save(fruit)
    ripe = td / 'ripe.png'
    case('compose writes the state and its mask', cmd_compose(base, [f'{fruit}@80,90', f'{fruit}@125,110,0.75'], ripe, quiet=True) == 0 and (td / 'ripe.mask.png').is_file())
    A, B = K.load_rgba(base), K.load_rgba(ripe)
    M = np.asarray(Image.open(td / 'ripe.mask.png')) > 0
    case('nothing outside the pieces changed, and the canvas is the same', A.shape == B.shape and not (np.abs(A - B).sum(-1) > 0)[~M].any())
    case('pieces are drawn over the base: no see-through rim (alpha stays 255 where the base was opaque)', bool((B[..., 3][M & (A[..., 3] == 255)] == 255).all()))
    case('verify PASS', cmd_verify(base, ripe, td / 'ripe.mask.png', quiet=True) == 0)
    # counter: the state redrawn - a leaf moved outside the fruit
    redrawn = B.copy()
    redrawn[150:156, 60:70, :3] = (40, 110, 50)
    Image.fromarray(redrawn.astype(np.uint8), 'RGBA').save(td / 'redrawn.png')
    case('counter: a redrawn state (60 px changed outside the pieces) FAIL', cmd_verify(base, td / 'redrawn.png', td / 'ripe.mask.png', quiet=True) == 1)
    case('counter: the same picture twice FAIL (no second state)', cmd_verify(base, base, td / 'ripe.mask.png', quiet=True) == 1)
    # counter: overwriting instead of blending leaves the base showing through the piece rim
    ow = A.copy()
    pf = np.asarray(Image.open(fruit).convert('RGBA'), dtype=np.float32)
    ow[70:110, 60:100][pf[..., 3] > 0] = pf[pf[..., 3] > 0]
    case('counter (why compose blends): overwriting makes see-through pixels inside the opaque body', bool((ow[..., 3][A[..., 3] == 255] < 255).any()))
    # key tint on a pasted piece's rim
    tinted = td / 'tinted.png'
    _disc(40, (240, 140, 40), rim=(255, 0, 255)).save(tinted)
    cmd_compose(base, [f'{tinted}@100,100'], td / 'ripe2.png', quiet=True)
    case('counter: magenta left on a pasted piece rim FAIL with --key (and unseen without a key)',
         cmd_verify(base, td / 'ripe2.png', td / 'ripe2.mask.png', key='#ff00ff', quiet=True) == 1 and cmd_verify(base, td / 'ripe2.png', td / 'ripe2.mask.png', quiet=True) == 0)
    case('clean pieces pass the rim check with a key', cmd_verify(base, ripe, td / 'ripe.mask.png', key='#ff00ff', quiet=True) == 0)
    case('a piece that leaves the canvas is an input problem', main(['compose', str(base), '--piece', f'{fruit}@5,5', '--out', str(td / 'x.png')]) == 2)
    small = td / 'small.png'
    _disc(120, (90, 170, 80)).save(small)
    case('different canvas FAIL', cmd_verify(small, ripe, td / 'ripe.mask.png', quiet=True) == 1)
    import json
    root = td / 'proj'
    (root / '.ai/2d-art').mkdir(parents=True)
    (root / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'work_dir': 'out'}))
    Image.open(td / 'redrawn.png').save(root / 'redrawn.png')
    cmd_verify(base, root / 'redrawn.png', td / 'ripe.mask.png', quiet=True)
    rec = K.read_record(K.load_kit(root), K.sha256(root / 'redrawn.png'), 'states')
    case('kit project: states record written (FAIL, 60 px outside)', rec and rec['verdict'] == 'FAIL' and rec['outside_px'] == 60, str(rec and rec.get('outside_px')))
    bad = cases.count(False)
    print(f"states selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    c = sub.add_parser('compose')
    c.add_argument('base')
    c.add_argument('--piece', action='append', required=True)
    c.add_argument('--out', type=Path, required=True)
    c.add_argument('--mask', type=Path)
    v = sub.add_parser('verify')
    v.add_argument('base')
    v.add_argument('state')
    v.add_argument('--mask', type=Path, required=True)
    v.add_argument('--key')
    v.add_argument('--band', type=int, default=3)
    v.add_argument('--allow', type=int, default=0)
    sub.add_parser('selftest')
    a = ap.parse_args(argv)
    if a.cmd == 'selftest':
        return selftest()
    try:
        missing = [str(p) for p in ([a.base] + ([a.state, a.mask] if a.cmd == 'verify' else [])) if not Path(p).is_file()]
        if missing:
            raise K.KitError('NO_IMAGE', ', '.join(missing))
        if a.cmd == 'compose':
            return cmd_compose(a.base, a.piece, a.out, a.mask)
        return cmd_verify(a.base, a.state, a.mask, a.key, max(1, a.band), a.allow)
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
