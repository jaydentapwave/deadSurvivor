# 레나 공격 끝 그림: 기준 그림 + 당긴 시위(흰 1px) + 메긴 화살(갈색 1px, 쇠 촉). 손으로 찍은 픽셀, 생성 아님.
import sys
from PIL import Image
base=sys.argv[1]; out=sys.argv[2]
im=Image.open(base).convert('RGBA')
def line(a,b,c):
    (x0,y0),(x1,y1)=a,b; n=max(abs(x1-x0),abs(y1-y0))
    for i in range(n+1):
        x=round(x0+(x1-x0)*i/n); y=round(y0+(y1-y0)*i/n); im.putpixel((x,y),c+(255,))
STR=(255,249,237); SHAFT=(175,100,46); TIP=(200,184,184); OUT=(47,17,8)
pull=(40,41)
line((47,30),pull,STR); line((44,47),pull,STR)
for x in range(41,51): im.putpixel((x,41),SHAFT+(255,))
for x in range(40,51): 
    for y in (40,42):
        if im.getpixel((x,y))[3]==0: im.putpixel((x,y),OUT+(255,))
im.putpixel((51,41),TIP+(255,)); im.putpixel((50,40),TIP+(255,)); im.putpixel((50,42),TIP+(255,))
for p in [(52,41),(51,40),(51,42),(50,39),(50,43)]:
    if im.getpixel(p)[3]==0: im.putpixel(p,OUT+(255,))
im.putpixel((39,41),(255,224,138,255))  # 깃
im.save(out)
