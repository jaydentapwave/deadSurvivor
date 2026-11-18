"""2d-art kit shared code: project config, art spec approval and measure block, identity files, image helpers, records.

Config: <repo root>/.ai/2d-art/config.json (project-owned, created by /2d-art-setup), found by walking up from the working
folder, or KIT2D_CONFIG=<path>. Paths in it are relative to the repo root. Kit-owned: this tools folder (overwritten on update).
Needs numpy and Pillow."""
import hashlib
import json
import os
import re
import time
from pathlib import Path

import numpy as np
from PIL import Image

CONFIG_REL = Path('.ai/2d-art/config.json')
MEASURE_SCHEMA = '2d-measure/1'
IDENTITY_SCHEMA = '2d-identity/1'
ALPHA_SOLID = 128
DEFAULT_TOL = {'de76': 8.0, 'stroke_ratio': 0.003, 'part_area': 0.05, 'part_dim': 0.05, 'part_offset': 0.03,
               'same_tol': 8, 'same_max_px': 0.001, 'dilate_px': 4, 'haze_max': 0.002, 'jitter_max': 0.03, 'jitter_px': 0,
               'lab_tol': 30.0, 'min_part_px': 0.10, 'pose_area': 0.30}


class KitError(Exception):
    """input/config problem -> exit 2. code is a short UPPER_CASE reason for the caller."""
    def __init__(self, code, msg):
        super().__init__(f'{code}: {msg}')
        self.code = code


# ---------- config ----------

class Kit:
    def __init__(self, root, cfg):
        self.root = Path(root)
        self.cfg = cfg

    def path(self, key, default=None):
        v = self.cfg.get(key, default)
        if v is None:
            raise KitError('CONFIG_KEY_MISSING', f'{key} in {CONFIG_REL}')
        p = Path(v)
        return p if p.is_absolute() else self.root / p

    @property
    def work(self):
        return self.path('work_dir', 'output/2d-art')

    @property
    def measure_dir(self):
        return self.work / 'measure'


def load_kit(start=None):
    env = os.environ.get('KIT2D_CONFIG')
    if env:
        cfg_path = Path(env).resolve()
        root = cfg_path.parent.parent.parent if cfg_path.parent.name == '2d-art' else cfg_path.parent
    else:
        here = Path(start or os.getcwd()).resolve()
        for d in (here, *here.parents):
            if (d / CONFIG_REL).is_file():
                root, cfg_path = d, d / CONFIG_REL
                break
        else:
            raise KitError('NO_CONFIG', f'{CONFIG_REL} not found from {here} upward -> run /2d-art-setup or set KIT2D_CONFIG')
    try:
        cfg = json.loads(cfg_path.read_text(encoding='utf-8'))
    except ValueError as e:
        raise KitError('CONFIG_BROKEN', f'{cfg_path}: {e}')
    if cfg.get('schema') != '2d-art-config/1':
        raise KitError('CONFIG_SCHEMA', f'{cfg_path}: schema {cfg.get("schema")!r}, want 2d-art-config/1')
    return Kit(root, cfg)


# ---------- art spec: approval (same rules as art-spec/scripts/approve.py) and the 2d-measure block ----------

STATUS_RE = re.compile(r'^\s*-?\s*상태\s*:\s*(.*)$', re.M)
APPROVED_RE = re.compile(r'(?:r(\d+)\s*)?승인(?!\s*(?:전|대기|요청|필요|중))')
BLOCK_RE = re.compile(r'^```json[ \t]+2d-measure[ \t]*\n(.*?)^```', re.M | re.S)


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def spec_problem(spec, rec):
    """None if the spec is approved and unchanged since `approve.py record`, else the reason (approve.py verify)."""
    if not spec.is_file():
        return f'기준서 없음: {spec}'
    text = spec.read_text(encoding='utf-8')
    st = STATUS_RE.search(text)
    val = st[1].replace('*', '').strip() if st else ''
    ok = APPROVED_RE.match(val)
    if not ok:
        return f'상태 줄이 승인이 아님: "{val[:40]}"'
    rev = f'r{ok[1]}' if ok[1] else ''
    tag = rf'\(r{ok[1]}\)' if ok[1] else ''
    if not re.search(rf'^\s*-?\s*승인{tag}\s*:\s*\**\s*\d{{4}}-\d{{2}}-\d{{2}}', text, re.M):
        return f'날짜가 있는 승인 줄이 없음({"승인(r" + ok[1] + ")" if ok[1] else "승인"}: YYYY-MM-DD)'
    if not rec.is_file():
        return f'승인 기록 없음: {rec} (art-spec/scripts/approve.py record)'
    data = json.loads(rec.read_text(encoding='utf-8'))
    if data.get('schema') != 'art-spec-approval/1':
        return f'승인 기록 형식이 다름: {data.get("schema")}'
    if rev and data.get('revision') != rev:
        return f'기록은 {data.get("revision")}, 상태 줄은 {rev}'
    if data.get('sha256') != sha256(spec):
        return '본문이 승인 기록 뒤 바뀜'
    return None


def spec_paths(kit):
    spec = kit.path('spec', 'docs/art/아트-기준서.md')
    rec = kit.path('spec_approval') if kit.cfg.get('spec_approval') else spec.with_suffix('.approval.json')
    return spec, rec


def approved_tolerance(kit):
    """tolerance dict from the approved spec's 2d-measure block (defaults filled). Raises KitError if not approved."""
    spec, rec = spec_paths(kit)
    why = spec_problem(spec, rec)
    if why:
        raise KitError('SPEC_NOT_APPROVED', why + ' -> /art-spec')
    m = BLOCK_RE.search(spec.read_text(encoding='utf-8'))
    if not m:
        raise KitError('NO_MEASURE_BLOCK', f'{spec}: no ```json 2d-measure block (3.9) -> /art-spec')
    try:
        block = json.loads(m[1])
    except ValueError as e:
        raise KitError('MEASURE_BLOCK_BROKEN', f'{spec}: {e}')
    tol = dict(DEFAULT_TOL)
    tol.update(block.get('tolerance') or {})
    out = dict(block.get('outline') or {})
    return tol, {'luma_max': float(out.get('luma_max', 60))}, spec


# ---------- identity ----------

def load_identity(kit, ident):
    p = kit.path('identities_dir', 'docs/art/identities') / f'{ident}.json'
    if not p.is_file():
        raise KitError('NO_IDENTITY', f'{p} -> register the approved reference first (2d-art-produce step 0)')
    try:
        d = json.loads(p.read_text(encoding='utf-8'))
    except ValueError as e:
        raise KitError('IDENTITY_BROKEN', f'{p}: {e}')
    if d.get('schema') != IDENTITY_SCHEMA:
        raise KitError('IDENTITY_SCHEMA', f'{p}: schema {d.get("schema")!r}')
    parts = d.get('parts')
    missing = [k for k in ('ref', 'ref_sha256', 'approved', 'width_part') if not d.get(k)]
    if missing or not isinstance(parts, dict) or not parts:
        raise KitError('IDENTITY_INCOMPLETE', f'{p}: needs ref, ref_sha256, approved, width_part and parts (missing {missing or ["parts"]})')
    if d['width_part'] not in parts:
        raise KitError('IDENTITY_INCOMPLETE', f'{p}: width_part {d["width_part"]!r} is not one of the parts')
    if not isinstance(d['approved'], str) or not re.search(r'\d{4}-\d{2}-\d{2}', d['approved']):
        raise KitError('IDENTITY_INCOMPLETE', f'{p}: approved must be "<YYYY-MM-DD> — \"<user quote>\""')
    nohex = [n for n, v in parts.items() if not isinstance(v, dict) or not re.fullmatch(r'#?[0-9A-Fa-f]{6}', str(v.get('hex', '')))
             or ('box_rel' in v and not (isinstance(v['box_rel'], list) and len(v['box_rel']) == 4))]
    if nohex:
        raise KitError('IDENTITY_INCOMPLETE', f'{p}: parts need a #RRGGBB hex (and box_rel, if any, of 4 numbers): {nohex}')
    ref = Path(d['ref'])
    ref = ref if ref.is_absolute() else kit.root / ref
    if not ref.is_file():
        raise KitError('NO_REF_IMAGE', f'{ref}')
    if d['ref_sha256'] != sha256(ref):
        raise KitError('REF_CHANGED', f'{ref} differs from the approved ref_sha256 in {p}')
    d['_ref_path'], d['_path'], d['_sha256'] = ref, p, sha256(p)
    return d


def stale_reason(kit, rec):
    """None if a measure record still holds: the spec is approved now and unchanged, the identity file and its reference
    image are the ones the record was measured against. Else the reason (re-measure)."""
    try:
        _, _, spec = approved_tolerance(kit)
        ident = load_identity(kit, rec.get('identity', ''))
    except KitError as e:
        return e.code
    if rec.get('spec_sha256') != sha256(spec):
        return 'spec changed since measured'
    if rec.get('identity_sha256') != ident['_sha256'] or rec.get('ref_sha256') != ident['ref_sha256']:
        return 'identity or reference changed since measured'
    return None


# ---------- images ----------

def load_rgba(path):
    try:
        return np.asarray(Image.open(path).convert('RGBA'), dtype=np.float32)
    except (OSError, ValueError) as e:
        raise KitError('BAD_IMAGE', f'{path}: {e}')


def hex_rgb(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


def rgb_to_lab(rgb):
    """rgb [...,3] in 0..255 -> CIE Lab (D65)"""
    c = rgb / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]], dtype=np.float32)
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883], dtype=np.float32)
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def de76(a, b):
    return float(np.linalg.norm(np.asarray(a) - np.asarray(b)))


def luma(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def bbox(mask):
    ys, xs = np.nonzero(mask)
    if not len(xs):
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def erode(mask):
    m = mask.copy()
    m[1:, :] &= mask[:-1, :]
    m[:-1, :] &= mask[1:, :]
    m[:, 1:] &= mask[:, :-1]
    m[:, :-1] &= mask[:, 1:]
    m[1:, 1:] &= mask[:-1, :-1]
    m[:-1, :-1] &= mask[1:, 1:]
    m[1:, :-1] &= mask[:-1, 1:]
    m[:-1, 1:] &= mask[1:, :-1]
    m[0, :] = m[-1, :] = False
    m[:, 0] = m[:, -1] = False
    return m


def dilate(mask, n):
    m = mask.copy()
    for _ in range(int(n)):
        s = m.copy()
        s[1:, :] |= m[:-1, :]
        s[:-1, :] |= m[1:, :]
        s[:, 1:] |= m[:, :-1]
        s[:, :-1] |= m[:, 1:]
        m = s
    return m


def chessboard_distance(mask, cap=256):
    """distance to the nearest outside pixel (chessboard metric) by repeated erosion; numpy only"""
    dist = np.zeros(mask.shape, dtype=np.int32)
    cur = mask.copy()
    for i in range(1, cap + 1):
        if not cur.any():
            break
        dist[cur] = i
        cur = erode(cur)
    return dist


def stroke_width(mask):
    """stroke width in pixels = mean of the distance map on its ridge (local maxima) x 2; None if no stroke.
    The mean is steadier than the median across resolutions (odd widths, diagonal strokes)."""
    d = chessboard_distance(mask)
    if not d.any():
        return None
    p = np.pad(d, 1)
    nb = np.max(np.stack([p[1 + dy:p.shape[0] - 1 + dy, 1 + dx:p.shape[1] - 1 + dx]
                          for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx]), axis=0)
    ridge = (d > 0) & (d >= nb)
    return float(d[ridge].mean()) * 2


# ---------- records ----------

def write_record(kit, data):
    """work_dir/measure/<sha256>.<kind>.json — one file per check kind (compare, jitter) of one image"""
    kit.measure_dir.mkdir(parents=True, exist_ok=True)
    data = dict(data, recorded_at=time.strftime('%Y-%m-%dT%H:%M:%S%z'))
    (kit.measure_dir / f"{data['sha256']}.{data['kind']}.json").write_text(json.dumps(data, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    return data


def read_record(kit, sha, kind='compare'):
    p = kit.measure_dir / f'{sha}.{kind}.json'
    return json.loads(p.read_text(encoding='utf-8')) if p.is_file() else None


def read_records(kit, sha):
    """every check recorded for this image content: {kind: record}"""
    out = {}
    if kit.measure_dir.is_dir():
        for p in kit.measure_dir.glob(f'{sha}.*.json'):
            out[p.name[len(sha) + 1:-5]] = json.loads(p.read_text(encoding='utf-8'))
    return out


def font(size):
    from PIL import ImageFont
    for cand in ('/System/Library/Fonts/AppleSDGothicNeo.ttc', '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc',
                 '/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc', 'C:/Windows/Fonts/malgun.ttf'):
        if os.path.isfile(cand):
            try:
                return ImageFont.truetype(cand, size)
            except OSError:
                pass
    try:
        return ImageFont.load_default(size)
    except TypeError:
        return ImageFont.load_default()
