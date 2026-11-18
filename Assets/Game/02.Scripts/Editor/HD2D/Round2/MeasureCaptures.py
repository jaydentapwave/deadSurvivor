"""Measure known camera shifts and pixel widths from Round2 capture manifests.
Run with /usr/bin/python3 from the Unity project root after Capture All/texel probes.
No best-fit registration, AI judgment, or background pixels in the hero mask.
"""
import json
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[6]
OUT = ROOT / 'output/hd2d-시안'


def rgb(path):
    return np.asarray(Image.open(ROOT / path).convert('RGB')).astype(float)


def run_widths(line):
    changes = np.flatnonzero(np.diff(line) != 0) + 1
    return dict(Counter(int(v) for v in np.diff(changes)))


def measure():
    plates = json.loads((OUT / 'plates2.json').read_text())
    result = {'runId': plates['runId'], 'sourceFingerprint': plates['sourceFingerprint'],
              'units': 'mean absolute RGB channel difference, 0..255',
              'registration': 'Only recorded camera x displacement; no search or optimal alignment.',
              'heroThreshold': 0.042, 'motion': [], 'density': []}
    for plate in plates['plates'][2:]:
        name = plate['id']
        shots = [s for s in plate['shots'] if '-move-' in s['path']]
        ref = rgb(shots[0]['path'])
        hero = rgb(f'output/hd2d-시안/{name}-hero-mask.png').max(2) > 2
        foreground = Image.open(OUT / f'{name}-foreground-mask.png').convert('L')
        foreground = foreground.point(lambda p: 255 if p > 2 else 0).filter(ImageFilter.MaxFilter(9))
        background = np.asarray(foreground) == 0
        background[:260] = False
        background[850:] = False
        background[:, :20] = False
        background[:, -20:] = False
        samples = []
        for shot in shots[1:]:
            shift = round((shot['cameraPosition']['x'] - shots[0]['cameraPosition']['x']) * 1080 / (2 * shot['orthographicSize']))
            diff = np.abs(ref - np.roll(rgb(shot['path']), shift, axis=1))
            samples.append({'shiftPixels': shift, 'hero': float(diff[hero].mean()), 'background': float(diff[background].mean())})
        maximum = max(s['hero'] for s in samples)
        result['motion'].append({'plate': name, 'heroPixels': int(hero.sum()), 'backgroundPixels': int(background.sum()),
                                'backgroundMask': 'All non-foreground pixels in rows 260..849; foreground dilated 4px to exclude bloom/edge spill; 20px horizontal border excluded.',
                                'samples': samples, 'heroMax': maximum, 'heroPass': maximum <= .042,
                                'backgroundMax': max(s['background'] for s in samples)})
    probes = json.loads((OUT / 'measure2-texel-probes.json').read_text())
    for probe in probes['probes']:
        pixels = rgb(probe['image'])
        boards = []
        for board in probe['boards']:
            rect = board['viewport']
            x0 = round(rect['x'] * 1920)
            x1 = round((rect['x'] + rect['width']) * 1920)
            y0 = round((1 - rect['y'] - rect['height']) * 1080)
            y1 = round((1 - rect['y']) * 1080)
            crop = pixels[y0:y1, x0:x1, 0]
            boards.append({'name': board['name'], 'rectPixels': [x0, y0, x1, y1],
                           'horizontalRuns': run_widths(crop[len(crop)//2]),
                           'verticalRuns': run_widths(crop[:, crop.shape[1]//2]),
                           'horizontalPixelsPerTexel': board['horizontalPixelsPerTexel'],
                           'verticalPixelsPerTexel': board['verticalPixelsPerTexel']})
        result['density'].append({'plate': probe['plate'], 'boards': boards})
    measurement = json.loads((OUT / 'measure2-mobile-300.json').read_text())
    result['performance'] = {k: measurement[k] for k in ('settings', 'seconds', 'monsters', 'distinctSpriteTextures', 'distinctSpriteMaterials')}
    result['performance']['samples'] = len(measurement['samples'])
    for key in ('draws', 'srpDraws', 'standardDraws', 'dynamicBatchDraws', 'setPass', 'triangles', 'shadowCasters', 'frameMs', 'renderMs', 'deltaMs'):
        values = [s[key] for s in measurement['samples']]
        result['performance'][key] = {'mean': sum(values)/len(values), 'max': max(values)}
    (OUT / 'measure2-art-metrics.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    measure()
