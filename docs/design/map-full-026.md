> 2026-11-01 후속 지시: N2 실제 빌드·테스트는 사용자가 직접 수행한다. 해당 작업을 에이전트가 실행하지 않고 [028 N3 대표 구역](map-art-028.md)으로 진행한다. N1/G2 미완료를 완료로 간주하지 않는다.

# MAP-FULL-026 r001 — 024 N1~N3 실행

- 날짜: 2026-11-01. 규모 L2. 상태: 사전 설계 검토 중.
- 요구: 사용자 요청에 따라 024 전체 작업을 진행한다. 목표 환경은 사용자가 지정한 현재 개발 Mac이다. 기존 024/025/007/018/021~023 결과는 보존한다.
- 원본: [024](map-next-steps-024-plan.md), [025 준비](map-n1-025-preparation.md), [009 실행 계약](map-harness-implementation.md), [데이터 계약](map-harness-data-contract.md), [필수 MC/FI](맵-하네스-사례명세.md).
- 완료 기준: N1은 유효 G2와 전체 G3, N2는 G4·전체 런·실제 Mac 실행 파일·부하 기준, N3는 대표 구역의 구조/충돌 보존·동일조건 비교·가독성/성능·자산 근거. 준비/부분 PASS를 전체 완료로 사용하지 않는다.

## 구현 책임과 변경 파일

메인이 구현한다. design-reviewer/algorithm-tester/validator-client는 읽기 전용 독립 검토다. Runtime/Editor 단일 원본은 Assets/DeadSurvivor다. 기존 미커밋 문서·씬·설정·원본은 보존한다.

1. `Tools/Verify/map_full.py`: 별도 명령 prepare/run/judge. strict JSON·입력 지문·개정 검사, 사전 manifest, 예상 ID·기준 동결, 외부 프로세스 watchdog·단조 deadline·journal·원자료 지문·독립 판정/집계. 기존 fixture/I1 수집기의 scope·128건 제한을 변경하지 않는다.
2. `Runtime/DSGame.MapFrame.cs`와 DSGame Update/상태 명령 진입점: 맵 시험의 소유 입력·명령 큐·한 프레임 한 Simulate와 전후 snapshot. 상태 작성은 DSGame만 담당한다. 기존 LegacyNoMap 및 017 fixture 경로 유지. 명령 순서·nonce/중복/초과·거부 결과·RNG trace를 기록한다. UI/키보드/시험이 같은 명령 진입점을 사용한다.
3. `Editor/DSMapFullVerification.cs`: 기존 lock/소유 marker/복구와 018 프리팹을 재사용하는 격리 실제 Update 시험. 소유 게임/가상 장치만 사용. 고정 재생과 DEVICE_SMOKE 분리. 실제 사례 초기 설정은 별도 사건으로 기록하고 관측값을 정답으로 역산하지 않는다.
4. `test_map_full.py`: manifest 누락·중복·지문·과거 nonce·관찰 누락·중단/복구·FI 판정 반례. 기존 local/all/gate 기준을 완화하지 않는다.
5. N2/N3의 별도 Editor/Runtime 측정/미술 구성과 저장 장면은 N1 결과로 범위를 구체화한다. 빌드는 Unity API로 현재 Mac 대상에 생성한다. 새 유료 생성·외부 게시 없이 기존 바위의 provenance를 확인하거나 프로젝트 내 기본 mesh/재질로 제작한다.

## 자료·좌표·기준

007의 외피는 원작 충돌 사실이 아니다. 026에서는 원본과 별도로 ‘우리 시험 지형 후보 C’를 유지하고, 진단 뒤 필요한 CH/개정을 작성한다. 후보를 실제 채택한 이유와 불확실성/영향 RT를 남긴다. 표시만 바꿔 C를 채택했다고 주장하지 않는다.

개발 시험 기준 후보: s=4, pixel 원점(565,580), 논리 x/y 원본 방향, world_scale=.02, circle 반경8, epsilon=.001, 카메라55도/ortho6. 고정dt=1/30, 시간 경계1/120~.1(게임 clamp .05), 도착 반경4 logical, 정지/추종 오차1 logical, replay 위치 .002/시간 .001. 수치는 실행 manifest에서 동결하고 수집 뒤 소급 조정하지 않는다. 경로 입력은 방향 제어로 경유점에 도달하도록 사전 생성하며 진단과 인수 입력열을 분리한다.

025의 708개 조합과 시작/경계/생사/정비/시간/가림/재현/정리·FI를 빠짐없이 전개한다. RT007/009는 진단, 나머지는 원본 역할을 유지한다. 실패 경로를 삭제/진단으로 바꿔 합격시키지 않는다. 독립 계산과 실제 이동이 불일치하면 FAIL로 남기고 자료/로직/도구 문제를 구분한다. 전체 자유 지면은 반경/진형을 고려한 구성공간 분석으로 조사하며 해상도 미해결을 전체 연결 PASS로 보고하지 않는다.

## 세션·실패·복구

실행 전 ready Editor의 정확한 프로젝트/PID, 정지·저장 clean 씬·표준 reload·pending 부재·콘솔 cursor를 확인한다. 기존 사용자 Play를 중단하지 않는다. 새 run/nonce·소스/입력 지문과 복구 snapshot을 저장한 뒤 임시 소유 씬을 연다. 직접 바꾼 설정·장치·객체만 소유권과 값 대조 후 복구한다. 사용자 변경 충돌은 BLOCKED·증거 보존이다.

외부 감독 프로세스가 실행 worker heartbeat와 Editor PID·deadline을 관찰한다. worker 강제 종료 시 기존 run을 PASS로 바꾸지 않고 중단/정리 결과를 기록한다. 외부 정리도 소유 marker/nonce/PID 증명이 필요하다. 소유 자원이 확인되지 않으면 강제 씬 복원/삭제하지 않는다. 예상 FI 검출과 실제 외부 정리를 별도 집계한다. 출력 실패/누락/이전 결과/NaN/관찰정지는 성공 불가다.

MapRequired 자료/바인딩 부재는 명시 거부하고 Legacy fallback 금지. 실패 명령은 HP/장비/시간/진형/난수/이력 전후가 같아야 한다. 예외 중간 commit은 자동 rollback 성공 대신 FAIL과 실제 상태를 기록한다. fixture 피해는 실제 Damage 경로, FinishWave는 명시 초기화 사건만 허용한다.

## N2 기준과 N3 범위

사용자 지정 현재 Mac의 CPU/GPU/RAM/OS를 기록하고 1280×720 Mac arm64 Development build를 우선 시험한다. 성능 기준은 첫 진단 전 별도 프로필에 동결한다: 목표60FPS, p95 frame≤16.67ms/p99≤33.34ms, 최대100ms, 총 메모리≤2GiB, 첫 사용 가능 화면≤5s. Editor/Player를 구분하고 측정 불가 항목은 BLOCKED다. 부하는 기존 웨이브의 실제 동시 수를 진단한 뒤 기본/최대/스트레스 반복 구간을 인수 전에 동결한다. 탐색 방문/실패/생성/정지·CPU/GPU/GC를 함께 본다.

N1/G3 통과 후 N2, N2 통과 후 N3를 실행한다. N3 구역은 OB001와 인접 통로 후보이며 N1 문제 지도/N2 부담 자료로 선정한다. 충돌 hash를 보존하고 미술 전후 동일 카메라로 캡처·직접 검토한다. 새 자산을 필요로 하지 않는 Unity mesh/재질부터 사용한다. 외부 바위는 현재 A5 미채택이므로 provenance만으로 최종 승인하지 않는다.

## 검증 순서

사전 설계/계산 검토 → 메인 구현과 영향 단위/local → 독립 코드/계산 검토 → G2 정상/FI/재현/복구 → N1 전체 사례/연결·직접 화면 → N2 전체 런/부하/Mac 빌드 → N3 표현/구조/동작/성능 재검사 → 현재 all --screens → 독립 증거 검토 → 상태 문서/local/gate.

막힌 단계가 있으면 원인과 실제 자료를 남기고 해소 작업을 계속한다. 기준 없는 전체 완료 선언·하네스 미구현의 PASS·과거 결과 전용은 금지한다. 기존 hook/baseline/검사 기준은 변경하지 않는다.

## 사전 검토 보완 r001

- DR026-01: 첫 구현은 `map_full.py prepare/judge` strict 계약과 판정 기반이다. scope=G2_FIXTURE/G3_MAP을 분리한다. Geometry는 Case.geometry→C# immutable map, initial→DSGame 초기화 사건, input_steps→소유 frame queue, thresholds/expected→Python assertion, display→화면 검사로 소비한다. v1을 부분 지원하면서 전체 요청을 승인하지 않는다. 지원하지 않는 명령/assertion/필드는 BLOCKED 또는 형식 FAIL로 거부한다. 실행 어댑터 미연결은 NOT_RUN/BLOCKED이며 PASS 불가다.
- DR026-02: `DSMapSession.Bind`의 고정4인 검사를 자료 바인딩에서 분리한다. 실제 StartRun이 Selected 인원/최초 F0를 판정한다. 시험의 F1/F2는 F0 초기화→명시 정비 초기화→SelectFormation→NextWave 순서를 기록하거나, G2의 소유 초기화 사건에 한해 초기 진형/실제/예약 상태를 설정한다. FI 초기 겹침은 관찰 초기화 marker를 기록한 뒤 독립 oracle가 거부를 판정한다.
- DR026-03: UI의 진형/부활/다음 웨이브/일시정지/재개와 Update의 Space/Escape·Digit1을 명령 제출에 연결한다. FIFO는 프레임 시작에 큐 개수를 동결하고 소비 중 추가는 다음 프레임이다. 명령마다 before/after와 결과를 기록한다. 소유 시험 중 소비 token 없는 공개 명령/Simulate는 거부하고 사건을 남긴다. battle 활성1회, prep/pause0회를 호출 카운터와 교차 확인한다. RNG는 Next(max)/Next(min,max)/NextDouble 요청·응답·순번을 모두 기록한다.
- DR026-04/AT026: RT12~15는 접근/정지/반전/복귀와 잘못된 시작 거부를 나눈다. RT13의 외부 종점은 도착 필수 지점으로 사용하지 않는다. 진단 입력 생성과 인수 실행을 별도 run으로 만들고 인수 전 배열/해시·초기 actual/reserved·MC·deadline/stall/hold를 동결한다. 카메라는 16:9·넓은 화면·4:3을 검사한다. 구성공간은 Fcenter ∩ 각(Fbody-offset)이다. 전체 성분은 보수적 내부/외부 근사와 미해결 영역을 구분하여 조사한다. 좁은 통로/고립 성분이 미해결이면 BLOCKED이며 단순 격자 일치로 인증하지 않는다. 실제 추종 궤적 증거는 별도다.
- DR026-05: N2 계측 파일은 `DSMapPlayerProbe.cs` 및 `DSMapFullVerification.cs`, 출력은 프로젝트 output 내부다. Mac arm64/Metal·1280×720·vSync0·targetFrameRate60·품질 수준을 프로필에 고정한다. 각 부하 warmup10s/측정30s×3, 프레임/CPU/GPU·Mono/총 할당/GC·적/투사체·탐색 방문/실패를 기록한다. GPU counter 미지원은 NOT_RUN/BLOCKED이며0으로 대체하지 않는다. 로딩은 호스트 프로세스 시작 monotonic부터 Player가 출력한 첫 사용 가능 프레임 marker까지 측정한다. 10웨이브·정비·결과·재시작은 실제 경로이며 단축키 skip은 전체 런 합격으로 인정하지 않는다.
- DR026-06: 새 전체 맵 결과는 별도 accept/judge에서 필수 사례/원자료/소스/입력/정리 지문을 재검증하며 gate PASS와 별도로 보고한다. 정상·결과누락·변조·오래된 nonce·중단을 반증한다. N3 이후 Runtime/Editor/검사기/자료/기준 변화의 영향 범위를 기록하고, 범위를 증명 못하면 G2/N1/N2를 재실행한다. 충돌 hash 불변 외에 같은 배치·실제 동작·직접 화면·실제 성능 증거도 필요하다. 대표 구역 채택은 전체 G5와 구분한다.

읽기 전용 초기 질의(실행 인수 아님): `output/map-full-026/preliminary-route-query.json`. 기존 C# 질의로29경로×12파티/진형348행 중82행에 초기 점유/직선 구간 불안전이 관측됐다. 방향별 actual 추종·전체 입력 검증은 아직 아니다. 이를 근거로 경로를 삭제하거나 PASS로 바꾸지 않는다.

### 첫 기반 단위의 명시 계약

`map_full.py`의 첫 요청 역할은 `MAP_EXECUTION_REQUEST_026`이며 009 full map v1 완전 인수기가 아니다. 완전 snapshot/display/driver/FI consumer가 연결되기 전 prepare의 PASS는 strict 자료 인수만 뜻하고 acceptance_status=BLOCKED, execution_status=NOT_RUN을 함께 반환한다. judge도 원자료 해시 검사가 통과해도 runtime PASS를 반환하지 않는다. 이를 유지한 채 후속 adapter를 구현한다. full v1과 다른 초기 상태/필드를 조용히 변환해 승인하지 않는다.

### 실제 프레임 첫 연결 단위

전체 N1 전의 분리된 `OWNED_FRAME_ADAPTER_ONLY` 범위로 Editor 소유 바인딩을 추가한다. `DSGame.Update`의 최상위 분기에서 한 실제 Update당 한 고정 입력을 소비하고 같은 Simulate를 호출한다. 초기 DSGame/MapSession 바인딩은 기존 방식, 선택 인원·사례 초기 배치는 소유 인스턴스에만 기록된 초기화 사건이다. 모든 명령은 소유 token 구간에서 기존 도메인 메서드를 호출하고 직전/직후를 관측한다. 바깥 직접 명령/Simulate는 실패로 기록한다. UI/키보드의 Paused 직접 대입은 공통 Pause 명령 메서드로 교체한다. 일반 MapSession의 전체 명령 큐 이행은 후속이며 이 단위를 전체 G2로 표시하지 않는다.

매 프레임 DTO는 실제 frame/dt·고정dt·before/after·명령 기록·simulation calls·run/nonce/case를 가진다. snapshot에는 Config/Selected/Team/Stash/Enemies/Shots/Effects/History·모든 게임 scalar/private timer·RNG 요청응답 trace를 포함한다. UI toast/렌더/실제시간 누적은 명시 제외한다. prep/pause에서는 simulation_calls=0, battle 활성은1이다. 예상 스텝 수와 파일의 start/end marker·소스 지문·nonce를 Python이 대조한다.

### 구현 중 경계·지적 반영

- strict 기반은 자료 preflight/봉인 무결성 전용이다. 009 full v1·전체 G2/G3 어댑터로 승격하지 않는다. 중첩 manifest·링크·용량 초과·필수 사례 불일치·거대 정수·봉인 후 파일 치환 반례를 추가했다.
- 첫 프레임 연결은 `Tools/Verify/map_frame_run.py`와 `DSMapFullVerification.cs`가 소유한 개발 맵 사례19건이다. 독립 계산식으로 초기 인원/진형/생사, 예약 슬롯, 명령 결과·순서, 적용 dt, alpha와 추종·모든 몸체 sweep, RNG trace를 대조한다. 직접 RecordTime을 우회하므로 **실제시간 누적·전체 G2·외부 watchdog·전체 공간 연결·N1 완료 증거가 아니다**.
- 스냅샷의 Survivor/Gear 비직렬화 타이머는 별도 DTO로 기록한다. 전투 비활성 범위라 Enemy/Shot의 전체 상태 계약과 전투 RNG/캐시 증거는 후속 N2 연결 전까지 미구현이다. 일반 맵의 전체 큐 이행도 후속이다.
- 코드 검토 지적: 적용dt·초기진형·잘린 reserved/alive/team·명령 no-op·FIFO 거부·타이머 누락·cleanup 등록 시점·026 장치 집계·링크 경계 문제를 보완했다. 독립 재검토 진행 중이다.
- 최초 실제 실행은 경로 prefix의 중복 구분자로 BLOCKED, 두 번째는 렌더가 ViewWidth/Height를 갱신하는 관측 시점 문제로 FAIL이었다. 모두 원래 장면/선택/설정/파일 복구 PASS. 과거 실행을 덮지 않는다. 렌더 후 snapshot으로 정렬하고 오염 시 expected/observed를 함께 기록한다.
- 현재 C 후보의 불안전 직선 구간은 RT002/005, LC02/10에 남아 있다. RT009는 diagnostic, RT012~015는 boundary_required여서 안전 접근/정지·반전·귀환으로 별도 분류해야 한다. 원본 역할·실패를 삭제하지 않는다.
- 개발 Mac 실측: `output/map-full-026/development-mac.json`의 Apple M5 / 24GB / arm64 / Metal 지원. [Mac 프로필](map-full-026-mac-profile.json)은 측정 전 기준 제안이며 실제 성능/부하/빌드 인수 NOT_RUN이다.

### 정상 맵 큐 후속 단위 — MQ026

후속 구현은 일반 MapSession까지 명령 제출/소비를 연결한다. LegacyNoMap은 현재 동기 진입점을 유지한다. 소유 실제 프레임 바인딩과 정상 큐는 동시에 소비하지 않는다.

| 명령 | 소비 직전 허용 조건 | 결과 |
|---|---|---|
| ToggleSelection | lobby, 유효 캐릭터 ID, 선택/해제 또는4인 제한 검사 | 선택 목록 변경, 제한 거부는 불변 |
| StartRun | lobby, 유효·유일한1~4 Selected, 맵 자료/시작 점유 유효 | 1회 초기화, 이 소비 프레임 Simulate0, 다음 프레임부터 이동 |
| SelectFormation / CancelFormation | prep, 유효 진형, 전환 sweep 안전 | Pending 반영 또는 거부 |
| Revive | prep, 유효한 사망 슬롯, 전환/예약 sweep 안전 | 기존 실제 Revive 경로 |
| NextWave | prep, 유효 Team, Wave 범위, Wave<10은 전환 안전 | 기존 NextWave; Wave>=10 결과 경로는 지형으로 차단하지 않음 |
| Pause / Resume | battle | 공통 RequestPause 호출 |
| DebugFinish | battle이며 unpaused, 개발 단축키 | skipped 기록을 남기며 전체 런 증거에서 제외 |
| ReturnLobby | lobby 이외 | 기존 lobby 경로·대기 명령 취소 |

MQ-01: token은 상태 검사를 대신하지 않는다. 각 명령을 소비하기 직전 위 조건을 재검사하며 동일 FIFO의 앞 명령이 상태를 바꾸면 뒤 명령은 새 상태로 판정한다. 거부는 상태/RNG 불변·이유로 기록한다. StartRun 내부 NextWave는 소비 token의 내부 초기화 문맥이다.

MQ-02: UI는 제출만 한다. 초기 큐 소비는 소유 bootstrap의 명시적 1회 단계이며 Update 소비와 재진입/중복을 거부한다. bootstrap frame/event·큐 경계·simulation_calls0을 기록한다. 일반 StartRun 소비 프레임 역시 simulation0이다.

MQ-03: queue nonce는 DSGame 맵 세션 생성과 StartRun/ReturnLobby/Result 경계에 새로 발급한다. 이전 nonce의 대기/분리된 FIFO 잔여 명령은 취소 사건을 남기고 새 run에 재사용하지 않는다. 중복 ID 집합은 nonce 수명 동안 유지한다. 소비 중 예외는 finally로 token을 해제하고 나머지 명령/Simulate를 중단하며 실제 부분 상태를 보존한 FAIL로 기록한다. 바인딩 소실은 명시 세션 오류다. 고정 재생 중 외부 제출은 저장하지 않고 오염/취소 사건으로 중단한다.

MQ-04: 기존 Map022/023의 동기 단위 호출은 별도 Editor 소유 단위시험 scope로 한정한다. instance/run/nonce와 기존 DSMapMotionVerification 소유 root를 대조하고 disabled+VerificationRun, 중복/프레임 바인딩 배타성을 요구한다. 제한된 callback 수명과 finally 해제를 적용한다. 이를 실제 프레임·G2 증거로 인수하지 않는다. 일반 사용자 Play에서는 사용할 수 없다.

### 가상 장치 시험의 입력 설정 소유 범위

실제 Update/에디터 조회의 키 상태가 달라지는 원인을 설치된 Input System 소스에서 확인했다. 현재 설정은 `PointersAndKeyboardsRespectGameViewFocus`, `ResetAndDisableNonBackgroundDevices`, 앱 unfocused다. 이 설정은 Keyboard의 canRunInBackground를 강제로 false로 하므로 Dynamic 이벤트 주입만으로 장치가 활성화되지 않는다. 레이아웃 override로 프로젝트 정책을 우회하지 않는다.

소유 가상 장치 시험에서만 기존 `InputSystem.settings`의 참조와 직렬값을 보존하고 별도 transient clone을 만든다. clone의 editor input behavior를 AllDeviceInputAlwaysGoesToGameView, background behavior를 IgnoreFocus로 설정하여 소유 가상 키 이벤트가 실제 Update 입력 버퍼까지 도달하게 한다. 원본 설정 객체·자산·프로젝트 파일은 수정/저장하지 않는다. 이 값은 시험 실행 환경에 명시하며 실제 물리 기기 또는 기존 포커스 설정의 사용자 동작 인수로 표현하지 않는다.

clone 설치 전 intent·원본 참조/직렬값을 저장하고, cleanup에서는 현재 설정 참조/직렬값이 소유 clone과 일치하는지 확인한다. 불일치하면 사용자의 설정을 덮지 않고 BLOCKED다. 일치하면 원본 참조로 복원하고 clone을 해제하며 원본 직렬값 불변과 가상 장치/콜백 제거를 확인한다. 이 소유 범위는 동일 Editor에서 직렬 실행하며 기존 소유 root guard/부모 finally와 연결한다. 취소/예외에도 cleanup을 적용한다. 설정 복원을 못 하면 어떤 입력 검사 PASS도 최종 성공이 아니다.

IS026-01 보완: clone은 전역 런타임 입력 설정에 영향을 주므로 InputSystem.onEvent에서 등록된 소유 가상 장치 외의 새 입력 변경/텍스트 사건을 감지한다. 물리 장치를 끄거나 삭제하지 않는다. 혼입 시 payload/입력 내용은 저장하지 않고 장치 ID·종류·시각·취소 이유만 기록하며 소유 게임/관측을 중단한다. 실제 입력을 성공 결과에 섞지 않는다. 원래 장치 ID/활성 상태도 설치 전후 기록하고 예기치 않은 차이는 BLOCKED로 남긴다.

IS026-02 보완: clone 설치 전에 프로젝트 output의 새 input-scope journal에 원본/clone의 instance ID·원본 asset 식별 가능 여부·원본 직렬값/hash·clone 기대값·run/nonce/Editor PID와 PLANNED를 기록한다. 설치 후 INSTALLED, 복원 후 RESTORED, 충돌은 CONFLICT다. beforeAssemblyReload와 ExitingPlayMode에서도 현재 소유 참조/값이 검증될 때만 복원을 시도한다. reload 이후 안전하게 재식별할 수 없는 미완료 journal은 새 scope를 BLOCKED로 막고 보존한다. 원본 직렬값 변경도 복원 전 검사한다. 추측한 설정 교체·clone 삭제·미완료 journal 자동 성공 처리는 하지 않는다.

### IS026의 실제 취소/재로딩 대조군

일반 입력 성공과 별도로 소유 격리 게임에서만 가상 외부 장치의 사건을 주입해 혼입 탐지를 검증한다. 새 run manifest에 IS026-FOREIGN-DEVICE mutation과 예상 오류/장치 조건을 먼저 고정하고, scope journal에 실제 주입 장치 ID를 사건 전에 기록한다. 그 장치는 시험이 소유하지만 입력 허용 장치 집합에는 등록하지 않아 OnInput→취소→게임/합성 드라이버 중단을 실제로 통과시킨다. 실제 사용자 장치 사건을 주입 대조군으로 승격하지 않는다. 이벤트 직후 fixture 장치는 finally로 제거한다.

대조군의 expected detection PASS와 cleanup PASS는 일반 입력 PASS와 구분한다. 부모는 명시된 mutation·주입 ID·실제 검출 ID가 모두 일치하는 scope의 EXTERNAL_DEVICE_INPUT만 예상 취소로 인수할 수 있다. 일반 runner는 여전히 같은 오류를 성공으로 인수하지 않는다. 원본 설정·장치·콜백·원래 장면 복원은 실제로 확인한다.

재로딩 대조군은 소유 scope를 설치한 채 정상 소유 Play 종료를 진행해 ExitingPlayMode/beforeAssemblyReload 복원 경로를 관찰한다. 소유 종료·입력 복원 journal·정지 후 원본 설정 직렬값과 가상 자원0을 교차 확인한다. 임의 사용자 Play 중지 또는 소유권 불일치를 자동 정리 성공으로 처리하지 않는다. probe는 별도 Editor 클래스/runner로 만들고 기존 일반 runner의 성공 기준을 낮추지 않는다.
