"""s3 채택 UI의 직접 픽셀 손질·9-slice 합성. Unity 연결은 하지 않는다."""
from pathlib import Path
import importlib.util
import json
import re
import subprocess
import sys
from PIL import Image, ImageDraw, ImageFont
ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT/'docs/art/samples/ui/s3'
DST = ROOT/'Assets/Game/03.Resources/Art/AIGenerated/UI'
WORK = ROOT/'docs/art/sheets/ui-frames-work'
T = ROOT/'.ai/2d-art/tools'
PY = sys.executable

def rgba(s): return tuple(bytes.fromhex(s.removeprefix('#'))) + (255,)
O,D,L,M,S,G = map(rgba,['#34140e','#2f1108','#5c493c','#774c3b','#c2864d','#f8c838'])

def frames():
    spec=(ROOT/'docs/art/아트-기준서.md').read_text()
    palette=json.loads(re.search(r'```json\n(.*?)```',spec.split('### 3.5')[1],re.S)[1])
    (WORK/'palette.json').write_text(json.dumps(palette))
    for name,old in [('button','button'),('panel','panel'),('bag_cell','cell')]:
        im=Image.open(SRC/(old+'.png')).convert('RGBA')
        # 수정 전 견본은 버린 신규 생성 후보로 둔갑시키지 않는다.
        im.save(WORK/(name+'-before.png'))
        w,h=im.size
        if name=='button':
            # s3의 금속 모서리·바늘땀을 보존, 흰 글자용 가운데만 낮춘다.
            ImageDraw.Draw(im).rectangle((4,4,27,11),fill=L)
        elif name=='panel':
            p=im.load()
            for y in range(h):
                for x in range(w):
                    r=min(x,y,w-1-x,h-1-y)
                    if r==0: p[x,y]=O
                    elif r>=6: p[x,y]=D  # 글자에 닿던 안쪽 모서리 눈금 삭제
                    elif 8<=x<w-8 and (y<6 or y>=h-6):
                        p[x,y]=[O,L,S if (x-8)%4<2 else L,L,L,rgba('#af642e')][r]
                    elif 8<=y<h-8 and (x<6 or x>=w-6):
                        p[x,y]=[O,L,S if (y-8)%4<2 else L,L,L,rgba('#af642e')][r]
            # 모서리 조각의 끝과 반복 변의 시작에 같은 4픽셀 위상을 적용.
            # 12 border -> centre 24 = 6 periods. 장식은 실제 6px 안에만 둔다.
        else:
            p=im.load()
            for y in range(h):
                for x in range(w):
                    r=min(x,y,w-1-x,h-1-y)
                    if r==0: p[x,y]=O
                    elif r==1: p[x,y]=rgba('#c9a264')
                    elif x==2 or y==2: p[x,y]=rgba('#d3ae90')
                    elif x==13 or y==13: p[x,y]=rgba('#e4b87d')
            # 원래 한쪽 아래에 튀던 점과 비대칭 모서리는 테두리로 덮인다.
        logical=WORK/(name+'-logical.png'); im.save(logical)
        # 격자 추정기는 4px 미만 블록을 찾지 못한다. 실제 1px 저작본의 정수 16배를 검사한다.
        logical=WORK/(name+'-blocks.png'); im.resize((w*16,h*16),Image.Resampling.NEAREST).save(logical)
        subprocess.run([PY,str(T/'pixelize.py'),'grid',str(logical),'--size',f'{w}x{h}'],check=True)
        subprocess.run([PY,str(T/'pixelize.py'),'one',str(logical),'--out',str(DST/(name+'.png')),'--size',f'{w}x{h}','--palette',str(WORK/'palette.json')],check=True)

def panel_nine(src, ui, w, h):
    """12px 9-slice 바탕 + 목적 크기에 맞춘 바늘땀 변 조각.

    실제 장식 모서리 8x8은 보존한다. 12px 모서리의 나머지 4px는
    변 조각의 연장부이므로 원본 위상을 고정하지 않고 목적 좌표로 이어 붙인다.
    2px 땀을 끝에서 자르지 않는다. 남는 0..3px는 모서리 앞 가죽 여백이다.
    Unity 기본 Tiled 하나로는 이 배치가 재현되지 않는다.
    """
    out=ui.nine(src,12,w,h)
    for y in (2,h-3):
        for x in range(8,w-8):out.putpixel((x,y),L)
        for x in range(8,w-9,4):
            for dx in (0,1):out.putpixel((x+dx,y),S)
    for x in (2,w-3):
        for y in range(8,h-8):out.putpixel((x,y),L)
        for y in range(8,h-9,4):
            for dy in (0,1):out.putpixel((x,y+dy),S)
    return out


def verify_panel_rails(im):
    # 출력 픽셀에서 땀 길이와 사이 가죽 길이를 센다. 양쪽 12px 이음도 포함한다.
    from itertools import groupby
    w,h=im.size
    rails=[[im.getpixel((x,y))==S for x in range(8,w-8)] for y in (2,h-3)]
    rails += [[im.getpixel((x,y))==S for y in range(8,h-8)] for x in (2,w-3)]
    for rail in rails:
        runs=[(value,len(list(group))) for value,group in groupby(rail)]
        assert all(length==2 for value,length in runs if value),runs
        assert all(length==2 for value,length in runs[1:-1] if not value),runs
        assert runs[0]==(True,2),runs
        if not runs[-1][0]:assert runs[-1][1]<=3,runs
    # 8x8 장식 모서리 밖의 실제 땀은 네 변 모두 길이2, 간격2다.
    return True


def place_panel(small,big,src,ui,box):
    x0,y0,x1,y1=box
    piece=panel_nine(src,ui,x1-x0,y1-y0)
    verify_panel_rails(piece)
    print(f'PASS destination stitch rails {piece.width}x{piece.height}: dash=2 gap=2; all four corner seams included')
    small.alpha_composite(piece,(x0,y0))
    big.alpha_composite(piece.resize((piece.width*3,piece.height*3),Image.Resampling.NEAREST),(x0*3,y0*3))

def compose():
    module_spec=importlib.util.spec_from_file_location('sample_ui',ROOT/'docs/art/samples/ui/build_ui.py')
    ui=importlib.util.module_from_spec(module_spec); module_spec.loader.exec_module(ui)
    battle=Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/.director/evidence/20261117-screen-fixes/2/1/art-flows/10-MON-1920x1080.png')
    camp=Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/.director/evidence/20261117-screen-fixes/2/1/playable-main/03-G3-1920x1080.png')
    btn,pan,cell=[Image.open(DST/(n+'.png')).convert('RGBA') for n in ['button','panel','bag_cell']]
    font=str(ROOT/'Assets/Game/03.Resources/Font/NotoSansKR.ttf')
    f=ImageFont.truetype(font,28); fb=ImageFont.truetype(font,30)
    for kind,path in [('camp',camp),('battle',battle)]:
        cap=Image.open(path).convert('RGBA'); assert cap.size==(1920,1080)
        small=cap.resize((640,360),Image.Resampling.NEAREST); big=cap.copy()
        panels=ui.CAMP_PANELS if kind=='camp' else [(x0,310,x1,360) for x0,_,x1,_ in ui.BATTLE_PANELS]
        buttons=ui.CAMP_BUTTONS if kind=='camp' else ui.BATTLE_BUTTONS
        for box in panels: place_panel(small,big,pan,ui,box)
        if kind=='camp':
            for x0 in ui.BAG_X0:
                for j in range(4):
                    for i in range(4):
                        ui.place(small,big,cell,3,(x0+i*22+1,73+j*22,x0+i*22+17,89+j*22))
        for box,_ in buttons: ui.place(small,big,btn,4,box)
        for rect in (ui.CAMP_COPY if kind=='camp' else []):
            offset=0 if kind=='camp' else 12
            big.paste(cap.crop(rect),(rect[0],rect[1]+offset))
        dr=ImageDraw.Draw(big)
        if kind=='camp':
            dr.text((36,30),'웨이브 1 완료',font=ImageFont.truetype(font,40),fill='white')
            for k,(n,hp) in enumerate(ui.HEROES):dr.text((322+k*340,100),f'{n} HP {hp}',font=f,fill='white')
            dr.text((36,514),'보관함',font=f,fill='white')
            for x0,label in zip(ui.BAG_X0,ui.BAG_LABELS):dr.text(((x0+1)*3+6,225),label,font=ImageFont.truetype(font,24),fill=D)
        else:
            for idx,((x0,y0,x1,_),(n,_)) in enumerate(zip(panels,ui.HEROES)):
                hp=['273/400','99/110','119/140','89/120'][idx]
                dr.text((x0*3+24,y0*3+14),n,font=f,fill='white')
                dr.text((x1*3-24-dr.textlength(hp,font=f),y0*3+14),hp,font=f,fill='white')
                # 최신 MON 캡처에서 잰 HP 막대·칩 좌표. 옛 G2 좌표를 쓰지 않는다.
                bar=(x0*3+12,960,x1*3-12,970)
                chip=(x0*3+12,972,x0*3+90,996)
                big.paste(cap.crop(bar).resize(((x1-x0)*3-48,10),Image.Resampling.NEAREST),(x0*3+24,y0*3+60))
                big.paste(cap.crop(chip),(x0*3+24,y0*3+84))
        for box,label in buttons:ui.text_c(dr,box,label,fb)
        out=Image.new('RGBA',(1920,1128),rgba('#fff9ed'));out.alpha_composite(big,(0,48))
        ImageDraw.Draw(out).text((12,6),f'합성 — 게임 화면 아님 · UI 양산 · {kind} · 게임 크기 ×3 · 9-slice 변 반복',font=f,fill=O)
        out.save(ROOT/f'docs/art/sheets/ui-frames-{kind}.png')
    cap=Image.open(battle).convert('RGBA')
    context=cap.crop((30,100,798,388))
    # 게임 16px 칸을 3배로, 바닥의 서로 다른 노란 면 위에 놓는다.
    cell3=cell.resize((48,48),Image.Resampling.NEAREST)
    for x in range(36,720,84):
        context.alpha_composite(cell3,(x,72))
    preview=Image.new('RGBA',(768,336),rgba('#fff9ed'))
    preview.alpha_composite(context,(0,48))
    ImageDraw.Draw(preview).text((12,8),'합성 — 게임 화면 아님 · 노란 바닥 / 가방 칸 ×3',font=ImageFont.truetype(font,22),fill=O)
    preview.save(ROOT/'docs/art/sheets/ui-frames-yellow-ground.png')
    # 셀별 높이가 다른 원본을 같은 48px 캔버스에 배치. sheet 높이 144 = 전부 정확히 3배.
    for n,old in [('button','button'),('panel','panel'),('bag_cell','cell')]:
        for tag,path in [('sample',SRC/(old+'.png')),('final',DST/(n+'.png')),('before',WORK/(n+'-before.png'))]:
            im=Image.open(path).convert('RGBA'); canvas=Image.new('RGBA',(48,48))
            canvas.alpha_composite(im,((48-im.width)//2,(48-im.height)//2));canvas.save(WORK/f'{n}-{tag}-canvas.png')
    pics=[str(WORK/f'{n}-{tag}-canvas.png') for n in ['button','panel','bag_cell'] for tag in ['sample','final','before']]
    subprocess.run([PY,str(T/'sheet.py'),'grid',str(ROOT/'docs/art/sheets/ui-frames.png'),*pics,'--size','144','--numbered','--unverified','양산 — 사용자 판정 / 3칸씩: 채택 견본 | 채택본 후보 | 수정 전(미채택), 게임 크기 3배'],check=True)

def verify_exports():
    spec=importlib.util.spec_from_file_location('sample_ui_verify',ROOT/'docs/art/samples/ui/build_ui.py')
    ui=importlib.util.module_from_spec(spec);spec.loader.exec_module(ui)
    src=Image.open(DST/'panel.png').convert('RGBA')
    for kind,boxes in [('camp',ui.CAMP_PANELS),('battle',[(a,310,c,360) for a,b,c,d in ui.BATTLE_PANELS])]:
        final=Image.open(ROOT/f'docs/art/sheets/ui-frames-{kind}.png').convert('RGBA')
        for x0,y0,x1,y1 in boxes:
            w,h=x1-x0,y1-y0
            exported=final.crop((x0*3,y0*3+48,x1*3,y1*3+48)).resize((w,h),Image.Resampling.NEAREST)
            verify_panel_rails(exported)
            fixed=panel_nine(src,ui,w,h);old=ui.nine(src,12,w,h)
            for ax,ay in [(0,0),(w-8,0),(0,h-8),(w-8,h-8)]:
                assert fixed.crop((ax,ay,ax+8,ay+8)).tobytes()==old.crop((ax,ay,ax+8,ay+8)).tobytes()
            try:verify_panel_rails(old)
            except AssertionError:negative='old layout fails as expected'
            else:
                assert (w-24)%4==0 and (h-24)%4==0
                negative='old layout already periodic at this size'
            print(f'PASS exported {kind} {w}x{h}: full rails + 8x8 corners; {negative}')

if __name__=='__main__':
    frames();compose();verify_exports()
