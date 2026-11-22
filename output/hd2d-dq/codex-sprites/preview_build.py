"""Build the requested local comparison page from measured artifacts."""
from pathlib import Path
import json,html,datetime
P=Path('output/hd2d-dq/codex-sprites')
measure=json.loads((P/'frame-measurements.json').read_text())['frames']
names={'walk_right':'오른쪽 걷기','walk_down':'정면 걷기','attack_right':'오른쪽 칼 공격'}
verdicts={
 'A/walk_right/retry':('FAIL','다리 교대는 보임. 갑옷·방패·칼 무늬와 크기가 장마다 달라짐.'),
 'A/walk_down':('FAIL','왼발·오른발은 번갈아 나옴. 몸통·방패가 흔들리고 장비 세부가 바뀜.'),
 'A/attack_right':('FAIL','위→앞→아래→돌아옴은 읽힘. 준비 때 칼이 머리 뒤에 가리고 칼 크기가 달라짐. 구멍 검사 실패.'),
 'B/walk_right':('FAIL','머리·몸통·장비는 원본으로 고정. 1·3장의 발은 교대하지만 2·4장 중간 자세가 비슷해 박자가 끊김.'),
 'B/walk_down':('FAIL','양발 교대와 원본 머리·몸통은 유지. 다리 무늬와 칼 아래쪽 이음이 장마다 달라짐.'),
 'B/attack_right':('FAIL','세 방법 중 공격은 가장 잘 읽힘. 방패·팔·다리와 칼 모양 변화, 3장째 작은 구멍이 남음.'),
 'C/walk_right':('FAIL','다리 교대·원본 색과 장비 유지. 무릎이 굽지 않고 다리 조각이 좌우로 밀리는 느낌.'),
 'C/walk_down':('FAIL','양발을 번갈아 들지만 발만 위아래로 움직이는 느낌. 팔·몸통 동작 없음.'),
 'C/attack_right':('FAIL','같은 칼을 유지. 90도씩 움직이는 칼과 각진 팔 때문에 자연스러운 휘두르기에는 못 미침.'),
}
review_path=P/'review-r1/review-status.json'
if (P/'review-r2/review-status.json').exists():review_path=P/'review-r2/review-status.json'
if (P/'review-final/review-status.json').exists():review_path=P/'review-final/review-status.json'
if review_path.exists():
 review=json.loads(review_path.read_text())
 for clip in review['carried_from']:
  verdicts[clip]=('PASS','Claude 독립 판독 PASS. 그림·재생 파일을 바꾸지 않고 판정을 이어 갑니다. 작은 흠은 리뷰 기록에 남겼습니다.')
 if 'review-r2' in str(review_path) or 'review-final' in str(review_path):
  verdicts['B/walk_down']=('PASS','Claude 독립 판독 PASS(낮은 확신). 다리 사이 망토 조각 때문에 읽기 어렵고 3장째는 거의 읽히지 않는다는 후속 의견을 유지합니다. 이번에는 수정하지 않았습니다.')
  verdicts['A/attack_right']=('PASS','Claude 독립 판독 PASS. 3장째 손 근처의 작은 검은 덩어리는 후속 목록에 남겼으며 이번에는 수정하지 않았습니다.')
  verdicts['C/walk_down']=('PASS','Claude 독립 판독 PASS. 올린 발이 조금 떨어져 보인다는 후속 의견을 유지하며 이번에는 수정하지 않았습니다.')
 if review.get('finished'):
  verdicts['C/walk_right']=('PASS','Claude C4 동작 판독 PASS(중간 확신). 밝은 가까운 다리가 앞→뒤로 가며 교대하고 장화는 정강이에 연결됩니다. 상체가 고정돼 조금 뻣뻣하다는 후속 의견을 유지합니다. 기준 그림 별도 대조는 하지 않은 판독입니다.')
  verdicts['C/attack_right']=('PASS','Claude C5 동작 판독 PASS. 준비→휘두름→끝→복귀로 읽히며 팔이 몸에 붙어 보입니다. 1장째 팔이 조금 가늘고 2·3장째 상체가 넓어 보이는 점은 후속 의견입니다. 기준 그림 별도 대조는 하지 않은 판독입니다.')
 for clip,reason in review['recheck'].items():
  verdicts[clip]=('NEEDS_REVIEW','이전 FAIL을 원본 픽셀 조각으로 수정했습니다. 수정 후 독립 재판독 대기입니다. '+reason)
legs={
 'A/walk_right/retry':['가까운 다리','가까운 다리 접지·통과','먼 다리','먼 다리 접지·통과'],
 'A/walk_down':['화면 왼발','왼발 접지·통과','화면 오른발','오른발 접지·통과'],
 'B/walk_right':['가까운 다리','다리 통과·뒤꿈치 연결(Claude PASS)','먼 다리','다리 통과·뒤꿈치 연결(Claude PASS)'],
 'B/walk_down':['화면 왼발','왼발 접지·통과','화면 오른발','오른발 접지·통과'],
 'C/walk_right':['가까운 다리','가까운 다리 접지·통과','먼 다리','먼 다리 접지·통과'],
 'C/walk_down':['화면 왼발','왼발 접지·통과','화면 오른발','오른발 접지·통과']
}
(P/'producer-observations.json').write_text(json.dumps({'status':'final motion reads: nine clip PASS with scope limitations; user adoption not run' if review_path.exists() and review.get('finished') else f'latest independent VERDICT: FAIL; {len(review["carried_from"])} clip PASS carried, {len(review["recheck"])} repairs awaiting recheck' if review_path.exists() else 'producer-only; independent reading NOT_RUN','verdicts':verdicts,'legs':legs,'attack_order':['wind-up: upward','strike: right','follow-through: downward','recovery: waist'],'recommendation':'소량 자세 시안과 손질 출발점은 B. 40클립 양산 승인은 보류. Aether 품질은 이번에 측정하지 않아 우열을 단정하지 않음.'},ensure_ascii=False,indent=2))
e=html.escape
def image(path,alt,klass=''):
 return f'<a href="{e(path)}"><img class="{klass}" src="{e(path)}" alt="{e(alt)}" loading="lazy"></a>'
def trio(clip,method):
 folder=method+'/'+clip
 if folder=='A/walk_right':folder+='/retry'
 v,reason=verdicts[folder]
 status='독립 판독 PASS' if v=='PASS' else '수정본 · 재판독 대기' if v=='NEEDS_REVIEW' else '떨어진 것'
 return f'<section><h2>{names[clip]} · 방법 {method} <span class="fail">{status}</span></h2><p>{reason}</p><div class="three"><figure>{image(folder+"/anim_8x.gif","새 동작 재생 8배")}<figcaption>새 동작 · 8배 · 10fps · 0.4초 반복</figcaption></figure><figure>{image(folder+"/row_4x.png","새 동작 프레임 줄 4배","row")}<figcaption>프레임 줄 · 4배 · 눌러 크게 보기</figcaption></figure><figure>{image("old/"+clip+"/anim_8x.gif","예전 같은 동작 비교")}<figcaption>예전 같은 동작 · 10fps · 박자 참고용</figcaption></figure></div><p><a href="{folder}/anim_4x.gif">4배 재생</a> · <a href="{folder}/decoded/00.png">완성 GIF에서 꺼낸 첫 장</a> · <a href="{folder}/holes.log">구멍 검사 기록</a></p></section>'
h=['''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Codex 전사 동작 프레임 시험</title><style>
body{font:16px/1.6 system-ui,sans-serif;margin:0;background:#111823;color:#e6edf5}main{max-width:1600px;margin:auto;padding:28px}h1{font-size:30px}h2{font-size:23px}section{background:#1b2636;border:1px solid #36465c;border-radius:12px;padding:20px;margin:24px 0}a{color:#9fd2ff}figure{margin:0;min-width:0}img{max-width:100%;height:auto;image-rendering:pixelated;background:#ddd}.three{display:grid;grid-template-columns:1fr 1.3fr 1fr;gap:18px}.row{max-height:430px;object-fit:contain;object-position:top}.fail{color:#ffb4b4;font-size:15px;border:1px solid #a35252;padding:3px 8px;border-radius:4px}table{border-collapse:collapse;width:100%;font-size:14px}th,td{padding:8px;border:1px solid #45546b;text-align:left;vertical-align:top}th{background:#26384e}.scroll{overflow:auto}.notice{background:#3a2f20;padding:16px;border-left:4px solid #ffca78}small{color:#b7c4d7}summary{cursor:pointer}@media(max-width:1000px){.three{grid-template-columns:1fr}main{padding:14px}}
</style><main><h1>Codex로 전사 걷기·공격을 뽑아 본 결과</h1><p class="notice">제작자 판정: <b>세 방법 모두 최종 사용 기준에는 못 미쳤습니다.</b> 다리 교대와 공격 자세는 만들 수 있었습니다. 소량 시안과 손질 출발점으로는 B가 낫지만, 40클립을 이대로 만들기에는 품질이 불안정합니다. 독립 판독·사용자 채택은 아직 하지 않았습니다.</p><p>게임에 넣지 않은 별도 시험입니다. 내장 이미지 생성 17회, Aether·Tripo 0회. 아래의 빨간 띠는 검증·채택되지 않은 시험이라는 표시이며 실패본도 그대로 실었습니다. 예전 그림은 동작 박자만 비교합니다.</p>''']
h.extend([trio('walk_right','B'),trio('walk_down','A'),trio('attack_right','B')])
h.append('<section><h2>같은 오른쪽 걷기 · A/B/C 비교</h2><div class="three">')
for method,folder in [('A','A/walk_right/retry'),('B','B/walk_right'),('C','C/walk_right')]:
 h.append(f'<figure><h3>방법 {method}</h3>{image(folder+"/anim_8x.gif","방법 "+method+" 오른쪽 걷기")}<figcaption>{verdicts[folder][1]}</figcaption></figure>')
h.append('</div><details><summary>첫 시도의 실패도 보기</summary><div class="three">'+image('A/walk_right/anim_8x.gif','A 첫 시도: 같은 다리 반복')+image('B/walk_right/attempt1/anim_8x.gif','B 첫 시도: 같은 다리 반복')+image('C/walk_right/attempt1/anim_8x.gif','C 첫 시도: 다리 이음 문제')+'</div><p>A·B 첫 묶음은 1·3장에서 같은 다리가 앞이었습니다. A는 지시를 강화했고, B는 색 막대에 실제 픽셀 자세 러프를 더해 3장째만 다시 생성했습니다. C는 조각 연결과 색 구분을 바꿨습니다.</p></details></section>')
h.append('''<section><h2>방법별 비용과 남은 문제</h2><p>시간은 이 시험의 병행 실행 구간입니다. 준비·손질·판독 시간이 겹쳐 서로 더하면 전체 시간이 되지 않습니다. 세부 시각은 기록 파일을 따릅니다.</p><div class="scroll"><table><tr><th>방법</th><th>걸린 구간</th><th>생성</th><th>판정</th><th>남은 문제</th></tr><tr><td>A · 한 장에 프레임 줄</td><td>약 3분 생성 + 약 4분 공통 손질·확인(겹침)</td><td>4회 / 3동작<br>오른쪽 걷기 2회</td><td>FAIL</td><td>가장 빠름. 팔레트를 고정해도 갑옷·장비 무늬가 바뀜.</td></tr><tr><td>B · 한 장씩 기준 그림 고치기</td><td>약 7분 생성 + 약 4분 손질·확인(겹침)</td><td>13회 / 3동작<br>오른쪽 걷기 4+1회</td><td>FAIL</td><td>머리·몸통 고정은 쉬움. 자세 러프가 있어도 다리가 안 바뀌는 실패와 중간 자세·무기 변화가 남음.</td></tr><tr><td>C · 원본 조각 이동</td><td>약 4분 제작·수정·확인(겹침)</td><td>0회 / 3동작<br>조립 2회</td><td>FAIL</td><td>색·물건은 일정함. 무릎이 굽지 않고 공격이 90도씩 끊김. 구멍은 2차 손질 후 PASS.</td></tr></table></div><p><b>추천:</b> 조금 더 손질할 자세 시안은 B. 빠르게 동작 모양만 보는 데는 A. C는 작은 발 움직임에 한정하는 편이 낫습니다. 이번에는 Aether를 시험하지 않았으므로 Aether와의 품질·시간 우열은 알 수 없습니다.</p></section>''')
h.append('<section><h2>어느 다리가 앞인가 · 제작자가 GIF를 풀어 순서대로 본 기록</h2><table><tr><th>방법·동작</th><th>1장</th><th>2장</th><th>3장</th><th>4장</th></tr>')
for clip,v in legs.items():h.append('<tr><td>'+e(clip)+'</td>'+''.join('<td>'+e(x)+'</td>' for x in v)+'</tr>')
h.append('</table><p>앞다리는 접촉 자세 1·3장으로, 중간 장은 접지·통과 상태로 구분했습니다. B 오른쪽의 4장째는 앞뒤 구분이 명확하지 않아 통과로 적지 않았습니다.</p></section>')
h.append('<section><h2>프레임 간 크기와 색</h2><p>원본 PNG 36장은 모두 72×72, 키 56, 바닥선 64, 반투명 0, 기준 팔레트 밖 색 0입니다. 머리의 원본 불투명 칸은 모두 그대로입니다. 아래는 칸 단위 실측이며, 몸통은 [30,31,41,46] 비교 창, 방패는 빨간 면, 칼은 밝은 날의 범위입니다. 물건 전체 크기·자연스러움을 대신하는 검사가 아닙니다. 특히 공격은 팔이 몸통 앞을 지나갑니다.</p><details><summary>36장 실측 표 펼치기</summary><div class="scroll"><table><tr><th>묶음·장</th><th>머리</th><th>몸통 창 폭×높이 / 변한 칸</th><th>방패 빨간 면</th><th>칼 밝은 날</th><th>바닥 부근 중심 x</th></tr>')
for clip,rows in measure.items():
 for r in rows:
  f=lambda v:'없음' if v is None else '×'.join(map(str,v))
  h.append(f'<tr><td>{e(clip)} · {r["frame"]}</td><td>{f(r["head_size"])}</td><td>{f(r["torso_window_bbox_size"])} / {r["torso_window_changed_pixels"]}</td><td>{f(r["shield_red_face_bbox_size"])}</td><td>{f(r["sword_bright_blade_bbox_size"])}</td><td>{r["bottom_band_center_x"]}</td></tr>')
h.append('</table></div></details><p>바닥 부근 중심은 y61~63의 불투명 칸 범위입니다. 반 칸 값은 정수 격자에서 폭이 짝수인 경우이며, C 공격 3장째는 내려간 칼이 발 범위와 섞여 38.5로 잡힙니다. 발 뿌리 위치와 혼동하지 않았습니다. 생성 격자 추정은 LOW·GRID_MISMATCH가 있어 원본·기록을 남겼으며 정규 생산 PASS로 승격하지 않았습니다. pixelcheck.py가 없어 motion 검사와 dot_retouch.py는 사용할 수 없었습니다.</p></section>')
h.append('''<section><h2>40클립으로 늘리면 · 추정</h2><p>대원 4명 × 5방향 × 걷기·공격 = 40클립. 이번 세 동작만 바탕으로 한 계산이며 품질 통과를 보장하지 않습니다. 자동 생성보다 프레임별 확인·손질이 더 큰 변수가 됩니다.</p><table><tr><th>방법</th><th>생성 횟수 추정</th><th>생성·손질·확인 시간 추정</th><th>계산 근거</th></tr><tr><td>A</td><td>약 54회 (최소 40회)</td><td>약 7~13시간</td><td>이번 4÷3×40을 올림. 클립당 손질·확인 포함 10~20분 가정.</td></tr><tr><td>B</td><td>약 174회 (4장씩이면 최소 160회)</td><td>약 10~20시간</td><td>이번 13÷3×40을 올림. 클립당 손질·확인 포함 15~30분 가정.</td></tr><tr><td>C</td><td>0회</td><td>약 7~17시간</td><td>방향·인물별 부위 나누기와 조립·확인에 클립당 10~25분 가정. 자연스러움을 올리려면 더 걸림.</td></tr></table><p>범위는 병행 생성으로 줄어드는 대기와 수작업으로 늘어나는 시간을 함께 고려한 계획용 추정입니다. Aether 크레딧이나 비용으로 환산하지 않았습니다.</p></section><details><summary>각 방법의 나머지 동작도 보기</summary>''')
for method in 'ABC':
 for clip in names:
  if (method,clip) in [('B','walk_right'),('A','walk_down'),('B','attack_right')]:continue
  h.append(trio(clip,method))
h.append('</details><p><a href="frame-measurements.json">실측·파일 지문</a> · <a href="generation-records.json">첫 생성 지시문</a> · <a href="B/more-generation-records.json">추가 생성 지시문</a> · <a href="producer-observations.json">제작자 관찰</a></p><small>독립 리뷰 대기. 사용자 채택·Unity 적용·컴파일·Play는 이 시험 범위 밖입니다.</small></main></html>')
page=''.join(h)
if review_path.exists():
 page=page.replace('제작자 판정: <b>세 방법 모두 최종 사용 기준에는 못 미쳤습니다.</b>','Claude 1차 독립 판독: <b>A 정면 걷기·B 오른쪽 걷기·B 공격은 PASS입니다.</b>')
 page=page.replace('독립 판독·사용자 채택은 아직 하지 않았습니다.','나머지 여섯 묶음은 수정 후 재판독 대기입니다. 사용자 채택은 아직 하지 않았습니다.')
 page=page.replace('B 오른쪽의 4장째는 앞뒤 구분이 명확하지 않아 통과로 적지 않았습니다.','B 오른쪽의 4장째는 Claude가 뒤꿈치가 붙어 있는 통과 자세로 읽어 PASS했습니다.')
 page=page.replace('독립 리뷰 대기. 사용자 채택','여섯 수정본 재판독 대기. PASS 세 묶음은 변경 없이 유지. 사용자 채택')
 page=page.replace('<h2>방법별 비용과 남은 문제</h2>','<h2>방법별 비용과 남은 문제 · 아래 비용 표는 최초 시험 기록</h2><p><a href="review-r1/review-status.json">리뷰 원문 판정과 수정 항목</a> · <a href="review-r1/recheck-brief.md">수정 확인 브리핑</a></p><p>최초 생성 17회에서 추가 생성은 없습니다. A 정면·B 오른쪽·B 공격은 독립 판독 PASS, 나머지는 픽셀 손질 뒤 재판독 대기입니다. 최초 실패본은 review-r1/before/에 보존했습니다.</p>')
if 'review-r2' in str(review_path) or 'review-final' in str(review_path):
 page=page.replace('Claude 1차 독립 판독: <b>A 정면 걷기·B 오른쪽 걷기·B 공격은 PASS입니다.</b>','Claude 수정 확인: <b>아홉 묶음 중 일곱 묶음은 PASS입니다.</b>')
 page=page.replace('나머지 여섯 묶음은 수정 후 재판독 대기입니다.','C 오른쪽 걷기·공격은 방식을 바꾼 C2 수정본으로 재판독 대기입니다.')
 page=page.replace('여섯 수정본 재판독 대기. PASS 세 묶음은 변경 없이 유지.','C2 두 수정본 재판독 대기. PASS 일곱 묶음은 변경 없이 유지.')
 page=page.replace('최초 생성 17회에서 추가 생성은 없습니다.','최초 생성 17회에서 추가 생성은 없습니다. C2는 단순 조각 이동에 더해 앞뒤 겹침·반대 다리 명암·국소 팔 그리기를 사용합니다. 초기 C 방식의 성공으로 바꿔 적지 않습니다. <a href="review-r2/recheck-brief.md">C2 수정 확인 브리핑</a>.')
 page=page.replace('<h3>방법 C</h3>','<h3>방법 C2 · 손질 방식 변경</h3>')
 page=page.replace('키 56, 바닥선 64','키 56~57(허용 55~57), 바닥선 64')
 page=page.replace('C 공격 3장째는 내려간 칼이 발 범위와 섞여 38.5로 잡힙니다.',f'C2 공격 3장째는 내려간 칼이 발 범위와 섞여 {measure["C/attack_right"][2]["bottom_band_center_x"]}로 잡힙니다.')
if review_path.exists() and review.get('finished'):
 page=page.replace('Claude 수정 확인: <b>아홉 묶음 중 일곱 묶음은 PASS입니다.</b>','최종 동작 판독: <b>아홉 묶음 모두 PASS입니다.</b>')
 page=page.replace('C 오른쪽 걷기·공격은 방식을 바꾼 C2 수정본으로 재판독 대기입니다.','C2 두 묶음도 C4·C5 재판독을 통과했습니다. 최신 두 판독은 기준 그림을 따로 열지 않아 원본 부위 대조까지 통과했다는 뜻은 아닙니다.')
 page=page.replace('C2 두 수정본 재판독 대기. PASS 일곱 묶음은 변경 없이 유지.','독립 동작 판독 마감. 그림은 마지막 수정 커밋 그대로 유지.')
 page=page.replace('A 정면·B 오른쪽·B 공격은 독립 판독 PASS, 나머지는 픽셀 손질 뒤 재판독 대기입니다.','생성 그대로가 아니라 원본 픽셀 고정·손질 뒤 아홉 묶음의 동작 판독이 PASS했습니다.')
 page=page.replace('아래의 빨간 띠는 검증·채택되지 않은 시험이라는 표시이며 실패본도 그대로 실었습니다.','빨간 띠는 정규 identity 측정·사용자 채택을 거치지 않은 시험이라는 표시입니다. 별도로 받은 동작 판독 PASS와 구분하며 실패본도 보존했습니다.')
 page=page.replace('<tr><td>C</td><td>0회</td><td>약 7~17시간</td><td>방향·인물별 부위 나누기와 조립·확인에 클립당 10~25분 가정. 자연스러움을 올리려면 더 걸림.</td></tr>','<tr><td>C2 · 국소 그림 손질 포함</td><td>0회</td><td>약 13~27시간</td><td>반대 다리·가려진 팔과 방향별 손질·확인까지 클립당20~40분 가정. 실제 40클립 생산 속도를 잰 값이 아님.</td></tr>')
 page=page.replace('</main></html>','<section><h2>최종 판정의 범위</h2><p>최신 Claude C4·C5는 동작과 프레임끼리의 일관성을 PASS했습니다. 기준 그림 별도 대조는 하지 않았고, C3 수치와 C6 페이지는 리뷰 범위 밖으로 미평가입니다. 제작자의 수치·경로·GIF 확인과 구분합니다. 정규 measure/jitter는 미실행, pixelcheck는 부재로 실행 불가이며 사용자 채택·게임 적용은 하지 않았습니다.</p><p><a href="review-final/review-status.json">판정 원문·범위·후속 의견</a> · <a href="../../../.director/outbox/20261122-dq-codex-sprites-1.md">최종 핸드오프</a></p></section></main></html>')
(P/'preview.html').write_text(page,encoding='utf-8')
print('preview.html written')
