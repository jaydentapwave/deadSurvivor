"""기준 뒤 그림 픽셀로 등불을 들어 올린 끝 자세. 원본은 보존."""
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np
import sys
ROOT=Path(__file__).resolve().parents[5]
sys.path.insert(0,str(ROOT/'Tools'))
import check_motion as cm
out=ROOT/'output/2d-art/bolt-back-aether/inputs';out.mkdir(parents=True,exist_ok=True)
base=Image.open(ROOT/'Assets/Game/03.Resources/Art/AIGenerated/Characters/bolt/back/bolt_back.png').convert('RGBA')
a=np.array(base);hm=cm.exclude_boxes(cm.head_mask(a),cm.held_boxes('bolt_back.png'))
# 배낭 촛불은 고정 부속이다. 촛불만 제거해도 머리 판정이 유지되는지 생성 전에 확인한다.
t=a.copy();t[21:33,19:25]=0
score=cm.head_score(t,a,hm)
assert score[0]==1 and score[1]==0,score
print('held precheck:',score,'mask pixels',int(hm.sum()))
# 오른손의 등불/손 픽셀. 기준 등불은 x41..46, y41..50.
prop=np.zeros_like(a);prop[40:51,41:47]=a[40:51,41:47]
body=a.copy();body[40:51,41:47]=0
end=Image.fromarray(body);d=ImageDraw.Draw(end)
d.line([(42,38),(48,33),(49,27)],fill=(47,17,8,255),width=6)
d.line([(42,38),(48,33),(49,27)],fill=(255,249,237,255),width=4)
d.line([(44,39),(50,33),(51,27)],fill=(227,215,200,255),width=2)
end.alpha_composite(Image.fromarray(prop),(7,-15))
end.save(out/'attack_end_back.png')
for name,im in [('stand_back',base),('attack_end_back',end)]:
    # 최근접 ×8, 위·오른쪽 각16 게임px 여백. 피벗x32 유지.
    wide=Image.new('RGBA',(640,640));wide.alpha_composite(im.resize((512,512),Image.Resampling.NEAREST),(0,128));wide.save(out/(name+'_x8_wide.png'))
