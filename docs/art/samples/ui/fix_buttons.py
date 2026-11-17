"""버튼 직접 손질(기준서 3.3 UI 행 "Codex + 직접 손질").

s2·s3 버튼은 2회 생성 모두 32x16 격자보다 잘게 그려져(pixelize GRID_MISMATCH), 내린 뒤 바늘땀이 군데군데 빠지고
변이 고르지 않았다. 9-slice로 늘리려면 변이 고르게 반복돼야 한다. 그래서 테두리 4도트를 생성 프롬프트
(s*/prompt_button_r2.txt)에 적은 줄 구성 그대로 다시 칠하고, 가운데 24x8은 생성 결과(내린 그림)의 가운데 색을 쓴다.
s1은 2회차 생성이 격자와 맞아(32x16, 점수 0.95) 손질하지 않는다.
실행: /usr/bin/python3 docs/art/samples/ui/fix_buttons.py <내린 s2 버튼> <내린 s3 버튼>
"""
import sys
from collections import Counter
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
W, H, B = 32, 16, 4


def hx(s):
    return tuple(int(s[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


OUTLINE, DARK, LEATHER, MID, LIGHT, STITCH, BRASS, WHITE = map(
    hx, ["#34140e", "#2f1108", "#5c493c", "#774c3b", "#af642e", "#c2864d", "#f8b828", "#fff9ed"])


def ring(x, y):
    """테두리 안에서 바깥부터 몇 번째 줄인가(0~3), 테두리 밖이면 None."""
    d = min(x, y, W - 1 - x, H - 1 - y)
    return d if d < B else None


def build(src, level):
    out = src.copy()
    center = Counter(src.getpixel((x, y)) for x in range(B, W - B) for y in range(B, H - B)).most_common(1)[0][0]
    for y in range(H):
        for x in range(W):
            r = ring(x, y)
            if r is None:
                c = center  # 9-slice 가운데는 한 색이어야 반복해도 줄이 생기지 않는다
            elif r == 0:
                c = OUTLINE
            elif r == 1:
                c = LEATHER
            elif r == 2:
                along = x if y in (2, H - 3) else y
                c = STITCH if along % 2 == 0 else LEATHER
            elif level == "s2":
                c = LEATHER
            else:
                # s3 프롬프트의 "가운데 위 한 줄 밝게"는 위 테두리 안쪽 줄로 옮겼다(가운데에 두면 늘릴 때 반복돼 글자를 가로지른다)
                c = LIGHT if y == B - 1 and B - 1 <= x <= W - B else DARK
            out.putpixel((x, y), c)
    for cx, cy in ((0, 0), (W - B, 0), (0, H - B), (W - B, H - B)):
        if level == "s2":
            out.putpixel((cx + (2 if cx == 0 else 1), cy + (2 if cy == 0 else 1)), BRASS)
        else:
            ox, oy = cx + (1 if cx == 0 else 1), cy + (1 if cy == 0 else 1)
            for dx in (0, 1):
                for dy in (0, 1):
                    out.putpixel((ox + dx, oy + dy), BRASS)
            out.putpixel((ox, oy), WHITE)
    return out


def main():
    for path, level in zip(sys.argv[1:3], ("s2", "s3")):
        src = Image.open(path).convert("RGBA")
        assert src.size == (W, H), src.size
        build(src, level).save(HERE / level / "button.png")
        print(level, "->", HERE / level / "button.png")


if __name__ == "__main__":
    main()
