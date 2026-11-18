# Adapted from Assets/Game/02.Scripts/Editor/HD2D/Round8/GenerateFieldTextures.py SHA256 5f158876f889ba3807efb2b54c70179fb9f1aa93498ac489b28c0fcb167c5d6b
"""Blend adopted grass marks; no external image is loaded or sent."""
from pathlib import Path
import json
import shutil
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'Assets/Game/03.Resources/Textures/HD2D'
ART = ROOT/'Assets/Game/03.Resources/Art/AIGenerated'


def path_mask(cells, settings, seed, width=3456, height=1728):
    """Point mask over -36..36m, driven by the baked layout's metre-valued controls."""
    yy, xx = np.mgrid[:height, :width]
    wx = ((xx + .5) * 72 / width - 36).astype(np.float32)
    wz = ((yy + .5) * 72 / height - 36).astype(np.float32)
    nearest = np.full((height, width), np.inf, dtype=np.float32)
    for cell in cells:
        x = (cell['x'] - cell['y']) / 2
        z = (cell['x'] + cell['y']) / 2
        np.minimum(nearest, np.abs(wx-x)+np.abs(wz-z), out=nearest)
    along = (wx+wz) / np.sqrt(2)
    variation = (np.sin(along*1.38)+.4*np.sin(along*4.2))/1.4
    # Independent stream: changing the path must not perturb other generated textures.
    edge = np.random.default_rng(seed).uniform(-1, 1, (height, width))
    half_width = settings['halfWidth'] + settings['widthVariation']*variation + settings['edgeVariation']*edge
    return np.where(nearest < half_width, 255, 0).astype(np.uint8)


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
    Image.fromarray(ground).save(OUT/'FieldGrassOnly.png')
    # The selected PCG owns the path boundary; do not reconstruct it from iso cells.
    shutil.copyfile(ROOT/'output/map-pcg/seed-8/export/FieldPathMask.png', OUT/'FieldPathMask.png')
    # Accepted water, soil and mineral textures remain authored assets.


if __name__=='__main__':
    main()
