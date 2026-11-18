#!/usr/bin/env python3
"""Bring a generated "pixel art" picture down to the real target grid and palette (2d-generation rule 8).

  pixelize.py grid  <image> [--size WxH]                         estimate the grid the picture was actually drawn on
  pixelize.py one   <image> --out <png> --size WxH|auto [--palette ...] [--extra N]
                                                               the whole non-background bbox -> one sprite
  pixelize.py cells <image> --out <dir> --size WxH [--grid 2x2] [--palette ...] [--extra N]
                                                               split a sheet on background gaps, one PNG per cell
  pixelize.py selftest

A generated picture is not on a real grid: it is drawn in blocks of some pixels each, and the block count is often
not the one asked for (asked 20 wide, drawn about 30 wide: scaling that to 20 crushes shapes). So:
  grid   measures the block size (the pitch at which colour edges pile up, blocks taken as square) and prints the
         native grid with a score (share of edges on that grid beyond chance: a clean upscale is near 1, generated
         pictures with uneven blocks score 0.2-0.5 and are marked LOW under 0.4 = confirm by eye on a one-block
         detail). With --size it exits 1 (GRID_MISMATCH) when the native grid is more than 10% off the target on
         either axis: generate again with the exact grid in the prompt instead of scaling down. `one` and `cells`
         print the same warning but still write.
  one / cells   every target cell takes the MOST COMMON colour of its block (not nearest-neighbour, not an average), so
         a stray pixel or an anti-aliased edge does not win. --size auto (one only) uses the estimated native grid.
--palette  JSON list of "#rrggbb" or a path to a JSON file holding one (the art spec palette). Every cell snaps to it.
           Without it colours are only binned (16 levels a channel) and the block's most common bin is averaged.
--extra N  add up to N accent colours the palette lacks: the most common source colours (away from the background
           fringe) that are far from every palette colour. They are printed ("extra colours: ...") — record them in
           the art spec palette as additions, or the next sprite will not have them.
--bg       background colour of the generated picture (default #ff00ff); it becomes transparent.
--bg-tol   how far (sum of channel differences) a pixel may be from --bg and still be background (default 60).
           Raise it when the generated background is uneven and the bbox runs to the picture edge.
Exit: 0 ok / 1 GRID_MISMATCH (grid --size only) / 2 input problem. Needs numpy and Pillow."""
import argparse
import json
import os
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

MISMATCH = 0.10


class InputError(Exception):
    pass


def hex_rgb(h):
    h = h.strip().lstrip('#')
    if len(h) != 6:
        raise InputError(f'colour must be #rrggbb: {h!r}')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def to_hex(c):
    return '#%02x%02x%02x' % tuple(int(v) for v in c[:3])


def load_palette(arg):
    if not arg:
        return []
    text = Path(arg).read_text(encoding='utf-8') if os.path.isfile(arg) else arg
    try:
        data = json.loads(text)
    except ValueError as e:
        raise InputError(f'--palette is neither a JSON list nor a file holding one: {e}')
    if not isinstance(data, list) or not data:
        raise InputError('--palette must be a non-empty JSON list of "#rrggbb"')
    return [hex_rgb(h) for h in data]


def parse_size(s):
    try:
        w, h = s.lower().split('x')
        w, h = int(w), int(h)
    except ValueError:
        raise InputError(f'size must be WxH: {s!r}')
    if w < 1 or h < 1:
        raise InputError(f'size must be positive: {s!r}')
    return w, h


BG_TOL = 60


def bg_mask(rgb, bg, tol=None):
    tol = BG_TOL if tol is None else tol
    return np.abs(rgb.astype(np.int32) - np.array(bg, dtype=np.int32)).sum(axis=-1) <= tol


def content_bbox(rgb, bg):
    ys, xs = np.nonzero(~bg_mask(rgb, bg))
    if not len(xs):
        raise InputError('the picture is all background')
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def runs(mask):
    """[(start, end)] of consecutive True values."""
    out, start = [], None
    for i, v in enumerate(list(mask) + [False]):
        if v and start is None:
            start = i
        elif not v and start is not None:
            out.append((start, i))
            start = None
    return out


def split_cells(rgb, gx, gy, bg):
    content = ~bg_mask(rgb, bg)
    h, w = content.shape
    cx = runs(content.sum(axis=0) > h * 0.1)
    cy = runs(content.sum(axis=1) > w * 0.1)
    cx = sorted(cx, key=lambda r: r[1] - r[0], reverse=True)[:gx]
    cy = sorted(cy, key=lambda r: r[1] - r[0], reverse=True)[:gy]
    if len(cx) != gx or len(cy) != gy:
        raise InputError(f'expected {gx}x{gy} cells, found {len(cx)}x{len(cy)} content bands')
    return [(x0, y0, x1, y1) for (y0, y1) in sorted(cy) for (x0, x1) in sorted(cx)]


# ---------- native grid ----------

def _edge_profile(rgb, tol=40):
    """per column boundary, how many rows have a colour edge there. An edge is a local maximum of the colour step,
    so a soft (blended) edge a few pixels wide counts once, at its centre."""
    a = rgb.astype(np.int32)
    d = np.abs(a[:, 1:] - a[:, :-1]).sum(axis=-1)
    pad = np.pad(d, ((0, 0), (1, 1)))
    return ((d > tol) & (d >= pad[:, :-2]) & (d > pad[:, 2:])).sum(axis=0).astype(np.float64)


def _comb(wide, p, offsets):
    """largest sum of the (+-1px widened) profile on the teeth o, o+p, o+2p, ... over the offsets"""
    n = len(wide)
    k = np.arange(int(n / p) + 1)
    pos = np.rint(offsets[:, None] + k[None, :] * p).astype(np.int64)
    return float(np.where(pos < n, wide[np.minimum(pos, n - 1)], 0.0).sum(axis=1).max())


def _block(rgb, min_block=4.0, min_excess=0.15):
    """(block size in pixels, score) or None. Blocks are taken as square. Colour edges sit on block boundaries, so the
    block is the pitch whose comb (teeth +-1px) catches the largest share of all edges beyond what its coverage
    (3/pitch) catches by chance, summed over both axes: half the pitch catches the same edges with twice the coverage,
    twice the pitch misses every other boundary. Blocks under 4px are not estimated."""
    wides = []
    for prof in (_edge_profile(rgb), _edge_profile(rgb.transpose(1, 0, 2))):
        if len(prof) < 4 * min_block or prof.sum() < 16:
            return None
        wide = prof.copy()
        wide[1:] += prof[:-1]
        wide[:-1] += prof[1:]
        wides.append((wide, float(prof.sum())))

    def score(p):
        return sum(_comb(w, p, np.arange(0, p, 0.5)) / t - 3.0 / p for w, t in wides) / 2

    ps = np.arange(min_block, min(len(w) for w, _ in wides) / 4 + 0.05, 0.1)
    sc = np.array([score(p) for p in ps])
    if not len(sc) or sc.max() < min_excess:
        return None
    coarse = ps[int(np.argmax(sc))]
    fine = np.arange(max(min_block, coarse - 0.1), coarse + 0.1, 0.01)
    fs = [score(p) for p in fine]
    return float(fine[int(np.argmax(fs))]), float(max(fs))


def native_grid(rgb, bg):
    """(cells_x, cells_y, block_px, score) of the content bbox, or None when no regular block pitch shows.
    score: share of colour edges on the block grid beyond chance, 0..1 (a clean upscale is near 1)."""
    x0, y0, x1, y1 = content_bbox(rgb, bg)
    est = _block(rgb[y0:y1, x0:x1])
    if not est:
        return None
    return max(1, round((x1 - x0) / est[0])), max(1, round((y1 - y0) / est[0])), est[0], est[1]


def grid_report(rgb, bg, size=None):
    """prints the estimate; True when it is within MISMATCH of size (or no size / no estimate)"""
    g = native_grid(rgb, bg)
    if g is None:
        print('native grid: NOT_ESTIMATED (no regular block pitch found: too little detail, blocks under 4px, or not pixel art)')
        return True
    cx, cy, block, score = g
    print(f'native grid: {cx}x{cy} (block {block:.1f}px, score {score:.2f}{" LOW" if score < 0.4 else ""})')
    if not size:
        return True
    w, h = size
    off = max(abs(cx - w) / w, abs(cy - h) / h)
    if off > MISMATCH:
        print(f'GRID_MISMATCH target {w}x{h}, drawn on about {cx}x{cy} ({off:.0%} off): scaling will crush shapes -> '
              f'check a one-block detail by eye, then generate again with the exact grid in the prompt, '
              f'or pixelize at the native grid (--size auto) and redraw')
        return False
    return True


# ---------- pixelize ----------

def _nearest_index(rgb, pal):
    a = rgb.astype(np.int32)[..., None, :] - np.array(pal, dtype=np.int32)
    d = a[..., 0] ** 2 * 2 + a[..., 1] ** 2 * 4 + a[..., 2] ** 2 * 3
    return d.argmin(axis=-1)


def pixelize(rgb, w, h, pal, bg):
    """rgb [H,W,3] uint8 -> RGBA image w x h. Each cell = the most common colour of its block."""
    H, W = rgb.shape[:2]
    back = bg_mask(rgb, bg)
    if pal:
        idx = _nearest_index(rgb, pal)
        table = np.array(pal, dtype=np.uint8)
    else:
        q = (rgb // 16).astype(np.int32)
        idx = q[..., 0] * 256 + q[..., 1] * 16 + q[..., 2]
        table = None
    n = (len(pal) if pal else 4096)
    idx = np.where(back, n, idx)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    for by in range(h):
        y0, y1 = by * H // h, max((by + 1) * H // h, by * H // h + 1)
        for bx in range(w):
            x0, x1 = bx * W // w, max((bx + 1) * W // w, bx * W // w + 1)
            blk = idx[y0:y1, x0:x1]
            win = int(np.bincount(blk.ravel(), minlength=n + 1).argmax())
            if win == n:
                continue
            col = table[win] if table is not None else rgb[y0:y1, x0:x1][blk == win].mean(axis=0).round()
            out[by, bx] = (*col, 255)
    return Image.fromarray(out, 'RGBA')


def extra_colors(rgb, pal, n, bg, min_dist=60, min_share=0.005):
    """up to n common source colours (binned) that no palette colour is close to. Pixels within a few pixels of the
    background are left out: that is the blended fringe between the outline and the background, not an accent."""
    if n <= 0:
        return []
    inner = ~bg_mask(rgb, bg, tol=max(200, BG_TOL))
    for _ in range(max(2, min(rgb.shape[:2]) // 200)):
        m = inner.copy()
        m[1:, :] &= inner[:-1, :]
        m[:-1, :] &= inner[1:, :]
        m[:, 1:] &= inner[:, :-1]
        m[:, :-1] &= inner[:, 1:]
        inner = m
    px = rgb[inner]
    if not len(px):
        return []
    q = (px // 16).astype(np.int32)
    keys, counts = np.unique(q[:, 0] * 256 + q[:, 1] * 16 + q[:, 2], return_counts=True)
    out = []
    for i in np.argsort(-counts):
        if counts[i] < len(px) * min_share:
            break
        k = int(keys[i])
        c = (k // 256 * 16 + 8, k // 16 % 16 * 16 + 8, k % 16 * 16 + 8)
        near = min((sum((a - b) ** 2 for a, b in zip(p, c)) ** 0.5 for p in [*pal, *out]), default=1e9)
        if near >= min_dist:
            out.append(c)
        if len(out) == n:
            break
    return out


def load_rgb(path):
    try:
        im = Image.open(path)
    except (OSError, ValueError) as e:
        raise InputError(f'{path}: {e}')
    return np.asarray(im.convert('RGB'))


# ---------- selftest ----------

def _sprite(w=20, h=28):
    """a small sprite filling its grid: outline, body, a yellow band and a red roof"""
    O, B, Y, R = (26, 18, 8), (158, 142, 126), (236, 200, 40), (200, 40, 40)
    g = np.zeros((h, w, 3), dtype=np.uint8)
    g[:] = O
    g[1:h - 1, 1:w - 1] = B
    g[1:7, 1:w - 1] = R
    g[12:14, 1:w - 1] = Y
    g[18:20, 8:12] = O
    for i in range(4, w - 4, 2):                      # one-cell details so the block size is measurable
        g[9, i] = O
        g[22, i + 1] = O
    return g


def _blow_up(grid, block, noise=0, seed=7):
    big = np.repeat(np.repeat(grid, block, axis=0), block, axis=1).astype(np.int32)
    if noise:
        rng = np.random.default_rng(seed)
        keep = np.abs(big - np.array((255, 0, 255))).sum(axis=-1) > 0
        big[keep] += rng.integers(-noise, noise + 1, size=big.shape)[keep]
    pad = np.full((big.shape[0] + 2 * block, big.shape[1] + 2 * block, 3), (255, 0, 255), dtype=np.int32)
    pad[block:-block, block:-block] = big
    return np.clip(pad, 0, 255).astype(np.uint8)


def selftest():
    cases = []

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    bg = (255, 0, 255)
    pal = [(26, 18, 8), (158, 142, 126), (68, 186, 10)]
    src = _sprite()
    big = _blow_up(src, 32, noise=6)
    g = native_grid(big, bg)
    case('native grid of a 20x28 sprite drawn in 32px blocks', g and g[:2] == (20, 28), str(g))
    case('grid --size 20x28 matches', grid_report(big, bg, (20, 28)))
    wide = _blow_up(_sprite(30, 42), 21, noise=6)
    g = native_grid(wide, bg)
    case('native grid of a picture drawn 30x42 when 20x28 was asked', g and g[:2] == (30, 42), str(g))
    case('grid --size 20x28 reports GRID_MISMATCH for it', not grid_report(wide, bg, (20, 28)))
    box = content_bbox(big, bg)
    crop = big[box[1]:box[3], box[0]:box[2]]
    out = np.asarray(pixelize(crop, 20, 28, pal, bg))
    case('block mode restores the grid (outline, body)', tuple(out[0, 0][:3]) == (26, 18, 8) and tuple(out[15, 10][:3]) == (158, 142, 126))
    case('without accent colours the yellow band and red roof snap to the wrong palette colour',
         tuple(out[12, 10][:3]) in pal and tuple(out[5, 10][:3]) in pal)
    add = extra_colors(big, pal, 2, bg)
    far = lambda c, t: sum((int(a) - int(b)) ** 2 for a, b in zip(c, t)) ** 0.5 < 30  # noqa: E731
    case('--extra 2 finds the yellow and the red', len(add) == 2 and any(far(c, (236, 200, 40)) for c in add)
         and any(far(c, (200, 40, 40)) for c in add), ' '.join(to_hex(c) for c in add))
    out = np.asarray(pixelize(crop, 20, 28, pal + add, bg))
    case('with them the band stays yellow and the roof red', far(out[12, 10][:3], (236, 200, 40)) and far(out[5, 10][:3], (200, 40, 40)))
    case('extra colours never include the background fringe', all(not far(c, bg) for c in extra_colors(big, pal, 8, bg)))
    stray = crop.copy()
    stray[32 * 15 + 3, 32 * 10 + 3] = (0, 0, 0)
    case('one stray dark pixel does not win a block', tuple(np.asarray(pixelize(stray, 20, 28, pal + add, bg))[15, 10][:3]) == (158, 142, 126))
    full = np.asarray(pixelize(big, 22, 30, pal + add, bg))
    case('background becomes transparent', full[0, 0, 3] == 0 and full[15, 11, 3] == 255)
    nop = np.asarray(pixelize(crop, 20, 28, [], bg))
    case('no palette: block colour is the average of the winning bin', far(nop[12, 10][:3], (236, 200, 40)) and far(nop[15, 10][:3], (158, 142, 126)))
    sheet = np.full((96, 96, 3), bg, dtype=np.uint8)
    cols = [(70, 185, 12), (160, 142, 125), (66, 188, 9), (30, 20, 10)]
    for i, c in enumerate(cols):
        x0, y0 = 4 + (i % 2) * 48, 4 + (i // 2) * 48
        sheet[y0:y0 + 40, x0:x0 + 40] = c
    boxes = split_cells(sheet, 2, 2, bg)
    got = [to_hex(np.asarray(pixelize(sheet[b[1]:b[3], b[0]:b[2]], 4, 4, pal, bg))[1, 1]) for b in boxes]
    case('cells splits a 2x2 sheet in reading order and snaps each', got == ['#44ba0a', '#9e8e7e', '#44ba0a', '#1a1208'], str(got))
    td = Path(tempfile.mkdtemp(prefix='pixelize-selftest-'))
    Image.fromarray(big, 'RGB').save(td / 'big.png')
    (td / 'pal.json').write_text(json.dumps([to_hex(c) for c in pal]))
    rc = main(['one', str(td / 'big.png'), '--out', str(td / 'o.png'), '--size', 'auto', '--palette', str(td / 'pal.json'), '--extra', '2'])
    case('one --size auto with a palette file writes a 20x28 sprite', rc == 0 and Image.open(td / 'o.png').size == (20, 28))
    case('all-background picture is an input problem (exit 2)',
         (Image.new('RGB', (8, 8), bg).save(td / 'e.png') or main(['one', str(td / 'e.png'), '--out', str(td / 'x.png'), '--size', '4x4'])) == 2)
    bad = cases.count(False)
    print(f"pixelize selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    g = sub.add_parser('grid')
    g.add_argument('image')
    g.add_argument('--size')
    o = sub.add_parser('one')
    c = sub.add_parser('cells')
    for p in (o, c):
        p.add_argument('image')
        p.add_argument('--out', required=True)
        p.add_argument('--size', required=True)
        p.add_argument('--palette')
        p.add_argument('--extra', type=int, default=0)
    c.add_argument('--grid', default='2x2')
    for p in (g, o, c):
        p.add_argument('--bg', default='#ff00ff')
        p.add_argument('--bg-tol', type=int, default=60)
    sub.add_parser('selftest')
    a = ap.parse_args(argv)
    if a.cmd == 'selftest':
        return selftest()
    global BG_TOL
    BG_TOL = a.bg_tol
    try:
        bg = hex_rgb(a.bg)
        rgb = load_rgb(a.image)
        if a.cmd == 'grid':
            return 0 if grid_report(rgb, bg, parse_size(a.size) if a.size else None) else 1
        pal = load_palette(a.palette)
        added = extra_colors(rgb, pal, a.extra, bg)
        if added:
            print('extra colours:', ' '.join(to_hex(x) for x in added), '(record them in the art spec palette as additions)')
        pal = pal + added if pal else pal
        if a.cmd == 'one':
            if a.size.lower() == 'auto':
                est = native_grid(rgb, bg)
                if est is None:
                    raise InputError('--size auto: the native grid could not be estimated, give WxH')
                w, h = est[:2]
                print(f'native grid: {w}x{h} (block {est[2]:.1f}px, score {est[3]:.2f})')
            else:
                w, h = parse_size(a.size)
                grid_report(rgb, bg, (w, h))
            x0, y0, x1, y1 = content_bbox(rgb, bg)
            Path(a.out).parent.mkdir(parents=True, exist_ok=True)
            pixelize(rgb[y0:y1, x0:x1], w, h, pal, bg).save(a.out)
            print(a.out)
            return 0
        w, h = parse_size(a.size)
        gx, gy = parse_size(a.grid)
        os.makedirs(a.out, exist_ok=True)
        for i, (x0, y0, x1, y1) in enumerate(split_cells(rgb, gx, gy, bg)):
            path = os.path.join(a.out, f'cell{i}.png')
            pixelize(rgb[y0:y1, x0:x1], w, h, pal, bg).save(path)
            print(path, (x0, y0, x1, y1))
        return 0
    except InputError as e:
        print(f'INPUT: {e}')
        return 2


if __name__ == '__main__':
    sys.exit(main())
