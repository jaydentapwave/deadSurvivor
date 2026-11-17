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
        for box in panels: ui.place(small,big,pan,12,box)
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
    # 셀별 높이가 다른 원본을 같은 48px 캔버스에 배치. sheet 높이 144 = 전부 정확히 3배.
    for n,old in [('button','button'),('panel','panel'),('bag_cell','cell')]:
        for tag,path in [('sample',SRC/(old+'.png')),('final',DST/(n+'.png')),('before',WORK/(n+'-before.png'))]:
            im=Image.open(path).convert('RGBA'); canvas=Image.new('RGBA',(48,48))
            canvas.alpha_composite(im,((48-im.width)//2,(48-im.height)//2));canvas.save(WORK/f'{n}-{tag}-canvas.png')
    pics=[str(WORK/f'{n}-{tag}-canvas.png') for n in ['button','panel','bag_cell'] for tag in ['sample','final','before']]
    subprocess.run([PY,str(T/'sheet.py'),'grid',str(ROOT/'docs/art/sheets/ui-frames.png'),*pics,'--size','144','--numbered','--unverified','양산 — 사용자 판정 / 3칸씩: 채택 견본 | 채택본 후보 | 수정 전(미채택), 게임 크기 3배'],check=True)

if __name__=='__main__':
    frames();compose()
