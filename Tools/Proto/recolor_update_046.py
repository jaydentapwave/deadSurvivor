#!/usr/bin/env python3
"""046: hero colour adjustment (lena, mira, hans) on the reviewed 045 v2 mask, same formula as recolor_heroes_045.py:
hue/saturation := target's, V' = min(1, V * V_target / V_median_identity) on the 'target' texels of the base texture.

Why: 045 game-screen review — lena (muddy ochre) blends with sand, mira is close to bruno, hans has the lowest grass
contrast. New colours were picked by OKLab distance to grass/dirt/stone and to the other heroes (see map-full-046.md).
Writes Assets/DeadSurvivor/Scenes/Proto045/Art_s3b/hero045v2_<hero>.png (and a copy under output/map-full-046/recolor/)
and the hero colour in PrototypeData.json. Checks: changes only inside the target mask, untouched heroes unchanged.
Usage: /usr/bin/python3 Tools/Proto/recolor_update_046.py
"""
import json, re, sys
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ASSET = ROOT / 'output/keyart-target-045/r003/tripo/hero_base045v2'
UNITY = ROOT / 'Assets/DeadSurvivor/Scenes/Proto045/Art_s3b'
DATA = ROOT / 'Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json'
COPY = ROOT / 'output/map-full-046/recolor'
NEW = {'lena': ('#c6a660', '#f0cf3c'), 'mira': ('#cd8274', '#e88aa8'), 'hans': ('#7f9b82', '#c8e0b0')}


def rgb_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 0
    rr = m & (mx == r); gg = m & (mx == g) & ~rr; bb = m & ~rr & ~gg
    h[rr] = ((g - b)[rr] / d[rr]) % 6; h[gg] = (b - r)[gg] / d[gg] + 2; h[bb] = (r - g)[bb] / d[bb] + 4
    s = np.where(mx > 0, d / np.where(mx > 0, mx, 1), 0)
    return np.stack([h / 6, s, mx], -1)


def hsv_rgb(hsv):
    h, s, v = hsv[..., 0] * 6, hsv[..., 1], hsv[..., 2]; i = np.floor(h).astype(int) % 6; f = h - np.floor(h)
    p, q, t = v * (1 - s), v * (1 - s * f), v * (1 - s * (1 - f))
    out = np.zeros(hsv.shape)
    for k, (a, b, c) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        sel = i == k; out[..., 0][sel], out[..., 1][sel], out[..., 2][sel] = a[sel], b[sel], c[sel]
    return out


def hexrgb(h):
    return np.array([int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)])


def load(p):
    return np.asarray(Image.open(p).convert('RGBA'), dtype=np.uint8)


def main():
    base = load(ASSET / 'recolor/bruno.png')
    masks = np.load(ASSET / 'recolor-mask/masks.npz')
    # Blender pixel rows are bottom-up; pick the orientation under which the reviewed 045 lena differs only inside the mask
    old = load(UNITY / 'hero045v2_lena.png'); diff = (old != base).any(-1)
    target = next((m for m in (masks['target'][::-1], masks['target']) if not (diff & ~m).any()), None)
    if target is None:
        raise SystemExit('MASK_ORIENTATION_UNKNOWN')
    hsv = rgb_hsv(base[..., :3] / 255.0)
    vmed = float(np.median(hsv[..., 2][target]))
    report = {'v_median_identity': round(vmed, 4), 'heroes': {}}
    before = {p.name: p.read_bytes() for p in UNITY.glob('hero045v2_*.png')}
    COPY.mkdir(parents=True, exist_ok=True)
    for hero, (was, col) in NEW.items():
        th, ts, tv = rgb_hsv(hexrgb(col)[None, None, :])[0, 0]
        nv = hsv[..., 2] * tv / vmed
        h2 = hsv.copy(); h2[..., 0][target] = th; h2[..., 1][target] = ts; h2[..., 2][target] = np.minimum(1, nv[target])
        out = base.copy(); out[..., :3][target] = np.clip(np.round(hsv_rgb(h2)[target] * 255), 0, 255).astype(np.uint8)
        changed = (out != base).any(-1)
        if (changed & ~target).any():
            raise SystemExit(f'CHANGE_OUTSIDE_MASK:{hero}')
        for dst in (UNITY / f'hero045v2_{hero}.png', COPY / f'{hero}.png'):
            Image.fromarray(out).save(dst)
        back = load(UNITY / f'hero045v2_{hero}.png')
        if not np.array_equal(back, out):
            raise SystemExit(f'SAVE_MISMATCH:{hero}')
        report['heroes'][hero] = {'from': was, 'to': col, 'changed_texels': int(changed.sum()), 'target_texels': int(target.sum()),
                                  'clipped_pct': round(100 * float((target & (nv > 1)).sum()) / float(target.sum()), 3)}
    after = {p.name: p.read_bytes() for p in UNITY.glob('hero045v2_*.png')}
    untouched = [n for n in before if not any(h in n for h in NEW)]
    if any(before[n] != after[n] for n in untouched):
        raise SystemExit('OTHER_HERO_CHANGED')
    text = DATA.read_text(encoding='utf-8')
    for hero, (was, col) in NEW.items():
        pat = re.compile(r'"color": "(?:%s|%s)"(,\s*"id": "%s")' % (was, col, hero))
        text, n = pat.subn(lambda m: '"color": "%s"' % col + m.group(1), text, count=1)
        if n != 1:
            raise SystemExit(f'DATA_COLOR_NOT_FOUND:{hero}')
    DATA.write_text(text, encoding='utf-8')
    json.loads(text)
    report['untouched_heroes'] = untouched
    (COPY / 'recolor-046.json').write_text(json.dumps(report, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    sys.exit(main())
