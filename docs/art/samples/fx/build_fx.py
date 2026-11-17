"""효과 그림체 견본 합성 — 최신 전투 캡처(640x360으로 내림) 위에 장검 베기·화염구를 게임 크기로 얹는다.

입력: docs/art/samples/fx/<s>/slash.png, fireball.png (배경 지움·도트 정리 끝, bbox로 자름)
      과녁 몬스터: 게임 그림 slime.png·boar.png를 1:1(게임 크기)로 놓는다. 캡처 G2에는 몬스터가 없어서다.
출력: <s>/battle.png(640x360 합성), <s>/battle_x3.png(합성 일부 ×3 + 같은 축척 컨셉 조각, 머리에 "합성 — 게임 화면 아님")
실행: /usr/bin/python3 docs/art/samples/fx/build_fx.py <캡처 png> s1 s2 s3
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
MON = ROOT / "Assets/Game/03.Resources/Art/AIGenerated/Monsters"
FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"
CONCEPT_TO_X3 = 3 * 640 / 1672  # 컨셉(1672 폭) 1px = 게임 640/1672 px
# 640x360 좌표. 브루노 머리 x 300~317(뒷줄), 미라 x 291~320 y 138~185(앞줄 왼쪽, 지팡이 끝 약 (293,145))
SLASH_RIGHT_MID = (292, 152)   # 베기 호의 열린 쪽(오른쪽) 가운데 = 브루노 앞(왼쪽)
FIREBALL_LEFT_MID = (214, 150)  # 화염구 머리 = 미라에서 왼쪽 몬스터 쪽으로 날아가는 중
TARGETS = [("slime", 186, 166), ("boar", 236, 204), ("slime", 252, 160)]  # (이름, 피벗 x, 바닥 y)
CROP_X3 = (150, 100, 350, 215)  # x3 보기로 키울 부분


def sprite(name):
    im = Image.open(MON / name / f"{name}.png").convert("RGBA")
    return im.crop(im.getbbox())


def main():
    cap = Image.open(sys.argv[1]).convert("RGBA").resize((640, 360), Image.NEAREST)
    crops = [Image.open(HERE / f"concept_{n}.png").convert("RGBA") for n in ("slash", "bolt", "comet")]
    crops = [c.resize((round(c.width * CONCEPT_TO_X3), round(c.height * CONCEPT_TO_X3)), Image.LANCZOS) for c in crops]
    font = ImageFont.truetype(FONT, 24)
    small = ImageFont.truetype(FONT, 18)
    for s in sys.argv[2:]:
        d = HERE / s
        slash = Image.open(d / "slash.png").convert("RGBA")
        fire = Image.open(d / "fireball.png").convert("RGBA")
        comp = cap.copy()
        for n, cx, by in sorted(TARGETS, key=lambda t: t[2]):
            m = sprite(n)
            comp.alpha_composite(m, (cx - m.width // 2, by - m.height))
        comp.alpha_composite(slash, (SLASH_RIGHT_MID[0] - slash.width, SLASH_RIGHT_MID[1] - slash.height // 2))
        comp.alpha_composite(fire, (FIREBALL_LEFT_MID[0], FIREBALL_LEFT_MID[1] - fire.height // 2))
        comp.save(d / "battle.png")
        x0, y0, x1, y1 = CROP_X3
        big = comp.crop(CROP_X3).resize(((x1 - x0) * 3, (y1 - y0) * 3), Image.NEAREST)
        side = max(c.width for c in crops) + 20
        out = Image.new("RGBA", (big.width + side + 10, max(big.height, sum(c.height + 34 for c in crops)) + 48),
                        (255, 249, 231, 255))
        out.paste(big, (0, 48))
        dr = ImageDraw.Draw(out)
        dr.text((10, 10), f"합성 — 게임 화면 아님 · 효과 견본 단계 {s[1:]} · 전투 캡처 02-G2 위, 게임 크기 ×3",
                fill=(52, 20, 14), font=font)
        y = 54
        for c, name in zip(crops, ("컨셉 베기", "컨셉 마법탄(밤)", "컨셉 혜성탄(밤)")):
            dr.text((big.width + 20, y), name + " (같은 축척)", fill=(52, 20, 14), font=small)
            out.alpha_composite(c, (big.width + 20, y + 22))
            y += c.height + 34
        out.convert("RGB").save(d / "battle_x3.png")
        print(s, "slash", slash.size, "fireball", fire.size)


if __name__ == "__main__":
    main()
