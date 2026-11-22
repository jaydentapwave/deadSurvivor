#!/usr/bin/env python3
"""Pack only adopted DQ gameplay frames. Requires the existing studio-kit Python/Pillow.
No generation, installation, source edits or network access.
"""
import argparse
import hashlib
import json
import math
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Assets/Game/03.Resources/Art/HD2D/DQAppliedCharacters'
DATA = ROOT / 'Assets/Game/03.Resources/Resources/Data/DQ'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def build(source):
    judge = source / 'judge-night'
    actors = []
    evidence = {'judgments': {}, 'frames': [], 'differences': [], 'exceptions': []}
    for actor in ['bruno', 'lena', 'mira', 'bolt', 'slime', 'mushroom', 'rock', 'boar', 'puffer']:
        hero = actor in ['bruno', 'lena', 'mira', 'bolt']
        judgment = judge / ('confirm-hero-' + actor + '.md' if hero else 'confirm-monsters-' + ('a' if actor in ['slime','mushroom','rock'] else 'b') + '.md')
        evidence['judgments'][judgment.name] = sha(judgment)
        # Read only final selection rows: these contain source paths, unlike quality comparison tables.
        selections = {}
        for line in judgment.read_text().splitlines():
            cells = [c.strip().strip('`') for c in line.split('|')[1:-1]]
            if len(cells) < 4 or not re.match(r'(walk|idle|attack|fall|move|shoot|contact|windup|dash|recover|inflate)_', cells[0].split('/')[-1]):
                continue
            match = re.search(r'(?:output/hd2d-dq/)?(motions(?:-fix[12]|-monsters(?:-r2)?)?/[\w/]+)', cells[2])
            if not match or 'boar/fall_front' in match[1]:
                continue
            key = cells[0].split('/')[-1]
            if '/' in cells[0] and cells[0].split('/')[0] != actor:
                continue
            selected = match[1].rstrip('/')
            if selected.split('/')[1] != actor:
                continue
            selections[key] = (selected, cells[3])
        if actor == 'rock':
            selections['contact_right'] = ('motions-monsters/rock/contact_right', '약함: confirm 조건부 구멍 없는 원본')
        expected = 16 if hero else (17 if actor == 'boar' else 9)
        assert len(selections) == expected, (actor,len(selections),selections)
        rows = []; clips = []; sprites = []; cache = {}
        canvas = 72 if hero else 64
        pivot_x, pivot_y = (36,64) if hero else (32,64)
        def sprite(path):
            path = path.resolve()
            if path in cache:
                return cache[path]
            im = Image.open(path).convert('RGBA')
            assert im.size == (canvas,canvas), (path,im.size)
            key = 'dq_' + actor + '_' + str(len(rows)).zfill(4)
            cache[path] = key; rows.append((key,im))
            sprites.append({'key':key,'index':len(rows)-1,'size':canvas,'pivotX':pivot_x,'pivotY':pivot_y})
            evidence['frames'].append({'actor':actor,'key':key,'source':str(path.relative_to(source)) if path.is_relative_to(source) else str(path.relative_to(ROOT)),'sha256':sha(path)})
            return key
        for clip_id,(rel,quality) in sorted(selections.items()):
            folder = source / rel
            frames = sorted(folder.glob('frame_[0-9][0-9].png'))
            durations = json.loads((folder/'durations.json').read_text())
            assert frames and len(frames)==len(durations) and min(durations)>0,(rel,len(frames),durations)
            event = -1
            meta = folder/'clip.json'
            if meta.exists():
                obj=json.loads(meta.read_text())
                times=obj.get('durations_ms',obj.get('ms'))
                assert times == durations, (rel,'metadata times differ')
                e=obj.get('event_frame_zero_based',obj.get('event_frame_0based'))
                event = e if e is not None else -1
            if clip_id.startswith('attack_'):
                event = 3 if actor=='bruno' and clip_id in ['attack_up','attack_front'] else 2
            if event>=0: assert event < len(frames)
            orig=source/'motions-monsters'/actor/clip_id/'durations.json'
            if not hero and orig.exists() and json.loads(orig.read_text())!=durations:
                evidence['differences'].append({'actor':actor,'clip':clip_id,'old':json.loads(orig.read_text()),'selected':durations})
            clips.append({'id':clip_id,'frameKeys':[sprite(f) for f in frames],'durationsMs':durations,'loop':clip_id.startswith(('walk_','move_','idle_','dash_','windup_')),'eventFrame':event,'sourceClip':rel,'quality':quality})
        directions=['down','front','right','upright','up'] if hero else ['front','front','right','upright','up']
        statics=[]
        for direction in directions:
            p=ROOT/'Assets/Game/03.Resources/Art/HD2D/RoundDQ2'/('Characters' if hero else 'Monsters')/(actor+'_dq_'+direction+'.png')
            statics.append(sprite(p))
        lying=''
        if hero:
            lying=sprite(source/selections['fall_right'][0]/'lying.png')
        if hero and len(next(c for c in clips if c['id']=='fall_right')['frameKeys'])!=10:
            evidence['exceptions'].append({'actor':actor,'clip':'fall_right','expected':10,'actual':len(next(c for c in clips if c['id']=='fall_right')['frameKeys']),'reason':'confirm 최종 선택의 12장 보존, 숫자 기준과 불일치. 임의 절삭하지 않음'})
        # Four-pixel gutter around every cell; source canvas and feet unchanged.
        stride=canvas+8; cols=16; height=math.ceil(len(rows)/cols)*stride
        sheet=Image.new('RGBA',(cols*stride,height))
        for i,(key,im) in enumerate(rows): sheet.paste(im,((i%cols)*stride+4,(i//cols)*stride+4))
        ART.mkdir(parents=True,exist_ok=True)
        sheet.save(ART/(actor+'.png'))
        phase=[{'phase':p,'action':a} for p,a in [('Approach','move' if actor in ['slime','mushroom','rock'] else 'walk'),('Swarm','walk'),('Windup','windup'),('Dash','dash'),('Recover','recover'),('Fuse','inflate')]]
        actors.append({'actorId':actor,'hero':hero,'atlasPath':'Art/DQ/'+actor,'sheet':str((ART/(actor+'.png')).relative_to(ROOT)),'columns':cols,'stride':stride,'sprites':sprites,'clips':clips,'directionSources':directions,'staticKeys':statics,'lyingKey':lying,'phaseBindings':phase})
    DATA.mkdir(parents=True,exist_ok=True)
    (DATA/'dq_character_clips.json').write_text(json.dumps({'actors':actors},ensure_ascii=False,indent=2)+'\n')
    (ART/'source.txt').write_text('Adopted DQ gameplay sprites only. Repack: existing studio-kit Python Tools/dq_character_pack.py --source <allowed output/hd2d-dq>.\nSelection and source hashes: source-manifest.json. No new generation.\n')
    (ART/'source-manifest.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n')
    out=ROOT/'output/hd2d-dq/apply/8';out.mkdir(parents=True,exist_ok=True)
    (out/'pack-report.json').write_text(json.dumps({'actors':len(actors),'sprites':len(evidence['frames']),'differences':evidence['differences'],'exceptions':evidence['exceptions']},ensure_ascii=False,indent=2))
    print('packed',len(actors),'actors',len(evidence['frames']),'frames; exceptions:',evidence['exceptions'])

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--source',required=True,type=Path);args=ap.parse_args();build(args.source)
