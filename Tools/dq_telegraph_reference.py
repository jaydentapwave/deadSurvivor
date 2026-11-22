"""Canonical DQ-r2 raster reference v1. Native tiles -> floor capsule -> RGBA SHA.
Run with /usr/bin/python3 -B. Does not read or execute the C# implementation.
"""
import hashlib
import json
import math
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
TILES=ROOT/'Assets/Game/03.Resources/Art/HD2D/DQAppliedCharacters/BoarTelegraphR2'
OUTPUT=ROOT/'Assets/Game/Tests/EditMode/DQTelegraphReference.json'


def assemble(stage):
    image=Image.new('RGBA',(34,209))
    body=Image.open(TILES/('body_%02d.png'%stage)).convert('RGBA')
    for y in range(17,192,32): image.paste(body,(0,y))
    image.paste((0,0,0,0),(0,0,34,62));image.paste(Image.open(TILES/('tip_%02d.png'%stage)).convert('RGBA'),(0,2))
    image.paste((0,0,0,0),(0,192,34,209));image.paste(Image.open(TILES/('start_%02d.png'%stage)).convert('RGBA'),(0,192))
    if stage:
        front=json.loads((TILES/'assembly.json').read_text())['stage_fill_front_y'][stage]
        image.paste(assemble(0).crop((0,0,34,front)),(0,0))
    return image


def raster(source,length,radius,angle):
    length*=48;radius*=48
    cosine=math.cos(math.radians(angle));sine=math.sin(math.radians(angle))
    ex,ey=cosine*length,sine*length*.5
    mx,my=math.ceil(radius)+2,math.ceil(radius*.5)+2
    w,h=math.ceil(abs(ex))+mx*2,math.ceil(abs(ey))+my*2
    ox,oy=mx-min(0,ex),my-min(0,ey)
    pixels=source.load(); result=bytearray(w*h*4)
    # Iterate Unity bottom-up pixels directly. The native sheet uses top-down rows.
    for y in range(h):
        for x in range(w):
            dx,dy=x+.5-ox,(y+.5-oy)*2
            along=dx*cosine+dy*sine;across=-dx*sine+dy*cosine
            distance=along-max(0,min(length,along))
            if distance*distance+across*across>radius*radius:continue
            sx=math.floor(17+across/radius*15)
            sy=math.floor(192-along/radius*15 if along<0 else 17-(along-length)/radius*15 if along>length else 192-along/length*175)
            if not(0<=sx<34 and 0<=sy<209):continue
            color=pixels[sx,sy]
            if color[3]>=128:result[(y*w+x)*4:(y*w+x+1)*4]=bytes((*color[:3],255))
    return w,h,hashlib.sha256(result).hexdigest()


def main():
    samples=[]
    for length,radius in [(3.6386,.3164),(2.25,.4)]:
        for angle in [i*11.25 for i in range(32)]+[5.625,39.7,181.3]:
            for stage in range(4):
                w,h,sha=raster(assemble(stage),length,radius,angle)
                samples.append(dict(length=length,radius=radius,angle=angle,stage=stage,width=w,height=h,sha256=sha))
    inputs=[Path(__file__).resolve()]+sorted(TILES.glob('*.png'))+[TILES/'assembly.json']
    OUTPUT.write_text(json.dumps(dict(version='DQ-r2-raster-v1',sources=[dict(path=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in inputs],samples=samples),indent=2)+'\n')
    print(len(samples),'canonical exact RGBA samples',OUTPUT)

if __name__=='__main__': main()
