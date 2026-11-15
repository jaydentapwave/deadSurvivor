# F1 한 판의 뼈대 — 설계 브리핑

- 상태: 구현 기준 (설계 검토 1회차 FAIL 필수 4건 반영, 아래 마지막 절)
- 규모: L2 — Core 기반 신설, DI 등록, 상태 소유권(RunModel·PartyModel·MonsterModel), 데이터 스키마 5종, 렌더 파이프라인 설정(RP 자산 렌더러 목록) 변경
- 기준: 승인된 기획서 [plan.md](plan.md)(절 번호는 그 문서). 수치는 기획서·데이터 파일에만 있고 여기에는 다시 적지 않는다.

## 이번에 쓸 수 있게 될 것·완료 조건
- Play → 로비 → 전투 → 정비 → 결과 → 로비가 실제 규칙으로 이어진다. 완료 조건은 기획서 AC1~AC19. 판정 경로는 G2까지 통과, G3~G8은 경로에 적기만 한다(H절).
- 제외: F2~F6 시스템(이동·진형, 아이템 발동, 몬스터 5종, 가방, 피드백), S10~S13, 빌드 설정.

## 기존 코드와 소유처
- `Assets/Game/`은 빈 폴더 틀. Core 기반 없음. 예전 `Assets/DeadSurvivor/Runtime/`(네임스페이스 `DeadSurvivor`, Assembly-CSharp)은 규칙만 읽고 참조하지 않는다.
- 새 코드는 `Game` 어셈블리, 네임스페이스 없음(전역). 판정 경로 식이 `PlayableProbe.X`로 바로 부르므로 진입점이 전역이어야 하고, 나머지도 맞춘다. 예전 코드와 이름이 겹치는 형 없음(grep 확인).

## 상태 소유권
| 상태 | 소유(쓰는 쪽) | 읽는 쪽 | 형태 |
|---|---|---|---|
| 판 단계·웨이브·남은 시간·처치·버닝·일시정지·배속·결과·판 수 (`RunModel`) | `RunService`만 | Presenter들, 진입점 | R3 `ReactiveProperty`, 밖으로는 `ReadOnlyReactiveProperty<T>`. 쓰기는 모델의 `Set*` 메서드 |
| 다음 판 설정 시드·적 수 배율 (`RunSettingsModel`) | 진입점 `UseRunSettings`, 시작값은 `run_rules.json` | `LobbyPresenter`(출발 때 값을 넘김) | 일반 속성 + `Set(seed, scale)` |
| 원정대 4명(캐릭터 id, HP, 최대 HP, 바닥 좌표, 무기 쿨 시계, 장착 아이템 id 목록) (`PartyModel`) | `PartyService`(만들기·피해), `BasicWeaponService`(쿨 시계) | `WorldPresenter`·HUD·정비 Presenter, 진입점 | 일반 클래스 목록. 1/30초마다 바뀌는 실체 값이라 R3로 감싸지 않고 Presenter가 프레임마다 읽는다 |
| 살아 있는 적(번호, 바닥 좌표, HP, 접촉 쿨) (`MonsterModel`) | `MonsterService`(생성·이동·제거), `BasicWeaponService`(HP 깎기) | `WorldPresenter`, 진입점 | 위와 같음 |
| 생성 난수 | `MonsterService`(판 시작 때 시드로 `System.Random` 새로 만듦) | — | 생성 방향만 쓴다. 전리품 난수(F5)는 따로 둔다 |

- 판 시작·웨이브 끝·로비 복귀 때 적 목록을 비운다(예전 `FinishWave`). 로비로 가면 원정대도 비운다(B절).

## 흐름 (B·C절)
- `RunService`: `StartRun(seed, enemyCountScale)`, `AdvanceFrame(frameSeconds)`, `Step(stepSeconds)`, `NextWave()`, `ReturnToLobby()`, `TogglePause()`, `ToggleSpeed()`.
- **기획서와 다른 자리**: C절은 "BattlePresenter가 프레임 시간 × 배속을 모아 Step"이다. 모으는 코드를 `RunService.AdvanceFrame`에 두고 `BattlePresenter.Tick`은 `Time.deltaTime`을 넘기기만 한다. 이유: AC11의 시험 이름이 `RunServiceTests.PauseStopsTime`·`SpeedDoubles`이고, 엔진 시간 없이 프레임 나누기를 시험해야 한다(`SameSeedSameResultAcrossFrameSplits`). 동작은 같다.
- 모으기는 걸음 단위 double로 한다: 프레임 시간 × 배속 × 초당 걸음 수(`round(1 / simStep)` = 30, 정수)를 더하고 1 − 0.0001이 차면 한 걸음. 한 걸음에 더하는 게임 시간은 `simStep`(float) 그대로다(시도 2 변경 절).
- 모으기의 경계: 일시정지 중이거나 전투가 아니면 쌓지 않는다. 걸음마다 `Phase`를 보고 전투가 끝났으면(정비·결과) 남은 몫을 버리고 멈춘다. `StartRun`·`NextWave`(웨이브 시작)·`ReturnToLobby`에서 0으로 한다. 그래서 웨이브마다 도는 걸음 수가 프레임 나누기와 상관없다(`RunServiceTests.LeftoverFrameTimeIsDroppedAtWaveEnd`, `SameResultAcrossWavesForDifferentFrameSplits`).
- 기본 무기 쿨: 쿨이 남았을 때만 줄이고, 그 걸음에 0을 넘어선 몫은 다음 쿨에서 뺀다(간격을 1/30초 걸음으로 정확히 지킴). 표적이 없어 기다리면 0에 둔다.
- 적 번호는 판이 바뀌어도 이어서 늘린다(같은 프레임 로비 → 출발 때 지난 판 View가 등록부에 남는 경우).
- `Step` 순서는 C절 ①~⑦ 그대로. 남은 시간을 원본으로 두고(경과 = 웨이브 시간 − 남은 시간) ①에서 `min(걸음, 남은 시간)`을 빼면 마지막 걸음에서 정확히 0이 된다. ⑥ 생존 0 → 패배가 ⑦ 시간 끝보다 먼저다.
- 생성(E절): 웨이브 시작 때 생성 시계 0(첫 걸음에 1마리), 간격 = (웨이브 시간 − 4) ÷ max(1, 수 − 1). 버닝 추가 생성은 예전 `Burning()`의 "그때까지 나와야 할 수" 방식으로 세되, 간격(× `burningIntervalScale`)마다 반대쪽 두 방향에서 1마리씩(기획서 E절, 시도 2 변경 절). **수가 0이면(적 수 배율 0) 버닝 추가 생성도 0** — 예전 식은 수 0에서도 1마리를 내지만 AC6의 "적이 없는 판"과 맞지 않는다.
- 생성 거리 = 보이는 바닥 영역 대각선 절반 + `spawnMargin`. 보이는 영역은 `WorldPresenter`가 카메라(직교 크기·화면비)에서 바닥 단위로 바꿔 `MonsterService.SetViewHalfExtent`로 넘긴다(바뀔 때만). 시험은 640×360 기준값을 직접 넣는다.
- 기본 무기: 살아 있는 대원마다 쿨 시계를 줄여 0 이하이면 사거리 안 가장 가까운 산 적에게 즉시 `damage × atk`, 시계 += `interval`. 표적이 없으면 시계를 0에 둔다(쌓인 음수로 연사하지 않게). 쓰러진 적은 같은 걸음의 다음 대원이 고르지 않는다. 처치 수 = 무기 뒤 제거된 적 수.
- 적: 가장 가까운 산 대원에게 직진(접촉 거리에서 멈춤), 거리 ≤ 반지름 + `contactGap`이고 쿨 ≤ 0이면 `PartyService.ApplyDamage`(피해 × (1 − 방어)), 쿨 = `attackInterval`. HP 0 대원은 표적·무기에서 빠진다.

## DI (`GameSceneLifetimeScope`, A절)
- Scoped: `ResourceService`, `PoolManager`, `EventBus`, `PlaceholderSpriteFactory`, 저장소 3개(`CharacterRepository`·`ItemRepository`·`WaveRepository`) + `RunRulesData`·`PlaceholderCombatData`(저장소가 읽어 인스턴스로 등록하지 않고, 읽는 클래스를 하나 둘지 — 아래 질문 1), 모델 4개, 서비스 4개(`RunService`·`PartyService`·`BasicWeaponService`·`MonsterService`), `MonsterViewFactory`, `MonsterViewRegistry`.
- Entry point(Scoped, `.AsSelf()`로 진입점이 꺼낼 수 있게): `ScreenPresenter`, `LobbyPresenter`, `BattlePresenter`(ITickable), `BattleHudPresenter`(ILateTickable), `PrepPresenter`, `ResultPresenter`, `WorldPresenter`(IInitializable + ILateTickable).
- 장면 컴포넌트: `RegisterComponentInHierarchy`로 `UI_LobbyView`·`UI_BattleHudView`·`UI_PrepView`·`UI_ResultView`·`FieldView`·`Camera`. 대원 그림 4개(`MemberView`)는 LifetimeScope의 `[SerializeField] MemberView[]`를 하나씩 `RegisterComponent`해 `IReadOnlyList<MemberView>`로 받는다.
- 이벤트: `GameEvent.RunEndedEvent { Outcome }` 하나(기획서 Core 목록). `BattlePresenter`가 `AdvanceFrame` 뒤 단계가 Result로 바뀌었으면 다른 시스템에 알리는 사실로 발행한다. **F1에는 받는 쪽이 없다**(뒤에서 F6 피드백·S10 패배 원인이 받는다). 결과·정비 화면은 `RunModel.Phase`를 R3로 구독해 채운다(같은 기능 안 통신에 EventBus를 쓰지 않음). Service에는 `EventBus`를 넣지 않는다.
- 모델 쓰는 순서: 단계가 바뀔 때 `RunService`는 결과·완료 웨이브·남은 시간 등 그 단계가 보여 줄 값을 모두 쓴 뒤 **`SetPhase`를 마지막에** 부른다(R3 구독이 `Step` 안에서 동기로 불리므로). `RunServiceTests.PhaseSubscribersSeeFinalValues`가 본다.
- Presenter는 모두 `IInitializable`(버튼·구독 연결) + `IDisposable`(구독 해제)을 구현한다.

## 화면 (G·I절)
- Canvas(Overlay, 640×360, Match 1) → `UI_SafeAreaPanel` 하나 → 화면 View 4개(각각 안전 영역 전체로 늘림). `ScreenPresenter`가 `Phase`로 4개를 켜고 끈다. 전체 화면 딤은 두지 않는다: 월드(바닥)가 화면 끝까지 배경이 되고, 로비·정비·결과는 안전 영역 안의 상자다.
- 최소 폭은 4:3의 480 기준 px(높이 맞춤이라 640보다 좁다). 로비 카드 줄 460 고정 폭, 상단 표시줄·하단 판넬은 안전 영역 폭에 맞춰 늘어난다. 하단 캐릭터별 칸은 4등분, 아이템 목록 틀은 4열 격자에 장착 아이템 수만큼 칸(F1은 1칸).
- 대원 4명·로비 카드 4장·하단 상태 4칸·정비 자리 4칸은 장면에 미리 놓는다(A3: 4명 고정, 검사 명령이 4명을 본다). `CharacterRepository` 수가 칸 수보다 많으면 Presenter의 `Initialize`에서 한 번 `GameLogger.LogError`(원정대 모델 수와 비교하지 않음 — 로비에서는 원정대가 비어 있다). 원정대가 비면 `MemberView`를 숨긴다.
- 적 그림 `MonsterView`는 프리팹 `03.Resources/Resources/Prefab/MonsterView.prefab`을 `ResourceService.Load`로 읽어 `PoolManager.Get` → `MonsterViewFactory`가 색·크기를 넣고 `MonsterViewRegistry`(적 번호 → View)에 올린다. `WorldPresenter`가 프레임마다 모델과 대조해 새 적은 만들고 사라진 적은 풀로 돌린다.
- `PoolManager.Get`의 "상태 초기화"(AC17) = 켜기·부모·위치/회전/크기 되돌리기. 그림별 값은 팩토리가 넣는다(`IPoolable` 같은 인터페이스는 만들지 않음).
- 임시 그림은 `PlaceholderSpriteFactory`(Scoped, 만든 텍스처를 `Dispose`에서 지움)가 실행 중에 만든다: 대원 원(발 가운데 피벗), 적 마름모(크기 = 반지름 × `unitScale`), 바닥 마름모 48×24 두 색, HP 막대. 대원 원 크기·HP 막대 크기·바닥 두 색은 그림 단계가 바꿀 임시 값이라 코드가 아닌 장면의 SerializeField에 둔다. PPU 48은 `Const.PixelsPerUnit`.

## 장면·자산을 만드는 방법
- `Main.unity`와 `MonsterView.prefab`은 에디터 스크립트 `02.Scripts/Editor/MainSceneBuilder.cs`(메뉴 하나, `Game.Editor` 어셈블리)로 **처음 한 번** 만든다. 판넬·카드·버튼·SerializeField 연결이 수십 개라 손으로 YAML을 쓰거나 명령 수십 번으로 만들면 검토할 수 없다. 기획서 클라 변경 목록에 없는 파일이다(`Game.Editor.asmdef`는 목록에 있다).
- **처음 만든 뒤에는 장면과 프리팹이 원본이다.** 빌더는 `Main.unity`나 `MonsterView.prefab`이 이미 있으면 아무것도 쓰지 않고 경고만 남긴다(그림 단계·뒤 기능이 장면에서 바꾼 것을 덮지 않게). 두 파일이 있는지 먼저 모두 보고, 만들 때는 프리팹 → 장면 순서로 저장하며 중간에 실패하면 이번 실행에서 만든 파일을 지운다. 다시 만들려면 에이전트가 두 파일을 지운 뒤 돌린다(사용자에게 파일 작업을 시키지 않음).
- `Game2DRenderer.asset` 생성과 두 RP 자산 목록 끝 한 줄 추가는 에디터 명령 한 번(`script eval`)으로 하고 `git diff`로 한 줄인지 본다(AC16). 다른 칸이 같이 바뀌면 되돌리고 그 한 줄만 텍스트로 넣은 뒤 다시 가져온다.

## 데이터 (데이터 스키마 절)
- JSON은 `JsonUtility`로 읽는다(새 패키지 없음, F절). **`placeholder_combat.json`의 `memberSpots`는 `[[x, y], …]` 대신 `[{"x": …, "y": …}, …]`(4개)로 적는다.** `JsonUtility`가 중첩 배열을 못 읽고, Newtonsoft는 예전 3D 키트·CLI 패키지의 간접 의존이라 기대지 않는다. 뜻·개수·값은 같다. 핸드오프 "기준 문서와 다른 점"과 열린 질문으로 올린다.
- `Tools/check_data.py`: 형식·필수 칸·id = 파일명·중복·`startItem` 대상·웨이브 `order` 빈칸·개수 범위·캐릭터 4명. `--self-test`는 임시 폴더에 틀린 데이터 4종을 만들어 각각 exit 1인지 본다.

## Core
- `Const.IsLogEnabled => Debug.isDebugBuild`(에디터·개발 빌드 참, 고칠 수 없음). AC17의 "거짓이면 안 찍힘"은 `GameLogger`의 `internal` 쓰기 함수(켜짐 여부를 인자로 받음)를 `InternalsVisibleTo("Game.Tests.EditMode")`로 시험한다. 정적 상태를 바꾸지 않는다.
- `SafeAreaLayout.Calculate`와 `IsoProjection`은 순수 정적 함수라 `Utils/`에 둔다: `UI/Utils/SafeAreaLayout.cs`(**기획서 경로 `UI/SafeAreaLayout.cs`와 다름** — 아키텍처 규칙 "Utils·Const 밖 public static 금지"), `Field/Utils/IsoProjection.cs`.
- `Game.asmdef`는 `autoReferenced: false`(예전 Assembly-CSharp가 새 코드를 자동 참조하지 않게, A절 "서로 참조하지 않게").

## 판정 (H절)
- `PlayableProbe`(전역 정적, 파일 전체 `#if UNITY_EDITOR`): `FindAnyObjectByType<GameSceneLifetimeScope>().Container`에서 모델·Presenter를 꺼내 읽고, 버튼은 Presenter의 `On*Clicked`를 부른다(버튼 `onClick`과 같은 메서드). `Outcome`·`Phase`에 쓰지 않는다. `BotMode`는 `PlayableProbe` 안의 `[Flags]` 열거형.
- `PlayableBot`(같은 파일): `AutoNext`만 동작. `Move`·`Equip`은 경고 한 번 남기고 무시. 봇은 정적 필드에 두지 않고 `FindAnyObjectByType<PlayableBot>()`로 찾는다(정적 상태 없음). `PlayableProbe`의 public static은 판정 규칙이 정적 클래스를 강제하는 경우로 본다.
- 경로 파일의 `timeout`은 단계마다 근거를 둔다: 1배속 웨이브 1(30초)을 기다리는 `P1`은 30초 + 여유, 20배속 단계는 남은 전투 시간 ÷ 20 + 여유.
- AC19의 Device Simulator 노치 캡처는 판정 경로가 아니라 따로 한다(Simulator 창 전환 후 캡처). 못 하면 미확인으로 적는다.
- **검사 도구 결함**: `Tools/playable.py`가 지금 Unity CLI(1.0.0-beta.17)의 응답을 못 읽는다 — `script eval` 결과가 여러 줄 JSON의 `data.result.result`에 있는데 마지막 줄만 읽어 모든 `wait`가 거짓, 콘솔 오류 수도 `data.result.groundTruth`를 못 찾음, 캡처 인자는 `--output-path`인데 CLI는 `save_path`(프로젝트 기준 경로). `.ai/rules/playable-check.md`의 "처음 쓰는 프로젝트에서 어긋나는 점이 나오면 도구를 고친다"에 따라 응답 읽기·캡처 인자만 고치고 판정(단계 순서·wait·expect·오류 수·timeout)은 바꾸지 않는다. 자체 시험을 붙인다. 키트 원본(`studio-kit`)은 이 레포 밖이라 남은 일로 적는다.

## 실패·전환 경계
- 같은 프레임에 버튼 → `Phase` 변경(ScreenPresenter 구독이 바로 판넬을 바꿈). 정비·결과·로비에서는 `AdvanceFrame`이 아무것도 하지 않는다.
- 웨이브 수 = 웨이브 파일 수. 코드에 10이 없다. 데이터 읽기 실패(파일 0개, 칸 수 다름)는 `GameLogger.LogError` 후 진행하지 않는다.

## 최소 필수 검증
| 변경 | 실패 가능성 | 확인 |
|---|---|---|
| 판 규칙·시간 쪼개기 | 동시 패배·버틴 웨이브·걸음 수 | EditMode `RunServiceTests`, `PlaceholderCombatTests` + algorithm-tester |
| 데이터 | 형식·참조 | `check_data.py`, `--self-test`, `DataLoadTests` |
| 장면·DI | 등록 누락, 화면 전환 | Play + `playable.py --until G2`, `f1-flows.json` |
| 렌더 설정 | 예전 장면 렌더 변화 | RP 자산 `git diff` 한 줄, 에디터 명령으로 목록·기본 번호 읽기 |
| 화면비·안전 영역 | 잘림·겹침 | G1·G2·P1·WIN 4화면비 캡처 + Device Simulator 노치 기기 1대 |

## 초기 실제 연결
`MainSceneBuilder` → `Main.unity` → Play → 로비 카드 4장 → 진입점 `PressDepart()` → 전투 남은 시간 감소. 이것부터 짧은 Play 한 번으로 본다.

## 열린 질문 (설계 검토에 묻는 것)
1. (해소됨) `run_rules.json`·`placeholder_combat.json`처럼 하나뿐인 데이터를 읽는 자리: 별도 저장소 클래스를 만들지 않고 `GameSceneLifetimeScope`에서 `ResourceService.LoadJson`으로 읽어 인스턴스로 등록할지(지금 안), 저장소를 둘지.
2. (해소됨) `SafeAreaLayout`(기획서 경로 `UI/SafeAreaLayout.cs`, 순수 정적 함수)이 "Utils 밖 public static 금지"에 걸리는지. 걸리면 `UI/Utils/`로 옮길지.
3. (해소됨 — 아래 결과 절) `RunEndedEvent`의 발행·구독 자리가 같은 층 통신이라 EventBus 금지 사례인지.

## 설계 검토 결과와 해소한 지적
1회차(design-reviewer): FAIL — 필수 4건. 모두 위 본문에 반영했다. 수정 확인(design-reviewer, 새 대화): PASS — 4건 해소, 새로 깨진 것 없음. 후속 제안 F-a(시험 프레임 30.03초로), F-b(빌더 실패 복구), F-c(질문 해소 표시), F-d(simStep µs 0 방어)를 반영했다.
- M1 같은 기능 안 Presenter끼리 EventBus → 결과·정비 화면은 `RunModel.Phase` R3 구독, `RunEndedEvent`는 받는 쪽 없이 사실로만 발행(DI절).
- M2 R3 동기 전파와 쓰는 순서 → 값을 모두 쓴 뒤 `SetPhase` 마지막(DI절) + 시험.
- M3 빌더가 장면을 덮어씀 → 있으면 쓰지 않음, 처음 뒤에는 장면이 원본(장면·자산절).
- M4 프레임 모으기 경계 → 일시정지·전투 아님·웨이브 끝·시작 때 규칙과 시험(흐름절).
- 질문 답: 1 함수 등록(`builder.Register(r => r.Resolve<ResourceService>().LoadJson…)`), 2 `UI/Utils/`로 옮김, 3 금지 사례 → M1처럼.
- 후속 제안 중 반영: 적 번호 이어 늘리기(F4), 경로 timeout 근거(F2), 로비 빈 원정대(F3), Presenter 생명주기(F10), 봇 찾기(F11), `autoReferenced: false`(F12), 렌더 확인 범위에 URP 전역 설정 더함(F8).

### 시도 2 변경 (algorithm-tester 지적, 424ca5e)
- 프레임 모으기: 정수 µs 대신 걸음 단위 double로 모으고 1 − 0.0001걸음이면 한 걸음으로 친다. 걸음 단위는 `simStep`으로 나누지 않고 초당 걸음 수(정수 30)를 곱한다 — float simStep으로 나누면 1초가 29.999998걸음이라 65초 웨이브에서 1초×64(1919)와 1/60초×3840(1920)이 갈렸다(설계 검토 수정 확인 지적, `LongWaveSameStepCountForDifferentFrameSplits`). 프레임마다 µs로 반올림하면 1/144초 × 144가 29걸음이 됐다(1초 1번은 30). 경계(일시정지·전투 아님·웨이브 끝 버림·시작 때 0)는 그대로. `RunServiceTests.FrameRateDoesNotChangeStepCount`(144·60·30·3fps).
- 버닝 추가 생성: 간격(일반 간격 × 0.5)마다 반대쪽 두 방향에서 1마리씩 = 한 번에 2마리(기획서 E절 문장 그대로). 앞서는 예전 `Burning()`처럼 번갈아 1마리였다. `PlaceholderCombatTests.BurningTimeSpawnsExtra`가 간격 수 × 2를 본다.
