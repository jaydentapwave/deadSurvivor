"""Reproducible local lookdev textures; no generation service and no source edits.
Run with /usr/bin/python3, or the Round2 Build Assets Unity menu.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[6]
ART = ROOT / 'Assets/Game/03.Resources/Art'
OUT = ART / 'HD2D/Round2'


def diamond_square(path):
    image = np.array(Image.open(path).convert('RGBA'))
    y, x = np.mgrid[:24, :48]
    u, v = (x + .5) / 48, (y + .5) / 24
    sx = np.clip(((u-v+1)*.5*image.shape[1]).astype(int), 0, image.shape[1]-1)
    sy = np.clip(((u+v)*.5*image.shape[0]).astype(int), 0, image.shape[0]-1)
    result = image[sy, sx, :3].copy()
    result[image[sy, sx, 3] < 128] = [161, 152, 93]
    return result


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(18112026)
    grass_files = sorted((ART/'AIGenerated/Ground/grass').glob('*.png'))
    grass = np.stack([diamond_square(p) for p in grass_files])
    width, height = 2048, 1024
    y, x = np.mgrid[:height, :width]
    # Long, irregular fields, not a grid of four intact tile squares.
    coarse = rng.random((33, 65))
    smooth = np.array(Image.fromarray((coarse*255).astype('uint8')).resize((width,height),Image.Resampling.BICUBIC))/255
    fine = rng.random((height,width))
    palette = np.array([[194,173,95],[161,152,93],[214,192,116],[179,169,84],[145,140,75],[112,104,62]])
    field = np.array([[174,166,87],[178,170,90],[182,174,93]])[np.select([smooth<.35,smooth<.70],[0,1],default=2)].copy()
    # Sparse 1-3 texel fragments sampled from all four adopted grass pictures.
    for _ in range(65000):
        px, py = int(rng.integers(width)), int(rng.integers(height))
        tile = grass[int(rng.integers(4))]
        sx, sy = int(rng.integers(46)), int(rng.integers(22))
        fragment = tile[sy:sy+2,sx:sx+2]
        # Adopt source light/dark relationships, quantized to quiet grass colors.
        light = np.mean(fragment,axis=2)
        fragment = np.array([[145,140,75],[179,169,84],[194,173,95]])[np.select([light<125,light<160],[0,1],default=2)]
        for dy in range(2):
            for dx in range(2):
                if rng.random()<.57:
                    field[(py+dy)%height,(px+dx)%width] = fragment[dy,dx]
    # No rectangular patch borders. One world-space road with ragged pixel edges.
    world_z = (y+.5)/24 - height/48
    edge_noise = np.array(Image.fromarray(rng.integers(0,255,(1,129),dtype='uint8')).resize((width,1),Image.Resampling.BICUBIC))[0]/255
    edge = .70 + (edge_noise[x]-.5)*.30 + (fine-.5)*.10
    distance = np.abs((height/48 - y/24) - 2.8)
    path_files=sorted((ART/'AIGenerated/Ground/path').glob('*.png'))
    path_pixels=np.concatenate([np.array(Image.open(p).convert('RGBA')).reshape(-1,4) for p in path_files])
    path_pixels=path_pixels[(path_pixels[:,3]>200)&(path_pixels[:,0]>170)&(path_pixels[:,1]>120)&(path_pixels[:,1]<205)&(path_pixels[:,2]<145),:3]
    if len(path_pixels)==0:
        raise RuntimeError('No opaque dirt colors in source pictures')
    road_colors=np.array([[228,184,125],[207,163,108],[186,145,97]])
    source_choices=path_pixels[rng.integers(len(path_pixels),size=(height,width))]
    path_mask=distance<edge
    # Blend edge by coverage at individual texels, never a bilinear gradient.
    fringe=(distance<edge+.15)&(fine<(edge+.15-distance)/.15)
    field[path_mask]=road_colors[rng.choice(3,size=path_mask.sum(),p=[.25,.65,.10])]
    field[fringe&~path_mask]=source_choices[fringe&~path_mask]
    # Explicitly close both wrap seams; the repetition period is >42 m.
    field[-1]=field[0]
    field[:,-1]=field[:,0]
    Image.fromarray(field.astype('uint8')).save(OUT/'F2Ground.png')
    noise=rng.random((128,128))
    clusters=np.array(Image.fromarray(rng.integers(0,255,(32,32),dtype='uint8')).resize((128,128),Image.Resampling.NEAREST))/255
    values=np.select([noise*.5+clusters*.5<.30,noise*.5+clusters*.5<.70],[0,127],default=255).astype('uint8')
    values[-1]=values[0];values[:,-1]=values[:,0]
    Image.fromarray(values).convert('RGB').save(OUT/'F2Pattern.png')
    board=((np.indices((48,48)).sum(axis=0)%2)*255).astype('uint8')
    Image.fromarray(board).convert('RGB').save(OUT/'F2Density.png')
    (OUT/'texture-source.json').write_text(json.dumps({'seed':18112026,'grassSources':[str(p.relative_to(ROOT)) for p in grass_files],'pathSources':[str(p.relative_to(ROOT)) for p in path_files],'floorTexelsPerUnit':[48,24],'floorWorldPeriod':[width/48,height/24],'method':'stochastic source fragments, irregular low-frequency fields, per-texel road fringe; periodic borders; no whole-tile grid'},ensure_ascii=False,indent=2))


if __name__ == '__main__':
    main()
