#!/usr/bin/env python3
"""Paint (fill) colours: measure saturation and value per colour family, judge them against the adopted style sample or
neighbouring game pictures, and pull a family to a target without touching the outline (2d-art-produce "칠 색").

  paint.py measure <png>... [--ref <png>...] [--tol 0.05] [--luma-min 110] [--class name=h0..h1[:s0..s1]]... [--json <out.json>]
  paint.py pull <png>... --out <dir> --family <name> [--s <0..1>] [--v <0..1>] [--min-diff 0.05] [--luma-min 110] [--key #RRGGBB] [--class name=…]...
  paint.py selftest

Why: an image generator does not follow the hex it is told. In the source case it drew wood at saturation 0.33-0.46 where
the reference was 0.27-0.30, at every style strength, and nothing measured it: outline colour, stroke and size all passed
and the user said the props did not follow the reference at all.
Paint = opaque pixels (alpha >= 250) brighter than --luma-min (above the outline; the default 110 suits a dark brown or
black outline - set it well above your outline's luma). Families by HSV hue in degrees:
  red 345..15  orange 15..50  yellow 50..70  green 70..170  cyan 170..200  blue 200..260  purple 260..345, saturation > 0.12
  neutral: saturation <= 0.12 (any hue)
--class name=h0..h1[:s0..s1] replaces them (repeatable), e.g. wood=15..50:0.12..0.35 fruit=15..50:0.35..1 to split one hue.
measure prints, per picture and family, the median saturation S and value V and the family's share of the paint (key
  colour left inside the picture is matte.py fringe --whole, not this tool). With --ref (the adopted style sample, or game pictures
  that share the screen) each family present on both sides is judged: |dS| and |dV| <= --tol, else FAIL. A family the
  references do not have is shown as "no reference". Give references of the SAME kind of object: a leaf against a leaf.
pull scales S (and V) of one family (--family) so its median lands on the target. A value is changed only when it is more than
  --min-diff away (small differences are left alone - every recolour costs rim quality). The outline, other families and
  alpha are untouched. Recolouring shifts the paint under a keyed rim, so the rim can read as key colour again: when the
  key is known (--key, or the chunk matte.py key wrote) pull re-runs the fringe check on what it wrote and exits 1 on
  FAIL -> matte.py fix, then fringe again. Without a key it says the fringe check was not run.
Inside a kit project measure --ref writes work_dir/measure/<sha256>.paint.json. sheet.py does not gate on it: the limit is
a starting value (calibrate it on accepted and rejected pictures before making it a gate). Needs no art spec or identity.
Exit: 0 PASS (pull: written, fringe not FAIL) / 1 FAIL / 2 input problem. Needs numpy and Pillow."""
import argparse
import json
import re
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402

OPAQUE = 250
NEUTRAL_S = 0.12
MIN_PX, MIN_SHARE = 50, 0.005
DEFAULT_CLASSES = {'red': (345, 15, NEUTRAL_S, 1.0), 'orange': (15, 50, NEUTRAL_S, 1.0), 'yellow': (50, 70, NEUTRAL_S, 1.0),
                   'green': (70, 170, NEUTRAL_S, 1.0), 'cyan': (170, 200, NEUTRAL_S, 1.0), 'blue': (200, 260, NEUTRAL_S, 1.0),
                   'purple': (260, 345, NEUTRAL_S, 1.0), 'neutral': (0, 360, 0.0, NEUTRAL_S)}
CLASS_RE = re.compile(r'^([^=\s]+)=(-?[\d.]+)\.\.(-?[\d.]+)(?::([\d.]+)\.\.([\d.]+))?$')


def to_hsv(rgb):
    """rgb 0..255 float [...,3] -> h degrees 0..360, s 0..1, v 0..1"""
    c = rgb / 255.0
    mx, mn = c.max(-1), c.min(-1)
    d = np.maximum(mx - mn, 1e-6)
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    h = np.where(mx - mn < 1e-6, 0, h % 360)
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def to_rgb(h, s, v):
    hh = (h % 360) / 60
    i = np.floor(hh).astype(int) % 6
    f = hh - np.floor(hh)
    p, q, t = v * (1 - s), v * (1 - s * f), v * (1 - s * (1 - f))
    sel = [(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]
    out = np.zeros(h.shape + (3,), dtype=np.float32)
    for k, (a, b, c) in enumerate(sel):
        m = i == k
        out[m, 0], out[m, 1], out[m, 2] = a[m], b[m], c[m]
    return out * 255


def parse_classes(items):
    if not items:
        return dict(DEFAULT_CLASSES)
    out = {}
    for it in items:
        m = CLASS_RE.match(it)
        if not m:
            raise K.KitError('BAD_CLASS', f'--class wants name=h0..h1[:s0..s1], got {it}')
        out[m[1]] = (float(m[2]) % 360, float(m[3]) % 360 if float(m[3]) != 360 else 360.0,
                     float(m[4]) if m[4] else NEUTRAL_S, float(m[5]) if m[5] else 1.0)
    return out


def class_mask(h, s, paint, spec):
    h0, h1, s0, s1 = spec
    hue = (h >= h0) & (h < h1) if h0 < h1 else (h >= h0) | (h < h1)
    return paint & hue & (s > s0 if s0 > 0 else s >= 0) & (s <= s1)


def families(img, classes, luma_min):
    rgb, a = img[..., :3], img[..., 3]
    paint = (a >= OPAQUE) & (K.luma(rgb) >= luma_min)
    h, s, v = to_hsv(rgb)
    total = max(1, int(paint.sum()))
    out = {}
    for name, spec in classes.items():
        m = class_mask(h, s, paint, spec)
        n = int(m.sum())
        if n >= MIN_PX and n / total >= MIN_SHARE:
            out[name] = {'S': float(np.median(s[m])), 'V': float(np.median(v[m])), 'share': n / total, 'px': n, 'mask': m}
    return out, int(paint.sum())


def cmd_measure(paths, refs=None, tol=0.05, luma_min=110.0, classes=None, json_out=None, quiet=False):
    classes = classes or dict(DEFAULT_CLASSES)
    ref = {}
    if refs:
        pool = {}
        for r in refs:
            img = K.load_rgba(r)
            fam, _ = families(img, classes, luma_min)
            _, s, v = to_hsv(img[..., :3])
            for name, f in fam.items():
                pool.setdefault(name, ([], []))
                pool[name][0].append(s[f['mask']])
                pool[name][1].append(v[f['mask']])
        ref = {n: {'S': float(np.median(np.concatenate(a))), 'V': float(np.median(np.concatenate(b)))} for n, (a, b) in pool.items()}
        if not ref:
            raise K.KitError('NO_REF_PAINT', 'the reference pictures have no paint in any family (check --luma-min and --class)')
        if not quiet:
            print('REF  ' + '  '.join(f"{n} S{f['S']:.2f}/V{f['V']:.2f}" for n, f in ref.items()))
    bad, report = 0, {}
    for p in paths:
        fam, npaint = families(K.load_rgba(p), classes, luma_min)
        if not fam:
            raise K.KitError('NO_PAINT', f'{Path(p).name}: no paint pixels in any family (alpha >= {OPAQUE}, luma >= {luma_min:g})')
        row, why, cells = {}, [], []
        for name, f in fam.items():
            row[name] = {'S': round(f['S'], 3), 'V': round(f['V'], 3), 'share': round(f['share'], 3)}
            cell = f"{name} S{f['S']:.2f}/V{f['V']:.2f}({f['share']:.0%})"
            if ref:
                if name in ref:
                    ds, dv = f['S'] - ref[name]['S'], f['V'] - ref[name]['V']
                    row[name].update(dS=round(ds, 3), dV=round(dv, 3))
                    cell += f"[dS{ds:+.2f} dV{dv:+.2f}]"
                    if abs(ds) > tol or abs(dv) > tol:
                        why.append(f"{name} dS{ds:+.2f} dV{dv:+.2f}")
                else:
                    cell += '[no reference]'
            cells.append(cell)
        verdict = None
        if ref:
            judged = [n for n in fam if n in ref]
            verdict = 'NOT_ASSESSED' if not judged else 'FAIL' if why else 'PASS'
            bad += verdict != 'PASS'
            try:
                kit = K.load_kit(Path(p).resolve().parent)
                K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': 'paint', 'image': str(Path(p).resolve()), 'sha256': K.sha256(p),
                                     'verdict': verdict, 'tol': tol, 'luma_min': luma_min, 'families': row,
                                     'refs': [str(Path(r).resolve()) for r in refs]})
            except K.KitError:
                pass
        report[str(p)] = {'paint_px': npaint, 'families': row, **({'verdict': verdict} if verdict else {})}
        if not quiet:
            head = f'{verdict} ' if verdict else ''
            tail = f"  ({'; '.join(why)} over ±{tol:g})" if why else ('  (no family in common with the references)' if verdict == 'NOT_ASSESSED' else '')
            print(f"{head}{Path(p).name}: " + '  '.join(cells) + tail)
    if json_out:
        Path(json_out).parent.mkdir(parents=True, exist_ok=True)
        Path(json_out).write_text(json.dumps({'ref': ref, 'pictures': report}, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    if ref and not quiet:
        print(f"{'FAIL' if bad else 'PASS'} paint: {len(paths)} pictures, {bad} off the references" + (' -> paint.py pull --family <name> --s/--v <reference value>' if bad else ''))
    return 1 if bad else 0


def pull_array(img, spec, target_s=None, target_v=None, min_diff=0.05, luma_min=110.0):
    """(new image, info). Only the family's paint pixels change; None if the family is not in the picture."""
    rgb, a = img[..., :3], img[..., 3]
    paint = (a >= OPAQUE) & (K.luma(rgb) >= luma_min)
    h, s, v = to_hsv(rgb)
    m = class_mask(h, s, paint, spec)
    if int(m.sum()) < MIN_PX:
        return None, None
    ms, mv = float(np.median(s[m])), float(np.median(v[m]))
    ks = target_s / ms if target_s is not None and abs(ms - target_s) > min_diff and ms > 0 else 1.0
    kv = target_v / mv if target_v is not None and abs(mv - target_v) > min_diff and mv > 0 else 1.0
    out = img.copy()
    if ks != 1.0 or kv != 1.0:
        new = to_rgb(h, np.clip(s * ks, 0, 1), np.clip(v * kv, 0, 1))
        out[..., :3] = np.where(m[..., None], new, rgb)
    return out, {'S': ms, 'V': mv, 'kS': ks, 'kV': kv, 'px': int(m.sum())}


def cmd_pull(paths, out_dir, name, target_s=None, target_v=None, min_diff=0.05, luma_min=110.0, classes=None, key=None, quiet=False):
    import matte as MT
    classes = classes or dict(DEFAULT_CLASSES)
    if name not in classes:
        raise K.KitError('NO_CLASS', f'{name} is not one of {", ".join(classes)}')
    if target_s is None and target_v is None:
        raise K.KitError('NO_TARGET', 'give --s and/or --v (the reference value from paint.py measure --ref)')
    bad = 0
    for p in paths:
        out, info = pull_array(K.load_rgba(p), classes[name], target_s, target_v, min_diff, luma_min)
        if out is None:
            raise K.KitError('CLASS_NOT_FOUND', f'{Path(p).name}: no {name} paint (fewer than {MIN_PX} px)')
        try:
            kc = MT.read_key(p, key)
        except K.KitError:
            kc = None
        dst = Path(out_dir) / Path(p).name
        if kc is not None:
            MT.save(out, dst, kc)
            an = MT.analyse(K.load_rgba(dst), kc)
            counts = {n: int(an[n].sum()) for n in ('spill', 'edge', 'stray')}
            fail = any(counts.values())
            MT.record(dst, 'FAIL' if fail else 'PASS', counts, kc, 0.10, 2, 0)
            fringe = f"fringe {'FAIL' if fail else 'PASS'} (spill {counts['spill']}, edge {counts['edge']}, stray {counts['stray']} px)" + (' -> matte.py fix, then fringe again' if fail else '')
            bad += fail
        else:
            dst.parent.mkdir(parents=True, exist_ok=True)
            Image.fromarray(np.clip(out + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(dst)
            fringe = 'fringe NOT_RUN (no key colour known: --key #RRGGBB, or run matte.py fringe yourself)'
        if not quiet:
            same = ' (inside --min-diff: unchanged)' if info['kS'] == 1.0 and info['kV'] == 1.0 else ''
            print(f"PULL {dst}: {name} S {info['S']:.3f} x{info['kS']:.3f}, V {info['V']:.3f} x{info['kV']:.3f}, {info['px']} px{same}; {fringe}")
    return 1 if bad else 0


# ---------- selftest ----------

def _disc(fill, outline=(60, 38, 22), size=160, extra=None):
    """outlined disc on transparent; drawn 4x and scaled down. extra = (fill, box) second blob."""
    from PIL import ImageDraw
    n = size * 4
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([n * 0.12, n * 0.12, n * 0.88, n * 0.88], fill=outline + (255,))
    d.ellipse([n * 0.17, n * 0.17, n * 0.83, n * 0.83], fill=tuple(fill) + (255,))
    if extra:
        d.rectangle([n * extra[1][0], n * extra[1][1], n * extra[1][2], n * extra[1][3]], fill=tuple(extra[0]) + (255,))
    return im.resize((size, size), Image.LANCZOS)


def _hsv_rgb(h, s, v):
    return tuple(int(round(c)) for c in to_rgb(np.array([float(h)]), np.array([float(s)]), np.array([float(v)]))[0])


def selftest():
    td = Path(tempfile.mkdtemp(prefix='paint-selftest-'))
    cases = []

    def case(label, ok, detail=''):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}" + (f' :: {detail}' if detail else ''))

    ref, hot, both = td / 'ref.png', td / 'hot.png', td / 'both.png'
    _disc(_hsv_rgb(120, 0.40, 0.80)).save(ref)
    _disc(_hsv_rgb(120, 0.52, 0.78)).save(hot)
    _disc(_hsv_rgb(120, 0.52, 0.78), extra=(_hsv_rgb(30, 0.60, 0.90), (0.40, 0.40, 0.60, 0.60))).save(both)
    fam, _ = families(K.load_rgba(hot), DEFAULT_CLASSES, 110)
    case('measure: one green family, S 0.52 V 0.78, the dark outline is not paint', set(fam) == {'green'} and abs(fam['green']['S'] - 0.52) < 0.02 and abs(fam['green']['V'] - 0.78) < 0.02,
         str({k: (round(f['S'], 2), round(f['V'], 2)) for k, f in fam.items()}))
    case('same picture against itself PASS', cmd_measure([ref], [ref], quiet=True) == 0)
    case('counter: green drawn at S 0.52 against a 0.40 reference FAIL (the generator ignored the hex)', cmd_measure([hot], [ref], quiet=True) == 1)
    case('a wider --tol passes it', cmd_measure([hot], [ref], tol=0.15, quiet=True) == 0)
    case('pull green to S 0.40: exit 0 without a key, then measure PASS',
         cmd_pull([hot], td / 'out', 'green', 0.40, 0.80, quiet=True) == 0 and cmd_measure([td / 'out/hot.png'], [ref], quiet=True) == 0)
    a, b = K.load_rgba(hot), K.load_rgba(td / 'out/hot.png')
    dark = K.luma(a[..., :3]) < 110
    case('pull leaves the outline and alpha untouched', np.array_equal(a[dark], b[dark]) and np.array_equal(a[..., 3], b[..., 3]) and not np.array_equal(a, b))
    _, info = pull_array(a, DEFAULT_CLASSES['green'], 0.40, 0.80)
    case('V 0.78 is inside --min-diff of 0.80: only S is scaled', info['kV'] == 1.0 and info['kS'] < 0.85, str(info))
    cmd_pull([both], td / 'out2', 'green', 0.40, quiet=True)
    f2, _ = families(K.load_rgba(td / 'out2/both.png'), DEFAULT_CLASSES, 110)
    case('pull changes only the named family (orange stays 0.60)', abs(f2['orange']['S'] - 0.60) < 0.02 and abs(f2['green']['S'] - 0.40) < 0.02, str({k: round(f['S'], 2) for k, f in f2.items()}))
    case('a family the picture does not have is an input problem', main(['pull', str(hot), '--out', str(td / 'x'), '--family', 'blue', '--s', '0.3']) == 2)
    cl = parse_classes(['wood=15..50:0.12..0.35', 'fruit=15..50:0.35..1'])
    wood = td / 'wood.png'
    _disc(_hsv_rgb(30, 0.28, 0.80), extra=(_hsv_rgb(30, 0.60, 0.85), (0.40, 0.40, 0.60, 0.60))).save(wood)
    f3, _ = families(K.load_rgba(wood), cl, 110)
    case('--class splits one hue by saturation (wood 0.28 / fruit 0.60)', set(f3) == {'wood', 'fruit'} and abs(f3['wood']['S'] - 0.28) < 0.02 and abs(f3['fruit']['S'] - 0.60) < 0.02)
    case('references with no family in common: NOT_ASSESSED, not PASS', cmd_measure([wood], [ref], quiet=True) == 1)
    # keyed picture: recolouring the paint makes the old rim read as key colour -> pull reports the fringe
    import matte as MT
    raw = td / 'raw.png'
    MT._scene((255, 0, 255), body=_hsv_rgb(120, 0.55, 0.80)).save(raw)
    MT.cmd_key([raw], td / 'keyed', quiet=True)
    rc = cmd_pull([td / 'keyed/raw.png'], td / 'pulled', 'green', 0.30, quiet=True)
    after = MT.cmd_fringe([td / 'pulled/raw.png'], quiet=True)
    case('pull on a keyed picture re-runs the fringe check and its exit matches matte.py fringe', rc == after, f'pull {rc}, fringe {after}')
    case('the key chunk survives pull', MT.hexs(MT.read_key(td / 'pulled/raw.png')) == MT.hexs(MT.read_key(td / 'keyed/raw.png')))
    root = td / 'proj'
    (root / '.ai/2d-art').mkdir(parents=True)
    (root / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'work_dir': 'out'}))
    Image.open(hot).save(root / 'hot.png')
    cmd_measure([root / 'hot.png'], [ref], quiet=True, json_out=root / 'paint.json')
    rec = K.read_record(K.load_kit(root), K.sha256(root / 'hot.png'), 'paint')
    case('kit project: paint record written (FAIL with dS), --json written', rec and rec['verdict'] == 'FAIL' and rec['families']['green']['dS'] > 0.08 and (root / 'paint.json').is_file())
    bad = cases.count(False)
    print(f"paint selftest {'PASS' if not bad else 'FAIL'}: {len(cases)} cases, {bad} failed")
    return 1 if bad else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    m = sub.add_parser('measure')
    m.add_argument('paths', nargs='+')
    m.add_argument('--ref', nargs='+')
    m.add_argument('--tol', type=float, default=0.05)
    m.add_argument('--json', type=Path)
    p = sub.add_parser('pull')
    p.add_argument('paths', nargs='+')
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--s', type=float)
    p.add_argument('--v', type=float)
    p.add_argument('--min-diff', type=float, default=0.05)
    p.add_argument('--key')
    p.add_argument('--family')
    for s in (m, p):
        s.add_argument('--luma-min', type=float, default=110.0)
        s.add_argument('--class', dest='define', action='append', default=[], metavar='name=h0..h1[:s0..s1]')
    sub.add_parser('selftest')
    a = ap.parse_args(argv)
    if a.cmd == 'selftest':
        return selftest()
    try:
        missing = [str(x) for x in a.paths + (getattr(a, 'ref', None) or []) if not Path(x).is_file()]
        if missing:
            raise K.KitError('NO_IMAGE', ', '.join(missing))
        classes = parse_classes(a.define)
        if a.cmd == 'measure':
            return cmd_measure(a.paths, a.ref, a.tol, a.luma_min, classes, a.json)
        if not a.family:
            raise K.KitError('NO_CLASS', 'pull needs --family <name>')
        return cmd_pull(a.paths, a.out, a.family, a.s, a.v, a.min_diff, a.luma_min, classes, a.key)
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
