"""FX 마감 증거 감사. 게임·킷 파일을 바꾸지 않는다."""
from pathlib import Path
import csv
import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'output/hd2d-fx/verification'
now = datetime.now(timezone.utc).isoformat()
results = []
glow = {'hit_star','fireball','explosion','heal_single','lightning','pulse'}
for path in sorted((ROOT/'output/hd2d-fx/sequence').glob('*/frames.csv')):
    rows = list(csv.DictReader(path.open()))
    frames = []
    for row in rows:
        index = int(row['frame'])
        if index and (not frames or frames[-1] != index):
            frames.append(index)
    count = int(re.search(r'expectedFrames=(\d+)', (path.parent/'state.txt').read_text())[1])
    alphas = sorted({int(r['alpha']) for r in rows})
    core = max(int(r['glowCore']) for r in rows)
    binary = all(r['sourceAlphaBinary'] == 'True' for r in rows)
    contiguous = all(int(b['unityFrame']) - int(a['unityFrame']) == 1 for a,b in zip(rows,rows[1:]))
    timestep = all(abs(float(b['gameTime'])-float(a['gameTime'])-1/60)<.0001 for a,b in zip(rows,rows[1:]))
    ok = len(rows)==21 and frames[:count]==list(range(1,count+1)) and set(alphas)<={0,255} and binary and contiguous and timestep
    ok &= core>0 if path.parent.name in glow else core==0
    results.append(dict(key=path.parent.name,samples=len(rows),order=frames,alpha=alphas,glowCoreMax=core,binary=binary,
                        contiguous=contiguous,timestep_1_over_60=timestep,result='PASS' if ok else 'FAIL'))
assert len(results)==15
(OUT/'sequence-audit.json').write_text(json.dumps(dict(utc=now,result='PASS' if all(r['result']=='PASS' for r in results) else 'FAIL',effects=results),indent=2)+'\n')
old=ROOT/'Assets/Game/03.Resources/Art/AIGenerated/Effects'
guids={}
for path in [old.with_suffix('.meta'),*old.glob('*.png.meta')]:
    match=re.search(r'^guid: (\w+)',path.read_text(),re.M)
    if match:guids[match[1]]=str(path.relative_to(ROOT))
refs=[]
for path in (ROOT/'Assets/Game').rglob('*'):
    if path.suffix not in ('.cs','.unity','.prefab','.json','.spriteatlasv2','.asset','.mat'):continue
    if any(p in path.parts for p in ('Editor','Tests','AIGenerated','Round1','Round2','Round3','Round4','Round5','Round6','Round7','Round8')):continue
    text=path.read_text(errors='replace')
    for guid,source in guids.items():
        if guid in text:refs.append(dict(path=str(path.relative_to(ROOT)),legacy=source))
trial=[r for r in refs if r['path']=='Assets/Game/01.Scene/Lookdev_HD2D.unity']
active=[r for r in refs if r not in trial]
(OUT/'legacy-reference-audit.json').write_text(json.dumps(dict(utc=now,legacy_pngs=len(list(old.glob('*.png'))),active_game_reference_count=len(active),active_references=active,
    all_assets_game_reference_count=len(refs),preserved_lookdev_references=trial,scope_evidence='docs/game/hd2d-사전조사.md:282 — Lookdev_HD2D is an unbuilt first-stage trial; scene and Round1~8 preservation instruction applies'),ensure_ascii=False,indent=2)+'\n')
base='d3b01b15badf81361fcbfbe7d12f31cada741cf7'
changed=subprocess.check_output(['git','diff','--name-only',base],cwd=ROOT,text=True).splitlines()
source={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in changed if (ROOT/p).is_file() and p!='output/hd2d-fx/verification/fingerprints.json'}
artifacts={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT/'output/hd2d-fx').rglob('*') if p.is_file() and p.suffix in ('.png','.csv','.txt','.json') and p.parent!=OUT}
(OUT/'fingerprints.json').write_text(json.dumps(dict(utc=now,base=base,source=source,artifacts=artifacts),indent=2)+'\n')
print('sequence',len(results),'effects',sum(r['result']=='PASS' for r in results),'PASS; active legacy references',len(active),'; preserved Lookdev',len(trial))
