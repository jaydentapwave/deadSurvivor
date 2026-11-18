#!/usr/bin/env python3
"""Derive the joining tiles of a path (road, river, wall) from ONE adopted horizontal straight tile.

  path_tiles.py make <straight.png> --out <dir> [--prefix tile_path]
  path_tiles.py check <dir> [--prefix tile_path]
  path_tiles.py selftest

A straight tile alone cannot draw a map: a path that turns or stops needs corner and end tiles, and drawing a turn
with the straight tile leaves the band cut at every corner. make writes the 10 tiles of a two-connection path set:
  <prefix>_h, _v                 straight (west-east, north-south)
  <prefix>_ne, _nw, _se, _sw     corners, named by the two connected sides
  <prefix>_end_n, _e, _s, _w     ends, named by the one connected side
Every output pixel gets an `along` coordinate (distance along the path) and a `cross` coordinate (signed distance from
the centre line) and takes the colour of the straight tile there, so all tiles share one edge pattern and meet their
neighbours on the same rows and columns. The straight tile must be square, with the band running west-east and the
ground outside it either transparent or one flat colour (the colour of the top-left pixel). A band that is not centred
is centred first by dropping interior rows (reported). T-junctions and crossings are not made.
check (also run by make) reads a tile folder: every connected side must show the band where the straight tile shows
it (+-1px), every other side must be ground. Exit: 0 ok / 1 edge problems / 2 input problem.
The check is about edges only. Look at the tiles on a map before showing them: tileprev.py. Needs Pillow."""
import argparse
import sys
import tempfile
from pathlib import Path

from PIL import Image

SIDES = 'nesw'
OPEN = {'h': 'we', 'v': 'ns', 'sw': 'sw', 'nw': 'nw', 'ne': 'ne', 'se': 'se',
        'end_e': 'e', 'end_s': 's', 'end_w': 'w', 'end_n': 'n'}


class InputError(Exception):
    pass


def tile_key(sides):
    """tile suffix for a set of connected sides ('n','e','s','w'); None if this set has no tile (0, 3 or 4 sides)"""
    s = set(sides)
    if len(s) == 1:
        return 'end_' + next(iter(s))
    if s == {'w', 'e'}:
        return 'h'
    if s == {'n', 's'}:
        return 'v'
    if len(s) == 2:
        return ''.join(c for c in 'ns' if c in s) + ''.join(c for c in 'ew' if c in s)
    return None


def ground_of(im):
    """the ground test of one tile: transparent pixels if the tile has any, else the colour of its top-left pixel"""
    px = im.load()
    n = im.width
    if any(px[x, y][3] == 0 for y in range(im.height) for x in range(n)):
        return lambda c: c[3] == 0
    g = px[0, 0]
    return lambda c: c == g


def _rows(im):
    px = im.load()
    return [[px[x, y] for x in range(im.width)] for y in range(im.height)]


def normalise(straight):
    """(tile with the band centred, rows dropped). Interior band rows are dropped until the ground margins above and
    below are equal; the corner and end tiles are made by turning the band, which needs it on the centre line."""
    im = straight.convert('RGBA')
    n = im.width
    if im.height != n:
        raise InputError(f'the straight tile must be square, got {im.width}x{im.height}')
    is_ground = ground_of(im)
    rows = _rows(im)
    fill = [rows[0][0]] * n

    def band():
        return [y for y in range(n) if not all(is_ground(c) for c in rows[y])]
    b = band()
    if not b:
        raise InputError('no band found: the whole tile is ground (top-left pixel colour or transparent)')
    if b[0] == 0 or b[-1] == n - 1:
        raise InputError('the band touches the top or bottom edge: the tile needs ground rows on both sides of the band')
    dropped = 0
    while b[0] != n - 1 - b[-1]:
        rows.pop((b[0] + b[-1]) // 2)
        if b[0] < n - 1 - b[-1]:
            rows.insert(0, list(fill))
        else:
            rows.append(list(fill))
        dropped += 1
        b = band()
        if len(b) < 2:
            raise InputError('the band is too thin to centre')
    out = Image.new('RGBA', (n, n))
    out.putdata([c for row in rows for c in row])
    return out, dropped


def _sample(h, along, cross):
    n = h.width
    c = (n - 1) / 2
    y = min(max(int(round(c + cross)), 0), n - 1)
    return h.getpixel((int(along) % n, y))


def corner_ws(h):
    """path enters from the west and leaves to the south"""
    n = h.width
    c = (n - 1) / 2
    out = Image.new('RGBA', (n, n))
    for y in range(n):
        for x in range(n):
            # west arm above the diagonal; south arm below it, travelling south (left of travel is east)
            out.putpixel((x, y), _sample(h, x, y - c) if x + y < n - 1 else _sample(h, y, c - x))
    return out


def end_e(h):
    """path connects to the east only; the west side is closed with the same edge pattern (a square cap)"""
    n = h.width
    c = (n - 1) / 2
    out = Image.new('RGBA', (n, n))
    for y in range(n):
        for x in range(n):
            # west of the centre the distance to the path is the larger of the vertical and westward distances;
            # where the westward one wins, the colour comes from the edge rows turned to face west
            west = x < c and (c - x) > abs(y - c)
            out.putpixel((x, y), _sample(h, y, -(c - x)) if west else _sample(h, x, y - c))
    return out


def _rot(im, quarter_turns_cw):
    for _ in range(quarter_turns_cw % 4):
        im = im.transpose(Image.Transpose.ROTATE_270)
    return im


def make_all(straight):
    """({suffix: tile}, interior rows dropped to centre the band)"""
    h, dropped = normalise(straight)
    ws, e = corner_ws(h), end_e(h)
    return {'h': h, 'v': _rot(h, 1),
            'sw': ws, 'nw': _rot(ws, 1), 'ne': _rot(ws, 2), 'se': _rot(ws, 3),       # cw: w->n, s->w
            'end_e': e, 'end_s': _rot(e, 1), 'end_w': _rot(e, 2), 'end_n': _rot(e, 3)}, dropped


def band_on_side(im, side):
    """indices along one side of the tile that are not ground (the opening)"""
    n = im.width
    px = im.load()
    is_ground = ground_of(im)
    pts = {'n': [(i, 0) for i in range(n)], 's': [(i, n - 1) for i in range(n)],
           'w': [(0, i) for i in range(n)], 'e': [(n - 1, i) for i in range(n)]}[side]
    return [i for i, p in enumerate(pts) if not is_ground(px[p])]


def check(tiles):
    """problems: a connected side whose band is not where the straight tile's is (+-1px), a closed side with band pixels"""
    missing = [k for k in OPEN if k not in tiles]
    if missing:
        return [f'missing tile: {k}' for k in missing]
    ref = band_on_side(tiles['h'], 'w')
    east = band_on_side(tiles['h'], 'e')
    if not ref or not east:
        return ['the straight tile (h) shows no band on its west or east side']
    problems = []
    if abs(ref[0] - east[0]) > 1 or abs(ref[-1] - east[-1]) > 1:
        problems.append(f'h: west band {ref[0]}..{ref[-1]} and east band {east[0]}..{east[-1]} differ: it will not tile with itself')
    for name, sides in OPEN.items():
        if tiles[name].size != tiles['h'].size:
            problems.append(f'{name}: size {tiles[name].size} != {tiles["h"].size}')
            continue
        for side in SIDES:
            b = band_on_side(tiles[name], side)
            if side in sides and (not b or abs(b[0] - ref[0]) > 1 or abs(b[-1] - ref[-1]) > 1):
                problems.append(f'{name}: connected side {side} band {b[:1]}..{b[-1:]} != {ref[0]}..{ref[-1]} (+-1)')
            if side not in sides and b:
                problems.append(f'{name}: closed side {side} has {len(b)} band pixels')
    return problems


def load_set(folder, prefix):
    """{suffix: RGBA tile} for the files <prefix>_<suffix>.png that exist"""
    folder = Path(folder)
    if not folder.is_dir():
        raise InputError(f'no folder {folder}')
    out = {}
    for k in OPEN:
        p = folder / f'{prefix}_{k}.png'
        if p.is_file():
            out[k] = Image.open(p).convert('RGBA')
    return out


def _straight(n=20, top=3, bottom=17, opaque=True):
    """synthetic straight tile: ground, an edge row at `top` and `bottom`, fill between with a mark every 5px"""
    g = (68, 186, 10, 255) if opaque else (0, 0, 0, 0)
    e, d, m = (150, 100, 50, 255), (237, 168, 88, 255), (210, 140, 70, 255)
    im = Image.new('RGBA', (n, n), g)
    for y in range(top, bottom + 1):
        for x in range(n):
            im.putpixel((x, y), e if y in (top, bottom) else m if (x % 5 == 0 and y == (top + bottom) // 2) else d)
    return im


def selftest():
    cases = []

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    for label, n, top, bottom, opaque in (('20px, band off centre, flat ground', 20, 3, 17, True),
                                          ('32px, centred band, transparent ground', 32, 8, 23, False),
                                          ('17px (odd), band off centre', 17, 2, 11, True)):
        tiles, dropped = make_all(_straight(n, top, bottom, opaque))
        problems = check(tiles)
        b = band_on_side(tiles['h'], 'w')
        case(f'{label}: 10 tiles, every side as named', len(tiles) == 10 and not problems, '; '.join(problems[:3]))
        case(f'{label}: band centred (dropped {dropped} rows)', b[0] == n - 1 - b[-1] and (dropped == 0) == (top == n - 1 - bottom))
    tiles, _ = make_all(_straight())
    as_drawn = dict(tiles, sw=tiles['h'], ne=tiles['h'])
    problems = check(as_drawn)
    case('a straight tile used as a corner is reported (closed side has band, connected side has none)',
         any('sw: closed side e' in p for p in problems) and any('sw: connected side s' in p for p in problems), f'{len(problems)} problems')
    case('a missing tile is reported', check({k: v for k, v in tiles.items() if k != 'end_n'}) == ['missing tile: end_n'])
    case('tile_key names', [tile_key(s) for s in ('we', 'ns', 'ws', 'en', 'n', 'nes', '')] == ['h', 'v', 'sw', 'ne', 'end_n', None, None])
    td = Path(tempfile.mkdtemp(prefix='path-tiles-selftest-'))
    _straight().save(td / 's.png')
    rc = main(['make', str(td / 's.png'), '--out', str(td / 'out'), '--prefix', 'tile_road'])
    case('make writes <prefix>_<name>.png and check reads them back', rc == 0 and len(list((td / 'out').glob('tile_road_*.png'))) == 10
         and main(['check', str(td / 'out'), '--prefix', 'tile_road']) == 0)
    Image.new('RGBA', (20, 12), (0, 0, 0, 255)).save(td / 'bad.png')
    case('a non-square tile is an input problem (exit 2)', main(['make', str(td / 'bad.png'), '--out', str(td / 'o2')]) == 2)
    case('a tile whose band touches the edge is an input problem (exit 2)',
         (_straight(20, 0, 10).save(td / 'edge.png') or main(['make', str(td / 'edge.png'), '--out', str(td / 'o3')])) == 2)
    bad = cases.count(False)
    print(f"path_tiles selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    m = sub.add_parser('make')
    m.add_argument('straight')
    m.add_argument('--out', required=True)
    c = sub.add_parser('check')
    c.add_argument('dir')
    for p in (m, c):
        p.add_argument('--prefix', default='tile_path')
    sub.add_parser('selftest')
    a = ap.parse_args(argv)
    if a.cmd == 'selftest':
        return selftest()
    try:
        if a.cmd == 'make':
            try:
                src = Image.open(a.straight)
            except (OSError, ValueError) as e:
                raise InputError(f'{a.straight}: {e}')
            tiles, dropped = make_all(src)
            out = Path(a.out)
            out.mkdir(parents=True, exist_ok=True)
            for name, im in tiles.items():
                im.save(out / f'{a.prefix}_{name}.png')
            print(f'wrote {len(tiles)} tiles to {out}' + (f' (band centred: {dropped} interior rows dropped)' if dropped else ''))
        else:
            tiles = load_set(a.dir, a.prefix)
        problems = check(tiles)
        print(f"edge check {'PASS' if not problems else 'FAIL'}: {len(OPEN)} tiles x 4 sides, {len(problems)} problems")
        for p in problems:
            print('  ', p)
        return 1 if problems else 0
    except InputError as e:
        print(f'INPUT: {e}')
        return 2


if __name__ == '__main__':
    sys.exit(main())
