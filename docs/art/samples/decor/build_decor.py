"""장식 그림체 견본 합성 — 최신 전투 캡처(640x360으로 내림)의 가장자리에 꽃·나무·돌을 게임 크기로 놓는다.

입력: docs/art/samples/decor/<s>/flower.png, tree.png, rock.png (배경 지움·도트 정리 끝, bbox로 자름, 피벗 = 바닥 가운데)
출력: <s>/field.png(640x360 합성), <s>/field_x3.png(3배 + 같은 축척의 컨셉 조각, 머리에 "합성 — 게임 화면 아님")
실행: /usr/bin/python3 docs/art/samples/decor/build_decor.py <캡처 png> s1 s2 s3
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"
# 컨셉(1672 폭) 1px = 게임 640/1672 px. x3 보기에서는 3배.
CONCEPT_TO_X3 = 3 * 640 / 1672
# (이름, 피벗 x, 피벗 y) — 가운데(대략 x 200~440, y 90~230)는 비우고 가장자리에만 (기준서 3.2)
PLACES = [
    ("tree", 34, 112), ("flower", 66, 120), ("rock", 22, 150), ("flower", 40, 205),
    ("rock", 80, 262), ("flower", 140, 270), ("flower", 250, 40), ("rock", 300, 34),
    ("flower", 420, 44), ("tree", 606, 96), ("flower", 572, 104), ("rock", 616, 170),
    ("flower", 596, 236), ("tree", 560, 274), ("rock", 470, 270), ("flower", 515, 262),
]


def paste(dst, spr, cx, by):
    x = cx - spr.width // 2
    y = by - spr.height
    dst.alpha_composite(spr, (x, y))


def main():
    cap_path = sys.argv[1]
    cap = Image.open(cap_path).convert("RGBA").resize((640, 360), Image.NEAREST)
    crops = [Image.open(HERE / f"concept_PE-G001_{n}.png").convert("RGBA") for n in ("flower", "tree", "rock")]
    crops = [c.resize((round(c.width * CONCEPT_TO_X3), round(c.height * CONCEPT_TO_X3)), Image.LANCZOS) for c in crops]
    font = ImageFont.truetype(FONT, 26)
    for s in sys.argv[2:]:
        d = HERE / s
        spr = {n: Image.open(d / f"{n}.png").convert("RGBA") for n in ("flower", "tree", "rock")}
        comp = cap.copy()
        for n, cx, by in sorted(PLACES, key=lambda p: p[2]):  # 화면 Y로 앞뒤
            paste(comp, spr[n], cx, by)
        comp.save(d / "field.png")
        big = comp.resize((1920, 1080), Image.NEAREST)
        side = max(c.width for c in crops) + 20
        out = Image.new("RGBA", (1920 + side, 1080 + 48), (255, 249, 231, 255))
        out.paste(big, (0, 48))
        y = 60
        dr = ImageDraw.Draw(out)
        dr.text((10, 8), f"합성 — 게임 화면 아님 · 장식 견본 단계 {s[1:]} · 전투 캡처 02-G2 위, 게임 크기 ×3", fill=(52, 20, 14), font=font)
        for c, name in zip(crops, ("컨셉 꽃", "컨셉 나무", "컨셉 돌")):
            dr.text((1930, y), name + " (같은 축척)", fill=(52, 20, 14), font=ImageFont.truetype(FONT, 20))
            out.alpha_composite(c, (1930, y + 26))
            y += c.height + 40
        out.convert("RGB").save(d / "field_x3.png")
        print(s, {n: spr[n].size for n in spr})


if __name__ == "__main__":
    main()
