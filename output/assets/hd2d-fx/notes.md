# F0 r003 보정 그림 — 판독 대기

원본 기록은 notes-original.md. 원본 입력은 메인 폴더 절대 경로에서 읽기만 했다. 현재 게임에는 아직 넣지 않았다.

- 68 PNG 중 25 PNG 보정(7종). 그림 SHA256·크기·불투명 상자·알파는 review/asset-manifest.json, 입력 지문은 review/input-sha256.json.
- PPU 48. 모든 알파 0/255. 세운 그림 가운데 피벗. 바닥 판 local y=2 예정. 돌진 피벗 (0.07647058823529412, 0.16883116883116883).
- heal 5×20fps: #7fa040 #a8c828 #ffe08a #ffffff, 3단 띠·잎·십자.
- pulse 5×20fps: #7a4a9e #b07fd0 #ffffff #7fd0c8, 겹 고리.
- boom 4×16fps: #77211d #c44334 #e4b67e #774c3b, 톱니·점선·그을음.
- blast 2×8fps: #77211d #c44334 #e4b67e, 톱니·점선·디더 채움.
- dash 3×12fps: #77211d #c44334 #e4b67e, 갈매기 길·화살촉.
- chain 3×12fps: #ffffff #b07fd0 #7fd0c8, 130×48 예시(110×28 벡터), 2도트 심·가지·끝 불꽃. 좌표 피벗은 시작점 (10/130, 10/48). RNG xorshift32 (seed 0은 1). numpy PCG64 원본과 달라진 새 비교 기준 후보.
- hit_star 3×20fps: #ffffff #fff9ed #ffe08a #f8c838, 20×20 캔버스·가운데 피벗, 정점 실제 16×16(48/3), 시작 8×8.
- 나머지 프레임·그림자 점·방향 그림은 원본 바이트 그대로.
- 제작: 생성 호출 0 / 유료 0. 데이터 크기로 그리는 래스터 함수 보정 + 피격 플래시 순수 래스터. 도구·명령·현재 관문은 docs/features/hd2d-fx/f0-art-review.md.
- 독립 판독: NOT_RUN, Claude에 F0-A1~A6 요청. 미리보기는 review/index.html, 시트와 APNG는 킷 sheet.py만 사용.
