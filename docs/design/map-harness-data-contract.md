# 맵 하네스 데이터 계약 v1

[016 I1 기록 계층](map-i1-016.md)은 별도 I1_COLLECTION_PLAN/PROTOCOL_FIXTURE_ONLY envelope로 구현됐다. 아래 map v1 전체 request/관측/판정 계약의 부분 인수 또는 Unity 실행기로 사용하지 않는다. I1의 수집 PASS·정상 NOT_RUN 요약은 G2 실행 증거가 아니다.

MAP-HARNESS-009/r001의 [구현 설계](map-harness-implementation.md)를 따른다. 아래는 구현할 직렬화/검사 계약이다. 현재 실행기나 확정된 C# API가 아니다. 이름·자료형·단위·필수성을 함께 고정한다.

2026-10-31 연결 보충: [ASSET-PIPELINE-011 자산 계약](ai-3d-asset-harness.md)은 별도 asset schema와 map-bindings sidecar를 정의한다. 본 map v1 필드/geometry/snapshot/기존 MC·FI 의미는 변경하지 않는다. 후속 연결기가 두 묶음의 map/layout/C/변환·프리팹 지문을 대조한 뒤 G5에 합산하도록 설계하며, 현재 request에 미지원 자산 필드를 추가하지 않는다. 자산 외형이나 생성 collider는 geometry의 원본이 아니다. G2/G3 기본 도형 시험에는 외부 자산 승인을 요구하지 않는다.

## 파일 묶음과 인수 순서

`request.json` → 소스/환경 검증 → `manifest.json` → `journal.jsonl` → 사례별 `steps.jsonl/events.jsonl`와 캡처 → `case-result.json` → `cleanup.json` → `summary.json` 순서다. 각 run은 UUID nonce를 생성한다. 상대경로는 출력 root 안에만 resolve되어야 하며 절대경로/상위 이탈/심볼릭 링크는 거부한다. 실제 Unity 대상 절대경로는 환경 메타데이터에만 허용한다.

JSON은 UTF-8, 중복 키·비유한 숫자·unknown schema version·중복 ID·필수 키 누락을 거부한다. 명시적 `not_applicable`은 모드별 허용한 필드에만 사용할 수 있다. null을 0/빈 배열/PASS로 바꾸지 않는다. 읽기 전용 prepare 검사는 자료 오류를 반환하고 Play를 시작하지 않는다.

| 파일 | 필수 키와 자료형 | 소비자 / 검사 |
|---|---|---|
| request | schema_version:int=1, suite_id/revision:string, mode:enum, map_mode:MapRequired, command_queue_limit:int>0, scope:enum(G2_FIXTURE/G3_MAP), expected_case_ids:string[], cases:Case[], required_fi_variants:string[], controls:대응표, wall_timeout_s:number>0, max_evidence_bytes:int>0 | Python 인수기. 예상 ID=cases ID 집합, 빈 집합 거부, 단조 wall deadline 적용 |
| manifest | run_id/nonce:string, created_at:KST ISO8601, request_sha256, producer_revision, source_hashes/target_hashes/artifact_inputs:파일별 SHA256, engine_version/platform/package_hashes, planned_scope, expected_case_ids, mode, readonly_project_path | 실행기·판정기. 실행 전후 모든 입력 hash 비교. 중간 명시적 FI는 별도 사건 |
| journal | seq:int, run_id/nonce, phase:enum, case_id 또는 run_scope, monotonic_elapsed_s:number, timestamp, owned_resources, planned_mutation/before/after_reference | 실행기/watchdog. 자원/설정 변경 전에 기록, 종료 시 정리 대조 |
| case-result | case_id, observed_status, reason_codes:string[], first_bad_step:int 또는 명시적 none, measurements, missing_evidence, raw_hashes, start/end_marker, expected_detection, detection_status | 독립 판정기. 게임은 관찰만 기록하며 이 파일을 작성하지 않음 |
| cleanup | requested/restored/conflicted resource records, ownership proof, play_stopped, owned_objects_remaining:int, lock_released, status, errors | 부모 실행기. fixture 내부 실패와 실제 외부 복구 상태를 각각 기록 |
| summary | expected/started/completed/failed/blocked/not_run ID와 수, axis_status F/R/B/H/C, ordinary_cases_status, fi_detection_status, cleanup_status, final_status, evidence_manifest_sha256 | Python 최종 작성자. FAIL→BLOCKED→NOT_RUN→PASS. 누락/중복/0건 실행 PASS 금지 |

최종 상태 집계는 일반 사례의 observed_status와 FI의 detection_status를 사용한다. FI raw FAIL은 숨기지 않되 예상된 오류인 경우 H 검사 결과와 구분한다. 외부 cleanup FAIL은 어떤 예상 FI 성공으로도 상쇄하지 못한다. 자료가 완료됐다고 주장하면서 내용이 틀리면 FAIL, 단순 미시도 NOT_RUN, 증거/환경 부족 BLOCKED를 사용한다.

## Case 형식

| 필드 | 형식/단위 | 조건 |
|---|---|---|
| case_id / parent_ids / requirement_ids | string / string[] / MC·FI·MP ID[] | 유일 ID, 원본 사례군으로 추적 |
| geometry | geometry_id/revision/hash, coordinate_space, boundary, obstacles | G2는 logical_fixture 단위. G3는 원본/layout hash·006 변환·채택 C 개정 추가 필수 |
| bodies | 슬롯별 `{slot:int,shape:"circle",radius:number>0}` | 슬롯 누락/중복 금지. G2의 .25/1 등은 분석 도형용, 최종 몸체 아님 |
| initial | character_ids:int[], formation/pending:int 0..2, center:[x,y], slot_positions/HP/gear/State/Wave/Elapsed, config_copy, rng_seeds:[int,int] | 초기화 결과를 관찰하여 요청과 대조. 실제 설정에서 생성한 HP/gear는 resolved manifest에 값/hash 동결 |
| input_steps | `{index,frame_dt_supplied,move:[x,y],commands:Command[],continue_on_rejection:bool}`[] | 연속 index, clamp 전/후 기록. 생략 프레임은 없음. repeat 문법은 실행 전 배열로 확장/hash |
| thresholds | geometry_epsilon, replay_position_epsilon/replay_time_epsilon, arrival/hold/deadline/stall/formation 등 해당 assertion의 참조 키 | 모든 값은 유한·단위 포함·출처/개정 포함. 필요 없는 항목은 assertion 부재로만 제외 |
| expected | assertions:[{id,type,scope,operator,target,threshold_key}], end_condition, expected_status/reasons, max_steps | 도달/정지/부활 등 허용 변화를 명시. 오류가 나도 끝까지 가야 하는 FI는 그 조건도 등록 |
| mutation | `{fi_id,variant,trigger,scope,expected_effect}` 또는 `{kind:"none"}` | G2 소유 fixture에서만. G3 일반 인수의 mutation은 none |
| display | viewport_pixels/UI_rects/camera_parameters/capture_steps 또는 logical_only 사유 | 실제 프레임/DEVICE_SMOKE는 display/capture 필수. camera G3는 3화면비 필수 |

Command 허용 집합: SelectFormation(value), CancelFormation(), Revive(slot), NextWave(), Pause(), Resume(), FixtureDamage(slot,amount), FixtureInvalidateMap(new_geometry_hash). fixture 명령은 소유 token+G2 mutation 선언을 검증한다. State를 문자열로 직접 쓰는 입력 명령은 없다. 정상 상태 전환을 만들기 위한 FinishWave는 실제 게임 경로의 명시적 초기화 사건으로 기록하며 시험 중 자유롭게 상태를 바꾸는 용도로 쓰지 않는다.

G2 logical fixture는 s/k/reference image를 `not_applicable:logical_fixture`로 명시한다. 표시 어댑터를 쓴다면 k와 카메라를 실제 값으로 기록한다. G3는 s/k/body/model/layout/C/threshold 채택 근거가 없으면 BLOCKED. 007의 C BLOCKED를 문자열만 바꾸어 인수하지 않는다.

## 스텝/사건 원자료

필수 공통 필드: run_id/nonce, case_id, frame_id, step_id, observer_sequence, event_sequence, actual_frame_dt, supplied_dt, config_speed, requested_dt, applied_dt, sim_time_before/after, wall_elapsed, input_raw/clamped, commands/result, state_before/after, snapshot_before/after, geometry_hash_before/after, input_owner, map_mode, command_ids/consume_sequence, simulation_call_count, combat_call_counts.

Snapshot: Center, Selected 순서, Team 슬롯별 index/HP/maxHP/실제 위치/목표/예약/반경, Formation/Pending, State/Paused/Win/Wave, Elapsed/TotalTime/Kills/Spawned/WaveKills, 전투/정비 통계, gear·Stash·Enemies·Shots·Effects·History, uid/enemyUid/생성·재배치 타이머와 각도, Config hash, RNG 각 흐름의 호출수/trace hash. 서로 다른 ID 순서를 정렬해 의미를 잃지 않는다. UI 표시용 toast·실제시간/렌더 통계는 게임 명령 원자성 비교에서 제외하되 제외 키를 버전으로 고정한다.

원자성 비교는 같은 command의 직전/직후를 비교한다. 이후 프레임의 UI/실제시간 변화와 섞지 않는다. 동일 입력 재현에서는 이벤트 ID/종류/순서·정수/불리언/문자열을 정확 비교, 위치는 replay_position_epsilon(논리 단위), 시뮬레이션 시간은 replay_time_epsilon(초), actual frame/wall time·run nonce는 제외한다. 난수 요청/응답열은 정확 비교한다.

관찰 누락 검사는 driver 기대 스텝/게임 Simulate 호출 카운터/observer_sequence를 교차 대조한다. 관찰기 자체 heartbeat만으로 정상 동작이라고 판정하지 않는다. pause/prep 프레임은 simulation_call_count=0이며 별도 frame event를 남긴다. 사건 없는 before/직전 after 불일치는 상태 오염 FAIL.

## 기하 형식과 첫 예제의 분석 정답

boundary/obstacles는 논리 좌표의 정점 배열, 마지막 정점 반복 없음, edge는 마지막→처음 포함. 최소3점, 유한 좌표, 중복점/영길이변/자기교차 금지. 순서는 반시계로 정규화하고 정규화 전/후 hash를 남긴다. boundary는 하나의 단순 polygon, 장애물은 서로 겹칠 수 있는 단순 polygon 목록이다. 장애물 합집합을 볼록껍질로 대체하지 않는다. 구멍/복수 경계 형식은 v1에서 BLOCKED이며 원형 내부는 실제 별도 띠 조각 사이 자유 지면으로 표현한다. 향후 해당 형식이 필요하면 schema 개정 후 구현한다.

[시험 자료](map-harness-fixtures.json)의 세 예제는 한 명·원점·반경.25·dt.05·속도배수1·1스텝이다. 현재 이동140에 의해 무장애 중심=7, 추종 계수.6으로 실제 위치=4.2. 벽 x=[3.4,3.6], y=[-20,20]에 대해:

- 정상 벽 검사는 예약 슬롯 중심이 `x < 3.4-.25-epsilon`에서 멈추고, 실제 슬롯의 전체 구간도 안전하며 이동량은 0보다 커야 한다. solver는 g>=2×epsilon을 요구한다. 최대 alpha의 정확한 값은 oracle 기대값으로 복제하지 않는다.
- 차단 우회 주입은 실제 위치0→4.2, 예약0→7. 양 끝의 실제 몸체는 벽 밖이지만 중간은 관통한다. 따라서 endpoint 검사만 있는 잘못된 검사기를 반증한다.
- epsilon=.0001은 |좌표|≤500인 이 fixture의 분석 허용오차 제안이다. float/double 구현의 실제 오차가 이 기준을 만족하는지 경계 대조군으로 확인해야 한다. 부족하면 fixture 기준을 개정하고 재실행하며 G3 오차로 복사하지 않는다.

추가 G2 family의 고정 후보: `EMPTY` 경계[-500,500]²; `DIVIDER` 같은 경계에서 x=[3.4,3.6], y=[-500,500] 벽; `L_SHAPE` [(20,-20),(40,-20),(40,20),(30,20),(30,-10),(20,-10)]; `NARROW` 경계 x=[-60,60], y=[-500,500]. NARROW의 4인 균형/쐐기 슬롯은 반경1로 들어가지만 횡대±72는 들어가지 않는다. 유효/무효 진형 선택 대조로 사용한다. 이들은 첫 구현 fixture 정의이며 원작과 무관하다.

원형/곡선은 007 좌표를 복사하지 않고 별도 띠 fixture로 정의한다. 구현 전에 모든 family의 실제 입력열·기대값을 펼친 manifest에 넣고, 독립 oracle의 알려진 정답을 먼저 검사한다. 즉 예제 JSON은 G2 전체 실행 자료를 가장하지 않으며 전체 사례 전개는 구현 H1~H4의 완료 조건이다.

## 실행 명령 계약과 완료 보고

후속 구현의 예정 명령: `run.py map --manifest <request.json> --mode prepare|run|judge`. prepare는 자료/경로/지문 검사만, run은 소유 Unity 세션 실행 및 judge/복구, judge는 완료 원자료 재판정만 수행한다. judge 결과는 실행/입력 증거를 새로 생성하지 못하며 자기 시각과 원래 실행 시각을 분리한다. 아직 어떤 모드도 실행 가능하다고 안내하지 않는다.

새 명령은 JSON 요약에 status/report/run_id/missing_evidence를 출력하고 PASS=exit0, FAIL=1, BLOCKED/NOT_RUN=2를 반환한다. Python/Unity timeout·예외의 종류를 상태/이유로 남긴다. 통합 gate는 문서 검사 성공과 map 상태를 별도 필드로 보존한다. map 검사가 요구된 작업인데 보고서가 없으면 BLOCKED다.

추가 oracle_vectors는 중심 거리 d와 몸체 여유 g=d-r의 분석 정답이다. g=epsilon은 수치 경계대로 BLOCKED, g<-epsilon은 FAIL이다. 정확한 침투 깊이를 산출하지 못한 sweep은 witness_bound로 증명하고 최소 여유 측정과 구분한다.
