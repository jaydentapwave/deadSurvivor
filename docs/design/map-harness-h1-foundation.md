# MAP-HARNESS-010/r001 — H1 첫 구현

2026-10-31 KST · H1 기초 코드 구현·시험·독립 검토 PASS · 게임 적용 미구현

## 브리핑

- 규모 L2. 009에서 설계한 Python 독립 기하 판정과 입력 경계의 첫 실제 구현이다. [009 설계](map-harness-implementation.md), [데이터 계약](map-harness-data-contract.md), [분석 정답](map-harness-fixtures.json)을 따른다.
- 사용자 결정: 외부 3D 생성은 웹 반복 조작 대신 CLI/API를 기본으로 하고 구현 단계로 이동한다. API 잔액 재조회는 0이다. 추가 결제나 새 공개 생성 승인으로 확대하지 않는다.
- 이번 완료 범위: 엄격한 JSON 읽기/안전한 상대 경로, 단순 polygon 검사·CCW 정규화, 원형 몸체의 전체 선분 이동에 대한 독립 PASS/FAIL/BLOCKED 판정, 명령행 분석 시험과 반례 테스트.
- 제외: Unity 이동 해결기/입력 드라이버/상태 전환, 전체 request/manifest/원자료 인수 및 FI 결과 집계, `run.py map` 연결, 최종 3D 맵과 Tripo 유료 생성. H1 전체나 G2 완료로 보고하지 않는다.
- 상태 소유: 게임은 기존 DSGame에 유지. 새 Python 모듈은 전달된 도형·이동 선분만 읽으며 게임 좌표/HP/RNG를 변경하지 않는다. 007 후보나 기존 009 예제의 NOT_RUN을 덮어쓰지 않는다.
- 변경 파일: `Tools/Verify/map_io.py`, `map_oracle.py`, `map_check.py`, `test_map_foundation.py`; `run.py`의 local/all에 명시적인 `map_oracle_foundation` 분석 시험 항목 추가; 실행/검증 문서와 결과 기록. 기존 게임/동기화/gate 기준은 변경하지 않는다. 기존 hook이 실행하는 local에서 새 순수 검사도 빠지지 않게 하며 실제 map 실행과는 분리한다.
- 재사용: 표준 라이브러리만 사용하며 기존 gate·회귀를 유지한다. C# 게임 검사기를 Python으로 복사하지 않는다.
- 실패/복구: 잘못된 자료는 예외를 숨기지 않고 FAIL/BLOCKED로 출력한다. 빈 시험 집합·누락/중복 ID·비유한 값·도형 오류는 PASS 불가. 외부 쓰기·Editor 조작이 없는 순수 검사로 복구 자원이 없다.
- 검증: 009의 분석 벡터, 양 끝이 안전한 얇은 벽 관통, 오목 경계 이탈 후 재진입, 꼭짓점 접촉·평행/영길이 선분·회전/반전·좁은 통로·반경 침투·잘못된 입력. 독립 design-reviewer와 algorithm-tester 검토 → 구현 → 실행 → validator-client → gate.
- I0: 이번 시각의 로컬/Unity 코드·데이터 지문과 버전·패키지를 읽기 전용 재확인한다. 라이브 씬 확인은 실제 Unity 시험 직전에 한다. 소스는 UnityMigration을 원본으로 유지하고 대상 차이가 있으면 중단한다.

## 판정 구현 기준

1. JSON은 UTF-8이며 중복 키, NaN/Infinity와 파싱 후 비유한 수(예: 1e999)를 거부한다. bool을 숫자로 받지 않는다. 파일 입력 경로의 심볼릭 링크는 상위 경로까지 거부한다. 상대 증거 경로는 절대경로/`..`/심볼릭 링크/출력 root 이탈을 거부한다.
2. polygon은 3개 이상 유한 점, 반복점/영길이 edge/자기교차/겹친 edge/0면적을 거부한다. hole/multiple boundary는 미지원 BLOCKED. CCW로 정규화하고 입력/정규화된 JSON의 SHA256을 따로 반환한다. 겹치는 장애물은 합치지 않는다.
3. 수치 범위: 기존 경계 밖 벡터 x=500.1도 지원하도록 도형/실행점 좌표 절댓값≤1024로 제한한다. radius와 epsilon은 유한한 양수이며 [1e-12,1024] 밖은 BLOCKED. 0이 아닌 edge/이동 선분 길이가 1e-12 미만이면 BLOCKED(정확한 길이0 이동은 허용). 입력 오류(0/음수/비유한)와 지원 범위 부족을 구분한다. 범위를 G3로 몰래 일반화하지 않는다.
   - 위상 predicate, 교차 t의 정렬·중복제거·구간 midpoint, 제곱 최소거리는 입력 double을 정확히 표현한 Fraction으로 계산한다. 중간 교차점을 float로 바꾸지 않는다. 009의 Python double 판정안은 이 첫 구현에서 보수적인 유리수 보조 계산으로 구체화한다.
   - 제곱거리 q≥0를 float로 바꾼 값을 한 ULP 아래/위로 확장해 q의 구간을 얻고, 양 끝 sqrt를 한 ULP 아래/위로 확장한다. 두 끝의 제곱이 정확한 q를 감싸는지 Fraction으로 재확인한다. 실패하면 추가로 제한된 ULP 확장 후에도 증명 못 할 경우 BLOCKED. 따라서 underflow가 있어도 0을 정확한 양의 거리로 취급하지 않는다.
4. 선분과 모든 polygon edge의 교차 매개변수를 구하고 선분을 분할한다. 각 열린 구간 중점과 양 끝을 분류하여 장애물 내부/경계 외부의 witness가 있으면 signed gap=d-r로 침투를 증명한다. 이 경우 `clearance_kind=witness_bound`; 이를 전 선분 최소 여유라고 부르지 않는다.
5. 전 구간이 자유 지면이면 모든 edge와 선분의 최소거리에서 반경을 빼 `minimum_clearance`를 계산한다. d와 g를 분리한다. 위 거리 구간에서 반경을 정확히 빼 얻은 여유 구간 전체가 -epsilon보다 작아야 FAIL, +epsilon보다 커야 PASS이며 나머지는 BLOCKED다. 거리 경계 증명과 epsilon 비교도 Fraction으로 수행한다. 출력 소수값은 편의 표시이며 하한/상한·판정 상태를 함께 남긴다. 이 구간은 도형의 통과 폭을 늘리는 용도로 사용하지 않는다.
6. 여러 장애물/경계에서 FAIL이 하나라도 증명되면 FAIL, 불확실이 남으면 BLOCKED, 모두 안전할 때만 PASS다. 장애물/경계가 없는 무한 지면으로 자료 누락을 해석하지 않는다.

## 실행 인터페이스와 산출물

이번 공개 명령은 `python3 Tools/Verify/map_check.py oracle-selftest` 하나다. 009 분석 자료에서 직접 입력을 읽고 알려진 기대값을 확인한다. NORMAL-WALL의 solver 끝점을 추정하지 않으며 별도 이름의 분석 probe를 사용한다. `output/verification/<새 실행ID>/map/`에 입력·모듈 지문, 각 실제 관찰 상태/기대 상태, 검사 범위, 실행 시각을 기록한다. 기존 실행 폴더를 재사용하지 않는다. 출력은 짧은 JSON(status/report/scope/unity_status)이며 오류 상세는 파일로 남긴다. PASS=0/FAIL=1/BLOCKED·NOT_RUN=2다. `PASS`는 **분석 oracle 시험**의 성공이고 Unity/G2 상태는 NOT_RUN이다.

전체 실행 request를 받아들이는 `prepare/run/judge`는 H1 후속에서 별도로 구현한다. 이를 지금 지원하는 것처럼 보이는 스텁이나 무조건 성공 명령은 만들지 않는다.

## 유지·변경과 남은 조건

- CH010-01: 009의 미구현 기하 판정 중 제한된 분석 fixture 범위를 실제 코드로 만든다. RF/OB/RT 배치와 도면 네 판은 변경하지 않아 새 그림은 해당 없음.
- 입력 단위는 logical_fixture이며 s/k/최종몸체/원본 접지면은 해당 없음. 009 원본과 새 모듈의 실제 지문은 실행 보고서가 소유한다.
- 다음: H1 전체 데이터 인수·원자료/결과 집계·FI04/05/06/09 연결, 이후 H2~H4 실제 Unity 경로/복구/기기 입력. 원본 C 채택과 G3는 후속 보정 조건을 유지한다.
- 설계 검토 결과: design-reviewer HD010-01(수치 지원 한계), algorithm-tester AM010-01(경계 밖500.1 벡터 지원) 지적을 수정한 뒤 각각 PASS. 실제 구현 검토도 별도로 수행했다.
- 구현 검토와 실행 결과: [010 검토 기록](../reviews/map-harness-010-review.md). 기초 23개+hook 13개 단위 시험, 8개 분석, 현재 코드 기준 기존 게임 회귀 PASS. H1 전체/G2 완료는 아니다.
