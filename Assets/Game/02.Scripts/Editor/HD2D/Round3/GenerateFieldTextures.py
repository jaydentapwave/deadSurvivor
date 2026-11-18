"""Original field materials from seeded marks; external references are never loaded."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[6]
OUT = ROOT/'Assets/Game/03.Resources/Art/HD2D/Round3'
ART = ROOT/'Assets/Game/03.Resources/Art/AIGenerated/Decor'


def main():
    OUT.mkdir(exist_ok=True)
    rng = np.random.default_rng(181103)
    w,h = 2048,1024
    y,x = np.mgrid[:h,:w]
    noise = rng.integers(0,256,(h,w),dtype=np.uint8)
    smooth = np.asarray(Image.fromarray(noise).filter(ImageFilter.GaussianBlur(4.5))).astype(float)
    smooth = (smooth-smooth.mean())/smooth.std()
    ix = np.digitize(smooth,[-1.4,-.55,.55,1.4])
    # Short diagonal grass marks, all on the source texel lattice.
    marks = rng.random((h,w)) < .09
    marks |= np.roll(np.roll(marks,1,0),1,1) & (rng.random((h,w))<.65)
    ix = np.clip(ix + marks.astype(int),0,4)
    grass = np.array([[102,99,64],[132,128,82],[169,164,105],[198,192,123],[223,216,139]],dtype=np.uint8)
    ground = grass[ix]
    Image.fromarray(ground).save(OUT/'FieldGrassOnly.png')
    worldx = x/48-w/96
    worldz = h/48-y/24
    center = 2.8 + .24*np.sin(worldx*.73)+.12*np.sin(worldx*1.91)
    width = .67+.10*np.sin(worldx*1.38)+.04*np.sin(worldx*4.2)
    fringe = (rng.random((h,w))-.5)*.17
    road = np.abs(worldz-center)<width+fringe
    horizontal = np.asarray(Image.fromarray(rng.integers(0,256,(h,w),dtype=np.uint8)).filter(ImageFilter.GaussianBlur(.3)))
    horizontal = (horizontal.astype(int)+np.roll(horizontal,1,1)+np.roll(horizontal,2,1))//3
    dirt = np.array([[70,58,39],[91,74,48],[105,85,55],[126,103,68]],dtype=np.uint8)
    ground[road]=dirt[np.digitize(horizontal,[95,128,160])][road]
    ground[-1]=ground[0];ground[:,-1]=ground[:,0]
    Image.fromarray(ground).save(OUT/'FieldGround.png')
    # Unique pond reflection clusters, no foam perimeter.
    water = np.array([[30,48,42],[43,65,54],[57,85,69]],dtype=np.uint8)
    wave = rng.random((512,1024))
    ripple = (wave+np.roll(wave,1,1)+np.roll(wave,2,1)+np.roll(wave,3,1))/4
    Image.fromarray(water[np.digitize(ripple,[.40,.72])]).save(OUT/'FieldWater.png')
    pattern = rng.choice(np.array([0,127,255],dtype=np.uint8),(128,128),p=[.18,.70,.12])
    Image.fromarray(pattern).convert('RGB').save(OUT/'StonePattern.png')
    tree = np.asarray(Image.open(ART/'tree_01.png').convert('RGBA')).copy()
    rgb = tree[:,:,:3].astype(float)
    leaf = (rgb[:,:,1]>rgb[:,:,0]*.92)&(rgb[:,:,1]>rgb[:,:,2]*1.02)&(tree[:,:,3]>127)
    luma = rgb[:,:,0]*.2126+rgb[:,:,1]*.7152+rgb[:,:,2]*.0722
    cuts = np.percentile(luma[leaf],[23,60,88])
    palette = np.array([[28,39,56],[44,60,65],[68,84,65],[118,142,82]],dtype=np.uint8)
    tree[:,:,:3][leaf]=palette[np.digitize(luma,cuts,right=True)][leaf]
    Image.fromarray(tree).save(OUT/'FieldTree.png')
    # Derive a bush from our adopted leaf pixels only, not from a reference image.
    yy,xx=np.where(leaf)
    bush=tree[yy.min():yy.max()+1,xx.min():xx.max()+1].copy()
    bush[:,:,3]=np.where(leaf[yy.min():yy.max()+1,xx.min():xx.max()+1],255,0)
    bush=Image.fromarray(bush).resize((48,32),Image.Resampling.NEAREST)
    mask=bush.getchannel('A');rim=mask.filter(ImageFilter.MaxFilter(3))
    result=Image.new('RGBA',bush.size,(52,20,14,0));result.putalpha(rim);result.alpha_composite(bush)
    result.save(OUT/'FieldBush.png')
    (OUT/'source.json').write_text(json.dumps({'seed':181103,'externalReferenceInputs':[], 'derivedFrom':[str((ART/'tree_01.png').relative_to(ROOT))], 'density':[48,24], 'worldPeriod':[w/48,h/24], 'grassPalette':grass.tolist(),'method':'non-repeating quantized Gaussian patches plus diagonal texel strokes; stepped wandering dirt edge'},indent=2))


if __name__=='__main__':
    main()
