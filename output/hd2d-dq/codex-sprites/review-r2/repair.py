"""C2: explicit depth ordering and a locally drawn armored forearm.
Only the two failed method-C clips are touched. Original palette, integer cells.
"""
from pathlib import Path
import sys,json
import numpy as np
from PIL import Image,ImageDraw
P=Path('output/hd2d-dq/codex-sprites')
sys.path.insert(0,str(P))
from produce import REF,patch
sys.path.insert(0,str(P/'review-r1'))
from repair import leg_piece,dark_leg,put_leg
OUT=P/'review-r2'
INK=(14,10,25,255)
METAL=(99,112,156,255)
LIGHT=(172,176,198,255)
GOLD=(252,182,96,255)

def restore_hand(f):
    # The previous whole-hand rectangle also contained the original bright
    # shin. Reapplying it froze that shin in front in every frame.
    # Restore palm, red glove and outline together, not floating gold fingers.
    # The row55 blue pixel at x37 is a shin pixel, explicitly excluded.
    src=np.array(REF['right']);out=np.array(f)
    for y in range(50,56):
        for x in range(37,43):
            if (y<55 and x<42) or (y==55 and 38<=x<41):out[y,x]=src[y,x]
    return Image.fromarray(out)

def walk():
    near=leg_piece();far=dark_leg(near)
    # A separate opposite-leg surface, not another identical copy: preserve
    # the near shin's bright edge and keep the far shin in two dark armor steps.
    # This makes the reversed physical overlap visible at 72px as well.
    aa=np.array(near)
    for y in range(1,5):
        if aa[y,5,3]:aa[y,5]=(251,251,251,255)
    near=Image.fromarray(aa)
    aa=np.array(far);original=aa.copy()
    for old,new in [((74,86,131),(51,59,99)),((99,112,156),(51,59,99)),((172,176,198),(74,86,131)),((251,251,251),(99,112,156))]:
        aa[:,:,:3][np.all(original[:,:,:3]==old,axis=2)]=new
    far=Image.fromarray(aa)
    poses=[(4,0,-5,-4),(0,0,-4,-5),(-5,-4,4,0),(-4,-5,0,0)]
    order=[]
    for i,(nx,ny,fx,fy) in enumerate(poses):
        f=REF['right'].copy();f.paste((0,0,0,0),(31,52,44,64))
        # Explicitly reverse depth AND highlight identity for opposite contact.
        # Frames 02/03 must not have the bright near shin restored over far.
        layers=[('far',far,fx,fy),('near',near,nx,ny)] if i<2 else [('near',near,nx,ny),('far',far,fx,fy)]
        for name,piece,dx,dy in layers:put_leg(f,piece,dx,dy)
        # Keep only waist and sword above the moving thighs. No source shin
        # rectangle is allowed here; doing so copied the same front leg back.
        patch(f,REF['right'],(35,47,53,51))
        f=restore_hand(f)
        # The bent rear boot is connected by the complete original shin. No
        # separate brown wedge is pasted or restored from the old silhouette.
        f.save(P/'C/walk_right'/f'frame_{i:02}.png')
        order.append({'frame':i,'draw_order':[s[0] for s in layers],'leading_leg':'near' if i<2 else 'far','near_shift':[nx,ny],'far_shift':[fx,fy]})
    (OUT/'leg-order.json').write_text(json.dumps(order,indent=2))

def armored_arm(f,points):
    # Five-pixel silhouette, three-pixel metal face. Keep the bright face
    # visible by routing forearm outside the fixed shield rather than painting
    # one thin line under its lower edge.
    d=ImageDraw.Draw(f);d.line(points,fill=INK,width=5,joint='curve')
    d.line(points,fill=METAL,width=3,joint='curve')
    for x,y in points[1:]:d.rectangle((x-1,y-1,x+1,y+1),fill=METAL)

def attack():
    base=REF['right'];sword=base.crop((36,47,53,52));records=[]
    poses=[(Image.Transpose.ROTATE_90,(51,7),[(35,34),(47,35),(53,24)],(53,24)),
           (None,(51,37),[(35,40),(39,49),(53,39)],(53,39)),
           (Image.Transpose.ROTATE_270,(50,47),[(35,40),(39,50),(52,49)],(52,49))]
    for i,(t,xy,points,hand) in enumerate(poses):
        f=base.copy();f.paste((0,0,0,0),(36,47,53,52))
        d=ImageDraw.Draw(f);d.rectangle((36,47,40,53),fill=(51,59,99,255))
        armored_arm(f,points)
        patch(f,base,(40,33,49,47))
        # All visible forearm end points lie beyond the shield's right edge.
        # Source shield is kept byte-for-byte; visible metal cannot cover it.
        f.alpha_composite(sword.transpose(t) if t is not None else sword,xy)
        d=ImageDraw.Draw(f);x,y=hand
        d.rectangle((x-1,y-1,x+1,y+1),fill=GOLD)
        # Metal face highlight on exposed forearm (outside shield at x>=50).
        if i==0:d.line([(51,29),(50,32)],fill=LIGHT,width=1)
        elif i==1:d.line([(50,43),(52,40)],fill=LIGHT,width=1)
        else:d.line([(45,49),(50,49)],fill=LIGHT,width=1)
        # Exact reference head layers in front of the upper arm, keeping eyes.
        f.alpha_composite(base.crop((0,0,72,31)),(0,0))
        f.save(P/'C/attack_right'/f'frame_{i:02}.png')
        records.append({'frame':i,'sword_box':[xy[0],xy[1],xy[0]+(5 if t else 17),xy[1]+(17 if t else 5)],'forearm_points':points,'hand':hand,'silhouette_width':5,'metal_face_width':3})
    # Recovery is the exact reference, so no leftover dark bar under the hilt.
    base.save(P/'C/attack_right/frame_03.png')
    (OUT/'arm-geometry.json').write_text(json.dumps(records,indent=2))

if __name__=='__main__':
    walk();attack();print('C2 two clips repaired; seven PASS clips untouched')
