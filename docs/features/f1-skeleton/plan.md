# F1 한 판의 뼈대 기획서

- 상태: 초안
- 서버 필요: 아니오
- 관련 문서: [게임 기획서](../../game/gdd.md) F1(S4, S8, V1, V2, V3, V4), 판정 단계 G1·G2(이 기능에서 통과), G3~G8(경로 파일에 적기만 함). 참고 분석 문서 없음. 예전 판 코드 `Assets/DeadSurvivor/Runtime/DSGame.Run.cs`·`DSGame.Combat.cs`·`DSUI.cs`와 예전 값 `Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json`(읽기 전용, 규칙만 옮김). 긴 근거는 [notes.md](notes.md)

## 목표
Play를 누르면 로비 → 전투 → 정비 → 결과 → 로비가 실제 규칙으로 이어지는 한 판의 틀을 만든다. 임시 도형 원정대 4명이 기본 무기 하나로 임시 적과 싸우고, 10웨이브의 시간이 흘러 승패가 난다.
뒤 기능(F2~F6)은 이 틀의 정해진 자리에 시스템을 채우고, 판정 경로(`tools/playable/route.json`)에 자기 단계를 이어 붙인다.

## 참고 분석 요약
예전 판(3D, 같은 게임의 이전 구현) 코드에서 옮길 규칙. 모두 관찰이며 출처는 위 경로다.

- 한 판 시작: 편성 순서대로 원정대를 만들고 HP를 최대로, 시작 아이템 1개를 장착, 웨이브·처치·전투 시간을 0으로 한 뒤 웨이브 1을 연다 (관찰, `DSGame.Run.cs` `StartRun`)
- 웨이브 끝: 시간이 끝나면 먼저 전멸인지 본다. 전멸이면 패배, 아니면 마지막 웨이브면 승리, 그 밖에는 정비로 간다 (관찰, `FinishWave`). 전투 중 전멸은 즉시 패배 (관찰, `Result(false)` 호출 위치)
- 웨이브 시간: 한 걸음의 시간을 남은 시간으로 자른다. 같은 걸음에 시간이 끝나고 전멸해도 패배가 먼저다 (관찰, `Simulate`의 `dt = Min(dt, Duration - Elapsed)`와 `FinishWave`의 전멸 우선)
- 버닝 타임: 6웨이브부터, 웨이브 시간의 절반 지점에서 10초 동안 반대쪽 두 방향에서 추가 적이 나온다 (관찰, `DSGame.Combat.cs` `Burning`)
- 적 생성: 화면 대각선 절반 + 70 거리 밖, 간격 = (웨이브 시간 − 4) ÷ (적 수 − 1) (관찰, `Spawn`·`Simulate`)
- 접촉 공격: 거리 ≤ 적 반지름 + 5면 1초마다 피해, 받는 피해 = 피해 × (1 − 방어) (관찰, `UpdateEnemy`·`Damage`)
- 사거리 단위: 아이템 사거리 × 42 (관찰, `DSGame.EquipmentCombat.cs`)
- 전투 화면의 "위협" = 살아 있는 적 수 (관찰, `DSUI.cs` 414행)
- 결과 기록: 웨이브별 시간·처치·생존 (관찰, `WaveRecord`)

## 적용안

### 가져올 것 / 버릴 것 / 바꿀 것
- 가져올 것: 위 판정 순서(전멸 우선), 버닝 타임의 시점·길이, 접촉 공격·방어 공식, 위협 = 살아 있는 적 수, 거리 수치(예전 단위 그대로, 게임 기획서 A5)
- 버릴 것: 6명 중 고르기(A3), 예전 `DSGame` 한 덩어리 구조, 맵 내비게이션·3D 장면, 문자열 상태(`"battle"`), 시험 기록 파일 내보내기
- 바꿀 것: 상태는 Model(R3), 규칙은 Service, 화면은 Presenter + `UI_` View로 나눈다. 전투는 고정 간격으로 쪼개 돌린다(아래 C). 수치는 모두 데이터 파일로 뺀다

### 기존 시스템과의 관계
신규. `Assets/Game/`은 빈 폴더 틀뿐이고 Core 기반도 없다. 예전 코드는 `Assembly-CSharp`의 `DSGame` partial 한 덩어리라 옮겨 쓰지 않고 규칙만 새 구조로 다시 쓴다. 새 코드는 어셈블리 정의(`Game`)로 묶어 예전 코드와 서로 참조하지 않게 한다.

### A. 장면과 DI
- 장면 하나: `Assets/Game/01.Scene/Main.unity`. 화면 네 개(V1~V4)는 이 장면 안의 UI 판넬이고, 전투 월드(바닥·원정대·적)는 같은 장면에 둔다. 화면 전환에 장면을 바꾸지 않는다(한 판의 상태가 장면 사이를 건너지 않아도 된다).
- `GameSceneLifetimeScope` 하나가 이 장면의 모든 등록을 맡는다. Model·Service·Presenter는 `Lifetime.Scoped`, 매 프레임 진행은 Presenter의 `ITickable`.
- 빌드 설정의 장면 목록(`ProjectSettings/EditorBuildSettings.asset`)은 바꾸지 않는다(빌드는 게임 기획서 12절 나중). 판정은 `route.json`의 `scene`으로 이 장면을 연다.

### B. 화면 흐름 (V1~V4)
한 판의 단계 `RunPhase` = 지금 화면이다. `RunModel.Phase`(R3) 하나가 원본이고 `ScreenPresenter`가 이것을 보고 판넬을 켜고 끈다.

| 지금 | 일 | 다음 | 누가 바꾸나 |
|---|---|---|---|
| Lobby(V1) | 출발 버튼 | Battle(V2), 웨이브 1 | `LobbyPresenter.OnDepartClicked` → `RunService.StartRun` |
| Battle(V2) | 웨이브 시간 끝, 생존 ≥ 1, 마지막 웨이브 아님 | Prep(V3) | `RunService.Step` |
| Battle(V2) | 웨이브 시간 끝, 생존 ≥ 1, 마지막 웨이브 | Result(V4), 승리 | `RunService.Step` |
| Battle(V2) | 생존 0(시간과 겹쳐도) | Result(V4), 패배 | `RunService.Step` |
| Prep(V3) | 다음 웨이브 버튼 | Battle(V2), 웨이브 +1 | `PrepPresenter.OnNextWaveClicked` → `RunService.NextWave` |
| Result(V4) | 로비로 버튼 | Lobby(V1) | `ResultPresenter.OnToLobbyClicked` → `RunService.ReturnToLobby` |

- Play를 켜면 Lobby. 버튼 처리는 같은 프레임에 `Phase`를 바꾼다(판정 경로가 누른 뒤 바로 다음 버튼을 누를 수 있게).
- 로비로 돌아가면 적·한 판 상태를 비운다. 다시 출발하면 웨이브 1, 처치 0, 생존 4명, 시작 아이템만 장착한 새 판이다.

### C. 한 판 규칙 (S4)
- 웨이브 수 = 웨이브 데이터 파일 수(10개). 코드에 10을 쓰지 않는다. 웨이브 시간은 게임 기획서 9절 M1: 30·30·35·40·40·45·45·50·55·65초(합 435초).
- 전투는 고정 간격 `simStep`(1/30초, 데이터)으로 쪼개 돈다. 프레임 시간 × 배속을 모아 `simStep`씩 `Step`을 부른다. 같은 시드면 프레임 속도·시간 배율과 상관없이 결과가 같다(M6 시드 판정의 바탕).
- `Step` 한 번의 순서: ① 걸음 시간을 남은 웨이브 시간으로 자름 ② 경과·전투 시간 더함 ③ 적 생성(버닝 타임 포함) ④ 기본 무기 ⑤ 적 이동·접촉 공격 ⑥ 생존 0이면 즉시 패배하고 끝 ⑦ 남은 시간 0이면 웨이브 끝(위 B 표).
- 버닝 타임: 웨이브 데이터 `hasBurningTime`이 참인 웨이브(6~10)에서 `웨이브 시간 × burningStartRatio(0.5)`부터 `burningSeconds(10)`초 동안 `RunModel.IsBurningTime`이 참이다. 그동안 생성기가 반대쪽 두 방향에서 추가 적을 낸다(임시 규칙 E).
- 일시정지: 켜면 `Step`이 돌지 않는다(남은 시간이 멈춘다). 배속: ×1 ↔ ×2 토글(`speedOptions` 데이터). 둘 다 `Time.timeScale`을 건드리지 않는다(판정 경로의 `time_scale`과 겹치지 않게).
- 결과 값: 승패, 버틴 웨이브(승리 10, 패배는 진 웨이브 − 1), 누적 처치, 전투 시간(정비 제외 `Step` 시간 합).
- 난수: 한 판 시작 때 시드를 받는다(A11). 로비 출발은 `run_rules.json`의 기본 설정(시드 0 = 시작 시각에서 뽑음)을 쓴다.
- 판 설정 `RunSettings`(시드, 적 수 배율 `enemyCountScale` 기본 1): 게임의 시작 입력이다. 로비 출발은 `RunSettingsModel`의 지금 값을 넘긴다. 판정 진입점은 이 값을 바꾼 뒤 출발 버튼을 누른다(승패를 써 넣지 않고 같은 규칙을 탄다).

### D. 원정대 편성 (S8)
- 4명 고정, 고르기 없음(A3). 캐릭터 데이터 파일 4개를 `order` 순서(브루노·레나·미라·볼트)로 읽어 편성한다. 수는 파일 수에서 오고 검사 명령이 4명인지 본다.
- 캐릭터 카드(V1): 이름, 직업, HP, 공격 배수(×0.8 등), 방어(15% 등), 시작 아이템 이름. 값은 A1·예전 값(아래 데이터 스키마).
- 시작 아이템 1개씩: 한 판 시작 때 그 캐릭터의 장착 목록에 시작 아이템 하나를 넣는다. F1에서 장착 목록은 아이템 id 목록일 뿐이고, 가방 칸·모양·발동은 F5·F3이 채운다.
- F3(C4)는 **같은 캐릭터 파일과 같은 키**를 넓혀 쓴다(그림 키 등 칸 추가, `atk`·`def`·`hp`를 S2 계산에 사용). 아이템도 F1이 만든 `Item/<id>.json`(이름만)을 F3(C2)가 같은 파일에 칸을 더해 32종으로 늘린다.

### E. 임시 전투와 대체되는 자리
임시 값은 파일 하나(`Run/placeholder_combat.json`)에 모은다. 대체하는 기능이 이 파일의 해당 칸을 지운다.

| 임시 규칙 (F1) | 값 | 바뀌는 곳 |
|---|---|---|
| 원정대 4명: 캐릭터 `color`의 원 도형 + 머리 위 HP 막대. 자리 = 원정대 중심 둘레 고정 4자리(`order` 순), 움직이지 않음 | (−24,−24)·(24,−24)·(−24,24)·(24,24)(예전 기본 진형, `DSGame.Formation.cs`) | F2(S1): 조이스틱 이동·진형 3종이 자리를 정한다 |
| 기본 무기 하나: 살아 있는 4명이 각자 `interval`마다 사거리 안 가장 가까운 적에게 즉시 피해 `damage × atk` | 13, 0.7초, 사거리 8 × 42(예전 단궁 값) | F3(S2·C2): 장착 아이템의 발동 방식·쿨타임으로 바뀐다 |
| 임시 적 한 종류: 초록 마름모 도형. 가장 가까운 산 원정대원에게 직진, 거리 ≤ 반지름 + 5면 1초마다 접촉 피해 | HP 20, 피해 5, 속도 63, 반지름 18(예전 일반형) | F4(S3·C3): 몬스터 5종 데이터·행동으로 바뀐다 |
| 생성: 웨이브마다 `spawnCount × enemyCountScale`(반올림)마리를 (웨이브 시간 − 4) ÷ (수 − 1) 간격으로, 원정대 중심에서 보이는 바닥 영역 대각선 절반 + `spawnMargin` 거리의 무작위 방향에(예전 규칙. 보이는 영역은 카메라가 알려 준다) | 98마리(1웨이브 M2 값을 모든 웨이브에), 여유 70 | F4(C1): 웨이브 데이터의 적 수·체력·피해 배수·유형 비율로 바뀐다 |
| 버닝 타임 추가 생성: 창 동안 생성 간격의 절반마다 반대쪽 두 방향에서 1마리씩 | 간격 배율 0.5 | F4(C1·S3): 예전처럼 일반·돌진 유형 비율로 |
| 받는 피해 = 피해 × (1 − 방어) | 캐릭터 `def` | F3(S2): 방어 → 보호막·인접 효과 계산 |
| HP 0 = 쓰러짐: 공격하지 않고 표적에서 빠진다 | | F3(S7): 발동 정지·장비 잠금·무료 부활 |

- 바닥 평면 좌표(A5): 예전 단위, x = 화면 오른쪽, y = 화면 위. 그릴 때만 화면 좌표로 바꾼다: 화면 x = x × `unitScale`, 화면 y = y × `unitScale` × 0.5(2:1 아이소의 바닥 압축). `unitScale`은 데이터(출발값: 예전 화면 폭 1180을 가상 폭 640px에 맞춘 0.5424px/단위 ÷ PPU 48). 거리·충돌은 바닥 평면에서만 계산한다.

### F. 2D 렌더·카메라·아이소 바닥 (예전 3D 설정 보호)
지금 설정(읽어서 확인): `GraphicsSettings`와 품질 단계 Mobile·PC가 `Assets/Settings/Mobile_RPAsset.asset`·`PC_RPAsset.asset`을 쓴다. 두 자산의 렌더러 목록은 3D용 `UniversalRendererData` 하나(0번, 기본)뿐이다. 예전 장면의 카메라 6개는 모두 렌더러 −1(= 기본 0번)이다.

- 방법: 2D 렌더러 자산 `Assets/Game/03.Resources/Settings/Game2DRenderer.asset`(`Renderer2DData`)을 새로 만들고, 두 RP 자산의 렌더러 목록 **끝에 1번으로 덧붙이기만** 한다. 기본 렌더러 번호(0)와 그 밖의 칸은 그대로 둔다. `Main.unity`의 카메라만 렌더러 1번을 고른다. 예전 장면은 계속 0번 3D 렌더러를 쓴다.
- 고르지 않은 방법: 새 RP 자산을 만들어 품질 단계나 `GraphicsSettings`에 넣기, 실행 중 파이프라인 바꾸기 — 둘 다 예전 장면과 편집기 전체의 렌더가 바뀐다.
- 2D 렌더러 설정: Transparency Sort Mode = Custom Axis (0, 1, 0)(아트 기준서 3.2, 화면 Y로 앞뒤 정렬). 정렬 레이어는 새로 만들지 않고(`TagManager` 안 바꿈) `Default` 안의 `sortingOrder`로 겹을 나눈다: 바닥 −100, 바닥 장식 −50, 원정대·적 0, 효과 50.
- 카메라: 직교, URP `PixelPerfectCamera` — Assets PPU 48, 기준 해상도 640×360(아트 기준서 K2·3.1 48px 열), Crop Frame 없음(긴 화면은 옆으로 더 보여 준다), Grid Snapping = Pixel Snapping. 카메라 중심 = 원정대 중심(F1은 고정, 따라가기는 F2).
- 아이소 바닥: `Grid`(Cell Layout Isometric, Cell Size (1, 0.5, 1)) + `Tilemap`. 타일 48×24px 마름모(K6), PPU 48 = 1유닛이 타일 폭. F1은 임시 두 가지 색 마름모 타일을 실행 중에 만든 스프라이트로 깐다(그림 파일 없음, 아트 폴더 안 건드림). 들판 크기(칸 수)는 데이터. 풀 타일·흙길 그림은 아트 단계(`/2d-art-produce`)가 같은 Tilemap에 넣는다.
- 새 패키지 없음: Tilemap 실행 모듈(`com.unity.modules.tilemap`)과 URP 17.4의 2D 렌더러·`PixelPerfectCamera`·`Light2D`로 충분하다. Tile Palette 편집기(`com.unity.2d.tilemap`)·Sprite Editor(`com.unity.2d.sprite`)는 그림을 넣는 단계에서 필요하면 더한다.
- 글꼴: 예전 폴더의 `NotoSansKR.ttf`를 `Assets/Game/03.Resources/Font/`에 복사해 쓴다(예전 자산을 참조하지 않는다). UI는 uGUI.

### G. 화면과 기능의 경계

| 화면 | F1이 넣는 것 | 뒤 기능이 넣는 것 |
|---|---|---|
| V1 로비 | 판넬, 캐릭터 카드 4장(이름·직업·HP·공격 배수·방어·시작 아이템 이름, 임시 도형 색), 출발 버튼 | 카드 그림(아트), 캐릭터 데이터 전체·능력치 의미(F3: C4) |
| V2 전투 | 아이소 들판(임시 타일), 원정대 4명 임시 도형 + 머리 위 HP, 임시 적, 상단(웨이브 n/10·처치·위협·남은 시간·버닝 타임 표시·배속·일시정지), 하단 캐릭터별 HP 막대와 아이템 목록 틀(4열 최대 16칸, F1은 시작 아이템 이름 1칸) | 이동·진형·조이스틱(F2: S1), 아이템 쿨타임·상태 표시와 발동(F3: S2·S7), 몬스터 5종·웨이브 수치(F4: S3·C1·C3), 피해 숫자·피격 연출·몬스터 HP(F6: S9) |
| V3 정비 | 판넬, "웨이브 n 완료", 캐릭터 4명 자리(이름·HP), 다음 웨이브 버튼 | 4×4 가방·보관함·아이템 설명·수치 미리보기·전리품(F5: S5·S6), 부활(F3: S7), 진형 3종(F2: S1) |
| V4 결과 | 승리·패배, 버틴 웨이브, 처치 수, 전투 시간, 로비로 버튼 | 패배 원인(S10, 첫 판 뒤 — 이번 범위 아님) |

### H. 판정 경로와 진입점
- 경로 파일 `tools/playable/route.json`(macOS에서 기존 `Tools/` 폴더와 같은 곳. Git 경로는 `Tools/playable/route.json`). `scene` = `Assets/Game/01.Scene/Main.unity`, `size` = [1920, 1080], `sizes` = 가로 화면비 [1920, 1080](16:9)·[2340, 1080](19.5:9)·[2520, 1080](21:9)·[2048, 1536](4:3), `time_scale` = 1. G2의 "남은 시간이 줄어든다"를 놓치지 않게 G1·G2는 1배로 돌리고, 오래 걸리는 단계(G3·G7)의 `do`가 `UnityEngine.Time.timeScale = 20f;`로 올린다(게임의 배속·일시정지는 `Time.timeScale`을 쓰지 않으므로 겹치지 않는다).
- 단계 G1~G8을 게임 기획서 8절 번호 그대로 모두 적는다(빼거나 합치지 않는다). 화면이 처음 나오는 단계(G1 V1, G2 V2, G3 V3, G5 V4)는 `"capture": "all"`.

| 단계 | do | wait / expect | 진입점 멤버를 채우는 기능 |
|---|---|---|---|
| G1 | — | `Screen == "Lobby" && CardCount == 4 && CanDepart` | F1 |
| G2 | `UseRunSettings(1, 1f); PressDepart();` | `Screen == "Battle" && Wave == 1 && AliveCount == 4 && RemainingSeconds < WaveSeconds - 1` | F1 |
| G3 | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.Move);` | `Screen == "Prep" && CompletedWave == 1` / `StashCount >= 3 && StashCount <= 5 && AliveCount == 4`(M4) | 봇 이동 F2, `StashCount` F5 (`Screen`·`CompletedWave`·`AliveCount`·`StartBot` 틀은 F1) |
| G4 | `PlaceFirstStashItem(); PressNextWave();` | `Screen == "Battle" && Wave == 2` / `IsPlacedItemActive` | `PlaceFirstStashItem`·`IsPlacedItemActive` F5(발동 목록은 F3) |
| G5 | `StartBot(BotMode.AutoNext);` | `Screen == "Result"` / `Outcome == "Lose" && WavesSurvived < 10 && AliveCount == 0` | F1(이동을 끄고 정비만 자동으로 넘김) |
| G6 | `StopBot(); UseRunSettings(7, 1f); PressToLobby(); PressDepart();` | `Screen == "Battle" && Wave == 1 && Kills == 0 && AliveCount == 4 && HasOnlyStartItems` | F1 |
| G7 | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.Move \| BotMode.Equip \| BotMode.AutoNext);` | `Screen == "Result"` / `Outcome == "Win" && WavesSurvived == 10 && AliveCount >= 1 && Math.Abs(TotalBattleSeconds - 435) <= 2`(M1) | 자동 장착 F5, 이동 F2 |
| G8 | `StopBot(); UseRunSettings(8, 1f); PressToLobby(); PressDepart();` | `Screen == "Battle" && Wave == 1 && Kills == 0 && RunCount == 3` | F1 |

(표의 식은 `PlayableProbe.` 접두어를 줄였다. 경로 파일에는 모두 `PlayableProbe.`를 붙인다.) M2·M3·M5·M7을 재는 진입점 멤버와 `expect`는 그 값을 만드는 F4가 더한다.

- 진입점 `Assets/Game/02.Scripts/App/PlayableProbe.cs`: 정적 클래스, 파일 전체 `#if UNITY_EDITOR`. 장면의 `GameSceneLifetimeScope.Container`에서 Model·Presenter를 꺼내 읽고 부르기만 한다. 게임 코드는 진입점을 모른다.
  - F1이 만드는 읽기: `Screen`(Lobby·Battle·Prep·Result), `CardCount`, `CanDepart`, `Wave`, `CompletedWave`, `WaveSeconds`, `RemainingSeconds`, `IsBurningTime`, `AliveCount`, `Kills`, `EnemyCount`, `Outcome`(None·Win·Lose), `WavesSurvived`, `TotalBattleSeconds`, `RunCount`(Play 뒤 시작한 판 수), `HasOnlyStartItems`.
  - F1이 만드는 조작: `PressDepart()`·`PressNextWave()`·`PressToLobby()`(각 Presenter의 버튼 처리 메서드 = 버튼 `onClick`과 같은 길), `UseRunSettings(int seed, float enemyCountScale)`(`RunSettingsModel`에 다음 판 설정을 넣음), `StartBot(BotMode)`·`StopBot()`.
  - 봇(`PlayableBot`, 같은 파일의 `#if UNITY_EDITOR` MonoBehaviour): `BotMode`는 깃발 묶음 `Move`·`Equip`·`AutoNext`. F1은 `AutoNext`(정비 화면이면 `PrepPresenter`의 다음 웨이브 처리를 부른다)만 만든다. `Move`(조이스틱 입력과 같은 이동 명령)는 F2, `Equip`(보관함 전리품 자동 장착)은 F5가 채운다. 아직 없는 깃발이 들어오면 F1 봇은 경고 로그를 남기고 그 깃발을 무시한다. `StartBot`은 지금 모드를 바꾸고, `StopBot`은 봇을 없앤다.
- 경로가 아직 못 밟는 F1 흐름은 별도 경로 `tools/playable/f1-flows.json`(같은 진입점, `--route`로 지정)으로 확인한다. `time_scale` 1. 단계: `L1` 로비 도착 → `B1` `UseRunSettings(1, 0f); PressDepart();` 뒤 전투 웨이브 1 → `P1` 시간 끝까지 기다려 `Screen == "Prep" && CompletedWave == 1`(V3 `capture: "all"`) → `B2` `PressNextWave()` 뒤 `Screen == "Battle" && Wave == 2 && AliveCount == 4` → `WIN` `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.AutoNext);` 뒤 결과 승리·`WavesSurvived == 10`·`AliveCount == 4`·`Math.Abs(TotalBattleSeconds - 435) <= 2`(V4 `capture: "all"`) → `LOSE` `UseRunSettings(2, <패배 배율>); PressToLobby(); PressDepart();` 뒤(봇은 계속 정비를 넘김) 결과 패배·`AliveCount == 0`·`WavesSurvived < 10` → `LOBBY2` `StopBot(); UnityEngine.Time.timeScale = 1f; PressToLobby();` 뒤 로비 → `RUN3` `PressDepart()` 뒤 `Screen == "Battle" && Wave == 1 && Kills == 0 && AliveCount == 4 && RunCount == 3`. `<패배 배율>`은 구현이 기본 무기로 버티지 못하는 적 수 배율(출발값 4)을 정해 파일에 적는다.

### I. 화면 배치 (가로 게임)
- Player Settings 방향: 지금 값(기본 = 자동 회전, 허용 = 가로 왼쪽·오른쪽만, 세로 금지)을 유지한다. 가로 두 방향 사이 뒤집기만 되고 세로로는 돌지 않는다.
- Canvas: Screen Space Overlay, CanvasScaler Scale With Screen Size, 기준 640×360, Match = 1(높이). 이유: 가상 해상도와 같은 기준이라 UI 도트 크기가 월드와 같고, 가로 게임이라 높이를 맞추고 긴 화면은 옆으로 넓힌다. 높이가 360의 정수배가 아닌 화면(예: 1170px)에서는 UI 배율이 정수가 아니다.
- 안전 영역: Canvas 루트 아래 `UI_SafeAreaPanel` 하나가 `Screen.safeArea`에 앵커를 맞추고(크기·안전 영역이 바뀌면 다시), 상단 표시줄·하단 판넬·모서리 버튼은 이 판넬에 앵커한다. 배경·딤은 화면 끝까지.
- 안전 영역 계산은 화면 크기·`safeArea`를 받는 순수 함수(`SafeAreaLayout.Calculate`)로 두고 EditMode로 시험한다.

## 데이터 스키마
위치는 `Assets/Game/03.Resources/Resources/Data/`(Resources 로드, `.ai/rules/data-driven-content.md` 출발값). 콘텐츠 단위마다 JSON 파일 하나. 검사 명령 `python3 -B ./tools/check_data.py`(형식, id 중복, 가리키는 대상, 개수 범위. 실패하면 exit 1).

| 위치 | 이름 | 필드 | 타입 | 키/기본값 | 쓰는 쪽 → 읽는 쪽 |
|---|---|---|---|---|---|
| JSON `Character/<id>.json` | 캐릭터(4개) | `id`, `order`, `name`, `role`, `hp`, `atk`, `def`, `startItem`, `color` | string, int, string, string, int, float, float(0~0.8), string(→ `Item` id), string(#rrggbb) | 키 `id` = 파일명. bruno(1, 브루노, 전사, 400, 0.8, 0.15, sword, #d79b68), lena(2, 레나, 궁수, 110, 1.2, 0, shortbow, #67966e), mira(3, 미라, 마법사, 140, 1.0, 0.05, firestaff, #8475ad), bolt(4, 볼트, 사제, 120, 1.1, 0.05, relic, #c2ac6f). 검사: 개수 4, `order` 1~4 중복 없음 | 기획(데이터) → `CharacterRepository` → `PartyService`·`LobbyPresenter`. F3이 같은 파일에 칸 추가 |
| JSON `Item/<id>.json` | 아이템(F1은 시작 아이템 4개) | `id`, `name` | string, string | 키 `id`. sword 장검, shortbow 단궁, firestaff 화염 지팡이, relic 치유 성물. 검사: 개수 ≥ 4 | 데이터 → `ItemRepository` → 카드·하단 목록. F3(C2)가 같은 파일에 칸 추가·32종으로 |
| JSON `Wave/wave<NN>.json` | 웨이브(10개) | `id`, `order`, `duration`, `hasBurningTime` | string, int, float(초 > 0), bool | 키 `id`(wave01~wave10). duration 30·30·35·40·40·45·45·50·55·65, hasBurningTime 6~10만 true. 검사: `order` 1..N 빈칸 없음, 개수 범위 1~20, duration 합 출력 | 데이터 → `WaveRepository` → `RunService`. F4(C1)가 같은 파일에 적 수·배수·유형 칸 추가 |
| JSON `Run/run_rules.json` | 한 판 규칙(1개) | `simStep`, `burningStartRatio`, `burningSeconds`, `speedOptions`, `defaultSeed`, `defaultEnemyCountScale`, `unitScale`, `fieldCells` | float, float, float, float[], int, float, float, int | 1/30, 0.5, 10, [1, 2], 0(= 시각에서 뽑음), 1, 0.0113(= 0.5424 ÷ 48), 64(가로·세로 칸 수) | 데이터 → `RunService`·`RunSettingsModel`·`FieldView` |
| JSON `Run/placeholder_combat.json` | 임시 전투(1개, F3·F4가 칸을 지우고 마지막에 파일 삭제) | `memberSpots`, `weapon{damage, interval, range}`, `enemy{hp, damage, speed, radius, attackInterval, contactGap, color}`, `spawnCount`, `spawnMargin`, `burningIntervalScale` | float[4][2], object, object, int, float, float | 위 E절 4자리, {13, 0.7, 336}, {20, 5, 63, 18, 1, 5, #819363}, 98, 70, 0.5 | 데이터 → `BasicWeaponService`·`MonsterService` |
| 세이브 | 없음 | | | 저장 없음(A9) | |
| 서버 | 없음 | | | | |

- 실행 중 상태(저장 안 함): `RunModel`(Phase, Wave, Elapsed, Remaining, IsBurningTime, Kills, TotalBattleSeconds, Outcome, WavesSurvived, IsPaused, SpeedIndex, RunCount), `PartyModel`(멤버별 캐릭터 id, HP, 최대 HP, 바닥 좌표, 쿨 시계, 장착 아이템 id 목록), `MonsterModel`(살아 있는 적 목록: 번호, 바닥 좌표, HP, 접촉 쿨), `RunSettingsModel`(다음 판 시드·적 수 배율).

## 클라 변경
경로는 `Assets/Game/` 기준. 어셈블리: `02.Scripts/Game.asmdef`(VContainer, R3.Unity, UniTask, Unity.RenderPipelines.Universal.Runtime, Unity.InputSystem 참조), `02.Scripts/Editor/Game.Editor.asmdef`(에디터 전용), `Tests/EditMode/Game.Tests.EditMode.asmdef`.

Core 기반 (CLAUDE.md "Core Infrastructure")
- `02.Scripts/Core/Const/Const.cs` — `IsLogEnabled`(에디터·개발 빌드 참)
- `02.Scripts/Core/Utils/GameLogger.cs` — `Log`·`LogWarning`·`LogError`, `Const.IsLogEnabled`를 따름
- `02.Scripts/Core/Event/EventBus.cs`, `02.Scripts/Core/Event/GameEvent.cs` — 구독·발행. F1의 이벤트 구조체는 이 파일에만 둔다(F1은 화면 흐름을 콜백·R3로 잇고, 이벤트는 실제로 층을 건너는 것만 정의한다 — 처음에는 `RunEndedEvent { Outcome }` 하나)
- `02.Scripts/Core/Pool/PoolManager.cs` — 키별 `ObjectPool` 감싸기, `Get`·`Release`, Get 때 상태 초기화
- `02.Scripts/Core/Resource/ResourceService.cs` — `Load<T>(path)`(Resources), JSON 데이터 읽기 `LoadJson<T>`
- `02.Scripts/App/GameSceneLifetimeScope.cs` — 이 장면의 모든 등록

판 흐름 (S4, V2~V4)
- `02.Scripts/Features/Run/Data/WaveData.cs`, `RunRulesData.cs`, `PlaceholderCombatData.cs` — JSON 형
- `02.Scripts/Features/Run/Registry/WaveRepository.cs` — 웨이브 파일 읽기, `order` 정렬
- `02.Scripts/Features/Run/Model/RunModel.cs`, `RunSettingsModel.cs`, `RunPhase.cs`, `RunOutcome.cs`
- `02.Scripts/Features/Run/Service/RunService.cs` — `StartRun`·`Step`·`NextWave`·`ReturnToLobby`, C절 순서
- `02.Scripts/Features/Run/Presenter/BattlePresenter.cs`(`ITickable`: 프레임 시간 × 배속을 모아 `Step`), `BattleHudPresenter.cs`, `PrepPresenter.cs`, `ResultPresenter.cs`
- `02.Scripts/Features/Run/View/UI_BattleHudView.cs`, `UI_MemberStatusView.cs`(하단 캐릭터별 HP·아이템 목록 틀), `UI_PrepView.cs`, `UI_ResultView.cs`

편성 (S8, V1)
- `02.Scripts/Features/Party/Data/CharacterData.cs`, `02.Scripts/Features/Party/Registry/CharacterRepository.cs`
- `02.Scripts/Features/Item/Data/ItemData.cs`, `02.Scripts/Features/Item/Registry/ItemRepository.cs`
- `02.Scripts/Features/Party/Model/PartyModel.cs`, `02.Scripts/Features/Party/Service/PartyService.cs`(편성 만들기, 피해 적용, 생존 수)
- `02.Scripts/Features/Party/Presenter/LobbyPresenter.cs`, `View/UI_LobbyView.cs`, `View/UI_CharacterCardView.cs`
- `02.Scripts/Features/Party/View/MemberView.cs` — 월드 임시 도형 + 머리 위 HP 막대

임시 전투 (E절)
- `02.Scripts/Features/Combat/Service/BasicWeaponService.cs` — 임시 기본 무기(F3이 대체)
- `02.Scripts/Features/Monster/Model/MonsterModel.cs`, `Service/MonsterService.cs`(생성·이동·접촉, F4가 대체), `View/MonsterView.cs`(풀에서 꺼냄), `Factory/MonsterViewFactory.cs`(`PoolManager.Get` + 초기화), `Registry/MonsterViewRegistry.cs`

월드·카메라·바닥 (F절)
- `02.Scripts/Features/Field/Utils/IsoProjection.cs` — 바닥 평면 ↔ 월드 좌표(순수 함수)
- `02.Scripts/Features/Field/View/FieldView.cs` — Tilemap에 임시 마름모 타일 깔기
- `02.Scripts/Features/Field/Presenter/WorldPresenter.cs` — 모델 좌표 → View 위치, 카메라 중심
- `02.Scripts/Effect/Placeholder/PlaceholderSpriteFactory.cs` — 임시 원·마름모 스프라이트를 실행 중에 만듦(Point 필터, PPU 48)
- `03.Resources/Settings/Game2DRenderer.asset` — 2D 렌더러(새 자산)
- `Assets/Settings/PC_RPAsset.asset`, `Assets/Settings/Mobile_RPAsset.asset` — 렌더러 목록 끝에 `Game2DRenderer` 한 줄 추가만(F절)
- `03.Resources/Font/NotoSansKR.ttf` — 예전 폴더에서 복사

화면 (I절)
- `02.Scripts/UI/ScreenPresenter.cs` — `RunModel.Phase`로 판넬 4개 켜고 끄기
- `02.Scripts/UI/UI_SafeAreaPanel.cs`, `02.Scripts/UI/SafeAreaLayout.cs`(순수 함수)
- `01.Scene/Main.unity` — LifetimeScope, 카메라(렌더러 1번, PixelPerfectCamera), Grid·Tilemap, Canvas(판넬 4개), EventSystem(`InputSystemUIInputModule`)

판정 (H절)
- `02.Scripts/App/PlayableProbe.cs` — 진입점 + `PlayableBot`(`#if UNITY_EDITOR`)
- `Tools/playable/route.json` — G1~G8
- `Tools/playable/f1-flows.json` — F1 흐름 확인 경로
- `Tools/check_data.py` — 데이터 검사 명령(에디터 없이)

테스트
- `Tests/EditMode/RunServiceTests.cs`, `PartyServiceTests.cs`, `PlaceholderCombatTests.cs`, `CoreTests.cs`(EventBus·PoolManager·GameLogger), `IsoProjectionTests.cs`, `SafeAreaLayoutTests.cs`, `DataLoadTests.cs`

## 서버 변경과 API 계약
없음. 게임 기획서: 서버 없음, 저장 없음(A9). 재화·랭킹·치팅 방지가 걸리는 것이 없다.

## 수용 기준
확인 방법: 자동 테스트(EditMode·명령), 에디터(컴파일·에디터 명령으로 상태 확인), 플레이(플레이 모드 동작·화면), 사람.
EditMode 테스트 명령: `unity test --platform editmode --assembly Game.Tests.EditMode`(실행기 명령은 구현 단계가 이 레포의 unity-cli 형식으로 적는다).

| 번호 | 기준 (확인할 수 있는 문장) | 확인 방법 | 확인 절차 |
|---|---|---|---|
| AC1 | 새 코드·자산을 넣은 뒤 컴파일 에러가 0개다 | 에디터 | `python3 -B ./tools/editor_ready.py` 후 콘솔·Editor.log에 `error CS` 없음 |
| AC2 | 데이터 검사 명령이 지금 데이터에서 OK(exit 0)이고, id 중복·없는 `startItem`·웨이브 `order` 빈칸·캐릭터 수 4 아님을 넣은 시험 데이터에서는 exit 1이다 | 자동 테스트 | `python3 -B ./tools/check_data.py`, `python3 -B ./tools/check_data.py --self-test`(틀린 데이터 4종을 임시 폴더에 만들어 각각 실패하는지) |
| AC3 | 판정 경로 파일에 게임 기획서 8절의 G1~G8이 번호 그대로 모두 있고 형식 검사가 통과한다 | 자동 테스트 | `python3 -B ./tools/playable.py --check`, 경로 파일 단계 id 목록 = G1…G8 |
| AC4 | 판정 경로 G1까지 통과 | 플레이 | `python3 -B ./tools/playable.py --until G2`(보고서 `Logs/playable/latest/report.json`의 G1 결과) |
| AC5 | 판정 경로 G2까지 통과 | 플레이 | `python3 -B ./tools/playable.py --until G2` |
| AC6 | 적이 없는 판(적 수 배율 0)에서 웨이브 시간이 끝나면 정비 화면(웨이브 1 완료)이 나오고, 다음 웨이브를 누르면 웨이브 2가 생존 4명으로 시작한다 | 플레이 | `python3 -B ./tools/playable.py --route tools/playable/f1-flows.json`의 `P1`·`B2` 단계 통과 |
| AC7 | 10웨이브를 끝까지 버티면 결과 화면이 승리, 버틴 웨이브 10을 보이고 전투 시간 합이 435초 ±2초다(M1) | 플레이 | 같은 경로의 `WIN` 단계 통과(시간 배율 20, V4 캡처 4장) |
| AC8 | 적 수 배율을 올린 판에서 가만히 두면 전멸해 결과 화면이 패배를 보이고, 로비로 → 출발을 누르면 웨이브 1·처치 0·생존 4명인 새 판이 시작한다(Play 뒤 세 번째 판) | 플레이 | 같은 경로의 `LOSE`·`LOBBY2`·`RUN3` 단계 통과. 승패를 써 넣는 호출 없음(진입점 코드에서 `Outcome`·`Phase` 대입이 없는지 확인) |
| AC9 | 한 판 규칙: 웨이브 시간이 30·30·35·40·40·45·45·50·55·65초로 흐르고, 같은 걸음에 시간 끝과 전멸이 겹치면 패배, 전투 중 전멸은 남은 시간과 상관없이 즉시 패배, 마지막 웨이브 끝에 생존 ≥ 1이면 승리, 버틴 웨이브는 승리 10·패배 시 진 웨이브 − 1 | 자동 테스트 | `RunServiceTests`(웨이브 시간표, 동시 패배, 즉시 패배, 승리, 버틴 웨이브) |
| AC10 | 버닝 타임은 6~10웨이브에서 웨이브 시간의 절반부터 10초 동안만 켜지고 1~5웨이브에서는 켜지지 않으며, 그동안 임시 생성기가 추가 적을 낸다 | 자동 테스트 | `RunServiceTests.BurningTimeWindow`, `PlaceholderCombatTests.BurningTimeSpawnsExtra` |
| AC11 | 일시정지 중에는 남은 시간이 줄지 않고, 배속 ×2에서는 같은 프레임 시간에 남은 시간이 두 배로 준다. 프레임을 1/60초 60번으로 주든 1초 1번으로 주든 같은 시드면 처치 수·HP가 같다 | 자동 테스트 | `RunServiceTests.PauseStopsTime`, `RunServiceTests.SpeedDoubles`, `PlaceholderCombatTests.SameSeedSameResultAcrossFrameSplits` |
| AC12 | 편성: 한 판 시작 때 원정대는 데이터 순서 브루노·레나·미라·볼트 4명, HP가 데이터의 최대 HP, 장착 목록은 각자 시작 아이템 하나뿐이다. 받는 피해는 피해 × (1 − 방어)다 | 자동 테스트 | `PartyServiceTests` |
| AC13 | 임시 전투: 기본 설정(시드 고정)으로 10초를 돌리면 처치가 1 이상이고, 접촉한 적이 원정대 HP를 줄인다. HP 0인 대원은 공격하지 않고 적의 표적에서 빠진다 | 자동 테스트 | `PlaceholderCombatTests` |
| AC14 | 로비 카드 4장에 이름·직업·HP·공격 배수·방어·시작 아이템 이름이 보이고, 전투 화면에 아이소 바닥·원정대 4명 + 머리 위 HP·임시 적·상단(웨이브·처치·위협·남은 시간·배속·일시정지)·하단(캐릭터별 HP·아이템 목록 틀)이, 정비 화면에 웨이브 n 완료·4명 자리·다음 웨이브 버튼이, 결과 화면에 승패·버틴 웨이브·처치·전투 시간·로비로 버튼이 보인다 | 플레이 | G1·G2 캡처와 `f1-flows.json`의 `P1`·`WIN` 캡처를 열어 항목별로 확인하고 본 캡처 목록을 보고에 적음 |
| AC15 | 2D 화면이 기준대로 그려진다: Pixel Perfect Camera 기준 640×360·PPU 48, 1920×1080 캡처에서 바닥 마름모 한 칸이 144×72px(3배), 원정대·적이 바닥 위에 화면 Y 순서로 겹친다 | 플레이 | G2의 1920×1080 캡처에서 타일 크기를 잰다. 에디터 명령으로 카메라의 `PixelPerfectCamera` 값을 읽는다 |
| AC16 | 예전 3D 렌더 설정이 그대로다: 두 RP 자산의 diff는 렌더러 목록 한 줄 추가뿐이고 기본 렌더러 번호 0이 3D 렌더러를 가리키며, `Main.unity` 카메라만 렌더러 1번(2D)이다. `Assets/DeadSurvivor/`·`Assets/Scenes/`·`ProjectSettings/EditorBuildSettings.asset`·`ProjectSettings/GraphicsSettings.asset`·`ProjectSettings/QualitySettings.asset`·`ProjectSettings/TagManager.asset`에 변경이 없다 | 에디터 | `git diff --stat`과 두 RP 자산의 `git diff`, 에디터 명령으로 두 RP 자산의 `rendererDataList`·기본 번호와 Main 카메라의 렌더러 번호를 읽음 |
| AC17 | Core 기반: EventBus 구독·발행·해제가 되고, PoolManager가 Release한 것을 다음 Get에 다시 내주며 Get 때 상태를 초기화하고, `Const.IsLogEnabled`가 거짓이면 GameLogger가 아무것도 찍지 않는다 | 자동 테스트 | `CoreTests` |
| AC18 | 바닥 평면 ↔ 월드 좌표 변환이 왕복해도 같은 값이고(오차 1e-4), 바닥 (100, 0)과 (0, 100)의 화면 길이 비가 2:1이다. 안전 영역 계산이 노치 있는 가로 화면(왼쪽 여백)과 없는 화면에서 판넬 앵커를 맞게 낸다 | 자동 테스트 | `IsoProjectionTests`, `SafeAreaLayoutTests` |
| AC19 | 노치 기기에서 가장자리 UI가 가려지지 않고, 16:9·19.5:9·21:9·4:3 가로 화면에서 판과 UI가 잘리거나 겹치지 않는다 | 플레이 | 경로의 G1·G2와 `f1-flows.json`의 `P1`·`WIN` `capture: "all"` 4장씩을 열어 봄 + Device Simulator 노치 기기 1대(가로) 캡처 |

### 기존 테스트
기대가 바뀌는 기존 테스트 없음 — 레포에 NUnit 테스트(`[Test]`·`[UnityTest]`)가 하나도 없다(`Assets/` 전체 검색, 2026-11-15). 예전 판 검사(`Tools/Verify/`)는 2D 작업에서 쓰지 않고 이 기능이 고치지 않는다.

기대 변경 허용(이 목록에 있는 것만 구현 단계가 기대값을 고칠 수 있다):

| 테스트 | 지금 기대(끝 상태·값) | 새 기대 | 이유(기획서 절) |
|---|---|---|---|
| (없음) | | | |

## 열린 질문
없음. 작은 갈림길은 아래 가정으로 정했다(승인 때 함께 확인).

### 가정
| 번호 | 가정 | 틀리면 바뀌는 것 |
|---|---|---|
| FA1 | 화면 네 개는 장면 하나의 판넬이다(장면 전환 없음) | A절, `Main.unity` |
| FA2 | 2D 렌더러는 기존 RP 자산 두 개의 렌더러 목록 끝에 덧붙이고 새 카메라만 고른다. 예전 장면 카메라는 모두 기본(−1)이라 영향이 없다 | F절, AC16 |
| FA3 | 기본 무기 값은 예전 단궁(13, 0.7초, 사거리 8)을 즉시 명중으로, 임시 적은 예전 일반형, 적 수는 모든 웨이브 98(1웨이브 M2 값), 대원 자리는 예전 기본 진형 4자리 | E절 — F3·F4가 대체 |
| FA4 | 바닥 평면 → 화면은 x × `unitScale`, y × `unitScale` × 0.5(2:1). `unitScale` 출발값은 예전 화면 폭 1180 = 가상 폭 640px | E절, F2·F3의 거리 감각 |
| FA5 | 전투는 1/30초 고정 간격으로 쪼개 돈다 | C절, AC11 |
| FA6 | F1의 승리·패배 확인은 판 설정의 적 수 배율(0 / 패배 배율)로 같은 규칙을 태운다. 판 설정은 로비 출발에도 쓰는 게임의 시작 입력이다. 판정 경로는 1배로 시작하고 긴 단계에서 `Time.timeScale`을 20으로 올린다 | H절, AC6~AC8 |
| FA7 | 배속은 ×1·×2 두 가지 | C절, HUD |
| FA8 | Player Settings 방향은 지금 값(가로 두 방향만)을 유지한다 | I절 |
| FA9 | UI는 uGUI + 예전 폴더에서 복사한 NotoSansKR | F절 |
