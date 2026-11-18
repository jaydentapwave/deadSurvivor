#!/usr/bin/env python3
"""Read baked view data only; draw the spatial contract and actual counts."""
from pathlib import Path
import json
from collections import Counter
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/Game/03.Resources/Resources/Data/Field'
def main():
    data=json.loads((ART/'field_layout.json').read_text())
    im=Image.new('RGB',(1100,1100),'#303833');d=ImageDraw.Draw(im)
    def point(x,y):return (550+x*13,550-y*13)
    def poly(points,fill=None,outline='white',width=1):d.polygon([point(*p) for p in points],fill=fill,outline=outline,width=width)
    poly([(0,36),(36,0),(0,-36),(-36,0)],fill='#555f42',outline='#859077')
    poly([(0,32),(32,0),(0,-32),(-32,0)],outline='white')
    poly([(-10,22),(10,22),(20.75,11.25),(20.75,-11.25),(10,-22),(-10,-22),(-20.75,-11.25),(-20.75,11.25)],outline='#8bbce0',width=2)
    p=[point((c['x']-c['y'])/2,(c['x']+c['y'])/2) for c in json.loads((ART/'ground_path.json').read_text())['cells']]
    d.line(p,fill='#cfa677',width=12)
    for w in data['water']:
        if w['name']=='pond':
            d.ellipse([point(-7.6,9),point(-2,4)],fill='#345548')
        else:
            d.line([point(p['x'],p['y']) for p in w['points']],fill='#519eac',width=10)
    colors={'tree':'#8cc692','rim':'#6aac79','bush':'#52a879','boulder':'#d8c894','stone':'#bab894','flowers':'#edcb63','shore':'#aaaa99','tent':'#ead3ad','bridge':'#e48762','fence':'#bd9065','sign':'#f1b17b','flag':'#cc6688'}
    for p in data['placements']:
        x,y=point(p['x'],p['y']);c=colors.get(p['group'],'#e2e2e2');r=4 if p['group']!='flowers' else 2
        d.ellipse((x-r,y-r,x+r,y+r),fill=c)
    d.rectangle([point(-10,11.25),point(10,-11.25)],outline='#ff8050',width=2)
    for i,(k,n) in enumerate(sorted(Counter(p['group'] for p in data['placements']).items())):
        d.text((12,12+i*20),f'{k}: {n}',fill=colors.get(k,'white'))
    out=ROOT/'output/hd2d-background';out.mkdir(parents=True,exist_ok=True);im.save(out/'layout-main.png')
if __name__=='__main__':main()
