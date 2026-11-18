# Adapted from Assets/Game/02.Scripts/Editor/HD2D/Round8/GenerateFieldTextures.py SHA256 5f158876f889ba3807efb2b54c70179fb9f1aa93498ac489b28c0fcb167c5d6b
"""Blend adopted grass marks; no external image is loaded or sent."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'Assets/Game/03.Resources/Textures/HD2D'
ART = ROOT/'Assets/Game/03.Resources/Art/AIGenerated'


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    rng = np.random.default_rng(181108)
    w,h = 2048,1024
    grass_paths = [ART/f'Ground/grass/grass_{i:02}.png' for i in range(1,5)]
    patches=[]
    for path in grass_paths:
        source=np.asarray(Image.open(path).convert('RGBA'))
        luma=source[:,:,0]*.2126+source[:,:,1]*.7152+source[:,:,2]*.0722
        opaque=source[:,:,3]>127
        cuts=np.percentile(luma[opaque],[22,55,83])
        levels=np.digitize(luma,cuts,right=True)
        for yy in range(2,18):
            for xx in range(5,35):
                if opaque[yy:yy+5,xx:xx+8].all():
                    patches.append(levels[yy:yy+5,xx:xx+8])
    palette=np.array([[147,143,91],[158,153,98],[169,164,105],[179,174,112]],dtype=np.int16)
    levels=np.full((h+8,w+8),2,dtype=np.uint8)
    # No placement lattice or shared diagonal phase: independent native patch stamps.
    for _ in range(125000):
        patch=patches[int(rng.integers(len(patches)))]
        patch=np.rot90(patch,int(rng.integers(4)))
        if rng.random()<.5:patch=patch[:,::-1]
        ph,pw=patch.shape
        px=int(rng.integers(w));py=int(rng.integers(h))
        feather=np.ones((ph,pw),bool)
        feather[0]=rng.random(pw)>.45;feather[-1]=rng.random(pw)>.45
        feather[:,0]&=rng.random(ph)>.45;feather[:,-1]&=rng.random(ph)>.45
        target=levels[py:py+ph,px:px+pw];target[feather]=patch[feather]
    levels=levels[:h,:w]
    levels=np.repeat(np.repeat(levels[::2,::2],2,axis=0),2,axis=1)
    levels=np.median(np.stack([levels,np.roll(levels,1,0),np.roll(levels,-1,0),np.roll(levels,1,1),np.roll(levels,-1,1)]),axis=0).astype(np.uint8)
    # Broad nonperiodic, low-contrast light variation, bounded by 7/255 < .03.
    coarse=rng.integers(0,256,(9,17),dtype=np.uint8)
    sunlight=np.asarray(Image.fromarray(coarse).resize((w,h),Image.Resampling.BILINEAR)).astype(float)
    delta=np.clip(np.round((sunlight-128)/48)*3,-7,7)
    yy,xx=np.mgrid[:h,:w]
    ground=np.clip(palette[levels]+delta[:,:,None],0,255).astype(np.uint8)
    # Sparse fixed-seed soil/gravel clusters cover approximately 4 percent.
    gravel=np.asarray(Image.open(OUT/'FieldGravel.png').convert('RGB'))
    for _ in range(300):
        px=int(rng.integers(w-24)); py=int(rng.integers(h-16))
        cy,cx=np.mgrid[:16,:24]
        edge=((cx-11.5)/12)**2+((cy-7.5)/8)**2 < .65+(rng.random((8,12)).repeat(2,0).repeat(2,1))*.35
        target=ground[py:py+16,px:px+24]
        muted=(target.astype(float)*.65+gravel[:16,:24].astype(float)*.35).astype(np.uint8)
        target[edge]=muted[edge]
    Image.fromarray(ground).save(OUT/'FieldGrassOnly.png')
    # Ground rectangle -36..36: 48 texels/m X and 24 texels/m Z.
    mask=np.zeros((1728,3456),dtype=np.uint8)
    yy,xx=np.mgrid[:1728,:3456]; wx=(xx+.5)/48-36; wz=(yy+.5)/24-36
    cells=json.loads((ROOT/'Assets/Game/03.Resources/Resources/Data/Field/ground_path.json').read_text())['cells']
    for cell in cells:
        x=(cell['x']-cell['y'])/2; z=(cell['x']+cell['y'])/2
        # World-space L1 union follows the actual occupied isometric cells.
        dist=np.abs(wx-x)+np.abs(wz-z)
        mask[dist<.72]=255
    Image.fromarray(np.flipud(mask)).save(OUT/'FieldPathMask.png')
    # Keep the accepted dark water, not any reference pixels.
    water=np.array([[30,48,42],[43,65,54],[57,85,69]],dtype=np.uint8)
    wave=rng.random((512,1024));ripple=(wave+np.roll(wave,1,1)+np.roll(wave,2,1)+np.roll(wave,3,1))/4
    Image.fromarray(water[np.digitize(ripple,[.40,.72])]).save(OUT/'FieldWater.png')
    # Irregular connected mineral flecks, not a mortar grid or single-pixel noise.
    pattern=np.full((128,128),128,dtype=np.uint8)
    soil=np.full((128,128),1,dtype=np.uint8)
    for k in range(90):
        x=int(rng.integers(0,120));y=int(rng.integers(0,120))
        width=int(rng.integers(3,9));height=int(rng.integers(2,5))
        pattern[y:y+height,x:x+width]=64 if k%3 else 220
        if k%3==0:pattern[y+height,x+1:x+width-1]=64 if k%3 else 220
    for y in range(0,120,8):
        x=int(rng.integers(-8,0))
        while x<128:
            width=int(rng.integers(8,18));left=max(0,x);right=min(128,x+width)
            soil[y+1:y+5,left:right]=2
            soil[y+5:y+6,max(0,x+1):min(128,x+width-1)]=0
            x+=width+int(rng.integers(2,6))
    Image.fromarray(pattern).convert('RGB').save(OUT/'StonePattern.png')
    soil_colors=np.array([[65,56,39],[88,74,48],[112,96,63]],dtype=np.uint8)
    Image.fromarray(soil_colors[soil]).save(OUT/'FieldSoil.png')


if __name__=='__main__':
    main()
