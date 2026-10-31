"""Preparation diagrams only; no Unity scene or map harness implementation."""
from pathlib import Path
import json,csv,base64,hashlib,html,sys
root=Path(__file__).resolve().parents[4]; d=root/'docs/design'; out=Path(__file__).parent
p=Path(sys.argv[1]) if len(sys.argv)>1 else d/'map-meadow-007-r001-layout.json'
if len(sys.argv)>2: out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
a=json.loads(p.read_text()); digest=hashlib.sha256(p.read_bytes()).hexdigest()
img=base64.b64encode((root/'docs/art/maps/references/VerdantMeadowsFullMap.png').read_bytes()).decode()
old={r['object_id']:r for r in csv.DictReader((d/'map-meadow-005-r001-objects.csv').open(encoding='utf-8-sig'))}
S=1728/1195; X=48; Y=98
pts=lambda ps:' '.join(f'{x},{y}' for x,y in ps)
lines=lambda text:''.join(f'<text x="1848" y="{y}" font-size="24">{html.escape(t)}</text>' for y,t in text)
for mode,title in [('structure','구조 관찰'),('collision','충돌 후보'),('tests','시험 경로'),('changes','변경 대조')]:
 svg=[f'<svg xmlns="http://www.w3.org/2000/svg" width="2400" height="1800" viewBox="0 0 2400 1800"><metadata>layout_sha256={digest}; source_sha256={a["source_sha256"]}; MAP-MEADOW-007/r001; NOT_RUN</metadata>', '<rect width="2400" height="1800" fill="#F5F1E7"/>','<g font-family="Apple SD Gothic Neo, Arial, sans-serif" fill="#203248">', '<rect x="48" y="48" width="1728" height="1704" fill="none" stroke="#203248" stroke-width="3"/>',f'<g transform="translate({X},{Y}) scale({S})">']
 opacity={'structure':1,'collision':.20,'tests':.42,'changes':.17}[mode]
 svg.append(f'<image width="1195" height="1110" opacity="{opacity}" href="data:image/png;base64,{img}"/>')
 if mode!='structure':
  svg.append(f'<polygon points="{pts(a["boundary"]["points_px"])}" fill="none" stroke="#203248" stroke-width="{3/S}" stroke-dasharray="7 4"/>')
 for o in a['objects']:
  if not o['V']:continue
  ps=o['V']['points_px']; id=o['id']; r=old[id]
  if mode=='changes':
   svg.append(f'<rect x="{r["x_px"]}" y="{r["y_px"]}" width="{r["w_px"]}" height="{r["h_px"]}" fill="none" stroke="#6687a6" stroke-width="{2/S}" stroke-dasharray="3 3"/>')
  if mode=='changes':
   svg.append(f'<polygon id="V-{id}" points="{pts(ps)}" fill="none" stroke="#203248" stroke-width="{2/S}" stroke-dasharray="5 3"/>')
  if mode in ('collision','tests'):
   if o['U']:
    svg.append(f'<polygon id="U-{id}" points="{pts(o["U"]["points_px"])}" fill="#D89524" fill-opacity=".12" stroke="#997636" stroke-width="{1/S}" stroke-dasharray="1 4"/>')
   if o['C']:
    svg.append(f'<polygon id="C-{id}" points="{pts(o["C"]["points_px"])}" fill="#777B78" fill-opacity=".3" stroke="#203248" stroke-width="{2/S}" stroke-dasharray="5 3"/>')
  if mode!='tests':
   cx=sum(t[0] for t in ps)/len(ps);cy=sum(t[1] for t in ps)/len(ps)
   svg.append(f'<text x="{cx}" y="{cy+5}" text-anchor="middle" font-size="{24/S}" fill="#203248" stroke="#F5F1E7" stroke-width="3" paint-order="stroke">{id[-3:]}</text>')
 if mode=='tests':
  for link in a.get('connectors',[]):
   svg.append(f'<polyline points="{pts(link["points_px"])}" fill="none" stroke="#527460" stroke-width="{2/S}" stroke-dasharray="2 4"/>')
 if mode in ('tests','changes'):
  for route in a['routes']:
   if mode=='changes' and route['id']!='RT-018':continue
   ps=route['points_px'];dash='3 4' if route['role']=='diagnostic' else '8 4'
   svg.append(f'<polyline points="{pts(ps)}" fill="none" stroke="#b76705" stroke-width="{3/S}" stroke-dasharray="{dash}"/>')
   for px,py in ps:svg.append(f'<circle cx="{px}" cy="{py}" r="2.4" fill="#b76705"/>')
   tx,ty=ps[len(ps)//2];svg.append(f'<text x="{tx+6}" y="{ty-8}" font-size="{24/S}" fill="#203248" stroke="#F5F1E7" stroke-width="3" paint-order="stroke">{route["id"]}</text>')
  svg.append('<circle cx="565" cy="580" r="7" fill="none" stroke="#203248" stroke-width="2"/><text x="578" y="598" font-size="18">P01</text>')
 svg.append('</g>')
 svg.append(f'<text x="1848" y="100" font-size="48">{title}</text>')
 content=[(158,'MAP-MEADOW-007'),(194,'r001 · 준비 도면'),(230,'2026-10-30'),(282,'원본 픽셀 기준'),(318,'1195 × 1110'),(354,'비례 개념도 · 축척 미정'),(406,'원본 위 = 도면 위'),(442,'실제 방위 미확인'),(494,'REF-VM-001'),(530,'VerdantMeadowsFullMap'),(566,'출처: Spell Brigade Wiki'),(618,'실제 충돌 / 실행: 미검증'),(654,'숫자 = OB-번호'),(690,'64군집 + 외곽 후보'),(726,'부모 OB-G01: 비충돌')]
 extra={
 'structure':['E01 원본 자체를 표시','배치를 새로 생성하지 않음','숫자는 등록 군집 위치','V: 거친 관찰 외피','접지면 / 실제 크기 미상'],
 'collision':['점선: C 시험 외피 후보','황색 점선/채움: 불확실 U','C/V는 각각의 좌표 사용','원작 충돌 복원 결과 아님','외곽 접합 / 내부 틈 미검증','원형 내부는 채우지 않음'],
 'tests':['황색: RT 후보 경유점','짧은 점선: 추가 진단 통로','P01: 좌표 원점 / 시작 후보','RT006 → 018 → 역008','연속 진입 / 내부 / 이탈','주황 선은 통과 증거 아님','녹색 점선: 연결 후보 LC'],
 'changes':['CH-007-02','청색 상자: 005 검색 범위','남색 윤곽: 007 관찰 후보','상자를 충돌로 전용 안 함','CH-007-03','황색 RT018: 006에서 연결','기존 군집 이동 / 삭제 없음']}
 content += [(812+i*38,t) for i,t in enumerate(extra[mode])]
 content += [(1240,'전 판 동일 배치 / 변환'),(1280,'layout SHA-256'),(1320,digest[:24]),(1360,digest[24:48]),(1400,digest[48:]),(1480,'도면 일관성과'),(1518,'충돌 타당성은 별도 판정'),(1620,'C 타당성: BLOCKED'),(1660,'Unity 실행: NOT_RUN')]
 svg.append(lines(content));svg.append('</g></svg>')
 (out/f'map-meadow-007-r001-{mode}.svg').write_text(''.join(svg))
print('wrote four SVG sheets',digest)
