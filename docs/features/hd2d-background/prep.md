# HD-2D 5단계(배경을 본 게임에) — 구현 설계

- **상태: 확정 설계(11-18) — 5단계 구현용.** 1단계 시안 전에 쓴 준비 초안(같은 파일, 11-18 01:18)을 시안 8회차 F8(관문 전체 PASS)과 아트 기준서 r6.1에 맞춰 다시 썼다. 초안에서 바뀐 결정은 2절 표에 "대체"로 남겼다.
- 전제: **4단계(화면 기반, `docs/features/hd2d-screen/design-brief.md` W0~W7)가 끝나 커밋된 뒤** 시작한다. 이 문서를 쓸 때 W0~W2는 커밋됐고 W3(배경 도트화 패스 본 게임 연결)는 커밋 전이었다. W3 파일은 아직 바뀔 수 있으므로 B0 시작 전에 다시 읽고 13절 "W3과 부딪히는 곳"을 고친다.
- 쓴 날·방법: 2026-11-18, Claude 하위 에이전트. 파일만 읽었다(유니티 실행·커밋·`Assets/` 변경 없음). 크기는 `/usr/bin/python3` PIL로 png를, Tripo 기록 JSON으로 fbx를 쟀다.
- 읽은 것: [아트 기준서](../../art/아트-기준서.md) r6.1(K10·K18~K28, 3.1~3.6, 3.9, 3.12, 5절 "5단계로 넘김"), [화면 구조 설계](../hd2d-screen/design-brief.md) 3.1·3.4~3.9·3.13·5절, [완성 실행 계획](../../game/hd2d-완성-실행계획.md), [전환 계획](../../game/hd2d-전환-계획.md) 5단계, [사전 조사](../../game/hd2d-사전조사.md) D12, [다리·천막 Tripo 기록](../../art/hd2d-props-tripo-기록.md), `docs/art/asset-catalog-hd2d.json`, 시안 코드 `Editor/HD2D/Round8/*`, `Effect/HD2D/Round5·Round6/*`, `Editor/HD2D/LookdevSceneBuilder.cs`·`Round2/PixelLookdevSceneUtils.cs`, 시안 결과 `output/hd2d-시안/visual-check9.md`·`execution-note5.md`·`plates8.json`, 관문 `output/hd2d-관문/r8/`, 지시문 `.director/runs/20261118-hd2d-lookdev/steps/8-Deadsurvivor.md`, W3 작업 파일(`Effect/HD2DView/View/HD2DBackground*.cs`·`UprightShadowCaster.cs`·`Editor/MainSceneHD2DEffectsMigration.cs`, 커밋 전), 게임 데이터 `run_rules.json`·`view_rules.json`·`spawn_rules.json`·`decor.json`·`ground_path.json`·`Data/Character/*.json`, 코드 `FieldView`·`WorldPresenter`·`PlayableProbe`·`SpriteAtlasService`·`SpriteFacing8`, 시험 `FieldDecorTests`, 그림 기록 `output/assets/hd2d-8dir/**/notes.md`, 다리·천막 `post.json`·`planar.json`·`spec.md`·`dot/notes.md`(디렉터 추가 사실 11-18: 게임에는 새 UV + 구운 도트 질감 `dot/` 모델을 쓴다).
- 표기: **측정** = 파일에서 잰 값, **계산** = 측정값으로 계산, **추정** = 재지 않은 값, **[F8]** = 시안 F8 코드·결과에서 확인한 값.
- 규모: **L1**(`.ai/rules/work-levels.md`). 새 데이터 파일·뷰 구조·셰이더 구조를 더하지만 상태 소유권·DI 구조·세이브 형식·렌더 파이프라인 설정은 바꾸지 않는다(렌더러 기능은 W3 것을 그대로 쓴다). 그래서 설계 검토(design-reviewer) 없이 이 문서를 마감 리뷰 입력으로 넘긴다(12절 D-9).

## 0. 한눈에

| 항목 | 내용 |
|---|---|
| 하는 일 | F8에서 관문을 통과한 배경 방법(배경 도트화 패스·3단 툰·하드 도트 그림자·실루엣 외곽선·세운 도트 판 바위·무작위 풀 조각·본 화면 잘라 근접)을 본 게임 `Main.unity` 전투장 전체에 깐다. 다리·천막(Tripo)과 8방향 그림·클립·몬스터 상태 그림을 넣는다. "5단계로 넘김" 8건을 고친다 |
| 지형 배치 | **시작 화면 = 시안 F 구성**(연못+턱 왼쪽 위, 양옆 나무·바위 줄, 울타리, 덤불, 꽃 무리). 길은 **본 게임 `ground_path.json` 그대로**이고 F 소품이 길과 겹치는 곳만 민다. 나머지 들판은 F의 같은 부품으로 채운다(4절) |
| 2D 판 / 3D | 세운 도트 판: 나무·덤불·큰 바위·작은 돌·돌담·물가 돌·꽃·풀포기·깃발 천. 3D(배경 도트화 패스): 땅·길·낮은 턱·연못·개울·울타리·표지판·깃발 기둥·다리·천막(5절) |
| 작업 단위 | B0 그림·질감·셰이더 → B1 배치 데이터 → B2 땅·길·턱·물 → B3 세운 판 배경 → B4 3D 소품(다리·천막 포함) → B5 8방향 그림 넣기 → B6 몬스터 그림자·외곽선 → B7 가려짐 → B8 본 게임 관문 판 → B9 마감(10절) |
| 확인 | 단위마다 그 단위만(컴파일, 지정 `-testFilter`, `tools/playable.py --until <단계>` 1회, 캡처 1장 직접 열기). 판정 경로 전체·EditMode 전체는 B9에서 한 번. 관문 `gate.py`(킷 studio-kit-25d, G19 미룸)는 B8에서 본 게임 판으로. **성능은 재지 않는다**(7단계) |
| 다리·천막 질감 | **UV + 구운 도트 질감**(`output/assets/hd2d-{bridge,tent}/dot/`: 새 UV를 편 `model.fbx` + `albedo.png` 48텍셀/m). 명세의 "삼면 투영 도트 질감, UV 불필요(D10)"는 이 방식으로 바뀌었다(6절) |
| 유료 생성 | 없음. 새 그림(자갈 질감·깃발 천·필요하면 돌담 조각)은 Codex 내장 생성 + `pixelize.py`(무료) |
| 디렉터 결정 | 12절 D-1~D-9(권장값 있음) |

## 1. 바탕 사실

### 1.1 전투장과 보이는 범위 [측정·계산]

| 값 | 내용 | 출처 |
|---|---|---|
| 들판 | 마름모 \|x\|+\|y\| ≤ 32m(바닥 좌표 x = 화면 오른쪽, y = 화면 위·먼 쪽). 칸 (cx, cy) → 바닥 ((cx−cy)/2, (cx+cy)/2) | `run_rules.json` `fieldCells` 64, `IsoProjection.IsoCellToFloor` |
| 3D 자리 | 바닥 (x, y) → 월드 (x, 0, y). 카메라 정사영 30°, 거리 20, near 0.3·far 60 | `view_rules.json`, design-brief 3.3·3.4 |
| 원정대 이동 한계 | L1 ≤ 32 − 반폭 − 반깊이. 1920×1080: 10.75 | design-brief 3.4 표 |
| 몬스터 | 들판 L1 ≤ 32 안에서만 생기고 다닌다(W5). 충돌 없음 — 소품·물·턱을 뚫고 지나간다 | design-brief 3.6 |
| **보이는 바닥** | 바닥 점 p가 어떤 화면에서든 보이는 조건 = **(\|x\|−반폭)⁺ + (\|y\|−반깊이)⁺ ≤ 이동 한계** [계산: 화면 중심이 L1 ≤ 한계인 직사각형들의 합]. 들판 밖(L1 > 32)은 화면에 안 들어온다 | design-brief 3.4·3.5 |

화면별 보이는 범위(1.1 식, [계산]). 들판 마름모의 네 꼭짓점 부근은 어떤 화면에서도 보이지 않는다. 반대로 **마름모 네 변의 가운데 부분은 1920×1080에서 경계까지 보인다**(x∈[10, 20.75]인 변 위의 점은 식이 정확히 한계와 같다).

| 화면 | 반폭 / 반깊이 | 한계 L1 | 보이는 \|x\| 최대 | 보이는 \|y\| 최대 |
|---|---|---|---|---|
| 1920×1080(판정 화면) | 10.00 / 11.25 | 10.75 | 20.75 | 22.00 |
| 2340×1080 | 12.19 / 11.25 | 8.56 | 20.75 | 19.81 |
| 2520×1080 | 13.13 / 11.25 | 7.62 | 20.75 | 18.87 |
| 2048×1536 | 7.11 / 10.67 | 14.22 | 21.33 | 24.89 |
| 2532×1170 | 13.19 / 12.19 | 6.62 | 19.81 | 18.81 |
| 1280×720 | 6.67 / 7.50 | 17.83 | 24.50 | 25.33 |

- 이 문서는 **1920×1080 팔각형**(이하 V1080: (\|x\|−10)⁺ + (\|y\|−11.25)⁺ ≤ 10.75)을 "보여야 할 것"의 기준으로 쓴다. 판정 경로·관문이 이 화면으로 돈다.
- 테(L1 32~36, 들판 밖)는 땅 높이 0이면 보이지 않는다. 키 큰 것의 윗부분만 보인다: 높이 h인 것은 화면 위쪽 경계 너머 깊이 h·cos30°/sin30° = **1.73h m까지** 보인다[계산]. 나무 1.69m → 2.9m.

### 1.2 길·지금 장식 [측정]

| 값 | 내용 | 출처 |
|---|---|---|
| 흙길 | 70칸. 바닥 (−17, −15)[L1 32, 남서 경계] → (−2, 0) → (1, −3) → (17.5, 13.5)[L1 31, 북동 경계]. 가운데에서 두 번 꺾인다. 원정대 시작점 근처를 지난다(가장 가까운 칸 L1 2.0) | `ground_path.json` |
| 지금 장식 | `decor.json` 46개(나무 4, 바위 8, 꽃 15, 풀 9, 울타리 10). 시험 `FieldDecorTests`가 "가장자리 띠 16칸 안", "길에서 L1 3m 이상", "시작 생성 원 밖"을 요구 | `decor.json`, `FieldDecorTests.cs:36-90` |
| 그림 크기 | `tree_01` 75×81(1.56×1.69m), `rock_01` 17×9, `rock_02` 28×16, `flower_01~03` 17×16·18×17·18×17, `grass_tuft_01~02` 13×8·18×14, `fence_01` 24×30, `Common/shadow.png` 24×12 | png 측정, PPU 48 |
| 노란 꽃 | `flower_02`(#f8b828 42px·#ffe08a 15px가 주색). `flower_01` 흰색, `flower_03` 보라 | png 색 측정 |

### 1.3 시안 F8의 배치·값 [F8]

시안 장면은 원정대(원점) 둘레 한 화면이다. `LookdevSceneBuilder.cs`·`Round2/PixelLookdevSceneUtils.cs`·`Round8/FieldLookdevScene8.cs`에서 읽었다(바닥 좌표 = 월드 x, z).

| 부품 | 자리·값 |
|---|---|
| 바닥 | 50×50m 평면 하나, `Surface_0`(풀 질감 `FieldGround.png` 2048×1024, 월드 UV = xz ÷ 42.67m → 가로 48·깊이 24 텍셀/m). 흙길은 질감 안에 구워져 있다(z ≈ 2.8 가로 띠, 반폭 0.67±0.10 + 경계 흔들림 ±0.10) |
| 연못 | 예전 046 맵 `OB-006_Water` 메시를 ×1.25 해서 중심 약 (−4.8, 6.5)에. 물가 돌 메시는 끄고 연결 성분마다 `rock_02` 세운 판 **188개**(색 0.72배) |
| 낮은 턱 | `RoundBlock.asset`: 0.125m 칸, 높이 0.36m, 두 직사각형(x −6.75~−1.125, z 8.25~9.25 / x −5.5~−3.5, z 6~8.5). 가장자리 들쭉날쭉은 **주기 5·7 칸 규칙**(`(x%5+5)%5<2`) — 넘김 1번 "톱니"의 원인[추정]. 윗면=바닥 재질, 옆면=흙 3단 띠 |
| 나무·바위 줄 | 양옆 x = ±4.5 / ±6.1, z = −4 + 1.8i(i 0~6), 종류 i%3 = 나무·바위·작은 돌 → 한쪽 나무 3·바위 2·작은 돌 2. 큰 바위 하나(x>0, 0<z<4)를 `FieldBoulder.png`(52×42) 판으로. 바위·돌 판 색 0.72배. x<0·z>6 나무는 턱 위라 0.36m 올림 |
| 덤불 | `FieldBush.png`(51×47) 양옆 5개씩: x = ±(4.4 + 0.6·(i%2)), 왼쪽 z = −5 + 1.1i, 오른쪽 z = −3 + 2.1i |
| 울타리 | (±8.2, 0.3) 두 개. 기둥 2(둥근 상자 0.12×0.6×0.12, x ±0.4) + 가로대 2(0.9×0.09×0.09, 높이 0.2·0.42) → 폭 0.92·높이 0.6m[계산]. 메시 그림자 끔 + `shadow.png` 발밑 덩어리 2(HardShadowCard) |
| 꽃·풀 | 고정 시드 181108, 군집 7곳 (−7,−7) (−2,−5.8) (3,−7.5) (7,−4.8) (−7,1) (1,1.2) (7,4.3), 150개. 종류는 약 1/9 `flower_01`, 1/9 `flower_02`, 나머지 `grass_tuft_01` |
| 세운 판 그림자 | 장식 판은 실제 그림자 캐스터를 끄고 `PixelFootShadow`(그림 실루엣을 해 방향으로 받침면에 투영한 정적 메시, 배경 층, 턱 경계에서 잘림)를 쓴다. 꽃·풀·물가 돌은 그림자 없음 |
| 몬스터 재질 | `StableMonster.mat` = 그림자 받기 끔 + **`_HueShift` 0.68**(색상 회전) |
| 카메라 맞춤 | `SnapCamera`: 카메라 오른쪽·위 축을 1/48m 격자 + 반 픽셀 위상(출력 크기 ÷ (2·배율)의 소수부)에 맞춤. 세운 판은 왼쪽 아래 모서리를 같은 격자에 |
| 해·효과 | 해 Euler(32, 135, 0), 하드, 바이어스 0.05·법선 0.02. Bloom 문턱 2.0·세기 0.1 |

## 2. 초안에서 바뀐 결정 (대체)

| 초안 결정 | 대체 | 이유 |
|---|---|---|
| 0절·2.1절 바위·작은 돌·돌담을 예전 3D 모델(`boulder045`·`stone045`·`wall.fbx`) 다시 칠하기 | **세운 2D 도트 판**(큰 바위 `FieldBoulder.png`, 작은 돌 `rock_01~02`, 돌담·물가 돌 = 돌 판 줄) | 3D 바위 5·6·7회차 3회 연속 판독 실패 → 8회차 2D 판(K18·K22 r6.1) |
| D-1 나무 3D(tree045 ×0.7) / 세운 판 | **세운 판**(`FieldTree.png` = `tree_01` K23 색 다시 칠, 75×81) | F2c 채택(K18), 3D 나무는 시안 F2b에서 버림 |
| 2.2절 땅 질감(정사각 풀 48×48, 꼭짓점 색 길) | F8 무작위 풀 조각 질감(`FieldGrassOnly.png` 방식) + **길 가림 질감**(본 게임 길 모양) | K20 r6.1. F8 질감은 시안 길(가로 띠)이 구워져 있어 본 게임 길에 못 씀 |
| 2.3절 Tripo 후보 2~5개 | **천막·다리 2개만, 이미 끝남**(80크레딧, 588·490삼각) | Tripo 기록. 울타리·표지판은 코드 메시(F8 울타리 방식) |
| 다리·천막 명세 5절 "Tripo 질감은 버리고 삼면 투영 도트 질감(D10), UV 불필요" | **새 UV + 구운 도트 질감**(`dot/model.fbx` + `albedo.png`). 감면한 `post/`·`final2/` 모델에는 UV가 없어 게임에 쓰지 않는다 | 디렉터 11-18. 도트 질감 기록 `output/assets/hd2d-bridge/dot/notes.md`·`hd2d-tent/dot/notes.md` |
| 3.1절 구역 A(L1 ≤ 10 비움)·B·C·테 | **시작 화면 = F 구성**, 나머지는 V1080 안을 F 부품으로(4절). "가운데 비움"은 화면 단위로 지킨다(원정대 둘레 L1 3.5 안에 세운 소품 없음) | 사용자 11-18 "지형자체는 우선 기존에 만든 걸 그대로 사용" → 기준서 3.2 "지형 배치는 시안 F 그대로". F는 원정대 둘레를 비우고 화면 양옆에 소품을 둔다 |
| 3.1절 테 높낮이(먼 쪽 언덕 0→3m, 높은 턱 1.2m·폭포, 가까운 쪽 내리막 0.5m) | **테는 평평한 땅 + 먼 쪽 두 변 너머 나무 줄만**. 높은 턱·폭포는 들판 안 낮은 턱으로 옮기거나 뺀다(D-3) | 1.1절 계산: 테는 어떤 화면에서도 보이지 않는다(키 큰 것 윗부분만). design-brief 3.6이 넘긴 "테 시작부를 낮게"도 평평한 테로 끝난다 |
| 3.2절 연못 (0, −24) 반지름 3m | **F 연못 (−4.8, 6.5)**(046 연못 메시 ×1.25) | F 배치 그대로. (0, −24)는 V1080 밖(\|y\| 24 > 22)[계산] |
| 3.2절 야영지 (−23, 3) | **(−16, 6)~(−14, 8.5)** | (−23, 3)은 V1080 밖(13 > 10.75)[계산] |
| 3.2절 동쪽 돌담 (21, −6.5)~(25.5, −1.5) | **(17, −8)~(20, −4)** | 같은 이유(V1080 밖) |
| 3.3·3.4절 개수·삼각형 예산, D-7 | 개수는 4.4절 목표(추정), 예산은 기준서 3.6(K27). **5단계에서 재지 않는다** | 사용자 11-18 "아트 완료된 후에 … 마지막에 테스트" |
| D-2 가운데 비울 반지름 | 원정대 시작점 둘레 L1 3.5 안 세운 소품 0, 노란 꽃은 L1 6 안 0(넘김 6번) | F 구성 + 넘김 6번 |
| D-3 돌담 모델 | 돌 판 줄(`rock_01`·`rock_02` 섞음). 판독에서 다시 지적되면 새 돌담 조각 그림(무료) | K22 r6.1 |
| D-5 들판 안 높낮이 | 낮은 턱(0.36m)은 들판 안에 있다(F 연못 둘레). 사람·몬스터가 턱을 지나갈 때 묻히는 문제는 **D-2 결정**으로 | F 배치 |
| D-6 개울을 몬스터가 지나감 | 그대로(얕은 물, 충돌 없음) | design-brief 3.6 |
| D-8 테까지 보이게 넓힘 | 하지 않음(지금 규칙) | design-brief 3.4 |
| 4절 배치 스케치 `layout-sketch.png` | B1의 새 스케치 `output/hd2d-background/layout-main.png`가 대신한다 | 초안 스케치는 대체된 구역안 |

## 3. F8에서 본 게임으로 옮기는 것과 자리

원칙(design-brief 3.1): 게임 이름이 없는 다시 쓸 코드는 `HD2D.View`(`Effect/HD2DView/`), 게임 데이터·배치는 `Features/Field/`, 이행·굽기 메뉴는 `Editor/`(`Game.Editor`), 셰이더 `03.Resources/Shaders/HD2D/`, 재질 `03.Resources/Materials/HD2D/`. 시안 파일(Round1~8)은 **읽기만** 하고 복사본 머리에 원본 경로·SHA256을 적는다(W3 `HD2DBackgroundFeature.cs` 첫 줄과 같은 방식). 게임이 시안 자산을 참조하지 않는다.

| F8 원본 | 본 게임 자리 | 단위 | 바꿀 것 |
|---|---|---|---|
| `Effect/HD2D/Round5/PixelBackground*.cs`, `Art/HD2D/Round8/Background*.shader` | **W3가 이미 옮김**: `HD2DBackgroundRig`·`HD2DBackgroundFeature`·`HD2DBackgroundSource`, `Shaders/HD2D/HD2D_Background{Composite,Depth,Outline}.shader`, `Settings/HD2D_BackgroundRenderer.asset` | (4단계) | 5단계는 쓰기만. 배경 층 = `HD2DBackgroundRig.BackgroundLayer`(27) |
| `Art/HD2D/Round8/FieldSurface.shader`(범주 0 풀·1 돌·2 물·3 턱·4 나무) | `Shaders/HD2D/HD2D_FieldSurface.shader` + 재질 `HD2D_Field_{Ground,Water,Bank,Wood}.mat`, `HD2D_Prop_{Bridge,Tent}.mat` | B0 | ① 바닥 범주에 **길 가림 질감**(`_PathMask`, 들판 직사각형 `_FieldRect`)과 흙 결 질감, ② **UV 질감 범주(5)** 새로: `_MainTex`를 메시 UV로 Point 샘플(다리·천막 `albedo.png`), 알베도 = 해 드는 띠 색이라 F8 띠 ×1.0/×0.82/×0.60(문턱 0.25/0.68)과 그림자 ×(.44,.43,.39)만 곱한다(도트 notes 팔레트 절과 같은 값), ③ **바닥 깊이 쓰기**(`_FlatDepth`, D-2), ④ **디더 사라짐**(`_Dither`, B7), ⑤ 돌 범주(1)는 뺀다(돌이 판이 됨). 나무 범주(4)는 울타리·표지판·깃발 기둥만 |
| `Round8/GenerateFieldTextures.py` | `Tools/hd2d_field_textures.py` → `03.Resources/Textures/HD2D/` | B0 | 길을 굽지 않는다(풀만). 길 가림은 `ground_path.json`에서. 넘김 3번(2×2 묶음·자갈·흙 얼룩). 덤불 2종 |
| `Round8/FieldTree.png`·`FieldBush.png`·`FieldBoulder.png` | `Art/AIGenerated/Decor/tree_02.png`·`bush_01.png`·`bush_02.png`(새로 잘라 냄)·`boulder_01.png` + `source.txt` | B0 | 프리셋(`AIGeneratedSprite.preset`, PPU 48·아래 가운데·Point)이 그대로 붙는다. `Ground` 아틀라스(Decor 폴더 포함)에 자동으로 들어간다 |
| `Round8/HardShadowCard.shader`·`.mat` | `Shaders/HD2D/HD2D_HardShadowCard.shader`, `Materials/HD2D/HD2D_HardShadowCard.mat` | B0 | 그대로(Point·알파 클립 0.5·따뜻한 갈색) |
| `Effect/HD2D/Round6/PixelFootShadow.cs`, `FootShadow.mat`(`HD2D/Round6/FootShadow`) | `Effect/HD2DView/View/UprightFootShadow.cs`, `Shaders/HD2D/HD2D_FootShadow.shader`, `Materials/HD2D/HD2D_FootShadow.mat` | B3 | `RenderSettings.sun` 대신 해 방향을 인자로 받는다. `Initialize`에서만 만들고 Awake를 쓰지 않는다(design-brief 3.8과 같은 이유). 층 = `HD2DBackgroundRig.BackgroundLayer` |
| `FieldLookdevAssets8.BankStrip` | `Effect/HD2DView/Utils/BankMeshMath.cs`(순수: 칸 집합 → 윗면·옆면 두 서브메시) | B2 | 가장자리 규칙을 **고정 시드 무작위 걸음**으로(넘김 1번) |
| `FieldLookdevScene8.Fence`·`RoundedStone` | `Effect/HD2DView/Utils/PropMeshMath.cs`(둥근 기둥·가로대·판) | B4 | 울타리·표지판·깃발 기둥이 같이 씀 |
| `FieldLookdevScene8.SnapSprite` | `Effect/HD2DView/Utils/TiltedViewMath`에 붙이지 않고 새 `CardSnapMath.cs`(판 왼쪽 아래 모서리를 1/48m 격자에) | B3 | W1 파일을 고치지 않으려고 따로 둔다 |
| `FieldLookdevScene8.ReplaceShoreStones`(메시 연결 성분 → 돌 판 자리) | `Editor/FieldLayoutBaker.cs`(굽기 메뉴)가 한 번 계산해 `field_layout.json`에 적는다 | B1 | 런타임에 메시를 읽지 않는다 |
| 연못 메시 `Art/HD2D/OB-006_Water.asset` | `03.Resources/Meshes/HD2D/FieldPond.asset`(복사, `source.txt`에 원본·SHA256) | B1 | 게임이 시안 자산을 참조하지 않게 |
| `FieldLookdevCapture8.cs`·`FieldGate.py` | `Editor/HD2DMainPlateCapture.cs`, `Tools/hd2d_main_gate.py`(→ `output/hd2d-관문/main1/`) | B8 | 시안 장면 대신 본 게임 Play에서 같은 판(main·이동 8·화면비 2·마스크·근접 잘라 4배·반복·풀만·조명) |
| `StableMonster.mat` `_HueShift` 0.68 | **옮기지 않는다** | B6 | 넘김 5번 원인 후보(추정). 본 게임 몬스터는 `HD2D_SpriteLit`(색상 회전 0) |

## 4. 본 게임 지도 배치

### 4.1 규칙

| # | 규칙 | 근거 |
|---|---|---|
| R1 | 원정대 시작점(원점) 둘레 화면은 **F 구성**을 그대로 쓴다: 연못+턱(왼쪽 위), 양옆 나무·바위·돌 줄, 양옆 덤불, 양옆 울타리, 꽃 무리 7곳 | 1.3절, 기준서 3.2 |
| R2 | 길은 `ground_path.json` 그대로. F 소품이 길과 겹치면 **바깥쪽(x 부호 방향)으로 민다**(위치만, 종류·개수 그대로). 세운 소품·3D 소품과 길 칸 중심 사이 L1 ≥ 3m(지금 시험과 같은 값). 길가 소품(표지판·길 울타리)만 ≥ 1.2m | `FieldDecorTests.cs:54-58`. F 그대로면 길과 L1 3 안인 것이 14개[계산: 오른쪽 돌·나무 (4.5,−0.4)·(6.1,1.4) 0.9·0.7m 등] |
| R3 | 원정대 시작점 둘레 L1 ≤ 3.5에 세운 소품 0. 노란 꽃(`flower_02`)은 L1 ≤ 6에 0 | F의 가장 가까운 소품 L1 4.9, 넘김 6번 |
| R4 | "보여야 할" 부품(연못·야영지·다리·돌담·표지판)은 V1080 안 | 1.1절 |
| R5 | 화면에 안 보이는 꼭짓점 구역(\|x\| > 24.5 또는 \|y\| > 25.33)에는 아무것도 두지 않는다. 땅 메시만 덮는다 | 1.1절 표의 합 |
| R6 | 테(L1 32~35): 먼 쪽 두 변(북서·북동, y > 0) 너머 2.9m 안에 나무 줄. 가까운 쪽 두 변(y < 0) 너머는 아무것도 없음(화면 아래를 가림) | 1.1절, 초안 3.1 |
| R7 | 같은 시드면 같은 배치. 시드 181108(F8과 같음) | K20 |
| R8 | 밀도는 F 시작 화면을 넘지 않는다(화면 하나 450㎡에 나무 6·바위 4·돌 4·덤불 10·꽃풀 150) | F 측정 |

### 4.2 부품 자리 (바닥 x, y m, [계산]으로 V1080·길 거리 확인)

| 부품 | 자리 | L1 | 길까지 L1 | V1080 | 메모 |
|---|---|---|---|---|---|
| F 연못 + 낮은 턱 + 섬 | 연못 중심 약 (−4.8, 6.5), 턱 F 두 직사각형 그대로 | 11.3 | 9.3 | 안 | 원정대 이동 한계(10.75) 바로 밖. 1280×720 같은 낮은 화면(한계 17.8)에서는 원정대가 연못·턱 위로 갈 수 있다 — D-2 |
| F 나무·바위 줄 | 1.3절 자리, R2로 민 값 | 4.9~11.3 | ≥ 3 | 안 | 굽기 메뉴가 민 결과를 적는다 |
| F 울타리 | (−8.2, 0.3), (8.2, 0.3) | 8.5 | 6.5 / 3.9 | 안 | |
| 길 울타리 | 남서 길 입구 양옆, (−15, −10.5)~(−10, −6) 길에서 1.2~1.8m | 16~26 | 1.2~1.8 | 안 | 컨셉 "길 따라 울타리" |
| 표지판 2 | 남서 길가 (−14.5, −9.5), 다리 앞 (13.5, 10.5) | 24 / 24 | 3.0 / 1.0 | 안 | 다리 앞 것은 길가 규칙(≥1.2m)에 맞게 굽기에서 민다 |
| 개울 | 초안 경로 (3, 31.5) → (9.5, 19) → (13.5, 14.5) → (23.5, 5.5) → (31, 1.5), 폭 약 1.3m, 수면 −0.03m(추정) | 28~34 | — | 일부 | (9.5,19)·(13.5,14.5) 안, (23.5,5.5) 밖 |
| 다리 | 길(세 번째 토막 y = x − 4)과 개울(13.5,14.5)→(23.5,5.5)의 교점 **(16.1, 12.1)**, 긴 축 = 길 방향(1,1) → 요 45° | 28.3 | 0.3 | 안 | 다리 명세 3절 (16, 12)과 같음 |
| 야영지 | 천막 (−16, 6)·(−14, 8.5), 깃발 (−17.5, 8)·(−15, 4.5)·(−13, 10) | 19.5~25.5 | ≥ 17 | 안 | 초안 (−23, 3)은 V1080 밖 |
| 돌담(돌 판 줄) | 북서 (−17, 12)~(−9, 20.5) 6토막, 동쪽 (17, −8)~(20, −4) 4토막 | 24~29.5 | ≥ 20 | 안(21:9에서 북서 끝 일부 밖) | |
| 큰 바위 | F의 1개 + 띠에 5~8개 | | ≥ 3 | 안 | |
| 테 나무 줄 | 북서 변·북동 변 바깥 L1 32.5~34.5, 변 위 \|x\| 10~20.75 구간 | 32.5~34.5 | — | 윗부분만 | R6 |

### 4.3 데이터와 굽기

- 새 데이터 `Resources/Data/Field/field_layout.json`(+ `FieldLayoutData`). 규칙 수치(R2·R3 거리, 밀도, 시드)와 **굽은 결과**(자리·종류·반전·높이)를 함께 둔다. 런타임은 결과만 읽는다.
- 굽기: 에디터 메뉴 `DeadSurvivor/HD2D/Bake Field Layout`(`Editor/FieldLayoutBaker.cs`). F 자리 표(1.3절) + `ground_path.json` + 연못 메시 → 순수 계산 `Features/Field/Utils/FieldLayoutMath.cs`(보이는 팔각형 판정, 길 거리, 밀기, 고정 시드 흩뿌리기·군집) → json. 두 번 구워도 같은 바이트.
- 그림: `Tools/hd2d_field_sketch.py`가 json을 읽어 `output/hd2d-background/layout-main.png`(위에서 본 그림: 마름모·V1080·길·연못·턱·개울·다리·소품 종류별 점·시작 화면 1920×1080 상자). 굽은 개수를 범례에 적는다.
- 칸(json 형태, 이름은 기존 어휘 `placements`·`sprite`·`isFlipped`를 따른다): `seed`, `groundHalfDiagonal`(36 = 들판 32 + 테 4), `path`(반폭 0.67, 폭 흔들림 0.10, 경계 흔들림 0.10 — F8 값), `banks[]`(높이 0.36, 칸 0.125, 직사각형 목록, 가장자리 시드), `water[]`(연못 메시 이름·자리 / 개울 꺾은선·폭·수면 높이), `placements[]`(`kind`: card·prop, `sprite` 또는 `prop` 이름, `x`·`y`(바닥 m), `height`(받침 높이, 턱 위 0.36), `isFlipped`, `yaw`, `isCastingShadow`).
- `decor.json`은 지우지 않는다(되돌리기용). `WorldPresenter`가 더는 읽지 않는다. `FieldDecorTests`의 배치 규칙 시험 3개는 `FieldLayoutTests`로 바꾼다(D-4).

### 4.4 개수 목표 (추정 — 굽기가 센 값으로 확정해 이 표와 기준서 3.3을 고친다)

| 종류 | 목표 | 그림자 | 출처 |
|---|---|---|---|
| 나무 판 | 들판 18~24 + 테 12 안팎 | 발 그림자(투영) | F 밀도 |
| 큰 바위 판 | 6~9 | 발 그림자 | |
| 작은 돌 판 | 16~24 | 없음 | |
| 덤불 판(2종) | 30~40 | 발 그림자 | |
| 꽃·풀포기 판 | 군집 25~30곳, 450~600장 | 없음 | F 150장/화면 |
| 물가 돌 판 | 연못 F 188 + 개울 40~60 | 없음 | F |
| 돌담 토막 | 10(토막당 돌 판 3~5) | 없음 | 초안 |
| 울타리 | F 2 + 길 10 | 발밑 덩어리 2/토막 | F |
| 표지판 · 깃발 | 2 · 3 | 끔 | 기준서 3.12 |
| 천막 · 다리 | 2 · 1 | 해 그림자 냄 | |
| 연못 · 개울 · 턱 | 1 · 1 · F 1세트(+ 개울 둑 D-3) | — | |

## 5. 소품 표 — 2D 판 / 3D

| 소품 | 방식 | 그림·모델 | 크기 [측정·계산] | 외곽선 | 그림자 | 깊이 |
|---|---|---|---|---|---|---|
| 땅·흙길·자갈 | 3D 평면(배경 패스) | `HD2D_Field_Ground` + 풀 질감 + 길 가림 + 흙·자갈 | 마름모 L1 ≤ 36 | 없음 | 받음(하드 0/1) | 그대로 |
| 낮은 턱 | 3D 블록(배경 패스) | `BankMeshMath`, 윗면 = Ground 재질, 옆면 = Bank | 높이 0.36 [F8] | 실루엣 | 받음 | **D-2** |
| 연못 · 개울 | 3D 면(배경 패스) | `HD2D_Field_Water`, 연못 메시 복사 / 개울 띠 | 개울 폭 1.3(추정) | 없음 | 받음 | 그대로 |
| 나무 | 세운 판 | `tree_02`(K23 색) | 75×81px = 1.56×1.69m, 화면 162px(n=2) | 그림 안 1px | 발 그림자 | 그대로 |
| 덤불 | 세운 판 | `bush_01`(51×47 = 1.06×0.98m), `bush_02`(새로 잘라 냄) | | 그림 안 | 발 그림자 | |
| 큰 바위 | 세운 판 | `boulder_01`(52×42 = 1.08×0.88m), 색 0.72배 [F8] | | 그림 안 | 발 그림자 | |
| 작은 돌 · 물가 돌 · 돌담 | 세운 판 | `rock_01`(0.35×0.19m)·`rock_02`(0.58×0.33m), 색 0.72배 | | 그림 안 | 없음 | |
| 꽃 · 풀포기 | 세운 판(**눕히지 않는다**) | `flower_01~03`, `grass_tuft_01~02` | 지금 크기 | 그림 안 | 없음 | |
| 울타리 | 3D 코드 메시(배경 패스) | `PropMeshMath`, `HD2D_Field_Wood` | 0.92×0.12×0.6m [F8 코드] | 실루엣 | 메시 그림자 끔 + 발밑 덩어리 2 | 그대로 |
| 표지판 | 3D 코드 메시 | 기둥 + 판, Wood | 높이 1.0(K28 추정) | 실루엣 | 끔 | 그대로 |
| 깃발 | 기둥 3D + 천 세운 판 | Wood + 새 `flag_01.png`(무료 생성) | 높이 1.6(K28 추정) | 실루엣 / 그림 안 | 끔 | 그대로 |
| 다리 | 3D Tripo + UV 도트 질감(배경 패스) | `Models/HD2D/Bridge.fbx`(= `dot/model.fbx`) + `BridgeAlbedo.png`(512×512, 4색), `HD2D_Prop_Bridge` | 1.80×4.00×0.75m, 588삼각 [측정] | 실루엣 | 냄 | **D-2** |
| 천막 | 3D Tripo + UV 도트 질감(배경 패스) | `Models/HD2D/Tent.fbx`(= `dot/model.fbx`) + `TentAlbedo.png`(512×256, 5색), `HD2D_Prop_Tent` | 1.744×2.320×1.600m, 땅 자국 1.682×2.288m, 천 용마루 1.456m, 490삼각 [측정] | 실루엣 | 냄 | 그대로 |

- 세운 판은 모두 캐릭터와 같은 화면 해상도(배경 패스가 아님), `HD2D_SpriteLit`(M1), 알파 클립, `CardSnapMath`로 격자 맞춤. 배경 패스 3D는 층 27.
- 모든 판·소품에 충돌 없음(지금 2D와 같음). 몬스터는 뚫고 지나간다.

## 6. 다리·천막 가져오기

| 항목 | 다리 `hd2d_bridge` | 천막 `hd2d_tent` |
|---|---|---|
| 원본 모델 | **`output/assets/hd2d-bridge/dot/model.fbx`**(새 UV, 정점 1740·삼각형 588·경계 상자 = `post/`와 같음 [측정, notes]). `post/model.fbx`는 감면 때 UV가 지워져 쓰지 않는다. SHA256은 B4가 `source.txt`에 | **`output/assets/hd2d-tent/dot/model.fbx`**(새 UV, 정점 1460·삼각형 490·경계 = `final2/`와 같음). `final2/`는 쓰지 않는다 |
| 원본 질감 | `dot/albedo.png` 512×512, 4색(W0 #c29f82 · W1 #a38167 · W2 #80624c · W3 #5e4737), 47.9텍셀/m, 채움 17% [측정] | `dot/albedo.png` 512×256, 5색(천 C0 #e0d2b8 · C1 #c9b89d · C2 #a8947b, 나무 P0 #805f4a · P1 #664937), 47.9텍셀/m [측정] |
| 자리 | `Assets/Game/03.Resources/Models/HD2D/Bridge.fbx`, `Textures/HD2D/BridgeAlbedo.png` | `…/Models/HD2D/Tent.fbx`, `Textures/HD2D/TentAlbedo.png` |
| 가져오기 설정 | 모델: 배율 1(미터), 재질 만들기 없음(fbx가 상대 경로로 가리키는 `albedo.png`에서 재질을 만들지 않는다), 애니메이션·리그 없음, 읽기/쓰기 끔, **UV 유지**, 빛맵 UV 만들기 끔, 법선 "계산" 부드럽게 각 0(면마다 3단 툰 — 추정, 캡처로 판단). 질감: Point, 밉 없음, 압축 없음, NPOT 보정 끔, sRGB, Wrap Clamp | 같음 |
| 축 | 긴 축 = Unity Z, 피벗 = 바닥 가운데·가장 낮은 점 y 0(기록). **가져온 뒤 경계 상자로 다시 잰다**(미확인) | 출입구 = +Z, 피벗 같음 |
| 질감 방식 | **UV + 구운 도트 질감**(명세의 삼면 투영을 대체). 재질 `HD2D_Prop_Bridge` = `HD2D_FieldSurface` UV 범주(3절): 알베도 × 툰 띠 × 그림자. 칠한 외곽선 없음(외곽선은 배경 패스 실루엣) | 같음, `HD2D_Prop_Tent`. 천·장대 색은 질감 안에서 이미 나뉨 |
| 자리 놓기 | (16.1, 12.1), 요 45°, y 0. 개울 수면이 아래로 지나간다 | (−16, 6) 요 160°(추정)·(−14, 8.5) 요 200°(추정), 둘째는 배율 0.95(K28 "돌림·크기 ±10%") |
| 그림자 | 해 그림자 냄(배경 패스, 하드) | 냄 |
| 확인 | 에디터에서 `Renderer.bounds.size`를 읽어 1.80×4.00×0.75 ±0.01. 캡처에서 널 이음 줄이 1px 선으로 이어지는지(요 45°라 계단 — notes 미확인 항목) | 1.744×2.320×1.600 ±0.01(배율 1인 것). 0.58m 천 이음 줄이 둘째(0.95배)에서도 보이는지 |
| 카탈로그 | `asset-catalog-hd2d.json` `status` trial → 게임에 넣음(카탈로그 형식이 허락하는 값으로, 미확인) | 같음 |

## 7. 8방향 그림·클립·몬스터 상태 넣기

전제: W4가 `SpriteFacing8` → `SpriteFacing` 이름 옮기기, 5칸 그림·클립 표, `SpriteAtlasService` 접미어·별칭 표(`_front` = `_downright`, `_back` = `_upright`, 몬스터 접미어 없음 = `_downright`), `ResolveAvailable`, 판정 진입점 `FacingFallbackCount`를 끝낸 상태. B5는 **파일만 넣는다**(코드는 W4가 정한 이름을 따른다. 이름이 맞지 않으면 W4 표를 고치고 그림 이름은 바꾸지 않는다).

| 묶음 | 원본 | 넣을 자리(지금 폴더 관례) | 수 [측정] |
|---|---|---|---|
| 대원 정지 | `output/assets/hd2d-8dir/heroes/<id>_{down,right,up}.png` | `Art/AIGenerated/Characters/<id>/{down,right,up}/<id>_<dir>.png` | 12 |
| 대원 걷기 | `heroes/anim/<id>_<dir>_walk/<id>_walk_<dir>_NN.png` | `Characters/<id>/walk_<dir>/<id>_walk_<dir>_NN.png` | bruno·bolt 7×3, lena 6×3, mira 7×2(up 없음) = 74 |
| 대원 공격 | `heroes/anim/<id>_<dir>_attack/…` | `Characters/<id>/attack_<dir>/…` | 6×12 = 72 |
| 몬스터 정지 | `monsters/<id>_{down,right,upright,up}.png` | `Art/AIGenerated/Monsters/<id>/<id>_<dir>.png` | 20(slime·mushroom·rock `_upright` = `_up` 복사 3 포함) |
| 몬스터 상태 | `monsters/states/{boar_telegraph,puffer_inflate}_{down,right,upright,up}.png` | `Monsters/boar/`, `Monsters/puffer/` | 8 |
| 합계 | | | **186장** + 폴더마다 `source.txt`(원본 경로·SHA256·채택 기록 notes 경로) |

- 가져오기: 모두 64×64, 발 = 맨 아래 줄(행 63), 발 가운데 x 31.5~32.5 [측정: notes·bbox]. 프리셋(아래 가운데 피벗 (0.5, 0), PPU 48, Point, 압축 없음, 밉 없음)이 그대로 맞다 — 지금 `bruno_front.png.meta`와 같은 값(`alignment 7`, `spritePivot {0.5, 0}`, PPU 48) [측정]. `.meta`를 손으로 고치지 않는다.
- 아틀라스: `Characters.spriteatlasv2`의 묶음 대상이 `AIGenerated/Characters`·`Monsters` **폴더**라 새 파일이 저절로 들어간다 [측정: packables guid → 폴더 meta].
- 장 수: 데이터 `walkFrames`(bruno 7·lena 6·mira 7·bolt 7)와 새 걷기 장 수가 같다. 공격 6 = `attackFrames` 6 [측정].
- **맞는 프레임 어긋남(D-5)**: 새 위(up) 공격 클립의 맞는 장은 그 대원의 앞 공격과 같게 만들었다(bruno 2, notes). design-brief 3.9는 위 = `hitBack`을 쓴다. bruno만 `hitFront` 2 ≠ `hitBack` 3이라 위 공격 그림이 피해보다 0.1초 먼저 맞는다[계산: 1장 ÷ 10fps]. lena·mira(3=3)·bolt(2=2)는 같다.
- 대신 쓰기: mira 위 걷기만 없다 → `ResolveAvailable`이 오른쪽 위(뒤) 걷기를 쓴다. 넣은 뒤 `FacingFallbackCount` 기대값 = 1(추정 — W4의 세는 방법을 확인해 고친다). 사용자 허용(9크레딧 재접수)이 나면 1장 묶음만 더 넣고 0이 된다.
- 알려진 그림 결함(notes "알려진 결함")은 사람 판정 항목으로 핸드오프에 옮긴다. 고치지 않는다.

## 8. "5단계로 넘김" 8건 — 고칠 것

| # | 지적 | 원인(확인/추정) | 고침 | 단위 | 다시 볼 것 |
|---|---|---|---|---|---|
| 1 | 연못 섬 턱 가장자리 고른 톱니 | `BankStrip`의 주기 5·7 칸 규칙(확인: 코드) | `BankMeshMath`: 가장자리 칸을 고정 시드 무작위 걸음(한 번에 0~2칸, 같은 깊이 2~7칸 이어짐)으로. 직선 구간 길이가 주기를 갖지 않게 | B2 | 근접 잘라 4배(본 화면에서), G20 판독 |
| 2 | 연못 둘레 돌 크기·모양 반복 | `rock_02` 한 종류만 188개(확인: 코드) | `rock_01`·`rock_02` 섞기(약 4:6), 반전 반, 자리 흔들림 ±1텍셀, 연결 성분 중 작은 것 20% 비우기. 그래도 지적되면 새 돌 조각 1~2종(무료) | B1·B3 | 근접 4배, G20 |
| 3 | 넓은 풀밭 단조로움·인물 도트보다 잘다 | 풀 조각이 1텍셀 단위 점으로 흩어짐(K20 "2×2 묶음"이 질감 생성에 없음 — 확인: `GenerateFieldTextures.py`) | ① 풀 결을 2×2텍셀 덩어리로 묶는다(텍셀 크기는 그대로 1/48m), ② 흙 얼룩·자갈 자리를 드문드문(풀밭 넓이의 3~5%, 추정), ③ 꽃·풀 군집을 빈 곳과 섞어 4.4절 밀도로. 밝기 덩어리 ±0.03, 자기상관 ≤ 0.30 유지 | B0·B1 | G15, G20, 풀만 판 |
| 4 | 몬스터 발밑 그림자 약함 | 4단계 몬스터 그림자 = 반투명 눕힌 판(M2) | `HD2D_HardShadowCard`(Point·알파 클립·따뜻한 갈색, 실제 그림자와 같은 명도 배율)로 바꾸고 폭 = 몬스터 그림 불투명 폭(텍셀). 층은 배경(27) — F8과 같음 | B6 | 판 근접, G20 |
| 5 | 몬스터 보라 외곽선 결 | F8 몬스터 재질의 색상 회전 0.68(갈색 외곽선이 보라로 돈다 — 추정) | 본 게임은 회전 없는 `HD2D_SpriteLit`. 회전을 옮기지 않는다. G12(적–바닥 색상 차 ≥ 60°)가 떨어지면 회전 대신 몬스터 그림 쪽 해결안을 핸드오프 `open_questions`로 | B6 | G12, G20 |
| 6 | 베기 효과와 노란 꽃 겹침 | F 꽃 군집 (1, 1.2)이 원정대 바로 옆 | R3: 시작점 L1 ≤ 3.5 꽃 군집 0, 노란 꽃 L1 ≤ 6 0 | B1 | 시작 화면 캡처, G20 |
| 7 | 가장자리 어둡게 밝기 출렁임(seq-1 → 2) | 미확인. 후보: 화면 내용이 바뀌어 가운데 밝기가 바뀐 측정 착시, 볼륨 값 변화 | 이동 8장 각각 모서리 ÷ 가운데 밝기(C15)를 재서 표로. 볼륨 값(Vignette 세기·부드러움)이 장마다 같은지 기록. 값이 같고 비만 출렁이면 측정 착시로 적고 끝. 값이 바뀌면 원인 코드만 고친다(값 다듬기는 6단계) | B8 | C15 표 |
| 8 | 턱 테/윗면 0.93(재질 1.2) | 측정 상자에 그늘·옆면이 섞임(추정) | 조명 진단 판(`_Diagnostic` 2: 윗면 빨강·테 초록·옆면 파랑)으로 가린 픽셀만 재기. 1.1 미만이 실제 값이면 테 배율만 올린다 | B8 | C10 |

## 9. K28 실측 고침 글 (B4가 기준서 3.1 "3D 소품 크기" 표에 넣는다)

다음 행으로 바꾸고, 표 아래 "모두 추정" 문장을 "다리·천막·판 그림은 측정, 나머지는 추정"으로 고친다. K28 결정 칸 끝에 "r6.2: 다리·천막·세운 판 크기 실측(5단계 B4)"을 붙이고 4절 개정 기록에 r6.2 한 줄을 더한다.

```
| 다리 | 폭 1.80 × 길이 4.00 × 높이 0.75 (588삼각) [측정: output/assets/hd2d-bridge/post/post.json size_after_xyz_m = dot/model.fbx 경계, Unity 경계 상자 B4] | 길이 방향 화면 폭 약 272px(요 45°, n=2) [계산] | 높이만 0.865배 눌림(기록) | Tripo 기록 |
| 천막 | 전체 폭 1.744 × 길이 2.320 × 높이 1.600, 땅 자국 1.682 × 2.288, 천 용마루 1.456(장대 머리 0.14 솟음) (490삼각) [측정: output/assets/hd2d-tent/dot/notes.md, Unity 경계 상자 B4] | 정면 폭 약 167px, 장대 머리까지 높이 약 133px(n=2) [계산] | 바닥이 2.0×2.0(r6 추정)과 다르다 — 모양 비율을 지키려 세 축 같은 배율(디렉터 11-18). 2개, 둘째 0.95배 | Tripo 기록, 도트 질감 notes |
| 나무(세운 판) | 1.56 × 1.69 (75×81px) [측정] | 162px | tree_02 = tree_01 K23 색 | F8 |
| 큰 바위(세운 판, r6.1) | 1.08 × 0.88 (52×42px) [측정] | 104×84px | 색 0.72배 | F8 |
| 작은 돌(세운 판) | 0.35×0.19 / 0.58×0.33 (rock_01 17×9, rock_02 28×16px) [측정] | | 돌담·물가 돌도 이 판 | F8 |
| 덤불(세운 판) | 1.06 × 0.98 (bush_01 51×47px) [측정] | | bush_02는 B0 측정값 | F8 |
| 울타리 토막 | 폭 0.92 × 0.12 × 높이 0.60 [측정: FieldLookdevScene8.Fence 코드] | | 기둥 2 + 가로대 2 | F8 |
| 낮은 턱 | 높이 0.36 [측정: plates8.json ledge.worldHeight] | | | F8 |
```

3.2 재질 표 "다리·천막" 행의 만드는 방식은 "3D(Tripo) + **새 UV에 구운 도트 질감**(`dot/albedo.png`, 48텍셀/m), 배경 도트화 패스"로, 대표색은 도트 notes 팔레트(다리 W0~W3, 천막 C0~C2·P0~P1)로 고친다. "바위 지름 약 0.8(boulder045 ×0.9)", "작은 돌 0.45~0.56(stone045)", "돌담 조각 3.0×0.75", "높은 턱 약 1.2"(D-3 결정에 따라) 행은 지운다.

## 10. 작업 단위

- 순서대로 하나씩(에디터 하나). 단위마다 커밋 하나(지정 파일만). 구현은 Codex 실행기.
- 확인은 **그 단위에서 바뀐 범위만**. "캡처 1장"은 1920×1080, 실행한 쪽이 직접 열고 핸드오프에 "본 것"을 적는다. 근접은 본 화면 캡처를 잘라 Point 4배(F8 방식, 상자 `[420, 96, 1644, 804]`)이고 따로 그리지 않는다.
- 새 판정 흐름 파일 `Tools/playable/hd2d-background-flows.json`(B2에서 만든다): 시작 화면 → 키 이동으로 북동(다리)·서쪽(야영지)·북서(돌담)·남서(길 입구) 이동 한계까지 → 단계마다 캡처. 기존 판정 파일의 기대식은 바꾸지 않는다(GND만 D-4).
- 성능 측정 없음. `art-insert-flows.json`을 돌리다 지나가는 `PERFREC` 기록은 열어 보지 않는다(7단계).

| 단위 | 하는 일 | 바꾸는 파일 | 완료 조건 | 그 단위의 확인 |
|---|---|---|---|---|
| **B0 그림·질감·셰이더** | 3절 B0 행: 질감 생성기, 풀만 질감(넘김 3번 ①), 흙 결·자갈(Codex 내장 생성 + `pixelize.py`, 무료)·물·길 가림 질감, 덤불 2종·나무 다시 칠·큰 바위 복사, 셰이더 3(`HD2D_FieldSurface`(UV 질감 범주 포함)·`HD2D_HardShadowCard`·`HD2D_FootShadow`)·재질 8. 다리·천막 `albedo.png` 복사(`Textures/HD2D/BridgeAlbedo.png`·`TentAlbedo.png`). **게임 연결 없음** | `Tools/hd2d_field_textures.py`, `03.Resources/Textures/HD2D/*.png`(새 폴더), `Art/AIGenerated/Decor/{tree_02,bush_01,bush_02,boulder_01}.png`+`source.txt`, `Textures/HD2D/{Bridge,Tent}Albedo.png`+`source.txt`, `Shaders/HD2D/` 3, `Materials/HD2D/` 8 (약 22) | 컴파일 0. 생성기를 두 번 돌려 출력 SHA256 같음. 질감 가져오기 = Point·밉 없음·압축 없음·NPOT 보정 끔(마스크·결 질감은 선형) | 컴파일. `-testFilter "ArtImportSettingsTests;FieldDecorTests"`(Decor 폴더에 그림이 늘어 깨지는지만). 질감·판 그림을 직접 열어 봄(4배). Play 없음 |
| **B1 배치 데이터** | 4.3절 굽기 메뉴·순수 계산·json·스케치. 넘김 2·3③·6번의 배치 몫. 연못 메시 복사 | `Features/Field/Data/FieldLayoutData.cs`, `Features/Field/Utils/FieldLayoutMath.cs`, `Editor/FieldLayoutBaker.cs`, `Resources/Data/Field/field_layout.json`, `03.Resources/Meshes/HD2D/FieldPond.asset`, `Tools/hd2d_field_sketch.py`, `Tests/EditMode/FieldLayoutTests.cs`, `FieldLayoutMathTests.cs` (약 9) | 4.1 R1~R8을 시험이 확인(길 거리, 시작점 비움, 노란 꽃, V1080, 꼭짓점 구역 0, 가까운 쪽 테 0, 같은 시드 같은 json). 스케치를 직접 열어 4.2 표 자리와 대조 | 컴파일. 굽기 메뉴 2회(바이트 같음). `-testFilter "FieldLayoutTests;FieldLayoutMathTests"`. 스케치 직접 봄. Play 없음 |
| **B2 땅·길·턱·물** | `FieldView`가 땅 메시(L1 ≤ 36)·턱(`BankMeshMath`, 넘김 1번)·연못·개울을 배경 층으로 만든다. 타일맵은 **데이터를 남기고 렌더러만 끈다**. `WorldPresenter`가 `decor.json` 대신 `field_layout.json`. DI 등록 줄 바꿈. 이행 메뉴(재질 연결·질감 가져오기 설정, 두 번 돌려도 같음). 판정 진입점 추가(`FieldGroundVisibleCount`, `FieldPartCount(kind)` — 이름 추정). 새 흐름 파일 | `Features/Field/View/FieldView.cs`, `Features/Field/Presenter/WorldPresenter.cs`, `App/GameSceneLifetimeScope.cs`, `Effect/HD2DView/Utils/BankMeshMath.cs`·`RibbonMeshMath.cs`, `Editor/MainSceneHD2DBackgroundMigration.cs`, `01.Scene/Main.unity`, `App/PlayableProbe.cs`, `Tools/playable/hd2d-background-flows.json`, (D-4) `art-insert-flows.json` GND 행, 시험 `FieldViewBuildTests`·`BankMeshMathTests` (약 12) | 컴파일 0. 시작 화면에 F와 같은 풀 결·흙길(본 게임 길 모양)·연못·턱. 턱 가장자리가 고른 톱니가 아님. `GroundTileCount` 판정 그대로 통과 | 컴파일. `-testFilter "FieldLayoutTests;FieldViewBuildTests;BankMeshMathTests;GroundTilePickerTests;FieldDecorTests"`. `playable.py --route Tools/playable/art-insert-flows.json --until GND` 1회. `hd2d-background-flows.json` 시작 화면 단계 1회 → 캡처 1장 + 연못 섬 근접 4배 직접 봄 |
| **B3 세운 판 배경** | 나무·덤불·큰 바위·작은 돌·물가 돌·돌담·꽃·풀포기를 세운 판으로(`CardSnapMath` 격자 맞춤), 발 그림자 `UprightFootShadow`(턱 위 받침 높이 0.36). 꽃·풀은 눕히지 않는다(W3 `AddDecor`의 눕힘을 대체) | `FieldView.cs`, `Effect/HD2DView/View/UprightFootShadow.cs`, `Effect/HD2DView/Utils/CardSnapMath.cs`, 시험 `UprightFootShadowTests`·`CardSnapMathTests`, `FieldViewBuildTests` (약 6) | 시작 화면이 F8 구성과 같은 결(인물·판 도트 같은 크기, 발 그림자가 밑동에서 시작). 물가 돌이 섞여 보임(넘김 2). 원정대 옆 노란 꽃 없음(넘김 6) | 컴파일. `-testFilter "FieldViewBuildTests;UprightFootShadowTests;CardSnapMathTests"`. `hd2d-background-flows.json` 시작 화면 1회 → 캡처 1장 + 근접 4배 직접 봄 |
| **B4 3D 소품** | 울타리·표지판·깃발 기둥(코드 메시)·깃발 천(새 판, 무료 생성)·다리·천막(6절). 그림자 규칙(5절). K28 고침(9절), 카탈로그 상태 | `Effect/HD2DView/Utils/PropMeshMath.cs`, `FieldView.cs`, `03.Resources/Models/HD2D/{Bridge,Tent}.fbx`(= `dot/model.fbx`)+`source.txt`, `Art/AIGenerated/Decor/flag_01.png`+`source.txt`, 이행 메뉴(모델 가져오기 설정), `docs/art/아트-기준서.md`(3.1 표·K28·4절), `docs/art/asset-catalog-hd2d.json`, 시험 `PropMeshMathTests` (약 10) | 다리·천막 경계 상자 6절 값 ±0.01, UV 유지(질감이 늘어나지 않음 — 4배 근접에서 텍셀 = 배경 1px). 실루엣 갈색 외곽선, 3단 칠, 하드 그림자. 울타리 메시 그림자 0·덩어리 2/토막 | 컴파일. `-testFilter "PropMeshMathTests;FieldViewBuildTests"`. 경계 상자 읽기(`script eval` 1회, Play 아님). `hd2d-background-flows.json` 북동·서쪽 단계 1회 → 다리·야영지 캡처 2장 직접 봄 |
| **B5 8방향 그림 넣기** | 7절 186장 + `source.txt`. D-5 결정 반영(데이터 칸을 더하면 그 줄만) | `Art/AIGenerated/Characters/**`, `Monsters/**`(새 파일만), (D-5 나)면 `Data/Character/bruno.json` 한 칸 | `FacingFallbackCount` = 기대값(7절). 8방향 이동마다 맞는 그림·클립. 몬스터 4방향 + 반전, 예고·부풂 방향별 | 컴파일. `-testFilter "SpriteAtlasServiceTests;MemberViewSpriteTests;MonsterViewStateTests;SpriteFacingTests"`. `art-insert-flows.json --until END4` 1회(B1·DR~STOP·MON·TEL·INF). `hd2d-background-flows.json`의 8방향 이동 캡처 8장 → 한 장 시트로 직접 봄. (D-5 나)면 `playable.py --until G7` + `wave_targets.py --metrics` 1회 |
| **B6 몬스터 그림자·외곽선** | 넘김 4·5번 | `Features/Monster/View/MonsterView.cs`, `Resources/Prefab/MonsterView.prefab`, (필요하면) `FootShadowView.cs`, 시험 `MonsterViewStateTests` (약 4) | 몬스터 발밑 하드 그림자가 대원·나무 그림자와 같은 명도. 몬스터 재질 색상 회전 0 | 컴파일. `-testFilter "MonsterViewStateTests"`. `art-insert-flows.json --until MON` 1회 → 캡처 1장 + 몬스터 근접 4배 직접 봄 |
| **B7 가려짐(D12)** | 원정대 4명을 가리는 키 큰 소품(나무·큰 바위·천막·깃발·표지판)을 디더로 비침. 공유 "비침" 재질로 바꿔 끼움(재질 속성 블록 안 씀 — SRP 배칭 유지). 대원 실루엣은 D-7 | `Effect/HD2DView/Utils/OcclusionMath.cs`(순수: 화면 사각형 겹침 + 앞뒤), `Features/Field/Presenter/FieldOcclusionPresenter.cs`(새, ITickable), `GameSceneLifetimeScope.cs` 등록 줄, `HD2D_SpriteLit.shader`·`HD2D_FieldSurface.shader` 디더 속성, 비침 재질 2, 시험 `OcclusionMathTests`·`FieldOcclusionPresenterTests` (약 8) | 대원이 나무 뒤에 서면 나무가 디더, 벗어나면 원래대로. 비침 판수 ≤ 대원 수 × 겹친 소품 | 컴파일. `-testFilter "OcclusionMathTests;FieldOcclusionPresenterTests"`. `hd2d-background-flows.json` "나무 뒤" 단계 1회 → 캡처 1장 직접 봄 |
| **B8 본 게임 관문 판** | 3절 B8 행. 넘김 7·8번 측정. `gate.py check`(킷 `feat/25d-art`, `gate.default.json` — G19 `deferred` 그대로) | `Editor/HD2DMainPlateCapture.cs`, `Tools/hd2d_main_gate.py`, `output/hd2d-관문/main1/**` (약 2 + 결과) | 자동 검사 G01~G18 PASS(G19 N/A, G20은 디렉터 판독 대기). C10·C15 표 | `hd2d_main_gate.py plan` → Play 캡처 → `finish` → `gate.py check`. 실패하면 게임 결함/검사 결함을 먼저 가른다. 문턱·관문 파일을 고치지 않는다 |
| **B9 마감** | 고치지 않는다. 마감 검사·대조표·리뷰 | `docs/art/아트-기준서.md` 3.3 대조표(행마다 다시 셈)·5절 넘김 표 처리 표시, 이 문서 진행 표, 핸드오프 | 1·10절 조건 전부. 대조표 11~24행 차이 0(또는 D-3·D-5 결정대로 받아들인 차이와 원문) | **판정 경로 전체 1회**(`tools/playable.py`, G1~G8) — 게임 동작 파일(뷰·데이터·DI·장면)이 바뀌었으므로. **EditMode 전체 1회**. 4화면비 캡처(1920×1080, 2340×1080, 2520×1080, 2048×1536) 직접 봄. 마감 리뷰 묶음 1회(병렬): validator-client, spec-reviewer, algorithm-tester(`FieldLayoutMath`, `BankMeshMath`, `CardSnapMath`, `OcclusionMath`). FAIL만 수정 확인 1회 |

- 규칙 시험(가방·전투·웨이브·부활)은 고치지 않고 통과해야 한다. 예외는 D-4(장식 배치 시험 교체)와 D-5 (나)뿐이다.
- 파일 수는 추정(시험 포함, 메타 제외).

## 11. 마감 판정

| 항목 | 기준 |
|---|---|
| 컴파일·EditMode | 오류 0, EditMode 전체 통과(B9 1회) |
| 판정 경로 | `tools/playable.py` G1~G8 통과(B9 1회). 웨이브 목표값 `wave_targets.py --metrics` 42행 OK(M7은 기록만) |
| 관문 | 본 게임 판 `gate.py` 자동 검사 PASS(G19 N/A), G20 두 판독자 5축 ≥ 3·큰 결함 0(디렉터, `Tools/hd2d_judge.py`) |
| 넘김 8건 | 8절 "다시 볼 것" 전부 기록 |
| 범위 대조표 | 기준서 3.3 11~24행과 1~3·6·7행 다시 셈(1~3·6·7은 B5 뒤 0, 2행은 mira 위 걷기 −1 남음 — 사용자 허용 대기) |
| 캡처 | 시작 화면·다리·야영지·돌담·길 입구·8방향 시트·4화면비를 실행한 쪽과 디렉터가 각각 직접 엶 |
| 성능 | **판단하지 않음**(7단계) |

## 12. 디렉터가 정할 것 (권장값)

| # | 정할 것 | 후보 | 권장 | 이유 |
|---|---|---|---|---|
| D-1 | 길 | (가) 본 게임 `ground_path.json` 그대로, F 소품을 민다 / (나) F 길(가로 z ≈ 2.8 띠)로 길 데이터를 바꾼다 | **(가)** | 길 데이터는 판정 GND·`GroundTilePicker`·장식 시험이 쓴다. F 구성 요소(연못·턱·줄·울타리·덤불·꽃)는 그대로 남는다 |
| D-2 | 사람·몬스터가 낮은 턱·다리 위를 지날 때 아랫부분이 묻힘(턱 0.36m, 다리 바닥판 0.15~0.45m — 바닥 평면에서만 걸음) | (가) 턱 윗면·옆면과 다리가 **깊이를 바닥 높이(y 0)로 쓴다**(`_FlatDepth`, 셰이더 한 곳, 게임 코드 0) / (나) 보기 전용 높이(뷰 위치에 받침 높이를 더함 — 대원·몬스터·효과·표시 소비처 여럿) / (다) 턱을 들판 밖에만(화면에 안 보여 F 구성을 잃음) | **(가)** | 게임 규칙·좌표를 안 건드린다. 대가: 턱·다리 뒤에 선 것도 가려지지 않는다(0.36m라 작음, 추정). B2 캡처로 확인하고 어색하면 (나)로 |
| D-3 | 높은 턱(1.2m)·폭포 | (가) 뺀다(대조표 14행 2→1, 22행 4→3) / (나) 개울 시작점에 들판 안 낮은 턱(0.36m) 작은 폭포 / (다) 테에 둔다(안 보임) | **(나)** | 대조표 행을 지키고 화면에 보인다. (다)는 1.1절 계산상 보이지 않는다 |
| D-4 | 장식 배치 시험·판정 | `FieldDecorTests` 배치 규칙 3개(가장자리 띠 16, 시작 생성 원 밖)를 `FieldLayoutTests`(R1~R8)로 바꾸고, `art-insert-flows.json` GND 기대식에 땅 메시가 화면에 있음을 **더한다**(타일 조건은 남김) | **바꾼다** | 2D 장식 규칙이 F 구성(시작 화면 소품)과 맞지 않는다. 판정을 낮추지 않고 더하기만 한다. 바꾼 사례와 이유를 커밋 메시지·검증 기록에 |
| D-5 | bruno 위 공격 맞는 장(그림 2, 데이터 `hitBack` 3) | (가) 그대로 두고 0.1초 어긋남을 기록 / (나) `bruno.json`에 방향별 칸(design-brief 3.9 "새 칸은 선택, 기본 −1") 위 = 2 → G7·웨이브 다시 잼 | **(가)** | 게임 시간이 그대로라 판정 경로·웨이브 목표를 다시 잴 일이 없다. 사용자 판정 때 같이 보인다 |
| D-6 | (해결됨) 천막 천과 장대 색 나누기 | — | — | 도트 질감 굽기에서 천(C0~C2)·나무(P0~P1)로 이미 나뉨(`hd2d-tent/dot/notes.md`). 명세 7절 미정 항목 끝 |
| D-7 | 가려짐 범위 | (가) 소품 디더만 / (나) 디더 + 대원 실루엣(스텐실, 사전 조사 D12) | **(가)** | 대원 4명이 소품 뒤에서 사라지지 않으면 실루엣이 필요 없다. B7 캡처로 대원이 안 보이는 사례가 나오면 (나) |
| D-8 | 꽃·풀포기 판 수(450~600장) | (가) 목표대로 / (나) 화면 밖은 줄임 | **(가)** | 성능은 7단계에서 한 번 본다. 줄일 때 같은 굽기에서 밀도만 낮추면 된다 |
| D-9 | 규모·검토 | L1(설계 검토 없음, 마감 리뷰 묶음 1회) / L2(design-reviewer 먼저) | **L1** | 상태 소유권·DI 구조·세이브·렌더 파이프라인 설정을 바꾸지 않는다(work-levels L2 조건 아님) |

## 13. W3 최종본과 부딪히는 곳 (b31974b 재확인)

2026-11-18 Codex 실행에서 `ad033bc`~`b31974b` 최종 소스를 직접 읽었다. 아래 몬스터 그림자 행은 최종 W3 이후 수정으로 이미 해결됐다. 나머지 FieldView·카메라·층 표는 최종본과 같다.

| W3 지금 상태 | 5단계 | 처리 |
|---|---|---|
| `FieldView.AddDecor`: `IsGround`(꽃·풀) 판을 **눕혀** 배경 층(27)에, 서 있는 판은 층 0 + 타일맵 재질 | 꽃·풀은 **세운 판**(K18·F8), 판마다 재질 `HD2D_SpriteLit`. `AddDecor`는 배치 데이터 기반 만들기로 대체 | B3에서 `FieldView`를 W3 최종본 위에 다시 쓴다 |
| 이행 메뉴가 타일맵을 배경 층으로 옮김(렌더러 켬) | 땅은 3D 메시, 타일맵 렌더러 끔(데이터는 남김) | B2 이행 메뉴가 렌더러만 끈다. W3 메뉴를 다시 돌려도 렌더러를 켜지 않는다(층만 바꿈) — 확인 |
| `TiltedCameraRig.Apply`: 바라보는 점을 바닥 텍셀 격자(x 1/48, z 1/(48·sin θ))에 맞춤 | F8 `SnapCamera`는 카메라 오른쪽·위 축 격자 + **반 픽셀 위상**(출력 크기 ÷ (2·배율)의 소수부) | 1920×1080·2340×1080·2520×1080·2048×1536은 위상 0이라 같다[계산]. 2532×1170은 1170 ÷ 4 = 292.5 → 위상 0.5라 배경이 반 픽셀 어긋날 수 있다(추정). 5단계 판정 화면 밖이라 핸드오프 열린 항목으로만 |
| `HD2DBackgroundRig.Sync`: 배율 = RoundToInt(H ÷ (size·2·48)) | 같은 값 | 없음 |
| design-brief 3.8: "5단계 세운 판 식물도 `UprightShadowCaster`" | F8은 장식 판의 실제 그림자 캐스터를 끄고 정적 투영 발 그림자(`PixelFootShadow`)를 썼다(관문 통과 방식) | 장식 판은 `UprightFootShadow`, 대원만 `UprightShadowCaster`. design-brief 3.8 문장은 B9에서 이 결정으로 고친다 |
| 4단계 최종 몬스터 발 그림자 = `HD2D_HardShadowCard` 공유 판(`b31974b`), `_ReceiveShadows` 0 `MonsterLit` | 하드 그림자 판은 이미 연결됨. 불투명 폭·색상 회전 없는 재질과 판정만 확인 | B6에서 중복 구현하지 않음 |
| 층 26 = `WorldIndicator`, 27 = 배경 | 5단계 새 층 없음 | 없음 |

## 14. 열린 항목 · 추정

- 다리·천막의 Unity 안 실제 축·크기(B4에서 경계 상자로 잼). 법선 "부드럽게 각 0"이 판독에서 각진 면으로 읽히는지.
- 다리·천막 도트 질감의 Unity 안 모습(실제 해 방향·그림자, 다리 요 45°에서 널 이음 줄 계단, 천막 0.95배에서 이음 줄 간격). 도트 notes의 미리보기는 흉내이고, 실루엣 문턱을 0.25m로 흉내 냈다(본 게임은 K19 0.055).
- UV 질감 범주의 띠·그림자 곱이 notes 흉내(F8 `FieldSurface` 값)와 같은 색을 내는지 — B4 캡처 색으로 잰다.
- 개울 폭 1.3m·수면 −0.03m, 꽃 450~600장, 자갈·흙 얼룩 3~5%는 추정. 굽기·캡처로 확정한다.
- D-2 (가)의 모습(턱 위 사람이 턱 앞면과 겹쳐 보임)은 추정. B2 캡처로 본다.
- `FacingFallbackCount` 기대값 1은 W4의 세는 방법에 달렸다(추정).
- 넘김 5·7번의 원인은 추정. 7번은 측정으로 원인을 가른다.
- 판정 진입점 새 이름(`FieldGroundVisibleCount`, `FieldPartCount`)은 제안이다. 기존 어휘를 grep으로 확인하고 정한다.
- 2532×1170 반 픽셀 위상(13절).
- mira 위 걷기 1클립(9크레딧 재접수)은 사용자 허용 대기(완성 실행 계획 5d-a).
- 떠다니는 입자(대조표 27행 일부)는 6단계로 둔다.

## 15. 하지 않는 것

- 성능 측정(300마리·버닝·PERF 판정) — 7단계. 빛·화면 효과 값 다듬기 — 6단계. UI — U단계. 밤 — 2단.
- 시안 Round1~8 파일, 예전 3D 작업물(`Assets/DeadSurvivor/`, `Assets/Scenes/`, `3dkit/`), 관문 도구(킷) 수정. 기존 판정 파일의 기대식 낮추기. push.
- 유료 생성. 새 그림은 무료 Codex 생성 + `pixelize.py`만.

## 16. 진행 표

| 단위 | 상태 | 커밋 | 근거 |
|---|---|---|---|
| B0 | 완료(연결은 B2부터) | `ef375e7` | attempt1 evidence: 컴파일·셰이더·지정 시험12개·질감 해시 동일 |
| B1 | 완료 | `26ee61b` | 복구 후 최신 소스 재굽기2회 바이트 동일, FieldLayout7+Math6 PASS, 스케치 직접 확인 |
| B2 | 완료 | `4f46825` | 땅·길·턱·물, 지정34시험·GND·START PASS, FlatDepth 유지 |
| B3 | 완료 | `d28c653` | 정적 판·투영 접촉 그림자, 지정7시험·START PASS, bush02 잘린 단면 수정 |
| B4 | 정상 이동 재확인·최종 채택 대기 | `2c36982` | 다리588·천막490삼각 UV·경계 실측, 지정5시험 PASS. 다리 승인 위치는 유지, NE 화면에 하단만 보여 전체 실루엣 확인은 제한됨 |
| B5 | 완료(원본 미제공1클립 명시) | `2bba272` | W4 정지40장 유지+클립146장, 지정48시험·8방향·END4 PASS. mira_up_walk 없음, bruno 위 타격0.1초 차(D-5) |
| B6 | 완료 | `a75f091` | 몬스터 그림자 폭·27레이어·하드 명도, HueShift0. 지정13시험·MON PASS |
| B7 | 완료 | `99a1cf5` | OcclusionMath6+Presenter2 PASS, TREE→CLEAR PASS·직접 확인. 공유 비침3재질(스프라이트·나무·천막 UV 보존), 설계2개와 차이 명시 |
| B8 | 수정 판 자동13개 PASS, G20 대기(전체 관문 FAIL) | `fd4e474` | `output/hd2d-관문/main1/gate-report.json`: G01~G18 지정13개 PASS, G19 기존 deferred N/A, G20 점수 없는 NOT_RUN 슬롯만 있음. C10 테1.202665·옆면0.439001. C15 유효 볼륨 값 일정, 6단계 조정 없음 |
| B9 | 이전 명령 관문 통과·수정 확인/판독 대기 | 본 마감 문서 커밋 | G1~G8 8/8 PASS(158.5초), EditMode469/469 PASS, wave_targets42행 OK·M7 미측정, 전투4화면비·battle-main 직접 확인. 리뷰는 Claude 세션에 요청, 아직 실행 안 함 |

attempt2 근거: `.director/evidence/20261118-hd2d-background/1/2/`의 `progress.json`, 단위별 명령 JSON·플레이 로그. 전체 경로·EditMode 전체는 B9에서 각각 한 번만 실행했다. B8 반복은 수집 결함과 실패한 관문 범위만 재수집했다. 관문 도구·문턱은 바꾸지 않았다.

### 16.1 구현에서 확인한 차이·남긴 제한

- W3 최종은 이미 `HD2D_HardShadowCard`·MonsterLit ReceiveShadows0을 쓰고 있었다(13절). B6는 재구현 대신 불투명 폭·명도·레이어를 맞췄다. 대원 캐스터는 유지, 정적 식물은 `UprightFootShadow`로 분리했고 화면 설계 문구도 맞췄다.
- F8 정적 판은 native paint에 연속 diffuse를 곱했는데 W3 몸 셰이더는 diffuse를 3단으로 나눈다. B8에서 `_BackgroundCard`를 배경 공유 재질에만 켜 F8 계산을 복구했다. 대원·몬스터 값과 6단계 빛·후처리 값은 바꾸지 않았다.
- bush02는 단순 잘라내기에서 수직 단면이 보여 무료 내장 편집 생성+pixelize로 바꿨다(45×35px). 깃발은 생성 원본 도트 격자가65×38이라 그 격자를 보존했다.
- 다리는 승인 위치(16.1,12.1)에 있고 수량·UV·경계 확인은 통과했으나, B4 이동 캡처에서 전체가 화면에 들어오지 않았다. B9 추가 이동 시험은 정상 중심 제한10.75m 밖인 x11.6m를 기다려 실패한 잘못된 setup이었다(웨이브 정비로 끝남). 정상 제한은32−10−11.25=10.75m다. 모델을 옮기지 않고 진단 focus(8.5,5)에서 양끝 데크·UV·그림자를 직접 봤으며 우측 나무 겹침은 남는다. 진단을 정상 플레이 전체 실루엣 채택으로 적지 않는다.
- C15 모서리/중앙 밝기 비는1.0515→1.0316으로 목표0.6~0.8과 다르다. 8장 유효 스택 Vignette 세기0.2·부드러움0.2는 동일하다. 8절 넘김7 지시에 따라 내용에 따른 비율 변화로 기록하고, 효과 값 조정은6단계에 둔다.
- 기준서3.3 재계수는 차이0인25행·남은18행이다. 이 단계의 배경 범위와 r6 전체 아트 완료를 구분한다. `mira_up_walk`·입자·UI·글꼴 목표가 남아 있다.

### 16.2 놓친 점·워크플로우 갱신 후보

- 재컴파일 직후 연결 재시작 중 실행된 이행 호출은 실패했는데 파일 해시만 비교하면 성공처럼 보였다(B2). 명령 봉투 성공·실제 재질 참조·다음 ready를 함께 확인하도록 실행 래퍼를 고쳤다.
- 캡처 동안 게임 시계와 이동 입력이 계속 흐르면 방향 시험이 경계 슬라이드나 다음 웨이브를 찍었다(B5·B7). 캡처 setup에서만 정지하고, 원래 기대식·문턱은 보존했다.
- F8 수집기는 독립 PNG와 한 볼륨 프로필을 가정했다. 본 게임 아틀라스 clone은 원본 PNG 읽기가 실패하므로 textureRect/offset GPU readback으로, 볼륨은 실제 스택으로 바꿨다. 화면 영역은 스프라이트 꼭짓점과 두 화면 축으로 사전 선언했다(B8).
- G09 작은 색차는 ARGB32 출력의1 RGB 변환 흔들림에 민감했다. HDR 수집 타깃→표준 linear-to-sRGB PNG 인코딩으로 방법을 바꾸고, 소품 전체 윤곽을 포함하는 영역을 사전 선언했다. 진단 판은 후처리와 focus feature 모두를 끄고 반드시 복구한다. 결과 이미지를 손질하거나 관문 문턱을 낮추지 않았다.
- 계수는 종 이름을 수기로 쓰지 않고 저장소 JSON id를 읽는다(초안 bat 오기를 실제 mushroom으로 고침). 같은 판의 풀·꽃 합450을 꽃450으로 부르지 않고 풀298+꽃152로 나눈다.
- 위 후보는 이 작업의 실행 래퍼·수집기·기록에 반영했다. 공통 워크플로우 문서 갱신은 지정 파일 범위 밖이므로 하지 않았으며, 디렉터가 작업 후 반영 여부를 결정할 항목으로 남긴다.

### 16.3 마감 리뷰 필수 수정(수정 확인 1회 요청 전)

- 수신 판정: validator-client `VERDICT: FAIL`, spec-reviewer `VERDICT: FAIL`, algorithm-tester `VERDICT: FAIL`. 독립 수정 확인은 아직 `NOT_RUN`이다.
- 재리뷰 기준: 코드 수정 구간은 `ee8eed0` 이후. 승인 문서는 디렉터 입력 커밋 `fe200b8`의 사본·SHA256으로 고정했다(`.director/evidence/20261118-hd2d-background/1/3/approved-input/manifest.json`). B4의 K28·실측·UV 문구와 B9의 범위 계수·진행/넘김 기록·정적 식물 그림자 문구 갱신은 원 지시가 허용했다. 이를 수용 기준을 바꾼 것으로 분류하지 않는다. 이 문서1~15절의 수용 기준은 고치지 않는다.
- 턱: 직사각형별 깎기 대신 합집합을 먼저 만들고, 실제 노출 외곽에서 안쪽0~2칸만 깎는다. 실제 F8 시드의 (-42,66)·(-31,66) 및5×5 합집합 내부 보존 회귀 시험을 추가했다. 윗면은 땅과 **같은 groundMaterial 객체**, 옆면은 bankMaterial이다. 공유 땅 재질 FlatDepth1과 높이별 물 마스크 조건으로 D-2를 유지했다.
- 길: 배치 데이터 path.halfWidth=.67·widthVariation=.10·edgeVariation=.10이 실제 마스크 경계에 곱해진다. 시드는 다른 질감 생성 흐름과 분리했다. 고정 폭·폭 변화·경계 변화 및 결정성 시험을 넣었다.
- 경로: DIR8 뒤 MOVE_NE가 timeScale을 재개하고 정상 조이스틱 입력으로 L1 중심 한계10.75m의 목표 근처까지 간다. 서쪽·북서·남서도 같은 도착 검사로 확인한다. 자동 다음 웨이브만 켜 정비로 멈추는 수집 오류를 피하며, 이동·배치·카메라 제한은 바꾸지 않는다. 돌담 판40장의 가시성 등록을 추가했다.
- 정상 북동 캡처에서도 다리의 위·오른쪽 일부는 화면 밖이다. 전체 실루엣 확인으로 승격하지 않는다. 이 배치를 그대로 채택할지, 다리·개울 교차 위치를 함께 조정할지는 디렉터 판단 항목이다. 제한 밖 진단 캡처를 정상 이동 증거로 대체하지 않는다.
- G20은 디렉터가 새 판으로 두 판독을 마치고 관문 결과를 갱신해야 한다. 실행기는 이를 수행한 것으로 적지 않는다. 이전 전체 EditMode469건·G1~G8 통과는 `ee8eed0` 시점의 근거로 보존하고, 이번에는 수정 범위 시험·정상 이동·본 게임 아트 판만 다시 확인한다.

- 수정 범위 실행 결과: 컴파일 오류0, BankMeshMathTests3/3·FieldViewBuildTests2/2·Python 길 마스크2/2 통과, 질감2회 생성 SHA256 동일. 수정 흐름30/30 통과(172.2초), 더 잘 보이는 북서 목표(-7.5,3.25)로 북서·남서6/6 추가 통과(55.0초). 새 시작 전투3/3 통과(18.5초). NE·W·NW·SW·새 battle-main·턱 Point4배를 직접 열었다.
- 수정 관문 판 `Main1-20261118T044646Z`: 자동13개 PASS, G19 N/A, G20 BLOCKED이므로 전체 관문은 FAIL이다. 디렉터 판독 뒤에만 마감 통과로 올린다. 판·경로·시험·승인 문서 근거는 `.director/evidence/20261118-hd2d-background/1/3/`에 있다. 야영지 최신 경로는 `output/hd2d-background/rework-locations/26-W-1920x1080.png`다(이전 요청의 잘못된 evidence 경로 수정).
