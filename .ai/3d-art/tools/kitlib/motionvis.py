"""How much of a walking character's lower body actually moves on the game screen. Reads a video or frames from the game camera
that follows the character (or crops that keep it in the same place), no Unity. Answers U10/U12 ("the legs do not move") with numbers:
  lower_change  share of the lower-body silhouette that changes between frames half a loop apart (0 = frozen)
  width_px      how much the lower-body width swings, in pixels
  bottom_px     how much the lowest point moves up and down, in pixels
Numbers only, no verdict in this module: a person compares them with an accepted clip. It says nothing about whether a motion looks
natural (U11 is out of its reach).

  python3 .ai/3d-art/tools/kitlib/motionvis.py <video.mp4 | frames_dir> [--loop-s 0.4] [--fps 30] [--box W,H] [--center X,Y]
"""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image


def non_ground(a):
    """character pixels: anything that is not green-dominant (grass and its shadow are)"""
    r, g, b = a[..., 0].astype(int), a[..., 1].astype(int), a[..., 2].astype(int)
    return ~((g > r + 8) & (g > b + 8)) & ~((b > r + 25) & (b > g - 5))   # water excluded too


def largest_near_centre(mask, cx, cy, radius):
    """connected foreground blob closest to the expected centre, as a mask"""
    h, w = mask.shape
    lab = -np.ones((h, w), int)
    best, best_n = None, 0
    ys, xs = np.nonzero(mask[max(cy - radius, 0):cy + radius, max(cx - radius, 0):cx + radius])
    ox, oy = max(cx - radius, 0), max(cy - radius, 0)
    k = 0
    for y0, x0 in zip(ys + oy, xs + ox):
        if lab[y0, x0] >= 0:
            continue
        st, cells = [(y0, x0)], []
        lab[y0, x0] = k
        while st:
            y, x = st.pop()
            cells.append((y, x))
            for v, u in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= v < h and 0 <= u < w and mask[v, u] and lab[v, u] < 0:
                    lab[v, u] = k
                    st.append((v, u))
        if len(cells) > best_n:
            best, best_n = k, len(cells)
        k += 1
    return lab == best if best is not None else np.zeros_like(mask)


def frames_from(path, fps):
    p = Path(path)
    if p.is_dir():
        return [np.asarray(Image.open(f).convert('RGB')) for f in sorted(p.glob('*.jpg')) + sorted(p.glob('*.png'))], None
    with tempfile.TemporaryDirectory() as d:
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(p), '-vf', f'fps={fps}', f'{d}/f_%04d.png'], check=True)
        return [np.asarray(Image.open(f).convert('RGB')) for f in sorted(Path(d).glob('*.png'))], None


def measure(frames, loop_frames, center=None, radius=70, lower_frac=0.45):
    h, w, _ = frames[0].shape
    cx, cy = center or (w // 2, h // 2)
    masks = []
    for a in frames:
        m = largest_near_centre(non_ground(a), cx, cy, radius)
        ys, xs = np.nonzero(m)
        if len(xs):
            cx, cy = int(np.median(xs)), int(np.median(ys))   # follow small drift
        masks.append(m)
    boxes = [(np.nonzero(m)[0].min(), np.nonzero(m)[0].max(), np.nonzero(m)[1].min(), np.nonzero(m)[1].max()) for m in masks if m.any()]
    top, bot = np.median([b[0] for b in boxes]), np.median([b[1] for b in boxes])
    H = float(bot - top)
    # lower body measured relative to the blob centre, so a body that travels on screen does not count as leg motion
    size = int(H * 1.6)
    low = []
    widths, bottoms = [], []
    for m in masks:
        ys, xs = np.nonzero(m)
        if not len(xs):
            continue
        t, b = ys.min(), ys.max()
        mx = int(np.median(xs))
        band_top = int(t + (1 - lower_frac) * (b - t))
        crop = m[band_top:band_top + size // 2, max(mx - size // 2, 0):mx + size // 2]
        pad = np.zeros((size // 2, size), bool)
        pad[:crop.shape[0], :crop.shape[1]] = crop[:size // 2, :size]
        low.append(pad)
        c = np.nonzero(crop.any(axis=0))[0]
        widths.append(int(c.max() - c.min()) if len(c) else 0)
        bottoms.append(int(b))
    half = max(1, int(round(loop_frames / 2)))
    change = [float((low[i] ^ low[i + half]).sum() / max(low[i].sum(), 1)) for i in range(len(low) - half)]
    return dict(frames=len(frames), character_px=int(H), lower_change=round(float(np.mean(change)), 3),
                width_px=int(np.percentile(widths, 95) - np.percentile(widths, 5)), bottom_px=int(np.percentile(bottoms, 95) - np.percentile(bottoms, 5)))


def selftest():
    def clip(amp):
        out = []
        for i in range(24):
            a = np.zeros((200, 200, 3), np.uint8)
            a[..., 1] = 180
            a[..., 0] = 90
            a[..., 2] = 60
            a[70:120, 85:115] = (200, 150, 60)             # body
            d = int(amp * np.sin(2 * np.pi * i / 12))
            a[120:150, 85 + d:97 + d] = (200, 60, 60)      # legs swing in opposite directions
            a[120:150, 103 - d:115 - d] = (200, 60, 60)
            out.append(a)
        return out
    still, moving = measure(clip(0), 12), measure(clip(8), 12)
    assert still['lower_change'] == 0 and still['width_px'] == 0, still
    assert moving['lower_change'] > 0.3 and moving['width_px'] >= 8, moving
    shift = [np.roll(a, 3 * i % 30, axis=1) for i, a in enumerate(clip(0))]
    assert measure(shift, 12)['lower_change'] < 0.05, 'a frozen body sliding on screen is not leg motion'
    return 'motionvis selftest PASS'


if __name__ == '__main__':
    a = sys.argv[1:]
    if a == ['selftest']:
        print(selftest())
        raise SystemExit
    def opt(n, d):
        return a[a.index(n) + 1] if n in a else d
    fps = int(opt('--fps', 30))
    frames, _ = frames_from(a[0], fps)
    c = opt('--center', None)
    print(json.dumps(measure(frames, float(opt('--loop-s', 0.4)) * fps, tuple(map(int, c.split(','))) if c else None), ensure_ascii=False))
