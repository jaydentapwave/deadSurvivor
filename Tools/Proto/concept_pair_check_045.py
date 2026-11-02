#!/usr/bin/env python3
"""045 stage 3 v2 (design D1): automatic consistency check of the front/back concept pair before Tripo multiview.
Silhouette = pixels farther than 40 (RGB L1) from the #d9d9d9 background. Pass when the silhouette height and width
differ by <= 5%, the IoU of the front silhouette with the left-right mirrored back silhouette is >= 0.85, and the median
identity colour (hue within 18 deg of #d79b68, sat >= 0.25, value >= 0.30) of the two images is within RGB distance 30.
Optional third image (left side, visual review note): silhouette height within 5% of the front, and the head depth seen
from the side (silhouette width over the top 40% of the height) <= 0.70 of the head width seen from the front.
Usage: concept_pair_check_045.py <front.png> <back.png> <out.json> [<left.png>]"""
import colorsys, json, sys, hashlib
from pathlib import Path
import numpy as np
from PIL import Image

front, back, out = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]); left = Path(sys.argv[4]) if len(sys.argv) > 4 else None
BG = np.array([0xd9, 0xd9, 0xd9]); BASE_H = colorsys.rgb_to_hsv(0xd7 / 255, 0x9b / 255, 0x68 / 255)[0] * 360


def load(p):
    a = np.asarray(Image.open(p).convert('RGB'), dtype=np.int32)
    sil = np.abs(a - BG).sum(axis=2) > 40
    ys, xs = np.nonzero(sil)
    f = a.reshape(-1, 3) / 255.0
    hsv = np.array([colorsys.rgb_to_hsv(*c) for c in f[::7]])          # every 7th pixel is enough for a median
    rgb = (f[::7] * 255)
    dh = np.abs(((hsv[:, 0] * 360 - BASE_H) + 180) % 360 - 180)
    ident = (dh <= 18) & (hsv[:, 1] >= 0.25) & (hsv[:, 2] >= 0.30)
    return dict(arr=a, sil=sil, box=(xs.min(), ys.min(), xs.max(), ys.max()), ident=np.median(rgb[ident], axis=0) if ident.any() else None,
                ident_frac=float(ident.mean()), sha=hashlib.sha256(p.read_bytes()).hexdigest(), size=a.shape[1::-1])


F, B = load(front), load(back)
hF, wF = F['box'][3] - F['box'][1], F['box'][2] - F['box'][0]; hB, wB = B['box'][3] - B['box'][1], B['box'][2] - B['box'][0]
mir = B['sil'][:, ::-1]
iou = float((F['sil'] & mir).sum() / max(1, (F['sil'] | mir).sum()))
cd = 0 if F['ident'] is not None and B['ident'] is not None else None
cdist = float(np.sqrt(((F['ident'] - B['ident']) ** 2).sum())) if cd is not None else None
r = dict(front=str(front), back=str(back), size_note='pixels', front_sha256=F['sha'], back_sha256=B['sha'], size=[[int(x) for x in F['size']], [int(x) for x in B['size']]],
         height_diff=round(float(abs(hF - hB)) / max(hF, hB), 4), width_diff=round(float(abs(wF - wB)) / max(wF, wB), 4), mirrored_iou=round(iou, 4),
         identity_rgb=[None if x is None else [round(v, 1) for v in x] for x in (F['ident'], B['ident'])], identity_distance=None if cdist is None else round(cdist, 2))
r['checks'] = dict(height=bool(r['height_diff'] <= 0.05), width=bool(r['width_diff'] <= 0.05), iou=bool(iou >= 0.85), colour=bool(cdist is not None and cdist <= 30))
def head_width(L):
    x0, y0, x1, y1 = L['box']; top = y0 + int(0.40 * (y1 - y0)); xs = np.nonzero(L['sil'][y0:top].any(axis=0))[0]
    return int(xs.max() - xs.min()) if len(xs) else 0


if left is not None:
    S = load(left); hS = S['box'][3] - S['box'][1]
    depth = head_width(S) / max(1, head_width(F))
    r.update(left=str(left), left_sha256=S['sha'], left_height_diff=round(float(abs(hF - hS)) / max(hF, hS), 4), head_depth_ratio=round(float(depth), 3))
    r['checks'].update(left_height=bool(r['left_height_diff'] <= 0.05), head_depth=bool(depth <= 0.70))
r['status'] = 'PASS' if all(r['checks'].values()) else 'FAIL'
out.write_text(json.dumps(r, indent=2) + '\n'); print(json.dumps({k: r.get(k) for k in ('status', 'height_diff', 'width_diff', 'mirrored_iou', 'identity_distance', 'left_height_diff', 'head_depth_ratio', 'checks')}))
sys.exit(0 if r['status'] == 'PASS' else 1)
