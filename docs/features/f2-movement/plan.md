# F2 원정대 이동과 진형 기획서

- 상태: 승인 (2026-11-15, 디렉터 확인 — 게임 시작 전 승인 범위 안, director-game autonomy 1절)
- 서버 필요: 아니오
- 관련 문서: [게임 기획서](../../game/gdd.md) F2(S1), 화면 V2(조이스틱·WASD)·V3(진형 3종). 판정 단계 G2(이 기능에서 회귀 통과), G3·G5·G7(진입점 `BotMode.Move`를 채움. 경로 전체 통과는 F5 뒤). 앞선 기능 [F1 기획서](../f1-skeleton/plan.md)(E절 임시 자리, G절 화면 경계, H절 진입점). 참고 분석 문서 없음. 예전 판 코드 `Assets/DeadSurvivor/Runtime/DSGame.Formation.cs`·`DSGame.MapMotion.cs`·`DSGame.cs`·`DSUI.cs`·`DSUI.Formation.cs`·`DSWorld.cs`(읽기 전용, 규칙만 옮김). 긴 근거는 [notes.md](notes.md)
- 구현 작업 규모: L2 — 새 데이터 스키마(진형·이동 규칙)와 대원 좌표를 쓰는 쪽이 바뀐다(F1 고정 자리 → `PartyMoveService`)

## 목표
손가락 하나(PC는 WASD)로 원정대 4명을 한 무리로 끌고 다닌다. 손을 떼면 멈추고, 대원은 고른 진형의 자기 자리를 따라가며, 쓰러진 대원은 그 자리에 남는다. 카메라가 원정대를 따라간다.
정비에서 진형 3종 중 하나를 골라 다음 웨이브에 쓴다. F1 임시 규칙 "고정 4자리, 움직이지 않음"과 고정 카메라를 대체하고, 판정 봇의 `BotMode.Move`를 채운다.

## 참고 분석 요약
예전 판(같은 게임의 이전 구현) 코드에서 옮길 규칙. 모두 관찰이며 출처는 위 경로다.

- 진형 3종: 균형형·쐐기형·횡대형, 4자리 좌표(`DSGame.Formation.cs` 9·14행). 자리는 편성 수(쓰러진 대원 포함) 기준으로 정한다(`DSGame.Run.cs` 58행)
- 이동: 중심 += 입력 × 140 × dt. 살아 있는 대원은 중심 + 자기 자리 쪽으로 min(1, dt × 12)만큼 다가간다. 쓰러진 대원은 좌표가 그대로다(`DSGame.MapMotion.cs` 17~18행)
- 입력: 조이스틱이 눌려 있으면 조이스틱, 아니면 키보드. 길이 1로 자른다(`DSGame.cs` 83~95행). 조이스틱 = (지금 − 누른 자리) ÷ 45(캔버스 1600×900), 누른 자리에 고리·손잡이가 나타나고 떼면 사라진다(`DSUI.cs` 435~467행). 누르는 영역은 전투 화면 위 표시줄 아래 전체(`DSUI.cs` 222~225행)
- 진형 고르기: 정비에서만, 다음 웨이브 시작에 적용, 바꾸기 전까지 유지, 한 판 시작은 균형형(`DSGame.Formation.cs` 20~28행, `DSGame.Run.cs` 34·72행). 정비 화면에 버튼 3개와 자리 모양 미리보기(`DSUI.Formation.cs`)
- 부활한 대원은 중심 + 자기 자리에 놓인다(`DSGame.Inventory.cs` 59행)
- 카메라 = 원정대 중심, 늦추지 않음(`DSWorld.cs` 150행). 예전 좌표는 y가 화면 아래로 +다(`DSWorld.cs` 91행, `DSGame.cs` 93~94행 W → y −1)

## 적용안

### 가져올 것 / 버릴 것 / 바꿀 것
- 가져올 것: 진형 3종의 모양·값, 이동 속도 140, 따라가기 비율 12, 쓰러진 대원 규칙, 조이스틱 우선, 손 떼면 0, 정비에서 고르고 다음 웨이브에 적용, 카메라 = 중심
- 버릴 것: 맵 내비게이션·충돌(`DSMapMotion`, 3D 맵), 맵 명령 큐, 방향키(게임 기획서는 WASD), 코드 안의 진형 이름 배열
- 바꿀 것: 좌표 y 부호(새 바닥 평면은 y = 화면 위), 화면 방향 → 바닥 방향을 아이소 역변환으로(A4·A5), 조이스틱 반지름을 새 캔버스 기준으로, 진형 값을 데이터로, 들판 경계(C절), 웨이브 시작에 살아 있는 대원을 자리에 놓기, 판정 봇은 원 돌기

### 기존 시스템과의 관계
확장. F1의 `PartyModel`(대원 바닥 좌표), `RunService.Step` 순서, `IsoProjection`, 카메라, `PlayableProbe`·`PlayableBot` 위에 이동(Service·Model)과 진형(Repository·Service·Model), 입력 Presenter·조이스틱 View, 정비 진형 View를 더한다. 대원 좌표를 쓰는 곳은 `PartyMoveService` 하나가 된다. F1 임시 칸 `memberSpots`를 지운다.

### A. 이동 규칙 (S1, 바닥 평면 A5)
- 원정대 중심 `Center`(바닥 좌표)가 이동의 원본이다. 한 판 시작 때 들판 가운데.
- 이동 명령 = 화면 기준 입력 `s`(x 화면 오른쪽, y 화면 위, 길이 ≤ 1).
- 한 걸음(F1 `simStep`)마다:
  1. 바닥 방향 `d` = `s`를 `IsoProjection` 역변환으로 바닥에 옮긴 뒤 길이 1로 맞춘 것(= (sx, 2·sy)의 단위 벡터). 화면에서 민 방향 그대로 움직이게 하려는 것(A4). `s`가 0이면 움직이지 않는다.
  2. `Center += d × |s| × moveSpeed × dt`
  3. `Center`를 들판 경계 안으로 옮긴다(C절).
  4. 살아 있는 대원 i: `pos += (Center + slot[i] − pos) × min(1, followRate × dt)`. 쓰러진 대원(HP 0): `pos` 그대로.
- `slot[i]` = 지금 진형의 i번째 자리(캐릭터 `order` 순). 자리는 4명 기준으로 고정이다. 쓰러진 대원이 있어도 다른 대원의 자리가 바뀌거나 당겨지지 않는다(사망자 자리 유지).
- 진형의 방향은 화면 기준으로 고정이다(A4). 움직이는 방향으로 돌지 않는다.
- 손을 떼면(입력 0) 중심은 그 걸음에서 멈추고 대원은 자기 자리로 다가가 멈춘다(비율 ≤ 1이라 넘치지 않는다).
- 웨이브 시작(한 판 시작의 웨이브 1, 다음 웨이브): 이동 입력 0, 대기 진형 적용(B절), 살아 있는 대원을 `Center + slot`에 바로 놓는다. 쓰러진 대원은 그대로 둔다. F3의 부활로 HP가 찬 대원은 다음 웨이브 시작에 자기 자리로 간다(부활 코드가 자리를 정하지 않는다). `Center`는 웨이브 사이에 그대로이고 새 판에서만 가운데로 돌아간다.
- 걸음 순서: F1 `RunService.Step`의 ③ 적 생성 뒤, ④ 기본 무기 앞에 "③-1 원정대 이동"을 넣는다(예전 순서: 생성 → 원정대 이동 → 장비 → 적).
- 일시정지 중에는 `Step`이 돌지 않아 움직이지 않는다. 배속 ×2는 같은 실제 시간에 두 배 간다.

### B. 진형 3종 (S1, V3)
| id | order | 이름 | 자리: 브루노·레나·미라·볼트 (바닥, y 위 +) | 예전 좌표 (y 아래 +) |
|---|---|---|---|---|
| balanced | 1 | 균형형 | (−24, 24)·(24, 24)·(−24, −24)·(24, −24) | (−24, −24)·(24, −24)·(−24, 24)·(24, 24) |
| wedge | 2 | 쐐기형 | (0, 48)·(−48, 0)·(48, 0)·(0, −48) | (0, −48)·(−48, 0)·(48, 0)·(0, 48) |
| line | 3 | 횡대형 | (−72, 0)·(−24, 0)·(24, 0)·(72, 0) | 같음 |

- 예전 값의 y 부호만 뒤집었다(화면에서 같은 모양, 같은 대원 위치). 세 진형 모두 자리 평균이 (0, 0)이라 예전의 "평균 빼기"는 하지 않고 데이터 검사가 평균 0을 본다.
- 한 판 시작: 지금 진형 = 대기 진형 = `order`가 가장 작은 진형(균형형).
- 고르기는 정비 화면에서만. 진형 버튼은 대기 진형만 바꾼다. 다음 웨이브 시작에 지금 진형 = 대기 진형. 고른 진형은 다시 고를 때까지 다음 웨이브에도 쓴다. 정비가 아닐 때나 없는 id면 경고 로그를 남기고 무시한다.
- 진형 수 = 데이터 파일 수. 정비 화면은 장면에 둔 버튼 중 진형 수만큼 켠다(진형을 더하면 데이터 파일 + 장면 버튼 하나, 코드 수정 없음). 버튼이 모자라면 오류 로그.

### C. 들판 경계
- F1 들판은 유한하다(`fieldCells` 64칸 아이소). 바닥 평면에서 들판은 마름모 |x − cx| + |y − cy| ≤ R이다(c = 들판 가운데, R은 F1 들판 크기에서 구함, 지금 값 약 2832).
- `Center`는 카메라 화면에 들판 밖이 보이지 않는 범위에서만 움직인다: |x − cx| + |y − cy| ≤ R − (hw + hh). hw·hh는 보이는 바닥 영역의 반폭·반높이(F1이 적 생성 거리에 쓰는 카메라 값과 같은 출처). 넘으면 그 마름모 위 가장 가까운 점으로 옮긴다(벽을 따라 미끄러진다).
- 보이는 영역이 들판보다 커서 R − hw − hh ≤ 0이면 `Center`를 들판 가운데에 두고 경고 로그.
- 적·투사체는 이 경계를 쓰지 않는다(F3·F4 몫).

### D. 입력 (V2)
- 조이스틱: 전투 판넬 전체(HUD 뒤, 안전 영역 밖 포함)를 누르면 누른 자리에 고리·손잡이가 나타난다. 끌면 `s` = (지금 − 누른 자리) ÷ (`joystickRadius` × 캔버스 배율), 길이 1로 자른다. 손잡이는 누른 자리에서 `s` × 손잡이 이동 거리에 그린다. 떼면 사라지고 `s` = 0. 처음 누른 손가락 하나만 따른다. 일시정지·배속 같은 HUD 버튼 위를 누르면 조이스틱이 생기지 않는다(HUD가 앞). PC에서 마우스로 끌어도 같다.
- `joystickRadius` = 18 캔버스 단위(F1 캔버스 기준 640×360). 예전 45 ÷ 900 = 화면 높이의 5%를 같은 비율로 옮겼다. 고리 56·손잡이 24·손잡이 이동 14(예전 140·60·35를 같은 비율로, 장면 배치 값).
- PC: W 위·S 아래·A 왼쪽·D 오른쪽, `s` = (D − A, W − S), 길이 1로 자른다. 조이스틱이 눌려 있으면 키보드는 무시한다.
- 입력 Presenter는 전투 중 매 프레임 조이스틱·키보드를 읽어 **지난 값과 다를 때만** `PartyMoveService.SetMoveInput(s)`를 부른다(사람이 만지지 않으면 판정 봇의 명령이 덮이지 않는다). 전투가 아니게 되면 조이스틱을 숨기고 0을 보낸다. 웨이브 시작에 기억한 지난 값을 지워 눌린 키가 다시 들어가게 한다.
- 그림: 고리·손잡이는 임시 원(F1 `PlaceholderSpriteFactory`). 그림은 아트 단계(기준서 3.3 UI 세트)가 넣는다.

### E. 카메라 따라가기
매 프레임 원정대를 그린 뒤 카메라 중심 = `IsoProjection`(`Center`). 늦추거나 부드럽게 하지 않는다(예전 그대로). C절 때문에 들판 밖이 보이지 않는다. F1 `WorldPresenter`의 카메라 중심 줄을 바꾼다.

### F. 정비 화면 진형 (V3)
- 판넬 하나(안전 영역 판넬 아래): 진형 버튼(데이터 이름, 대기 진형 강조), 미리보기(대원 4명 이름 표지를 대기 진형 자리에 전투와 같은 모양 — 세로 절반 압축 — 으로, 쓰러진 대원은 회색).
- 정비에 들어올 때와 버튼을 누를 때 `UpdateUI` 한 번으로 갱신한다. F1 `UI_PrepView`·`PrepPresenter`는 고치지 않는다(따로 View·Presenter).

### G. 화면과 기능의 경계 (F1 G절 표에서 이 기능이 채우는 칸)
| 화면 | F2가 넣는 것 | 넣지 않는 것 |
|---|---|---|
| V2 전투 | 조이스틱 영역·고리·손잡이, WASD, 원정대 이동·진형 따라가기, 카메라 따라가기 | 걷기 그림·바라보는 방향(아트), 아이템 쿨타임·발동(F3), 몬스터 5종(F4), 피격 연출(F6) |
| V3 정비 | 진형 판넬(버튼 3개 + 미리보기) | 가방·보관함·전리품(F5), 부활(F3) |

### H. 판정 진입점과 경로
진입점 `PlayableProbe`에 이 기능이 더하는 멤버(표·경로의 식은 `PlayableProbe.` 접두어를 줄였다. 파일에는 붙인다):
- 읽기: `Formation`·`PendingFormation`(진형 id), `PartyX`·`PartyY`(중심 바닥 좌표), `PartyDeltaX`·`PartyDeltaY`·`PartyMovedSinceMark`(표시 뒤 중심 변화), `MaxSlotError`(살아 있는 대원과 `Center + slot`의 최대 거리), `MaxDeadMoved`(표시 때도 지금도 쓰러진 대원이 표시 뒤 움직인 최대 거리, 없으면 0), `CameraError`(카메라 중심과 `IsoProjection(Center)`의 월드 거리), `IsJoystickShown`.
- 조작: `SetMove(float x, float y)`(= `PartyMoveService.SetMoveInput`, 입력 Presenter와 같은 명령), `JoystickDown(float x, float y)`·`JoystickDrag(float x, float y)`·`JoystickUp()`(화면 0~1 비율, 왼쪽 아래 0,0. `UI_JoystickView`의 포인터 처리 메서드를 EventSystem처럼 부른다), `PressFormation(string id)`(진형 버튼 처리 메서드), `MarkPositions()`(지금 중심·대원 좌표를 기억).
- 봇 `BotMode.Move`: 전투 중 매 프레임, 들판 가운데를 중심으로 반지름 600(바닥 단위)인 원을 시계 방향으로 돌도록 원하는 바닥 방향을 구해 화면 방향으로 바꾼 뒤 `SetMove`(세기 1)로 넣는다. 원보다 바깥·안쪽이면 원 쪽으로 돌아가는 성분을 섞고, 가운데에서 1 미만이면 오른쪽으로 간다. Move 깃발이 빠지면(`StartBot`으로 모드가 바뀜, `StopBot`) 0을 한 번 보낸다. F1 봇의 "아직 없는 깃발" 경고에서 Move를 뺀다.
- 경로 파일 `Tools/playable/route.json`은 고치지 않는다. G3·G7의 `StartBot(BotMode.Move …)`가 이 기능부터 실제로 원정대를 움직인다. G3 끝까지 통과는 F5의 `StashCount`, G4·G5는 F5의 `PlaceFirstStashItem`, G7은 F5의 자동 장착이 있어야 하므로 이 기능만으로는 G3부터 통과할 수 없다. 그래서 이 기능의 흐름 경로로 확인한다.
- 흐름 경로 `Tools/playable/f2-movement-flows.json`: `scene`·`size`·`sizes`는 `route.json`과 같고 `time_scale` 1. 단계(표의 순서 그대로):

| 단계 | do | sleep | wait / expect | capture |
|---|---|---|---|---|
| L1 | — | — | `Screen == "Lobby"` | — |
| B1 | `UseRunSettings(1, 0f); PressDepart();` | — | `Screen == "Battle" && Wave == 1` / `Formation == "balanced" && MaxSlotError < 0.5f && CameraError <= 0.021f` | — |
| RIGHT | `MarkPositions(); SetMove(1f, 0f);` | 1 | — / `PartyDeltaX > 70f && Math.Abs(PartyDeltaY) < 0.5f && CameraError <= 0.021f` | true |
| UP | `MarkPositions(); SetMove(0f, 1f);` | 1 | — / `PartyDeltaY > 70f && Math.Abs(PartyDeltaX) < 0.5f && CameraError <= 0.021f` | — |
| STOP | `SetMove(0f, 0f);` | 0.5 | — / `Screen == "Battle"` | — |
| STILL | `MarkPositions();` | 0.5 | — / `PartyMovedSinceMark < 0.01f && MaxSlotError < 0.5f` | — |
| PREP1 | — | — | `Screen == "Prep" && CompletedWave == 1`(timeout 40) | — |
| FORM | `PressFormation("wedge");` | — | — / `PendingFormation == "wedge" && Formation == "balanced"` | all |
| WEDGE | `PressNextWave();` | — | `Screen == "Battle" && Wave == 2` / `Formation == "wedge" && MaxSlotError < 0.5f` | true |
| JOY | `MarkPositions(); JoystickDown(0.5f, 0.5f); JoystickDrag(0.5f, 0.6f);` | 1 | — / `IsJoystickShown && PartyDeltaY > 70f && Math.Abs(PartyDeltaX) < 0.5f` | all |
| JOYUP | `JoystickUp();` | 0.3 | — / `!IsJoystickShown` | — |
| JOYSTILL | `MarkPositions();` | 0.5 | — / `PartyMovedSinceMark < 0.01f` | — |
| PREP2 | — | — | `Screen == "Prep" && CompletedWave == 2`(timeout 40) | — |
| LINE | `PressFormation("line"); PressNextWave();` | — | `Screen == "Battle" && Wave == 3` / `Formation == "line" && MaxSlotError < 0.5f` | true |
| WINA | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.AutoNext);` | — | `Screen == "Result"`(timeout 60) / `Outcome == "Win" && Formation == "line"` | — |
| BOT | `StopBot(); UnityEngine.Time.timeScale = 1f; UseRunSettings(1, 1f); PressToLobby(); PressDepart(); MarkPositions(); UnityEngine.Time.timeScale = 20f; StartBot(BotMode.Move);` | — | `Screen == "Prep" && CompletedWave == 1`(timeout 30) / `AliveCount == 4 && Formation == "balanced" && PartyMovedSinceMark > 300f` | true |
| MOVE2 | `UnityEngine.Time.timeScale = 1f; StartBot(BotMode.Move \| BotMode.AutoNext);` | — | `Screen == "Battle" && Wave == 2` | — |
| AUTONEXT | `StartBot(BotMode.AutoNext);` | 0.3 | — / `Screen == "Battle"` | — |
| STAND | `MarkPositions();` | 0.5 | — / `PartyMovedSinceMark < 0.01f` | — |
| MOVEALL | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.Move \| BotMode.AutoNext);` | — | `Screen == "Result"`(timeout 60) / `WavesSurvived >= 1` | — |
| DEAD | `StopBot(); UnityEngine.Time.timeScale = 1f; UseRunSettings(2, <사망 배율>); PressToLobby(); PressDepart(); StartBot(BotMode.AutoNext);` | — | `Screen == "Battle" && AliveCount >= 1 && AliveCount <= 3`(timeout 60) | — |
| DEADMOVE | `MarkPositions(); SetMove(1f, 0f);` | 1 | — / `AliveCount >= 1 && MaxDeadMoved < 0.01f && PartyDeltaX > 70f` | — |
| DEADSTOP | `SetMove(0f, 0f);` | 0.5 | — / `AliveCount >= 1 && MaxDeadMoved < 0.01f && MaxSlotError < 0.5f` | true |
| LOSE | `UnityEngine.Time.timeScale = 20f;` | — | `Screen == "Result"`(timeout 60) / `Outcome == "Lose"` | — |
| RUN4 | `StopBot(); UnityEngine.Time.timeScale = 1f; PressToLobby(); PressDepart();` | — | `Screen == "Battle" && Wave == 1` / `AliveCount == 4 && Formation == "balanced" && MaxSlotError < 0.5f && RunCount == 4` | — |

- `<사망 배율>`은 구현이 가만히 선 원정대에서 대원이 하나씩 쓰러지는(첫 사망과 전멸 사이가 몇 초 이상인) 적 수 배율을 정해 파일에 적는다. 거리 기준 70은 1초 × 속도 140의 절반, `CameraError` 0.021은 한 픽셀(1/48 유닛)이다.
- sleep·timeout·거리 기준은 구현이 실제 시간에 맞춰 고칠 수 있지만 단계와 확인 항목은 빼지 않는다. 한 웨이브(30초) 안에 끝나지 않으면 단계를 다음 웨이브로 나눈다. BOT의 `AliveCount == 4`가 실패하면 F1 수치가 아니라 봇부터 의심한다(게임 기획서 근거 메모).

## 데이터 스키마
위치는 F1과 같은 `Assets/Game/03.Resources/Resources/Data/`. 검사 명령은 F1의 `python3 -B ./tools/check_data.py`에 더한다.

| 위치 | 이름 | 필드 | 타입 | 키/기본값 | 쓰는 쪽 → 읽는 쪽 |
|---|---|---|---|---|---|
| JSON `Formation/<id>.json` | 진형(3개) | `id`, `order`, `name`, `slots` | string, int, string, float[N][2] | 키 `id` = 파일명. B절 표의 값. 검사: 개수 1~6, `order` 1..N 빈칸 없음, `slots` 수 = 캐릭터 파일 수(4), 자리 평균 (0, 0) ±0.01, 같은 자리 두 번 없음 | 기획(데이터) → `FormationRepository` → `FormationService`·`FormationPresenter` |
| JSON `Party/move_rules.json` | 이동 규칙(1개) | `moveSpeed`, `followRate`, `joystickRadius` | float(> 0), float(> 0), float(> 0) | 140(바닥 단위/초), 12(1/초), 18(캔버스 단위) | 데이터 → `PartyMoveService`·`MoveInputPresenter` |
| JSON `Run/placeholder_combat.json`(F1) | 임시 전투 | `memberSpots` 칸을 지운다 | | 검사: `memberSpots`가 있으면 실패 | F1 E절 "대체하는 기능이 칸을 지운다" |
| 세이브 | 없음 | | | 저장 없음(A9) | |
| 서버 | 없음 | | | | |

- 실행 중 상태(저장 안 함): `PartyMoveModel`(`Center`, `MoveInput`), `FormationModel`(`CurrentId`·`PendingId`, R3 `ReadOnlyReactiveProperty<string>`). 대원 바닥 좌표는 F1 `PartyModel` 그대로이고 쓰는 쪽만 `PartyMoveService`로 바뀐다.

## 클라 변경
경로는 `Assets/Game/` 기준. 어셈블리는 F1 것(`Game`, `Game.Tests.EditMode`)을 쓴다.

새 파일
- `02.Scripts/Features/Party/Data/FormationData.cs`, `MoveRulesData.cs` — JSON 형
- `02.Scripts/Features/Party/Registry/FormationRepository.cs` — 진형 파일 읽기, `order` 정렬
- `02.Scripts/Features/Party/Model/FormationModel.cs`, `PartyMoveModel.cs`
- `02.Scripts/Features/Party/Service/FormationService.cs` — `ResetForRun`, `SelectPending(id)`, `ApplyPending`, 대원 i의 자리
- `02.Scripts/Features/Party/Service/PartyMoveService.cs` — `SetMoveInput`, `ResetForRun`(가운데·모두 자리), `StartWave`(입력 0·살아 있는 대원 자리), `Step(dt)`(A절 1~4)
- `02.Scripts/Features/Party/Utils/PartyMoveMath.cs` — 화면 ↔ 바닥 방향, 따라가기 한 걸음, 들판 마름모 안으로 옮기기(순수 함수)
- `02.Scripts/Features/Party/Utils/MoveInputMath.cs` — 조이스틱 입력, WASD 입력(순수 함수)
- `02.Scripts/Features/Party/Presenter/MoveInputPresenter.cs` — `ITickable`, D절
- `02.Scripts/Features/Party/View/UI_JoystickView.cs` — 포인터 처리, 고리·손잡이 표시(`UpdateUI`), 사실만 알린다
- `02.Scripts/Features/Party/Presenter/FormationPresenter.cs`, `View/UI_FormationView.cs` — F절
- `03.Resources/Resources/Data/Formation/balanced.json`, `wedge.json`, `line.json`, `03.Resources/Resources/Data/Party/move_rules.json`
- `Tools/playable/f2-movement-flows.json` — H절 흐름 경로
- `Tests/EditMode/PartyMoveMathTests.cs`, `MoveInputMathTests.cs`, `PartyMoveServiceTests.cs`, `FormationServiceTests.cs`

F1 파일에 이 기능이 더하는 줄·칸(뒤 기능과 나란히 고치므로 이것만 바꾼다)
- `02.Scripts/App/GameSceneLifetimeScope.cs` — 등록 줄: `FormationRepository`, 이동 규칙 데이터, `FormationModel`, `PartyMoveModel`, `FormationService`, `PartyMoveService`(Scoped), `MoveInputPresenter`·`FormationPresenter`(EntryPoint), `UI_JoystickView`·`UI_FormationView`(`RegisterComponentInHierarchy`)
- `02.Scripts/Features/Run/Service/RunService.cs` — 생성자 의존 2개, 한 판 시작에 `FormationService.ResetForRun`·`PartyMoveService.ResetForRun`(편성을 만든 뒤), 웨이브 시작 처리 한 곳에 `ApplyPending`·`StartWave`, `Step`의 ③ 뒤 `PartyMoveService.Step(dt)` 한 줄
- `02.Scripts/Features/Party/Service/PartyService.cs` — 편성 때 `memberSpots`로 자리를 정하던 줄을 지운다. F1에 원정대 중심을 내주는 멤버(적 생성 기준 등)가 있으면 그 멤버가 `PartyMoveModel.Center`를 돌려주게 바꾸고 읽는 쪽은 고치지 않는다
- `02.Scripts/Features/Run/Data/PlaceholderCombatData.cs`, `03.Resources/Resources/Data/Run/placeholder_combat.json` — `memberSpots` 칸만 지운다
- `02.Scripts/Features/Field/Presenter/WorldPresenter.cs` — 카메라 중심을 정하는 줄(E절)
- `01.Scene/Main.unity` — 전투 판넬의 첫 자식(HUD보다 뒤)에 `JoystickArea`(`UI_JoystickView`, 화면 전체 투명 Image, 고리·손잡이 자식), 정비 판넬의 안전 영역 아래에 `FormationPanel`(`UI_FormationView`, 버튼 3개, 미리보기 표지 4개). 다른 판넬·HUD 배치는 건드리지 않는다
- `02.Scripts/App/PlayableProbe.cs` — H절 멤버와 `PlayableBot`의 Move 처리
- `Tools/check_data.py` — 진형·이동 규칙 검사, `memberSpots`가 남아 있으면 실패, `--self-test`에 틀린 데이터(진형 자리 3개, 진형 `order` 빈칸, 자리 평균 ≠ 0, `memberSpots` 남음) 추가
- HUD View(`UI_BattleHudView`, `UI_MemberStatusView`)·`Tools/playable/route.json`·`f1-flows.json`·`Core/Event/GameEvent.cs`(새 이벤트 없음, 콜백·R3로 잇는다)는 고치지 않는다

## 서버 변경과 API 계약
없음. 게임 기획서: 서버 없음, 저장 없음(A9). 이동·진형은 재화·랭킹·치팅 방지와 관계없다.

## 수용 기준
확인 방법: 자동 테스트(EditMode·명령), 에디터(컴파일·에디터 명령으로 상태 확인), 플레이(플레이 모드 동작·화면), 사람.
EditMode 테스트 명령은 F1과 같다: `unity test --platform editmode --assembly Game.Tests.EditMode`(실행기 명령은 구현 단계가 이 레포의 unity-cli 형식으로 적는다). "흐름 경로"는 `python3 -B ./tools/playable.py --route tools/playable/f2-movement-flows.json`이다.

| 번호 | 기준 (확인할 수 있는 문장) | 확인 방법 | 확인 절차 |
|---|---|---|---|
| AC1 | 새 코드·자산을 넣은 뒤 컴파일 에러가 0개다 | 에디터 | `python3 -B ./tools/editor_ready.py` 후 콘솔·Editor.log에 `error CS` 없음 |
| AC2 | 데이터 검사 명령이 지금 데이터에서 OK(exit 0)이고, 진형 자리 3개·진형 `order` 빈칸·자리 평균 ≠ 0·`memberSpots` 남음을 넣은 시험 데이터에서는 exit 1이다. 게임 코드와 데이터에 `memberSpots`가 없다 | 자동 테스트 | `python3 -B ./tools/check_data.py`, `python3 -B ./tools/check_data.py --self-test`, `grep -rn memberSpots Assets/Game` 결과 0줄 |
| AC3 | 판정 경로 G2까지 통과(F1 회귀) | 플레이 | `python3 -B ./tools/playable.py --until G2` |
| AC4 | F1 흐름 경로가 그대로 통과한다(웨이브 넘김·승리·패배·세 번째 판) | 플레이 | `python3 -B ./tools/playable.py --route tools/playable/f1-flows.json` |
| AC5 | 판정 경로 파일의 G1~G8이 바뀌지 않았고, 경로·흐름 경로 파일이 형식 검사를 통과한다 | 자동 테스트 | `python3 -B ./tools/playable.py --check`, `python3 -B ./tools/playable.py --check --route tools/playable/f2-movement-flows.json`, `git diff --stat <시작 커밋> -- Tools/playable/route.json` 빈 결과 |
| AC6 | (G3 진입점) `StartBot(BotMode.Move)`를 켜면 봇이 웨이브 1 동안 원정대를 움직여(시작 자리에서 300 이상) 정비 화면에 도착하고 생존이 4명이다. G3 전체(보관함 3~5개)는 F5 뒤 경로로 확인한다 | 플레이 | 흐름 경로 `BOT` 단계 통과(정비 화면 캡처) |
| AC7 | (G5 진입점) 전투 중 봇 모드가 Move 없는 `BotMode.AutoNext`로 바뀌면 원정대 중심이 멈춘다 | 플레이 | 흐름 경로 `MOVE2`·`AUTONEXT`·`STAND` 단계 통과 |
| AC8 | (G7 진입점) Move와 AutoNext 깃발을 함께 켠 봇으로 한 판이 결과 화면까지 간다. 승리 조건(M1·M6)은 F3·F5 뒤 G7로 확인한다 | 플레이 | 흐름 경로 `MOVEALL` 단계 통과 |
| AC9 | 화면 오른쪽 이동 명령이면 원정대 중심이 바닥 +x로만, 위 명령이면 +y로만 1초에 70 이상 움직이고, 카메라 중심이 원정대 중심에서 1픽셀 안에 있다 | 플레이 | 흐름 경로 `B1`·`RIGHT`·`UP` 단계 통과 |
| AC10 | 손을 떼면(명령 0, 조이스틱 놓기) 원정대 중심이 바로 멈추고 0.5초 안에 대원이 모두 자기 자리(0.5 이내)에 선다 | 플레이 | 흐름 경로 `STOP`·`STILL`·`JOYUP`·`JOYSTILL` 단계 통과 |
| AC11 | 전투 화면을 누르면 조이스틱이 나타나고, 위로 끌면 원정대가 화면 위로 움직이며, 떼면 조이스틱이 사라진다 | 플레이 | 흐름 경로 `JOY`·`JOYUP` 단계 통과 |
| AC12 | 정비에서 쐐기형을 고르면 대기 진형만 바뀌고 다음 웨이브에 대원이 쐐기형 자리에 서며, 횡대형도 같고, 고른 진형은 마지막 웨이브까지 유지되고, 새 판은 균형형으로 시작한다 | 플레이 | 흐름 경로 `FORM`·`WEDGE`·`LINE`·`WINA`·`BOT`·`RUN4` 단계 통과 |
| AC13 | 쓰러진 대원은 원정대가 움직여도 쓰러진 자리에 있고, 살아 있는 대원은 자기 진형 자리를 그대로 지키며, 새 판에서는 네 명 모두 자리에서 시작한다 | 플레이 | 흐름 경로 `DEAD`·`DEADMOVE`·`DEADSTOP`·`LOSE`·`RUN4` 단계 통과 |
| AC14 | 이동 계산: 화면 입력 (1, 0)·(0, 1)·(1, 1)/√2가 바닥 방향 (1, 0)·(0, 1)·(1, 2)/√5가 되고(화면에 그리면 민 방향과 같은 각), 속도는 140 × 입력 세기이며, 중심은 들판 마름모(R − hw − hh) 밖으로 나가지 않고 벽에 비스듬히 밀면 벽을 따라 움직이며, 화면이 들판보다 크면 가운데에 머문다 | 자동 테스트 | `PartyMoveMathTests` |
| AC15 | 입력 계산: 조이스틱은 (지금 − 누른 자리) ÷ (반지름 × 캔버스 배율)이고 반지름 안에서는 끈 거리에 비례, 밖에서는 길이 1이다. WASD는 8방향이고 대각선 길이 1, 반대 키를 함께 누르면 그 축이 0이다 | 자동 테스트 | `MoveInputMathTests` |
| AC16 | 따라가기: 진형 3종에서 오른쪽·왼쪽·위·아래로 움직이다 반대로 돌리거나 멈춰도 1초 뒤 모든 대원이 자리 5 이내, 한 걸음 대원 이동이 이동 속도 × 걸음 시간 + 0.01 이하, 대원 사이 거리가 진형의 가장 가까운 자리 간격 − 0.1 아래로 줄지 않고, 멈추면 자리 오차가 늘지 않으며 1초 뒤 0.01 미만이다 | 자동 테스트 | `PartyMoveServiceTests.FollowsSlotsInEveryFormation`, `StepMovementBounded`, `KeepsSpacing`, `StopConverges` |
| AC17 | 사망자: HP 0 대원은 걸음에서 좌표가 바뀌지 않고 나머지 대원의 자리는 4명 기준 그대로다. 웨이브 시작에는 살아 있는 대원(HP가 다시 찬 대원 포함)만 자리로 가고 쓰러진 대원은 그대로다. 새 판은 가운데에서 네 명 모두 자리로 시작한다 | 자동 테스트 | `PartyMoveServiceTests.DeadMemberStaysAndSlotsKept`, `StartWavePlacesAliveOnly`, `ResetForRunPlacesAll` |
| AC18 | 같은 이동 입력이면 프레임을 1/60초 60번으로 주든 1초 1번으로 주든 원정대 중심·대원 좌표가 같다 | 자동 테스트 | `PartyMoveServiceTests.SameInputSameResultAcrossFrameSplits`(F1 `SameSeedSameResultAcrossFrameSplits`와 같은 걸음 나누기) |
| AC19 | 진형: 한 판 시작에 지금·대기 진형이 `order` 1(균형형)이고, 정비에서만 대기 진형이 바뀌며 지금 진형은 다음 웨이브 시작에 바뀌고, 다시 고르지 않으면 유지되고, 새 판은 균형형이며, 정비 밖이나 없는 id의 고르기는 무시된다 | 자동 테스트 | `FormationServiceTests` |
| AC20 | 화면: 전투 중 누른 자리에 조이스틱 고리·손잡이가 보이고, 정비 화면에 진형 버튼 3개(균형형·쐐기형·횡대형, 고른 것 강조)와 대기 진형 모양의 4명 미리보기가 보이며, 진형을 바꾼 웨이브의 전투 화면에서 대원이 그 모양으로 서 있다 | 플레이 | 흐름 경로 `JOY`·`FORM`·`WEDGE`·`LINE`·`DEADSTOP` 캡처를 열어 항목별로 확인하고 본 캡처 목록을 보고에 적음 |
| AC21 | 노치 기기에서 진형 판넬이 가려지지 않고, 16:9·19.5:9·21:9·4:3 가로 화면에서 진형 판넬·조이스틱이 잘리거나 HUD와 겹치지 않는다 | 플레이 | 흐름 경로 `FORM`·`JOY`의 `capture: "all"` 4장씩을 열어 봄 + Device Simulator 노치 기기 1대(가로) 정비 화면 캡처 |
| AC22 | 조이스틱·WASD로 4명을 끌고 다니는 손맛이 있고 아이소 화면에서 방향이 헷갈리지 않으며, 진형 3종의 차이가 화면에서 보이고, 일시정지·배속 버튼을 누르면 조이스틱이 생기지 않으며, 정비 미리보기에서 쓰러진 대원이 회색이다 | 사람 | 디렉터가 사용자에게 넘김(게임 기획서 10절의 첫 줄) |

### 기존 테스트
F1 테스트 코드는 아직 레포에 없다(F1 구현 중, 2026-11-15 이 worktree의 `Assets/Game`에 `[Test]` 없음). 그래서 F1 기획서 수용 기준(AC6~AC13·AC17·AC18)과 `route.json` G1·G2·`f1-flows.json`의 마지막 기대를 이 기획서의 흐름과 대조했다.
- 대조 결과 바뀌지 않는 것: `RunServiceTests`(웨이브 시간표·동시 패배·즉시 패배·승리·버틴 웨이브·버닝 타임·일시정지·배속 — 좌표를 보지 않음), `CoreTests`, `IsoProjectionTests`, `SafeAreaLayoutTests`, `PartyServiceTests`의 순서·HP·시작 아이템·피해 공식, `PlaceholderCombatTests`의 "처치 1 이상·접촉 피해·쓰러진 대원 제외·프레임 나누기 동일", 경로 G1·G2와 `f1-flows.json`(좌표 기대 없음, 입력이 없으면 원정대가 움직이지 않는다).
- 바뀔 수 있는 것은 아래 목록뿐이다. 구현 단계는 실제 F1 테스트를 열어 대조하고, 목록 밖의 기대가 바뀌면 고치지 말고 멈춰 보고한다.

기대 변경 허용(이 목록에 있는 것만 구현 단계가 기대값을 고칠 수 있다):

| 테스트 | 지금 기대(끝 상태·값) | 새 기대 | 이유(기획서 절) |
|---|---|---|---|
| `PartyServiceTests` 중 대원 시작 좌표를 단정하는 것(있다면) | 대원 i = `memberSpots[i]`(브루노 (−24, −24) …) | 대원 i = 들판 가운데 + 균형형 `slots[i]`(브루노 (−24, 24) …) | 적용안 A·B, F1 E절 대체 |
| `DataLoadTests` 중 `placeholder_combat.json` 칸을 단정하는 것(있다면) | `memberSpots` 4×2 있음 | `memberSpots` 없음 | 데이터 스키마 |
| `PlaceholderCombatTests` 중 시드 고정 판의 정확한 처치 수·대원별 HP 값을 단정하는 것(있다면) | F1 자리에서 나온 값 | 균형형 자리(같은 네 좌표, 대원 배치만 위아래가 바뀜)에서 나온 값 | 적용안 B |
| `check_data.py --self-test`(F1)의 임시 전투 정상 데이터 | `memberSpots`가 있어야 통과 | `memberSpots`가 있으면 실패 | 데이터 스키마 |

## 열린 질문
없음. 작은 갈림길은 아래 가정으로 정했다(승인 때 함께 확인).

### 가정
| 번호 | 가정 | 틀리면 바뀌는 것 |
|---|---|---|
| FA1 | 예전 진형 좌표는 y 부호만 뒤집어 옮긴다(예전 y = 화면 아래, 새 바닥 평면 y = 화면 위). 화면에서 모양·대원 위치가 예전과 같다 | B절 표, 진형 데이터 |
| FA2 | "화면 기준 조이스틱"(A4)은 화면에서 민 각도 그대로 움직이는 것으로 읽고, 화면 방향을 아이소 역변환으로 바닥에 옮긴다. 바닥에서는 모든 방향이 같은 속도라 화면에서는 세로 이동이 가로의 절반 속도로 보인다 | A절 1, AC14 |
| FA3 | 들판 경계(C절)를 이동 규칙에 넣는다. S1에 적혀 있지 않지만 F1 들판이 유한해 이동이 생기면 들판 밖 빈 화면으로 나갈 수 있다. 경계는 카메라에 들판 밖이 보이지 않는 마름모다 | C절, AC14 |
| FA4 | 웨이브 시작에 살아 있는 대원을 자리에 바로 놓는다(예전은 한 판 시작·부활 때만). 진형 바꿈이 웨이브 시작에 바로 보이고, F3 부활이 자리 코드를 몰라도 된다 | A절, AC17 |
| FA5 | 조이스틱은 누른 자리에 생기는 떠다니는 방식, 반지름은 화면 높이의 5%(예전 비율), 첫 손가락 하나만 따른다 | D절, 이동 규칙 데이터 |
| FA6 | PC 입력은 WASD만(예전의 방향키는 게임 기획서에 없어 뺀다) | D절 |
| FA7 | 판정 봇 Move는 들판 가운데를 도는 원(반지름 600). 예전 실험 봇의 사각형 돌기(`Assets/DeadSurvivor/Editor/DSExperiments.cs` 109~111행)보다 경계에 밀려도 같은 길로 돌아온다. 못 버티면 봇의 반지름·방식부터 고친다 | H절, AC6, G3·G7 |
| FA8 | 진형 간격은 예전 값(바닥 단위) 그대로다. F1 `unitScale`에서 균형형 대원 간격은 화면에서 가로 약 26px·세로 약 13px라 48px 캐릭터 그림이 겹쳐 보일 수 있다. 그림이 들어간 뒤 사람이 보고 F1 `unitScale`이나 진형 `slots`를 데이터로 고친다(코드 수정 없음) | 진형 데이터, F1 `run_rules.json` |
| FA9 | F1 테스트는 기획서로 대조했다. 구현 단계가 실제 테스트를 열어 "기대 변경 허용" 목록 밖이 바뀌면 멈춘다 | 수용 기준 "기존 테스트" |
| FA10 | 카메라는 늦추지 않고 중심을 바로 따라간다(예전 그대로) | E절 |
| FA11 | 정비 진형 판넬에 자리 모양 미리보기를 둔다(예전 화면에 있었고, 진형을 보고 고르는 데 필요한 최소) | F절, AC20 |
