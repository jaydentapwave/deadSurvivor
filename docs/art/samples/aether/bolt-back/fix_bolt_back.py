"""볼트 Aether 손질. 영상 몸·팔다리는 보존, 기준 머리·등불·배낭 촛불을 정수 이동해 복원."""
import json
import sys
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'docs/art/samples/aether/bruno-front'))
sys.path.insert(0, str(ROOT / 'docs/art/samples/aether/lena-front'))
sys.path.insert(0, str(ROOT / 'Tools'))
import fix_front as ff
import check_motion as cm
from fix_lena import fix_key_residue


def main():
    p = json.loads(Path(sys.argv[1]).read_text())
    base = ff.load(p['base'])
    hm = cm.exclude_boxes(cm.head_mask(base), cm.held_boxes(p['base']))
    lantern = np.zeros((64, 64), bool)
    # 등불의 계단 모양 왼쪽 외곽선·맨 아래 줄까지 원본에서 가져온다.
    for y0,y1,x0 in [(41,43,42),(43,45,41),(45,49,40),(49,52,41)]:
        lantern[y0:y1,x0:47]=base[y0:y1,x0:47,3]>0
    candle = np.zeros((64, 64), bool)
    for x0, y0, x1, y1 in cm.held_boxes(p['base']):
        candle[y0:y1 + 1, x0:x1 + 1] = base[y0:y1 + 1, x0:x1 + 1, 3] > 0
    out = ROOT / p['out_dir']; out.mkdir(parents=True, exist_ok=True)
    log = []
    for i, fr in enumerate(p['frames']):
        rec = {'cell': fr.get('cell'), 'src': fr['src']}
        if fr['src'] == 'base':
            frame = base.copy()
        else:
            frame, _, lost = ff.to64(ff.load(fr['src']))
            rec['lost_by_crop'] = lost
            if lost:
                raise ValueError('원본 몸·소품 잘림은 확인 후 처리해야 한다')
            for x0, y0, x1, y1 in fr.get('erase_boxes', []):
                frame[y0:y1 + 1, x0:x1 + 1] = 0
            for x, y in fr.get('erase_px', []): frame[y, x] = 0
            dx, dy = fr.get('head_shift', ff.find_shift(frame, base, hm))
            rec['head_shift'] = [int(dx), int(dy)]
            area = np.zeros((64, 64), bool)
            for y, x in zip(*np.nonzero(hm)):
                area[max(0,y+dy-2):min(64,y+dy+3), max(0,x+dx-2):min(64,x+dx+3)] = True
            area[max(0,36+dy):] = False
            for y,x in zip(*np.nonzero(area)):
                if not ff.in_boxes(x,y,fr.get('keep_boxes',[])): frame[y,x] = 0
            ff.paste(frame, ff.patch_pixels(base, hm, dx, dy))
            # 배낭 위 작은 촛불은 별도 고정 부속. 등불과 혼동하지 않는다.
            if fr.get('candle_shift') is not None:
                cx,cy=fr['candle_shift'];ff.paste(frame,ff.patch_pixels(base,candle,cx,cy))
            if fr.get('lantern_shift') is not None:
                lx,ly=fr['lantern_shift']
                pix=ff.patch_pixels(base,lantern,lx,ly)
                rec['lantern_outside_px']=sum(not (0<=x<64 and 0<=y<64) for x,y,c in pix)
                if rec['lantern_outside_px']: raise ValueError('등불 캔버스 이탈')
                ff.paste(frame,pix)
            for x, y, color in fr.get('fill_px', []): frame[y,x] = color
            rec['key_residue_px']=fix_key_residue(frame)
        name=f"{p['prefix']}_{i:02d}.png"
        Image.fromarray(frame).save(out/name)
        rec['out']=name;log.append(rec)
    print(json.dumps(log,ensure_ascii=False,indent=2))


if __name__ == '__main__': main()
