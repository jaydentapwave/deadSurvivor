#!/usr/bin/env python3
"""045 key-art layout revision: replace OB-001..006 of Map018 r001 with projected key-art structures.

Writes only under output/keyart-target-045/<rev>/layout/ (never Assets). Deterministic for the same input.
Usage: keyart_layout_045.py <out_dir>
"""
import hashlib, json, math, os, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json'
SRC_SHA = 'bdce2bf8d303680b320bc04499c889a5510c4787598dfde1772e9564c5063011'
KEYART = ROOT / 'output/keyart-041/r002/result/day-1280x720.png'

# Key-art screen (1280x720) == one game screen centred on the start position (ortho 6, 55 deg, 16:9).
START_PX = (280.0, 300.0)
VIEW_W = 6 * 2 * (16 / 9) / 0.02          # logical units
VIEW_H = 6 * 2 / math.sin(math.radians(55)) / 0.02
SX = VIEW_W / 4 / 1280                    # px per key-art pixel (scale profile 4)
SY = VIEW_H / 4 / 720
AREA = (100, 100, 520, 360)               # representative area, px
BODY_PX = 8 / 4                           # party body radius in px
MARGIN_PX = 8 / 4


def K(u, v):
    return (START_PX[0] + (u - 640) * SX, START_PX[1] + (v - 360) * SY)


def ellipse(c, rx, ry, n=16):
    return [(c[0] + rx * math.cos(2 * math.pi * i / n), c[1] + ry * math.sin(2 * math.pi * i / n)) for i in range(n)]


def north_half(c, rx, ry, n=13):
    # flat side to the south; arc to the north (screen up = -y)
    return [(c[0] + rx * math.cos(math.pi + math.pi * i / (n - 1)), c[1] + ry * math.sin(math.pi + math.pi * i / (n - 1))) for i in range(n)]


def structures():
    s = {}
    s['OB-001'] = ('R1 ring + bush', ellipse(K(315, 147), 115 * SX, 82 * SY))
    s['OB-002'] = ('R2 half ring + bush', north_half(K(840, 232), 120 * SX, 60 * SY))
    s['OB-003'] = ('R3 arc ring + bush', north_half(K(367, 490), 152 * SX, 70 * SY))
    s['OB-004'] = ('R4 ring + bush', ellipse(K(857, 487), 92 * SX, 62 * SY))
    s['OB-005'] = ('R5 ring + bush', ellipse(K(1175, 480), 90 * SX, 65 * SY))
    e = K(1280, 175); t = K(1280, 0); f = K(960, 0)
    s['OB-006'] = ('P pond', [K(905, 40), K(960, 95), K(1040, 130), K(1130, 150), e,
                             (e[0] + 40, e[1] - 10), (t[0] + 40, t[1] - 25), (f[0], f[1] - 25)])
    return {k: (name, [(round(x, 2), round(y, 2)) for x, y in pts]) for k, (name, pts) in s.items()}


def cross(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def on(p, a, b):
    return cross(a, b, p) == 0 and min(a[0], b[0]) <= p[0] <= max(a[0], b[0]) and min(a[1], b[1]) <= p[1] <= max(a[1], b[1])


def seg_x(a, b, c, d):
    x, y, z, w = cross(a, b, c), cross(a, b, d), cross(c, d, a), cross(c, d, b)
    if (x == 0 and on(c, a, b)) or (y == 0 and on(d, a, b)) or (z == 0 and on(a, c, d)) or (w == 0 and on(b, c, d)):
        return True
    return ((x < 0 < y) or (x > 0 > y)) and ((z < 0 < w) or (z > 0 > w))


def edges(p):
    return [(p[i], p[(i + 1) % len(p)]) for i in range(len(p))]


def simple(p):
    if not 3 <= len(p) <= 256 or len(set(p)) != len(p):
        return False
    E = edges(p)
    for i in range(len(E)):
        for j in range(i):
            adj = j == i - 1 or (j == 0 and i == len(E) - 1)
            if not adj and seg_x(*E[i], *E[j]):
                return False
    area = sum(a[0] * b[1] - a[1] * b[0] for a, b in E)
    return abs(area) > 1e-8


def inside(pt, poly):
    r = False
    for a, b in edges(poly):
        if (a[1] > pt[1]) != (b[1] > pt[1]) and pt[0] < (b[0] - a[0]) * (pt[1] - a[1]) / (b[1] - a[1]) + a[0]:
            r = not r
    return r


def pt_seg(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]; n = dx * dx + dy * dy
    t = 0 if n == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / n))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)


def poly_dist(p, poly):
    return 0.0 if inside(p, poly) else min(pt_seg(p, a, b) for a, b in edges(poly))


def polys_overlap(p, q):
    if any(seg_x(*e, *f) for e in edges(p) for f in edges(q)):
        return True
    return inside(p[0], q) or inside(q[0], p)


def main(out):
    raw = Path(out) if Path(out).is_absolute() else ROOT / out
    # 045-K2: check the unresolved path first — no symlink in any component from the project root down to the output
    for p in [raw, *raw.parents]:
        if p == ROOT or ROOT not in p.parents and p != ROOT:
            break
        if p.is_symlink():
            raise SystemExit(f'OUTPUT_SYMLINK:{p}')
    for p in (ROOT / 'output', ROOT / 'output/keyart-target-045'):
        if p.is_symlink():
            raise SystemExit(f'OUTPUT_SYMLINK:{p}')
    out = raw.resolve()
    allowed = (ROOT / 'output/keyart-target-045').resolve()
    if allowed not in out.parents:   # 045-I8: never write outside output/keyart-target-045/
        raise SystemExit(f'OUTPUT_OUTSIDE_045:{out}')
    names = ('Layout045.json', 'layout-report.json', 'layout045.png')
    for n in names:   # 045-J7: refuse existing files and (dangling) symlinks; outputs are created exclusively below
        if os.path.lexists(out / n):
            raise SystemExit(f'OUTPUT_EXISTS:{out / n}')
    probe = out
    while probe != allowed and probe != probe.parent:
        if probe.is_symlink():
            raise SystemExit(f'OUTPUT_SYMLINK:{probe}')
        probe = probe.parent
    out.mkdir(parents=True, exist_ok=True)
    raw = SRC.read_bytes()
    sha = hashlib.sha256(raw).hexdigest()
    if sha != SRC_SHA:
        raise SystemExit(f'SOURCE_SHA_MISMATCH {sha}')
    data = json.loads(raw)
    new = structures()
    checks = []
    def check(name, ok, detail=''):
        checks.append({'check': name, 'ok': bool(ok), 'detail': detail})
    boundary = [(p['x'], p['y']) for p in data['boundary_px']]
    others = {o['id']: [(p['x'], p['y']) for p in o['points_px']] for o in data['objects'] if o['id'] not in new}
    for oid, (name, pts) in new.items():
        check(f'{oid} simple polygon', simple(pts))
        check(f'{oid} inside representative area', all(AREA[0] <= x <= AREA[2] and AREA[1] <= y <= AREA[3] for x, y in pts))
        check(f'{oid} inside boundary', all(inside(p, boundary) for p in pts) and not any(seg_x(*e, *f) for e in edges(pts) for f in edges(boundary)))
        for qid, q in others.items():
            if polys_overlap(pts, q):
                check(f'{oid} no overlap with {qid}', False)
        d = poly_dist(START_PX, pts)
        check(f'{oid} start clearance >= {BODY_PX + MARGIN_PX}px', d >= BODY_PX + MARGIN_PX, f'{d:.2f}px')
    ids = sorted(new)
    for i, a in enumerate(ids):
        for b in ids[:i]:
            check(f'{a} no overlap with {b}', not polys_overlap(new[a][1], new[b][1]))
    for o in data['objects']:
        if o['id'] in new:
            o['points_px'] = [{'x': x, 'y': y} for x, y in new[o['id']][1]]
            o['height_logical'] = 16
    p1 = new['OB-001'][1]
    data['rock_center_px'] = {'x': round(sum(x for x, _ in p1) / len(p1), 3), 'y': round(sum(y for _, y in p1) / len(p1), 3)}
    a, b = K(0, 560), K(1280, 600)
    for r in data['routes']:
        if r['id'] == 'RT-003':
            r['points_px'] = [{'x': 100.0, 'y': round(a[1] - (a[0] - 100) * (b[1] - a[1]) / (b[0] - a[0]), 2)},
                              {'x': round(a[0], 2), 'y': round(a[1], 2)}, {'x': round(b[0], 2), 'y': round(b[1], 2)},
                              {'x': 520.0, 'y': round(b[1] + (520 - b[0]) * (b[1] - a[1]) / (b[0] - a[0]), 2)},
                              {'x': 650, 'y': 325}, {'x': 850, 'y': 330}, {'x': 1020, 'y': 320}]
    data['package'] = 'MAP-KEYART-045'
    data['revision'] = 'r001'
    data['source_layout_sha256'] = sha
    text = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
    with open(out / 'Layout045.json', 'x', encoding='utf-8') as f:
        f.write(text)
    lsha = hashlib.sha256(text.encode('utf-8')).hexdigest()
    ok = all(c['ok'] for c in checks)
    report = {'status': 'PASS' if ok else 'FAIL', 'layout_sha256': lsha, 'source_sha256': sha,
              'projection': {'start_px': START_PX, 'sx': SX, 'sy': SY, 'view_logical': [VIEW_W, VIEW_H]},
              'structures': {k: v[0] for k, v in new.items()}, 'checks': checks}
    with open(out / 'layout-report.json', 'x', encoding='utf-8') as f:
        f.write(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    try:
        draw(out, data, new)
    except Exception as ex:  # drawing is evidence only
        report['drawing_error'] = str(ex)
    print(json.dumps({'status': report['status'], 'layout_sha256': lsha, 'failed': [c for c in checks if not c['ok']]}, ensure_ascii=False))
    return 0 if ok else 1


def draw(out, data, new):
    from PIL import Image, ImageDraw
    Z = 3; X0, Y0, X1, Y1 = 40, 40, 600, 440
    P = lambda x, y: ((x - X0) * Z, (y - Y0) * Z)
    im = Image.new('RGB', ((X1 - X0) * Z, (Y1 - Y0) * Z), (46, 52, 46)); d = ImageDraw.Draw(im, 'RGBA')
    fw, fh = VIEW_W / 4, VIEW_H / 4
    ka = Image.open(KEYART).convert('RGBA').resize((int(fw * Z), int(fh * Z))); ka.putalpha(120)
    im.paste(ka, (int((START_PX[0] - fw / 2 - X0) * Z), int((START_PX[1] - fh / 2 - Y0) * Z)), ka)
    b = [(p['x'], p['y']) for p in data['boundary_px']]
    d.line([P(*p) for p in b + [b[0]]], fill=(230, 230, 230, 255), width=3)
    for o in data['objects']:
        pts = [P(p['x'], p['y']) for p in o['points_px']]
        col = (80, 200, 255) if o['id'] == 'OB-006' else (255, 220, 60) if o['id'] in new else (150, 150, 150)
        d.polygon(pts, fill=col + (110,), outline=col + (255,))
    r = next(x for x in data['routes'] if x['id'] == 'RT-003')
    d.line([P(p['x'], p['y']) for p in r['points_px']], fill=(220, 220, 255, 200), width=4)
    d.rectangle((*P(AREA[0], AREA[1]), *P(AREA[2], AREA[3])), outline=(255, 230, 80, 255), width=3)
    d.ellipse((*P(START_PX[0] - 2, START_PX[1] - 2), *P(START_PX[0] + 2, START_PX[1] + 2)), fill=(255, 255, 255, 255))
    with open(out / 'layout045.png', 'xb') as f:
        im.save(f, format='PNG')


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else str(ROOT / 'output/keyart-target-045/r001/layout')))
