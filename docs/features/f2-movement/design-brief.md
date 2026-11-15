# F2 원정대 이동과 진형 — 설계 브리핑

- 상태: 검토 중
- 규모: L2 — 새 데이터 스키마 2종(진형·이동 규칙), F1 데이터 칸 삭제(`memberSpots`), 대원 좌표·원정대 중심의 쓰는 쪽 변경(F1 `PartyService` → `PartyMoveService`), DI 등록 추가
- 기준: 승인된 기획서 [plan.md](plan.md)(절 번호는 그 문서), 근거 [notes.md](notes.md). 수치는 기획서·데이터 파일에만 있고 여기에는 다시 적지 않는다.
- 이 실행의 범위: 에디터 없는 worktree에서 코드·데이터·테스트·흐름 경로·데이터 검사를 쓴다. 장면(`Main.unity`)의 오브젝트·SerializeField 연결은 합친 뒤 루트 실행이 에디터로 넣는다(디렉터 지시).

## 이번에 쓸 수 있게 될 것·완료 조건
- 손가락(조이스틱)·WASD로 원정대 이동, 진형 따라가기, 사망자 제자리, 카메라 따라가기, 정비에서 진형 3종 고르기(다음 웨이브 적용), 판정 봇 `BotMode.Move`.
- 완료 조건: 기획서 AC1~AC22. 이 실행에서 확인할 수 있는 것은 AC2(데이터 검사)·AC5(경로 형식 검사)뿐이고, 나머지는 합친 뒤 루트에서(컴파일·EditMode·흐름 경로).
- 제외: 걷기 그림·바라보는 방향, F3 아이템·부활, F4 몬스터, F5 가방, F6 피드백, 적·투사체 경계.

## 기존 코드와 데이터 소유처 (시작 커밋 92b4a56에서 읽음)
- `PartyModel`(일반 클래스): 대원 목록(`PartyMember.Position` 바닥 좌표, HP…)과 **`Center`(원정대 중심, `SetCenter`)가 이미 있다**. 주석: "F1은 원점 고정, 이동은 F2". 쓰는 쪽: `PartyService.CreateParty`(중심 0, 대원 = 중심 + `memberSpots[i]`).
- `Center`를 읽는 쪽: `MonsterService.Spawn`(적 생성 기준, F4가 동시에 고치는 파일), `WorldPresenter.LateTick`(카메라 중심 = `IsoProjection.FloorToWorld(_partyModel.Center)`).
- 보이는 바닥 영역: `WorldPresenter.UpdateViewExtent`가 카메라에서 바닥 단위 반 크기를 구해 바뀔 때만 `MonsterService.SetViewHalfExtent`로 넘긴다.
- 들판: `FieldView.Build(fieldCells)` = 원점 가운데 cells×cells 아이소 칸. 칸 폭 = 1유닛(`Const.PixelsPerUnit` 주석: 1유닛 = 타일 폭 48px), 그래서 바닥 평면 마름모 반대각선 R = `fieldCells` ÷ (2 × `unitScale`)(notes.md "들판 경계 수치"와 같은 값, 약 2832). 가운데 c = (0, 0).
- 걸음: `RunService.AdvanceFrame`(정수 µs로 모아 `simStep`씩) → `Step` ①~⑦. 웨이브 시작은 `BeginWave` 한 곳(StartRun의 웨이브 1, NextWave).
- 입력: Input System 1.19, `Game.asmdef`에 `Unity.InputSystem`·`UnityEngine.UI` 참조, 장면 EventSystem은 `InputSystemUIInputModule`. 캔버스 Screen Space Overlay, CanvasScaler 640×360.
- JSON은 `JsonUtility`(F1이 중첩 배열을 못 읽어 `memberSpots`를 `{x, y}` 목록으로 둠).

## 상태 소유권
| 상태 | 소유(쓰는 쪽) | 읽는 쪽 | 형태 |
|---|---|---|---|
| 원정대 중심 `Center` | **`PartyMoveService`만** (`PartyModel.SetCenter`) | `MonsterService`(그대로), `WorldPresenter`(그대로), `PartyMoveService`, 진입점 | `PartyModel.Center` 그대로 둔다(아래 "기획서와 다른 점 1") |
| 대원 바닥 좌표 `PartyMember.Position` | **`PartyMoveService`만** (만들 때 0, 한 판 시작·웨이브 시작·걸음) | 무기·적·View·진입점 | 그대로 |
| 이동 명령 `MoveInput`(화면 기준, 길이 ≤ 1) | `PartyMoveService.SetMoveInput`(호출자: `MoveInputPresenter`, 진입점 `SetMove`·봇), `StartWave`·`ResetForRun`에서 0 | `PartyMoveService.Step` | `PartyMoveModel`(일반 클래스, 1/30초 값이라 R3 아님) |
| 지금 진형·대기 진형 id | `FormationService`만 | `PartyMoveService`(자리), `FormationPresenter`, 진입점 | `FormationModel`: R3 `ReactiveProperty<string>` 2개, 밖으로 `ReadOnlyReactiveProperty<string>` |
| 보이는 바닥 반 크기(경계 계산용) | `WorldPresenter`가 `PartyMoveService.SetViewHalfExtent`로 넘김(바뀔 때만, F1 `MonsterService`와 같은 값·같은 자리) | `PartyMoveService.Step` | 서비스 내부 필드 |
| 조이스틱 눌림·누른 자리·지금 자리(화면 px)·포인터 id | `UI_JoystickView`(포인터 처리) | `MoveInputPresenter` | View 필드(사실만) |

## 흐름
- 한 판 시작(`RunService.StartRun`): `PartyService.CreateParty()`(대원 좌표 0) → `FormationService.ResetForRun()`(지금 = 대기 = `order` 최소) → `PartyMoveService.ResetForRun()`(중심 = c, 입력 0, 네 명 모두 c + slot) → `BeginWave(1)`.
- 웨이브 시작(`RunService.BeginWave` 한 곳, 웨이브 1 포함): `FormationService.ApplyPending()` → `PartyMoveService.StartWave()`(입력 0, 살아 있는 대원만 Center + slot, 중심 그대로).
- 걸음(`RunService.Step`): ③ 적 생성 뒤 `PartyMoveService.Step(deltaSeconds)` 한 줄 → ④ 무기. `deltaSeconds`는 ①에서 남은 시간으로 자른 값(다른 시스템과 같은 시간).
- `PartyMoveService.Step(dt)`(A절 1~4): `d = PartyMoveMath.ScreenToFloorDirection(s)`, `Center += d × |s| × moveSpeed × dt`, `Center = PartyMoveMath.ClampToDiamond(Center − c, R − hw − hh) + c`, 살아 있는 대원만 `PartyMoveMath.FollowStep(pos, Center + slot[i], followRate × dt)`.
  - `R − hw − hh ≤ 0`이면 중심 = c. 경고 로그는 `SetViewHalfExtent`에서 값이 그렇게 바뀔 때 한 번(걸음마다 찍지 않는다).
  - 마름모 위 가장 가까운 점: notes.md의 식(사분면 부호를 떼고 t = clamp((|x| − |y| + L) ÷ 2, 0, L)).
- 자리 `slot[i]` = `FormationService.GetSlot(i)` = 지금 진형 데이터의 i번째(캐릭터 `order` 순 = `PartyModel.MemberList` 순). 4명 기준 고정.
- 진형 고르기 `FormationService.SelectPending(id)`: `RunModel.Phase == Prep`이고 있는 id일 때만 대기 진형 = id, 아니면 경고 로그 후 무시.
- 입력(D절) `MoveInputPresenter : ITickable, IInitializable, IDisposable`:
  - 전투 중 매 프레임: 조이스틱이 눌려 있으면 `MoveInputMath.JoystickInput(press, current, joystickRadius × canvas.scaleFactor)`, 아니면 `MoveInputMath.KeyboardInput(W, A, S, D)`(`Keyboard.current`가 없으면 0 — 기기 경계). 지난 보낸 값과 다를 때만(또는 아직 안 보냈으면) `SetMoveInput`. 조이스틱 그림은 `UI_JoystickView.UpdateUI(isShown, pressScreenPosition, input)`.
  - `Phase`가 Battle로 바뀔 때(웨이브 시작마다) 지난 값을 지운다. Battle이 아니게 되면 조이스틱을 숨기고(눌림 상태도 버림) 지난 값이 0이 아니었으면 0을 보낸다.
- `UI_JoystickView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler`: 처음 누른 포인터 id 하나만 기억, 사실(눌림·누른 자리·지금 자리, 캔버스 배율)만 내놓는다. `UpdateUI`가 고리·손잡이를 켜고 누른 자리에 놓으며 손잡이를 input × `handleTravel`(장면 값 14)만큼 옮긴다. HUD 버튼이 앞이라 버튼 위 누름은 조이스틱에 오지 않는다(장면 순서).
- 정비 진형(F절) `FormationPresenter : IInitializable, IDisposable`: `Phase == Prep`로 바뀔 때와 버튼을 누를 때 `UI_FormationView.UpdateUI(…)` 한 번. 버튼 i = `FormationRepository.FormationDataList[i]`. 버튼 수 < 진형 수면 오류 로그. 버튼 처리 메서드 `OnFormationClicked(string id)`(진입점 `PressFormation`이 같은 메서드를 부른다). 미리보기 = 대원 이름·살아 있음·대기 진형 자리(View가 세로 절반 압축해 그림).
- 카메라(E절): `WorldPresenter.LateTick`의 카메라 줄은 이미 `PartyModel.Center`를 읽으므로 고칠 것이 없다. `ILateTickable`이라 그 프레임 걸음 뒤에 놓인다.
- 판정 진입점(H절): `PlayableProbe`에 읽기·조작 멤버, 표시(Mark) 상태는 진입점 안 private static(편집기 전용 `#if UNITY_EDITOR`, F1과 같은 자리). 봇 Move = 들판 가운데 반지름 600 시계 방향 원, 바닥 방향을 화면 방향 (fx, fy ÷ 2)로 바꿔 길이 1로 `SetMove`. 깃발이 빠지면(`SetMode`·`OnDestroy`) 0을 한 번.

## 변경할 파일과 변경 전 → 후
새 파일(기획서 "클라 변경" 목록 그대로): `Features/Party/Data/FormationData.cs`·`MoveRulesData.cs`, `Registry/FormationRepository.cs`, `Model/FormationModel.cs`·`PartyMoveModel.cs`, `Service/FormationService.cs`·`PartyMoveService.cs`, `Utils/PartyMoveMath.cs`·`MoveInputMath.cs`, `Presenter/MoveInputPresenter.cs`·`FormationPresenter.cs`, `View/UI_JoystickView.cs`·`UI_FormationView.cs`, 데이터 4개, `Tools/playable/f2-movement-flows.json`, 테스트 4개.

F1 파일:
- `GameSceneLifetimeScope.cs`: 등록 줄만 추가(이동 규칙은 `LoadJson<MoveRulesData>("Data/Party/move_rules")` 인스턴스 등록 — F1 `RunRulesData`와 같은 방식).
- `RunService.cs`: 생성자 의존 `FormationService`·`PartyMoveService`, `StartRun`에 Reset 두 줄, `BeginWave`에 `ApplyPending`·`StartWave` 두 줄, `Step` ③ 뒤 한 줄.
- `PartyService.cs`: `CreateParty`에서 `SetCenter`·`memberSpots` 줄을 지우고 대원을 0에 만든다. `PlaceholderCombatData` 의존이 쓸 곳이 없어지므로 생성자에서 뺀다.
- `PlaceholderCombatData.cs`·`placeholder_combat.json`: `memberSpots` 칸 삭제.
- `WorldPresenter.cs`: 생성자 의존 `PartyMoveService`, `UpdateViewExtent`에 `_partyMoveService.SetViewHalfExtent(floorHalfExtent)` 한 줄. 카메라 줄은 그대로.
- `PartyModel.cs`: 주석(쓰는 쪽)만.
- `PlayableProbe.cs`: H절 멤버, `PlayableBot`의 Move 처리, "아직 없는 깃발"에서 Move 제외.
- `Tools/check_data.py`: 진형·이동 규칙 검사, `memberSpots` 있으면 실패, `--self-test`에 4종 추가.
- 테스트: `TestRunContext.cs`(새 서비스 생성·`RunService`·`PartyService` 생성자 맞춤), `DataLoadTests`의 `MemberSpots` 단정 한 줄 삭제(기획서 "기대 변경 허용" 둘째 줄).

## 기획서와 다른 점(검토 요청)
1. **`PartyMoveModel.Center` 대신 `PartyModel.Center`를 원본으로 둔다.** 기획서 데이터 스키마는 `PartyMoveModel(Center, MoveInput)`, 클라 변경은 "F1에 중심을 내주는 멤버가 있으면 그 멤버가 `PartyMoveModel.Center`를 돌려주게 바꾸고 읽는 쪽은 고치지 않는다"이다. 실제 F1의 중심 멤버는 `PartyService`가 아니라 `PartyModel.Center`(값을 들고 있는 속성)이고, 이것이 `PartyMoveModel`을 돌려주려면 모델이 다른 모델에 의존하거나(생성자 변경 → `TestRunContext`·F3·F4 합치기 충돌) 같은 값을 두 곳에 둬야 한다. `PartyModel.Center`의 쓰는 쪽을 `PartyMoveService` 하나로 바꾸면 "원본 하나, 읽는 쪽 그대로"라는 기획 의도를 지키고 공유 파일(`MonsterService`)을 건드리지 않는다. `PartyMoveModel`은 `MoveInput`만 든다.
2. 진형 `slots`는 `float[N][2]` 대신 `{x, y}` 객체 목록(JsonUtility 제약, F1 `memberSpots`와 같은 처리). 값·개수·뜻은 같다.
3. `WorldPresenter` 변경이 "카메라 줄" 대신 "보이는 영역을 `PartyMoveService`에도 넘기는 한 줄 + 생성자 의존"이다(C절 hw·hh의 출처가 이 메서드라서). 카메라 줄은 이미 중심을 읽는다.
4. 장면 항목은 이 실행에서 넣지 않는다(디렉터 지시, 루트 실행 몫).

## 재사용 후보와 신설 이유
- 순수 계산은 `Utils/` 정적 클래스(`IsoProjection`과 같은 자리·규칙: Utils 밖 public static 금지). 화면 → 바닥 방향은 `IsoProjection`의 세로 압축(0.5)과 같은 비율이지만 `IsoProjection`은 위치 변환이고 `VerticalCompress`가 private이라, `PartyMoveMath`는 방향만 (sx, 2·sy) 정규화로 둔다(F1 파일을 고치지 않기 위해). 봇의 바닥 → 화면 방향은 봇 한 곳에서만 쓰므로 봇 안에 둔다.
- 진형 읽기는 `CharacterRepository`와 같은 모양(`LoadAllJson` + `order` 정렬 + 사전).
- 조이스틱 고리·손잡이 그림: 임시 원은 런타임 텍스처라 장면에 미리 넣을 수 없다. `MoveInputPresenter.Initialize`가 F1 `PlaceholderSpriteFactory.CreateCircle`로 만든 원을 `UI_JoystickView.Initialize(ringSprite, handleSprite)`로 넘긴다(F1 `WorldPresenter.InitializeMemberViews`와 같은 방식). 아트 단계가 그림을 넣으면 이 줄을 바꾼다.
- 새 이벤트 없음. 콜백·R3·직접 호출로 잇는다(기획서 클라 변경 마지막 줄).

## 실패·취소·생사·전환 경계
- 쓰러진 대원(HP 0): 걸음·웨이브 시작에서 좌표 그대로. 자리는 4명 기준(다른 대원 자리 불변). HP가 다시 차면(F3 부활) 다음 웨이브 시작에 자리로.
- 전투가 아닐 때: `Step`이 돌지 않음(F1). 입력 Presenter는 조이스틱을 숨기고 0을 보낸다. 일시정지 중에는 `AdvanceFrame`이 쌓지 않아 움직이지 않는다. 배속은 걸음 수로 처리(F1).
- 진형: 정비 밖·없는 id → 경고 로그, 무시. 데이터 0개 → `FormationRepository`에서 `ResourceService` 오류 로그(F1과 같음). 버튼 < 진형 수 → 오류 로그.
- 판 끝 → 로비 → 새 판: `ResetForRun`이 중심·진형·입력을 모두 처음으로.
- 진입점: `MarkPositions` 전에 읽으면 0 기준(초기값) — 경로는 항상 표시 뒤 읽는다.

## 데이터 호환성과 복구
- 세이브 없음(A9). 데이터 파일 추가·`memberSpots` 삭제는 git으로 되돌린다. `memberSpots`를 지우면 F1 테스트 `DataLoadTests`의 단정 한 줄을 지운다(허용 목록).

## 최소 필수 검증
- 이 실행: `check_data.py`·`--self-test`(AC2 데이터 부분), `grep -rn memberSpots Assets/Game` 0줄, `playable.py --check`·`--check --route f2-movement-flows.json`(AC5), `git diff --stat 92b4a56 -- Tools/playable/route.json` 빈 결과, `algorithm-tester`(`PartyMoveMath`·`MoveInputMath` 실제 실행, `PartyMoveService.Step`·`FormationService`·봇 방향 hand-trace 또는 추출 실행).
- 합친 뒤 루트: 컴파일, EditMode(새 테스트 4개 + F1 테스트), G2, F1 흐름, F2 흐름(캡처 열어 보기), spec-reviewer·validator-client.

## 초기 실제 연결
- 최소 정상 사례: 출발 → 웨이브 1 → `SetMove(1, 0)` 1초 → 중심 x +140 근처, 대원 4명이 균형형 자리를 따라감, 카메라가 중심 → 놓으면 멈춤. 흐름 경로 `B1`·`RIGHT`·`STOP`·`STILL`(합친 뒤 루트에서 실행).

## 검사 확대/도구 신설
- 새 도구 없음. 기존 `check_data.py`에 칸 검사만 더한다(기획서 요구).

## 알려진 환경 차단과 재개 조건
- 이 worktree에는 에디터가 없다. 컴파일·테스트·Play는 합친 뒤 루트 에디터에서. 새 파일 `.meta`도 그때 생긴다.

## 미정 사항
- `<사망 배율>`(흐름 경로 DEAD): 실제 시간을 재지 못하므로 F1 `LOSE` 단계의 배율 4를 출발값으로 적고, 루트 실행이 "첫 사망과 전멸 사이 몇 초 이상"을 보고 고친다.

## 설계 검토 결과와 해소한 지적
- 1회차(design-reviewer, Codex): `VERDICT: FAIL`. "기획서와 다른 점" 1~4는 수용, DI 순환 없음.
  - DR-01(필수): A절 수치(속도 140, 비율 12, 걸음 1/30초)로는 같은 방향 이동 중 자리 오차가 정상 상태 약 7이라 AC16 "1초 뒤 자리 5 이내"와 양립하지 않는다(notes.md "따라가기 수치"도 e = 7). 5는 예전 걸음 0.05초의 정상 오차(약 4.67)에서 온 값으로 보인다.
    - 처리: 승인된 수치·기준을 바꾸지 않는다. 이동 규칙은 A절 그대로 구현한다. AC16의 기준을 두 가지로 나눠 시험한다 — (가) 멈춘 뒤 1초: 자리 오차 5 이내이자 0.01 미만(AC16 문장 그대로, 양립함), (나) 반대로 돌린 뒤 1초(계속 이동 중): 기획서 값 5로 단정하면 규칙상 반드시 실패하므로, `PartyMoveServiceTests.FollowsSlotsInEveryFormation`의 이동 중 단정은 A절 식에서 나온 정상 상태 오차 상한(moveSpeed × dt × (1 − k) ÷ k, k = followRate × dt)에 여유 0.5를 더한 값으로 둔다. 이 차이를 핸드오프 `open_questions`(기획자 결정: 이동 중 5를 지키려면 따라가기 비율을 올릴지, 기준을 7 근처로 고칠지)와 "기준 문서와 다른 점"에 적는다.
  - DR-02(후속): 서비스·봇은 루트 통합 때 EditMode·흐름 경로로 실제 실행 확인. 이 실행의 hand-trace 결과는 별도로 표시한다.
