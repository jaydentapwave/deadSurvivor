"""Task-local sprite assembly, existing-tool invocation and artifact export.
No new checker: numeric observations are collected in the report assembly.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageSequence
import numpy as np
import json, subprocess, sys, os, datetime, hashlib

P = Path('output/hd2d-dq/codex-sprites')
ART = Path('Assets/Game/03.Resources/Art/HD2D/RoundDQ2/Characters')
REF = {d: Image.open(ART / f'bruno_dq_{d}.png').convert('RGBA') for d in ('right','down')}
ENV = dict(os.environ, PYTHONDONTWRITEBYTECODE='1')
ENV['PYTHONPATH'] = str(P/'.deps')

def run_tool(name, args, log):
    r = subprocess.run([sys.executable, '-B', str(P/'art_tool.py'), name, *map(str,args)], env=ENV, capture_output=True, text=True)
    Path(log).parent.mkdir(parents=True, exist_ok=True)
    Path(log).write_text(r.stdout+r.stderr)
    return r.returncode

def save_palette(d):
    a = np.array(REF[d]); colors=np.unique(a[:,:,:3][a[:,:,3]>0],axis=0)
    path=P/f'inputs/palette_{d}.json'
    path.write_text(json.dumps(['#%02x%02x%02x'%tuple(c) for c in colors]))
    return path

def patch(im, ref, box, shift=(0,0)):
    x0,y0,x1,y1=box
    im.paste((0,0,0,0),(x0+shift[0],y0+shift[1],x1+shift[0],y1+shift[1]))
    im.alpha_composite(ref.crop(box),(x0+shift[0],y0+shift[1]))

def reduce_sheet(folder,d,head=True,torso=False):
    folder=P/folder
    im=Image.open(folder/'generated.png').convert('RGBA')
    # Equal horizontal cells; retain original sheet and exact crops.
    for i in range(4):
        crop=im.crop((round(i*im.width/4),0,round((i+1)*im.width/4),im.height))
        src=folder/f'crop_{i:02}.png';crop.save(src)
        a=np.array(crop); rgb=a[:,:,:3].astype(int)
        # Alpha plus magenta key, no filtering, no interpolation.
        key=(rgb[:,:,0]>180)&(rgb[:,:,2]>140)&(rgb[:,:,1]<100)
        a[:,:,3]=np.where((a[:,:,3]>=128)&~key,255,0)
        mask=a[:,:,3]>0
        yy,xx=np.where(mask); box=(xx.min(),yy.min(),xx.max()+1,yy.max()+1)
        w=round((box[2]-box[0])/(box[3]-box[1])*56)
        matte=Image.new('RGBA',crop.size,'#ff00ff');matte.alpha_composite(Image.fromarray(a));matte.convert('RGB').save(folder/f'matte_{i:02}.png')
        gridrc=run_tool('pixelize.py',['grid',folder/f'matte_{i:02}.png','--size',f'{w}x56'],folder/f'grid_{i:02}.log')
        # LOW/grid failures stay in evidence. Candidate is an explicitly failed
        # experiment, not accepted production art; never hide these warnings.
        run_tool('pixelize.py',['one',folder/f'matte_{i:02}.png','--out',folder/f'pixelized_{i:02}.png','--size',f'{w}x56','--palette',save_palette(d)],folder/f'pixelize_{i:02}.log')
        dot=Image.open(folder/f'pixelized_{i:02}.png').convert('RGBA')
        a=np.array(dot);feet=np.where((a[-3:,:,3]>0).any(0))[0]
        cx=round((int(feet.min())+int(feet.max()))/2)
        canvas=Image.new('RGBA',(72,72));canvas.alpha_composite(dot,(36-cx,8));canvas.save(folder/f'unretouched_{i:02}.png')
        if head: patch(canvas,REF[d],(0,0,72,31))
        if torso: patch(canvas,REF[d],(30,31,40,46) if 'attack' in str(folder) else (0,31,72,47))
        canvas.save(folder/f'frame_{i:02}.png')

def piece_c():
    """Integer-only pieces from accepted base. Hidden shin uses same source pixels."""
    for d in ('right','down'):
        folder=P/'C'/f'walk_{d}';folder.mkdir(parents=True,exist_ok=True)
        base=REF[d]
        if d=='right':
            shin=base.crop((31,54,42,64))
            # Foot identity: nearer shin original; far shin uses existing darker colors.
            far=np.array(shin); pal=np.array(json.loads(save_palette(d).read_text()))
            colors={tuple(c): tuple(c) for c in np.unique(far[:,:,:3].reshape(-1,3),axis=0)}
            darkmap={(74,86,131):(51,59,99),(99,112,156):(74,86,131),(172,176,198):(99,112,156),(251,251,251):(172,176,198),(196,109,81):(146,60,36)}
            for old,new in darkmap.items():far[:,:,:3][np.all(far[:,:,:3]==old,axis=2)]=new
            far=Image.fromarray(far)
            phases=[(-4,0,4,-2),(-1,0,1,-2),(4,-2,-4,0),(1,-2,-1,0)]
            for i,(nx,ny,fx,fy) in enumerate(phases):
                frame=base.copy();frame.paste((0,0,0,0),(31,54,44,64))
                frame.alpha_composite(far,(31+fx,54+fy));frame.alpha_composite(shin,(31+nx,54+ny))
                # Connect moved knees by extending the original top row; integers only.
                for x,dy in [(nx,ny),(fx,fy)]:
                    row=shin.crop((0,0,11,1))
                    for y in range(52,54+dy):frame.alpha_composite(row,(31+x,y))
                frame.save(folder/f'frame_{i:02}.png')
        else:
            left=base.crop((29,53,37,64));right=base.crop((39,53,46,64))
            for i,(ly,ry) in enumerate([(0,-3),(0,-1),(-3,0),(-1,0)]):
                frame=base.copy();frame.paste((0,0,0,0),(29,53,37,64));frame.paste((0,0,0,0),(39,53,46,64))
                frame.alpha_composite(left,(29,53+ly));frame.alpha_composite(right,(39,53+ry));frame.save(folder/f'frame_{i:02}.png')
    folder=P/'C/attack_right';folder.mkdir(parents=True,exist_ok=True)
    base=REF['right'];sword=base.crop((36,47,53,52))
    for i,(trans,xy) in enumerate([(Image.Transpose.ROTATE_90,(38,32)),(None,(40,43)),(Image.Transpose.ROTATE_270,(39,48)),(None,(36,47))]):
        f=base.copy();f.paste((0,0,0,0),(36,47,53,52));p=sword.transpose(trans) if trans is not None else sword
        f.alpha_composite(p,xy);f.save(folder/f'frame_{i:02}.png')

def guides():
    folder=P/'B/walk_right';folder.mkdir(parents=True,exist_ok=True)
    b=REF['right']
    for i,(near,far) in enumerate([([(35,51),(40,56),(44,63)],[(35,51),(31,57),(29,61)]), ([(35,51),(35,57),(35,63)],[(35,51),(32,57),(31,60)]), ([(35,51),(31,57),(29,61)],[(35,51),(40,56),(44,63)]), ([(35,51),(32,57),(31,60)],[(35,51),(35,57),(35,63)])]):
        rough=b.copy();rough.paste((0,0,0,0),(30,52,48,64));dr=ImageDraw.Draw(rough)
        dr.line(far,fill='#25baff',width=4);dr.line(near,fill='#ffdf00',width=4)
        rough.resize((1152,1152),Image.Resampling.NEAREST).save(folder/f'guide_{i:02}_16x.png')

def previews(folder,label):
    folder=P/folder;frames=sorted(folder.glob('frame_*.png'))
    reason='EXPERIMENT - see verdict table; independent review pending'
    for scale in (4,8):
        run_tool('sheet.py',['anim',folder/f'anim_{scale}x.gif',*frames,'--fps','10','--size',str(72*scale),'--label',label,'--unverified',reason],folder/f'anim_{scale}x.log')
    run_tool('sheet.py',['grid',folder/'row_4x.png',*frames,'--frames','--size','288','--numbered','--unverified',reason],folder/'row.log')
    run_tool('frames.py',['holes',*frames,'--ref',ART/f'bruno_dq_{"down" if "down" in folder.name else "right"}.png'],folder/'holes.log')
    # Installed sheet.py has no decode subcommand. Decode the actual saved GIF,
    # not the PNG sources; expose every output frame in order for visual opening.
    dec=folder/'decoded';dec.mkdir(exist_ok=True)
    gif=Image.open(folder/'anim_8x.gif')
    for i,f in enumerate(ImageSequence.Iterator(gif)):
        f.convert('RGB').save(dec/f'{i:02}.png')
    (folder/'decode.json').write_text(json.dumps({'file':'anim_8x.gif','frames':gif.n_frames,'duration_ms':gif.info.get('duration'),'decoder':'Pillow ImageSequence; installed sheet.py has no decode command'},indent=2))

def repair_c():
    """Second and last C assembly: reconnect joints and use a connected sword hand."""
    import shutil
    for clip in ('walk_right','walk_down','attack_right'):
        folder=P/'C'/clip
        old=folder/'attempt1';old.mkdir(exist_ok=True)
        for f in folder.glob('*'):
            if f.is_file() and f.suffix in ('.png','.gif','.log','.json'):shutil.copy2(f,old/f.name)
    # Right: fixed torso, darker far shin clearly differs from the near shin.
    folder=P/'C/walk_right';base=REF['right'];shin=base.crop((31,54,42,64))
    far=np.array(shin)
    for old,new in [((74,86,131),(51,59,99)),((99,112,156),(51,59,99)),((172,176,198),(74,86,131)),((251,251,251),(99,112,156)),((196,109,81),(82,44,47))]:
        far[:,:,:3][np.all(far[:,:,:3]==old,axis=2)]=new
    far=Image.fromarray(far)
    # Both feet remain within bottom three rows, anchor midpoint stable.
    for i,(nx,ny,fx,fy) in enumerate([(4,0,-4,-2),(1,0,-1,-2),(-4,-2,4,0),(-1,-2,1,0)]):
        f=base.copy();f.paste((0,0,0,0),(31,52,46,64))
        # Pelvis stays joined to both shins, not a free rectangle outside silhouette.
        dr=ImageDraw.Draw(f);dr.polygon([(31,51),(40,51),(42+nx,55+ny),(31+nx,55+ny)],fill=(51,59,99,255))
        dr.polygon([(32,51),(39,51),(42+fx,55+fy),(31+fx,55+fy)],fill=(16,12,33,255))
        f.alpha_composite(far,(31+fx,54+fy));f.alpha_composite(shin,(31+nx,54+ny));f.save(folder/f'frame_{i:02}.png')
    # Down: a smaller lift retains both feet in the anchor band, no knee holes.
    folder=P/'C/walk_down';base=REF['down'];left=base.crop((29,53,37,64));right=base.crop((39,53,46,64))
    for i,(ly,ry) in enumerate([(0,-2),(0,-1),(-2,0),(-1,0)]):
        f=base.copy();f.paste((0,0,0,0),(29,53,37,64));f.paste((0,0,0,0),(39,53,46,64))
        f.alpha_composite(left,(29,53+ly));f.alpha_composite(right,(39,53+ry));f.save(folder/f'frame_{i:02}.png')
    # Attack: discrete right-angle sword poses, connected arm; this cannot make
    # a smooth diagonal swing and that limitation is retained as FAIL.
    folder=P/'C/attack_right';base=REF['right'];sword=base.crop((36,47,53,52))
    for i,(trans,xy,hand) in enumerate([(Image.Transpose.ROTATE_90,(44,30),(46,46)),(None,(41,42),(41,44)),(Image.Transpose.ROTATE_270,(43,46),(45,46)),(None,(36,47),(37,50))]):
        f=base.copy();f.paste((0,0,0,0),(36,47,53,52))
        dr=ImageDraw.Draw(f);dr.rectangle((36,47,40,53),fill=(51,59,99,255))
        dr.line([(35,39),(36,46),hand],fill=(14,10,25,255),width=5)
        dr.line([(35,39),(36,46),hand],fill=(99,112,156,255),width=3)
        dr.rectangle((hand[0]-1,hand[1]-1,hand[0]+1,hand[1]+1),fill=(252,182,96,255))
        f.alpha_composite(sword.transpose(trans) if trans is not None else sword,xy)
        f.save(folder/f'frame_{i:02}.png')
    # Fill only enclosed joint pinholes, using the existing tool's flood routine.
    # Every touched coordinate is recorded, never a changed checker/threshold.
    sys.path.insert(0,str(Path('.ai/2d-art/tools').resolve()))
    import frames
    edits=[]
    for path in (P/'C').glob('*/frame_*.png'):
        a=np.array(Image.open(path));clear=np.pad(a[:,:,3]==0,1,constant_values=True);border=np.zeros_like(clear)
        border[0,:]=border[-1,:]=border[:,0]=border[:,-1]=True
        holes=(clear & ~frames._flood(border,clear))[1:-1,1:-1]
        for y,x in np.argwhere(holes):
            color=(16,12,33,255);a[y,x]=color
            edits.append({'file':str(path.relative_to(P)),'xy':[int(x),int(y)],'before':[0,0,0,0],'after':list(color),'reason':'enclosed pinhole at shifted knee/arm joint; existing darkest armor palette color'})
        Image.fromarray(a).save(path)
    (P/'C/retouch-log.json').write_text(json.dumps(edits,indent=2))

def repair_a_attack():
    import shutil
    folder=P/'A/attack_right'
    backup=folder/'before-sword-restoration';backup.mkdir(exist_ok=True)
    for path in folder.glob('frame_*.png'):shutil.copy2(path,backup/path.name)
    f=Image.open(folder/'frame_00.png').convert('RGBA');raw=Image.open(folder/'unretouched_00.png').convert('RGBA')
    # Raised weapon was lost under a full-row head reset. Keep the weapon at
    # its observed location, then layer the exact reference head in front.
    f.alpha_composite(raw.crop((45,12,52,32)),(45,12))
    head=REF['right'].crop((0,0,72,31));f.alpha_composite(head,(0,0))
    f.save(folder/'frame_00.png')

def finish_b():
    # Fixed walking equipment is restored from the approved reference, after
    # the frame-local pose generation. Do not let a different sword pop in F3.
    for clip,d in [('walk_right','right'),('walk_down','down')]:
        folder=P/'B'/clip
        for i in range(4):
            path=folder/f'frame_{i:02}.png';f=Image.open(path).convert('RGBA')
            if d=='right':patch(f,REF[d],(0,31,72,52))
            else:
                patch(f,REF[d],(23,46,29,62))
                patch(f,REF[d],(41,34,53,52))
            f.save(path)
    # The raised sword is to the right of the source head, not a head pixel.
    # The full-row reset must not erase it. Restore that observed region.
    folder=P/'B/attack_right'
    f=Image.open(folder/'frame_00.png').convert('RGBA')
    raw=Image.open(folder/'unretouched_00.png').convert('RGBA')
    f.alpha_composite(raw.crop((51,0,72,31)),(51,0))
    f.alpha_composite(REF['right'].crop((0,0,72,31)),(0,0))
    f.save(folder/'frame_00.png')

if __name__=='__main__':
    cmd=sys.argv[1]
    if cmd=='C':piece_c();guides()
    elif cmd=='repair-C':repair_c()
    elif cmd=='repair-A-attack':repair_a_attack()
    elif cmd=='finish-B':finish_b()
    elif cmd=='reduce':reduce_sheet(sys.argv[2],sys.argv[3],torso='--torso' in sys.argv)
    elif cmd=='preview':previews(sys.argv[2],sys.argv[2])
