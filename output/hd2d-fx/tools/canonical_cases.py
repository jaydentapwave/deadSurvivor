"""F0에서 판독 채택된 r003만 사용하여 51개 비교 표본을 내보낸다."""
from pathlib import Path
import hashlib
import importlib.util
import json
ROOT = Path(__file__).resolve().parents[3]
source = ROOT/'output/assets/hd2d-fx/tools/raster.py'
spec = importlib.util.spec_from_file_location('fxraster', source)
raster = importlib.util.module_from_spec(spec)
spec.loader.exec_module(raster)
folder = ROOT/'output/hd2d-fx/verification/r002/reference'
folder.mkdir(parents=True, exist_ok=True)
cases = []
def add(kind, frames, **args):
    for frame, pixels in enumerate(frames):
        file = folder/f'{len(cases):02d}.png'
        raster.save(pixels,file)
        cases.append(dict(kind=kind,frame=frame,file=str(file.relative_to(ROOT)),sha256=hashlib.sha256(file.read_bytes()).hexdigest(),**args))
for kind, radius in [('heal',.1),('heal',1.8984),('pulse',1.13),('blast',1.017),('boom',.8475)]:
    add(kind,raster.ground_ring_frames(kind,radius),radius=radius)
for angle in [0,35,90,180,270]:
    add('dash',raster.dash_line_frames(3.6386,angle)[0],radius=3.6386,angle=angle)
for dx,dy,seed in [(1,0,1),(110,28,7),(-110,-28,7),(0,80,7),(80,0,4294967295)]:
    add('chain',raster.chain_bolt_frames(dx,dy,seed),dx=dx,dy=dy,seed=seed)
assert len(cases)==51
original=Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/output/assets/hd2d-fx/tools/raster.py')
manifest=dict(version='r003',source=str(source),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
    original_source=str(original),original_sha256=hashlib.sha256(original.read_bytes()).hexdigest(),
    authority='F0 director correction instructions + returned Claude F0-A1~A6 VERDICT: PASS; imported at 94a6e39. Original pre-correction source is provenance only.',cases=cases)
(folder/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
print('canonical r003',manifest['source_sha256'],'cases',len(cases))
