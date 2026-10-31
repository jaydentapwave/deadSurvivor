# AI 3D 자산 하네스 구현 설계

ASSET-PIPELINE-011/r001 · 2026-10-31 · 전체 설계 명세 · 012 최소 전송 부분 구현

[012 최소 구현](asset-pipeline-minimum.md)은 기존 원본의 기록/재개·Blender 처리·Unity 전송/실측/상태 복구를 시험한다. 아래 AC/AFI의 전체 집계·A5 등록·G5와 동일한 구현 범위가 아니다. 최소 도구의 실행 증거는 [별도 검토](../reviews/asset-pipeline-012-review.md)에 둔다.

[워크플로우 A0~A5](../workflows/ai-3d-assets.md)의 자료/판정/실패 주입 계약이다. [외부 근거 AD01~07](ai-3d-reference-adoption.md)을 우리 프로젝트에 맞춰 보강했다. 새 명령·파서·검사기·자동 hook은 이번 작업에 구현하지 않는다.

## 책임과 기존 맵 하네스와의 경계

| 역할 | 단일 작성자 / 소비자 | 경계 |
|---|---|---|
| 자산 계약 | 작업 설계자가 A0 동결 / 모든 어댑터·판정기 | 공급자 응답으로 기준을 수정하지 않음 |
| 작업 journal | 로컬 작업 실행기 / 복구기 | 생성 task와 원본 지문을 연결, 품질 합격 작성 금지 |
| 처리 관측 | Blender 처리기 / 독립 자산 판정기 | 처리 전후 메시·변환·재질 및 출력 해시 |
| 임포트 관측 | Unity Editor 어댑터 / 독립 자산 판정기 | 실제 저장·재로드한 프리팹 측정, 환경/콘솔 포함 |
| 판정·시각 검토 | 독립 판정기 및 검토자 / 등록 작성자 | 생산자의 ok/성공 문자열을 그대로 PASS로 변환 금지 |
| 자산 등록부 | 단일 등록 작성자 / 맵 배치 어댑터 | 정확한 지문·승인 범위만 선택 가능 |
| 맵 geometry/변환 | 기존 채택 맵 묶음 / DSGame·맵 oracle | 자산 메시로 C·s/k를 재계산하지 않음 |

기존 [맵 데이터 계약 v1](map-harness-data-contract.md)은 그대로 둔다. 본 자료는 `asset_schema_version=1`의 **별도 예정 형식**이며 map request v1에 새 필드를 임의 삽입하지 않는다. 연결부는 향후 별도 L2 구현에서 sidecar를 검사한 뒤 두 결과를 합산한다. 기존 H1은 아래 자료를 검사하지 않는다.

## 파일·필드·생산/소비 계약

모든 자료에는 asset ID/revision, run ID/nonce, scope(FIXTURE 또는 PRODUCTION), KST ISO8601 시각, producer 버전과 입력 해시가 있다. 파일 경로는 프로젝트 내 지정 root 기준 상대 경로이며 상위 이탈·심볼릭 링크·중복 경로·비유한 수·중복 JSON 키·미지원 schema·필수값 누락은 거부한다. 실제 Unity 절대경로는 환경 필드에만 기록하고 허용된 단일 대상으로 검증한다. GUID만으로 파일 동일성을 판단하지 않는다.

| 예정 파일 | 필수 정보 | 소비와 인수 |
|---|---|---|
| asset-spec.json | asset ID/revision, scope, 역할, source_refs(RF/OB/출처·허용 범위), 형상/스타일 조건, 단위/축/원점/치수, budgets/thresholds(값·단위·근거·개정), 카메라/조명 규격, format, required_case_ids, generation_policy | 모든 어댑터. 항목별 미정은 명시. 필요한 기준 미정이면 그 단계 BLOCKED |
| request.json | spec hash, provider/server/account_alias, model/version, 입력 파일/프롬프트 hash, 전체 생성 옵션, 요청 지문, attempt ID, 허용 근거·한도·최대 유료 시도 수, 조회/다운로드 횟수·간격·총 timeout | 공급자 어댑터. 비밀 값/인증 헤더/서명 URL 금지. POST 재시도 동작 미확인 시 제출 차단 |
| journal.jsonl | 연속 seq, run/attempt, 단계·이전/이후 상태, task ID(수락 후), 단조 경과/벽시각, 취소/오류, 예정 변경·소유 파일·before hash | 복구기. 제출 의도는 POST 전, task ID는 polling 전에 저장. 빈/손상 journal을 새 제출 허가로 해석 금지 |
| raw-manifest.json | 원본 모델/부속 텍스처의 path/hash/size/type, task 연결 또는 existing_source 출처, 받은 시각, 사용/공개 범위 | 처리기. 원본 덮어쓰기 금지, 무결성/부속 파일 누락 FAIL |
| process-receipt.json | 원본 hash, Blender/스크립트/설정 hash, 연산 순서, 변환 행렬/단위, 처리 전후 삼각형/정점/재질/텍스처·bounds, 출력/캡처 hash | 독립 판정기. 연산·출력이 없는 성공 로그만으로 PASS 금지 |
| import-receipt.json | processed hash, 대상 프로젝트·Unity/패키지/렌더 설정, import 설정·변환, 후보 경로/GUID/.meta 및 재귀 의존 hash, 저장 후 재로드한 실측/컴포넌트/콘솔 | 판정기. 0메시/미지원 shader/필수 texture 누락/예산 초과는 FAIL. importer 없으면 BLOCKED |
| visual-review.json | spec/camera/environment hash, 원본/처리본/Unity별 캡처 ID·파일 hash·frame/대상 ID, 항목별 관찰/상태/검토자·시간·이유 | 판정기. 이미지 존재와 검토 완료를 구분. 미검토는 NOT_RUN; 잘못된 자산/이전 화면은 FAIL |
| case-results.json + cleanup.json | expected/started/completed ID, 사례별 observed_status/reason/measurement/raw hash, FI의 detection_status, 소유 자원 복구·충돌 | 집계기. 중복/예상 외 결과/누락·비유한값 검사, 실제 복구 실패는 덮지 않음 |
| pre-acceptance-summary.json | spec/input/evidence manifest hash, A0~A4와 AC-09-pre 결과/필수 축·사례 수, missing_evidence, cleanup, planned_scope, 시작/종료 표시 | 등록 작성자. 등록 전 인수 결과이며 AC-09-post는 포함하지 않음. 불완전/0건 PASS 거부 |
| acceptance.json | 정확한 asset/revision·원본/처리본/프리팹/의존 hash, spec/pre-summary/cleanup/review hash, environment, 허용 용도/범위, 후보 작성 시각·판정 주체 | 등록 후보의 불변 증거. 이 파일 존재만으로 approved 아님; 이름만 같은 다른 파일은 미채택 |
| registration-receipt.json | transaction ID, acceptance hash, 이전 registry 세대, 예정 entry(asset/revision/acceptance/scope)의 지문, pending 재조회/최종 의존 검사인 AC-09-post 결과·cleanup | 등록 최종 작성자/소비자. 자기 hash나 최종 summary hash를 내부에 넣지 않음 |
| summary.json | pre-summary/acceptance/registration receipt hash, 실제 registry 세대/선택 가능 상태, 전체 사례/축/cleanup/final_status | 등록 이후 최종 보고. acceptance가 역참조하지 않으므로 순환 지문 없음 |
| map-bindings.json(sidecar) | map/layout/C/좌표변환 개정·hash, OB별 asset acceptance hash/프리팹 hash/배치 행렬·RF/RT/영향 MC, 실제 scene/instance 식별·의존 hash | 미래 맵-자산 연결기. G5 실제 배치 실측과 대조, 기존 map v1에 주입하지 않음 |

spec과 request의 선택적 값은 생략 조건을 명시한다. 예: 기존 원본 재사용에는 provider/task/비용 필드 대신 `source_mode=existing` 및 원본 출처·사용 범위를 요구한다. textured=false인 승인된 무텍스처 자산에는 텍스처 슬롯 검사를 제외 사유와 함께 고정한다. unknown/null을 0·무제한·PASS로 바꾸지 않는다.

별도 파일 `asset-registry.json`은 예정 영구 경로 `docs/art/assets/asset-registry.json`에서 asset ID/revision→acceptance와 registration receipt 경로/hash를 인덱싱한다. 레코드는 pending, approved, superseded, revoked 상태를 구분하고 과거 개정/증거를 삭제하지 않는다. pending은 맵에서 선택할 수 없다. 승인 뒤 파일/의존성이 달라지면 재해시 검사에서 선택을 차단한다. 등록부 자체가 새로운 검증 근거를 만들지 않는다.

### 지문과 원자적 채택

1. request 지문은 공급자/서버/비밀 아닌 계정 alias/spec/모든 생성 옵션/프롬프트/입력 파일 hash를 정규화해 계산한다. 같은 프롬프트가 다른 계정/모델과 섞이지 않게 한다. 계정 alias의 실제 인증 계정 대응을 확인하지 못하면 재개 BLOCKED.
2. 동일 요청 잠금과 동일 asset 개정의 처리/가져오기 잠금을 구분한다. 잠금 대기에는 timeout을 두며 stale lock은 PID뿐 아니라 소유 nonce/미완료 journal/작업 상태를 확인하고 회수한다. 살아 있는 작업의 잠금을 삭제하지 않는다.
3. 중간 결과는 임시 파일에 쓰고 검증 후 교체한다. 재시작 시 완료 표시뿐 아니라 파일 해시와 모든 필수 부속물을 다시 확인한다. 디스크 부족·손상·다른 원본이면 캐시를 채택하지 않는다.
4. A5의 AC-09-pre는 A0~A4 증거와 영구 보존 사본의 원본/처리본/계약/절차/증거 및 Unity 의존 집합을 재해시한다. 통과한 pre-summary를 불변 acceptance 후보가 참조한다. 이때 AC-09-post는 아직 NOT_RUN이며 사전 인수 집계에서 제외한다.
5. 단일 등록 작성자는 잠금 아래 acceptance 후보→registry pending을 기록한다. AC-09-post는 기록된 후보와 pending을 다시 읽고 사전 고정 entry/세대·최종 대상 의존 지문·등록 자원 cleanup을 검사한다. 통과 receipt를 기록한 후에만 같은 잠금에서 approved entry를 원자적으로 공개한다. entry에는 receipt hash도 포함한다. receipt의 예정 entry 지문은 자신의 hash와 registry 전체 hash를 제외한 고정 필드만 대상으로 하여 순환을 막는다.
6. 소비자는 approved와 일치하는 acceptance/receipt/현재 의존 지문을 모두 확인해야 선택한다. 최종 summary는 공개 뒤 다시 읽은 결과를 기록하며 acceptance를 수정하지 않는다. 중간 종료로 남은 후보/pending은 선택 금지하고 다음 실행에서 AC-09-pre/post를 재검사한다. receipt 없는 approved·누락/손상 후보·세대 불일치는 FAIL이며 자동 승인하지 않는다.
7. 사용자 수정·다른 실행의 덮어쓰기를 발견하면 원본을 유지하고 차이/미완료 상태를 기록한다. 이전 승인본으로 무조건 덮어 복구하지 않는다.

## 시각·성능·충돌 판정

세 비교 세트를 별도로 남긴다. **S1** 원본→처리본의 형태 보존, **S2** Blender→Unity 전달 충실도, **S3** 실제 게임 카메라에서 자산/캐릭터/통로 판독이다. 동일 asset 개정/출력 파일을 연결하고, 자동 object framing으로 매번 화면 크기가 달라지지 않도록 camera preset을 동결한다.

카메라 투영/transform/orthographic size 또는 FOV/near/far/viewport, 조명·노출·색 공간·톤매핑·배경·렌더 파이프라인·해상도를 기록한다. 엔진별 표현 차이는 동등 조건으로 변환한 값과 차이를 명시한다. 동일 카메라라고 주장할 수 없으면 비교 조건을 보정하거나 BLOCKED로 둔다. 메시 수치/실루엣·방향·지면 접점·색·텍스처·그림자를 항목별로 검토하고 단일 이미지 점수로 대신하지 않는다.

실제 후보 프리팹을 저장 후 재로드해 Renderer world bounds, root/mesh transform, 지면 접점과 원점, 각 메시의 삼각형·정점·재질 슬롯과 텍스처 연결/해상도/색 공간을 읽는다. 자산 예산과 맵의 동시 배치 예산을 분리한다. 상대 감소율만으로 예산을 통과시키지 않는다. 목표 기기·viewport·인스턴스 수·warmup/측정 구간·프레임 시간 지표/메모리 기준을 사전에 고정한다. Editor 측정을 물리 기기 성능으로 보고하지 않는다.

외형 V, 채택 충돌 C, 불확실 U는 계속 분리한다. 별도 생성 collider 또는 예기치 않은 스크립트/물리 컴포넌트는 첫 정적 소품 계약 위반이다. 시각적 벽이 통과 가능하거나 시각 통로를 C가 막는 경우도 OB/RT별 검토 대상이며, 이를 고치려고 자동으로 C를 바꾸지 않는다. G5의 논리 C hash와 실제 배치/동작은 기존 맵 oracle·사례로 검사한다.

## 필수 정상 사례

각 ID는 실행 전에 실제 파라미터로 펼친 expected 목록을 가진다. 아래는 **시험 설계**이며 현재 모두 NOT_RUN이다. 자산 고유 기준이 필요한 사례는 현재 readiness=BLOCKED(기준 미정)다. scope에 맞지 않는 사례만 사전에 이유를 적어 제외하고 실행 중 실패를 피하려고 빼지 않는다.

| ID | 입력/관찰 | 통과 조건 / 적용 단계 |
|---|---|---|
| AC-01 | spec/경로/출처/요청/기능·한도 검사 | 필수값과 단계별 근거 완전; A0/모든 실행의 사전 검사 |
| AC-02 | 가짜 공급자 및 허용된 live 각 작업의 수락→조회→파일 수집 | task/파일 연결과 한도, 중복 제출 없음; A1. fake/live 증거 분리 |
| AC-03 | 원본 보존과 같은 원본·절차 2회 처리 | 원본 hash 동일, 허용치 내 측정/외형 재현; A2. 바이트 동일 출력은 도구가 보장할 때만 요구 |
| AC-04-process / AC-04-import | 처리본 / 최종 Unity 프리팹의 단위·축·원점·치수 | 각각의 실제 관측으로 모든 축/원점 조건 충족; A2 / A3 |
| AC-05-process / AC-05-import | 처리본 자원 / Unity 자원·컴포넌트·콘솔 | 필수 예산·슬롯·허용 목록 충족, 미지원 shader/누락 없음; A2 / A3 |
| AC-06-S1 / AC-06-S2 / AC-06-S3-asset / AC-06-S3-map | 원본→처리 / Blender→Unity / 게임 카메라 단독 자산 / 실제 맵 비교 | 같은 지문/조건과 항목별 검토 완료; A2 / A3 / A4 / G5 |
| AC-07 | 사전 고정 기기·부담으로 실행 | 등록 성능 기준 충족; A4 자산 시험, G5는 전체 맵 MC-PERF-01 추가 |
| AC-08 | 성공/실패/취소·재시작 각각의 소유 자원 | 원본/기존 승인본 보존, 상태 복구·사용자 충돌 검출; 모든 단계 |
| AC-09-pre / AC-09-post | 사전 인수·영구 보존 / 기록된 pending·후보 재조회·최종 지문·cleanup | pre-summary는 pre까지만, post PASS receipt 후 approved 공개. 소비자도 근거 재검사; A5 |
| AC-10 | 실제 맵 OB별 binding, C/변환 및 영향 사례 | 승인 자산만 배치, 시각/충돌 대응, 맵 필수 회귀 PASS; G5 연결 |

## 실패 주입과 정상 대조군

asset 접두어 AFI는 맵 FI-01~10과 독립이다. 소유한 fixture/결과 사본/가짜 공급자에 주입하며 실제 유료 POST·회사 프로젝트·사용자 원본을 파괴하는 시험은 하지 않는다. 각 변형과 동일 경로의 정상 대조군을 manifest에 등록한다.

| ID | 주입/대응 정상 | 기대 observed_status 및 이유 |
|---|---|---|
| AFI-01a/b | 수락 후 polling timeout / 다운로드 실패; 정상 수집 | 중단 BLOCKED, 같은 task 재개 후 수집 가능. create 호출 수는 1 유지 |
| AFI-02 | POST 응답 유실 또는 ID 기록 전 종료; 정상 ID 저장 | BLOCKED/SUBMISSION_UNKNOWN, 자동 재제출 0회 |
| AFI-03 | 동일 요청 2실행, 계정 alias 불일치; 직렬 정상 | 두 번째 실행 BLOCKED/LOCKED 또는 ACCOUNT_MISMATCH; 서로 다른 task/계정 증거 혼용 금지 |
| AFI-04 | 손상/누락 다운로드 또는 임시파일만 남김; 완전 파일 | FAIL/RAW_INCOMPLETE, 캐시 채택 금지; 재다운로드와 유료 재생성 구분 |
| AFI-05 | 이전 원본·프리팹·캡처/nonce 혼입; 일치 자료 | FAIL/EVIDENCE_MISMATCH |
| AFI-06 | 원본 덮어쓰기, 단위/축/원점 오류, 예산 초과; 계약 충족 | FAIL/SOURCE_MUTATED 또는 TRANSFORM/BUDGET_MISMATCH; 변형별 오류 기대값 고정 |
| AFI-07 | 텍스처 누락/잘못된 shader/금지 컴포넌트; 정상 프리팹 | FAIL/IMPORT_CONTRACT_MISMATCH. 필수 importer 자체 부재는 별도 BLOCKED/IMPORTER_UNAVAILABLE |
| AFI-08 | 다른 카메라/미검토 이미지; 정상 비교 | FAIL/CAPTURE_CONTRACT_MISMATCH 또는 NOT_RUN/VISUAL_REVIEW_MISSING; 둘을 분리 |
| AFI-09 | 예상 사례 삭제/중복/0건/종료표시 제거; 완전 보고 | FAIL/RESULT_SET_MISMATCH 또는 BLOCKED/INCOMPLETE_RUN; 완료 주장과 모순되면 FAIL |
| AFI-10 | 취소/디스크 부족/사용자 수정/복구 실패; 정상 정리 | BLOCKED/CANCELLED·WRITE_UNAVAILABLE·USER_CONFLICT, 실제 복구 실패 FAIL/RESTORE_MISMATCH. 원래 FAIL 보존 |
| AFI-11 | 검증 뒤 프리팹/texture/.meta 수정 또는 등록 중 종료; 정상 등록 | FAIL/ACCEPTANCE_STALE 또는 BLOCKED/REGISTRATION_INCOMPLETE; 미완료 approved 금지 |
| AFI-12 | 미승인 자산 배치/C hash·OB 변환 변경; 정상 binding | FAIL/MAP_BINDING_MISMATCH; 기존 맵 C나 기대값 자동 갱신 금지 |

`observed_status`와 `detection_status`를 분리한다. 의도한 raw FAIL/BLOCKED/NOT_RUN을 탐지했을 때도 원자료 상태를 보존한다. 탐지 PASS에는 정확한 오류·정상 대조 PASS·주입 확인·실제 외부 정리 PASS가 모두 필요하다. 예상 밖 예외로 멈춘 것을 탐지 성공으로 세지 않는다. 주입 복구 자체를 모의 실패시킨 경우 실제 소유 환경의 정리는 별도로 성공해야 한다.

## 종합 상태와 증거 무효화

필수 축은 provenance(출처/허용), integrity(작업/파일), geometry_budget(변환/자원), import, visual, performance, cleanup, harness_integrity다. 단계별 관련 AC와 AFI 검증 유효성을 연결한다. 실행 집계는 FAIL→BLOCKED→NOT_RUN→PASS이고, 품질 실패를 생성 성공으로 상쇄하지 않는다. 미시도 NOT_RUN, 필요한 기준/도구/증거 부족 BLOCKED, 관찰된 계약 위반 FAIL이다. 기존의 유효 증거를 재사용할 때는 원래 실행 시각/환경과 이번 확인 시각을 분리한다.

| 변경 | 최소 무효화 / 다시 확인 |
|---|---|
| 프롬프트/모델/옵션/원본 | A1 이후 해당 개정 전체. 새 generation 없이 기존 파일을 쓴다면 source_mode=existing으로 별도 provenance |
| 처리 스크립트/Blender/설정 | A2 이후 처리·임포트·비교·채택 |
| importer/Unity/패키지/재질/프리팹/.meta | A3 이후 실측·비교·성능·채택. 바뀐 범위 특정 불가면 넓게 재검사 |
| 카메라/조명/렌더/기준값 | 관련 비교/성능·채택. 결과를 본 뒤 기준 완화 시 새 개정으로 실행 |
| OB 배치/맵 C/s/k/인스턴스 수 | AC-10·관련 맵 MC, 카메라/전체 맵 성능. 자산 단독 합격을 맵 합격으로 승격 금지 |
| 검사기/복구기/등록기 | 영향받은 정상/AFI와 모든 소비 결과. 의존 범위 불명은 관련 묶음 전체 |

A5 자산 채택에는 PRODUCTION AC-01~09의 해당 단계 필수 증거 및 그 검사기의 유효한 실패 탐지/복구 증거가 필요하다. existing_source는 live AC-02 대신 출처/파일 수집 검사를 실행하며 생성기 검증을 주장하지 않는다. 맵 배치 AC-10은 A5 자산 단독 승인에 필요하지 않고 G5에서 필수다.

### 단계별 필수 변형의 고정 범위

이 표가 단계 집계 범위의 원본이다. AC-01/08은 각 실행 단계 suffix(A1~A5)를 붙여 사전 검사/복구를 반복한다. AC-02는 fake/live/existing을 구분한다. AFI-06은 source/process/import, AFI-08은 S1/S2/S3-asset/S3-map, AFI-10은 각 실행 단계의 cancel/write/conflict/restore 변형으로 펼친다. AFI-05/09도 각 생산 단계의 증거를 변조하는 공통 사례다. 미지원 변형을 PASS로 생략하지 않는다.

| 단계 | 필수 정상 사례(이전 단계의 유효 증거 포함) | 해당 검사기 필수 실패 탐지/정상 대조 |
|---|---|---|
| A0 | AC-01-spec | 잘못된 schema/경로/미정 기준/허용 범위 누락의 인수 거부 대조 |
| A1 | AC-01-A1, AC-02의 source mode에 맞는 변형, AC-08-A1 | AFI-01~04, AFI-05/09/10-A1. existing은 유료 제출 관련 AFI를 수행하지 않고 파일 경로 대조만 적용 |
| A2 | AC-01-A2, AC-03, AC-04-process, AC-05-process, AC-06-S1, AC-08-A2 | AFI-05/09/10-A2, AFI-06-source/process, AFI-08-S1 |
| A3 | AC-01-A3, AC-04-import, AC-05-import, AC-06-S2, AC-08-A3 | AFI-05/09/10-A3, AFI-06-import, AFI-07, AFI-08-S2 |
| A4 | AC-01-A4, AC-06-S3-asset, AC-07, AC-08-A4 | AFI-05/09/10-A4, AFI-08-S3-asset; 기준 미정·측정 누락·성능 초과 대조 |
| A5 | AC-01-A5, AC-09-pre, 이후 별도로 AC-09-post/AC-08-A5 | AFI-05/09/10-A5, AFI-11의 등록 전/후/중단 변형 |
| G5 연결 | AC-10, AC-06-S3-map, 기존 MC-CAMERA-01/MC-PERF-01 및 G4 필수 결과 | AFI-12, AFI-08-S3-map와 기존 맵 FI/MC의 유효 증거 |

미래 단계 NOT_RUN은 현재 단계 집계에 넣지 않되 전체 파이프라인 미완료로 남긴다. 예: A2의 AC-06-S1 PASS가 AC-06-S2/S3 PASS를 뜻하지 않는다. P1의 가짜 공급자 검증도 A1 live 성공이 아니다.

## 구현 순서와 gate 통합 인수

| 순서 | 구현 내용 | 완료에 필요한 증거 |
|---|---|---|
| P1 | schema/지문/journal/가짜 공급자/집계 | 위 A0/A1의 fake 변형과 실패 탐지 전수; 유료 작업 없이 검증 |
| P2 | Blender 처리/실측/캡처 | 위 A2의 정상/실패 변형 전수; Unity가 필요한 import/S2/S3는 아직 NOT_RUN |
| P3 | 기존 Unity CLI 어댑터/프리팹 재측정 | 위 A3/A4의 정상/실패 변형 전수; 개인 Unity 격리 실행·화면·성능·복구 |
| P4 | 채택/보존/등록·map sidecar·gate | AC-09~10, AFI-11~12 및 누락/옛 증거 차단. AC-10 실제 맵 검증은 G5 전 준비 필요 |
| P5 | 허용된 API 최소 실제 시험 | 기능/과금/공개 사전 확인 후 AC-02 live, 나머지 해당 AC. fake 성공을 API 성공으로 전용 금지 |

각 구현은 development.md에 따라 재분류하고 필요한 독립 검토를 받는다. G5를 위한 P4 연결 완료와 실제 맵 검증 전에는 전체 자산-맵 파이프라인 완료를 주장하지 않는다. 순수 자산 검사/등록 구현은 AC-10 fixture로 검증할 수 있으나 그 결과는 G5 PASS가 아니다.

향후 gate는 자산 입력·처리기·Unity 대상·binding 변경 범위를 감지해 현재 지문의 해당 증거를 요구하도록 확장한다. 누락/미지원 어댑터는 BLOCKED, 실패는 FAIL이며 검사 중 자동 생성/동기화/패키지 설치/유료 요청은 하지 않는다. 현재 gate의 UnityArtExport 변경 차단을 이 설계만으로 해제하거나 baseline을 갱신하지 않는다. gate 확장도 누락·옛 지문·정상 대조 시험 후에만 구현 완료로 기록한다.
