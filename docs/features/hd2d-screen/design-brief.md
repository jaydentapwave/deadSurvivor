# HD-2D 화면 구조 — 설계 브리핑 (전환 3단계, 확정판)

- **상태: 확정판(11-18 확정) — design-reviewer r001 FAIL → 필수 수정 DR-01~DR-06 반영(11-18, 12절). 수정 확인 대기.** 초안의 "정사영이면 / 원근이면" 두 갈래는 정사영 하나로 줄였다. 초안에서 바뀐 곳은 **(11-18 확정)**으로 표시했다.
- 승인: 위임 — 2026-11-18 사용자 "이번에는 좀 다르게 온전히 완성된 결과물을 내가 받아서 보고싶어 … 중간중간에 나한테 보고하는 방식이아니라". 사용자가 자는 동안 묻지 않고 정했다. 판단 근거는 각 절과 9절(결정 기록)에 있다.
- 작성: 초안 2026-11-18(Claude 하위 에이전트), 확정 2026-11-18(Claude 하위 에이전트, 디렉터 지시). 기준 코드 HEAD `e45394d`. `e98041a` 뒤 커밋은 HD2D 시안 파일만 바꿨다(`git diff --stat e98041a HEAD -- Assets/Game/02.Scripts` 확인). 그래서 게임 코드 줄 번호는 두 커밋에서 같다.
- 규모: **L2**. 렌더 파이프라인 설정을 바꾸고(카메라 렌더러 1→0, SRP Batcher, Render Scale, 그림자, 품질 단계), 좌표 변환 원본을 바꾸고, 상태 소유권도 바꾼다(카메라 위치·흔들림을 카메라 장치가 가짐). 게임 동작에 닿는 계산도 바뀐다(보이는 바닥 범위, 몬스터 경계, 공격 방향 8구간)(`.ai/rules/work-levels.md`).
- 바탕 문서: [완성 실행 계획](../../game/hd2d-완성-실행계획.md)(C1~C9, 3절 "바뀐 범위만 확인"), [전환 계획](../../game/hd2d-전환-계획.md) 3단계 표, [사전 조사](../../game/hd2d-사전조사.md) D1~D16·4절, [배경 준비](../hd2d-background/prep.md) 1·3절, [작업 개선 기록](../../game/hd2d-작업-개선-기록.md) I4, 시안 2회차 기록 `output/hd2d-시안/execution-note3.md`, [UI 관찰](../../art/reference/hd2d-ui-octopath-20261118/UI-관찰.md).
- 수치 원본: 이 문서에서 처음 정하는 값(정수 배율 규칙, 경계, 방향 경계 넘김)은 데이터 파일 `view_rules.json`에 둔다. 색·빛·흐림 값은 아트 기준서 r6에 둔다. 여기에 적힌 r6 값은 시안 2회차에서 옮긴 출발값이다.

## 0. 확정된 결정 (입력)

| # | 결정 | 출처 |
|---|---|---|
| E1 | 카메라: **정사영, 30° 내려다봄**, 요(yaw) 0. 바닥 격자는 화면에 대해 45° 돌아 있다. 지금의 아이소 구도와 같다 | 사용자 시안 F, r6 초안 K2 |
| E2 | 인물 화면 키는 **정수 배율**이다. 1080줄에서 2배, 인물 96px(8.9%) | 디렉터 위임 결정 F-int(실행 계획 6절 1행) |
| E3 | 배경 방식 F2c: 나무·덤불·풀포기·꽃은 **세운 도트 판**이다. 바닥·턱·바위·돌담·물·울타리·다리·천막은 **도트 질감 3D**에 1/48m 갈색 뒤집은 껍데기 외곽선을 두른다. 지형 배치는 시안 F를 따른다 | 위임 결정, execution-note3 |
| E4 | 대원 4명과 몬스터 5종은 **8방향**이다. 그림은 5방향(아래·오른쪽 아래·오른쪽·오른쪽 위·위)이고, 나머지는 좌우 반전으로 만든다. 코드의 방향 고르기를 4방향에서 8방향으로 바꾼다 | 사용자 11-18, r6 초안 K14 |
| E5 | 게임 규칙과 계산은 2D 바닥 좌표(`Vector2`)에 그대로 둔다. 그리는 쪽만 3D로 옮긴다 | 전환 계획 1절 |
| E6 | UI는 U단계에서 다시 꾸민다. 이 설계는 UI와 3D 화면의 경계만 정한다 | 실행 계획 2절 |

## 1. 목표와 완료 조건

| 항목 | 내용 |
|---|---|
| 이번에 쓸 수 있게 될 것 | 본 게임(`Main.unity`) 전투 화면이 3D 렌더러에서 돈다. 카메라는 정사영 30°에 정수 배율이다. 도트 대원·몬스터·효과·장식은 세운 판이다. 바닥은 지금 타일맵을 바닥에 눕혀 임시로 쓴다(5단계에서 바꿈). 해 1개, 대원 실제 그림자, 화면 효과 볼륨(위아래 흐림·Bloom)이 들어간다. 대원과 몬스터가 **8방향**으로 그림을 고른다. 새 방향 그림이 오기 전에는 가장 가까운 기존 그림을 대신 쓴다. 몬스터는 들판 경계 안에서만 생기고 다닌다(I4) |
| 완료 조건(실행 계획 4단계 행) | 컴파일 오류 0, EditMode 전부 통과, 판정 경로 8/8(`tools/playable.py`, G1~G8), 웨이브 목표값 42행 OK(`tools/wave_targets.py --metrics`), 짧은 Play 캡처 4화면비(1920×1080 16:9, 2340×1080 19.5:9, 2520×1080 21:9, 2048×1536 4:3)와 노치 안전 영역 확인, 카메라 이동 연속 캡처 8장, 디렉터가 캡처를 직접 봄 |
| 시험 후보 / 정식 채택 | **정식 채택**: 좌표 변환, 정수 배율·픽셀 맞춤, 카메라 장치, 보이는 범위, 8방향 고르기와 대신 쓰기, 몬스터 경계, 재질 구성, 품질 단계 처리. **시험 후보**(5·6단계에서 바꿈): 바닥(눕힌 타일맵), 볼륨 값, 그림자 색·길이 |
| 제외(후속) | 배경 만들기(5단계), 빛·효과 값 다듬기(6단계), 성능 줄이기(7단계), 새 방향 그림 생산(5d단계), UI 다시 꾸미기(U단계), 가려짐 디더(D12, 배경 소품이 생기는 5단계), 밤 조명, 킷 팩 옮기기(9단계) |

## 2. 바뀌지 않는 것

| 무엇 | 근거(파일:줄) | 보장 방법 |
|---|---|---|
| 게임 계산은 바닥 좌표 `Vector2`로만 한다(바닥 단위, x = 화면 오른쪽, y = 화면 위, 1 바닥 단위 = 0.0113m) | `PartyModel.Center`, `Monster.Position`, `PartyMoveService.cs:3`, `MonsterSpawnService.cs:147`, `run_rules.json` `unitScale` | 규칙 서비스의 시그니처를 바꾸지 않는다. **예외(11-18 확정)**: 몬스터 경계(3.6)와 공격 방향 8구간(3.9)은 규칙 쪽 계산이 바뀐다. 둘 다 2D 바닥 좌표 안에서 바뀐다 |
| 규칙 서비스가 받는 보이는 범위는 `Vector2` 반 크기 하나다 | `MonsterService.cs:45`, `PartyMoveService.cs:28`, `MonsterModel.cs:19` | 정사영이라 형태가 그대로다. 값만 카메라에 따라 커진다(3.5) |
| 들판은 마름모 `|x|+|y| ≤ 반대각선`(64칸, 반대각선 32m)이다 | `PartyMoveService.cs:10-11,23`, `PartyMoveMath.cs:22-38`, `run_rules.json` `fieldCells` | 그대로 둔다 |
| 판 흐름, DI 수명(Scoped), 데이터 파일 형식(새 파일·새 칸 추가만) | `GameSceneLifetimeScope.cs` | 등록 줄만 더한다 |
| 도트 임포트는 PPU 48, 발 가운데 피벗, Point, mip 없음이다 | 사전 조사 3.2 | 3D에서도 그대로 쓴다 |
| 화면 UI 캔버스(Screen Space Overlay, 640×360 기준 높이 맞춤, `UI_SafeAreaPanel`) | `MainSceneBuilder.cs:181-191` | 이 단계에서는 손대지 않는다. 경계는 3.10절 **(11-18 확정)** |
| 판정 경로 G1~G8의 기대식(`Tools/playable/route.json`) | 카메라·방향·타일을 직접 보는 식이 없다(쓰는 진입점 목록을 grep으로 확인) | 고치지 않고 통과해야 한다. 바뀌는 것은 기능 판정 파일 2개뿐이다(5절 W4·W5) |

## 3. 구조

### 3.1 폴더·어셈블리와 새/바뀌는 클래스 (11-18 확정)

다시 쓸 부분(게임 이름이 없는 코드)은 새 어셈블리 **`HD2D.View`**에 둔다. 9단계에서 킷 패키지로 폴더째 옮긴다(초안 6절 후보 A를 고침).

- 자리: `Assets/Game/02.Scripts/Effect/HD2DView/HD2D.View.asmdef`. 참조는 URP Runtime·Core Runtime뿐이다. `autoReferenced`는 false다. `Game`이나 시안 어셈블리 `HD2D.Runtime`을 참조하지 않는다. 데이터 경로나 게임 이름도 쓰지 않는다.
- 시안 어셈블리 `HD2D.Runtime`(`Effect/HD2D/`, 시안 장면 전용)에 넣지 않는 이유가 있다. 3회차가 같은 폴더에서 아직 돌고 있다. 또 그 안의 시안 스크립트(`LookdevRenderScope`, `PixelWalkPreview` 등)가 게임 빌드에 섞인다. 시안 코드는 읽기만 한다. 출발점(`TiltedCameraUtils`, `PixelShadowCaster`)은 코드 규칙에 맞춰 새로 쓴다. `GroundPoint`는 예외를 던지고, 그림자 캐스터는 Awake/Initialize 순서 문제(execution-note3 마지막 항목)가 있다.
- `Game.asmdef`의 `references`에 `HD2D.View`를 더한다. 명시 참조 방식이라 `autoReferenced`로는 보이지 않는다. 시험 어셈블리 `Game.Tests.EditMode.asmdef`에도 더한다. **(DR-03) 에디터 어셈블리 `Game.Editor.asmdef`(`Editor/Game.Editor.asmdef:4-11`, 지금 참조는 Game·VContainer·URP·URP 2D·InputSystem·UI)에도 더한다.** 이행 메뉴 `MainSceneTiltedViewMigration`이 `TiltedCameraRig`·`UprightShadowCaster`를 붙이고 찾기 때문이다. 이 줄은 W2(이행 메뉴가 처음 생기는 단위)에서 넣는다.
- 셰이더는 `Assets/Game/03.Resources/Shaders/HD2D/`(새 폴더)에, 재질은 `Assets/Game/03.Resources/Materials/HD2D/`에 둔다. 볼륨 프로필은 `Assets/Game/03.Resources/Settings/HD2D_BattleVolume.asset`이다. 시안 셰이더(`Art/HD2D/PixelSpriteLit.shader`, `Round3/FieldSurface.shader`·`FieldOutline.shader`·`FieldBandBlur.shader`)는 3회차가 끝난 뒤 **복사**해 온다. 시안 자산을 게임이 참조하지 않게 하려는 것이다. 이름은 `HD2D/` 아래로 정리한다.

| 클래스(새/바뀜) | 폴더 | 책임 |
|---|---|---|
| `TiltedViewMath`(새, static Utils) | `Effect/HD2DView/Utils/` | 바닥 평면 ↔ 화면 평면 순수 계산: `FloorToScreenPlane`, `ScreenToFloorDirection`, `SnapToTexels`, `Footprint`, `UprightRotation`, `UprightOffset(높이m, θ) = (0, h·cosθ, h·sinθ)`(세운 축 방향 3D 오프셋, DR-01) |
| `PixelScaleMath`(새, static Utils) | `Effect/HD2DView/Utils/` | 화면 높이 → 정수 배율 n, 정사영 세로 반값 |
| `TiltedCameraRig`(새, MonoBehaviour) | `Effect/HD2DView/View/` | 카메라 오브젝트에 붙는다. 바라보는 점·흔들림 오프셋·화면 크기를 받아 위치와 정사영 크기를 정한다. `CameraShakeView`를 대신한다 |
| `UprightShadowCaster`(새, MonoBehaviour) | `Effect/HD2DView/View/` | 세운 스프라이트의 그림자 전용 메시(시안 `PixelShadowCaster`를 다시 씀). 대원 4명과 5단계의 세운 판 식물 |
| `ViewRulesData`(새, 데이터) + `Data/View/view_rules.json` | `Features/Field/Data/` | 각도·카메라 거리·클립·목표 인물 비율·배율 하한·방향 경계 넘김·몬스터 경계 여유. **수치를 정하는 곳은 여기 하나다** |
| `IsoProjection`(바뀜) | `Features/Field/Utils/` | 바닥 단위 ↔ 미터 3D: `FloorToWorld → Vector3 (x·s, 0, y·s)`, `WorldToFloor(Vector3)`, `IsoCellToFloor(Vector3Int)`. 이름은 그대로 둔다(사용처 13곳, 어휘 유지). 반환형이 바뀌어 모든 사용처가 컴파일에서 드러난다 |
| `PartyMoveMath`(바뀜) | `Features/Party/Utils/` | `ScreenToFloorDirection(input, sinTilt)` = `(x, y ÷ sin θ)` 정규화. 30°면 지금 ×2와 같다 |
| `SpriteFacing`(바뀜) + `FacingDirection`(새 enum, 같은 파일) | `Effect/Facing/` | 8방향 값·고르기·대신 쓰기(3.9). 이름과 자리는 그대로 둔다(9단계 이동 후보) |
| `WorldPresenter`(바뀜) | `Features/Field/Presenter/` | 장치에 화면 크기·바라보는 점을 넘기고, 보이는 범위를 계산하고, 뷰에 3D 위치·8방향을 준다. 이동량은 바닥 좌표로 계산한다 |
| `HitReactionPresenter`(바뀜) | `Features/Feedback/Presenter/` | 흔들림 오프셋만 장치에 준다. `ShakeMath.ResolveBase`(위치 역산)는 쓰지 않는다 |
| 뷰(바뀜): `MemberView`, `MonsterView`, `ProjectileView`, `BurstView`, `EnemyShotView`, `DamageNumberView`, `MonsterHpBarView`, `FootShadowView`, `FieldView` | 각 기능의 `View/` | `UpdateView(Vector3)`. 세운 자식과 눕힌 자식(3.7). 실행 중에 만드는 렌더러는 재질을 `[SerializeField]`로 받는다 |
| `MonsterSpawnService`, `MonsterRecycleService`, `MonsterBehaviorService`(바뀜) | `Features/Monster/Service/` | 들판 경계 자르기(3.6) |
| `CombatService`(바뀜), `MemberAttackMotion`, `MemberMotionData` | `Features/Combat/Service/`, `Effect/Motion/`, `Features/Party/Data/` | 공격 방향 8구간, 방향별 맞는 프레임 |
| `SpriteAtlasService`(바뀜) | `Core/Resource/` | 방향 접미어 표 하나(3.9) |
| `PlayableProbe`(바뀜) | `App/` | `CameraError`·투사체·방향 표기(6절) |
| `MainSceneTiltedViewMigration`(새, 에디터 메뉴) | `Editor/` | `Main.unity`와 프리팹 6개를 한 번에 바꾼다. 두 번 돌려도 결과가 같다. `MainSceneBuilder`는 "처음 한 번만" 도구라서 고치지 않는다(`MainSceneBuilder.cs:12-13,38-45`) |

### 3.2 상태 소유권 (11-18 확정)

| 상태 | 지금 주인 | 새 주인 | 쓰는 쪽 |
|---|---|---|---|
| 원정대 중심·대원·몬스터 바닥 좌표 | `PartyModel`, `MonsterModel` | 그대로 | 규칙 서비스(쓰기), 뷰 쪽(읽기) |
| 카메라 위치 | `WorldPresenter`가 직접 씀(`WorldPresenter.cs:105-106`). `HitReactionPresenter`가 다시 덮어씀(`:76,90`) | **`TiltedCameraRig` 하나**. 입력은 셋이다: 바라보는 점(`WorldPresenter`), 흔들림 오프셋(`HitReactionPresenter`), 화면 크기(`WorldPresenter`). 위치 = 텍셀 맞춤(바라보는 점) − forward·거리 + 카메라 오른쪽·위 축 × 흔들림. 입력 하나가 들어올 때마다 바로 다시 계산하므로 LateTick 순서에 기대지 않는다(초안 R3) | 판정 진입점 `CameraError`는 **실제 카메라 transform**에서 다시 계산한다(DR-04, 3.12). `Rig.Focus` 입력값만 비교하지 않는다 |
| 정사영 크기·정수 배율 | 장면 값 3.75 + `PixelPerfectCamera` | `TiltedCameraRig`가 `camera.pixelHeight`로 정한다(3.4) | `WorldPresenter.UpdateViewExtent`가 `camera.orthographicSize`·`aspect`를 읽는다(지금과 같은 자리) |
| 보이는 바닥 범위(규칙용) | `MonsterModel.ViewHalfExtent`, `PartyMoveService._centerLimit` | 그대로. 값 계산만 `TiltedViewMath.Footprint`로 바뀐다 | 생성 거리·재활용·이동 제한 |
| 그림 방향 | `MemberView.Facing`, `MonsterView.IsFlipped`, `MemberAttackMotion.Facing` | 그대로(형만 8방향 `SpriteFacing`). `MonsterView`는 `Facing`을 갖는다 | 판정 진입점 `MemberFacing` |
| 각도·배율 규칙 값 | 코드 상수(`IsoProjection.cs:8`, `PartyMoveMath.cs:7`) | `ViewRulesData`(Scoped 데이터) | `WorldPresenter`, `PartyMoveService`, `CombatService`, 뷰 쪽 Presenter들 |

### 3.3 좌표 변환은 한 곳 (11-18 확정)

- 바닥 → 3D는 `IsoProjection.FloorToWorld(floor, s) = (x·s, 0, y·s)` 하나다(미터, 바닥 높이 0). 세로를 누르는 일은 카메라가 맡는다. 30°에서는 화면 위치가 지금과 **픽셀 단위로 같다**. 화면 위 성분 = z·sin 30° = y·s·0.5로, 지금의 `VerticalCompress`와 같다.
- 바닥 방향 → 화면 방향은 `TiltedViewMath.FloorToScreenPlane(d, sinθ) = (dx, dy·sinθ)`다. 정사영이라 화면 어디서나 정확하다. 방향 고르기·공격 방향·효과 각도에 쓴다.
- 세운 판 오프셋(머리 높이, HP 막대, 숫자)은 세운 자식의 **로컬 좌표**에 둔다(3.7). 따로 변환하지 않는다. 세운 자식의 회전 = `UprightRotation(θ)` = Euler(θ, 0, 0) = 카메라 회전이다. 그래서 로컬 1/48m가 화면에서 정확히 1텍셀이다.
- **텍셀 맞춤**: `TiltedViewMath.SnapToTexels(world, sinθ, 48)`은 바닥 점의 x를 1/48m, z를 1/(48·sinθ)m 격자에 맞춘다. 바닥 점이 화면에서 정확히 한 텍셀씩 움직이고 높이는 0으로 남는다. 30°면 z 격자는 1/24m다. 바닥 질감 밀도(가로 48, 깊이 24 텍셀/m, execution-note3)와도 맞다. **그리는 위치를 정할 때만** 쓴다. `IsoProjection.FloorToWorldSnapped`는 대원·몬스터·효과·표시 뿌리와 카메라 바라보는 점에 쓴다. 판정·계산에는 맞추기 전 값을 쓴다. 지금 `PixelPerfectCamera` `PixelSnapping`과 `CombatEffectMotion.Snap`(1/48)이 하던 일을 화면 평면에서 똑같이 한다(초안 R4: **맞춘다**).
- 타일 칸 → 바닥은 `IsoProjection.IsoCellToFloor(cell) = ((cx−cy)/2, (cx+cy)/2)`다. 단위는 미터를 바닥 단위로 바꾼 값이다. 장식 위치 계산(`FieldView.cs:24`의 `CellToWorld`)을 대신한다.
- 버린 안: 모든 그림을 카메라에 수직인 평면 하나에 두고 2D 좌표를 그대로 쓰는 안. 화면은 같지만 3D 소품 가림·해 그림자·깊이 정렬이 모두 깨진다(초안과 같음).

### 3.4 카메라 장치·정수 배율·화면비·안전 영역 (11-18 확정)

| 항목 | 값 | 근거 |
|---|---|---|
| 투영 | 정사영, 회전 Euler(30, 0, 0) | E1 |
| 거리 | 바라보는 점에서 20m 뒤(near 0.3, far 60) | execution-note3: 그림자 거리 35m 안에 소품이 들어오게 35→20m. 정사영이라 구도는 같다 |
| 정수 배율 | `n = max(minScale, RoundToInt(H × targetFraction ÷ 48))`, `targetFraction = 0.1`, `minScale = 1`. H = `camera.pixelHeight` | 시안 2회차 F-int와 같은 식(`PixelLookdevCaptureUtils.cs:151`). 1080줄 → 2.25 → **2**(E2) |
| 정사영 세로 반값 | `size = H ÷ (2·48·n)` | 세운 판 1텍셀 = 정확히 n픽셀. 바닥은 가로 48·깊이 24 텍셀/m라 바닥 1텍셀도 n픽셀(execution-note3) |
| 남는 공간 | 레터박스나 자르기를 쓰지 않는다. 화면이 넓거나 높으면 **바닥(배경)이 더 보인다**. 원정대 이동 제한이 보이는 범위만큼 줄어서, 들판 밖 바닥은 보이지 않는다(지금 규칙 그대로, 배경 준비 D-8). 들판 밖 테(32~36m)는 키 큰 것의 머리만 화면 위쪽에 보인다 | prep 3.1 |
| Render Scale | 1.0(모든 품질) | D4. 1 미만이면 정수 배율이 깨진다 |
| 화면 크기 변화 | `WorldPresenter.LateTick` 맨 앞에서 `rig.UpdateScreen(camera.pixelWidth, camera.pixelHeight)`를 부른다. 크기가 바뀌었을 때만 n·size를 다시 정한다 | 같은 프레임의 `UpdateViewExtent`보다 먼저 돈다 |

화면별 값(계산: `python3` 식 = 위 표. n은 Unity `RoundToInt`(짝수 반올림)과 같은 결과):

| 화면(가로×세로) | n | size | 인물 | 보이는 반폭 / 반깊이(m) | 원정대 이동 한계 L1(m) | 생성 거리(m) |
|---|---|---|---|---|---|---|
| 지금 2D(모든 기기, 16:9) | — | 3.75 | 13.3% | 6.67 / 7.50 | 17.83 | 10.83 |
| 1920×1080 16:9 | 2 | 5.625 | 96px 8.9% | 10.00 / 11.25 | 10.75 | 15.84 |
| 2340×1080 19.5:9 | 2 | 5.625 | 96px 8.9% | 12.19 / 11.25 | 8.56 | 17.38 |
| 2520×1080 21:9 | 2 | 5.625 | 96px 8.9% | 13.13 / 11.25 | 7.62 | 18.08 |
| 2048×1536 4:3 | 3 | 5.333 | 144px 9.4% | 7.11 / 10.67 | 14.22 | 13.61 |
| 2532×1170 (iPhone) | 2 | 6.094 | 96px 8.2% | 13.19 / 12.19 | 6.62 | 18.75 |
| 2778×1284 | 3 | 4.458 | 144px 11.2% | 9.65 / 8.92 | 13.44 | 13.93 |
| 3200×1440 | 3 | 5.000 | 144px 10.0% | 11.11 / 10.00 | 10.89 | 15.74 |
| 1280×720 | 2 | 3.750 | 96px 13.3% | 6.67 / 7.50 | 17.83 | 10.83 |

- 이 표는 **게임 동작이 바뀐다**는 뜻이다. 보이는 범위가 1.5배가 되면서 생성 거리가 10.8m에서 15.8m로 늘고, 이동 한계가 17.8m에서 10.75m로 준다(1920×1080 기준). 지금 2D는 화면비만으로 달라졌는데, 이제 세로 픽셀 수로도 달라진다. 대응과 측정은 7절 위험 1·2에 있다.
- 안전 영역: 3D 화면은 화면 전체에 그린다. 노치 뒤는 배경이다. 원정대 중심은 **화면 가운데**에 둔다. 안전 영역의 가운데가 아니다. 노치 폭(가로 화면 약 40~50px)만큼 치우쳐 보이는 것보다 화면비마다 같은 구도가 낫다고 봤다. HUD·조이스틱은 지금처럼 `UI_SafeAreaPanel` 안에 둔다. 생성·재활용은 화면 전체 기준이다. 노치 뒤에서 몬스터가 생겨도 화면 밖과 같게 본다.
- `PixelPerfectCamera`를 뺀다. 3D 렌더러에서 쓸 수 없고, 정수 배율은 위 식이 맡는다.

### 3.5 보이는 바닥 범위 (11-18 확정: 정사영 하나)

- 순수 함수 `TiltedViewMath.Footprint(size, aspect, sinθ) → Vector2(반폭, 반깊이)`(미터)다. 반폭 = size·aspect, 반깊이 = size ÷ sinθ다. `WorldPresenter.UpdateViewExtent`(`:234-247`)는 이것을 s로 나눠 바닥 단위로 바꾸고, 바뀔 때만 서비스 둘에 알린다(지금과 같은 자리).
- 쓰는 곳은 셋이다. 생성 거리(`MonsterSpawnService.cs:147`), 이동 제한(`PartyMoveService.cs:30`), 화면 밖 재활용(`MonsterRecycleService.cs:47-51,82-84`). 규칙 코드는 바꾸지 않는다.
- 30°이고 size가 3.75면 지금 `IsoProjection.WorldToFloor(halfExtent)`와 같은 값이 나온다. 이것이 회귀 시험 F1의 근거다.
- 세운 판의 키를 생각해야 한다. 발이 화면 아래 끝 바로 밖에 있어도 몸은 화면에 보인다. 생성은 대각선 + 여유 70이라 문제가 없다(15.8m ≫ 반깊이 11.25m). 재활용 여유 300(약 3.4m)도 몸 키(1m = 화면 2m 깊이 상당)보다 크다. 지금과 같은 판단이다.
- 원근 갈래(초안 P1·P2, R2, F4~F7 시험)는 지운다.

### 3.6 몬스터 생성·이동 경계 — I4 결정 (11-18 확정)

- 문제(개선 기록 I4, prep 1절): 생성 위치(`MonsterSpawnService.cs:147-151`), 재활용 위치(`MonsterRecycleService.cs:82-84`), 행동 이동(`Behavior/*.cs`의 `monster.Position +=`)이 모두 들판 경계를 보지 않는다. 그래서 몬스터가 띠 C(L1 20~32)와 그 바깥 테(32~36, 먼 쪽 언덕 0→3m·턱)까지 걷는다. 3D에서는 언덕 안에 묻히거나 낮은 내리막 위에 떠 보인다.
- **결정: 생성·재활용 위치와 매 걸음 이동을 들판 마름모 `L1 ≤ 반대각선 − monsterEdgeMargin` 안으로 자른다. `monsterEdgeMargin`은 0이다(데이터 `view_rules.json`이 아니라 `spawn_rules.json` 새 칸 `fieldEdgeMargin`. 규칙 값이라서다).**
  - 자르기 함수는 `PartyMoveMath.ClampToDiamond(position, limit)`을 그대로 쓴다(`PartyMoveMath.cs:22-38`, 시험 있음. 들판이 원점 가운데라 위치를 그대로 넣는다).
  - 자리: 생성은 `Spawn`에서 위치를 만든 직후다. 재활용은 `monster.Position = …` 직후다. 이동은 `MonsterBehaviorService.Tick`의 행동 호출(`:55`) 직후 **한 곳**이다. 행동 4종(접촉·투사체 뒷걸음·돌진·자폭 무리)을 각각 고치지 않는다.
  - 여유를 0으로 둔 이유가 있다. 원정대 이동 한계가 "들판 − 보이는 범위"라서 보이는 직사각형은 늘 마름모 안에 있다. 그래서 마름모 경계 위의 점은 늘 화면 밖이거나 화면 끝이다(직사각형 모서리 L1 ≤ |cx|+|cy|+반폭+반깊이 ≤ 반대각선). 여유를 0보다 크게 두면 화면 안 모서리에서 몬스터가 생길 수 있다. 몸이 테 쪽으로 반 칸쯤 걸치는 것은 5단계 배경에서 테 시작부를 낮게 두는 것으로 처리한다(배경 준비에 넘김).
- 띠 C의 소품(나무·바위·돌담)은 몬스터가 뚫고 지나간다. 충돌이 없는 것은 지금 2D 장식과 같다. 가려지는 문제는 5단계 D12 디더로 다룬다. 개울은 prep D-6 "얕은 물 그대로"를 따르고, 몬스터가 지나간다.
- 버린 안
  - (가) 바깥 띠·테를 모두 걷는 평평한 땅으로 두기. prep의 테 언덕·턱 배치와 부딪힌다.
  - (나) 생성 위치만 자르기. 투사체 몬스터의 뒷걸음과 돌진 넘침이 여전히 밖으로 나간다.
- 게임 동작 영향: 원정대가 들판 가운데 근처에 있으면 바뀌지 않는다(1920×1080에서 생성 거리 15.84m → 최대 L1 22.4 < 32). 원정대가 이동 한계 근처에 있으면 그쪽 생성이 경계로 당겨져 더 가까이에서 생긴다. 판정 경로 G5·G7과 웨이브 목표값으로 잰다(W5).

### 3.7 세운 판 뷰 (초안 3.4 유지, 11-18 확정 표시는 바뀐 행만)

원칙: **뷰 뿌리는 바닥 점이다(회전 없음, 텍셀 맞춤). 서 있는 그림은 "세운 자식"(회전 = 카메라 회전) 아래에 둔다.** 바닥에 눕는 그림(그림자·폭발 범위·돌진 선·바닥 장식·치유 범위)은 뿌리 아래에 Euler(90,0,0)으로 눕히고 1mm 띄운다. 돌리는 빌보드가 아니다(D4). 그래서 지금의 로컬 픽셀 오프셋(HP 막대 높이, 대기 1px, 피격 1px, 숫자 자리)은 세운 자식 안에서 그대로 맞다.

| 그림 | 새 구조 |
|---|---|
| 대원 몸·번쩍임·HP 막대 | 뿌리 = 바닥 점. 몸·번쩍임·HP 막대·빨간 구간은 세운 자식 아래에 둔다. 로컬 값은 그대로다(`MemberView.cs:69-72,96`) |
| 대원 발 그림자 | **(11-18 확정)** 실제 그림자(`UprightShadowCaster`)와 함께 **발 그림자 판도 남긴다.** 바닥에 눕히고 반투명으로 둔다. 해 고도 30°의 실제 그림자는 길고 발에서 떨어져 보여서 발을 땅에 붙이는 판이 필요하다. 알파·크기는 6단계 값. `FootShadowView.cs:16-21`의 "Y로 반 높이 내림"을 없앤다 |
| 몬스터 몸 | 뿌리 = 바닥 점, 몸은 세운 자식(프리팹 Body를 세움). 실제 그림자는 내지 않는다(D7) |
| 몬스터 발 그림자 | 바닥에 눕힌 판(`MonsterView.cs:65-75`) |
| 몬스터 폭발 범위 | 바닥에 눕힌 원. 지름 = 반지름×2×s다. 아이소 누르기 계산(`MonsterView.cs:56-61`)은 없앤다 |
| 몬스터 돌진 선 | 바닥에 눕히고 Y축 회전 = 바닥 방향 각도로 둔다(`:104-107`의 Z 회전 대신) |
| 투사체·적 탄 | 뿌리 = 바닥 점, 세운 자식 높이 = 몸 높이. 그림 회전은 세운 자식 안 Z = `FloorToScreenPlane(방향)`의 화면 각도 |
| 발동 그림(베기·치유·폭발·고리·연결) | 기준점은 바닥 점 + 세운 오프셋이다. 각도·마디 수는 화면 평면 길이(`FloorToScreenPlane`)로 계산한다. 정사영이라 연결 마디 보간이 정확하다. 치유 범위·파동 고리는 눕힌다 |
| 피해 숫자 | 뿌리 = 바닥 점. 숫자 뷰 = 세운 자식이고, 머리 높이 + 오르기는 로컬 y다(`DamageNumberPresenter.cs:112,118-122`) |
| 몬스터 HP 막대 | 같은 방식(`MonsterHpBarPresenter.cs:235-239`) |
| 바닥(4단계 임시) | **(11-18 확정, 초안 R5 = G1)** 지금 타일맵을 그대로 눕힌다. Grid 회전 Euler(90,0,0), 칸 (1,1), 타일 행렬 y 배율 2. 화면 모습은 지금과 같고(30°에서 48×24 그림 = 화면 48×24), 판정 진입점 `GroundTileCount`와 `GroundTilePicker` 시험이 그대로 돈다. 재질은 도트 Lit(그림자를 받음). 5단계에서 3D 바닥으로 바꾼다 |
| 장식(`decor.json`) | **(11-18 확정)** F2c 원칙대로 나무·덤불·꽃·풀(`IsGround`가 아닌 것)은 세운 판, `IsGround`는 눕힌 판으로 둔다. 위치는 `IsoCellToFloor` → `FloorToWorld`. 5단계에서 시안 F 배치로 바꾼다 |

- 위치를 정하는 메서드는 `UpdateView(Vector3 worldPosition)`으로 바꾼다.
- `WorldPresenter`가 이동량을 transform에서 읽는 곳(`:264,389`, `position − view.transform.localPosition`)은 3D·텍셀 맞춤에서 깨진다. **뷰마다 지난 바닥 좌표**(`Vector2[] _lastMemberFloors`, 몬스터는 id별 사전)를 들고 바닥 이동량을 쓴다. 걷기 시계(`:338`)는 바닥 이동량의 길이를 그대로 쓴다. 방향 고르기(`:333,420`)는 `FloorToScreenPlane(바닥 이동량 × s)`을 쓴다. `minMove`의 단위(화면 미터)는 30°에서 지금과 같다.
- 겹침 순서: 불투명 + 알파 클립 그림은 깊이 버퍼가 정한다(D14). 지금 `sortingOrder`·`SpriteSortPoint`는 반투명 그림(3.8)에서만 의미가 남는다.

### 3.8 재질·그림자·번쩍임·월드 표시 (11-18 확정)

재질은 4단계에 **6개**다(눕힌 타일맵 바닥도 M1을 쓴다). 5단계 3D 소품 재질은 따로 센다.

| # | 재질(셰이더) | 쓰는 그림 | 큐·상태 |
|---|---|---|---|
| M1 | `HD2D_SpriteLit`(시안 `PixelSpriteLit` 출발. 손으로 쓴 HLSL, 툰 3단, 알파 클립 0.5, 양면, `unity_SpriteColor`·반전 지원) | 대원 몸, 몬스터 몸, 투사체, 적 탄, 세운 장식, 눕힌 타일맵 바닥 | AlphaTest, ZWrite On. 그림자를 받는다 |
| M2 | `HD2D_SpriteFade`(빛 없음, 반투명) | 발동 그림(알파 페이드 `BurstView.cs:22`), 반투명 돌진 선·폭발 범위, 눕힌 발 그림자 판 | Transparent, ZWrite Off, `sortingOrder` 유지 |
| M3 | `HD2D_ShadowOnly` | `UprightShadowCaster`의 그림자 전용 메시 | ShadowCaster 패스만 |
| M4 | `HD2D_SpriteFlash`(흰색, 알파 클립, 빛 없음) | 대원 번쩍임 자식 | AlphaTest |
| M5 | `HD2D_Indicator`(빛 없음, ZTest Always, ZWrite Off) | 대원·몬스터 HP 막대, 빨간 구간, 피해 숫자 | 레이어 `WorldIndicator`. 렌더러의 일반 그리기에서 빼고 Render Objects 기능이 **후처리 뒤**(AfterRenderingPostProcessing)에 그린다 |
| M6 | `HD2D_BandBlur`(Full Screen Pass) | 화면 위아래 흐림 | 렌더러 기능 |

- **번쩍임(초안 R6 = 띄운 판)**: 지금 구조를 그대로 둔다. 몸 자식의 자식 렌더러(`flashRenderer`)이고 같은 스프라이트·반전이다. 이 자식을 세운 자식의 로컬 −z(카메라 쪽)로 1/48m 띄우고 재질을 M4로 바꾼다. 재질 속성 블록을 쓰지 않아 SRP 배칭이 그대로다. 대상이 대원 4명뿐이라 그리기 4번이 늘어날 뿐이다. 시험 `MemberViewSpriteTests`의 `IsFlashOn`은 그대로 맞다.
- **HP 막대·피해 숫자(초안 R7 = 늘 위)**: 앞의 몬스터·소품에 가려지지 않는다. 위아래 흐림·Bloom도 받지 않는다(후처리 뒤에 그림). 흐림 띠 안에서도 읽혀야 하는 정보라서 이렇게 정했다. 서로의 순서는 지금 `sortingOrder`(막대 1·2, 숫자 60)를 따른다.
- **그림자(초안 R8 = 채택)**: `SpriteRenderer` 그림자 패스는 그림 데이터를 제대로 받지 못한다(시안 관찰). 그래서 그림자 전용 메시를 쓴다. 대상은 대원 4명이다. 5단계에서 세운 판 식물(F2c)도 같은 컴포넌트를 쓴다. 몬스터는 눕힌 판이다(D7, 400마리 그림자 패스 비용). 새 컴포넌트는 시안 문제(활성 상태에서 `AddComponent` → Awake가 Initialize보다 먼저 돎 → 재질 null)를 피한다. 메시와 렌더러를 `Initialize`에서 만들고 Awake는 쓰지 않는다. 해: 고도 30°, 방위 135°, 하드 그림자, 따뜻한 갈색(r6 출발값).
- 재질을 바꿀 곳
  - 장면 `Main.unity`의 Sprite-Unlit-Default 참조 17곳.
  - 프리팹 6개(`BurstView`, `EnemyShotView`, `MonsterHpBarView`, `MonsterView`, `ProjectileView`, `DamageNumberView`).
  - **실행 중 만드는 렌더러 3곳.** `FootShadowView.cs:12`, `FieldView.cs:27`, `DamageNumberView.cs:23`이다. 지금은 재질을 정하지 않아 3D 렌더러에서 빛을 안 받는 기본 재질이 된다. 소유 뷰에 `[SerializeField] Material`을 두고 넘긴다: `MemberView`·`MonsterView` → 그림자 판, `FieldView` → 장식, `DamageNumberView` → 숫자. 프로젝트 규칙대로 SerializeField 연결을 믿고 null 검사는 하지 않는다.
- `SpriteFlashWhite.mat`(GUI/Text 셰이더)은 장면 참조 4곳을 M4로 바꾼 뒤 지운다.

### 3.9 8방향 고르기 (11-18 확정)

**값**: `FacingDirection` = { Down, DownRight, Right, UpRight, Up }(그림 5방향). `SpriteFacing(Direction, IsFlipped)`. `Front` = (DownRight, false)로, 지금 앞·오른쪽과 같은 모습이다. `Label`은 8값이다: `"down"`, `"down-right"`, `"right"`, `"up-right"`, `"up"`, `"down-left"`, `"left"`, `"up-left"`. Down·Up 표기는 반전과 상관없다.

**고르기**(순수 static, EditMode 시험 대상):

```
SpriteFacing.Next(SpriteFacing last, Vector2 screenDelta, float minMove, float holdDegrees)
```

1. 짧은 움직임: `|delta.x| < minMove && |delta.y| < minMove`면 `last`를 그대로 쓴다(지금 규칙과 같음).
2. 화면 각도 a = atan2(dy, dx)를 구한다. 0° = 오른쪽, 반시계가 +다.
3. 경계 넘김: last의 구간 가운데 각 c(오른쪽 0, 오른쪽 아래 −45, 아래 −90, 오른쪽 위 45, 위 90. 반전이면 180 − c)와 a의 각 차가 `22.5 + holdDegrees` 이하이면 `last`를 그대로 쓴다.
4. 아니면 k = round(a/45) (−4..4, 4와 −4는 같다)로 고른다: 0 → Right, −1 → DownRight, −2 → Down, 1 → UpRight, 2 → Up, ±4 → Right+반전, −3 → DownRight+반전, 3 → UpRight+반전.
5. Down·Up이면 반전은 `last.IsFlipped`를 그대로 둔다. 위·아래로만 움직일 때 좌우가 깜빡이지 않게 하려는 것이다.

| 쓰는 곳 | 입력 | `holdDegrees` |
|---|---|---|
| 대원 이동(`WorldPresenter.cs:333`) | `FloorToScreenPlane(바닥 이동량·s, sinθ)`, `minMove = MinMovePixels/48` | `view_rules.json` `facingHoldDegrees` = **12**(방향 조사 10~15°의 가운데) |
| 몬스터 이동(`:420`) | 같음. 몬스터도 이제 8방향(위 1~5)이다 | 12 |
| 공격 방향(`CombatService.cs:407`) | `FloorToScreenPlane(대상 방향, sinθ)`. sinθ는 주입된 `ViewRulesData`에서 온다. 카메라 뷰가 아니라 데이터라서 레이어 규칙 위반이 아니다 | 0(늘 가장 가까운 구간). `minMove` 0.001 그대로 |

- **공격 중에는 대상 쪽을 본다**: 대원은 지금처럼 한다(`WorldPresenter.cs:357-360`). 몬스터는 이동 방향만 쓴다(초안 R9 = 아니오). 몬스터에는 대원 같은 "공격 동작 시계"가 없고, 돌진 예고 중에는 멈춰 있어서 마지막 이동 방향(대개 원정대 쪽)이 남는다.
- **방향별 맞는 프레임**(게임 계산에 닿음): `MemberMotionData.HitSeconds(FacingDirection)`이다. UpRight·Up이면 `hitBack`, 나머지는 `hitFront`를 쓴다. 방향별 칸이 데이터에 생기면 그 값을 먼저 쓴다(새 칸은 선택이고 기본은 −1). **지금과 다른 점(DR-05로 고침)**: 지금 `SpriteFacing.Next`(`SpriteFacing.cs:27-36`)는 세로 성분이 `|dy| ≥ 0.25·|dx|`를 넘을 때만 앞·뒤를 바꾸고, 넘지 않으면 **지난 `IsBack`을 그대로 둔다**. 그래서 바뀌는 경우가 두 가지다.
  - (가) **지난 방향이 뒤쪽**이고 대상이 화면 각 |a| < 14.0°(좌우 반전 쪽은 180°±14°)일 때. 예: 지난 = 뒤, 입력 (1,0) → 지금 hitBack, 새 Right → hitFront.
  - (나) 대상이 각 14.0°~22.5°(위쪽, 반전 쪽 157.5°~166°)일 때. 지난 방향과 상관없이 지금은 뒤(hitBack), 새로는 Right(hitFront)다.
  - 그 밖의 각도(아래쪽 전부, 위쪽 22.5° 이상)는 앞·뒤 판단이 같다.
  - 시간 차이가 실제로 나는 대원은 `hitFront ≠ hitBack`인 bruno(2 vs 3프레임, 10fps → 0.1초, `Data/Character/bruno.json` — 03:4x 데이터 확인으로 정정)뿐이다. bolt·mira·lena는 두 값이 같아 맞는 시각이 바뀌지 않는다. 그림 방향은 네 명 모두 바뀐다.
  - 결정론 시험은 그대로 통과해야 한다(같은 입력 → 같은 결과). 이 변경은 W4에 들어가므로 **웨이브 목표값·G5·G7 재측정을 W4 뒤에 한 번 더 한다**(W4 확인 칸, 6절 위험 4).
- **그림이 없을 때 대신 쓰기**: 순수 함수 `SpriteFacing.ResolveAvailable(FacingDirection wanted, int availableMask) → FacingDirection`이다. 각도가 가장 가까운 있는 방향을 고르고, 같으면 아래쪽(앞)을 고른다. 지금 그림으로 따지면 대원은 Down·DownRight·Right → DownRight(앞), UpRight·Up → UpRight(뒤)다. 몬스터(앞 하나)는 모두 DownRight다. 뷰는 `Initialize`에서 5칸 표(방향 → 그림·클립)를 한 번 만든다. 대신 쓴 방향 수는 판정 진입점 `FacingFallbackCount`로 내보낸다(기준서 3.3 범위 대조용).
- **그림 이름(제안, r6에서 확정)**: 접미어 `_down`, `_downright`, `_right`, `_upright`, `_up`. 지금 `_front` = `_downright`, `_back` = `_upright`로 읽는 **별칭 표**를 `SpriteAtlasService` 한 곳에 둔다(`SpriteAtlasService.cs:12-13`). 그래서 지금 그림 114장을 이름 바꾸지 않고 쓴다. 클립도 같다(`walk_front` → `walk_downright` 별칭, `WorldPresenter.cs:225-228`). r6가 다른 이름을 정하면 이 표만 고친다. 로비·HUD 초상(`LobbyPresenter.cs:53`, `BattleHudPresenter.cs:80`)은 `_front`를 그대로 쓴다. 몬스터의 `_telegraph`·`_inflate` 상태 그림도 방향 접미어를 받는다. 없으면 대신 쓰기를 따른다.
- 쓰러진 그림은 방향 1장 + 반전이다(지금과 같음). 부활 때는 지난 방향을 쓴다.

### 3.10 UI 캔버스와 3D 화면의 경계 (11-18 확정)

| 무엇 | 어디에 | 이유 |
|---|---|---|
| HUD·로비·정비·결과·팝업·조이스틱 | **Screen Space Overlay 캔버스 그대로**(`MainSceneBuilder.cs:181-191`). U단계가 모양만 바꾼다 | 오버레이는 URP 후처리(흐림·Bloom·비녜트) 뒤에 그려져 영향을 받지 않는다. Screen Space Camera로 바꾸면 흐림 띠가 UI를 뭉갠다 |
| 대원·몬스터 HP 막대, 피해 숫자 | **3D 월드의 세운 판**(M5, 후처리 뒤 그림) | 수백 개가 매 프레임 바닥 점을 따라간다. 캔버스로 옮기면 화면 좌표 변환과 캔버스 다시 그리기가 매 프레임 생긴다. 월드 판이면 텍셀 크기 = n픽셀로 3D와 같은 도트 배율이다 |
| 효과(베기·폭발·고리·투사체) | 3D 월드(M1·M2) | 후처리(Bloom)를 받는 것이 맞다 |
| 픽셀 배율 | 월드 표시는 n(3.4). 캔버스는 지금 높이 맞춤(1080줄에서 3배, 1170줄에서 3.25배) | **U단계에 넘김**: 캔버스 배율을 정수로 맞출지(Constant Pixel Size + `PixelScaleMath`), 피해 숫자 크기(레퍼런스 약 7%, UI 관찰) — 숫자 배율은 텍셀이 정수 픽셀이 되도록 정수배만 쓴다 |
| 화면 효과를 UI 뒤 배경에만 | 볼륨은 전투 카메라 하나에만 둔다 | 로비·정비에서도 3D 장면이 뒤에 그려지는 것은 지금과 같다(판넬이 덮음). 비용은 7단계에서 본다 |

### 3.11 DI 등록 자리 (`App/GameSceneLifetimeScope.cs`)

| 추가·변경 | 자리 | 비고 |
|---|---|---|
| `builder.Register(r => r.Resolve<ResourceService>().LoadJson<ViewRulesData>("Data/View/view_rules"), Lifetime.Scoped)` | 데이터 묶음(`:34-52`) | 다른 규칙 데이터와 같은 방식 |
| `builder.RegisterComponentInHierarchy<TiltedCameraRig>()` | 장면 컴포넌트(`:103-111`). `CameraShakeView` 줄을 바꾼다 | `Camera` 등록(`:103`)은 판정 진입점과 `WorldPresenter`가 쓰므로 그대로 둔다 |
| 받는 쪽 생성자 | `WorldPresenter`(+`TiltedCameraRig`, `ViewRulesData`), `HitReactionPresenter`(`CameraShakeView` → `TiltedCameraRig`), `PartyMoveService`·`CombatService`·`CombatWorldPresenter`·`DamageNumberPresenter`·`MonsterHpBarPresenter`·`EnemyShotPresenter`(+`ViewRulesData`) | 새 서비스는 없다. 초안의 `ViewProjection` 서비스는 **없앤다**(11-18 확정). 계산은 static Utils + 데이터 하나로 충분하고, 주입 대상 서비스가 하나 줄어든다 |
| 진입점 순서 | 그대로. `:131-135` 주석("흔들림은 WorldPresenter 뒤")은 장치가 입력을 따로 받아 순서 의존이 없어졌다고 고친다 | 초안 R3 |

- 순환 의존: 새 의존은 모두 데이터(`ViewRulesData`)이거나 장면 컴포넌트(`TiltedCameraRig`)로 향한다. 둘 다 아무것도 주입받지 않으므로 순환이 생기지 않는다. `TiltedCameraRig`는 `Initialize(각도, 거리, 클립, 목표 비율, 배율 하한)`을 `WorldPresenter.Initialize`에서 받는다.

### 3.12 판정 진입점 `CameraError` (DR-04, 11-18)

- 지금은 `PlayableProbe.cs:189-197`이 카메라 transform 위치와 원정대 중심의 2D 위치를 비교한다. 기능 판정 `f2-movement-flows.json:38,45,53`은 `≤ 0.021`을 기대한다. 이 검사가 확인하는 것은 "카메라가 실제로 따라감"이다.
- 새 정의는 **실제 카메라 transform에서 초점을 복원**해 원정대 중심과 비교한다. 장치 입력값(`Rig.Focus`)만 비교하면 장치가 transform을 갱신하지 않아도 0이 나오므로 쓰지 않는다.

```
cam = Camera transform (실제 값)
restored = cam.position + cam.forward·rig.Distance − (cam.right·rig.ShakeOffset.x + cam.up·rig.ShakeOffset.y)
expected = IsoProjection.FloorToWorld(PartyModel.Center, s)
diff = restored − expected
CameraError = |(dot(diff, cam.right), dot(diff, cam.up))|   // 화면 평면 거리(m). 깊이 방향 성분은 정사영이라 화면에 안 보여 뺀다
```

- `rig.Distance`(설정값)와 `rig.ShakeOffset`(마지막으로 받은 흔들림)은 장치가 공개하는 **입력 기록**이다. 위치는 transform에서 읽는다. 그래서 transform 갱신이 빠지면 오차가 커져 잡힌다(시험 C5).
- 허용치: 텍셀 맞춤 오차는 화면 평면에서 최대 x 1/96, 위 1/96이라 hypot 0.0147m다. 그래서 기존 기대 `≤ 0.021`을 그대로 쓴다. 기능 판정 파일은 고치지 않는다.
- 시험(7절 C1~C5): 같은 프레임에 입력이 두 순서로 들어오는 경우(바라보는 점 → 흔들림, 흔들림 → 바라보는 점), 흔들림이 끝나 오프셋이 0으로 돌아오는 경우, 일시정지(흔들림 오프셋은 남고 바라보는 점만 바뀜), transform이 갱신되지 않은 경우를 본다. 장치를 붙인 GameObject + Camera를 EditMode에서 만들어 돈다(`TiltedCameraRigTests`).

### 3.13 좌표 형 바꾸기와 소비처 이행 — 같은 단위(W2)에서 (DR-01, 11-18)

`IsoProjection.FloorToWorld`의 반환형을 `Vector3`로 바꾸면 소비처가 두 가지로 깨진다.

- `Vector3 + Vector2` 연산은 연산자 모호성으로 **컴파일 오류**가 난다.
- `Vector3`를 `Vector2` 인자·변수에 넣으면 **조용히 암시 변환**되어 z(바닥 깊이)를 잃는다. 컴파일은 통과하므로 더 위험하다. 예: 공격 방향이 세로 성분을 잃어 이전 방향에 기대게 되고, 맞는 시각과 G5 측정이 오염된다.

그래서 **형 바꾸기와 모든 소비처 이행을 W2 한 단위에서 끝낸다.** 화면 방향 계산은 `FloorToScreenPlane`으로 바꿔 지금 값을 그대로 유지한다(30°에서 `(x, 0.5y)` = 지금 `FloorToWorld(d, 1)`). W3로 미루는 것은 시각 구조(세운 자식·눕힘·재질·레이어·그림자·후처리)뿐이다. 호출처는 `grep -rn "IsoProjection\." Assets/Game`로 확인했다(HEAD `e45394d`, 시안 폴더 제외).

| # | 소비처(파일:줄) | 지금 | W2에서 바꿀 것(값 유지 방법) |
|---|---|---|---|
| 1 | `WorldPresenter.cs:105-106` 카메라 | 2D 중심 → 카메라 x·y | `rig.SetFocus(FloorToWorld(center))`(3.2) |
| 2 | `WorldPresenter.cs:238` 보이는 범위 | `WorldToFloor(Vector2)` | `Footprint(...) ÷ s`(`Vector2`, 3D 없음) |
| 3 | `WorldPresenter.cs:263-264` 대원 위치·이동량 | `Vector2` − transform | `FloorToWorldSnapped` → `MemberView.UpdateView(Vector3)`. 이동량은 지난 바닥 좌표(3.7) |
| 4 | `WorldPresenter.cs:338` 걷기 시계 | `WorldToFloor(delta)` | 바닥 이동량 길이 그대로 |
| 5 | `WorldPresenter.cs:387-389` 몬스터 | 같음 | 3과 같음, `MonsterView.UpdateView(Vector3)` |
| 6 | **`CombatService.cs:407` 공격 방향** | `SpriteFacing.Next(…, FloorToWorld(direction, 1f), 0.001, 0.25, true)` | `TiltedViewMath.FloorToScreenPlane(direction, sinθ)`(`ViewRulesData`). **W2에서는 옛 4방향 `SpriteFacing.Next`를 그대로 쓴다** → 맞는 시각이 지금과 같다. 8구간은 W4 |
| 7 | **`DamageNumberPresenter.cs:113,118-122`** | `Vector2 foot + Vector2.up·h`, `StartPosition + Vector2.up·rise` | 시작 = `FloorToWorldSnapped(foot) + UprightOffset(h)`(`Vector3`), 상승 = `StartPosition + UprightOffset(rise)` |
| 8 | **`DamageNumberViewFactory.cs:35-40`, `DamageNumberViewRegistry.cs:21,24` (DR-02)** | `Create(int, Vector2)`, `DamageNumberEntry.StartPosition: Vector2` | `Create(int, Vector3)`, `StartPosition: Vector3`, `DamageNumberView.UpdateView(Vector3, float)`. 이 셋을 같이 바꾸지 않으면 7의 `Vector3`가 팩터리 인자에서 `Vector2`로 암시 변환되어 z를 잃는다. 그러면 원점에서 먼 몬스터의 숫자가 다른 바닥 자리에서 뜬다 |
| 9 | **`MonsterHpBarPresenter.cs:235-239`** | 7과 같은 꼴 | 7과 같음, `MonsterHpBarView.UpdateView(Vector3, float)` |
| 10 | `EnemyShotPresenter.cs:40` | `EnemyShotView.UpdateView(Vector2)` | `Vector3`(뿌리 + `UprightOffset(몸 높이)`) |
| 11 | `CombatWorldPresenter.cs:82,114,137,142,160,196` | `World()`가 `Vector2`, `+ Vector2.up·BodyHeight`, 방향 `World(projectile.Direction)` | `World()` → `Vector3`(맞춤), 높이는 `UprightOffset(BodyHeight)`, 각도·길이용 방향은 `FloorToScreenPlane(d·s)`(지금 값과 같음). `ProjectileView.UpdateView(Vector3, Vector2 화면방향)`(`:17,36`의 `(Vector2)center` 빼기 → `Vector3`), `BurstView.UpdateView(Vector3, …)`(`:17,29`), `CombatEffectMotion.Snap` → `SnapToTexels` |
| 12 | `MonsterView.cs:57`(폭발 범위 크기), `:104`(돌진 선) | `FloorToWorld(크기·방향)` → 2D 타원·선 | W2에서는 `FloorToScreenPlane(d·s)`로 지금 모습(누른 타원·화면 각도)을 유지한다. W3에서 눕힌 원·Y축 회전으로 바꾼다(3.7) |
| 13 | `PlayableProbe.cs:195` `CameraError` | 2D 비교 | 3.12 |
| 14 | `PlayableProbe.cs:724-733` `IsProjectileAwayFromParty` | 2D 경계 비교 | 대원 발과 투사체를 `camera.WorldToViewportPoint`로 바꿔 화면 비율 좌표에서 같은 상자(가로 ±24px, 위 52px를 화면 높이 비율로)로 비교 |
| 시험 | `IsoProjectionTests.cs:17,26-27`, `PartyMoveMathTests.cs:27`, `MonsterSpawnTests.cs:104`, `FieldDecorTests.cs:80,87`, `TestRunContext.cs:49`, `DamageNumberMotionTests.cs:68,80,83-84` | `Vector2` 기대 | 3D 왕복 / `FloorToScreenPlane` / `Footprint` / `IsoCellToFloor`. `DamageNumberMotionTests:83-84`의 `Assert.AreEqual(Vector2, …StartPosition)`은 형이 달라 `object` 비교로 실패하므로 `Vector3` 기대로 고친다 |

- W2 완료 조건에 넣는다: 위 표 1~14와 시험 줄을 하나씩 고쳤다는 대조표를 단위 기록(검증 기록)에 남긴다. 그리고 `grep -rn "IsoProjection.FloorToWorld" Assets/Game/02.Scripts`의 모든 결과가 `Vector3` 변수·인자로 들어가는지 확인한다. 암시 변환은 컴파일러가 잡지 못하므로 사람이 대조한다. 바뀌지 않아야 할 값(공격 방향 4구간 결과, 피해 숫자 x·z)은 W2 시험으로 확인한다.

## 4. 렌더 설정 (11-18 확정)

| 무엇 | 파일 | 지금 | 바꿀 값 | 근거 |
|---|---|---|---|---|
| 카메라 렌더러 | `Main.unity:49941` | 1(2D) | 0 | 사전 조사 3.1 |
| 카메라 후처리 / AA / MSAA | `Main.unity:49947-49948` | 끔 / 끔 | **켬** / 끔 / 끔 | D4·D5·D6 |
| 카메라 투영 | `Main.unity:50009-50011` | 정사영 3.75 | 정사영, 크기는 장치가 정함(3.4) | E1·E2 |
| PixelPerfectCamera | `Main.unity:49913-49917` | 있음 | 뺌 | 3.4 |
| PC 렌더러(`PC_Renderer`) | `Assets/Settings/PC_Renderer.asset` | Forward+, SSAO | **SSAO 뺌**. 렌더링 경로(Forward+)는 그대로 두고 기능 목록을 Mobile과 같게: Full Screen Pass(위아래 흐림 M6), Render Objects(`WorldIndicator` 후처리 뒤) | 편집기 화면과 기기 화면이 같아야 캡처 판정이 맞다 |
| Mobile 렌더러(`Mobile_Renderer`) | `Assets/Settings/Mobile_Renderer.asset` | Forward, 기능 없음 | 위 두 기능 추가. 불투명 레이어 마스크에서 `WorldIndicator` 뺌 | D5·D13 |
| SRP Batcher | `PC_RPAsset.asset:73`, `Mobile_RPAsset.asset:73` | 0 | 1 | D8. 시안 2회차 Mobile 300마리 423 그리기·평균 7.2ms가 이 설정에서 잰 값 |
| Render Scale | `Mobile_RPAsset.asset:30` | 0.8 | 1.0 | D4, 정수 배율 |
| 해 그림자 | `*_RPAsset.asset:47,58,59,67` | PC 2048/50/4단계/소프트, Mobile 1024/50/1/하드 | **둘 다 거리 35, 단계 1, 하드.** 해상도 PC 2048, Mobile 1024 | execution-note3(35m), D7 |
| 깊이·불투명 질감 | `*_RPAsset.asset:23-24` | PC 켬, Mobile 끔 | **둘 다 끔** | 위아래 흐림은 화면 UV만 쓴다(Round3 `FieldBandBlur`). 깊이가 필요한 기능이 없다 |
| HDR | `*_RPAsset.asset:27` | 켬 | 켬(R11G11B10) | D6 Bloom 문턱 1.8 |
| 볼륨 | 새 `03.Resources/Settings/HD2D_BattleVolume.asset` + 장면 Global Volume 1개 | — | Bloom(문턱 1.8, 세기 0.4, 퍼짐 0.25, Dual·Quarter), Tonemapping Neutral, Vignette(r6), 색 조정. 위아래 흐림은 볼륨이 아니라 렌더러 기능(M6). 선명 띠 UV 0.16~0.76, 밖은 9탭 약한 흐림 | execution-note3, D5·D6. 값은 6단계 시험 후보 |
| 조명 | 장면 | 없음 | Directional "Sun" 1개(30°/135°), 환경광 Color | r6 출발값 |

**품질 단계(execution-note3 문제)**: 지금 `QualitySettings.asset`에서 Mobile(0)은 `excludedTargetPlatforms: Standalone`(`:61-62`)이다. 그래서 편집기 Play가 늘 PC(1)로 돈다. 1회차 성능이 PC 값이었던 원인이다(관문 G04).

- **결정**: Mobile의 `excludedTargetPlatforms`에서 Standalone을 뺀다. Standalone 기본 품질과 편집기 현재 품질(`m_CurrentQuality`)을 **Mobile(0)**로 둔다. 게임은 모바일 전용이라(CLAUDE.md 개요) 편집기 Play·캡처·판정이 기기와 같은 설정으로 돌게 하려는 것이다. 시안이 한 "메모리에서만 잠깐 바꾸고 되돌리기"는 본 게임에서는 쓰지 않는다.
- PC(1)는 지우지 않는다. 설정 값을 Mobile과 같게 맞춰 남긴다(누가 실수로 골라도 화면이 같게). 성능 측정 도구는 표본마다 품질 이름과 활성 파이프라인 자산을 기록한다(G04, 시안 코드에 이미 있음).
- 작업 폴더의 `QualitySettings.asset`에는 아직 커밋하지 않은 변경이 있다. 에디터 판을 올리면서 생긴 칸(`serializedVersion 5`, `meshLodThreshold`, `Nintendo Switch 2`)과 줄바꿈뿐이고, 시안이 만든 변경이 아니다(초안 3.8 디렉터 확인). W0에서 **이 변경만 먼저 커밋**한다. 메시지는 "에디터 판 올림으로 생긴 칸". 그다음에 품질 결정을 따로 커밋한다. 두 변경이 섞이지 않고, 되돌리기도 커밋 하나로 된다. 초안은 사용자에게 물으려 했지만, 위임 결정으로 바꿨다.

**재질 수와 배칭**: 4단계 재질은 6개다(3.8 M1~M6). 몬스터 400마리는 모두 M1 하나이고 그림은 아틀라스 `Characters.spriteatlasv2` 한 장이라 SRP Batcher에서 set-pass 하나로 묶인다. 재질 속성 블록은 그림자 전용 메시(대원 4개)에만 쓴다.

## 5. 4단계 구현 작업 나누기 (11-18 확정)

- 순서대로 하나씩 한다. 에디터가 하나라 나란히 하지 않는다(CLAUDE.md "기능은 하나씩").
- 구현은 Codex 실행기(`codex-step.py --model gpt-6.1-sol --effort medium`)가 한다. 단위마다 커밋 하나다.
- 확인은 **그 단위에서 바뀐 범위만** 한다(실행 계획 3절). 판정 경로 전체와 EditMode 전체는 단계 마감 W7에서 한 번만 돈다.
- "짧은 Play"는 1~2분이다. 시작 → 전투 → 이동 → 멈춤 → 콘솔 확인 → 캡처 1장(art-core-rules 1절). 캡처는 디렉터가 직접 연다.

| 단위 | 하는 일 | 바꾸는 파일(수) | 완료 조건 | 그 단위에서 돌릴 확인 | 깨질 수 있는 지금 EditMode 시험 |
|---|---|---|---|---|---|
| **W0 렌더 설정** | 4절 표: RP 자산 2, 렌더러 2(기능 추가·SSAO 뺌), 품질(앞 커밋 분리), 볼륨 프로필, 셰이더 5·재질 6 복사·정리(3회차 끝난 뒤). 카메라는 아직 2D 렌더러(1)라 게임 화면은 그대로다 | `PC_RPAsset`, `Mobile_RPAsset`, `PC_Renderer`, `Mobile_Renderer`, `QualitySettings.asset`, 새 볼륨 1, 새 셰이더 5, 새 재질 6 (약 17) | 컴파일 0. 편집기 Play에서 활성 품질 = Mobile, Render Scale 1, SRP Batcher 켬. 2D 화면이 그대로임 | 컴파일. 짧은 Play 1회 + `QualitySettings.names[GetQualityLevel()]`·`GraphicsSettings.currentRenderPipeline` 출력. 캡처 1장(2D 그대로) | 없음(코드 안 바뀜). `ArtImportSettingsTests`는 셰이더·재질 폴더가 대상이 아님을 확인 |
| **W1 순수 계산** | `HD2D.View` 어셈블리, `TiltedViewMath`, `PixelScaleMath`, `ViewRulesData` + `view_rules.json`, `SpriteFacing` 8방향(`Next`, `ResolveAvailable`, `Label`) — **이 단위에서는 아직 연결하지 않는다.** 단 `SpriteFacing`을 다시 쓰면 지금 호출 3곳(`WorldPresenter.cs:333,420`, `CombatService.cs:407`)과 `MemberView`의 `IsBack`이 컴파일에서 깨진다. 그래서 **`SpriteFacing8`을 새 이름으로 만들고**, W4에서 옛 `SpriteFacing`을 지우며 이름을 옮긴다(`mv` + `replace_all`) | 새 asmdef 1, 새 .cs 4, 새 json 1, `Game.asmdef`, 시험 asmdef, 새 시험 3 (약 11) | 컴파일 0. 7절 사례 전부 통과 | 컴파일. `-testFilter "TiltedViewMathTests;PixelScaleMathTests;SpriteFacing8Tests"`. Play 없음(화면 안 바뀜). **algorithm-tester**는 W7 묶음에서 | 없음 |
| **W2 좌표 형·카메라·모든 소비처·대원·몬스터·바닥** | `IsoProjection` 3D와 **3.13 소비처 1~14 전부**(공격 방향은 `FloorToScreenPlane` + 옛 4방향 유지, 피해 숫자 Presenter·Factory·Registry·View `Vector3`, HP 막대·적 탄·효과 위치 `Vector3` + `UprightOffset`), `PartyMoveMath` 각도, `TiltedCameraRig`(+텍셀 맞춤, 정수 배율), `WorldPresenter`(장치, 범위, 지난 바닥 좌표, 바닥 눕힘, 장식), `MemberView`·`MonsterView`·`FootShadowView`·`FieldView` 세움/눕힘, `HitReactionPresenter`(장치로 흔들림), `CameraShakeView`·`ShakeMath.ResolveBase` 지움, DI, `PlayableProbe.CameraError`(3.12)·`IsProjectileAwayFromParty`, **`Game.Editor.asmdef`에 `HD2D.View` 참조(DR-03)**, 이행 메뉴(장면 카메라 렌더러 0·후처리·PPC 뺌·해·볼륨·Grid 회전·대원 세운 자식·재질 M1/M2/M4), 프리팹 `MonsterView` | 약 30 (.cs 24, asmdef 1, 장면 1, 프리팹 1, 이행 메뉴 1, 시험 수정) | 컴파일 0. 3.13 대조표 완료(암시 변환 0). 대원·몬스터·바닥이 3D 렌더러에서 지금과 같은 구도. 이동·흔들림 정상. 1080줄 n=2, 인물 96px. 효과·숫자·HP 막대는 **위치가 맞는 자리**(fix-r002: 바닥 앵커 = 발 위치, 표시 위치 = 앵커 + `UprightOffset(높이)` — 7절 N1과 같은 식)에 있고, 세운 자식·재질·늘 위 그리기는 W3에서 한다(판이 아직 세로 평면이라 화면에서 0.87배로 눌려 보여도 됨). 공격 방향·맞는 시각은 지금과 같음 | 컴파일. `-testFilter "IsoProjectionTests;PartyMoveMathTests;PartyMoveServiceTests;MonsterSpawnTests;MonsterRecycleTests;FieldDecorTests;MemberViewSpriteTests;MonsterViewStateTests;ShakeMathTests;GroundTilePickerTests;TiltedCameraRigTests;DamageNumberMotionTests;CombatEffectTests;CombatDeterminismTests"` + 새 사례 C1~C5·N1. 판정 `Tools/playable/f2-movement-flows.json`(CameraError·이동) 1회. **`playable.py --until 5`(G1~G5) 1회**(보이는 범위 변화만의 영향, 6절 위험 1). 짧은 Play 캡처 1장(1920×1080) + 카메라 이동 연속 캡처 8장(떨림) | `IsoProjectionTests`(2D 왕복·세로 0.5 → 3D 왕복, F1 회귀), `PartyMoveMathTests:27`(`FloorToScreenPlane`), `TestRunContext.cs:49`(→ `Footprint`, 값 같음), `MonsterSpawnTests:97-110`(같은 기대, 계산 경로만), `FieldDecorTests:72-80,87,100-`(`CellToWorld` → `IsoCellToFloor`, 정렬 순서 단언 → 세운/눕힌 단언), `MemberViewSpriteTests:60,69,93-99,202-205`(그림자 Y 내림·정렬 단언), `MonsterViewStateTests:86,91`(폭발 타원 → `FloorToScreenPlane` 값), `ShakeMathTests:73`(`ResolveBase` 시험 삭제), `DamageNumberMotionTests:68,80,83-84`(`Vector3`), `CombatEffectTests`(투사체·발동 위치 기대가 `Vector3`로) |
| **W3 효과·표시 시각 구조·그림자·후처리** | 위치 계약은 W2에서 끝났다. 여기서는 시각 구조만 한다: 투사체·적 탄·발동 그림·숫자·HP 막대를 세운 자식으로, 폭발 범위·돌진 선·치유 범위·고리를 눕힌 판으로(3.7), 재질 M2·M5, `WorldIndicator` 레이어와 Render Objects(후처리 뒤), `MemberView` HP 막대 레이어, `UprightShadowCaster`(대원 4), 프리팹 5개, `art-insert-flows.json` PERF → 기록 전용 `PERFREC`(5절 PERF 항목, `PlayableProbe.WritePerfRecord` 추가) | 약 16 | 컴파일 0. 효과·숫자·HP 막대가 세운 판으로 눌림 없이 대원·몬스터 머리 위 같은 자리. 숫자·막대가 흐림 띠와 앞 몬스터에 가리지 않음. 대원 그림자가 오른쪽 아래로 나옴. PERF가 판정 목록에 없고 `perf-wave6.json`의 `verdict`가 `NOT_JUDGED` | 컴파일. `-testFilter "CombatEffectTests;DamageNumberMotionTests;MonsterHpBarRuleTests;HpLossTrailTests;UiArtLayoutTests;MonsterViewStateTests"`(N1 다시: **z ≠ 0인 몬스터에서 숫자 생성·상승·풀 재사용**, 세운 자식으로 바뀐 뒤에도 x·z 유지, DR-02). 판정 `Tools/playable/f6-feedback-flows.json`(피해 숫자·흔들림)과 `art-insert-flows.json` 1회(FX1~FXHIT2, `PERFREC`). 짧은 Play 캡처 1장 + 그림자 켬/끔 한 쌍(관문 G18 형식) + **같은 조건 비교(DR-F02)**: 같은 시드·시간 멈춘 같은 프레임에서 흐림 띠 안 숫자·HP 막대, 앞 몬스터와 겹친 막대, 바닥에 떨어진 대원 그림자 — 대안(오버레이 카메라, 바닥 메시)으로 바꾸면 같은 조건으로 다시 찍는다. 웨이브 10 버닝 표본 1회(6절 위험 3) | `CombatEffectTests`(세운 자식 안 로컬 기대), `DamageNumberMotionTests`(오르기가 로컬 y로 옮겨지면 기대 바뀜 — 확인), `UiArtLayoutTests`(숫자 자리 계산은 그대로), `MonsterViewStateTests`(눕힌 폭발 범위) |
| **W4 8방향 연결** | 옛 `SpriteFacing` 지우고 `SpriteFacing8` → `SpriteFacing`으로 옮김. `WorldPresenter`(대원·몬스터 8방향), `CombatService:407`, `MemberAttackMotion`, `MemberMotionData.HitSeconds(FacingDirection)`, `MemberView`(5칸 그림·클립 표), `MonsterView`(`Facing`), `SpriteAtlasService`(접미어·별칭 표), `PlayableProbe`(`MemberFacing`, `FacingFallbackCount`), `Tools/playable/art-insert-flows.json`(DR→`down-right`, DL→`down-left`, UL→`up-left`, UR→`up-right`, R·STOP→`right`, B1→`down-right`) | 약 12 | 컴파일 0. 대각 4입력·가로 입력에서 위 표기. 지금 그림으로 대신 쓰기가 맞음(대원 2장, 몬스터 1장) | 컴파일. `-testFilter "SpriteFacingTests;MemberAttackMotionTests;MemberViewSpriteTests;MonsterViewStateTests;SpriteAtlasServiceTests;CombatDeterminismTests"`. 판정 `art-insert-flows.json` 1회(B1·DR~STOP·MON). **공격 맞는 시각 변경(3.9 (가)·(나)) 재측정(DR-05): `playable.py --until 7`(G1~G7) 1회 + `wave_targets.py --metrics <그 기록>`**. 새 사례 D7(지난 방향 포함). 짧은 Play 연속 캡처 8장(대각 이동 중 방향 튐 없음) | `SpriteFacingTests`(전체 다시 씀), `MemberAttackMotionTests:18`(`IsBack` → 방향), `MemberViewSpriteTests`(앞·뒤 단언 → 방향), `SpriteAtlasServiceTests:77`(클립 4개 → 별칭 표), `CombatDeterminismTests`(같은 입력 같은 결과라 통과해야 함. 실패하면 맞는 시각 변화가 원인인지 먼저 본다) |
| **W5 몬스터 경계(I4)** | `spawn_rules.json` `fieldEdgeMargin` 0, `MonsterSpawnService.Spawn`, `MonsterRecycleService`(재활용 위치), `MonsterBehaviorService.Tick`(행동 뒤 자르기), 데이터 클래스 칸 | 5 | 몬스터 위치가 늘 `L1 ≤ 반대각선`. 가운데 원정대 생성은 그대로 | 컴파일. `-testFilter "MonsterSpawnTests;MonsterRecycleTests;MonsterBehaviorTests;CombatDeterminismTests;CombatWaveTests"` + 새 사례 B1~B5(7절, B5 돌진 경계 DR-F01). 판정 경로 `playable.py --until 5`(G1~G5, 서 있는 원정대 패배 웨이브 1~4). `python3 -B ./tools/wave_targets.py`(데이터 행) | `MonsterSpawnTests`(가장자리 원정대 사례가 있으면 위치 기대 바뀜), `MonsterRecycleTests:102`(재활용 위치), `MonsterBehaviorTests`(투사체 뒷걸음이 경계를 넘는 사례가 있으면), `CombatWaveTests`(웨이브 결과 수치를 단언하면) |
| **W6 화면비·안전 영역** | 코드 변경 없음이 목표다. 4화면비에서 n·size가 표(3.4)와 같은지, 노치 안전 영역 안 HUD·조이스틱이 3D 화면과 겹쳐 읽히는지 확인한다. 어긋나면 장치만 고친다 | 0~2 | 4화면비 캡처에서 인물 키 = 48n px(±1), 도트 폭 고름(관문 G08) | 컴파일(바뀐 경우). `playable.py`의 기능 파일 하나로 4화면비 캡처(`"capture": "all"`) — 전투 한 장면. `gate.py check`의 G06·G08만 | 없음 |
| **W7 단계 마감** | 고치지 않는다. 마감 검사와 리뷰만 한다 | 0 | 1절 완료 조건 전부 | **판정 경로 전체 1회(G1~G8)**, **EditMode 전체 1회**, `wave_targets.py --metrics <기록> --require M5,M7`(42행), 4화면비 캡처, 디렉터가 직접 엶. 마감 리뷰 묶음 1회(병렬): validator-client, spec-reviewer, algorithm-tester(`TiltedViewMath`, `PixelScaleMath`, `SpriteFacing.Next`·`ResolveAvailable`, `ClampToDiamond` 쓰는 자리 3곳, `PartyMoveMath`). FAIL만 수정 확인 1회 | — |

- 규칙 시험(가방·전투·웨이브·부활 등)은 고치지 않고 그대로 통과해야 한다. 예외: W4의 맞는 시각, W5의 경계 사례. 이 두 곳의 기대가 바뀌면 바뀐 사례와 이유를 커밋 메시지와 검증 기록에 적는다.
- **PERF 단계(DR-06, 디렉터 결정 11-18)**: `art-insert-flows.json` PERF(`Tools/playable/art-insert-flows.json:118-124`, 기대식 `RenderBatchesP95 >= 0 && RenderBatchesP95 <= 50`)는 2D 예산이다. 3D에서는 시안 기준 300마리에 423 그리기라 넘는다.
  - **`<= 50`을 `>= 0`으로 바꾸지 않는다.** 늘 통과하는 조건은 쓰지 않는다(AGENTS.md "판정 기준은 낮추지 않는다").
  - 2D 예산(≤ 50)을 언제까지 적용할지와 HD-2D 새 예산은 아트 기준서 r6의 7단계 성능 절에서 정한다. 그 전까지 PERF는 **미판정(NOT_JUDGED)**으로 기록만 하고, 기능 판정 결과와 따로 보고한다.
  - 도구 확인: `tools/playable.py`가 받는 단계 키는 `id, name, do, key, tap, sleep, wait, timeout, expect, capture, allow_errors`뿐이다(`playable.py:56`). 단계 결과도 `"pass" if ok else "fail"` 둘뿐이다(`:372`). 그래서 **미판정 상태를 지원하지 않고**, `expect`가 없는 단계도 `pass`로 적힌다.
  - 방법(W3): PERF 단계를 `art-insert-flows.json`의 판정 목록에서 **뺀다**. 같은 위치(웨이브 6, 1배 시간)에 기록 전용 단계 `PERFREC`를 둔다. (fix-r001: `playable.py`는 단계 하나에 `do` 하나를 대기 전에 실행하므로 두 단계로 나눈다) 첫 단계 `PERFREC-START`는 `do: PlayableProbe.MarkFrameStats();`, `sleep: 3`. 다음 단계 `PERFREC-WRITE`는 `do: PlayableProbe.WritePerfRecord("Logs/playable/perf-wave6.json");`(새 진입점: p95 그리기·set-pass·프레임 ms, 살아 있는 적 수, 품질·파이프라인 이름, `"verdict": "NOT_JUDGED"`, `"budget_source": "r6 7단계 미정"`)를 부르고 **`expect`를 두지 않는다**. 단계 이름에 "기록 전용 — 판정 아님"을 적는다.
  - 보고: playable 보고서의 `PERFREC-START: pass`·`PERFREC-WRITE: pass`는 "기록이 실행됨"이라는 뜻일 뿐이다. 성능 결과는 `perf-wave6.json`의 `verdict`로만 따로 적는다. 디렉터의 판정 요약 표에 "성능: NOT_JUDGED(값 …)" 줄을 따로 둔다.
  - r6가 예산을 정하면 그 값으로 PERF 판정 단계를 되살리고 `PERFREC`를 지운다(7단계 일). 옮긴 사실과 이유는 기능 판정 파일 머리 주석과 개선 기록에 적는다.
- 파일 수는 추정이다(시험 파일 포함, 메타 제외).

## 6. 위험과 되돌리기 (11-18 확정)

| # | 위험 | 깨질 수 있는 것 | 대응 | 되돌리기 |
|---|---|---|---|---|
| 1 | **보이는 범위가 1.5배**가 된다(1080줄 기준, 3.4 표). 생성 거리 10.8 → 15.8m, 이동 한계 17.8 → 10.75m. 몬스터가 늦게 닿고 화면 안 밀도가 약 0.44배가 된다(같은 수 ÷ 2.25배 넓이) | 판정 G5(가만히 선 원정대가 웨이브 1~4에 짐), G7(봇 10웨이브 승리·435±2초), 웨이브 목표 M5 | W2 직후 `playable.py --until 5`를 먼저 돌려 G5를 본다. 깨지면 **데이터로** 맞춘다(규칙 코드는 안 바꿈). 후보는 `spawnMargin`을 줄이는 것(하한 0, 생성은 화면 밖 유지), 몬스터 `speed` 일괄 배율이다. 어느 것을 쓸지는 디렉터 결정이고, 개선 기록에 적는다. (13절 디렉터 결정: 대안 B와 아래 "되돌리기"의 `targetFraction` 13.3%는 **쓰지 않는다** — 정수 배율 결정과 부딪힘. 기록으로만 남긴다) 그래도 안 되면 대안 B: 규칙 범위를 기준 화면(1920×1080) 값으로 고정하고, 더 넓은 화면은 테까지 보이게 한다(테 4m ≥ 넓은 화면 초과분 3.2m). 이때 들판 경계 생성이 화면 안에 보일 수 있다 | `view_rules.json` `targetFraction`을 13.3%로 두면(n=3, size 3.75) 지금 범위로 돌아간다. 코드는 그대로다 |
| 2 | 화면 높이마다 n이 달라 이동 한계가 6.6~14.2m로 흩어진다. 지금 2D는 화면비로만 15.6~17.8m였다 | 기기 사이 공정성, 테가 안 보이는 전제 | 판정은 1920×1080 하나로 한다(route `size`). 기기 표는 문서로 남기고 7단계 실기기에서 다시 본다 | 같음 |
| 3 | 3D + 후처리로 편집기 프레임이 늘어난다 | G7의 `BurningFrameMsP`(M7 ≤ 16.7ms)가 판정식에 들어 있으면 FAIL | W7 전에 W3 끝에서 웨이브 10 버닝 표본 하나를 잰다. 넘으면 7단계 일을 앞당긴다(그림자 해상도, 볼륨 끔 비교) | 볼륨·Full Screen Pass 끄기는 설정 하나다 |
| 4 | 공격 방향 8구간이 맞는 시각을 바꾼다(3.9 (가)·(나), 실제 시간 차는 bruno 0.1초) | `CombatDeterminismTests`, G5, G7 시간 435±2, 웨이브 목표 | 결정론은 유지된다. **W4 뒤에 G5·G7(`playable.py --until 7`)과 `wave_targets.py`를 다시 잰다(DR-05).** 시간이 바뀌면 원인이 맞는 시각인지 먼저 가린다. 방향별 `hitFront/hitBack` 대응은 데이터로 고칠 수 있다 | `HitSeconds`가 옛 축 비율 판정으로 돌아가는 함수 하나 |
| 5 | 몬스터 경계 자르기(I4)가 가장자리 전투를 어렵게 한다 | G7, M5 | 위 1과 함께 잰다. `fieldEdgeMargin`은 데이터다 | 자르기 세 줄을 지운다 |
| 6 | 그림자 전용 메시·번쩍임 띄운 판·월드 표시 후처리 뒤 그리기가 3D 렌더러에서 생각대로 안 될 수 있다(Render Objects를 AfterRenderingPostProcessing에 거는 것은 추측) | 피해 숫자·HP 막대가 흐림 띠에서 뭉개짐 | W3 캡처로 본다. 안 되면 오버레이 카메라(후처리 끔, `WorldIndicator`만) 한 대로 바꾼다. 비용은 카메라 1대. **(DR-F02) 대안으로 바꿔도 같은 캡처 조건으로 다시 본다**: 같은 시드·같은 프레임(시간 멈춤)에서 흐림 띠 안의 숫자·HP 막대, 앞 몬스터와 겹친 막대, 그리기 수 | 레이어 마스크 원복 |
| 7 | 눕힌 타일맵이 M1(Lit)로 빛·그림자를 받는지(`TilemapRenderer` + 손으로 쓴 셰이더) | 바닥에 대원 그림자가 안 보임 | W2 캡처로 본다. 안 되면 바닥을 시안 3회차 바닥 메시(5단계 것을 앞당김)로 바꾸고, `GroundTileCount` 판정은 타일맵 데이터를 렌더러 없이 남겨 유지한다. **(DR-F02) 이때 타일 수 판정은 바닥 렌더링 성공의 근거가 아니다.** 바닥 그림자 수신은 같은 캡처 조건(같은 시드·위치, 해 켬/끔 한 쌍)에서 대원 그림자가 바닥에 보이는지로 따로 본다 | — |
| 8 | 장면 이행이 손작업이면 되풀이가 안 된다 | 되돌린 뒤 다시 하기 | 이행 메뉴 하나로 묶고, 두 번 돌려도 같은 결과가 나오게 한다(이미 바뀐 것은 건너뜀). 이행 전 장면·프리팹은 커밋되어 있어 `git checkout <파일>`로 되돌린다(디렉터만) | 커밋 단위 |
| 9 | 사람이 에디터로 플레이하는 중에 명령을 보냄 | 테스트 실행기 오류 | `test-before-claim.md` "사람이 플레이하는 에디터" 절을 따른다. 명령 전에 `Application.isPlaying`을 확인한다 | — |
| 10 | 3회차 시안이 같은 셰이더 파일을 계속 바꾼다 | W0 복사본이 낡음 | W0는 3회차 관문 PASS 뒤에 한다(실행 계획 2절 순서 그대로). 복사본 머리에 원본 경로·해시를 적는다 | — |

- **판정 경로와 자동 판정 8단계(G1~G8)를 지키는 법**
  - `route.json`의 기대식은 카메라·방향·타일을 직접 보지 않는다. 그래서 진입점 이름(`PlayableProbe`의 공개 멤버)을 지우거나 이름을 바꾸지 않는다.
  - 바뀌는 진입점 `CameraError`·`MemberFacing`·`GroundTileCount`·투사체 판정은 같은 이름과 같은 형으로 남긴다.
  - 기능 판정 파일(`f2-movement`, `f6-feedback`, `art-insert`)은 단위마다 그 파일만 돌린다. 전체 경로는 W7에서 한 번 돈다.

## 7. 새 EditMode 시험 사례 (순수 함수, algorithm-tester 대상)

| # | 대상 | 입력 | 기대 |
|---|---|---|---|
| F1 | `Footprint` 회귀 | 30°, size 3.75, aspect 16/9, s 0.0113 | 바닥 단위 반 크기 = 지금 `IsoProjection.WorldToFloor((6.667, 3.75))`과 1e-3 안에서 같음 |
| F2 | `Footprint` | 30°, size 5.625, 16:9 | 반폭 10.0m, 반깊이 11.25m |
| F3 | `Footprint` 화면비 | 21:9, 4:3 | 반폭만 바뀜(13.125, 7.5) |
| S1 | `PixelScaleMath` | H = 720, 1080, 1170, 1284, 1440, 1536, 2048 / 0.1 / 48 | n = 2, 2, 2, 3, 3, 3, 4. size = H ÷ (96n) |
| S2 | 배율 하한 | H = 200 | n = 1(하한), 경고 로그 없음(정상 흐름) |
| T1 | `SnapToTexels` | (0.013, 0, 0.03), 30° | (1/48, 0, 1/24) — 높이 0 유지 |
| T2 | 맞춘 점의 화면 좌표 | T1 결과를 카메라 오른쪽·위 축에 투영 | 둘 다 1/48의 정수배(1e-5) |
| P1 | 입력 보정 | 30°, (1,1) | (1,2)/√5 — 지금과 같음 |
| P2 | 입력 보정 | 30°, (0,0) | (0,0) |
| D1 | 8구간 | 각 0, −45, −90, 45, 90, 180, −135, 135(지난 = Front, hold 0) | Right, DownRight, Down, UpRight, Up, Right+반전, DownRight+반전, UpRight+반전 |
| D2 | 경계 넘김 | 지난 = Right, 각 −30°(경계 −22.5 + 7.5), hold 12 | Right 유지. −35°면 DownRight |
| D3 | 짧은 움직임 | 이동량 < minMove | 지난 값 그대로 |
| D4 | 위·아래 반전 유지 | 지난 = DownRight+반전, 각 90° | Up, 반전 true 유지 |
| D5 | 대신 쓰기 | 있는 것 {DownRight, UpRight}에서 Down, Right, Up / 있는 것 {DownRight} | DownRight, DownRight, UpRight / 모두 DownRight |
| D6 | 화면 평면 | 바닥 (0,1), 30° | 각 90°, 길이 0.5 |
| D7 | 공격 방향 회귀(DR-05: **지난 방향 포함**) | 지난 방향 {앞, 뒤} × 대상 화면 각 {0°, 10°, 18°, 30°, −10°, −30°, 90°, 170°, 180°} | 옛 `SpriteFacing.Next(지난, 방향, 0.001, 0.25, true).IsBack`과 새 `HitSeconds` 쪽(UpRight·Up = 뒤)을 나란히 적는다. 다른 칸은 (가) 지난 뒤 & |a|<14°(0°, 10°, −10°, 180°, 170°)와 (나) 18°뿐이어야 한다. 대표 예: 지난 뒤, 입력 (1,0) → 옛 hitBack, 새 hitFront |
| G1 | `IsoCellToFloor` | 칸 (1,0), (0,1), (5,−3) | 지금 `CellToWorld` + `WorldToFloor` 결과와 같음 |
| B1 | 생성 자르기 | 원정대 중심 (2400, 0) 바닥 단위, 각 0 | 위치 L1 ≤ 반대각선, x = 반대각선 |
| B2 | 생성 그대로 | 중심 (0,0), 보이는 범위 1920×1080 | 자르지 않음(지금 거리와 같음) |
| B3 | 이동 자르기 | 투사체 몬스터가 경계 밖으로 뒷걸음 | 경계 위에서 멈춤, 미끄러짐 |
| B4 | 재활용 자르기 | 중심 이동 한계 근처, 재활용 위치가 밖 | 경계 위 |
| B5 | 돌진 경계(DR-F01) | 돌진 몬스터가 경계 근처에서 바깥으로 돌진(선분 일부가 L1 > 반대각선), 대원 4명은 실제 배치 | 피해 대상·횟수는 자르기 전 선분 기준 그대로, 최종 위치는 경계 위. 근거: 피해 판정(`ChargeBehavior.cs:73-82`)은 자르기 전 선분으로 하고, 자르기는 그 뒤 `MonsterBehaviorService.cs:55` 다음에 한다. 대원은 원정대 중심(L1 ≤ 32 − 반폭 − 반깊이, 1920×1080에서 10.75) + 진형 자리에 있어서 경계 밖 선분 구간과의 거리가 `dashHitRadius`보다 훨씬 크다. 그래서 경계 밖 구간이 대원을 맞히는 일은 지원 화면비에서 생길 수 없다고 본다(추측: 진형 자리 최대 거리는 W5에서 데이터로 확인). 이 제약을 시험 이름·주석에 적는다 |
| C1 | `CameraError` 따라감 | 바라보는 점 → 흔들림 순서로 같은 프레임 입력 | transform에서 복원한 초점이 원정대 중심과 텍셀 맞춤 오차 이내(≤ 0.0147m) |
| C2 | 입력 순서 반대 | 흔들림 → 바라보는 점 | C1과 같은 결과 |
| C3 | 흔들림 끝 | 흔들림 오프셋 0으로 돌아옴 | 카메라 = 맞춘 초점 − forward·거리, 오차 ≤ 0.0147m |
| C4 | 일시정지 | 흔들림 오프셋이 남은 채 바라보는 점만 바뀜 | 복원 초점이 새 중심을 따라감 |
| C5 | transform 미갱신 검출 | 장치 입력은 받았지만 transform을 바꾸지 않은 가짜 상황(시험용 덮어쓰기) | `CameraError` > 0.021 → 실패로 잡힘 |
| N1 | 피해 숫자 z(DR-02) | 몬스터 바닥 (1000, 1500)에서 숫자 생성 → 0.3초 상승 → 반납 → 다른 바닥 (−800, −400)에서 재사용 | (fix-r001) 바닥 앵커와 표시 위치를 나눠 본다: **앵커**(`DamageNumberEntry`의 바닥 기준점)는 그 몬스터 발 위치(`FloorToWorldSnapped`)와 같다. **표시 위치**는 매 프레임 `anchor + UprightOffset(headHeight + rise)`와 같다(30°에서 z도 h·sinθ만큼 변하는 것이 맞다). W3에서 숫자를 세운 부모의 로컬 y로 옮겨도 최종 월드 위치가 이 식과 같다. 풀 재사용 때 앵커가 새 몬스터 발 위치로 바뀌고 이전 값이 남지 않음 |

- 장면 확인(구현 뒤)은 5절 단위별 확인과 W7이다.

## 8. 실패·전환 경계

| 경계 | 처리 |
|---|---|
| 판 시작·풀에서 꺼냄 | 방향 = Front(DownRight). 지난 바닥 좌표 = 지금 좌표로 둔다(첫 프레임의 자리 점프를 이동으로 보지 않음, 지금 `WorldPresenter.cs:268-277,388-389`과 같음) |
| 쓰러짐·부활 | 쓰러진 그림은 1장 + 반전. 부활 때는 지난 방향 |
| 화면 크기·화면비 바뀜 | 장치가 n·size를 다시 정한다. `Footprint`를 다시 계산하고 바뀔 때만 알린다 |
| 일시정지 | 방향·오프셋 그대로. 장치의 바라보는 점은 계속 따라간다 |
| 그림 없음 | 대신 쓰기. 한 방향도 없으면 지금 F1 임시 도형(`WorldPresenter.cs:197-216`) |
| 이동 한계 ≤ 0(아주 넓은 화면) | 지금처럼 경고 로그 + 원정대 가운데 고정(`PartyMoveService.cs:31-34`) |

## 9. 결정 기록 (초안 10절의 R1~R11, 11-18 확정)

| # | 결정 | 정한 값 | 근거 |
|---|---|---|---|
| R1 | 카메라 방식·각도·인물 키 | 정사영 30°, 정수 배율(1080줄 2배) | 사용자 시안 F + 디렉터 위임(F-int) |
| R2 | 원근 바라보는 점·P1/P2 | 해당 없음(지움) | R1 |
| R3 | 흔들림 | 장치가 오프셋을 따로 받음. `ResolveBase` 지움 | 순서 의존 제거(3.2) |
| R4 | 픽셀 맞춤 | 맞춤: 바닥 점을 x 1/48, z 1/(48 sinθ) 격자에 | 지금 PPC 픽셀 맞춤과 같은 결과(3.3) |
| R5 | 4단계 임시 바닥 | G1: 타일맵 눕히기 | 판정·시험 유지, 화면 같음. 위험 7의 대안 있음 |
| R6 | 번쩍임 | 띄운 판(1/48m) + 흰 재질 | 속성 블록 없음, 시험 유지 |
| R7 | HP 막대·피해 숫자 | 늘 위, 후처리 뒤 | 3.10 |
| R8 | 그림자 | 대원 4명 그림자 전용 메시 + 발 그림자 판 유지 | 3.8, D7 |
| R9 | 몬스터 공격 중 방향, 경계 넘김 | 몬스터는 이동 방향만. 경계 넘김 12° | 3.9 |
| R10 | 다시 쓸 코드 자리 | 새 어셈블리 `HD2D.View`(`Effect/HD2DView/`) | 3.1 |
| R11 | 땅 질감 도트 크기 | 4단계는 지금 타일 그대로. 5단계 바닥은 가로 48·깊이 24 텍셀/m(execution-note3) | r6 K6로 넘김 |
| R12(새) | I4 몬스터 경계 | 생성·재활용·이동을 들판 마름모로 자름, 여유 0 | 3.6 |
| R13(새) | 품질 단계 | Mobile을 Standalone·편집기 기본으로 | 4절 |
| R14(새) | `ViewProjection` 서비스 | 만들지 않음. static Utils + `ViewRulesData` | 3.11 |
| R15(새, DR-06 디렉터 결정) | 2D 성능 예산 PERF ≤ 50 | 조건을 느슨하게 하지 않는다. r6 7단계 예산 전까지 판정 목록에서 빼고 기록 전용 `PERFREC`(NOT_JUDGED)로 옮김 | 5절 PERF 항목 |
| R16(새, DR-01) | 좌표 형 바꾸기와 소비처 | 같은 단위(W2)에서 모든 `FloorToWorld` 소비처를 `Vector3`로 옮기고, 화면 방향 계산은 `FloorToScreenPlane`으로 바꿔 지금 값을 유지한다 | 3.13 |

## 10. 추측으로 남긴 것 (구현 단위에서 확인)

- 추측: Render Objects 기능을 `AfterRenderingPostProcessing`에 걸어 `WorldIndicator` 레이어를 후처리 뒤에 그릴 수 있다(URP 17 Render Graph). 확인 W3, 대안은 위험 6.
- 추측: `TilemapRenderer`에 손으로 쓴 Lit 셰이더(M1)를 주면 빛과 해 그림자를 받는다. 확인 W2, 대안은 위험 7.
- 추측: 보이는 범위 1.5배와 경계 자르기 뒤에도 G5(웨이브 1~4 패배)와 G7(435±2초)이 범위 안이다. 확인 W2 직후·W5, 대안은 위험 1.
- 추측: 3D·후처리를 켠 편집기에서 웨이브 10 버닝 P95 ≤ 16.7ms. 시안 Mobile 300마리 평균 7.2ms(p95 아님)가 근거다. 확인 W3 끝.
- 추측: 노치 쪽으로 치우친 구도(화면 가운데 고정)가 눈에 띄지 않는다. 확인 W6 캡처.
- 추측: 돌진 선분의 경계 밖 구간이 대원을 맞힐 수 없다(B5). 진형 자리 최대 거리를 W5에서 데이터로 확인한다.
- 추측: 기록 전용 단계(`PERFREC`, `expect` 없음)는 `playable.py`에서 `pass`로 적힌다. 이것은 "실행됨"의 뜻으로만 읽는다. 도구 출력의 단계 결과를 성능 판정으로 옮겨 적지 않는다.
- 계산(재 본 값): 3.4 화면 표는 위 식을 python으로 계산한 값이다. 실제 캡처의 인물 키는 W6에서 잰다(관문 G06).

## 11. design-reviewer에 넘길 요약

1. 무엇: 2D 아이소 도트 전투 화면을 URP 3D 렌더러(0번) + 정사영 30° 카메라로 바꾼다. 게임 규칙은 2D 바닥 `Vector2` 그대로이고, 그리는 쪽만 3D다(`IsoProjection.FloorToWorld → (x·s, 0, y·s)`).
2. 상태 소유권 변경: 카메라 위치의 주인이 `WorldPresenter`(+ `HitReactionPresenter` 덮어쓰기)에서 새 `TiltedCameraRig` 하나로 바뀐다. 입력은 바라보는 점, 흔들림 오프셋, 화면 크기 셋이고, 입력마다 즉시 다시 계산해 LateTick 순서에 기대지 않는다. `CameraShakeView`·`ShakeMath.ResolveBase`는 지운다. 판정 `CameraError`는 실제 카메라 transform에서 거리·흔들림을 빼 초점을 복원해 비교한다(3.12).
3. DI: 새 Scoped 데이터 `ViewRulesData`(`Data/View/view_rules.json`)와 장면 컴포넌트 `TiltedCameraRig`를 등록한다. 새 서비스는 없다. 둘 다 아무것도 주입받지 않아 순환이 없다. 규칙 서비스 `CombatService`·`PartyMoveService`가 각도 데이터를 받는다(뷰가 아니라 데이터).
4. 어셈블리: 다시 쓸 계산·장치는 새 `HD2D.View`(Game 비참조)에 둔다. `Game`·`Game.Editor`·시험 asmdef가 참조한다.
4b. 좌표 형 바꾸기: `FloorToWorld → Vector3`와 소비처 14곳(피해 숫자 Factory·Registry 포함) 이행을 W2 한 단위에서 끝낸다. 화면 방향은 `FloorToScreenPlane`으로 지금 값을 유지한다(3.13).
4c. 성능: 2D 예산 PERF ≤ 50은 느슨하게 하지 않는다. r6 예산 전까지 판정 목록에서 빼고 기록 전용 NOT_JUDGED로 따로 보고한다(5절).
5. 렌더 파이프라인: SRP Batcher 켬, Mobile Render Scale 0.8→1.0, 그림자 35m·1단계·하드, SSAO 뺌, Full Screen Pass(위아래 흐림), Render Objects(HP 막대·피해 숫자를 후처리 뒤에, 늘 위), PixelPerfectCamera 뺌. 재질 6개(Lit 알파 클립·반투명·그림자 전용·흰 번쩍임·표시·흐림).
6. 품질: Mobile을 Standalone·편집기 기본으로 바꿔 편집기 측정이 기기 설정과 같게 한다(1회차 PC 측정 사고의 원인 제거).
7. 정수 배율: `n = round(H·0.1/48)`, `size = H/(96n)`, 바닥 점을 텍셀 격자에 맞춘다. 레터박스 없이 남는 공간은 배경이다.
8. 게임 동작이 바뀌는 곳: (a) 보이는 범위 1.5배 → 생성 거리 +5m, 이동 한계 −7m. (b) 몬스터 생성·재활용·이동을 들판 마름모로 자름(I4). (c) 공격 방향 8구간 → 지난 방향이 뒤 & |각|<14°, 또는 각 14°~22.5°에서 맞는 프레임이 뒤→앞으로 바뀜(실제 시간 차는 bruno 0.1초). W4 뒤 G1~G7·웨이브 목표 재측정.
9. 확인하고 싶은 위험:
   - (a) 8의 세 변경이 판정 경로 G5·G7과 웨이브 목표 42행을 깨뜨리는가. 데이터로 되돌리는 경로(6절 위험 1)가 충분한가.
   - (b) 카메라 장치의 "입력마다 즉시 계산"이 같은 프레임에 두 번 쓰일 때 판정 진입점 `CameraError`와 맞는가.
   - (c) `MonsterBehaviorService` 한 곳에서 행동 뒤 자르는 것이 돌진 충돌 판정(`ChargeBehavior.cs:73-82` 선분)과 어긋나지 않는가. 자르기는 판정 뒤에 일어난다.
   - (d) 후처리 뒤 표시 그리기(추측)와 눕힌 타일맵 Lit(추측)의 대안이 적절한가.
   - (e) `SpriteFacing`을 W1에서 `SpriteFacing8`로 따로 만들고 W4에서 이름을 옮기는 순서가 컴파일을 늘 녹색으로 유지하는가.
10. 단위: W0 설정 → W1 순수 계산 → W2 좌표 형·카메라·모든 소비처·대원·몬스터·바닥 → W3 효과·표시 시각 구조·그림자 → W4 8방향 연결 → W5 경계 → W6 화면비 → W7 마감(전체 판정·EditMode·리뷰 묶음 1회).

## 12. 설계 검토 결과

### r001 — VERDICT: FAIL (Codex gpt-6.1-sol, 11-18, `design-review-r001.md`)

필수 수정은 6개 모두 반영했다. 반영 전에 지적마다 코드를 다시 읽어 사실을 확인했다.

| 지적 | 확인한 사실(코드) | 반영 위치 |
|---|---|---|
| DR-01 좌표 형 변경과 소비처 이행 분리 | `CombatService.cs:407`(`FloorToWorld(direction,1f)`가 `Vector2` 인자로 암시 변환되면 세로 성분을 잃음), `DamageNumberPresenter.cs:113,121`·`MonsterHpBarPresenter.cs:238`(`Vector3 + Vector2` 모호). 런타임 호출처 13곳 + 시험 6파일을 grep으로 확인 | 3.13 새 절(소비처 1~14 표, 완료 조건), 5절 W2·W3 행, 9절 R16, 11절 4b |
| DR-02 피해 숫자 위치 계약 | `DamageNumberViewFactory.cs:35` `Create(int, Vector2)`, `DamageNumberViewRegistry.cs:21` `StartPosition: Vector2`, 시험 `DamageNumberMotionTests.cs:68,80,83-84` | 3.13 표 7·8행·시험 행, 5절 W2(계약 이행 + N1), W3(세운 자식 뒤 N1 다시: z≠0 생성·상승·풀 재사용), 7절 N1 |
| DR-03 Editor 어셈블리 참조 | `Game.Editor.asmdef:4-11`에 `HD2D.View` 없음 | 3.1 어셈블리 항목, 5절 W2 행 |
| DR-04 `CameraError` 약화 | `PlayableProbe.cs:189-197`은 실제 transform을 비교함, `f2-movement-flows.json:38,45,53` `≤ 0.021` | 3.2 표 카메라 행, 3.12 새 절(복원 식, 허용치 0.0147), 7절 C1~C5, 5절 W2 확인(`TiltedCameraRigTests`), 11절 2 |
| DR-05 맞는 시각 변경 범위 | `SpriteFacing.cs:27-36`은 축 조건을 못 넘으면 지난 `IsBack` 유지. 데이터 `hitFront≠hitBack`은 bruno(2/3)뿐 | 3.9 "지금과 다른 점" (가)·(나), 7절 D7(지난 방향 포함), 5절 W4 확인(G1~G7 + 웨이브 목표 재측정), 6절 위험 4, 11절 8(c) |
| DR-06 성능 판정 완화(디렉터 결정) | `art-insert-flows.json:118-124` `<= 50`. `playable.py:56` 단계 키에 미판정이 없고 `:372` 결과는 pass/fail뿐 | 5절 PERF 항목(판정 목록에서 빼고 기록 전용 `PERFREC` + `WritePerfRecord` `verdict: NOT_JUDGED`, 늘 통과 조건 금지), 5절 W3 행, 9절 R15, 11절 4c |
| DR-F01(후속) 돌진 경계 | `ChargeBehavior.cs:73-82` 자르기 전 선분으로 피해 판정 | 7절 B5(도달 불가 근거와 확인할 추측), 5절 W5 확인 |
| DR-F02(후속) 렌더 대안 같은 조건 비교 | — | 6절 위험 6·7 대응 칸, 5절 W3 확인 |

- 수정 확인은 같은 검토자에게 한 번 받는다. 결과 파일은 `design-review-fix-r001.md`다.

## 13. 디렉터 결정 (2026-11-18 03:1x)

- 보이는 범위 1.5배(인물 96px의 결과): **기획서 목표값(gdd 판정 G5·G7 등, `wave_targets.py`)을 불변으로 두고, 벗어나면 데이터(`spawnMargin` 먼저, 그다음 몬스터 `speed` 일괄 배율)로 맞춘다.** 대안 B(카메라 크기 바꾸기)는 정수 배율 결정(사용자 위임, 완성 실행 계획 4절)과 부딪혀 쓰지 않는다. W2 직후와 W5에서 잰다.
- W0는 3회차 관문 통과 뒤. `QualitySettings.asset` 미커밋 변경은 W0에서 따로 커밋(에디터 판 올림 칸, 11-18 이전부터 있던 변경).
- (03:3x) 수정 확인 `design-review-fix-r001.md`: VERDICT FAIL — DR-01·03·04·05 해소, DR-02·DR-06 실행 형태 모순 2건(N1 기대 식, PERFREC 단계 순서). 디렉터가 지적한 수정 방향대로 직접 고침(7절 N1, 5절 PERF 두 단계, 6절 위험 1에 대안 B 미사용 표시 = 후속 DR-F03). CLAUDE.md "그래도 남으면 묻지 않고 고쳐서 다시 확인한다"(사용자 11-17)대로 고친 두 곳만 수정 확인 2회차(`design-review-fix-r002.md`)를 받는다. N1·PERFREC는 W2·W3에서 실제 시험으로도 확인한다.
- (03:2x) 수정 확인 2회차 `design-review-fix-r002.md`: VERDICT FAIL — DR-06-FIX 해소, DR-02-FIX는 N1은 고쳐졌고 W2 완료 조건(5절 W2 행)에 같은 모순이 남음 + 후속 DR-F04(PERFREC 단계 이름). 디렉터가 두 곳 고침. 남은 지적이 문장 일치 한 곳뿐이라 다시 받는 대신 grep으로 "발 x·z" 잔재 0을 확인했고, W2 구현 마감 리뷰(validator·spec)와 N1 실제 시험이 최종 확인이다.
- (03:3x) 사용자 "최적화개념인데 … 아트 완료된 후에 적용까지 완료된다으메 마지막에 테스트를해서" → **W0~W6에서는 성능을 재지 않는다**(W3 웨이브 10 버닝 표본, W2·W3의 PERF 관련 확인 모두 뺌). PERFREC-START/WRITE 단계는 W3에 만들어 두되 측정 결과는 7단계에서 한 번만 본다. 성능 판단은 7단계(아트·UI 적용이 끝난 뒤) 한 번.
