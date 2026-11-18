"""UI 그림체 견본 합성 — 최신 전투·정비 캡처의 임시 판넬·버튼·가방 칸 자리에 견본을 게임 크기로 얹는다.

입력: docs/art/samples/ui/<s>/button.png(32x16), panel.png(48x48), cell.png(16x16) — 배경 지움·도트 정리 끝
9-slice: 버튼 테두리 4도트, 판넬 테두리 6도트(s3는 12도트). 모서리는 그대로, 변·가운데는 반복(Unity Image Type Sliced + Tiled 변 기준).
출력: <s>/battle.png, <s>/camp.png(640x360, 글자 없음),
      <s>/battle_x3.png, <s>/camp_x3.png(1920x1080 + 같은 축척 컨셉 조각, 지금 글꼴 NotoSansKR로 글자를 얹음,
      머리에 "합성 — 게임 화면 아님")
실행: /usr/bin/python3 docs/art/samples/ui/build_ui.py <02-G2 캡처> <03-G3 캡처> s1 s2 s3
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
GAME_FONT = str(ROOT / "Assets/Game/03.Resources/Font/NotoSansKR.ttf")
LABEL_FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"
BTN_BORDER = 4
PAD_Y = 12  # 전투 대원 판넬의 글자·HP 막대·칩을 테두리 안으로 내리는 값(1920 px)
# 판넬 테두리(도트): 안쪽 선·모서리 장식이 모두 테두리 안에 들도록 단계마다 잼(s3는 안쪽 모서리 눈금이 12도트 안에 있다)
PANEL_BORDER = {"s1": 6, "s2": 6, "s3": 12}
CONCEPT_TO_X3 = 3 * 640 / 1672  # 컨셉 1px = 게임 640/1672 px, x3 보기

# 640x360 좌표 (x0, y0, x1, y1) — 캡처에서 잰 임시 도형 자리
BATTLE_PANELS = [(3, 285, 159, 358), (163, 285, 318, 358), (322, 285, 477, 358), (481, 285, 637, 358)]
BATTLE_BUTTONS = [((542, 2, 578, 20), "×1"), ((582, 2, 634, 20), "일시정지")]
CAMP_PANELS = [(3, 2, 637, 66), (3, 164, 184, 294), (456, 164, 637, 294), (180, 298, 460, 358)]
CAMP_BUTTONS = [((520, 5, 630, 27), "다음 웨이브"), ((270, 196, 370, 220), "회전"), ((270, 228, 370, 252), "선택 취소")]
BAG_X0 = [110, 222, 334, 446]
BAG_Y0 = 72
BAG_PITCH = 22
BAG_LABELS = ["장검", "단궁", "화염", "치유"]
HEROES = [("브루노", "400/400"), ("레나", "110/110"), ("미라", "140/140"), ("볼트", "120/120")]
# 1920 좌표 — 판넬 위에 그대로 옮겨 붙이는 기존 게임 요소(HP 막대·아이템 칩·보관함 카드·진형 버튼)
BATTLE_COPY = []
for i, (x0, _, _, _) in enumerate(BATTLE_PANELS):
    BATTLE_COPY += [(x0 * 3 + 14, 894, x0 * 3 + 466, 906), (x0 * 3 + 14, 912, x0 * 3 + 106, 954)]
CAMP_COPY = [(24, 540, 192, 692), (204, 540, 372, 692), (24, 708, 192, 860), (204, 708, 372, 860),
             (556, 906, 786, 1062), (980, 930, 1204, 1040)]


def nine(src, b, w, h):
    """모서리 b x b는 그대로, 변·가운데는 반복해 w x h로 만든다."""
    sw, sh = src.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))

    def tile(box, dst):
        piece = src.crop(box)
        pw, ph = piece.size
        x0, y0, x1, y1 = dst
        for y in range(y0, y1, ph):
            for x in range(x0, x1, pw):
                cw, ch = min(pw, x1 - x), min(ph, y1 - y)
                out.alpha_composite(piece.crop((0, 0, cw, ch)), (x, y))

    xs = [(0, b, 0, b), (b, sw - b, b, w - b), (sw - b, sw, w - b, w)]
    ys = [(0, b, 0, b), (b, sh - b, b, h - b), (sh - b, sh, h - b, h)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            tile((sx0, sy0, sx1, sy1), (dx0, dy0, dx1, dy1))
    return out


def place(comp, big, spr, b, box):
    """640 합성과 1920 합성(원본 캡처 위, 견본만 최근접 3배)에 같은 자리로 얹는다."""
    x0, y0, x1, y1 = box
    piece = nine(spr, b, x1 - x0, y1 - y0)
    comp.alpha_composite(piece, (x0, y0))
    big.alpha_composite(piece.resize((piece.width * 3, piece.height * 3), Image.NEAREST), (x0 * 3, y0 * 3))


def text_c(dr, box, s, font, fill=(255, 255, 255)):
    x0, y0, x1, y1 = [v * 3 for v in box]
    l, t, r, btm = dr.textbbox((0, 0), s, font=font)
    dr.text(((x0 + x1 - (r - l)) / 2 - l, (y0 + y1 - (btm - t)) / 2 - t), s, font=font, fill=fill)


def frame(big, title, crops):
    side = max(c.width for c in crops) + 24
    out = Image.new("RGBA", (1920 + side, 1080 + 48), (255, 249, 231, 255))
    out.paste(big, (0, 48))
    dr = ImageDraw.Draw(out)
    dr.text((10, 8), title, fill=(52, 20, 14), font=ImageFont.truetype(LABEL_FONT, 26))
    y = 60
    for c, name in crops_named(crops):
        dr.text((1932, y), name + " (같은 축척)", fill=(52, 20, 14), font=ImageFont.truetype(LABEL_FONT, 20))
        out.alpha_composite(c, (1932, y + 26))
        y += c.height + 40
    return out.convert("RGB")


def crops_named(crops):
    return zip(crops, ("컨셉 가방 모서리", "컨셉 가방 아래 칸", "컨셉 양피지 칸"))


def main():
    battle_cap = Image.open(sys.argv[1]).convert("RGBA")
    camp_cap = Image.open(sys.argv[2]).convert("RGBA")
    crops = []
    for n in ("bag_corner", "bag_bottom", "cell"):
        c = Image.open(HERE / f"concept_PE-G004_{n}.png").convert("RGBA")
        crops.append(c.resize((round(c.width * CONCEPT_TO_X3), round(c.height * CONCEPT_TO_X3)), Image.LANCZOS))
    f_btn = ImageFont.truetype(GAME_FONT, 30)
    f_small = ImageFont.truetype(GAME_FONT, 28)
    f_title = ImageFont.truetype(GAME_FONT, 44)
    f_cell = ImageFont.truetype(GAME_FONT, 24)
    for s in sys.argv[3:]:
        d = HERE / s
        btn = Image.open(d / "button.png").convert("RGBA")
        pan = Image.open(d / "panel.png").convert("RGBA")
        cell = Image.open(d / "cell.png").convert("RGBA")

        # 전투
        comp = battle_cap.resize((640, 360), Image.NEAREST)
        big = battle_cap.copy()
        for box in BATTLE_PANELS:
            place(comp, big, pan, PANEL_BORDER[s], box)
        for box, _ in BATTLE_BUTTONS:
            place(comp, big, btn, BTN_BORDER, box)
        comp.save(d / "battle.png")
        for r in BATTLE_COPY:  # 테두리 안으로 4도트(12px) 내려 놓는다 — 지금 배치는 판넬 안쪽 여백이 없다
            big.paste(battle_cap.crop(r), (r[0], r[1] + PAD_Y))
        dr = ImageDraw.Draw(big)
        for box, label in BATTLE_BUTTONS:
            text_c(dr, box, label, f_btn)
        for (x0, y0, x1, _), (name, hp) in zip(BATTLE_PANELS, HEROES):
            dr.text((x0 * 3 + 22, y0 * 3 + 2 + PAD_Y), name, font=f_small, fill=(255, 255, 255))
            w = dr.textlength(hp, font=f_small)
            dr.text((x1 * 3 - 22 - w, y0 * 3 + 2 + PAD_Y), hp, font=f_small, fill=(255, 255, 255))
        frame(big, f"합성 — 게임 화면 아님 · UI 견본 단계 {s[1:]} · 전투 캡처 02-G2 위, 게임 크기 ×3, 글자는 지금 글꼴",
              crops).save(d / "battle_x3.png")

        # 정비
        comp = camp_cap.resize((640, 360), Image.NEAREST)
        big = camp_cap.copy()
        for box in CAMP_PANELS:
            place(comp, big, pan, PANEL_BORDER[s], box)
        cell3 = cell.resize((48, 48), Image.NEAREST)
        for x0 in BAG_X0:
            for j in range(4):
                for i in range(4):
                    x, y = x0 + i * BAG_PITCH + 1, BAG_Y0 + j * BAG_PITCH + 1
                    comp.alpha_composite(cell, (x, y))
                    big.alpha_composite(cell3, (x * 3, y * 3))
        for box, _ in CAMP_BUTTONS:
            place(comp, big, btn, BTN_BORDER, box)
        comp.save(d / "camp.png")
        for r in CAMP_COPY:
            big.paste(camp_cap.crop(r), r[:2])
        dr = ImageDraw.Draw(big)
        dr.text((36, 18), "웨이브 1 완료", font=f_title, fill=(255, 255, 255))
        for k, (name, hp) in enumerate(HEROES):
            dr.text((322 + k * 340, 86), f"{name}  HP {hp}", font=f_small, fill=(255, 255, 255))
        dr.text((28, 500), "보관함", font=f_small, fill=(255, 255, 255))
        for x0, label in zip(BAG_X0, BAG_LABELS):
            dr.text(((x0 + 1) * 3 + 4, (BAG_Y0 + 1) * 3 + 6), label, font=f_cell, fill=(92, 73, 60))
        for box, label in CAMP_BUTTONS:
            text_c(dr, box, label, f_btn)
        frame(big, f"합성 — 게임 화면 아님 · UI 견본 단계 {s[1:]} · 정비 캡처 03-G3 위, 게임 크기 ×3, 글자는 지금 글꼴",
              crops).save(d / "camp_x3.png")
        print(s, btn.size, pan.size, cell.size)


if __name__ == "__main__":
    main()
