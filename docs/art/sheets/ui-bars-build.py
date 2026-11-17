"""채택 s3 UI 직접 픽셀 손질. 실행: studio-kit/bin/python 이 파일.
후보 4세트, 격자/팔레트/fringe, 시트 및 합성. Unity 연결 없음.
"""
from pathlib import Path
import json, re, subprocess, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont
ROOT=Path(__file__).resolve().parents[3]
UI=ROOT/'Assets/Game/03.Resources/Art/AIGenerated/UI'
W=ROOT/'docs/art/sheets/ui-bars-work'
T=ROOT/'.ai/2d-art/tools'
PY=sys.executable
S=ROOT/'docs/art/samples/ui/s3'
CAP=Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/.director/evidence/20261117-screen-fixes/2/1/art-flows/10-MON-1920x1080.png')
def col(h):return tuple(bytes.fromhex(h.lstrip('#')))+(255,)
O,D,L,M,ST,G,WHITE,BLACK=map(col,['34140e','2f1108','5c493c','774c3b','c2864d','f8c838','ffffff','050411'])
GLYPHS=['01110/11011/11011/11011/11011/11011/01110','00100/01100/00100/00100/00100/00100/01110','01110/11011/00011/00110/01100/11000/11111','11110/00011/00011/01110/00011/00011/11110','00011/00111/01111/11011/11111/00011/00011','11111/11000/11000/11110/00011/00011/11110','01110/11000/11000/11110/11011/11011/01110','11111/00011/00011/00110/01100/01100/01100','01110/11011/11011/01110/11011/11011/01110','01110/11011/11011/01111/00011/00011/01110']

def make(v):
    out={}
    button=Image.open(S/'button.png').convert('RGBA')
    # 원본 견본의 양끝 가죽+금속 조각. 중앙은 단색 레일: 늘릴 때 바늘땀 위상 단절 없음.
    hp=Image.new('RGBA',(32,6),D); d=ImageDraw.Draw(hp)
    d.rectangle((0,0,31,5),outline=O); d.line((3,1,28,1),fill=L)
    for x,srcx in [(0,0),(28,28)]:
        hp.paste(button.crop((srcx,0,srcx+4,3)),(x,0))
        hp.paste(button.crop((srcx,13,srcx+4,16)),(x,3))
    # 내측 두 줄은 막대가 끊김 없이 채워지는 자리.
    d.rectangle((3,2,28,3),fill=D)
    if v in (1,3):d.point((2,2),fill=G); d.point((29,2),fill=G)
    if v in (2,3):d.line((4,4,27,4),fill=M)
    out['hp_bar_bg']=hp
    out['hp_bar_fill']=Image.new('RGBA',(8,2),WHITE)
    cd=Image.new('RGBA',(24,4),D); d=ImageDraw.Draw(cd)
    d.rectangle((0,0,23,3),outline=O)
    d.point((1,0),fill=ST if v<2 else G);d.point((22,0),fill=ST if v<2 else G)
    if v%2:d.line((2,3,21,3),fill=L)
    out['cooldown_bg']=cd;out['cooldown_fill']=Image.new('RGBA',(8,2),WHITE)
    for name,n in [('joystick_base',40),('joystick_knob',16)]:
        im=Image.new('RGBA',(n,n)); d=ImageDraw.Draw(im)
        d.ellipse((0,0,n-1,n-1),fill=L,outline=O,width=1)
        d.ellipse((2,2,n-3,n-3),fill=M,outline=ST,width=1)
        d.ellipse((4,4,n-5,n-5),fill=D if n==40 else L,outline=O,width=1)
        # 밝은 점은 테두리 안에만 두고 중앙을 가로지르는 선은 두지 않는다.
        if n==40:
            for x,y in [(10,4),(18,2),(26,4),(34,11),(36,19),(34,27),(26,34),(18,36),(10,34),(4,27),(2,19),(4,11)]:
                d.rectangle((x,y,x+1,y),fill=ST if v<2 else G)
        else:
            d.line((5,4,10,4),fill=M if v<2 else ST)
        if v%2:
            for x,y in [(n//2,1),(n-2,n//2),(n//2,n-2),(1,n//2)]:d.point((x,y),fill=G)
        out[name]=im
    for k,g in enumerate(GLYPHS):
        mask=Image.new('L',(7,9));p=mask.load()
        rows=g.split('/')
        for y,row in enumerate(rows):
            for x,b in enumerate(row):
                if b=='1':p[x+1,y+1]=255
        # 후보는 1/7 서체의 작은 차이만 둠. 10장 한 벌의 획 두께는 유지.
        if v%2 and k==1:
            for x in (1,5):p[x,7]=255
        if v>=2 and k==7:
            for x in (1,2):p[x,2]=255
        im=Image.new('RGBA',(7,9));im.paste(BLACK,(0,0,7,9),mask.filter(ImageFilter.MaxFilter(3)))
        im.paste(WHITE,(0,0,7,9),mask)
        out[f'digit_{k}']=im
    return out

def run(*args):subprocess.run([PY,*map(str,args)],cwd=ROOT,check=True)
def nine(im,width,b):
    w,h=im.size; o=Image.new('RGBA',(width,h))
    o.paste(im.crop((0,0,b,h)),(0,0));o.paste(im.crop((w-b,0,w,h)),(width-b,0))
    o.paste(im.crop((b,0,w-b,h)).resize((width-2*b,h),Image.Resampling.NEAREST),(b,0))
    return o

def tile(im,width):return im.resize((width,im.height),Image.Resampling.NEAREST)
def create():
    W.mkdir(parents=True,exist_ok=True);(UI/'digits').mkdir(exist_ok=True)
    spec=(ROOT/'docs/art/아트-기준서.md').read_text()
    palette=json.loads(re.search(r'```json\n(.*?)```',spec.split('### 3.5')[1],re.S)[1]);(W/'palette.json').write_text(json.dumps(palette))
    allsets=[]
    for v in range(4):
        folder=W/f'c{v+1:02}';folder.mkdir(exist_ok=True);items=make(v);allsets.append(items)
        for k,im in items.items():
            im.save(folder/(k+'.png'))
            bbox=im.getbbox(); cropped=im.crop(bbox)
            block=Image.new('RGBA',cropped.size,col('ff00ff'));block.alpha_composite(cropped)
            block=block.resize((cropped.width*16,cropped.height*16),Image.Resampling.NEAREST)
            blockpath=folder/(k+'-blocks.png');block.save(blockpath)
            run(T/'pixelize.py','grid',blockpath,'--size',f'{cropped.width}x{cropped.height}')
            if v==0:
                dest=UI/('digits/'+k+'.png' if k.startswith('digit_') else k+'.png')
                run(T/'pixelize.py','one',blockpath,'--out',W/'cropped.png','--size',f'{cropped.width}x{cropped.height}','--palette',W/'palette.json')
                rebuilt=Image.new('RGBA',im.size);rebuilt.alpha_composite(Image.open(W/'cropped.png').convert('RGBA'),bbox[:2]);rebuilt.save(dest)
                assert rebuilt.tobytes()==im.tobytes(),k
    candidates=[p for p in W.glob('c*/*.png') if not p.stem.endswith('-blocks')]
    run(T/'matte.py','fringe','--whole','--key','#ff00ff',*candidates)
    final=[UI/(k+'.png') for k in allsets[0] if not k.startswith('digit_')]+list((UI/'digits').glob('*.png'))
    run(T/'matte.py','fringe','--whole','--key','#ff00ff',*final)
    # 동일 72x48 캔버스로 게임 크기 3배를 보장. 중립 번호만 쓴다.
    pages=[]
    for v,items in enumerate(allsets):
        for i,(k,im) in enumerate(items.items()):
            canvas=Image.new('RGBA',(72,48));
            if k.endswith('_fill'):ImageDraw.Draw(canvas).rectangle((20,19,51,28),fill=D)
            canvas.alpha_composite(im,((72-im.width)//2,(48-im.height)//2))
            p=W/f'{v+1:02}-{i+1:02}.png';canvas.save(p);pages.append(p)
    refs=[]
    for name in ('button','panel'):
        im=Image.open(S/(name+'.png')).convert('RGBA');c=Image.new('RGBA',(72,48));c.alpha_composite(im,((72-im.width)//2,0));p=W/f'ref-{name}.png';c.save(p);refs.append(p)
    run(T/'sheet.py','grid',ROOT/'docs/art/sheets/ui-bars.png',*pages,'--against',*refs,'--size','144','--numbered','--unverified','양산 — 사용자 판정')
    # 이름을 가린 판독 시트: 첫 세트만, 용도와 채택 여부 표기 없음.
    run(T/'sheet.py','grid',ROOT/'docs/art/sheets/ui-bars-blind.png',*pages[:16],'--against',*refs,'--size','144','--numbered','--unverified','양산 — 사용자 판정')
    composite(allsets[0])

def composite(items):
    big=Image.open(CAP).convert('RGBA')
    def paste(im,x,y):big.alpha_composite(im.resize((im.width*3,im.height*3),Image.Resampling.NEAREST),(x*3,y*3))
    def tint(im,rgb):
        z=Image.new('RGBA',im.size,rgb+(255,));z.putalpha(im.getchannel('A'));return z
    hpvals=[273/400,99/110,119/140,89/120]
    for i,f in enumerate(hpvals):
        x=6+i*159
        # 기존 아이템 칩을 지운 뒤 4 가상px 아래 옮김: HP6px와 간섭 방지 합성 예시.
        chip=big.crop(((x)*3,324*3,(x+28)*3,333*3))
        ImageDraw.Draw(big).rectangle((x*3,320*3,(x+150)*3,336*3),fill=(25,21,12,255))
        paste(nine(items['hp_bar_bg'],150,4),x,320)
        fill=tile(items['hp_bar_fill'],144)
        paste(tint(fill,(82,210,72)),x+3,322)
        # 잃은 HP 구간도 기존 게임 색만 합성. PNG 자체는 white.
        loss=144-round(144*f)
        if loss:paste(tint(tile(items['hp_bar_fill'],loss),(94,25,10)),x+3+144-loss,322)
        big.alpha_composite(chip,(x*3,328*3))
        paste(nine(items['cooldown_bg'],26,2),x,337)
        paste(tint(tile(items['cooldown_fill'],12 if i==3 else 22),(243,157,25)),x+2,338)
    paste(items['joystick_base'],25,245);paste(items['joystick_knob'],42,253)
    for text,x,y in [('128',421,204),('36',223,55),('7',145,242)]:
        for j,k in enumerate(text):paste(items['digit_'+k],x+j*8,y)
    font=ImageFont.truetype('/System/Library/Fonts/AppleSDGothicNeo.ttc',25)
    d=ImageDraw.Draw(big);d.rectangle((0,72,580,114),fill=(30,20,10,255));d.text((12,77),'합성 — 게임 화면 아님',font=font,fill='white')
    p=ROOT/'docs/art/sheets/ui-bars-composite.png';big.save(p)
    run(T/'sheet.py','grid',ROOT/'docs/art/sheets/ui-bars-composite-sheet.png',p,'--size','540','--unverified','합성 — 게임 화면 아님')
if __name__=='__main__':create()
