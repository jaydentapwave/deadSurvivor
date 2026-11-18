"""F0 수정 행만, 같은 배율 영웅 + 시간 순서. 내보내기는 킷 sheet.py만 사용."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[2]
SHEET = REPO / '.ai/2d-art/tools/sheet.py'
ORIGINAL = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
HERO = ORIGINAL / 'output/assets/hd2d-8dir/heroes/bruno_right.png'
GROUPS = [
    ('ground_pulse', sorted((ROOT / 'raster').glob('ground_pulse_*.png')), 20),
    ('dash_line', sorted((ROOT / 'raster').glob('dash_line_*.png')), 12),
    ('hit_star', sorted((ROOT / 'frames/hit_star').glob('*.png')), 20),
    ('ground_boom', sorted((ROOT / 'raster').glob('ground_boom_*.png')), 16),
    ('chain_bolt', sorted((ROOT / 'raster').glob('chain_bolt_*.png')), 12),
    ('ground_heal', sorted((ROOT / 'raster').glob('ground_heal_*.png')), 20),
    ('ground_blast', sorted((ROOT / 'raster').glob('ground_blast_*.png')), 8),
]
BG = (155, 151, 101, 255)

def pad(image):
    canvas = Image.new('RGBA', (188, 96), BG)
    canvas.alpha_composite(image, ((188 - image.width) // 2, (96 - image.height) // 2))
    return canvas


def main():
    dst = ROOT / 'review'
    neutral = dst / 'numbered'
    neutral.mkdir(parents=True, exist_ok=True)
    hero = Image.open(HERO).convert('RGBA')
    pad(hero).save(neutral / 'reference.png')
    mapping = {}
    for number, (key, files, fps) in enumerate(GROUPS, 1):
        paths, animation_paths = [], []
        for index, source in enumerate(files, 1):
            image = Image.open(source).convert('RGBA')
            target = neutral / f'{number:02d}-{index:02d}.png'
            pad(image).save(target)
            paths.append(str(target))
            # 같은 밀도의 캐릭터를 매 프레임 왼쪽에 둔다.
            frame = Image.new('RGBA', (264, 96), BG)
            frame.alpha_composite(hero, (4, (96 - hero.height) // 2))
            frame.alpha_composite(image, (76 + (188 - image.width) // 2, (96 - image.height) // 2))
            anim = neutral / f'{number:02d}-anim-{index:02d}.png'
            frame.save(anim)
            animation_paths.append(str(anim))
        subprocess.run([sys.executable, str(SHEET), 'grid', str(dst / f'row-{number:02d}.png'), str(neutral / 'reference.png'), *paths,
                        '--props', '--numbered', '--size', '192'], check=True)
        subprocess.run([sys.executable, str(SHEET), 'anim', str(dst / f'motion-{number:02d}.png'), *animation_paths,
                        '--fps', str(fps), '--size', '192', '--label', f'#{number}',
                        '--unverified', 'F0 corrected effects; cross-model read pending; no character identity'], check=True)
        decoded = []
        playback = Image.open(dst / f'motion-{number:02d}.png')
        for index in range(playback.n_frames):
            playback.seek(index)
            path = neutral / f'{number:02d}-decoded-{index + 1:02d}.png'
            playback.convert('RGBA').save(path)
            decoded.append(str(path))
        subprocess.run([sys.executable, str(SHEET), 'grid', str(dst / f'decoded-{number:02d}.png'), *decoded,
                        '--props', '--numbered', '--size', str(playback.height)], check=True)
        mapping[str(number)] = {'key': key, 'fps': fps, 'frames': [str(p.relative_to(ROOT)) for p in files]}
    (dst / 'blind-map.json').write_text(json.dumps(mapping, indent=2) + '\n')
    html = ['<!doctype html><meta charset="utf-8"><title>F0 corrected effects — review pending</title>',
            '<style>body{background:#252624;color:#eee;font:16px sans-serif}img{image-rendering:pixelated;max-width:100%}</style>',
            '<p>F0 수정 7행 / 게임 캡처 아님 / 판독 대기. 왼쪽 인물과 효과는 같은 도트 배율.</p>']
    for number in range(1, len(GROUPS) + 1):
        html += [f'<h2>#{number}</h2>', f'<img src="motion-{number:02d}.png"><br><img src="row-{number:02d}.png">']
    (dst / 'index.html').write_text('\n'.join(html))
    manifest = {}
    for folder in ('frames', 'raster'):
        for p in sorted((ROOT / folder).rglob('*.png')):
            image = Image.open(p).convert('RGBA')
            manifest[str(p.relative_to(ROOT))] = {'sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
                'size': image.size, 'bounds': image.getbbox(), 'alpha': sorted(set(image.getchannel('A').getdata()))}
    (dst / 'asset-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')

if __name__ == '__main__':
    main()
