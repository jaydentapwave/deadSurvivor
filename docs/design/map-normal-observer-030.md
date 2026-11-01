# 030 — 일반 MapRequired 프레임 관찰 연결

- 상태: bounded 구현/검증 PASS. L2: 정상 Update/명령 큐·시간 소유권 경계에 관찰 hook을 연결한다. [실제 검증](../reviews/map-normal-observer-030-review.md)을 따르며 전체 v1/G2/G3와 구분한다.
- 요구: [026](map-full-026.md)과 [009](map-harness-implementation.md)의 남은 일반 경로 관찰을 구현한다. N2 빌드·부하 시험은 사용자 실행으로 제외한다. [028 체크포인트](../../output/map-art-028/final-checkpoint.json)는 보존한다.
- 원본 상태: 026의 `DSMapFrameBinding`은 Update 최상위 분기에서 별도 큐로 실행하고 RecordTime을 건너뛴다. 이를 정상 명령 큐나 full map v1 증거로 승격하지 않는다.
- 메인이 구현한다. 독립 설계/계산/코드 검토는 읽기 전용이다. 기존 LegacyNoMap·017·026 경로를 유지한다.

## 첫 연결 범위

첫 단위의 역할은 `NORMAL_MAP_FRAME_OBSERVER_030`이다. 정상 `AdvanceNormalMapFrame`을 사용하고 기존 임시 MapSession/몸체8/원본018을 유지한다. 별도 command executor나 직접 Simulate 반복을 만들지 않는다. 현재 전체 v1 snapshot·FI01~10·외부 감독·708사례 인수의 완료 기록이 아니다. 이 후속 작업도 전체 요청에서 계속 수행해야 한다.

변경 예정: Runtime `DSGame.MapObserved.cs`와 `DSGame.MapCommands.cs`, `DSGame.cs`, `DSGame.MapMotion.cs/MapFrame.cs/Formation.cs/Combat.cs`; Editor 관찰 생산자; `Tools/Verify/map_observed_run.py`와 시험. Game 상태 작성은 계속 DSGame만 담당한다.

1. 소유 root/게임/세션/장치와 실행 ID를 등록한 disabled game에만 observer를 bind한다. 기존 mapFrame/movementFixture와 상호 배타적이다. 입력 배열/명령 ID는 복사·검사하여 고정한다.
2. `AdvanceNormalMapFrame` 시작 hook은 이전 종료 snapshot과 현재 상태를 대조하고 그 프레임의 사전 등록 TEST 명령만 기존 Enqueue로 제출한다. UI/키보드의 기존 경로는 유지한다. 고정 재생 중 외부 제출은 취소/FAIL이며 조용히 무시하지 않는다.
3. RecordTime은 실제 dt로 원래 위치에서 실행한다. VerificationRun의 기존 통계 건너뜀은 **등록된 이 observer에 한해** 해제한다. Legacy/기존 검증은 원래 동작이다. RecordTime 직후 snapshot을 추가하여 명령 원자성 비교와 실제시간 변화를 섞지 않는다.
4. FIXED_REPLAY는 ReadMovementInput의 수집 값과 Simulate에 전달할 supplied dt만 소유 입력으로 바꾼다. ClampMagnitude/min(dt,.05)×speed/남은 Duration clamp·상태 검사·일반 큐 소비·World.Render/UI.Tick은 동일 경로다. DEVICE_SMOKE는 소유 Keyboard의 Dynamic 이벤트와 실제 dt를 사용한다.
5. DSGame의 정상 프레임/Simulate 카운터를 관찰기와 별도로 증가시켜 expected driver/observer/actual-call 수를 대조한다. 새 계측 필드는 게임 의미 snapshot에 섞지 않는다. 활성 battle은1회, pause/prep는0회다. 첫 단위는 전투 비활성으로 combat-call0을 확인한다.
6. 종료/오류 시 게임을 disabled로 고정하고 실제 raw/error만 기록한다. Game 쪽이 case PASS를 작성하지 않는다. terminal 이후 값/명령 변화도 거부한다. 직접 명령/Simulate 우회가 허용되지 않는다.

## 명령·시간 판정

정상 큐의 기존 `MapCommandEvents`를 초기화 offset 이후부터 관찰한다. command ID/종류/value/source·nonce·enqueue/consume frame·순서를 기록하고 직전/직후 snapshot을 독립 검사한다. 기존 거부 이유(NOT_DEAD 등)를 사용하며 특별 분기에서 만든 거부 결과로 바꾸지 않는다. 처리 중 제출은 다음 프레임이라는 frozen cut을 유지한다.

continue_on_rejection=false의 현재 프레임 후속 명령은 명시적으로 취소 사건을 남기고 이후 프레임에 몰래 실행하지 않는다. observer 없는 일반 큐는 기존 의미를 유지한다. source TEST를 허용한다는 이유만으로 외부 TEST 제출을 승인하지 않고 현재 driver 제출 범위/ID도 검사한다.

RecordTime은 명령 처리 **전 상태**에 실제 dt를 누적한다. 따라서 Pause를 소비하는 프레임의 실제시간이 이전 battle 상태로 기록되는 현재 의미를 독립 판정에 명시한다. 재현 비교에서는 버전 고정한 실제시간/렌더 필드만 제외하고, 명령 전후 원자성은 같은 명령 사이 값으로 비교한다. 요청/적용 dt와 시뮬레이션 시간은 별도로 검사한다.

## 보존·실패·시험

기존 owned scene/PID/nonce/lock/입력 설정 scope·정리를 재사용한다. 소유 초기화에서 StartRun/FinishWave/SelectFormation/NextWave/실제 Damage를 명시 사건으로 기록한다. State 문자열을 시험 명령으로 직접 쓰지 않는다. 장애물/원본 데이터·기존 저장 씬·hook·baseline을 바꾸지 않는다.

12인원/진형 + pause/prep + 실제 생사/예약 슬롯 + 거부/후속 정책 + 최소/최대dt·시간초과 + 동일 입력 재현 + DeviceSmoke + 부정 외부 호출을 사전 동결한다. 실제 Update 번호와 공급/실제 dt를 모두 기록한다. 부모의 finally 정리를 외부 worker 강제 종료 복구로 부르지 않는다. 단위→독립 코드/계산 검토→실제 실행/원자료 판정→현재 회귀/local/gate를 수행한다.

## 다음 필수 연결의 기준 (미완료 유지)

- full v1 parser는 [데이터 계약](map-harness-data-contract.md)의 전체 initial/config/HP/gear·geometry/adoption·threshold/assertion/display·command 필드를 strict 인수해야 한다. 026의 부분 요청을 이름만 바꾸지 않는다. 실제 게임에서 해소한 초기값은 진단/resolve 뒤 인수 실행 **전에** 별도 manifest로 동결한다.
- G2 분석 도형의 .25/1 반경은 실제 공유 이동/전환 질의에 body profile로 전달한다. 기본 G3/기존 씬은 몸체8을 유지하고 바인딩 숫자는 불변 복사한다. G2 fixture 입력은 Editor 소유 초기화에서만, G3 데이터와 혼용하지 않는다.
- full snapshot은 비직렬화 위치/타이머 외에 Enemy/Shot/Effect 및 참조/집합도 전용 DTO로 보존해야 한다. JsonUtility가 Serializable이 아닌 Enemy/Shot을 빠뜨리는 것을 성공으로 처리하지 않는다. driver metadata는 제외 목록으로 고정하고 정수/ID·순서는 정확 비교한다.
- 상주 외부 supervisor는 worker heartbeat·게임 frame/Simulate·observer sequence를 별도 감시한다. 강제 종료/시간초과 뒤 marker/nonce/PID 증명으로만 복구하며 pending/부분 원자료를 보존한다. 실제 사용자 자원은 FI07의 고의 실패 대상으로 쓰지 않는다.
- FI01~10은 각 정상 대조와 기대 오류의 정확한 일치를 요구한다. 메타데이터 변조는 새 실제 native 대조 결과의 격리 사본으로 시험한다. observer 정지는 게임/driver 카운터가 계속 진행한 증거와 비교한다. 전체 cleanup 실패를 기대 FI로 상쇄하지 않는다.
- 708개 RT/LC/연속 재고는 출발·차단/진단/경계 접근 기준과 부족한 MC 사례를 추가해야 한다. 진단 autopilot 입력과 인수 tape를 분리하고 실제 연속 구간에 초기화/위치 점프가 없음을 대조한다. 계획용 격자는 입력 생성에만 쓰고 전체 연결 인증으로 쓰지 않는다. 통과 실패를 사후 삭제/진단으로 바꾸지 않는다.

## OBS030-01~03 보완

- OBS030-01: 첫 시작은 초기화+Render/Tick 후 동결 snapshot과 대조한다. 매 프레임은 시작 → RecordTime 직후 → 명령 각각 전/후 → Simulate 전/후 → World.Render/UI.Tick 후 종료 순서다. LateUpdate의 재질 교체·캔버스/폰트 캐시는 게임 의미 snapshot에서 제외하며, gameplay 변경은 별도 사건 없으면 오염으로 거부한다.
- OBS030-02: 정상 경로의 모든 조기 반환·예외에 observer의 실패/종료 통지를 둔다. 원래 게임 오류와 관찰/기록 오류를 별도 원인으로 보존한다. token은 finally로 해제하고 실제 부분 상태를 기록한 뒤 게임을 중단한다. 실패 프레임은 completed count에 넣지 않는다. 기록 파일이 실패해도 Progress의 error/driver/game/observer 카운터로 누락을 탐지한다.
- OBS030-03: 첫 단위 snapshot 버전은 기존 DSMapFrameSnapshot의 명시 필드 및 Survivor/Gear의 비직렬화 timer/actual/reserved·두 RNG seed/count/trace를 사용한다. HP/maxHP/장비/stash/보상/history/config와 모든 현재 scalar/private timer를 포함한다. 실제시간은 별도 ledger로 기록하고 재현 제외 목록을 고정한다. 현재 Enemy/Shot/Effect의 비완전 직렬화를 full snapshot이라고 부르지 않고, 첫 단위는 Enemies/Shots/Effects가 존재하면 BLOCKED로 거부한다. 실제 Damage가 만든 초기 burst는 명시 초기화 사건으로 소유 정리하고 전투 객체 없는 초기 상태를 다시 동결한다. 미지원 assertion/상태는 BLOCKED다.
- 기존 MQ026에서 StartRun/ReturnLobby를 소비한 프레임은 활성 battle이어도 Simulate0회이며 이 예외가 일반1회 규칙보다 우선한다. observer 없는 경로의 순서는 유지한다.

## 구현 중 범위와 후속 제약

첫030 tape는 Pause/Resume/SelectFormation/CancelFormation/Revive/NextWave만 받는다. StartRun/ReturnLobby는 초기화의 소유 도메인 호출 외에는 BLOCKED다. 정상 큐의 수명/nonce 회전 구현을 첫030 관찰 인수로 승격하지 않으며 후속 lifecycle tape에서 별도로 검증한다. continue_on_rejection=false는 사전 거부와 도메인 기하 거부 모두 남은 cut을 CANCELLED:REJECTION으로 기록하고 해당 프레임의 시뮬레이션을 건너뛴다.

실제시간은 before→afterTime ledger뿐 아니라 모든 명령/시뮬레이션/렌더 뒤에도 불변을 대조한다. NextWave의 WaveReal/WavePaused=0만 명시적으로 허용한다. 첫 단위는 웨이브 종료 경계를 인수하지 않으며 clock/history 구조 변경을 거부한다. 초기 queue nonce/event sequence/event offset 및 게임 frame/Simulate 카운터를 원자료에 고정해 첫 관찰도 대조한다. 전체 v1의 웨이브 경계·전체 스냅샷·감독/FI는 미완료다.
