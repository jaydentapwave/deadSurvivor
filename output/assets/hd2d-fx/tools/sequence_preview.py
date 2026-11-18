"""실제 게임 연속 PNG를 시간 순서로 배치. 최종 그림 출력은 킷 sheet.py로만 한다."""
from pathlib import Path
import csv
import json
import subprocess
import sys
from PIL import Image, ImageDraw

REPO = Path(__file__).resolve().parents[4]
ROOT = REPO / 'output/hd2d-fx'
SHEET = REPO / '.ai/2d-art/tools/sheet.py'
OUT = ROOT / 'review'
ORDER = ['heal_area','crossbow_bolt','explosion','spore','slash_sword','ground_blast','lightning','arrow','pulse','hit_star','dash_line','heal_single','ground_boom','fireball','slash_axe']
OUT.mkdir(exist_ok=True)
(OUT / 'prepared').mkdir(exist_ok=True)
mapping = {}
for number, key in enumerate(ORDER, 1):
    folder = ROOT / 'sequence' / key
    rows = list(csv.DictReader((folder / 'frames.csv').open()))
    paths = sorted(folder.glob('*.png'))
    selected = []
    previous = None
    for i, row in enumerate(rows):
        if row['frame'] != previous and row['frame'] != '0':
            selected.append(i)
            previous = row['frame']
    selected = selected[:6]
    while len(selected) < 6:
        candidate = min(20, selected[-1] + 3)
        selected.append(candidate)
    for kind, indices in [('all', list(range(21))), ('compact', selected)]:
        strip = Image.new('RGB', (len(indices) * 320, 260), '#252624')
        draw = ImageDraw.Draw(strip)
        for column, index in enumerate(indices):
            frame = Image.open(paths[index]).convert('RGB')
            # 1/2 최근접. 원본 640×480은 그대로 보관한다.
            strip.paste(frame.resize((320, 240), Image.Resampling.NEAREST), (column * 320, 20))
            draw.text((column * 320 + 4, 4), f'#{number} t={index/60:.3f}s frame={rows[index]["frame"]}', fill='white')
        prepared = OUT / 'prepared' / f'{number:02d}-{kind}.png'
        strip.save(prepared)
        subprocess.run([sys.executable, str(SHEET), 'grid', str(OUT / f'{kind}-{number:02d}.png'), str(prepared), '--props', '--size', '260'], check=True)
    mapping[str(number)] = {'key': key, 'selected': selected, 'source': str(folder.relative_to(REPO))}
(OUT / 'blind-map.json').write_text(json.dumps(mapping, indent=2) + '\n')
html = ['<!doctype html><meta charset="utf-8"><title>HD2D FX game sequences</title>', '<style>body{background:#252624;color:#eee;font:16px sans-serif}img{max-width:100%;image-rendering:pixelated}details img{max-width:none}details{overflow:auto}</style>', '<p>실제 게임 캡처. 번호만 표시. 각 줄은 시간 순서. compact는 프레임 전환 대표6장, 펼치면 연속21장 전체.</p>']
for n in range(1, len(ORDER)+1):
    html += [f'<h2>#{n}</h2><img src="compact-{n:02d}.png"><details><summary>1/60s, 21 frames</summary><img src="all-{n:02d}.png"></details>']
(OUT / 'index.html').write_text('\n'.join(html))
