"""Address the six blind-read failures using original integer-pixel parts.
No generation or resampling; the reviewed PASS clip bytes remain untouched.
"""
from pathlib import Path
import sys,json
import numpy as np
from PIL import Image,ImageDraw
P=Path('output/hd2d-dq/codex-sprites')
sys.path.insert(0,str(P))
from produce import REF,patch
OUT=P/'review-r1'

def read(clip,i):
    return Image.open(OUT/'before'/clip/f'frame_{i:02}.png').convert('RGBA')

def leg_piece():
    a=np.zeros((72,72,4),dtype=np.uint8);src=np.array(REF['right'])
    # A complete six-column shin. Dark material outlines must not be mistaken
    # for cape-red pixels: removing those broke the first repair's knee.
    a[53:59,32:38]=src[53:59,32:38]
    a[53,37]=src[53,36]  # one adjacent hand pixel is not part of the shin
    a[59:64,31:42]=src[59:64,31:42]
    return Image.fromarray(a).crop((31,53,42,64))

def dark_leg(piece):
    a=np.array(piece);original=a.copy()
    for old,new in [((74,86,131),(51,59,99)),((99,112,156),(74,86,131)),((172,176,198),(99,112,156)),((251,251,251),(172,176,198)),((82,44,47),(34,10,14)),((146,60,36),(82,44,47)),((196,109,81),(146,60,36))]:
        a[:,:,:3][np.all(original[:,:,:3]==old,axis=2)]=new
    return Image.fromarray(a)

def put_leg(f,piece,dx,dy):
    # The retained full-length source shin bends at the hip, with a connected
    # upper leg, rather than scaling/squashing the whole lower body.
    d=ImageDraw.Draw(f)
    d.polygon([(33,50),(37,50),(38+dx,55+dy),(33+dx,55+dy)],fill=(14,10,25,255))
    d.polygon([(34,51),(36,51),(37+dx,55+dy),(34+dx,55+dy)],fill=(51,59,99,255))
    f.alpha_composite(piece,(31+dx,53+dy))

def side_walk():
    leg=leg_piece();far=dark_leg(leg)
    # Strong contact poses and clearly lifted heels, never floating gold bars.
    phases=[(4,0,-5,-4),(0,0,-4,-5),(-5,-4,4,0),(-4,-5,0,0)]
    for i,(nx,ny,fx,fy) in enumerate(phases):
        f=REF['right'].copy();f.paste((0,0,0,0),(31,52,44,64))
        put_leg(f,far,fx,fy);put_leg(f,leg,nx,ny)
        # The source equipment/hand stays in front of the hips, not on shins.
        patch(f,REF['right'],(35,47,53,53))
        patch(f,REF['right'],(37,50,43,56))
        f.save(P/'C/walk_right'/f'frame_{i:02}.png')
    # A uses its generated contact poses. Only the broken passing legs are
    # replaced by connected reference shin/boot patches; no B frame is copied.
    for i,(planted,raised) in [(1,(leg,far)),(3,(far,leg))]:
        f=read('A/walk_right/retry',i)
        f.paste((0,0,0,0),(26,51,44,64))
        # Retain the reference's hanging cape behind the legs.
        f.alpha_composite(REF['right'].crop((22,51,31,61)),(22,51))
        put_leg(f,raised,-4,-5);put_leg(f,planted,0,0)
        patch(f,REF['right'],(35,47,53,53))
        patch(f,REF['right'],(37,50,43,56))
        f.save(P/'A/walk_right/retry'/f'frame_{i:02}.png')

def front_walk():
    base=REF['down'];left=base.crop((29,53,37,64));right=base.crop((39,53,46,64))
    for i,(ly,ry) in enumerate([(0,-5),(0,-3),(-5,0),(-3,0)]):
        f=base.copy();f.paste((0,0,0,0),(29,53,37,64));f.paste((0,0,0,0),(39,53,46,64))
        f.alpha_composite(left,(29,53+ly));f.alpha_composite(right,(39,53+ry))
        # Tunic remains over the moving upper thighs. Soles now lift 3/5px.
        patch(f,base,(29,47,46,53))
        f.save(P/'C/walk_down'/f'frame_{i:02}.png')
    # Restore the WHOLE sword/hand envelope, not a narrow blade-only window.
    # The former x23..29 patch left a generated second blade and hilt behind.
    for i in range(4):
        f=read('B/walk_down',i)
        # Clear the full left envelope, including isolated generated fragments
        # left of x21. A narrow source patch could not erase those fragments.
        patch(f,base,(0,43,31,64))
        patch(f,base,(0,43,34,53))
        f.save(P/'B/walk_down'/f'frame_{i:02}.png')

def attacks():
    base=REF['right'];sword=base.crop((36,47,53,52))
    # A's upper-row head reset erased the wind-up. Put the source weapon fully
    # outside the head silhouette and attach the raised arm below the face.
    f=read('A/attack_right',0);patch(f,base,(0,0,72,31));patch(f,base,(40,31,51,35))
    d=ImageDraw.Draw(f)
    d.line([(35,35),(44,34),(53,32)],fill=(14,10,25,255),width=5)
    d.line([(35,35),(44,34),(53,32)],fill=(99,112,156,255),width=3)
    f.alpha_composite(sword.transpose(Image.Transpose.ROTATE_90),(51,16))
    d=ImageDraw.Draw(f);d.rectangle((52,31,54,33),fill=(252,182,96,255))
    f.save(P/'A/attack_right/frame_00.png')
    # C's shield was painted over by the thick arm. Layer source shield back
    # over the arm, with the blade entirely outside it.
    poses=[(Image.Transpose.ROTATE_90,(50,31),(52,47)),(None,(50,42),(50,44)),(Image.Transpose.ROTATE_270,(50,46),(52,46)),(None,(36,47),(37,50))]
    for i,(t,xy,hand) in enumerate(poses):
        f=base.copy();f.paste((0,0,0,0),(36,47,53,52))
        d=ImageDraw.Draw(f);d.rectangle((36,47,40,53),fill=(51,59,99,255))
        d.line([(35,40),(37,47),hand],fill=(14,10,25,255),width=5)
        d.line([(35,40),(37,47),hand],fill=(99,112,156,255),width=3)
        # Exact full shield patch, including rim and boss, never just red fill.
        patch(f,base,(40,33,49,47))
        f.alpha_composite(sword.transpose(t) if t is not None else sword,xy)
        d=ImageDraw.Draw(f);d.rectangle((hand[0]-1,hand[1]-1,hand[0]+1,hand[1]+1),fill=(252,182,96,255))
        f.save(P/'C/attack_right'/f'frame_{i:02}.png')

if __name__=='__main__':
    side_walk();front_walk();attacks()
    print('repaired 6 failed clips; 3 PASS clips untouched; no generation')
