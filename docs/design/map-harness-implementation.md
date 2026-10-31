# 맵 검증 도구 구현 설계

구현 상태 보충(017): [이동 핵심과 실제 프레임 시험](map-i2-017.md)은 §5 지면 질의/257후보와 DSGame 연결을 MOVEMENT_FIXTURE_ONLY로 구현했다. 별도 CLI에서 실행하며 full map v1·명령 큐·상태 전환·전체 FI/G2·외부 watchdog 완료는 아니다.

구현 상태 보충: [016 I1 로컬 기록 계층](map-i1-016.md)은 fixture 전용 manifest/journal/수집·중단/무결성 기반을 구현했다. 이 문서의 실제 Unity 세션·입력·이동·복구·G2 전체 계약은 아직 미구현이며, I1은 full map v1 실행 요청을 인수하지 않는다.

작업 ID MAP-HARNESS-009 · 개정 r002 · 2026-10-31 KST

상태: r001의 독립 설계/계산 검토 PASS는 [009 검토 기록](../reviews/map-harness-009-review.md)에 보존한다. r002는 §9의 자산 연결 설계만 보충하며 기존 기하·map v1 계약은 유지한다. 현재 [010 H1 기초 구현](map-harness-h1-foundation.md)에서 strict JSON/경로·단순 polygon·독립 circle sweep 분석을 구현했다. 전체 데이터 인수·집계·Unity 실행 하네스는 미구현이다. 범위는 [착수 명세](map-first-implementation-spec.md)의 I0~I3 및 I4/I5로 넘길 데이터 계약이다.

## 브리핑과 적용 범위

- 규모 L2: 입력 소유권·실행 생명주기·관찰 데이터 계약·게임/판정기 경계를 정한다. 구현 전에 design-reviewer와 계산 반례 검토를 받고, 작성된 문서의 정합성은 validator-client가 확인한다.
- 요구: 실제 게임 코드로 이동/상태 전환을 실행하고 독립된 관측으로 정상/오류를 구별하며, 동일 입력 재현과 취소/복구를 입증한다.
- 규칙 원본: [007 MP01~10](map-meadow-007-r001-policies.md), [007 실행 계약](map-meadow-007-r001-harness.md), [단계별 MC/FI](맵-워크플로우-하네스-설계.md), [FI 상세](맵-하네스-사례명세.md). 본 설계는 구현 경계와 데이터 형식을 구체화하며 필수 사례를 줄이지 않는다.
- 이번 변경: 본 설계, [데이터 계약](map-harness-data-contract.md), [시험용 자료](map-harness-fixtures.json), 최신 입구·검토 기록. 게임 코드/실제 Unity/패키지 변경 없음.
- 구현 시 변경: 아래 파일 책임표. 콘텐츠 원본 PrototypeData와 DSGame의 상태 소유권을 유지한다. 회사 스크립트/외부 서비스/전역 설정은 사용하지 않는다.
- 재사용: 기존 Unity 대상 제한·소스 비교·compile/규칙/화면 확인의 원칙과 DSGame의 이동/진형/상태 진입점. 기존 실험기의 전체 실행/복구 코드는 새 시험 환경에 그대로 복사하지 않는다.
- 실패/생사 경계: §4·§7. 부분 상태 변경, 관찰 누락, 기록 실패, 사용자 개입을 PASS로 만들지 않는다.
- 검증: 설계 데이터 정적 검사→독립 설계/계산 검토→지적 수정→문서 독립 검토→gate. 미래 구현에서는 정상/실패주입·실제 입력/프레임·복구 및 기존 게임 회귀 검사가 필요하다.
- 미정: G3 실제 C/몸체/s/k/시간·가림 기준은 CAL00~05에서 채택한다. G2 fixture에는 별도 치수/오차가 정의되어 있어 이 미정이 도구 구현을 막지 않는다.

## 1. 확인한 현재 코드와 대상

[읽기 전용 비교](../../output/verification/map-harness-design-20261031-130446/source-comparison.json): Runtime/Editor C#와 PrototypeData 24개가 개인 Unity 대상과 동일. ProjectVersion은 6000.4.3f1, manifest에는 Input System 1.19.0·Test Framework 1.6.0이 기록돼 있다. 설치/컴파일/API 사용 가능성을 실행으로 확인한 것은 아니다. 이 기록은 구현 시작 때 다시 확인한다.

| 현재 위치 | 관찰한 동작 | 새 설계에서 필요한 연결 |
|---|---|---|
| [DSGame.Update](../../UnityMigration/Runtime/DSGame.cs) | 실제 입력, ClampMagnitude, min(frameDt,.05)×speed, Simulate, Render/Tick | 공유 프레임 진입점과 입력 소유권, 전/후 관찰 |
| [Simulate](../../UnityMigration/Runtime/DSGame.Combat.cs) | 남은 시간으로 dt 제한, 적 생성, Center 이동, 생존자 Lerp, 장비·적·투사체 진행 | 이동 계산 결과 원자적 반영, 이동 전용 시험의 전투 차단 |
| [Formation](../../UnityMigration/Runtime/DSGame.Formation.cs) | 오프셋과 Pending 변경, VerificationRun이면 전투/정비 실제시간 기록 생략 | 오프셋 재사용, Pending 사전 검사, 외부 관찰기로 wall time 기록 |
| [Run](../../UnityMigration/Runtime/DSGame.Run.cs) | StartRun 초기화, NextWave의 Stash/RNG/상태 변경, FinishWave 보상 | 변경 전에 전환 검사, 초기화 순서·난수 추적 |
| [Revive](../../UnityMigration/Runtime/DSGame.Inventory.cs) | HP/좌표/장비/이력 변경 | 사전 검사 후 한 번에 반영 |
| [기존 실험기](../../UnityMigration/Editor/DSExperiments.cs) | 동기 반복 Simulate, 일부 상태만 복구 | 논리 실험 선례. 실제 프레임/입력 증거로 전용하지 않음 |
| [기존 Unity 검사](../../Tools/Verify/unity_check.py) | 대상/미저장/Play 상태 확인, 실제 프레임·캡처 | 직렬 실행과 새 결과 형식 연결. 기존 검사는 유지 |

## 2. 구현 파일과 단일 소유권

아래는 **009 설계 당시 예정 경로/이름**이다. 현재 map_oracle.py와 map_check.py는 010의 분석 범위만 구현됐으며 표의 전체 책임을 완수한 것으로 해석하지 않는다. Runtime에서 Editor/Python 타입을 참조하지 않는다.

| 예정 파일 | 책임 / 외부 계약 |
|---|---|
| `UnityMigration/Runtime/DSGame.Frame.cs` + 기존 Update 수정 | `AdvanceFrame(FrameRequest)`가 상태 명령→이동 소비→Simulate→관찰을 한 번만 수행. DSGame이 유일한 상태 작성자 |
| `UnityMigration/Runtime/DSGame.Map.cs` | `BindMap(MapGeometry, BodySet)`, `CaptureMapState()`, 안전 이동/전환 질의. Binding은 초기화/명시적 시험 사건에서만 변경 |
| `UnityMigration/Runtime/DSMapGeometry.cs` | 불변 논리 좌표 다각형/경계와 순수 질의. 게임용 C# 이동 검사, 입력을 바꾸거나 좌표를 직접 쓰지 않음 |
| `UnityMigration/Runtime/DSMapMotion.cs` | MP04 후보 계산과 검증, `MotionProposal` 반환. 적용은 DSGame에서 수행 |
| 기존 `DSUI.cs` / `DSUI.Formation.cs`와 Update 단축키 | Pause/Resume/Revive/NextWave/SelectFormation/취소 콜백을 공통 명령 큐 제출로 변경. UI에서 게임 상태 직접 변경 금지 |
| 기존 Formation/Run/Inventory | 유효성 확인→기존 명령 실행. UI와 시험 명령이 동일한 진입점을 호출 |
| `UnityMigration/Editor/DSMapHarness.cs` | Editor 작업 소유권·격리 씬·play 시작/종료·복구, 세션 ID로 특정 게임 인스턴스 바인딩 |
| `UnityMigration/Runtime/DSMapTestDriver.cs` | `UNITY_EDITOR` 조건의 Play Mode 입력/기록 드라이버. 한 실제 프레임에 한 시험 스텝; Player 빌드에는 포함하지 않음 |
| `Tools/Verify/map_oracle.py` | 저장된 도형·실제 궤적으로 Python double 판정. 게임 C# 기하 함수를 호출/번역 공유하지 않음 |
| `Tools/Verify/map_check.py` | 자료 인수·Editor 실행 중계·원자료 수집·독립 판정·최종 상태 집계. 순수 검사/실행 모드 분리 |
| `Tools/Verify/run.py` | 후속 `map` 단계 연결. 기존 local/all의 의미를 조용히 바꾸지 않음 |

외부 계약은 이름보다 아래 정보와 순서가 중요하다. C# 메서드/DTO 이름을 바꾸면 소비처와 문서를 같은 개정으로 바꾼다. `MapGeometry`는 준비 layout을 직접 수정하지 않고 인수된 실행 묶음에서 생성한다. 관찰기는 게임 상태를 복사해서 읽으며 성공을 위해 좌표·HP를 조정하지 않는다.

### 맵 활성화와 기존 회귀 호환성

DSGame의 명시적 `map_mode`는 `LegacyNoMap` 또는 `MapRequired`다. 기존 플레이·기존 DSVerification/DSExperiments는 LegacyNoMap으로 원래 이동/상태 전환을 유지한다. MapRequired는 맵 세션 생성 시 고정하며 유효 geometry/body/정책/개정이 모두 binding되기 전 StartRun을 BLOCKED로 거부한다. binding 누락을 자유 지면으로 해석하거나 실행 중 LegacyNoMap으로 fallback하지 않는다. mode 변경은 세션 해제 후 새 초기화에서만 가능하다.

필수 대조: LegacyNoMap에서 기존 all 회귀 / MapRequired+완전 자료 정상 / geometry 없음·body 누락·개정 불일치 각각 BLOCKED / 실행 중 binding 소실은 오류 기록 후 세션 중단. G2 fixture와 G3 map은 모두 MapRequired이며 자료의 출처만 다르다.

## 3. 입력과 시간 — 두 실행 방식

### 공통 프레임 경로

1. Update가 실제 프레임 번호/실제 dt를 읽는다. UI와 외부 장치는 입력 수집만 한다.
2. 입력 소유자가 `FrameRequest`를 제출한다. 일반 플레이는 현재 키보드/조이스틱 경로, 시험 중에는 명시적으로 획득한 세션이 소유한다. 시험 실행이 활성화된 프레임에서 일반 입력으로 MoveInput을 다시 덮지 않는다.
3. DSGame이 명령 전 snapshot을 기록한 뒤 한 프레임의 명령을 순서대로 처리한다. 이후 battle이고 unpaused일 때만 이동 입력 clamp와 요청 dt 계산을 수행한다.
4. Simulate가 적용 dt를 정하고 기존 게임 처리를 수행한다. 이동 전/후, 전환 전/후를 관찰한다. 정비/일시정지는 실제 프레임은 진행하되 simulation step이 0임을 기록한다.
5. World.Render/UI.Tick 후 화면이 반영된 프레임의 캡처를 연결한다. 캡처에 관련 `frame_id`와 직전 `step_id`를 기록한다. 같은 프레임의 추가 Simulate 호출은 감지해 `UNEXPECTED_STEP` FAIL.

명령 큐는 UI 콜백·키보드 단축키·시험 드라이버가 `{command_id,enqueue_frame,sequence,source,session_nonce,payload}`를 제출하는 하나의 진입점이다. AdvanceFrame 시작 시 큐를 잘라 그때까지 들어온 명령만 sequence 오름차순으로 소비한다. 처리 중 들어온 명령은 다음 프레임에 처리한다. command_id는 세션 안에서 유일하고 중복 소비는 FAIL, 오래된 nonce/다른 소유 token은 거부한다. FIXED_REPLAY는 사전 등록 입력열만 받으며 외부 UI/입력은 취소 사건으로 처리한다. DEVICE_SMOKE는 소유 장치에서 비롯된 UI 콜백을 받아 같은 큐로 처리한다. 큐 한도와 넘침 거부는 request에 사전 지정하며 숨은 재시도는 없다.

기존 공개 SelectFormation/Revive/NextWave는 동기 domain 명령 본체를 유지하고 큐 소비자가 호출한다. LegacyNoMap의 기존 규칙 검사 직접 호출은 계속 동작한다. MapRequired 세션에서는 명령 소비 token 없이 호출하면 거부하고 관찰 사건을 남긴다. StartRun의 내부 NextWave는 INITIALIZING 소유 token에서만 허용한다. UI의 기존 직접 호출/Paused 대입과 Update 단축키를 모두 큐 제출로 바꾸고, normal/DEVICE_SMOKE에서 동일한 명령 순서를 검사한다.

`AdvanceFrame`는 State/Paused/명령 유효성까지 공통 처리한다. 기존 공개 Simulate를 사용하는 일반 규칙 시험은 유지하되, 맵 세션에서는 사전 등록한 driver token과 관찰 hook 없는 직접 호출을 거부한다. 게임 실행 중 반사 호출로 우회해서 시험을 진행하지 않는다.

| 모드 | 시간/입력 | 증명하는 범위 |
|---|---|---|
| `FIXED_REPLAY` | 한 실제 프레임에 고정된 입력열 한 행. `frame_dt_supplied`를 사용하되 실제 frame_dt도 별도 저장 | 같은 소비/Simulate 경로의 재현·정상/실패 검출. 실제 기기 입력 성능은 별도 |
| `DEVICE_SMOKE` | 시험 장치/입력 이벤트를 기존 입력 수집 경로에 전달. 실제 unscaledDeltaTime 사용 | 키보드/조이스틱·UI 명령이 공통 경로까지 도달함, 실제 프레임·화면 연결. 고정 dt 재현으로 주장하지 않음 |

공식 입력 테스트 API의 구체적 호출은 구현 시 설치 버전에서 확인하고 unity-cli 스킬을 따른다. 장치 입력을 전달할 수 없으면 DEVICE_SMOKE는 BLOCKED다. Button.onClick 직접 호출만으로 장치 입력 PASS를 주지 않는다. G2 인수에는 두 모드의 증거가 모두 필요하다.

시간 필드: `actual_frame_dt`, `frame_dt_supplied`, `config_speed`, `dt_requested=min(frame_dt_supplied,.05)*speed`, `dt_applied=min(dt_requested,max(0,Duration-Elapsed))`. 정비/일시정지는 requested/applied=0과 no-simulation 이유를 기록한다. NaN/음수/비유한 speed·dt는 실행 전 거부. FIXED_REPLAY는 dt 스케줄과 배속을 함께 고정한다. 실제시간·CPU시간은 재현 비교의 동일값 요구에서 제외한다.

일반 입력 혼입이나 사용자의 장치 조작은 조용히 무시하지 않고 세션 취소 사유로 기록한다. 자동 DEVICE_SMOKE 장치는 세션 소유 장치로 식별하며 사용자 장치와 구분한다. 직접 사용자가 정지한 Play는 자동 재시작하지 않는다.

## 4. 게임 동작 연결과 실패 원자성

`FrameRequest.commands`는 선언된 순서의 배열이다. 각 명령 전후 상태/결과를 따로 저장하고, 실패한 명령의 후속 명령을 계속할지는 사례에 `continue_on_rejection`으로 사전 지정한다. 같은 스텝에 숨은 재시도 없음.

| 명령 | 공유 진입점과 검증 | 실패 시 불변 |
|---|---|---|
| SelectFormation | 현재/예정 슬롯과 모든 전환 구간 검사 후 Pending 반영 | Pending 포함 게임 상태, UI 오류 표시는 허용 |
| CancelFormation | 현 Formation을 SelectFormation 경로로 선택 | 무효라면 Pending 보존, 기존값을 조용히 덮지 않음 |
| Revive | 현 슬롯 안전성 검사 후 기존 Revive 처리 | HP/좌표/장비 cooldown·shield·공급자·History |
| NextWave | Wave<10 전환 경로는 Stash.Clear/RNG 사용보다 먼저 검사 | Wave/State/시간/적/효과/장비/HP/보관함/이력/난수 호출 |
| Pause/Resume | 일반 입력의 동일 상태 명령 | 잘못된 상태면 거부 사유, 시간·위치 불변 |
| FixtureDamage / FixtureInvalidateMap | Editor 소유 세션에서만 등록된 실패주입. 사망은 실제 Damage 경로에 위임 | 일반 Player/UI에 노출하지 않음, 변경 사건·전후 map hash 기록 |

단일 main-thread 명령에서 검사와 commit 사이 await/프레임 전환 없음. 실패 원자성은 **유효성 거부**에 대한 계약이다. commit 도중 예외는 예외 FAIL과 실제 부분 상태를 보존하고 전체 격리 세션을 폐기한다. 무조건 rollback 성공을 주장하지 않는다. NextWave의 Wave>=10 결과 전환은 지형 전환으로 막지 않고 기존 Result 경로 회귀를 검사한다.

진형 전환은 현 실제 생존자 위치→새 목표 구간과 현 예약→새 예약 구간 모두 검사한다. 성공 후 Formation만 commit하고 실제 위치는 다음 이동/0입력 추종으로 수렴한다. 부활의 과거 시체 위치→현 슬롯은 명시적인 부활 사건이며 생존자 이동의 sweep으로 잘못 판정하지 않는다. 새 슬롯은 반드시 안전해야 한다.

### 이동 전용 시험과 난수

격리 인스턴스에만 `MovementOnly` 실행 설정을 넣는다. Simulate의 이동·시간·명령 경로는 공유하고 Spawn/Burning/Recycle/장비 tick/Enemy/Shot/공격 진행을 명시적 분기에서 제외한다. 그냥 적 목록을 매 프레임 지우는 방식은 금지한다. 설정과 호출 카운터를 기록하고 전투 하위 호출이 1회라도 발생하면 `COMBAT_LEAK` FAIL. 피해 주입/정비 보상 등 명시적 상태 사건은 별도 명령 기록으로 허용한다. 일반 플레이 기본값은 전투 활성, 기존 회귀로 보존 확인한다.

새 소유 게임 인스턴스마다 Config를 콘텐츠에서 새로 복제하고 Rng/LootRng를 각 seed로 **StartRun 전에** 설치한다. 초기화·MakeItem·NextWave의 난수 사용을 전부 기록한다. 기존 `System.Random` 알고리즘은 바꾸지 않고 시험 전용 추적 어댑터가 현재 사용되는 Next(max), Next(min,max), NextDouble의 요청/응답/순번을 기록한다. 추가 overload/직접 RNG 호출이 발견되면 인수 차단 후 계약 확장. 각 거부 명령 전후 호출 수/trace hash가 같아야 한다. opaque RNG 상태를 직렬화할 수 있다고 가정하지 않고 동일 환경·seed·초기화 순서·전체 호출열로 재현한다. 사용자의 게임 인스턴스/RNG를 빌려 쓰지 않는다.

## 5. 이동 해결기와 독립 관찰

### 첫 구현 알고리즘 선택

MP04에 맞추어 alpha 후보 `j/256`, j=256..0을 큰 값부터 유한 탐색한다. 각 후보의 Center·생존자 Lerp 끝점·모든 예약 슬롯의 **전체 선분**을 확인해 첫 안전 후보를 선택한다. 앞 후보가 실패했다고 더 작은 후보까지 실패한다고 가정하지 않는다. 최대 257후보를 모두 확인할 수 있는 첫 정확성 기준이며 최적 성능을 주장하지 않는다. 안전한 연속 alpha를 놓칠 수 있으므로 이 알고리즘은 전역 최대 이동 증명이 아니다. 정체/성능 실패 시 새 개정으로 개선한다.

alpha=0에도 추종이 움직인다. 어떤 후보도 안전하지 않으면 Center와 생존자 실제 위치를 모두 그대로 유지한다. 현재 실제/예약 점 자체가 무효인 시작 상태는 `INVALID_INITIAL_OCCUPANCY` FAIL로 종료한다. 이동 없는 보류는 유효하지만 도달·진행 조건까지 충족하지 않으면 사례 PASS가 아니다.

C# 질의는 circle 몸체와 불변 다각형에 대해 선분 sweep을 수행한다. 초기 내부 검사 + 경계 edge까지 선분 최소거리로 circle sweep을 검사한다. 오목 형상은 convex hull로 바꾸지 않는다. 외곽은 닫힌 polygon 내부에 몸체 전 구간이 포함되어야 한다. 첫 버전은 circle 몸체만 지원하며 다른 몸체 형식은 BLOCKED. r_i와 geometry_epsilon은 묶음 필수값이다. 안전 계산은 아래 정의의 몸체 여유 G>=2×epsilon을 요구하여 oracle 안전 범위 G>epsilon 안에 둔다.

### 독립 판정 방식

Python 판정기는 게임의 alpha/접촉 목록/성공 flag와 무관하게 시작·끝 실제 위치와 저장된 geometry를 읽는다. 자체 double 기반 segment 교차, 점의 polygon 포함, segment-edge 최소거리로 circle의 전체 이동 구간을 판정한다. 게임 계산 함수를 import하거나 변환된 접촉 목록을 정답으로 쓰지 않는다. 동일 수학 문제인 만큼 공통 오류 가능성은 FI 및 분석 정답 도형으로 추가 검증한다.

거리 정의를 고정한다. 장애물에 대한 중심점 signed distance d(p)는 고체 밖에서 경계까지 양의 거리, 내부에서 음의 거리, 경계에서0이다. 외곽 경계에 대해서는 지면 내부가 양수, 지면 밖이 음수다. 몸체 여유는 `g(p)=d(p)-r_i`, 선분 sweep 여유는 `G=min(t∈[0,1], 모든 장애물/외곽) g(p(t))`다. 중심 거리 d를 몸체 여유 g로 잘못 보고하지 않는다. 초기 점은 길이0인 선분으로 같은 판정을 쓴다.

oracle은 선분과 polygon의 교점을 기준으로 안/밖 구간을 분류한다. 자유 지면 안에만 있는 구간은 segment-edge 최소거리에서 r_i를 뺀다. 고체 내부/외곽 밖 구간이 있으면 해당 구간의 내부 witness 점과 signed gap을 기록하여 관통을 증명한다. 관통이 확인된 경우 정확한 최대 침투 깊이는 필요 없으며 `clearance_kind=witness_bound`로 표시하고 이를 전 구간 최소 여유 실측값으로 보고하지 않는다. PASS에는 전 선분의 최소 여유 하한 증명이 필요하고, 수치적으로 안/밖 또는 거리 하한을 판별 못 하면 BLOCKED다.

관통 구분: 증명된 몸체 여유 G<-epsilon(또는 그보다 작은 witness gap)이면 FAIL, [-epsilon,+epsilon]의 수치 경계대는 접촉/불확실로 기록하고 안전 PASS에 사용하지 않는다. 계획상 정지 위치는 +epsilon보다 충분한 여유를 남긴다. 거리와 epsilon은 같은 논리 단위다. epsilon으로 통로를 넓히거나 관통을 보정하지 않는다. 정확한 접촉 판정이 필요한 fixture는 별도 분석 기대값을 갖고 경계대 감지를 확인한다.

관찰은 모든 Simulate 호출의 입구/출구에서 수행한다. 프레임마다 step_id가 연속이고 before=직전 after이며 observer_sequence가 연속이어야 한다. 초기화·부활·FI map 변경은 명시적 event로 연결한다. 전환을 포함한 슬롯은 실제/예약 궤적을 따로 판정한다. C# 현재 snapshot에는 public 필드뿐 아니라 uid/enemyUid/spawnClock/recycleCooldown/각종 난수 기반 각도 등 private 상태를 포함한다. 캡처 실패나 시계열 누락이면 해당 필수 축 BLOCKED.

## 6. G2 시험 묶음과 오류 탐지 인수

[fixture JSON](map-harness-fixtures.json)의 값은 시험용 논리 단위이며 원작/최종 게임 수치가 아니다. 비어 있는 지면 정상 진행, 벽 앞 정지, 얇은 벽 관통 주입을 구체 예제로 제공한다. 007 실제 맵 데이터로 가장하지 않는다. 분석 정답/수치 한계는 [데이터 계약](map-harness-data-contract.md)에 연결한다.

모든 FI에 대응하는 정상 대조군을 같은 입력/관찰/판정 경로로 실행한다. 하나의 정상 대조군을 공유할 때도 각 FI와의 대응을 manifest에 적는다. 오류 종류까지 일치해야 detection PASS다.

| FI | 주입 위치/정상 대조 | 기대 검사·상태 |
|---|---|---|
| FI-01 | 초기화 snapshot에서 슬롯을 벽 안으로 / 같은 슬롯을 안전 위치로 | `INVALID_INITIAL_OCCUPANCY`, B FAIL |
| FI-02 | 닫힌 fixture 경계 양쪽을 잇는 차단벽을 추가 / 벽 없는 같은 목표 | `ROUTE_UNREACHABLE` 또는 등록된 `ROUTE_DEADLINE`, B FAIL. unrelated 예외 불인정 |
| FI-03 | 소유 시험에서 이동 차단을 한 스텝 우회, 관찰은 유지 / 정상 차단 | `SWEPT_PENETRATION`, B FAIL. 끝점 바깥 여부와 무관 |
| FI-04 | 원자료 사본의 결과 삭제·중복·빈 예상 집합·완료 표식 삭제 / 완전한 원본 | `RESULT_SET_MISMATCH` 또는 `INCOMPLETE_RUN`, H FAIL/미완료 BLOCKED를 변형별 등록 |
| FI-05 | 다른 실행 nonce/geometry/capture 혼입 / 동일 실행 자료 | `EVIDENCE_MISMATCH`, H FAIL |
| FI-06 | threshold 키 제거 / 완전한 기준. 별도 변형은 관찰값 NaN 삽입 | `MISSING_THRESHOLD` BLOCKED, `NONFINITE_OBSERVATION` FAIL |
| FI-07 | 복구 대상의 시험 소유 설정 복원 실패 / 정상 복원 | `RESTORE_MISMATCH`, C FAIL. 외부 watchdog이 실제 설정 복원 후 별도 cleanup 기록 |
| FI-08 | cancel·wall timeout·worker 종료를 각각 주입 / 정상 종료 | `CANCELLED/TIMEOUT/INTERRUPTED`, BLOCKED와 중간 기록. 관찰된 FAIL은 보존 |
| FI-09 | 같은 revision의 다른 배치/도면 hash / 일치하는 묶음 | `REVISION_CONTENT_MISMATCH`, F/H FAIL |
| FI-10 | heartbeat는 진행하되 관찰 기록 생성 중지 / 모든 스텝 기록 | `OBSERVATION_GAP`, H/B BLOCKED. 조용한 성공 불가 |

결과 조작 FI는 원본을 보존하고 run-id가 연결된 변형 사본에 수행한다. 수정한 raw file의 hash를 새로 썼더라도 producer 실행 nonce/expected 목록/변형 선언에 대한 불일치를 검사한다. 서명된 보안 증거를 구현한다는 의미는 아니다.

**주입 사례의 raw FAIL/BLOCKED와 탐지 시험의 PASS를 분리한다.** `observed_status`는 그대로 보존하고 별도의 `detection_status`에 기대 오류 일치·정상 대조 통과·주입 경로 확인·정리 완료를 판정한다. G2 H는 모든 FI detection PASS와 정상 대조/REPLAY/CLEAN PASS를 요구한다. FI의 예상 FAIL을 일반 G3 맵 결과로 넣지 않는다. 예상과 다른 오류·실제 세션 복구 실패는 G2 FAIL/BLOCKED로 남는다.

필수 반례에 벽 밖 중심 x=3.3/r=.25/벽면3.4의 몸체 침투(g=-.15), 중심 자체가 고체 내부, 외곽 밖 중심, 외곽 안 중심의 몸체 이탈, g=epsilon 경계대를 포함한다. [시험 자료](map-harness-fixtures.json)의 oracle_vectors가 이 분석 기대값을 정의한다.

G2 추가 필수: 1~4인×3진형의 무장애/벽/반전, 실제/예약 위치가 다른 0입력 추종, 오목/곡선·경계, 전체 진형 전환과 부활 거부 원자성, dt=.05 전후·배속·남은 시간 잘림, FIXED_REPLAY 최소2회, DEVICE_SMOKE, 성공/실패/취소 cleanup. fixture 좌표/입력열은 실행 전에 예상 집합으로 펼쳐 동결한다. 세 예제만 통과해 G2 완료로 보고하지 않는다.

## 7. 실행·기록·취소·복구

`PRECHECK → LOCKED → SNAPSHOT → INITIALIZED → RUNNING → COLLECTED → JUDGED → RESTORING → FINALIZED`를 journal에 남긴다. 어느 상태에서든 예외/취소는 가능한 원자료를 flush한 뒤 RESTORING으로 이동한다. FINALIZED는 파일 쓰기 완료일 뿐 PASS와 동의어가 아니다.

1. **PRECHECK:** 허용 경로, 소스/대상 해시, 컴파일, 패키지, schema/expected case IDs, threshold, 출력 공간, 기존 미완료 journal 확인. 실행 중인 사용자 Play·미저장 씬·비표준 reload 상태는 BLOCKED. 기존 콘솔은 수집하되 지우지 않고 새 로그를 구분한다.
2. **LOCKED/SNAPSHOT:** 프로젝트 폴더의 로컬 lock은 run-id/PID/nonce 포함, 동시 실행 거부. Editor 세션 안에서도 소유 ID 확인. 파일 잠금만으로 다른 Unity 조작을 막았다고 가정하지 않는다. 씬 목록/active scene/selection, GameView 크기·카메라·runInBackground·timeScale·InputSystem 임시 장치를 기록한다.
3. **INITIALIZED:** 저장된 원본 씬을 변경하지 않는 임시 시험 씬을 열고 아래 reload 계약에 따라 Play 진입 후 새 DSGame 한 개를 생성한다. 기존 씬은 snapshot의 scene setup으로 복구한다. Awake에 필요한 Resources 데이터는 실제 콘텐츠를 사용한다. 기존 DSGame이 하나라도 남거나 FindAnyObject가 다른 인스턴스를 반환하면 차단한다. 새 인스턴스 reference+session token을 고정한다. Play/씬 API의 실제 성공은 구현 시 확인한다.
4. **RUNNING:** 하나의 드라이버가 사례를 직렬 실행. 각 독립 사례는 소유 인스턴스를 새로 초기화하고 RNG를 새로 설치한다. 연속 원형 사례 중간에는 초기화 금지. wall timeout은 외부 실행기가 감시하므로 게임/관찰기가 멈춰도 작동한다. 사례당 wall timeout·step limit은 manifest 필수값이다.
5. **COLLECTED/JUDGED:** JSONL step/event를 순서대로 flush, 최초 실패 즉시 snapshot, 완료 시 전체 hash manifest 작성. Python 독립 판정은 수집 종료 후 수행. 파일 크기 한도에 닿으면 잘라서 성공 처리하지 않고 `EVIDENCE_LIMIT` BLOCKED. 기록 실패는 실패 원인과 남아 있는 journal로 보고한다.
6. **RESTORING:** 입력 token 해제·소유 장치 제거·소유 Play 종료·임시 객체/씬 제거·직접 바꾼 설정/씬 setup 복원. 사용자 개입으로 값이 달라졌으면 무조건 덮지 않고 차이와 BLOCKED를 남긴다. 복원 후 값·소유 자원0·Play 정지·lock 해제를 대조한다.
7. **FINALIZED:** Python 실행기가 모든 필수 결과/원자료/cleanup을 검증한 뒤 summary를 마지막에 원자적으로 교체한다. 최종 요약 파일이 없거나 불완전하면 PASS 불가. worker는 스스로 최종 PASS를 쓰지 않는다.

### Play 전환/reload에서 소유권 재연결

Edit Mode의 C# object reference를 Play 진입 뒤 재사용하지 않는다. 부모 Python 실행기는 프로세스 밖 journal/lock을 계속 소유한다. Play 전에 run-id/nonce·예상 시험 씬 식별자·marker ID·복구 snapshot을 프로젝트 output journal에 기록하고 임시 씬에 직렬화 가능한 소유 marker를 둔다. marker에는 실행 요청 경로/hash만 포함하며 살아 있는 DSGame reference는 없다.

Play 진입의 새 domain에서 bootstrap이 journal과 같은 run-id/nonce의 유일 marker를 찾고 부모 실행기의 현재 허가를 확인한다. 0개/복수/이전 nonce면 실행 거부. 그 뒤 새 DSGame 생성→새 reference/instance ID 등록→MapRequired binding→RNG 설치→StartRun/초기 상태 설정→관찰 시작 순서다. 같은 phase/nonce의 중복 callback은 재실행하지 않으며, 이미 생성된 소유 instance가 정확히 하나인지 재확인한다. 소유권 확인 전 일반 Update가 진행하지 않도록 생성부터 driver 활성화까지 game.enabled=false를 유지하고, 초기화 완료 후 한 번만 활성화한다.

Play 종료 reload 뒤 Editor bootstrap은 journal의 RESTORING과 nonce를 다시 읽어 부모와 재연결한다. 새 domain의 callback만 복구를 이어가고 이전 메모리 reference/콜백은 사용하지 않는다. 원본 scene setup은 Python에 저장된 값으로 복원 요청하고 실제 Editor 결과를 재조회한다. journal에 복구 동작별 완료 여부를 기록해 재진입해도 사용자 씬을 다시 덮지 않는다. 재연결 실패/중복 marker/사용자 Play 시작은 BLOCKED이며 부모가 자동으로 새 시험을 시작하지 않는다. 구체 callback/API는 구현 환경에서 확인하고 정상 domain+scene reload, 중간 worker 종료, 종료 reload 중 실패를 필수 시험한다.

강제 종료에는 finally가 실행된다고 가정하지 않는다. journal은 각 상태 변경 전에 저장하고, 외부 실행기의 PID/heartbeat 확인으로 중단을 기록한다. 다음 실행은 stale lock을 바로 삭제하지 않고 해당 PID와 Editor session token·잔여 자원을 확인한다. 정상 사용자 활동과 구분 불가하면 BLOCKED와 수동 확인 절차를 제시한다. 재실행은 새 run-id이며 중단 실행의 성공 기록을 덮지 않는다.

출력은 이 프로젝트 `output/verification/<run-id>/map/` 아래로 제한한다. Unity가 이 경로에 직접 기록할 수 없으면 프로젝트 내부 수집 endpoint로 전달하는 구현을 먼저 마련하고 실행을 차단한다. 개인 Unity 폴더에 영구 보고서를 남기는 기존 방식은 이번 도구에 복사하지 않는다. 임시 씬은 메모리에 유지하며 사용자 원본을 저장하지 않는다. 로그/캡처/임시 데이터에 회사 경로를 포함시키지 않는다.

## 8. 구현 순서·회귀·완료 경계

| 순서 | 구현 단위 | 인수 증거 |
|---|---|---|
| H1 | 데이터 인수·결과 집계·Python 독립 기하 판정 | 분석 정답 도형과 불량 입력 검출, FI04/05/06/09. 아직 G2 미완료 |
| H2 | 세션·격리 씬·driver·공유 프레임 경로·기록 | 같은 게임 인스턴스/호출 수, DEVICE_SMOKE, 예외/취소/관찰 누락·복구 |
| H3 | 이동 해결기·명령 사전 검사·난수 관찰 | 정상/관통/정체·실패 원자성, FI01~03 및 전체 FI 재연결 |
| H4 | G2 전 필수 예상 사례 동결·실행 | 전 FI+정상 대조+REPLAY/CLEAN+실제 프레임/입력, 기존 all --screens 회귀 |
| H5 | 007 후보용 실행 묶음 생성과 CAL/G3 연결 | 채택된 C/몸체/기준/입력열·전체 G3 사례. 하네스 성공만으로 G3 PASS 금지 |

기존 검사 gate의 baseline을 바꾸어 새 코드를 통과시키지 않는다. 런타임 변경 시 현재 `all --screens` 증거가 필요하며 맵 전용 결과도 추가 연결한다. 맵 하네스의 새 `map` 단계나 gate 통합은 구현 후 새 테스트로 검증한다. 기존 gate PASS는 새 맵 검사를 실행했다는 뜻이 아니다.

개정 조건: 입력/게임 경로, schema, solver, oracle, 기준값, 코드/대상/환경/geometry가 달라지면 관련 증거 무효화. 영향 범위를 증명 못 하면 G2 전체 재실행. 실제 제작 승인·파일 쓰기 권한은 후속 구현 시 현재 범위와 권한을 다시 확인한다. 설계 검토 통과는 런타임 PASS를 대신하지 않는다.

설계 검토 이력과 실행 범위는 [009 검토 기록](../reviews/map-harness-009-review.md)에 보존한다.

## 9. G5 자산 연결 보충 — ASSET-PIPELINE-011

[자산 하네스](ai-3d-asset-harness.md)가 생성 작업/처리/임포트/시각/채택을 담당하고 본 맵 하네스는 실제 이동·상태·맵 구조의 독립 판정을 계속 담당한다. H1~H5 순서나 기존 FI/MC를 줄이지 않는다. API 잔액이나 외부 생성 없이도 G2 fixture/H1~H4 구현은 진행할 수 있다.

- G5 연결기는 sidecar의 map/layout/C/좌표변환과 실제 대상 scene/instance/프리팹/의존 해시를 대조한다. 기존 map request v1에 자산 필드를 몰래 추가하지 않는다.
- 자산 단독 A5 채택과 실제 OB별 AC-10 및 맵 MC-CAMERA-01/MC-PERF-01은 별도 결과다. G5는 기존 G4 필수 결과와 새 자산/연결 증거를 모두 요구한다.
- 시각 모델에 생성된 collider를 기존 논리 C로 사용하지 않는다. 배치 중 C/몸체/s/k를 변경해야 하면 새 맵 CH/인수로 돌아간다.
- 현재 Tools/Verify/map_check.py의 H1 분석이나 gate는 이 연결을 검사하지 않는다. P4 구현과 실패 주입 전에 자산 의존 해시 일치·완료 검사를 자동 강제한다고 보고하지 않는다.
