#!/usr/bin/env python3
"""Preview path tiles laid out as a map — the joining test pattern — before they go into the game.

  tileprev.py sheet <out.png> --tiles <dir> [--prefix tile_path] [--ground <png>...] [--map <txt>] [--scale 4]
  tileprev.py strip <out.png> [<layer.png>...] [--set G=<png> L=<png> ... --map "G G L P R G"] [--band 4] [--tol 2] [--de 12] [--repeat 3]
  tileprev.py roll <in.png> <out.png>
  tileprev.py mend <rolled.png> <regenerated.png> <out.png> [--band 0.2] [--no-palette] [--tol 2] [--de 12]
  tileprev.py selftest

A tile looked at alone, or a row of the straight tile, says nothing about corners and ends: a path set is judged
where tiles meet. sheet lays the tiles of <dir> (<prefix>_h, _v, _ne, _nw, _se, _sw, _end_n, _e, _s, _w — the names
path_tiles.py writes) on a small map and saves it enlarged (nearest-neighbour) next to a 1x copy:
  default map   a closed loop (both straights, all 4 corners) plus one horizontal and one vertical dead-end stretch
                (all 4 ends): every one of the 10 tiles appears at least once.
  --map <txt>   your own layout, one text row per map row: '#' path, anything else ground. Use it for a real stage
                layout. Each path cell takes the tile for its connected neighbours; a cell with 0, 3 or 4 path
                neighbours has no tile in a two-connection set and is an input problem.
  --ground      ground tile(s) drawn under the path, alternated by position; default: a flat grey checker.
The sheet header says what it is ("joining test pattern — not a game screen") and the seam check result:
  for every pair of neighbouring cells, the band on the two facing edges must sit on the same pixels (+-1px), and an
  edge facing ground must be ground. A straight tile reused for a corner fails here.
Inside a kit project (.ai/2d-art/config.json found) every tile on the map also gets a seam record
(work_dir/measure/<sha256>.seam.json, PASS or FAIL): `sheet.py grid --tiles` shows only tiles whose record is PASS.
Exit: 0 sheet written, seams PASS / 1 sheet written, seams FAIL / 2 input problem (missing tile, bad map).
This is not the game screen: after the tiles are in, the short Play capture is still required. Needs Pillow.

Side-view terrain that repeats horizontally (side-scroller ground, parallax layers) — needs numpy too:
strip   each <layer.png> is laid --repeat times in a row and its right edge is judged against its own left edge;
        --set/--map lays a ground set by a letter map ("G G L P R G": middle, pit left end, pit, pit right end — any
        letters) and judges every pair of neighbours in the map. A seam is judged over a band, not one pixel column:
          shape   rows that are solid on one side of the seam and have nothing solid within --tol px (up or down) on
                  the other. Layers with a transparent top are judged the same way: the silhouette must meet.
          colour  dE76 between the two edge columns, each row matched within --tol px; the 95th percentile must be
                  <= --de, or no worse than twice what neighbouring columns inside the --band px on either side differ
                  by (an outline that crosses the seam at a slant differs column to column everywhere, not only at
                  the seam; a 1px column comparison fails it).
          step    the longest run of rows over --de: a colour band whose edge jumps at the seam is a few rows only.
                  Limit: 2 x tol + 1 rows, or twice the longest such run between columns inside the band.
        The sheet header says "이음 시험 패턴 — 게임 화면 아님"; seams are ticked green/red. Inside a kit project every
        picture gets the same seam record as `sheet` writes, so `sheet.py grid --tiles` shows the ones that PASS.
        Exit 0 PASS / 1 FAIL / 2 input problem (heights differ, unknown letter).
roll / mend   mending a seam without redrawing the whole picture:
        1. roll: shift the picture by half its width, so the bad seam sits in the middle and the edges are pixels that
           were neighbours (they join by construction).
        2. outside the tool: redraw only the middle band of the rolled picture (image model edit).
        3. mend: takes the middle --band (fraction of the width) from the redrawn picture and everything else from the
           rolled one — whatever the model changed outside the band is thrown away and reported. Each band border is
           moved inward to the first columns the redraw left as they were (joins by construction); if the redraw
           changed everything up to the border, that border is judged like a `strip` seam. Redrawn pixels close to
           one of the picture's flat paints are snapped to it (redrawn paint drifts a little and shows as a stripe;
           --no-palette to skip). The loop seam is judged too. The result stays rolled: use it as it is.
           Exit 0 PASS / 1 FAIL / 2 input problem."""
import argparse
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import path_tiles as P  # noqa: E402

PATTERN = ['.........',
           '.####.#..',
           '.#..#.#..',
           '.#..#.#..',
           '.####.#..',
           '.........',
           '.###.....',
           '.........']
STEP = {'n': (0, -1), 'e': (1, 0), 's': (0, 1), 'w': (-1, 0)}
FACING = {'n': 's', 'e': 'w', 's': 'n', 'w': 'e'}
RED, GREEN, INK = (200, 30, 40), (30, 140, 60), (20, 20, 20)


def parse_map(rows):
    rows = [r.rstrip('\n') for r in rows if r.strip()]
    if not rows:
        raise P.InputError('the map is empty')
    w = max(len(r) for r in rows)
    return [[c == '#' for c in r.ljust(w)] for r in rows]


def layout(grid):
    """{(x, y): tile suffix} for every path cell"""
    h, w = len(grid), len(grid[0])
    out, bad = {}, []
    for y in range(h):
        for x in range(w):
            if not grid[y][x]:
                continue
            sides = [s for s, (dx, dy) in STEP.items() if 0 <= x + dx < w and 0 <= y + dy < h and grid[y + dy][x + dx]]
            key = P.tile_key(sides)
            if key is None:
                bad.append(f'({x},{y}) has {len(sides)} path neighbours')
            out[(x, y)] = key
    if bad:
        raise P.InputError('cells without a tile in a two-connection set: ' + ', '.join(bad[:6]))
    if not out:
        raise P.InputError("the map has no path cell ('#')")
    return out


def seams(cells, tiles):
    """problems where neighbouring cells meet: band positions differ by more than 1px, or a band runs into ground"""
    problems = []
    for (x, y), key in sorted(cells.items()):
        for side, (dx, dy) in STEP.items():
            mine = P.band_on_side(tiles[key], side)
            other = cells.get((x + dx, y + dy))
            if other is None:
                if mine:
                    problems.append(f'({x},{y}) {key}: band on side {side} runs into ground')
                continue
            if side in 'sw':                         # each pair once
                continue
            theirs = P.band_on_side(tiles[other], FACING[side])
            if not mine or not theirs:
                problems.append(f'({x},{y}) {key} | ({x + dx},{y + dy}) {other}: no band on the shared edge ({side})')
            elif abs(mine[0] - theirs[0]) > 1 or abs(mine[-1] - theirs[-1]) > 1:
                problems.append(f'({x},{y}) {key} | ({x + dx},{y + dy}) {other}: band {mine[0]}..{mine[-1]} meets {theirs[0]}..{theirs[-1]}')
    return problems


def render(grid, cells, tiles, grounds):
    n = next(iter(tiles.values())).width
    h, w = len(grid), len(grid[0])
    im = Image.new('RGBA', (w * n, h * n), (0, 0, 0, 255))
    for y in range(h):
        for x in range(w):
            if grounds:
                im.paste(grounds[(x * 7 + y * 13) % len(grounds)], (x * n, y * n))
            else:
                im.paste((170, 170, 170, 255) if (x + y) % 2 else (150, 150, 150, 255), (x * n, y * n, x * n + n, y * n + n))
    for (x, y), key in cells.items():
        im.alpha_composite(tiles[key], (x * n, y * n))
    return im


def _font(size):
    try:
        import kit2d
        return kit2d.font(size)
    except Exception:  # noqa: BLE001  (kit2d needs numpy; the preview does not)
        from PIL import ImageFont
        return ImageFont.load_default()


def sheet(out, tiles_dir, prefix='tile_path', ground=(), map_path=None, scale=4):
    tiles = P.load_set(tiles_dir, prefix)
    grid = parse_map(Path(map_path).read_text(encoding='utf-8').splitlines() if map_path else PATTERN)
    cells = layout(grid)
    missing = sorted({k for k in cells.values() if k not in tiles})
    if missing:
        raise P.InputError('missing tiles: ' + ', '.join(f'{prefix}_{k}.png' for k in missing)
                           + ' (a path that turns or ends needs them: path_tiles.py make, or draw them)')
    sizes = {t.size for t in tiles.values()}
    if len(sizes) != 1 or next(iter(sizes))[0] != next(iter(sizes))[1]:
        raise P.InputError(f'tiles must be square and all the same size, got {sorted(sizes)}')
    n = next(iter(sizes))[0]
    grounds = []
    for g in ground:
        try:
            im = Image.open(g).convert('RGBA')
        except (OSError, ValueError) as e:
            raise P.InputError(f'{g}: {e}')
        if im.size != (n, n):
            raise P.InputError(f'ground tile {g} is {im.size}, path tiles are {n}x{n}')
        grounds.append(im)
    problems = seams(cells, tiles)
    small = render(grid, cells, tiles, grounds)
    big = small.resize((small.width * scale, small.height * scale), Image.NEAREST)
    font, pad, line = _font(15), 12, 20
    used = sorted(set(cells.values()))
    head = ['이음 시험 패턴 — 게임 화면 아님 (joining test pattern, not a game screen)',
            f'{prefix}: {len(used)} tile kinds on the map ({", ".join(used)}), {n}px, x{scale} and x1',
            f"seam check {'PASS' if not problems else 'FAIL'}: {len(cells)} path cells, {len(problems)} problems"]
    notes = problems[:8] + ([f'... {len(problems) - 8} more'] if len(problems) > 8 else [])
    top = pad + line * (len(head) + len(notes)) + pad
    canvas = Image.new('RGBA', (max(pad * 3 + big.width + small.width, 760), top + big.height + pad), (245, 245, 245, 255))
    d = ImageDraw.Draw(canvas)
    d.rectangle([0, 0, canvas.width, 6], fill=GREEN if not problems else RED)
    for i, t in enumerate(head + notes):
        d.text((pad, pad + i * line), t, fill=(RED if problems else GREEN) if i == 2 else RED if i > 2 else INK, font=font)
    canvas.paste(big, (pad, top))
    canvas.paste(small, (pad * 2 + big.width, top))
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    canvas.save(out)
    print(head[2])
    for p in problems:
        print('  ', p)
    print(out)
    record_seams(Path(tiles_dir), prefix, used, problems, len(cells), out)
    return problems, used


def record_seams(tiles_dir, prefix, used, problems, n_cells, out):
    """seam record per tile file for sheet.py grid --tiles. Skipped silently outside a kit project (no config, no numpy)."""
    try:
        import kit2d
        kit = kit2d.load_kit(tiles_dir)
    except Exception:  # noqa: BLE001  the preview itself must work without the kit
        return 0
    files = [tiles_dir / f'{prefix}_{k}.png' for k in used]
    files = [f for f in files if f.is_file()]
    shas = [kit2d.sha256(f) for f in files]
    for f, sha in zip(files, shas):
        kit2d.write_record(kit, {'schema': kit2d.MEASURE_SCHEMA, 'kind': 'seam', 'image': str(f.resolve()), 'sha256': sha,
                                 'verdict': 'FAIL' if problems else 'PASS', 'set': shas, 'cells': n_cells,
                                 'problems': len(problems), 'sheet': str(Path(out).resolve())})
    return len(files)


# ---------- side-view strips ----------

def _np():
    import numpy as np
    import kit2d
    return np, kit2d


def _edge_diff(a, b, tol, de=12.0):
    """(p95 dE76, unmatched rows, longest step) between two pixel columns (H,4): each solid row is matched to the
    nearest-coloured solid row within tol px on the other column; a solid row with nothing solid within tol opposite
    is unmatched; step = the longest run of rows that differ by more than de (a colour band that jumps at the seam is
    a short run — too few rows to move the 95th percentile)"""
    np, K = _np()
    sa, sb = a[:, 3] >= K.ALPHA_SOLID, b[:, 3] >= K.ALPHA_SOLID
    la, lb = K.rgb_to_lab(a[:, :3]), K.rgb_to_lab(b[:, :3])
    h = len(sa)
    vals, unmatched, off = [], 0, np.zeros(h, dtype=bool)
    for (s1, l1), (s2, l2) in (((sa, la), (sb, lb)), ((sb, lb), (sa, la))):
        best = np.full(h, np.inf, dtype=np.float32)
        for dy in range(-tol, tol + 1):
            src = slice(max(0, dy), h + min(0, dy))
            dst = slice(max(0, -dy), h + min(0, -dy))
            d = np.full(h, np.inf, dtype=np.float32)
            d[dst] = np.where(s2[src], np.linalg.norm(l2[src] - l1[dst], axis=-1), np.inf)
            best = np.minimum(best, d)
        unmatched += int((s1 & ~np.isfinite(best)).sum())
        vals.append(best[s1 & np.isfinite(best)])
        off |= s1 & (best > de)
    v = np.concatenate(vals)
    edges = np.flatnonzero(np.diff(np.concatenate([[0], off.astype(np.int8), [0]])))
    step = int((edges[1::2] - edges[::2]).max()) if len(edges) else 0
    return (float(np.percentile(v, 95)) if len(v) else 0.0), unmatched, step


def seam_check(left, right, band=4, tol=2, de=12.0):
    """judge the seam between the right edge of `left` and the left edge of `right` (RGBA float arrays).
    Returns {'ok', 'de', 'limit', 'rows', 'rows_limit', 'inside'}"""
    np, _ = _np()
    if left.shape[0] != right.shape[0]:
        raise P.InputError(f'heights differ: {left.shape[0]} and {right.shape[0]}')
    band = max(2, min(band, left.shape[1], right.shape[1]))
    d, rows, step = _edge_diff(left[:, -1], right[:, 0], tol, de)
    inside = [_edge_diff(m[:, i], m[:, i + 1], tol, de) for m in (left[:, -band:], right[:, :band]) for i in range(band - 1)]
    base = float(np.median([x[0] for x in inside]))
    rows_limit = max(x[1] for x in inside)
    step_limit = max(2 * tol + 1, 2 * max(x[2] for x in inside))
    limit = max(de, 2 * base)
    return {'ok': d <= limit and rows <= rows_limit and step <= step_limit, 'de': round(d, 1), 'limit': round(limit, 1), 'rows': rows,
            'rows_limit': rows_limit, 'step': step, 'step_limit': step_limit, 'inside': round(base, 1)}


def _seam_text(r):
    return (f"colour dE95 {r['de']} (limit {r['limit']}, columns inside differ {r['inside']}), "
            f"longest step {r['step']} rows (limit {r['step_limit']}), shape {r['rows']} unmatched rows (limit {r['rows_limit']})")


def _load_rgba(path):
    np, _ = _np()
    try:
        return np.asarray(Image.open(path).convert('RGBA'), dtype=np.float32)
    except (OSError, ValueError) as e:
        raise P.InputError(f'{path}: {e}')


def strip(out, layers=(), tile_set=None, tile_map=None, band=4, tol=2, de=12.0, repeat=3, quiet=False):
    """returns (failed seam count, results). results: [(label, seam dict)]"""
    tile_set = dict(tile_set or {})
    rows, results, files = [], [], {}
    for f in layers:
        a = _load_rgba(f)
        r = seam_check(a, a, band, tol, de)
        results.append((f'{Path(f).name} | itself', r, [str(f)]))
        rows.append(([str(f)] * max(2, repeat), [r['ok']] * (max(2, repeat) - 1)))
        files[str(f)] = a
    if tile_map:
        letters = tile_map.split()
        unknown = sorted({c for c in letters if c not in tile_set})
        if unknown or len(letters) < 2:
            raise P.InputError(f'--map needs 2 or more letters, each given by --set LETTER=<png> (unknown: {unknown})')
        arr = {c: _load_rgba(tile_set[c]) for c in set(letters)}
        seen, oks = {}, []
        for a, b in zip(letters, letters[1:]):
            if (a, b) not in seen:
                seen[(a, b)] = seam_check(arr[a], arr[b], band, tol, de)
                results.append((f'{a}|{b} {Path(tile_set[a]).name} | {Path(tile_set[b]).name}', seen[(a, b)], [str(tile_set[a]), str(tile_set[b])]))
            oks.append(seen[(a, b)]['ok'])
        rows.append(([str(tile_set[c]) for c in letters], oks))
        files.update({str(tile_set[c]): arr[c] for c in letters})
    elif tile_set:
        raise P.InputError('--set needs --map "<letters>"')
    if not rows:
        raise P.InputError('nothing to lay out: give layer pictures and/or --set with --map')
    bad = [x for x in results if not x[1]['ok']]
    # sheet
    font, pad, line = _font(15), 12, 20
    width = 1500
    strips = []
    for names, oks in rows:
        ims = [Image.open(n).convert('RGBA') for n in names]
        h = max(i.height for i in ims)
        row = Image.new('RGBA', (sum(i.width for i in ims), h), (0, 0, 0, 0))
        x, xs = 0, []
        for i in ims:
            row.alpha_composite(i, (x, h - i.height))
            x += i.width
            xs.append(x)
        box = row.getbbox()
        if box:
            row = row.crop((0, max(0, box[1] - 16), row.width, row.height))       # leave out the empty sky above the layer
        s = min(1.0, width / row.width)
        small = row.resize((max(1, round(row.width * s)), max(1, round(row.height * s))), Image.LANCZOS) if s < 1 else row
        strips.append((small, [round(v * s) for v in xs[:-1]], oks))
    head = ['이음 시험 패턴 — 게임 화면 아님 (joining test pattern, not a game screen)',
            f'side-view strip: {len(results)} seam(s), band {band}px, shift tolerance {tol}px, dE limit {de:g}' + (f'; map "{tile_map}"' if tile_map else ''),
            f"seam check {'PASS' if not bad else 'FAIL'}: {len(results)} seams, {len(bad)} problems"]
    notes = [f"{'PASS' if r['ok'] else 'FAIL'} {label}: {_seam_text(r)}" for label, r, _ in results][:14]
    top = pad + line * (len(head) + len(notes)) + pad
    tick = 14
    canvas = Image.new('RGBA', (max(760, max(s.width for s, _, _ in strips) + pad * 2),
                                top + sum(s.height + tick + pad for s, _, _ in strips)), (245, 245, 245, 255))
    d = ImageDraw.Draw(canvas)
    d.rectangle([0, 0, canvas.width, 6], fill=GREEN if not bad else RED)
    for i, t in enumerate(head + notes):
        d.text((pad, pad + i * line), t, fill=(RED if bad else GREEN) if i == 2 else (RED if t.startswith('FAIL') else INK), font=font)
    y = top
    for small, xs, oks in strips:
        d.rectangle([pad, y + tick, pad + small.width - 1, y + tick + small.height - 1], fill=(205, 215, 225, 255))
        canvas.alpha_composite(small, (pad, y + tick))
        for x, ok in zip(xs, oks):
            d.rectangle([pad + x - 1, y, pad + x + 1, y + tick - 2], fill=GREEN if ok else RED)
        y += small.height + tick + pad
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    canvas.convert('RGB').save(out)
    if not quiet:
        print(head[2])
        for n in notes:
            print('  ', n)
        print(out)
    # records: a picture is FAIL if any seam it takes part in failed
    failed = {f for _, r, fs in results if not r['ok'] for f in fs}
    try:
        import kit2d
        kit = kit2d.load_kit(Path(next(iter(files))).resolve().parent)
        shas = {f: kit2d.sha256(f) for f in files}
        for f, sha in shas.items():
            kit2d.write_record(kit, {'schema': kit2d.MEASURE_SCHEMA, 'kind': 'seam', 'mode': 'strip', 'image': str(Path(f).resolve()), 'sha256': sha,
                                     'verdict': 'FAIL' if f in failed else 'PASS', 'set': sorted(shas.values()), 'cells': len(results),
                                     'problems': len(bad), 'sheet': str(Path(out).resolve())})
    except Exception:  # noqa: BLE001  outside a kit project nothing is recorded
        pass
    return len(bad), [(label, r) for label, r, _ in results]


def roll(src, dst):
    np, _ = _np()
    a = np.asarray(Image.open(src).convert('RGBA'))
    Path(dst).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.roll(a, a.shape[1] // 2, axis=1), 'RGBA').save(dst)
    print(f'ROLL {dst}: shifted {a.shape[1] // 2}px; the old edge seam is now at x={a.shape[1] // 2}')
    return 0


def mend(rolled, redrawn, out, band=0.2, palette=True, tol=2, de=12.0, quiet=False):
    np, K = _np()
    base, new = _load_rgba(rolled), _load_rgba(redrawn)
    if base.shape != new.shape:
        raise P.InputError(f'the redrawn picture is {new.shape[1]}x{new.shape[0]}, the rolled one {base.shape[1]}x{base.shape[0]}: '
                           'resize it to the same canvas first')
    if not 0 < band < 1:
        raise P.InputError('--band is a fraction of the width, between 0 and 1')
    w = base.shape[1]
    x0, x1 = round(w * (0.5 - band / 2)), round(w * (0.5 + band / 2))
    differ = np.abs(base - new).max(axis=-1)
    same_col = (differ <= 8).mean(axis=0) >= 0.995
    # cut where the redraw still equals the rolled picture (two columns running): that border joins by construction
    cuts, built = [], []
    for start, stop, step in ((x0, w // 2, 1), (x1, w // 2, -1)):
        hit = next((x for x in range(start, stop, step) if same_col[x] and same_col[x + step]), None)
        cuts.append(start if hit is None else hit + (1 if step > 0 else 0))
        built.append(hit is not None)
    cl, cr = cuts
    outside = np.ones(w, dtype=bool)
    outside[x0:x1] = False
    unused = (differ > 8)[:, outside]
    merged = base.copy()
    merged[:, cl:cr] = new[:, cl:cr]
    snapped = 0
    if palette:
        solid = base[..., 3] >= K.ALPHA_SOLID
        rgb8 = base[..., :3][solid].astype(np.int64)
        if len(rgb8):
            code = rgb8 // 4 @ np.array([4096, 64, 1])
            codes, counts = np.unique(code, return_counts=True)
            keep = codes[counts >= 0.005 * len(code)]                  # the flat paints of the picture, not its gradients
            pal = np.array([np.median(rgb8[code == c], axis=0) for c in keep], dtype=np.float32).reshape(-1, 3)
            part = merged[:, cl:cr]
            m = (part[..., 3] >= K.ALPHA_SOLID) & (differ[:, cl:cr] > 2)       # only pixels the redraw touched
            if len(pal) and m.any():
                px = part[..., :3][m]
                near, best = np.zeros(len(px), dtype=np.int64), np.full(len(px), np.inf, dtype=np.float32)
                lab, pal_lab = K.rgb_to_lab(px), K.rgb_to_lab(pal)
                for i in range(len(pal)):
                    d = np.linalg.norm(lab - pal_lab[i], axis=-1)
                    near[d < best] = i
                    best = np.minimum(best, d)
                close = best <= 6                 # flat paints snap; in-between (anti-aliased) pixels are left alone
                snapped = int((close & (np.abs(px - pal[near]).max(axis=-1) > 0.5)).sum())
                px[close] = pal[near][close]
                part[..., :3][m] = px
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(merged + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(out)
    seams = []
    for name, x, ok in (('band left border', cl, built[0]), ('band right border', cr, built[1])):
        if ok:
            seams.append((f'{name} x={x}', None))
        else:
            seams.append((f'{name} x={x}', seam_check(merged[:, :x], merged[:, x:], tol=tol, de=de)))
    seams.append(('loop (right edge | left edge)', seam_check(merged, merged, tol=tol, de=de)))
    bad = [n for n, r in seams if r is not None and not r['ok']]
    if not quiet:
        print(f'MEND {out}: x={cl}..{cr} taken from the redrawn picture, the rest from the rolled one; '
              f'{int(unused.sum())} px the redraw changed outside the band x={x0}..{x1} were not used ({float(unused.mean()):.1%} of it); '
              f'{snapped} redrawn px snapped to the picture\'s flat paints')
        for n, r in seams:
            print(f"  PASS {n}: the redraw equals the rolled picture here (joins by construction)" if r is None else
                  f"  {'PASS' if r['ok'] else 'FAIL'} {n}: {_seam_text(r)}")
        print(f"{'PASS' if not bad else 'FAIL'} mend")
    return len(bad)


def _strip_art(w=256, h=96, phase=0.0, cycles=4.0, soil=(120, 80, 50), shift=0, stripes=True):
    """side-view ground: grass hills with a dark outline that crosses any column at a slant, slanted soil stripes below"""
    import math
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    px = im.load()
    for x in range(w):
        top = 34 + shift + 22 * math.sin(2 * math.pi * cycles * x / w + phase)
        for y in range(h):
            if y < top:
                continue
            if y < top + 4:
                px[x, y] = (30, 14, 8, 255)
            elif y < 70:
                px[x, y] = (70, 170, 60, 255)
            else:
                px[x, y] = (60, 36, 20, 255) if stripes and (x * 3 + y * 2) % 16 < 6 else soil + (255,)
    return im


def selftest():
    cases = []

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    td = Path(tempfile.mkdtemp(prefix='tileprev-selftest-'))
    tiles, _ = P.make_all(P._straight())

    def save(label, ts, prefix='tile_path'):
        d = td / label
        d.mkdir()
        for k, im in ts.items():
            im.save(d / f'{prefix}_{k}.png')
        return d

    cells = layout(parse_map(PATTERN))
    case('the default pattern uses all 10 tiles', set(cells.values()) == set(P.OPEN), ', '.join(sorted(set(cells.values()))))
    good = save('good', tiles)
    problems, used = sheet(td / 'good.png', good)
    im = Image.open(td / 'good.png')
    case('derived tile set: seams PASS and the sheet holds the x4 and x1 maps', not problems and len(used) == 10
         and im.width >= 9 * 20 * 4 + 9 * 20 and im.height >= 8 * 20 * 4)
    case('exit 0 through the command line', main(['sheet', str(td / 'cli.png'), '--tiles', str(good), '--scale', '2']) == 0)
    # the failure this tool is for: one straight tile drawn on every path cell
    lazy = save('lazy', {k: tiles['h'] for k in P.OPEN})
    problems, _ = sheet(td / 'lazy.png', lazy)
    case('straight tile reused for corners, ends and the vertical: seams FAIL', len(problems) >= 10 and (td / 'lazy.png').is_file(),
         f'{len(problems)} problems')
    case('exit 1 for it through the command line', main(['sheet', str(td / 'lazy2.png'), '--tiles', str(lazy)]) == 1)
    # inside a kit project the seam verdict is recorded per tile (sheet.py grid --tiles reads it); outside, nothing is written
    try:
        import json
        import kit2d
        (td / '.ai/2d-art').mkdir(parents=True)
        (td / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'work_dir': 'out'}))
        kit = kit2d.load_kit(td)
        sheet(td / 'good-rec.png', good)
        sheet(td / 'lazy-rec.png', lazy)
        good_rec = kit2d.read_record(kit, kit2d.sha256(good / 'tile_path_ne.png'), 'seam')
        lazy_rec = kit2d.read_record(kit, kit2d.sha256(lazy / 'tile_path_h.png'), 'seam')
        case('kit project: seam record PASS for the good set, FAIL for the lazy set',
             good_rec and good_rec['verdict'] == 'PASS' and len(good_rec['set']) == 10 and lazy_rec and lazy_rec['verdict'] == 'FAIL')
    except ImportError:
        print('SKIP seam record case (kit2d needs numpy)')
    shifted = dict(tiles)
    sh = Image.new('RGBA', (20, 20), tiles['v'].getpixel((0, 0)))
    sh.paste(tiles['v'].crop((0, 0, 17, 20)), (3, 0))
    shifted['v'] = sh
    problems, _ = sheet(td / 'shift.png', save('shift', shifted))
    case('a vertical tile drawn 3px to the side: seams FAIL where it meets the corners and ends',
         any('| ' in p and 'meets' in p for p in problems), f'{len(problems)} problems')
    part = save('part', {k: v for k, v in tiles.items() if not k.startswith('end_')})
    case('missing end tiles: exit 2', main(['sheet', str(td / 'part.png'), '--tiles', str(part)]) == 2)
    (td / 'loop.txt').write_text('....\n.##.\n.##.\n....\n')
    case('with a map that needs no ends the same folder passes', main(['sheet', str(td / 'loop.png'), '--tiles', str(part), '--map', str(td / 'loop.txt')]) == 0)
    (td / 'tee.txt').write_text('.#.\n###\n...\n')
    case('a T-junction in the map: exit 2', main(['sheet', str(td / 'tee.png'), '--tiles', str(good), '--map', str(td / 'tee.txt')]) == 2)
    clear, _ = P.make_all(P._straight(opaque=False))
    Image.new('RGBA', (20, 20), (68, 186, 10, 255)).save(td / 'grass.png')
    problems, _ = sheet(td / 'clear.png', save('clear', clear, 'tile_road'), prefix='tile_road', ground=[td / 'grass.png'])
    px = Image.open(td / 'clear.png').convert('RGBA')
    case('transparent tiles over a ground tile: seams PASS and the ground shows through', not problems
         and (68, 186, 10, 255) in {px.getpixel((x, px.height - 14)) for x in range(12, 60)})
    try:
        import numpy as np
        import kit2d
    except ImportError:
        print('SKIP strip cases (need numpy)')
    else:
        sd = td / 'strip'
        sd.mkdir()

        def put(name, im):
            im.save(sd / name)
            return sd / name
        loop = put('loop.png', _strip_art())
        a = np.asarray(Image.open(loop), dtype=np.float32)
        r = seam_check(a, a)
        one_px = _edge_diff(a[:, -1], a[:, 0], 0)[:2]
        case('a strip that repeats (slanted outline and stripes across the seam): PASS, where a 1px column comparison fails it',
             r['ok'] and (one_px[0] > 12 or one_px[1] > 0), f"{_seam_text(r)}; 1px column: dE95 {one_px[0]:.1f}, {one_px[1]} rows")
        jump = put('jump.png', _strip_art(cycles=4.3))
        soil = Image.open(loop).copy()
        soil.paste(_strip_art(soil=(180, 130, 100), stripes=False).crop((200, 0, 256, 96)), (200, 0))
        soil = put('soil.png', soil)
        n_bad, res = strip(sd / 'layers.png', [loop, jump, soil], quiet=True)
        case('hill line that does not meet itself: FAIL (shape); soil of another colour on one side: FAIL (colour)',
             n_bad == 2 and res[0][1]['ok'] and res[1][1]['rows'] > res[1][1]['rows_limit'] and res[2][1]['de'] > res[2][1]['limit'],
             '; '.join(_seam_text(x[1]) for x in res[1:]))
        band_jump = Image.open(loop).copy()
        from PIL import ImageDraw as _D
        _D.Draw(band_jump).rectangle([0, 71, 40, 84], fill=(150, 60, 110, 255))      # a shadow band 14 rows thick that stops at the right edge
        r = seam_check(*[np.asarray(band_jump, dtype=np.float32)] * 2)
        case('a soil shadow band that stops at the seam (14 rows): the step is found and FAILs',
             not r['ok'] and r['step'] == 14 and r['step'] > r['step_limit'], _seam_text(r))
        top = Image.open(sd / 'layers.png').convert('RGB').getpixel((5, 3))
        case('the sheet is written with a red band when a seam fails', top == RED)
        # ground set by letter map: G middle, L pit left end, P pit, R pit right end
        g = _strip_art()
        flat = Image.new('RGBA', (64, 96), (0, 0, 0, 0))
        tiles_ = {'G': put('g.png', g), 'P': put('p.png', flat)}
        left = g.copy()
        left.paste((0, 0, 0, 0), (200, 0, 256, 96))
        right = g.copy()
        right.paste((0, 0, 0, 0), (0, 0, 56, 96))
        tiles_['L'], tiles_['R'] = put('l.png', left), put('r.png', right)
        n_bad, res = strip(sd / 'set.png', tile_set=tiles_, tile_map='G G L P R G', quiet=True)
        case('ground set "G G L P R G": 5 kinds of seam, all PASS', n_bad == 0 and len(res) == 5, ', '.join(x[0].split()[0] for x in res))
        tiles_['R'] = put('r_high.png', _strip_art(shift=-9).crop((0, 0, 256, 96)))
        n_bad, res = strip(sd / 'set_bad.png', tile_set=tiles_, tile_map='G G L P R G', quiet=True)
        case('pit right end drawn 9px higher than the middle tile: its seam to G FAILs', n_bad >= 1 and not dict((x[0].split()[0], x[1]) for x in res)['R|G']['ok'])
        case('exit codes through the command line: 0, 1, 2 (unknown letter)', main(['strip', str(sd / 'c0.png'), str(loop)]) == 0
             and main(['strip', str(sd / 'c1.png'), str(jump)]) == 1
             and main(['strip', str(sd / 'c2.png'), '--set', f'G={loop}', '--map', 'G X']) == 2)
        # kit project: seam records, as `sheet` writes them
        proj = td / 'strip-proj'
        (proj / '.ai/2d-art').mkdir(parents=True)
        (proj / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'work_dir': 'out'}))
        pl, pj = proj / 'loop.png', proj / 'jump.png'
        Image.open(loop).save(pl)
        Image.open(jump).save(pj)
        strip(proj / 'sheet.png', [pl, pj], quiet=True)
        kit = kit2d.load_kit(proj)
        r1, r2 = kit2d.read_record(kit, kit2d.sha256(pl), 'seam'), kit2d.read_record(kit, kit2d.sha256(pj), 'seam')
        case('kit project: seam record PASS for the repeating strip, FAIL for the other (sheet.py grid --tiles reads it)',
             r1 and r2 and r1['verdict'] == 'PASS' and r2['verdict'] == 'FAIL' and r1['mode'] == 'strip')
        # mending: roll, redraw the middle band, mend
        rolled = sd / 'jump_rolled.png'
        roll(jump, rolled)
        ra = np.asarray(Image.open(rolled)).copy()
        case('roll: the bad seam is in the middle, the edges now join', seam_check(ra.astype(np.float32), ra.astype(np.float32))['ok']
             and not seam_check(ra[:, :128].astype(np.float32), ra[:, 128:].astype(np.float32))['ok'])
        redo = ra.astype(np.int16)
        xs = np.arange(96, 160)
        t = (xs - 96) / 63.0
        tops = (1 - t) * np.argmax(ra[:, 95, 3] > 0) + t * np.argmax(ra[:, 160, 3] > 0)    # the hill line bridged across the band
        for x, top_y in zip(xs, tops):
            col = np.zeros((96, 4), dtype=np.int16)
            ys = np.arange(96)
            col[ys >= top_y] = (70, 170, 60, 255)
            col[(ys >= top_y) & (ys < top_y + 4)] = (30, 14, 8, 255)
            col[ys >= 70] = (120, 80, 50, 255)
            col[(ys >= 70) & ((int(x) * 3 + ys * 2 + 384) % 16 < 6)] = (60, 36, 20, 255)   # rolled by 128px: stripe phase 3*128
            redo[:, x] = col
        solid = redo[..., 3] > 0
        redo[..., :3][solid] += 5                                                             # the redraw drifts in colour, everywhere
        redo = np.clip(redo, 0, 255)
        redo[5:9, 5:9] = (200, 0, 0, 255)                                                     # and scribbles outside the band
        Image.fromarray(redo.astype(np.uint8), 'RGBA').save(sd / 'redo.png')
        rc = mend(rolled, sd / 'redo.png', sd / 'mended.png', band=0.25, quiet=True)
        ma = np.asarray(Image.open(sd / 'mended.png'))
        case('mend: PASS, pixels outside the band are the rolled picture\'s, band colours snapped back to the picture\'s paints',
             rc == 0 and np.array_equal(ma[:, :96], ra[:, :96]) and np.array_equal(ma[:, 160:], ra[:, 160:])
             and np.abs(ma[80, 128, :3].astype(int) - (60, 36, 20)).max() <= 2 and np.abs(ma[60, 128, :3].astype(int) - (70, 170, 60)).max() <= 2, str(ma[80, 128].tolist()))
        case('the mended strip passes strip', strip(sd / 'mended_sheet.png', [sd / 'mended.png'], quiet=True)[0] == 0)
        case('mend with a redraw of another size: exit 2', main(['mend', str(rolled), str(loop.parent / 'p.png'), str(sd / 'x.png')]) == 2)
    bad = cases.count(False)
    print(f"tileprev selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('sheet')
    s.add_argument('out')
    s.add_argument('--tiles', required=True)
    s.add_argument('--prefix', default='tile_path')
    s.add_argument('--ground', nargs='*', default=[])
    s.add_argument('--map')
    s.add_argument('--scale', type=int, default=4)
    st = sub.add_parser('strip')
    st.add_argument('out')
    st.add_argument('layers', nargs='*')
    st.add_argument('--set', nargs='*', default=[], metavar='LETTER=PNG')
    st.add_argument('--map')
    st.add_argument('--band', type=int, default=4)
    st.add_argument('--repeat', type=int, default=3)
    r_ = sub.add_parser('roll')
    r_.add_argument('src')
    r_.add_argument('dst')
    m_ = sub.add_parser('mend')
    m_.add_argument('rolled')
    m_.add_argument('redrawn')
    m_.add_argument('out')
    m_.add_argument('--band', type=float, default=0.2)
    m_.add_argument('--no-palette', action='store_true')
    for x in (st, m_):
        x.add_argument('--tol', type=int, default=2)
        x.add_argument('--de', type=float, default=12.0)
    sub.add_parser('selftest')
    a = ap.parse_args(argv)
    if a.cmd == 'selftest':
        return selftest()
    try:
        if a.cmd in ('strip', 'roll', 'mend'):
            given = [a.src] if a.cmd == 'roll' else [a.rolled, a.redrawn] if a.cmd == 'mend' else list(a.layers)
            tile_set = {}
            for item in getattr(a, 'set', []):
                letter, sep, path = item.partition('=')
                if not sep or not letter or ' ' in letter:
                    raise P.InputError(f'--set wants LETTER=<png>, got {item}')
                tile_set[letter] = path
            missing = [str(f) for f in given + list(tile_set.values()) if not Path(f).is_file()]
            if missing:
                raise P.InputError('no file ' + ', '.join(missing))
            if a.cmd == 'roll':
                return roll(a.src, a.dst)
            if a.cmd == 'mend':
                return 1 if mend(a.rolled, a.redrawn, a.out, a.band, not a.no_palette, max(0, a.tol), a.de) else 0
            return 1 if strip(a.out, a.layers, tile_set, a.map, a.band, max(0, a.tol), a.de, a.repeat)[0] else 0
        if a.map and not Path(a.map).is_file():
            raise P.InputError(f'no map file {a.map}')
        problems, _ = sheet(a.out, a.tiles, a.prefix, a.ground, a.map, max(1, a.scale))
        return 1 if problems else 0
    except P.InputError as e:
        print(f'INPUT: {e}')
        return 2


if __name__ == '__main__':
    sys.exit(main())
