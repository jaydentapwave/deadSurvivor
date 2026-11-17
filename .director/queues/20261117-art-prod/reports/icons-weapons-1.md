# icons-weapons 시도 1 보고

## 한 일
- 내장 image_gen으로 무기·발동 아이콘 axe·wand·censer·crossbow 4개를 만들고, pixelize로 기준서 r5 팔레트와 칸 모양에 맞춰 정리했다. 각 아이콘 폴더에 PNG와 생성 입력·프롬프트·해시·선정 근거를 담은 source.txt가 있다.
- 도끼·완드·쇠뇌는 각각 1회차 4장, 향로는 2회차 8장이다. 향로 첫 회차의 사선 사슬이 없는 칸을 침범해 수직 사슬 안내로 다시 생성했다. 채택 후보 4개와 버린 후보 16개를 비교 시트에 담았다. 구현 커밋: `fcaed34b45968f2b59b16dbc91cb953f38fa6539`.
- Claude 독립 이름 가림 판독 PASS를 받았다. 사용자 최종 채택과 게임 넣기는 남아 있다.

## 완료 조건별 결과
- **IW1 — 아이콘 4개 규격·투명 칸·번짐: 확인함.** axe·censer 32×48, wand 48×16, crossbow 48×32. shape 밖 불투명 픽셀 0, 알파 0/255, 팔레트 밖 색 0. 최종 4개 fringe 모두 PASS.
- **IW2 — 채택본·버린 후보 비교 시트: 확인함.** `docs/art/sheets/items-weapons.png`는 20칸(#1~4 채택 후보, #5~20 버림), 48×48 비교 캔버스 기준 144px로 게임 크기 3배다. sheet.py 출력 20 shown, 0 left out이며 완성 시트를 직접 열어 확인했다.
- **IW3 — 식별·칸 채움·같은 그림체: 확인함(주의 의견 포함 PASS).** Claude가 중립 이름 시트만 보고 4/4 식별했고 칸마다 내용과 2~3단 음영을 확인했다. 도끼 판독은 낮은 신뢰도다.
- **IW4 — 사용자 최종 채택: 못 함.** 지시대로 나중에 모아서 사람이 판정한다. 리뷰 PASS를 사용자 채택으로 보지 않는다.

## 실행한 검사와 결과
- 사전 `project-doctor.py --root . --host codex`: configured, findings 없음. `approve.py verify --spec docs/art/아트-기준서.md`: OK.
- 생성 후보 pixelize 격자 확인: 약 16px 블록 유지. 최종 후보의 내용 bbox 기준 axe 31×47(block 16.0), wand 46×14(block 15.9), censer 29×47(block 16.0), crossbow 47×31(block 16.0). bbox 격자 수는 캔버스 여백 때문에 전체 규격보다 작다.
- `/usr/bin/python3 .ai/2d-art/tools/matte.py fringe --key '#ff00ff' Assets/Game/03.Resources/Art/AIGenerated/Items/<id>/<id>.png`: 지시된 4명령 모두 exit 0, spill/edge/stray 0. 추가 --whole 검사도 최종 4개와 후보 20개 PASS.
- 캔버스·shape 마스크·알파·팔레트 포함 검사: 4개 PASS. 시각 파일은 리뷰 이후 변경하지 않아 기존 검사 증거를 유지했다. 실행 시각·SHA256·명령 근거는 `docs/art/sheets/items-weapons-checks.json`에 있다(제작 기록 UTC 2026-11-17 08:29:16).
- Claude general-purpose(opus, medium), 독립 name-blind part-check: IW1/IW2/IW3 PASS. `.claude/agents/`의 4종은 Codex 중계이므로 다른 계열 판독에 쓰지 않았다는 리뷰 세션 설명을 전달받았다. 판독자는 fringe를 재실행하지 않았다. 사용자 전달 판정 원문을 evidence로 기록한다.

VERDICT: PASS

- `measure.py compare`: NOT_RUN — 비교 identity 없는 새 종류이며 지시로 사용하지 않음. paint.py·shape.py fit: NOT_RUN — 팔레트 포함·칸 마스크와 채움 실측을 기록했다.
- Unity 임포트·데이터 icon 연결·장면 적용·컴파일·짧은 Play: NOT_RUN — 에디터 없는 worktree의 그림 생산 항목이며 디렉터가 2d-art-produce 7~9절을 범위에서 제외했다. 게임에 들어간 결과라고 보고하지 않는다.
- 최종 문서 변경: 경로·판정·원본 지문 일관성을 확인했다. 이번 마감에 그림 생성·변형·추가 리뷰는 하지 않았다.

## 남긴 것
- 비필수 리뷰 의견(사용자 결정 전 수정하지 않음): 도끼 날이 좁고 자루가 날 아래로 나와 괭이·하키채로 읽힐 수 있음; 쇠뇌 활대 끝과 시위의 외곽선이 약함(외곽선 비율 0.82, 채택 견본 0.96~1.00); 도끼·쇠뇌·완드 나무색이 시작 견본보다 차갑고 탁함; 향로 통풍구 2개가 눈으로 읽힐 수 있음. 필수 수정은 없었다.
- 다음 실행 몫: 아이템 데이터 icon 연결, Unity 임포트와 장면 적용, 게임 화면 확인, 사용자 최종 채택. 생성·검사로 게임 연결 완료를 대신하지 않는다.
- 놓친 것과 원인: 향로 안내 그림의 사선 사슬 자체가 L자 빈칸을 침범했다. 2회차에서 수직 사슬로 바꾸고 최종 칸 경계 외곽선 2px를 정리했다. 워크플로우 갱신 후보는 ‘생성 전에 안내 그림도 shape 밖 불투명 0 확인’. 워크플로우 문서는 허용 범위 밖이라 **미반영**, 디렉터 후속으로 남긴다. 리뷰에서 드러난 도끼·쇠뇌 문제는 자기 판독만으로 충분히 가르지 못한 것으로 기록한다. 기존 독립 판독을 유지하며 새 검사는 추가하지 않는다.
- 시간·토큰: 계측 가능한 안내 그림 생성부터 제작 증거 기록까지 363초(08:23:13~08:29:16 UTC)이며 앞선 규칙 읽기는 미계측이라 전체 작업 시간으로 쓰지 않는다. `Tools/Harness/session_time.py --markdown`은 로그 파일이 없어 ValueError로 실패했다. 전체 시간·단계별 시간·모델 토큰 사용량은 확보하지 못했으며 추정하지 않는다. 효율 개선 후보는 다음 실행부터 시작·생성·정리·검증 시각과 사용 가능한 토큰 계수를 남기는 것이다. 임시 조립 스크립트 select.py가 표준 모듈을 가린 오류는 assemble_candidates.py로 이름을 바꿔 해결했다.
