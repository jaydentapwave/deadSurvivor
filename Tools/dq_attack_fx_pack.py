"""Pack adopted, separate attack cues; never reads another checkout's game sources."""
import argparse
import hashlib
import json
import io
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/Game/03.Resources/Art/HD2D/DQAppliedFx'

def build(source):
    catalog = json.loads((ROOT / 'Assets/Game/03.Resources/Resources/Data/DQ/dq_character_clips.json').read_text())
    entries = []
    packed = []
    for actor in catalog['actors']:
        if not actor['hero']:
            continue
        for clip in actor['clips']:
            if not clip['id'].startswith('attack_'):
                continue
            direction = clip['id'][7:]
            folder = source / clip['sourceClip']
            native_direction = folder.name.removeprefix('attack_')
            event = clip['eventFrame']
            sheet = Image.new('RGBA', (72 * 7, 72))
            provenance = []
            for frame in range(event, min(7, event + (1 if actor['actorId'] == 'lena' else 3))):
                cue = Image.new('RGBA', (72, 72))
                if actor['actorId'] == 'bruno':
                    choices = [folder.parent / ('effect_' + direction), folder.parent / ('slash_' + direction)]
                    chosen = next((p / f'frame_{2 + frame - event:02}.png' for p in choices if (p / f'frame_{2 + frame - event:02}.png').is_file()), None)
                    if chosen is None:
                        raise FileNotFoundError((actor['actorId'], clip['id'], choices))
                    cue = Image.open(chosen).convert('RGBA')
                elif actor['actorId'] == 'lena':
                    choices = [folder.parent / ('effect_' + native_direction), folder.parent / ('attack_' + native_direction + '_effect')]
                    chosen = next((p / 'frame_02.png' for p in choices if (p / 'frame_02.png').is_file()), None)
                    if chosen:
                        cue = Image.open(chosen).convert('RGBA')
                    else:
                        chosen = folder / 'arrow.png'
                        image = Image.open(chosen).convert('RGBA')
                        values = json.loads((folder / 'game-values.json').read_text())
                        offset = [round(a-b) for a,b in zip(values['effect_attachment'], values['effect_png_origin'])]
                        cue.alpha_composite(image, tuple(offset))
                elif actor['actorId'] == 'mira':
                    prefix = 'glow' if (folder / 'fx/glow_00.png').is_file() else 'orb_glow'
                    chosen = folder / f'fx/{prefix}_{frame-event:02}.png'
                    cue = Image.open(chosen).convert('RGBA')
                else:
                    files = sorted((folder / 'effects').glob('ring_*.png'))
                    chosen = files[frame-event]
                    cue = Image.open(chosen).convert('RGBA')
                if cue.size != (72,72):
                    raise ValueError(('unexpected cue canvas',chosen,cue.size))
                alpha = set(cue.getchannel('A').getdata())
                if alpha - {0,255}:
                    raise ValueError(('nonbinary alpha',chosen,alpha))
                if cue.getchannel('A').getbbox() is None:
                    continue
                sheet.paste(cue, (frame*72,0))
                provenance.append({'frame':frame,'source':str(chosen.relative_to(source)),'sha256':hashlib.sha256(chosen.read_bytes()).hexdigest()})
            path = DEST / (actor['actorId'] + '_' + clip['id'] + '.png')
            buffer = io.BytesIO()
            sheet.save(buffer, format="PNG")
            packed.append((path, buffer.getvalue()))
            entries.append({'actor':actor['actorId'],'direction':direction,'eventFrame':event,'sheet':str(path.relative_to(ROOT)), 'sha256':hashlib.sha256(buffer.getvalue()).hexdigest(), 'sources':provenance})
    manifest = {'schema':1,'ppu':48,'size':72,'pivot':[36,8],'frames':7,'entries':entries,'policy':'Separate attack cues at actual body event frame. Lena only1-frame launch cue; real projectile path stays CombatWorldPresenter-owned. Mira glow only, real magic projectile unchanged. Source art unchanged; no generated art.'}
    for path, content in packed:
        path.write_bytes(content)
    (DEST / 'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('Packed',len(entries),'direction cues')

if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--source',required=True,type=Path);args=parser.parse_args();build(args.source.resolve())
