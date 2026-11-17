# 레나 뒤 공격 끝 그림: 기준 뒤 그림 + 당긴 시위(흰 1px) + 메긴 화살(갈색 1px, 쇠 촉, 오른쪽 위를 겨눔). 손으로 찍은 픽셀, 생성 아님.
# 앞 판 lena-front/make_end.py와 같은 방식. usage: make_end.py <lena_back.png> <out.png>
import sys
from PIL import Image
base = sys.argv[1]
out = sys.argv[2]
im = Image.open(base).convert('RGBA')


def line(a, b, c):
    (x0, y0), (x1, y1) = a, b
    n = max(abs(x1 - x0), abs(y1 - y0))
    for i in range(n + 1):
        x = round(x0 + (x1 - x0) * i / n)
        y = round(y0 + (y1 - y0) * i / n)
        im.putpixel((x, y), c + (255,))


STR = (255, 249, 237)
SHAFT = (175, 100, 46)
TIP = (200, 184, 184)
OUT = (47, 17, 8)
pull = (40, 39)
line((49, 26), pull, STR)
line((45, 51), pull, STR)
for x in range(41, 54):
    im.putpixel((x, 39), SHAFT + (255,))
for x in range(40, 55):
    for y in (38, 40):
        if im.getpixel((x, y))[3] == 0:
            im.putpixel((x, y), OUT + (255,))
im.putpixel((54, 39), TIP + (255,))
im.putpixel((53, 38), TIP + (255,))
im.putpixel((53, 40), TIP + (255,))
for p in [(55, 39), (54, 38), (54, 40), (53, 37), (53, 41)]:
    if im.getpixel(p)[3] == 0:
        im.putpixel(p, OUT + (255,))
im.putpixel((39, 39), (255, 224, 138, 255))  # 깃
im.save(out)
