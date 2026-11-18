"""Blend adopted grass marks; no external image is loaded or sent."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[6]
OUT = ROOT/'Assets/Game/03.Resources/Art/HD2D/Round7'
ART = ROOT/'Assets/Game/03.Resources/Art/AIGenerated'


def main():
    OUT.mkdir(exist_ok=True)
    rng = np.random.default_rng(181104)
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
    levels=np.full((h+8,w+12),2,dtype=np.uint8)
    # Irregular overlapping 8x5 native grass fragments; stochastic texel feather.
    for yy in range(0,h,3):
        for xx in range(0,w,6):
            px=xx+int(rng.integers(0,4));py=yy+int(rng.integers(0,2))
            patch=patches[int(rng.integers(len(patches)))]
            feather=np.ones((5,8),bool)
            feather[0]=rng.random(8)>.45;feather[-1]=rng.random(8)>.45
            feather[:,0]&=rng.random(5)>.45;feather[:,-1]&=rng.random(5)>.45
            target=levels[py:py+5,px:px+8];target[feather]=patch[feather]
    levels=levels[:h,:w]
    # Join isolated native fragments into short diagonal blade clusters.
    levels=np.median(np.stack([np.roll(np.roll(levels,dy,0),dx,1) for dy,dx in [(0,0),(1,0),(-1,0),(0,1),(0,-1)]]),axis=0).astype(np.uint8)
    # Keep 48x24 logical texels; adjacent 2x2 texels share a grass blade color.
    levels=np.repeat(np.repeat(np.median(levels.reshape(h//2,2,w//2,2),axis=(1,3)).astype(np.uint8),2,0),2,1)
    # Broad discrete sunlight offset: only -7/0/+7 sRGB, no blurred camouflage.
    yy,xx=np.mgrid[:h,:w]
    large=np.sin(xx/113+np.sin(yy/137))+.55*np.sin(yy/101+xx/173)
    delta=np.where(large>.55,4,np.where(large<-.55,-4,0))
    ground=np.clip(palette[levels]+delta[:,:,None],0,255).astype(np.uint8)
    Image.fromarray(ground).save(OUT/'FieldGrassOnly.png')
    worldx=xx/48-w/96;worldz=h/48-yy/24
    center=2.8+.30*np.sin(worldx*.73)+.16*np.sin(worldx*1.91)
    width=.67+.10*np.sin(worldx*1.38)+.04*np.sin(worldx*4.2)
    edge=(rng.random((h,w))-.5)*.20
    road=np.abs(worldz-center)<width+edge
    noise=rng.integers(0,256,(h,w),dtype=np.uint8).astype(int)
    grain=(noise+np.roll(noise,1,1)+np.roll(noise,2,1)+np.roll(noise,3,1))//4
    grain=np.median(np.stack([grain,np.roll(grain,1,0),np.roll(grain,-1,0)]),axis=0)
    dirt=np.array([[85,70,46],[96,78,51],[105,85,55],[117,97,64]],dtype=np.uint8)
    dirt_levels=np.digitize(grain,[108,130,151])
    dirt_levels=np.repeat(np.repeat(np.median(dirt_levels.reshape(h//2,2,w//2,2),axis=(1,3)).astype(np.uint8),2,0),2,1)
    ground[road]=dirt[dirt_levels][road]
    Image.fromarray(ground).save(OUT/'FieldGround.png')
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
    tree=np.asarray(Image.open(ART/'Decor/tree_01.png').convert('RGBA')).copy()
    rgb=tree[:,:,:3].astype(float)
    leaf=(rgb[:,:,1]>rgb[:,:,0]*.92)&(rgb[:,:,1]>rgb[:,:,2]*1.02)&(tree[:,:,3]>127)
    luma=rgb[:,:,0]*.2126+rgb[:,:,1]*.7152+rgb[:,:,2]*.0722
    cuts=np.percentile(luma[leaf],[23,60,88])
    tree_palette=np.array([[73,116,107],[92,141,115],[143,177,103],[179,202,123]],dtype=np.uint8)
    tree[:,:,:3][leaf]=tree_palette[np.digitize(luma,cuts,right=True)][leaf]
    Image.fromarray(tree).save(OUT/'FieldTree.png')
    # Preserve the scalloped native leaf clusters at 1:1 texels. No oval resize.
    yy,xx=np.where(leaf)
    bush=tree[yy.min():yy.max()+1,xx.min():xx.max()+1].copy()
    bush[:,:,3]=np.where(leaf[yy.min():yy.max()+1,xx.min():xx.max()+1],255,0)
    native=Image.fromarray(bush)
    bush=native.resize((49,round(native.height*49/native.width)),Image.Resampling.NEAREST)
    bush=Image.fromarray(np.pad(np.asarray(bush),((1,1),(1,1),(0,0))))
    # Keep the connected crown, removing isolated pixels that lower the foot pivot.
    pixels=np.asarray(bush).copy();mask_pixels=pixels[:,:,3]>127
    visited=np.zeros_like(mask_pixels);largest=[]
    for by,bx in zip(*np.where(mask_pixels)):
        if visited[by,bx]:continue
        stack=[(by,bx)];visited[by,bx]=True;component=[]
        while stack:
            cy,cx=stack.pop();component.append((cy,cx))
            for ny,nx in [(cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)]:
                if 0<=ny<mask_pixels.shape[0] and 0<=nx<mask_pixels.shape[1] and mask_pixels[ny,nx] and not visited[ny,nx]:
                    visited[ny,nx]=True;stack.append((ny,nx))
        if len(component)>len(largest):largest=component
    pixels[:,:,3]=0
    for by,bx in largest:pixels[by,bx,3]=255
    bush=Image.fromarray(pixels)
    mask=bush.getchannel('A');rim=mask.filter(ImageFilter.MaxFilter(3))
    result=Image.new('RGBA',bush.size,(52,20,14,0));result.putalpha(rim);result.alpha_composite(bush)
    # Crop away transparent bottom rows so visible leaves meet the ground pivot.
    result=result.crop(result.getbbox());result.save(OUT/'FieldBush.png')
    (OUT/'source.json').write_text(json.dumps({'seed':181104,'externalReferenceInputs':[], 'derivedFrom':[str(p.relative_to(ROOT)) for p in grass_paths]+[str((ART/'Decor/tree_01.png').relative_to(ROOT))], 'density':[48,24], 'worldPeriod':[w/48,h/24], 'grassPalette':palette.tolist(),'largeSunlightDeltaSRGB':[-4,4],'treePalette':tree_palette.tolist(),'method':'native grass fragment overlap with stochastic texel feather, broad low contrast discrete sunlight; native leaf cluster crop without oval scaling'},indent=2))


if __name__=='__main__':
    main()
