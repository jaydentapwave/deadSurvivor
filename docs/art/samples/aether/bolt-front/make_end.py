"""볼트 치유 끝 자세: 기준 성물과 손을 들어 오른쪽 앞으로 내민다. 정수 픽셀 편집."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[5]
OUT = ROOT / 'output/2d-art/bolt-front-aether/inputs'
OUT.mkdir(parents=True, exist_ok=True)
base = Image.open(ROOT / 'Assets/Game/03.Resources/Art/AIGenerated/Characters/bolt/front/bolt_front.png').convert('RGBA')
a = np.array(base)
# 등불 몸통·손. 배낭 위 촛불은 고정 부속이며 들고 있는 성물과 구분한다.
mask = np.zeros((64, 64), bool)
mask[36:51, 30:42] = a[36:51, 30:42, 3] > 0
prop = a.copy(); prop[~mask] = 0
body = a.copy(); body[mask] = 0
# 원래 성물 뒤 가려진 로브: 이웃 로브의 색만 쓰며 양쪽 외곽은 남긴다.
for y in range(40, 51):
    for x in range(30, 40):
        if mask[y, x]: body[y, x] = (255, 249, 237, 255) if x < 37 else (219, 197, 172, 255)
end = Image.fromarray(body)
d = ImageDraw.Draw(end)
# 어깨에서 팔꿈치·손까지 이어지는 흰 소매 전체를 위·앞으로 든다.
d.line([(42, 40), (47, 35), (44, 30)], fill=(47,17,8,255), width=6)
d.line([(42, 39), (47, 34), (44, 29)], fill=(255,249,237,255), width=4)
d.line([(43, 40), (48, 35), (45, 30)], fill=(219,197,172,255), width=2)
end.alpha_composite(Image.fromarray(prop), (10, -10))
base.resize((512,512), Image.Resampling.NEAREST).save(OUT / 'stand_front_x8.png')
end.save(OUT / 'attack_end_front.png')
end.resize((512,512), Image.Resampling.NEAREST).save(OUT / 'attack_end_front_x8.png')
