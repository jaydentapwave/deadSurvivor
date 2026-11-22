# DQ 본 게임 적용 5단계 — 설계 브리핑

- 작성: 2026-11-23 Codex. 작업 트리 `night-apply`, 브랜치 `claude/pack-expedition-2d-dq-apply`.
- 기준 커밋: `41c14ec8a110d1d62d70248c2a266a62b7d520b3`.
- 규모 L2. **구현 전 design-reviewer 대기**. 이 문서는 설계안이며 실행·컴파일·Play 통과 기록이 아니다.
- 사용자 지시의 실행 환경 3번에 따라 Codex는 리뷰 에이전트를 부르지 않는다. Claude 세션에서 검토하고 필수 지적을 해소한 뒤 구현한다. 이번에 런타임·장면·재질 변경은 없다.
- 입력: [시각 요소 전체 목록](시각-요소-목록.md), [기준서 r2](../../art/아트-기준서.md), 시안 `Assets/Game/01.Scene/Lookdev_DQ.unity`, 사용자 20261123-night-1to6 5단계 지시문.

## 1. 이번에 만들 결과·경계

Main에서 원근 18°·세로 FOV20°로 한 판이 돌고, 게임 바닥 좌표·조이스틱·카메라·스폰·가림·HP/피해 숫자가 같은 투영을 쓴다. 게임 시뮬레이션은 평평한 `Vector2` 바닥 좌표 그대로다. 높이는 시각용 TerrainHeightGrid에서 읽는다. 데이터 계약·세이브·전투 수치·판정 경로 기대식은 바꾸지 않는다.

구현 전에 해결해야 할 핵심은 **시안의 먼 시야가 현재 들판보다 크다**는 것이다(4절). 정사영 반크기 식을 원근의 큰 수로 바꾸는 것만으로 끝내지 않는다. 경계·스폰 계획이 이 조건을 만족할 수 있는지 design-reviewer가 우선 판정해야 한다.

- 배경 모델·질감·색은 **6단계 몫, 이 단계 판정 밖**. 화면 가장자리의 시각용 바닥 연장은 5단계의 빈 곳 방지이고 새 배경 디자인이 아니다.
- 빛·그림자·흐림 값은 **7단계 몫, 이 단계 판정 밖**. 현재 렌더 경로의 원근 대응·월드 표시 순서만 5단계가 한다.
- 새 대원·몬스터 그림·동작은 **8단계 몫, 이 단계 판정 밖**. K50 크기 검사는 56칸 기준 표지/시안과 현재48칸 몸을 구분한다.
- 효과·UI 그림은 **9단계 몫, 이 단계 판정 밖**. 투영·앵커의 잔여30° 상수는 5단계에서 제거해야 한다.
- 성능은 측정하지 않는다. 유료 생성0·패키지 설치0·원래 폴더 변경0·push/PR/merge0.

## 2. 읽은 소스와 확인된 값

### 2.1 시안 A / D1 / E1

`Lookdev_DQ.unity`의 `DQ Camera` GameObject fileID `1740130276`, Transform `1740130280`, Camera `1740130279`를 YAML에서 직접 읽었다. 원시 블록은 `output/hd2d-dq/apply/5/source-audit.json`의 `lookdev_camera`에 있다.

| 값 | 저장된 실제 값 | 근거 |
|---|---|---|
| 투영 | orthographic=0 | Camera 블록 |
| 세로 시야각 | 20° | field of view |
| 회전 | quaternion (0.15643448,0,0,0.98768836) = X18° | Transform 블록; Euler hint0은 실제 회전 아님 |
| 위치 | (0,10.806708,-30.335825)m | Transform 블록 |
| 시안 focus | (0,0.95,0)m | DQLookdevBuilder.Configure(18) |
| 광축 거리 | 약31.89697m(위 위치·focus에서 계산) | 시안 위치 보존, 아래 계산 로그 |
| near/far | 0.1 / 180m | Camera 블록 |
| PPU | 48 | 시안 그림 importer/기준서 K50 |
| 캐릭터 목표 | 기준 키56칸 × 기준 도트2px =112px/1080 ≈10.37% | K50가 K32의 옛48칸·96px를 보완 |

`DQLookdevBuilder.Configure`는 옛 Bruno의 불투명 높이와 `.0889`를 써 거리를 계산한다. `DQCharacterTrial`은 **새 그림을 끼우기 전에** 이 함수를 부른다. 따라서 새56칸으로 같은 `.0889`를 다시 계산하면 시안보다 카메라가 멀어진다. **Main은 저장된 시안 거리·회전·focus 높이를 사용한다.** 화면비마다 거리를 새 키에 맞추지 않는다. aspect는 출력 크기에서 읽는다.

고정된 같은 물리 카메라에서 키 비율은 유지되지만 1536줄에서는 도트가 약2.84px가 된다. 기준서의2px는1080줄의 주역 깊이 기준이다. 전 깊이·모든 해상도에 2px를 강제하려고 카메라를 바꾸거나 원근 축소를 보상하지 않는다. 불투명 높이와 pivot/앞뒤 대원 z 때문에 개별 실제 키에는 차이가 생긴다. 마스크 실측을 별도로 남긴다.

### 2.2 현재 Main

- `Resources/Data/View/view_rules.json`: tilt30°, distance20, near.3, far60, targetFraction.1, minScale1, facingHold12°, terrainDepthPad.907692.
- `TiltedCameraRig`가 카메라 transform 소유. `WorldPresenter`가 focus와 해상도를 전달하고 `HitReactionPresenter`가 흔들림을 보낸다. 입력 호출 순서에 상관없이 즉시 Apply한다.
- `IsoProjection`: floor×unitScale → XZ, y=높이 샘플. snapped 경로는 **정사영 화면 격자**를 가정한다.
- `PartyMoveService`: `ScreenToFloorDirection(input,sinTilt)` 후 게임 이동 속도로 정규화. `SetViewHalfExtent`에서 fieldHalfDiagonal−halfX−halfZ로 중심 한계를 정한다.
- `WorldPresenter.UpdateViewExtent`: orthographicSize와 `Footprint`로 대칭 크기를 만들어 MonsterModel·PartyMoveService에 전달한다.
- `MonsterSpawnService`: 반크기 벡터 길이+SpawnMargin 원에서 시작, 들판 마름모로 clamp. `MonsterRecycleService`: 대칭 상자 밖·HoldSeconds 후 방향별 상자 가장자리에 재배치하고 들판으로 clamp.
- RunRules: `unitScale=.0113`, `fieldCells=64`. 게임 바닥의 마름모 반대각선은 `64/(2*.0113)` 게임 단위, 월드에서는32m다.
- `HD2DBackgroundRig`: 출력/정사영크기에서 저해상 타깃 계산. 배경 카메라 투영 행렬 복사, BackgroundLayer 제외·합성. 원근으로 카메라만 바꾸면 저해상 배경과 깊이 해석이 함께 틀어질 수 있다.
- `PlayableProbe.CameraError`는 실제 transform에서 광축 거리·흔들림을 빼 focus를 복원한다. focus 높이를 더하면 그 offset도 빼야 같은 의미가 된다.

## 3. 좌표·입력·방향·텍셀 맞춤 설계

### 3.1 진입점과 소유권

기존 `HD2D.View` 어셈블리(게임 비참조), `TiltedCameraRig` 이름·DI 등록을 유지한다. 새 투영 모드 enum이나 orthographic 설정은 만들지 않는다. 데이터 `ViewRulesData`는 기존 View 데이터 안에서만 FOV·focus 높이 등 시각 설정을 받으며, 게임/세이브 데이터 스키마는 바꾸지 않는다.

순수 계산은 `Effect/HD2DView/Utils/PerspectiveViewMath.cs`에 둔다(계획). 카메라 행렬·pixelRect·높이 샘플에 필요한 값은 인자로 받는다. 기존 `TiltedViewMath`의 정사영 전용 투영·Footprint·snap은 소비처 이행 뒤 제거한다. UprightRotation/Offset 및 경계 관측의 일반 계산은 의미에 맞게 유지한다. 시안 과거 기록·이전 시안 장면을 재작성하지 않는다.

`IsoProjection.FloorToWorld`/`WorldToFloor`는 바닥↔월드의 유일한 게임 진입점으로 유지한다. 투영 함수가 게임 단위를 알지 않게 한다. 모든 변환 경계에서 `UnitScale`을 정확히 한 번만 곱하거나 나눈다.

### 3.2 화면점 ↔ 바닥점

- 화면점은 현재 camera pixelRect 기준 UV→inverse view-projection으로 near/far 두 점을 복원해 ray를 만든다. ray가 평행하거나 뒤를 향하거나 near/far 범위 밖이면 성공으로 위장하지 않고 실패를 반환한다.
- 순수 평지 시험: `t=(groundY−origin.y)/direction.y`, t≥0의 점을 XZ→floor로 보낸다.
- 실제 높이 격자: TerrainHeightGrid의 표면과 ray가 처음 만나는 점을 선택한다. 격자 XZ 영역으로 ray를 자르고 지나가는 셀 순서대로 각 셀의 두 삼각형과 교차한다. 메시에 사용한 대각선과 높이 보간을 일치시킨다. 수식 높이 y=0만 역투영해 비탈에서 발이 밀리지 않게 한다. 충돌체·새 패키지를 추가하지 않는다.
- 왕복 기준은 `ScreenToFloor(FloorToScreen(p))≈p`다. `screen→floor→screen`도 픽셀 오차로 본다. 바닥점과 머리 위 표시점은 서로 다른 좌표이며 xz 동일을 요구하지 않는다.

### 3.3 조이스틱·키보드와 방향

조이스틱 입력 크기(0~1), 게임 이동속도·FollowRate·고정 Step 순서는 유지한다. 방향만 다음처럼 계산한다.

1. party 중심의 바닥점 p와 현재 투영에서 작은 X/Z 기준변위의 화면 차를 구해 2×2 Jacobian J를 만든다.
2. 정규화 입력 s에 대해 `d=normalize(inverse(J)*s)`를 구한다. 0입력은0, determinant가 안전하지 않으면 오류를 남긴다.
3. View 계층 WorldPresenter가 계산한 투영 보정값을 PartyMoveService의 명시적 입력 설정으로 전달한다. 서비스가 Camera/MonoBehaviour를 참조하지 않게 한다. 서비스는 주어진 방향을 게임 단위 속도로 적용한다. 초기화·화면비 변경·focus 높이 변경 시 값을 동기화하고, GameSceneLifetimeScope의 의존 방향은 늘리지 않는다.
4. 실제 대상의 방향 고르기는 `Project(p+delta)−Project(p)`를 사용한다. WorldPresenter 대원·몬스터, CombatService 공격 방향, CombatWorldPresenter 투사체/연결선에서 같은 투영 보정값을 사용한다. 단일 sin18 보정으로 모두 대신하지 않는다. 고정걸음 없는 프레임은 기존 facing 유지.

실제 입력의 화면 이동 방향과 s의 cross 오차를 검사한다. 이동속도가 화면에서 균일해지도록 게임 속도를 바꾸지는 않는다. 원근 깊이에 따라 화면 이동 속도가 달라지는 것은 허용한다.

### 3.4 텍셀 맞춤·세운 판·정렬

- 세운 판은 floor root 회전0, Upright 자식 X18°다. 매 프레임 개체 쪽으로 회전하는 빌보드는 만들지 않는다. 렌더러 world scale1·PPU48 유지.
- 기존 고정 `round(world.x*48)`, `round(world.z*sinTilt*48)`를 원근 화면 격자로 쓰지 않는다. 물리 바닥점은 유지하고 **시각 오프셋만** 현재 깊이의 투영점/기준 도트 간격에서 round 후 역투영한다. 고저차가 있으면 같은 지형면에 재교차하고 허용 이동량을 제한한다. 반전·홀수 폭 pivot의 모서리 위상까지 시험한다.
- 카메라 focus는 party의 실제 바닥점+시안focus 높이다. 카메라를 별도 월드 격자에 맞춰 focus를 움직이지 않는다. 몸의 시각 snap은 별도로 처리해 CameraError의 실제 focus 계약을 유지한다.
- 불투명/alpha clip 몸·지형은 깊이 버퍼로 정렬. sortingOrder만으로 위아래 위치를 흉내 내지 않는다. 반투명 효과는 해당 sorting layer 정책 유지.
- 기존 ActorDepthBias와 terrain-height shader의 정사영 깊이 가정도 소비처다. 원근 clip z/w를 사용하고 발/낮은 턱 침투를 확인한다. 빛 색·그림자 모양을 바꾸는 일은7단계다.

## 4. 보이는 바닥·가장자리·스폰 — 리뷰가 먼저 풀어야 할 충돌

### 4.1 계산으로 확인한 크기

시안 저장 위치·회전·FOV로 y=0 평지의 화면 네 모서리 ray를 교차하면 가까운 z는 약−10.01m, 먼 z는 약46.56m다. 16:9의 먼 모서리 x는 약±23.97m(정확 계산은 `projection-plan.json`). 19.5:9는 가로가 더 넓다. **현재 마름모 |x|+|z|≤32m가 화면 전체를 덮을 수 없다.** 높이 격자를 반영한 최종 수치는 구현 때 다시 잰다.

대칭 반크기에 max절댓값을 넣으면 중심 한계 `32−halfX−halfZ`가 음수가 된다. 평균 반크기를 넣으면 화면 밖 빈 곳 또는 화면 안 스폰이 생긴다. 둘 다 채택하지 않는다.

### 4.2 제안한 시각 경계

`FloorViewBounds`(계획, HD2D.View의 값 형)는 **초점 기준 실제 모서리4점·min/max와 높이/몸 여유**를 가진다. 대칭 중심을0으로 가정하지 않는다. 필요한 화면 바닥은 카메라 위치·화면비·지형 높이 범위에서 계산한다. 따라가기 상태를 확정한 다음 같은 프레임에 계산해 입력·가림·스폰 관측이 낡은 transform을 읽지 않게 한다.

빈 배경 방지는 **렌더 전용 외곽 바닥**으로 한다. 이행 메뉴가 게임 들판 주변에 기존 ground 재질의 연장 메시를 만든다. 연장 범위는 게임의 허용 중심 마름모와 세 화면비 footprint의 Minkowski 합을 덮는다. 검증 때는 카메라 흔들림 최대값·가장자리에서 세운 물체 높이도 포함한다. 이 메시에는 Collider·게임셀·몬스터 스폰·아이템 판정을 만들지 않는다.6단계에서 같은 경계를 새 배경으로 채운다.

중심 이동 제한은 게임 들판 마름모와 진형 자리 여유를 보존하며, 시각용 외곽 바닥의 커버리지도 만족하는 영역으로 계산해야 한다. 기존 `SetViewHalfExtent` 대칭 inset 식은 폐기한다. 화면 footprint를 들판에 억지로 넣어 중심을0에 고정하지 않는다. 이동 가능 영역 변화는 이번 카메라 전환 범위지만 수치 조정으로 전투 판정을 맞추지 않는다.

### 4.3 스폰·재배치 충돌(DR-02)

원근의 올바른 화면 밖 경계는 party 중심에서 각 방향 ray와 footprint를 교차한 거리다. 이 경계에 기존 SpawnMargin·몸 높이 투영 여유를 더한다. 재활용의 outside/PlaceMargin도 같은 비대칭 경계로 계산한다. 유형 난수·번호·정규/버닝 수·간격·행동·공격 지연·재활용 횟수/시간은 보존한다.

**그러나 이것만으로는 충분하지 않다.** 원점에서 위쪽 바닥 화면 경계가46m인데 적의 게임 경계는32m다. 기존 diamond clamp를 유지하면 위쪽 적은 화면 안에서 나타난다. 시각용 외곽 바닥을 늘려도 적의 게임 경계는 늘지 않는다. 따라서 “정확한 시안 카메라 + 현재 게임 경계 + 모든 방향 화면 밖 스폰” 세 조건을 동시에 만족한다고 주장할 수 없다.

이 브리핑의 권장 순서는 다음과 같다.

1. 시안 카메라·게임 데이터·각도 난수/가중치를 그대로 두는 것을 우선한다.
2. 후보 각도의 화면 밖 구간이 게임 들판과 교차하면 그 구간에서 기존 margin으로 배치한다. 다이아몬드 clamp 후 몸이 들어오지 않는지 실제 투영으로 확인한다.
3. 교차하지 않는 각도는 위쪽 화면 내 출현이 불가피하다는 **설계 충돌**로 기록한다. 임의로 방향 가중치를 바꾸거나 적을 외곽 장식 영역에 놓아 성공시키지 않는다.
4. `design-reviewer`는 이 충돌이 구현 차단인지, 지시의 “위쪽 몬스터가 읽히는가”·기존 게임 경계 아래에서 허용 가능한 제한인지 먼저 판정해야 한다. 허용 판정/디렉터 결정이 없으면 A-05를 PASS로 만들 수 없어 카메라·스폰 통합 구현을 시작하지 않는다.

디렉터 확인 필요: 데이터·게임 규칙 변경 금지 아래에서 위쪽 화면 내 스폰을 허용할지. 이는 사용자에게 이번 실행에서 질문하지 않고 리뷰 요청에 올린다. 기준을 낮추거나 route 기대식을 바꾸지 않는다.

## 5. 모든 소비처 이행 대조표

아래 경로는 `Assets/Game/02.Scripts/` 기준이며 별도 표기가 있으면 레포 기준이다.

| 소비처 | 현재 가정 | 바꿀 계약/확인 |
|---|---|---|
| Effect/HD2DView/View/TiltedCameraRig.cs | orthographicSize·고정 격자 | 원근/FOV20·시안 거리·focusoffset, transform 유일 소유·순서 독립 |
| Features/Field/Data/ViewRulesData.cs + Resources/Data/View/view_rules.json |30°·distance20·target/minScale |18°·시안 거리·FOV·focus 높이, orthographic 설정 퇴역 |
| Features/Field/Utils/IsoProjection.cs | TerrainHeightMath.SnapOnTerrain | floor↔world 유지, projection snap은 유일 시각 경로 |
| Features/Party/Utils/PartyMoveMath.cs + Service/PartyMoveService.cs | sinTilt 입력·대칭 inset | 투영 보정 입력·비대칭 화면/시각 외곽 커버리지, 속도·follow 유지 |
| Features/Field/Presenter/WorldPresenter.cs | ortho footprint·sinTilt facing | 확정된 실제 camera frame에서 bounds·보정값, 대원/적 위치별 facing |
| Features/Monster/Model/MonsterModel.cs + Service/MonsterService.cs | ViewHalfExtent Vector2 | 내부 화면 범위 전달 계약을 FloorViewBounds로 일괄 이행(세이브 계약 아님) |
| MonsterSpawnService/MonsterRecycleService | 원·대칭상자·diamond clamp |4절 충돌 판정 뒤 방향별 ray 출구·몸 여유, 난수/전투 계약 유지 |
| Features/Combat/Service/CombatService.cs | 공격 facing=sinTilt | 위치별 screen tangent. Camera를 게임 서비스에 주입하지 않음 |
| Features/Combat/Presenter/CombatWorldPresenter.cs | Screen()·delta의 고정 투영 | 위치별 Project delta·머리 앵커, 연결선 양끝 실제 화면좌표 |
| Features/Combat/View/ProjectileView.cs | SnapScreenPlane .5 상수 | 현재 투영/깊이에서 시각 snap, 뿌리는 바닥점 |
| Features/Combat/View/BurstView.cs | rotation30 상수 |18°·투영 frame 전달, 바닥 효과와 세운 효과 분리 |
| Features/Monster/View/MonsterView.cs | 예고 localScale.y=2 | 몸 Upright18°, 예고 바닥 역투영; 그림·동작은8/9 |
| Features/Monster/View/EnemyShotView.cs | prefab Upright/ground shadow |18° prefab 이행, 포자 앵커 투영·그림 보존 |
| Features/Party/View/MemberView.cs | 기존 Upright/HP 자식 |18° 기울기·HP 앵커,48칸 임시 몸과56칸 최종 몸 구분 |
| Features/Feedback/Presenter/{MonsterHpBarPresenter,DamageNumberPresenter}.cs + View들 | 월드높이 offset | 실제 head height→project→화면 간격. 뷰 루트 floor와 표시점 분리 |
| Features/Feedback/Presenter/HitReactionPresenter.cs | rig shake | rig의 focusoffset 포함 실제 복원·흔들림 종료 원복 |
| Features/FirstExpedition/Presenter/FirstExpeditionPresenter.cs | WorldToScreenPoint | 새 projection에서 목표 말풍선/지도 밖·화면뒤 처리, 튜토리얼 로직 변경0 |
| Features/Field/View/FieldView.cs + Effect/HD2DView/Utils/OcclusionMath.cs | screen rect·depth·가림 |8꼭짓점 near-plane 처리, 대원만 창, 정상재질 복구 |
| Effect/HD2DView/{View/ConformedDecal.cs,Utils/GroundDecalMath.cs} | sin30 눕힘 밀도 | 바닥 표시 현재위치 투영 계약, 원화/래스터의 최종 손질9 |
| Effect/HD2DView/Utils/{TerrainHeightMath,CardSnapMath,PixelScaleMath}.cs | 정사영 격자/정수 배율 | 실제 projection용 계산으로 교체. 일반 높이 샘플 보존 |
| 03.Resources/Shaders/HD2D/HD2D_*.shader | actor bias·background depth | 원근 clip 깊이 계산·합성 대응, 색·빛 값 변경0 |
| Effect/HD2DView/View/{HD2DBackgroundRig,HD2DBackgroundFeature,HD2DBackgroundSource}.cs | 저해상/정사영 계산 | 저해상 도트 배경 활성경로 퇴역, 같은 카메라에서 고해상3D·몸·표시 렌더 |
| App/GameSceneLifetimeScope.cs | rig component 등록 | 등록 이름 유지, view frame 전달만 연결. DI 순환0 |
| App/PlayableProbe.cs | CameraError·unitPixels·view limits | 실제 focusoffset 역복원/두 점 투영 픽셀 간격. 공개 이름·의미 유지, route 기대식 변경0 |
| Editor/MainSceneTiltedViewMigration.cs |30° 장면·prefab 생성 | 같은 진입점에서 새 DQ 이행 호출, 옛30°로 되돌리는 메뉴 비활성/명시 실패 |
| Editor/MainSceneBuilder.cs 및 MainScene*Migration.cs/HD2DFxMigration.cs | orthographic/30° 상수 | 새로 빌드해도 최종 DQ 적용으로 끝나는 경로, Main 값 수동 수정0 |
| Editor/HD2DMainPlateCapture.cs |ortho_size·48칸·고정 영역 | 실제 camera matrix/frame·FOV/거리·키·사전영역 선언 |
| tools/hd2d_main_gate.py | 외부 KIT 절대경로·mode ortho30·height96 | 연결부를 설치된 도구/실제 원근 증거로 전환. 관문·문턱은 건드리지 않음 |

시안 전용 `Editor/HD2D/Round*`와 `Effect/HD2D/Round*`의 과거 정사영 실험은 Main 활성 경로가 아니다. 삭제하지 않으며 Main 소비처 검색에서 따로 분류한다. 게임 활성 경로에서 orthoSize·sin30/.5 투영 상수 잔재0을 확인한다. 정사영과 원근 두 벌 설정을 남기지 않는다.

## 6. 렌더·가림·월드 표시

### 6.1 고해상 배경으로 가는 연결

K30/K33과 r2의 매끈한 배경 기준 때문에 저해상 배경 합성을 Main에서 계속 켜둘 수 없다. 5단계 이행은 Main 카메라가 BackgroundLayer를 직접 그리도록 하고 `HD2DBackgroundRig`의 임시 camera/RTHandles를 정상 해제한다. 저해상 depth 합성·배경 외곽선 기능은 Main renderer에서 제거한다. 시안용 renderer를 그대로 복제해 reflection으로 URP renderer list를 런타임 변경하는 DQCameraScope는 본 게임에 가져오지 않는다.

Main 전용 renderer asset을 이행 메뉴가 갱신한다. 기존 효과·월드 표시 기능은 보존한다. 출력 renderScale1, AA 정책은 현재 요구 설정을 읽어 기록한다. 낮은 해상도 배경을 끈 것을 새 질감·색/빛 적용 완료로 보고하지 않는다. 원근과 충돌하는 depth 해석만 고치고7단계의 조명 수치·Bloom·흐림 띠 값은 그대로 둔다.

### 6.2 가림

기준 대상은 현재 정책대로 **대원만 비침**, 몬스터 비침 금지다. 가리개는 현재 등록 소품 전부(나무·덤불 포함). 화면 사각형 교차와 실제 camera depth 앞뒤를 함께 본다. near 뒤의 꼭짓점을 무조건 Rect에 넣어 화면 전체 창이 되지 않게 clip한다. 대원 몸 사각형에 현재 여유만 더한 창을 쓰며 소품 전체 alpha를 낮추지 않는다. 소품 앞·뒤·옆/진형 이동·풀 반환·씬 종료에서 재질 복구를 시험한다.6단계 새 모델을 넣으면 같은 등록 함수를 반드시 사용한다.

### 6.3 HP·숫자·예고 자리

- HP·숫자는 기존 `WorldIndicator` 레이어와 후처리 뒤 패스를 유지. 주카메라 일반 패스에 중복 그리지 않는다. Fullscreen blur 뒤 indicator 순서를 실제 프레임에서 확인한다.
- 머리 앵커는 실제 sprite의 불투명 높이/피벗+Upright18°로 계산한다. 화면상 띄움 거리는 기존 Px 데이터에서 환산하고 해당 깊이에서 역투영한다. 먼 적을 무조건 같은 크기로 확대하지 않는다. 기존 그림/폰트/색 보존,9단계에서 최종 크기·배치 다듬기.
- 피해 숫자의 시간·합산·풀 수명은 유지한다. 소멸/HP 감소 시험은 종전대로. 숫자를 actor 중심으로 옮기지 않는다.
- 예고선/원은 바닥 판정 좌표와 일치해야 한다. 기존 localScale2·상수sin30은 제거한다. 경사진 바닥의 ConformedDecal과 평지 SpriteRenderer 두 경로가 같은 영역을 가리키게 한다. 바닥 그림 최종 래스터 품질은9단계 검증 대상이다.

## 7. 이행 메뉴·단위 순서(승인 뒤 실행)

새 메뉴 계획: `DeadSurvivor/DQ/Apply Main screen` / 클래스 `Editor/MainSceneDQScreenMigration.cs`. 함수 `Apply()`는 같은 Main·prefab·renderer 자산을 갱신하고 GUID를 유지한다. 기존 다른 세션 변경을 복사하지 않는다. Main 전체를 MainSceneBuilder로 초기화해 튜토리얼 등 후속 변경을 덮지 않는다.

1. 순수 투영 계산·양수 건수 EditMode 시험. 기존 활성 경로는 아직 유지해 컴파일 통과. 임시 병존은 이 단위까지만.
2. 카메라+frame 전달+좌표/입력/facing+모든 소비처+경계/스폰을 한 통합 단위로 바꾼다.4절 설계 충돌의 필수 판정 전에는 이 단위를 시작하지 않는다. Main rig·prefab Upright·renderer 이행. 공개 probe/route 계약 유지.
3. 가림·월드 표시·고해상 배경·외곽 시각 바닥 연결. projector의 현재frame을 단일 사용. 원근 수정 때문에 효과 그림을 새로 제작하지 않는다.
4. 메뉴 두 번 실행: 각각 응답 성공·재컴파일 준비 상태·실제 참조 확인 후 Main/prefab/material/renderer/mesh의 hash 및 GUID·중복개체0 비교. 단순 “변경없음” 로그만 성공으로 읽지 않는다.
5. 관련 EditMode 한 묶음, playable 경로 전체1회,3화면비 시작/이동/가장자리 캡처·이동 연속 직접 열기. 컴파일0. 단위마다 파일 지정 stage·커밋, 마지막에 마감 리뷰3종.

공유 데이터와 이행 메뉴 의존을 바꾸는 중 컴파일이 깨진 상태를 단위 커밋으로 남기지 않는다. 씬·prefab·재질은 이행 메뉴 외의 수동 YAML 수정0.

## 8. 수용 기준·명령 관문

| ID | 완료 조건 | 검증 방법·반례 |
|---|---|---|
| A-00 | 목록73행에 네 질문·단계·완성/금지 정의, 누락0 | 코드 검색/자산 목록과 대조. 임시 링/선/적탄 포함, 다른 계열 실패 상상 보완 |
| A-01 | 실제 Main 원근18°·FOV20°·시안 거리/focus/clip | 저장 후 재열기 실제 camera 값,3화면비. ortho 설정 선택지0 |
| A-02 | 기준 깊이 도트 약2px@1080,56칸 표지 약112px/10.4% | alpha silhouette bbox·측정 frame, 현재48칸은96px 목표로 별도 기록. 새 그림 완료는8단계 |
| A-03 | floor↔screen 왕복/높이/텍셀 오차 | EditMode. 원점/±10m/경계, flat 및 실제 격자,3화면비. 평행ray·뒤·near/far·홀수폭pivot·반전. 예상 바닥 오차≤1e−3m/화면 오차≤0.5px(제안 시험 정밀도, 아트 문턱 대체 아님) |
| A-04 | 입력 방향 일치·게임 속도 유지 | 0·8방향×주역/비탈/가장자리,길이1/0.5,투영 cross 오차와 유한값; 실제 joystick Play 이동 |
| A-05 | 스폰·재활용 실제 화면 밖·몸 포함, 규칙 불변 | 방향별 교점+margin·3화면비·중심/경계/위쪽. DR-02 미해소이면 **BLOCKED**, clamp로 화면 안 진입0을 관측 |
| A-06 | 3화면비/가장자리/흔들림에서 빈 곳0, 이동 정지 결함0 | footprint+렌더 외곽 커버리지 시험·이동 도착 조건·시작/모서리 캡처, 게임 경계 밖 판정 없음 |
| A-07 | 가림은 대원 창만·정상 복구 | 소품 뒤→앞→옆·near 교차·풀 반환, 몬스터 비침0, 재질 hash/화면 확인 |
| A-08 | 월드HP/숫자 머리 겹침0,후처리 뒤/예고판정일치 | 앞/가운데/뒤/비탈/흐림 띠 안 같은 frame쌍, 표시는 기존 그림, 실제 피해/범위 숫자와 대조 |
| A-09 | 이행 메뉴 두 번 동일·컴파일0·관련시험>0 | 실제 응답·저장자산 hash/GUID·중복0·콘솔(기존FMOD도 구분), 양수 테스트 결과 |
| A-10 | 기대식 그대로 한 판과 재시작, 캡처 직접 열기 | `python3 -B tools/playable.py`, 결과 단계수/시각/fingerprint; 시작·이동·가장자리×3화면비+연속8프레임 |
| A-11 | 기준·구조·계산 마감 독립 리뷰 | 구현·검사·커밋 뒤 Claude의 spec-reviewer/validator-client/algorithm-tester, VERDICT 원문 |

명령 계획: `python3 .ai/studio-kit/project-doctor.py --root . --host codex --require unity-client.implement` → unity-cli로 **이 작업 트리 에디터** 확인 → 컴파일 → EditMode 관련필터(양수건수) → playable1회 → 캡처. 이번 설계 전환에서는 `--require design.write` exit0을 확인했다. 편집기 조작은 이번에는 실행하지 않았다.

### 시험 표본(구현 때 기대값으로 고정)

- 카메라 회전18°·FOV20°·시안 저장 위치에서 평지 원점, (±5,0,0),(0,0,±5)의 왕복. 행렬 inverse의 자기복사만 시험하지 않고 수식의 광선 교점과 대조한다.
- UV(0,0),(1,0),(1,1),(0,1),(0.5,0.5)의 y=0 교점. 중심과 먼/가까운 폭이 다른 것을 수치로 확인. 1920/1080,2340/1080,2048/1536.
- 평행 ray(0,0,1), 뒤로 향한 교차·높이 범위 밖·near-plane 걸친 bounds. invalid를(0,0) 성공으로 반환하지 않는다.
- 입력(1,1),(−1,1),(0,1),(0,0)과0.5 크기. 이동 후투영 방향의 부호/각도, 게임 거리/속도 보존.
- footprint 모서리의 min/max 비대칭,32m field에 upper ray46m가 들어가지 않는 반례. DR-02 해소 방안의 반례로 그대로 남긴다.
- focus→shake/역순·해상도변경·shake종료에서 camera 실제 transform 복원. transform 갱신을 일부러 빼면 CameraError가0이 아니어야 한다.
- 이행2회 및Play종료/다시시작 시 임시 배경카메라·RT/재질 누수0(성능 측정 아님, 소유자 수/해제 확인).

## 9. 기존 관문 연결과 한계

`tools/hd2d_main_gate.py`의 plan→capture→finish 구조는 유지한다. 지금 파일은 KIT 절대경로, `expected_camera.mode=ortho`, angle30, height96, ortho_size5.625, 고정 구역/closeup을 가진다. **그대로 실행해 새 원근 화면을 PASS라고 보고하지 않는다.**

5단계에서 고칠 것은 프로젝트 연결부와 수집기다. 사전영역은 실제 Main 대상의 화면 꼭짓점에서 선언, source-before/after에는 Main·새 투영 코드·그림/재질/셰이더·수집기·기준서 스냅샷을 결속한다. FOV/실제position/focus·view/projection matrix·pixelRect·출력·몸alpha bbox·현재 품질을 기록한다. 임시 캡처는 editor clock·새 frame으로 진행, 끝나면 aspect/target/입력/시간을 원복한다.

설치된 `.ai/25d-art/tools` 및 관문 설정/도구의 원근 지원 여부는 구현 전 확인해야 한다. 현재 작업 트리에는 `.ai/rules/25d-*.md`가 없어서, 이번에는 지정 킷의 `25d-art-setup/template/root/.ai/rules/` 네 문서를 읽었다. doctor의 configured는 파일 설치/런타임 지원을 보증하지 않는다. 킷 파일을 이번 범위에 몰래 설치/개조하지 않는다.

숫자 관문이 원근 형식을 지원하지 않거나 폐기된 도트배경/28° C10 문턱을 강제하면 **검사 부적합 BLOCKED**로 그대로 기록한다. 기대값·문턱을 낮추거나 도구를 고쳐 통과시키지 않는다.5단계 자체 A-01~A-10 증거와 다음6/7/8/9단계 관문을 분리한다.9단계가 최종 본 게임 관문을 완성한다. 성능G19/K47은 이번 실행에서 재지 않는다.

## 10. 기준 문서와 다른 점·이미 결정된 것

| 항목 | 이번에 따를 것 | 이유·상태 |
|---|---|---|
| K31 표의28° 제안·C10≥0.6 |18° 판A, 먼적 자연 축소 | 사용자 “A가 나은것 같애”,2.1 Q1 우선 |
| K32 48칸96px/8.9% |K50 56칸112px/10.4%,PPU48·2px | r2 대원 키가 갱신됨. 옛48칸을 늘려 새56칸 완료라 하지 않음 |
|3.10 걷기6~8 Aether |조각 회전 walk24/idle12/attack7/down10+누움 |11-23 사용자 지시;8단계 |
|3.10 피격 |맞는 한 장+흰 번쩍임·1px밀림 |11-23 “추천대로”;8단계 |
|3.3 소품6 |기존모델 새질감,유료0 | 디렉터 판단;6단계 |
|3.3 나무4·덤불2 |채택나무3·덤불1,결함2종 제외 | 디렉터 판단;6단계 |
|K16 UI Q2 |현재 UI 그대로·자리/크기만 | 디렉터 추천 적용,재디자인9단계에서도 안 함 |
|K47 |성능 측정 없음 |11-18 사용자 결정,별도 마지막 |
|킷의 정사영·도트배경 예시 |원근·매끈한3D | 이 프로젝트 기준서r2가 우선 |
|몬스터 방향 원본 |4원화+앞대각정면 재사용,반전 포함 | D1 인계의 이미 알려진 차이. 고유8방향이라 재보고 금지 |

이미 결정된 것은 리뷰에서 취향 대안으로 다시 열지 않는다. DR-02의 경계/스폰 충돌은 아직 결정된 것이 아니다. 다른 단계 항목은1절의 “N단계 몫, 이 단계 판정 밖”을 리뷰 입력에 그대로 넘긴다.

## 11. 이번 조사 근거·시간·놓친 것

- 원시 계수·scene camera 블록·재질 참조 닫힘·그림 경로: `output/hd2d-dq/apply/5/source-audit.json`. 정적 검색은 실행 중 활성 사용 전체를 증명하지 않는다.
- 카메라 계산과 직접 본 시안 목록·해시: `output/hd2d-dq/apply/5/projection-plan.json`, `opened-images.json`.
- 과거 핸드오프3개는 지정된 원래 폴더 `.director/outbox/`에서만 읽었다. 과거 PASS·FMOD FAIL은 참고이며 이번 관문 판정이 아니다. E1/D1 그림은 설계 근거로 직접 열었고 새 Main 캡처는 NOT_RUN.
- 레퍼런스 manifest는 존재하나 원본 이미지가 가리키는 `output/reference/hd2d-dq-20261121/`는 이 작업 트리에 없다. 원래 폴더 읽기 허용 범위에 이 경로가 없으므로 원본을 새로 읽지 않았다. 이번에는 문서 관찰과 허용된 D1/E1 캡처를 비교했다. 구현 마감의 레퍼런스 원본 직접 대조는 미완료다.
- 놓친 것: 요청한25d규칙이 레포에 설치되어 있다고 처음 가정했다. 직접 존재 검사로 부재를 확인하고 킷 원본을 읽었다. 원인=doctor 구성 상태와 실제 파일 혼동 가능. 워크플로우 갱신 후보=다음 단위 시작 때 필수 파일 존재를 먼저 확인. 이 브리핑9절에 반영, 공용 규칙 추가0.
- 비용 개선: 첫 읽기에 큰 문서를 묶어 출력해 잘린 구간과 중복 출력이 생겼다. 이후 필요한 함수·절로 좁혔다. 반복 전체검사 추가0, 이번 설계에서Unity 검사를 돌리지 않으므로 뺄 중복Play0. 기존 검사 삭제0.
- 시간·토큰은 `output/hd2d-dq/apply/5/session-time.md`에 집계한다. Claude·하위에이전트 호출0. 설계리뷰 반환 후 구현시간과 합쳐180분 상한을 다시 확인한다.
