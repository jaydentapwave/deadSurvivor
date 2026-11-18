# HD-2D 걷는 땅 높낮이 — 설계 브리핑 (r001 반영판)

- **상태: r001 FAIL → 반영, fix-r001 FAIL → 반영, fix-r002 FAIL → 반영, fix-r003 FAIL(남은 필수 DR-03-B 발광 보존) → 반영(11-18, 12절). 수정 확인 4회차(fix-r004) 대기.** r001 반영은 **(r001)**, fix-r001 반영은 **(fix-r001)**로 표시했다. 디렉터 결정 D-T1~D-T7은 **확정됨**(10절 머리, 18:0x).
- 사용자 결정(2026-11-18): 바닥 높이를 어디까지 넣을지 묻자 **"걷는 땅도 높낮이"**를 골랐다. 고른 보기의 글: "들판 안에도 완만한 언덕·단. 캐릭터·몬스터가 땅 높이를 따라가도록 게임 코드(이동·생성·그림자·발사체) 변경이 필요". 앞선 결정: 배경은 전부 3D(기준서 r7 K18), 지도는 PCG seed 8("seed 8 진행해주어"). **seed 8의 시작 화면 구성은 알아볼 수 있게 남긴다.**
- 작성: 초안 2026-11-18(Claude 하위 에이전트, 기준 `738df90`), r001 반영 2026-11-18(같은 에이전트, 기준 **HEAD `082c8ee`** — 3D 배경 교체 단계 커밋 `04006cd`·`804c988`·`082c8ee`가 들어간 상태). 3D 단계가 아직 끝나지 않았을 수 있으므로 구현을 시작할 때 6절 grep 명령으로 줄 번호를 다시 찾는다.
- 규모: **L2**(`.ai/rules/work-levels.md`). 좌표 변환 한 곳(`IsoProjection`)의 계약이 바뀌어 소비처 30곳 이상이 따라 바뀐다. 새 데이터 형식(`field_height.json`)이 생기고, 땅 셰이더의 깊이 기준(`_FlatDepth`)의 뜻이 바뀐다. **(r001) 규칙에 넘기는 보이는 범위 값도 바뀐다(3.6, 기복이 있으면 피할 수 없음).**
- 바탕 문서: [화면 구조 설계](../hd2d-screen/design-brief.md)(3.3 좌표 변환 한 곳, 3.5 보이는 범위, 3.6 몬스터 경계, 3.7 세운 판, 3.8 재질·그림자, 3.12 `CameraError`, 3.13 소비처 표, 7절 시험, 13절 디렉터 결정), [지도 PCG 설계](../hd2d-map-pcg/design.md)(2절 층, 10절 seed 8 확정), [배경 준비](../hd2d-background/prep.md) D-2(턱 깊이)·D-5(들판 안 높낮이), [아트 기준서](../../art/아트-기준서.md) K18·K19·K21, 검토 원문 [design-review-r001](design-review-r001.md).
- 표기: **측정** = 파일·도구로 잰 값, **계산** = 식·스크립트로 낸 값(식을 적음), **추정** = 재지 않은 값.

## 0. 확정된 입력

| # | 내용 | 출처 |
|---|---|---|
| E1 | 게임 규칙·계산은 2D 바닥 좌표(`Vector2`, 바닥 단위)에 그대로 둔다. 그리는 쪽만 3D | 화면 설계 E5 |
| E2 | 카메라는 정사영 30°, 정수 배율, 텍셀 맞춤 | 화면 설계 E1·E2·3.3 |
| E3 | 배경은 전부 3D + UV 도트 질감, 배경 도트화 패스, 깊이 경계 외곽선(0.055) | 기준서 K18·K19 |
| E4 | 지도는 PCG seed 8. 생성기 `Tools/map_pcg/`. 손으로 json을 고치지 않는다 | PCG 설계 10절, 3D 단계 1부 4번 |
| E5 | 걷는 땅에 높낮이(완만한 언덕·단). 대원·몬스터·효과·그림자가 땅 높이를 따른다 | 사용자 11-18 |
| E6 | 성능은 재지 않는다. 7단계에서 한 번 | 사용자 11-18("최적화개념인데 … 마지막에 테스트를해서") |
| E7 | 기획서 목표값(G5·G7, `wave_targets.py`)은 그대로 두고, 벗어나면 데이터(`spawnMargin` → 몬스터 `speed` 일괄 배율)로 맞춘다 | 화면 설계 13절 디렉터 결정 |

## 1. 목표와 완료 조건

| 항목 | 내용 |
|---|---|
| 이번에 쓸 수 있게 될 것 | 본 게임 전투장 땅에 완만한 언덕과 단(경사 띠로 이어진 높은 평지)이 생긴다. 대원·몬스터·투사체·적 탄·효과·피해 숫자·HP 막대·발 그림자·접촉 그림자·투사체/적 탄 그림자·폭발 범위·돌진 선·치유/파동 고리가 그 높이를 따른다. 카메라는 원정대 발밑 높이를 따라가고 텍셀 맞춤을 유지한다. 3D 소품(나무·바위·돌담·천막·울타리 등)은 땅 위에 선다. 높이는 PCG 생성기가 seed 8에 더하는 새 층에서 온다 |
| 완료 조건 | 컴파일 오류 0, EditMode 전부 통과, 판정 경로 G1~G8 통과(`tools/playable.py`), 웨이브 목표값 통과(`wave_targets.py --metrics`, **(r001) 보이는 범위 값이 바뀌므로 필수**), 지형 판정 흐름(새 `Tools/playable/hd2d-terrain-flows.json`) 통과, 생성기 자체 시험 통과, **seed 8의 `field_layout.json` 바이트가 지형 층 추가 전과 같음**, **(r001) 높이 0 회귀 묶음(8절 B0~B6)이 단위 H2·H3·H4 뒤 기준 캡처와 같음**, 지형 캡처 묶음(8절 H5)과 C10 묶음을 디렉터가 직접 봄 |
| 시험 후보 / 정식 채택 | **정식 채택**: 높이 자료 형식과 검증, `TerrainHeightMath`(표본·법선·발밑 최솟값·평면 투영·땅 위 맞춤), 좌표 변환 계약(`FloorToWorld`가 높이를 포함), 땅 위 텍셀 맞춤, 보이는 범위 깊이 여유 식, 바닥에 붙는 그림 처리 방식, 효과 프레임 원천(`IEffectFrameSource`), 생성기 지형 층과 검사. **시험 후보**(캡처를 보고 조정): 기복 크기 R, 최대 경사, 언덕 수·크기 |
| 제외(후속) | 성능 측정·최적화(7단계), 빛 값(6단계), 걷지 못하는 절벽(3.1 A2), 높이에 따른 이동 속도·시야 변화, UI, **(r001) 지금도 있는 "가장자리 생성 몬스터 몸이 화면 아래 모서리에 보임" 문제의 해결**(D-T7 (나)를 고르지 않는 한 — 3.6) |

## 2. 바뀌지 않는 것

| 무엇 | 근거(파일:줄, `082c8ee`) | 보장 방법 |
|---|---|---|
| 게임 계산은 바닥 `Vector2`만 쓴다. 이동·충돌·사거리·생성 식·재활용 식·진형·대상 고르기·돌진 판정 | `PartyMoveService`, `MonsterSpawnService.cs:143-154`, `MonsterRecycleService.cs:47-89`, `CombatService` | 규칙 서비스는 높이 자료를 받지 않는다(DI 생성자에 `TerrainHeightGrid` 없음 — 13절 리뷰 확인 항목) |
| 규칙이 받는 보이는 범위는 `Vector2` 반 크기 하나 | `WorldPresenter.cs:256-268` | 형과 받는 곳은 그대로. **(r001) 값은 깊이 쪽에 `terrainDepthPad`가 더해진다**(3.6). 규칙 코드는 바꾸지 않는다 |
| 방향 고르기(대원·몬스터 이동, 공격 방향) | `WorldPresenter.cs:355,444`, `CombatService.cs:409` | 바닥 이동량만 쓴다(3.4, T9) |
| 판정 경로 G1~G8 기대식 | `Tools/playable/route.json` | 고치지 않고 통과해야 한다. 진입점 이름·형을 지우거나 바꾸지 않는다 |
| 효과 판정 기대식 | `Tools/playable/art-insert-flows.json`(FX1~FXHIT2, FXSEQ-*) | **(r001)** 판정 진입점의 대상만 바뀐 렌더러로 옮기고 기대식은 그대로(3.11) |
| seed 8 소품·길·물·야영지 자리 | `Resources/Data/Field/field_layout.json` | 지형 층은 **맨 마지막 층**이고 **따로 된 난수**를 쓴다(3.2). 바이트 비교 |
| 장식 턱(0.36m, 연못 둑·섬·가장자리 단)은 사람·몬스터가 통과한다(D-2) | prep D-2, `HD2D_Field_Ground.mat`·`Bank.mat` `_FlatDepth: 1` | 그대로 통과. 깊이 기준만 "y 0"에서 "그 자리 땅 높이"로(3.8) |
| 판 흐름, DI 수명(Scoped), 데이터 형식은 새 파일·새 칸 추가만 | `GameSceneLifetimeScope.cs` | 등록 줄 1개, `view_rules.json` 칸 1개 |
| **(r001·fix-r001·fix-r002) 재질 자산** | `Materials/HD2D/*.mat` | 깊이 치우침은 재질 값이 아니라 높이 자료에서 계산한 **전역 셰이더 값**(평평하면 0, DR-05). 바뀌는 것: 땅 무리 재질 4개의 렌더 큐(1990), 그림자 셰이더를 깊이 자르기 없는 `HD2D_GroundShadow`(1995)로, `HD2D_EffectGlow`에 `_ZTest` 칸(기본 LEqual)과 효과 짝 재질 4개(1996, fix-r003). 평평한 자료에서 그림이 같다는 것은 B0~B6 통과 전까지 조건부(9절 되돌리기 범위) |

## 3. 구조와 결정

### 3.1 접근 판단 — 제안 받은 방향을 확인하고 다듬은 곳

제안("시뮬레이션은 평평한 바닥 그대로, 그리는 쪽만 높이 `H(x,z)`를 한 변환점에서 더한다")은 **채택**한다. 이유: 규칙·결정론 시험이 그대로 남고, 높이를 0으로 두면 지금 화면으로 돌아가 되돌리기가 데이터 하나다(r001: 깊이 치우침·띄움까지 평평한 자료에서 0이 되게 설계 — 3.8). 다듬은 곳:

| # | 제안 | 다듬은 것 | 이유 |
|---|---|---|---|
| A1 | 표본은 쌍선형 | **삼각형 선형**(땅 메시와 같은 대각선) | 쌍선형 면은 칸 안에서 휘고 메시 삼각형은 평면이라 칸 가운데에서 `|h00+h11−h01−h10|/4` 어긋난다. 같은 대각선이면 CPU 표본 = 그려진 땅(TH4) |
| A2 | "단과 경사로" | 걷는 땅 안의 단은 **경사 띠로 둘러싼 높은 평지**. 걷는 땅 안 절벽 없음 | 몬스터는 땅을 보지 않고 곧장 걷는다. 절벽은 한 걸음 뛰어오름이 되고, 막으려면 규칙 변경. 기존 0.36m 장식 턱은 지금처럼 통과(D-T2) |
| A3 | 보이는 범위는 규칙 그대로 | **(r001) 기복이 있으면 규칙 범위 깊이에 여유 `terrainDepthPad` = R·cotθ + 맞춤 오차를 더해야 한다** | 원정대가 이동 한계에 있을 때 `ClampToDiamond`로 당겨진 생성·재활용 점이 화면 끝에서 0.01m 밖에 생긴다(검토 반례 재현, 3.6). 기복이 조금만 있어도 화면 안에 발이 보인다 |
| A4 | 텍셀 맞춤은 그대로 | **(r001) 땅 위 맞춤** `SnapOnTerrain`: 화면 평면에서 맞춘 뒤 **맞춘 x·z에서 높이를 다시 정확히 푼다** | 지금 식은 x·z만 맞춘다. 맞추기 전 높이를 쓰면 발이 땅에서 0.003m 뜬다(검토 반례 H=0.2z, z=0.02) |
| A5 | 바닥 그림은 경사에 맞추거나 꼭짓점마다 표본 | 작은 것은 땅 평면에 기울이고 큰 것은 격자 메시로 붙임. **(fix-r001) 둘 다 배경 패스의 "바닥 그림 자리"(땅 무리 뒤·다른 3D 앞, `ZTest Always`)에서 그려 깊이 치우침 없이 관통을 없앰** | 3.7 광선 계산·반례 |

### 3.2 높이 자료 형식과 생성기 지형 층

**자료** — 새 파일 `Assets/Game/03.Resources/Resources/Data/Field/field_height.json`. `field_layout.json`은 바꾸지 않는다.

```json
{
  "cell": 1.0,
  "originX": -36.0,
  "originZ": -36.0,
  "width": 73,
  "depth": 73,
  "heightsMm": [ /* width×depth 정수 mm, z 행 우선(index = j·width + i) */ ],
  "generator": { "tool": "Tools/map_pcg", "layer": "terrain", "seed": 8 }
}
```

- 칸 1m, 들판 바깥 테까지(−36~36) 73×73 = 5,329값(계산). mm 정수라 생성기·Unity 값이 비트까지 같다.
- **(r001, F-01) 요약 칸(`relief`, `maxSlope`)은 파일에 두지 않는다.** 기복·경사·곡률은 언제나 배열에서 다시 계산한다(생성기 검사, `TerrainHeightGrid`의 계산 속성, 시험 TV1). 메타데이터와 배열이 어긋날 여지를 없앤다.
- **(r001, F-02) 실패 계약**: `TerrainHeightGrid.FromMillimetres`는 `width ≥ 2`, `depth ≥ 2`, `cell > 0`, `heightsMm.Length == width·depth`가 아니면 `GameLogger.LogError("[TerrainHeightGrid] …")` 뒤 `ArgumentException`을 던진다(fail fast, `error-handling.md`). 파일이 없으면 `ResourceService.LoadJson`이 지금처럼 실패를 낸다(데이터 파일 누락은 빌드 결함). 배열은 생성 때 복사하고 밖으로는 읽기 전용(`Height(i, j)`)만 낸다. 시험 TD1~TD3.
- 칸 크기(D-T3): **1m**. 경사 0.2에서 0.36m 단의 경사 띠가 1.8m(2칸). 땅 메시 약 5,200삼각형(계산). 0.5m는 약 20,700.
- `placement.height`(측정: 806개 0, 75개 0.36)의 뜻을 **절대 높이 → 그 자리 땅 위 높이**로 바꾼다. 값은 그대로다.
- 물 높이(`water[].height`)도 땅 위 높이로 읽는다. 물·다리 둘레는 높이 0으로 평평하게 두므로 지금 값 그대로 맞다(불변식, TW1).

**생성기** — `Tools/map_pcg/map_pcg/terrain.py`(새) + `generate.py` 끝에 한 줄, `export.py`, `constraints.py`, `render.py`, `selftest.py`, `config/deadsurvivor.json` `terrain` 칸.

1. 순서·난수: 지금 층 **뒤**, `np.random.default_rng(L.seed + 180004)` 하나(`_edge_clusters`의 `+180003`과 같은 방식). 앞 층 결과가 바이트까지 같다.
2. 바탕 기복: 언덕 2~4개(가우스, 반지름 4~8m) + 느린 값 잡음(파장 16m).
3. 단(D-T2): 0~2개, 먼 두 변 쪽(L1 18~30), 높이 0.3~0.45m(기준서 K21 낮은 턱 범위) + 둘레 경사 띠(폭 ≥ 높이 ÷ 최대 경사).
4. 평평한 구역(높이 0, 둘레는 smoothstep, 이어 붙이는 폭 ≥ 차이 ÷ 최대 경사): 시작점 L1 ≤ 6, 물 전체 + 둘레 1.5m, 다리 둘레 3m, 야영지 반지름 + 1m. 돌담 줄 둘레 0.8m는 줄 방향 경사 ≤ 0.1, 길은 길 방향 ≤ 0.12·가로 ≤ 0.08.
5. 경사 제한: 삼각형마다 기울기 크기 ≤ `maxSlope`(0.2, D-T6). 이웃 차이 완화를 수렴까지(최대 200번, 결정적).
6. **(r001, DR-03) 곡률 제한은 "꼭짓점을 공유하는 모든 삼각형 쌍"의 기울기 차 ≤ κ = 0.08**(초안의 "변을 공유하는 쌍 0.1"을 바꿈). 작은 바닥 판이 반지름 0.5m 안에서 넘을 수 있는 삼각형은 가운데 삼각형과 꼭짓점을 공유하는 것뿐이라서(칸 1m, 3.7), 이 제한 하나로 처짐 상한이 닫힌다.
7. 기복 상한: 걷는 땅(L1 ≤ 32)에서 `max H − min H ≤ R`(D-T1). 시작 화면 창(20×22.5m, 원점)에서 `|H| ≤ 0.25`.
8. mm 반올림 → `export/field_height.json`. **(r001, F-01) 위 4~7의 검사는 반올림해 내보낸 mm 배열을 다시 읽어 계산한다**(반올림으로 경사가 넘는 경우를 잡음).

**검사·반례(`selftest.py`)**: 경사 초과, 기복 초과, 시작 구역 평평 아님, 물 둘레 평평 아님, 다리·야영지 아래 경사, 길 가로 경사, 꼭짓점 공유 곡률 초과(두 칸 떨어진 삼각형이 아니라 **한 꼭짓점의 마주 보는 두 삼각형**에 기울기 ±0.05를 줌), 내보낸 배열 반올림 반례 — 각각 걸려야 한다. 결정성, **앞 층 불변**(지형 층 켜고 끈 두 실행에서 `field_layout.json`·`ground_path.json`·마스크 4장 해시 같음).

**미리보기**: `render.py`의 `start.png`·`far-nw.png`·`topdown.png`에 높이(땅 텍셀을 `H·cos30·96px/m` 위로, 경사 띠 3단 명암, 등고선 0.1m). `output/map-pcg/seed-8/terrain-compare.png`(지형 전/후 `start.png`).

### 3.3 새 / 바뀌는 클래스와 자리

다시 쓸 계산(게임 이름 없음)은 `HD2D.View` 어셈블리(`Effect/HD2DView/`).

| 클래스(새/바뀜) | 폴더 | 책임 |
|---|---|---|
| `TerrainHeightGrid`(새, 불변 클래스) | `Effect/HD2DView/Utils/` | 원점·칸·폭·깊이·높이(복사본, 읽기 전용). 정적 생성 `FromMillimetres(...)`(검증 포함, F-02), `Flat`(모두 0). 배열에서 계산하는 속성 `MaxSlope`, `MaxVertexCurvature`, `ReliefWithin(L1)`, `IsFlat`(모든 값이 같음) |
| `TerrainHeightMath`(새, static Utils) | 같음 | `Sample`(삼각형 선형, 3.4), `Normal`(그 삼각형 평면 법선), `MinUnder(x, z, r)`, `ProjectOntoPlane(point, direction, planePoint, planeNormal)`, **(r001) `SnapOnTerrain(grid, x, z, sinθ, ppu)`**(3.5), **(fix-r001) `ActorDepthBias(grid, footHalfWidth, sinθ)`**(3.8), **`RayGroundDepthGap(grid, point, θ)`**(같은 화면 광선 위 땅 교점과의 눈 깊이 차, 시험·유도 검산용, 3.7) |
| `TerrainMeshMath`(새) | 같음 | 땅 격자 메시 배열(꼭짓점·삼각형·부드러운 법선·uv2 = 땅 높이), 다이아몬드 L1 ≤ 36 안 칸(무게중심 기준), 대각선은 `TerrainHeightMath`와 같음 |
| `GroundDecalMath`(새) | 같음 | 큰 바닥 그림을 격자로 땅에 붙인 꼭짓점·UV(가운데·크기·요·간격·띄움) |
| `ConformedDecal`(새, MonoBehaviour) | `Effect/HD2DView/View/` | MeshFilter·MeshRenderer 하나. 가운데·크기·요·그림이 바뀔 때만 다시 만든다. 그림 텍스처는 재질 속성 블록 |
| **(fix-r002·fix-r003)** `HD2D_GroundShadow` 셰이더·재질, `HD2D_EffectGlow`에 `_ZTest` 칸 + 바닥 효과 짝 재질 `HD2D_EffectGround_*_Terrain` | `03.Resources/Shaders/HD2D/`, `Materials/HD2D/` | 바닥 그림자 곱셈(큐 1995)과 바닥 효과(큐 1996, 기존 발광 계산 그대로), 둘 다 `ZTest Always`·깊이 텍스처 안 읽음(3.7 표). `EffectGlowMath.Shade`(순수 C#)는 `HD2D_EffectGlow` 조각 식을 옮긴 함수(TC5). `ShadowFragmentSurvives`(순수 C#, `TerrainHeightMath` 옆)는 그림자 셰이더 조각 조건을 그대로 옮긴 함수(TC4) |
| **(r001) `IEffectFrameSource`**(새, 인터페이스) | `Effect/HD2DView/View/` | 효과 판정·연속 캡처용 사실 보고: `Renderer ActiveRenderer`(지금 실제로 그리는 렌더러), `Sprite SourceSprite`(원본 스프라이트, 붙인 판이면 텍스처를 준 스프라이트), `int FrameNumber`, `float Alpha`, `bool IsShown`. 3.11 |
| `TiltedViewMath`(바뀜) | 같음 | `SnapToTexels`는 **그대로 둔다**(기존 시험 유지, 새 호출처 없음). 새 `WorldToScreenPlane(Vector3 delta, θ) = (dx, dy·cosθ + dz·sinθ)`, **(fix-r001) 새 `SnapScreenPlane(world, sinθ, ppu)`**: x를 1/ppu, 화면 세로 `v = y·cosθ + z·sinθ`를 1/ppu에 맞추고 **y는 그대로, z를 풀어** 돌려줌. 이미 맞춘 점에는 항등. y = 0이면 `SnapToTexels`와 같음 |
| `FieldHeightData`(새, 데이터) | `Features/Field/Data/` | JSON 칸만 |
| `IsoProjection`(바뀜) | `Features/Field/Utils/` | **H0**: 격자 판 `FloorToWorld(floor, s, grid)`·`FloorToWorldSnapped(floor, s, sinθ, grid)`를 **더한다**. **H3**: 2인자 판을 지운다(DR-06, 3.4) |
| `FieldView`, 셰이더 2개, 뷰·Presenter 여럿, `PlayableProbe`, `PlayableEffectSampler`·`PlayableEffectSequenceSampler`, `FieldLayoutBaker` | 6절 표 | 6절 |

### 3.4 좌표 변환은 계속 한 곳 — 높이를 넣는 법

- 바닥 → 3D(계산용, 맞추지 않음): `FloorToWorld(floor, s, grid) = (x·s, Sample(grid, x·s, y·s), y·s)`.
- 그리는 위치: `FloorToWorldSnapped(floor, s, sinθ, grid) = SnapOnTerrain(grid, x·s, y·s, sinθ, 48)`(3.5). **결과 점은 늘 땅 위에 있다**(y = Sample(맞춘 x, 맞춘 z), 부동소수 오차 안).
- **(r001, DR-06) 단계**: H0에서 격자 판을 **더하고**(기존 2인자 판과 함께 있음, 컴파일 녹색), H3 첫 커밋에서 2인자 판을 지워 모든 소비처를 컴파일 오류로 드러낸 뒤 같은 단위에서 이행한다. 높이 없이 써야 하는 곳(왕복 시험)은 `TerrainHeightGrid.Flat`을 명시한다.
- 표본 식(삼각형 선형, 대각선 (i,j)–(i+1,j+1)):

```
gx = (x − originX)/cell,  gz = (z − originZ)/cell
i = clamp(floor(gx), 0, width−2),  j = clamp(floor(gz), 0, depth−2)
fx = clamp01(gx − i),  fz = clamp01(gz − j)
fx ≥ fz:  h = h(i,j) + fx·(h(i+1,j) − h(i,j)) + fz·(h(i+1,j+1) − h(i+1,j))
fx < fz:  h = h(i,j) + fz·(h(i,j+1) − h(i,j)) + fx·(h(i+1,j+1) − h(i,j+1))
```

- 높이 자료는 Scoped 하나를 뷰 쪽만 받는다. 규칙 서비스는 받지 않는다.
- 화면 방향: 방향 고르기는 바닥 이동량 그대로(T9). **(r001, F-03) 투사체 그림 방향은 "그 프레임 자리의 국소 접선" 하나로 정한다**: `t = WorldToScreenPlane(FloorToWorld(p + d·0.25m) − FloorToWorld(p))`(p = 바닥 위치, d = 바닥 진행 방향, 둘 다 맞추기 전 값). 지난 프레임 값에 기대지 않아 첫 프레임·풀 재사용에서도 같다. 이 `t` 하나를 그림 고르기(`EffectFrameMath.DirectionSprite`)와 `flipX`·`flipY`에 함께 쓴다. 연결선(발동 시작 → 끝)은 `WorldToScreenPlane(끝 − 시작)`. 경사 0.2에서 대각 비행의 화면 각 차이는 최대 약 7°(계산: atan(0.5) = 26.6° → atan(0.5 + 0.2·0.866) = 34.0°).

### 3.5 땅 위 텍셀 맞춤과 카메라 (r001, DR-04)

- 지금 `SnapToTexels`(`TiltedViewMath.cs:11-15`)는 x를 1/48, z를 1/(48·sinθ) 격자에 맞추고 y는 그대로 둔다. y = 0에서는 맞다.
- **새 `SnapOnTerrain(grid, x, z, sinθ, 48)`**:
  1. `x' = round(48x)/48`.
  2. `v = Sample(x', z)·cosθ + z·sinθ`, `v' = round(48v)/48`(화면 세로 목표).
  3. `x = x'`에서 `f(z) = Sample(x', z)·cosθ + z·sinθ − v' = 0`을 푼다. 한 삼각형 안에서 `Sample(x', z) = a + b·z`(선형)라 `z = (v' − a·cosθ)/(sinθ + b·cosθ)`. 해가 그 삼각형 밖이면 그쪽 이웃 삼각형으로 옮겨 다시 푼다(최대 4번). `|b| ≤ 0.2`면 분모 ≥ 0.5 − 0.173 > 0이라 f는 단조 증가이고 해는 하나다(계산).
  4. `y' = Sample(x', z')`. 돌려주는 점 = `(x', y', z')`.
  - 결과: 화면 가로·세로가 1/48 정수배이고, **y'는 맞춘 자리의 땅 높이와 정확히 같다**(배선 오차 0, 부동소수 오차만). 검토 반례(H = 0.2z, z = 0.02, 맞추기 전 높이 사용 시 0.002948m)는 시험 TS4로 넣는다.
  - 평평한 격자에서는 지금 `SnapToTexels(…y=0…)`과 결과가 같다(TS1).
- **(fix-r001, DR-04) 다시 맞추는 곳을 모두 항등으로**: 땅 위 맞춤으로 정한 점을 받는 쪽이 평지 방식(`SnapToTexels`, z를 1/(48 sinθ) 격자에)으로 다시 맞추면 y ≠ 0에서 화면 세로가 틀어진다. 검토 반례: H = 0.2z, 원래 z 0.02 → `SnapOnTerrain` z′ 0.03094649, y′ 0.00618930(화면 세로 정확히 1/48) → 지금 `TiltedCameraRig.cs:44`가 `SnapToTexels`로 z를 0.04166667로 되돌려 화면 세로 0.02619342m = **1.257텍셀**(계산으로 재현). 그래서 `TiltedCameraRig.Apply`, `BurstView.cs:28`, `ProjectileView.cs:43`의 `SnapToTexels`를 **`SnapScreenPlane`**으로 바꾼다. 이미 맞춘 점이면 아무것도 바꾸지 않고(TS3), 맞추지 않은 점이 와도 같은 화면 평면 텍셀 계약으로 맞춘다. 카메라 초점도 대원 발과 같은 계약이라 둘의 화면 좌표 차가 늘 1/48 정수배다.
- 같은 함수를 쓰는 곳: 대원·몬스터·효과·표시 뿌리, 카메라 바라보는 점. 투사체·세운 발동의 뿌리는 **땅 위 점**(바닥 위치의 `SnapOnTerrain`)이고 몸 높이는 세운 자식 오프셋이다 — 지금처럼 몸 높이 점을 맞추지 않는다(6절 11·12).
- 카메라는 원정대 중심의 땅 높이를 따라간다(`WorldPresenter.cs:125`). 지연 없음(지연이면 `CameraError ≤ 0.021`이 경사에서 깨짐). 세로 속도가 삼각형 경계에서 꺾이는 정도는 곡률 κ 0.08로 작다 — 연속 캡처 8장(H5 ⑧).
- `CameraError`(`PlayableProbe.cs:196`): 기대값 = `FloorToWorld(center, s, grid)`(맞추기 전). 복원식은 화면 설계 3.12 그대로, 허용치 그대로(맞춤 오차 x ≤ 1/96, 화면 세로 ≤ 1/96 → hypot 0.0147 ≤ 0.021).

### 3.6 보이는 범위·몬스터 생성·재활용 (r001, DR-01)

**검토 반례 재현(계산)**: 1920×1080(반폭 10, 반깊이 11.25m, 이동 한계 L1 10.75), 중심 (−7.99656, −2.75344)(L1 = 10.75, 이동 한계 위), 생성점을 `ClampToDiamond(…, 32)`로 당긴 결과 (−17.98645, −14.01355)(L1 = 32). 가로 차 −9.98989(화면 안), 깊이 차 11.26011 → **깊이 여유 0.01011m**. 초안 3.6의 "최소 1.038m"는 당기기 전 원 위의 점만 본 값이라 틀렸다.

**바른 하한(계산)**: 이동 한계는 `|cx| + |cy| + 반폭 + 반깊이 ≤ 32`라서 보이는 사각형이 마름모 안에 있고 한계에서 모서리가 마름모 변에 닿는다. 당겨진 점은 마름모 변 위에 있다. 그래서 **가로로 화면 안인 당겨진 점의 깊이 여유 하한은 0**이다(위 반례가 거의 그 경우). 평평한 땅에서도 발이 화면 끝에 생긴다 — 이것은 지금 게임에도 있는 동작이다(화면 설계 3.6 "경계 위의 점은 늘 화면 밖이거나 화면 끝").

**높이가 있으면**: 화면 세로 = (pz − cz)·sinθ + ΔH·cosθ (ΔH = H(p) − H(c)). 먼 쪽 땅이 낮거나 가까운 쪽 땅이 높으면 깊이로 |ΔH|·cotθ만큼 화면 안에 들어온다. 여기에 카메라 맞춤(화면 세로 ≤ 1/96)과 발 맞춤(≤ 1/96)이 더해져 화면 세로 오차 ≤ 1/48m, 깊이로 1/(48·sinθ) = 0.0417m.

**결정: 규칙에 넘기는 보이는 범위 깊이에 여유를 더한다.**

```
terrainDepthPad = R·cotθ + 1/(48·sinθ)        (R = 걷는 땅 기복 max−min, 배열에서 계산)
규칙용 반 크기 = (반폭, 반깊이 + terrainDepthPad) ÷ s     (WorldPresenter.UpdateViewExtent, 6절 5)
```

- 왜 이것으로 닫히나: 이동 한계가 늘어난 사각형(반깊이 + pad)이 마름모 안에 있게 줄어든다. 그러면 마름모 변 위의 점 중 가로로 화면 안인 것은 깊이로 ≥ pad 밖에 있다. 당기지 않은 생성 원(반지름 = 늘어난 대각선 + 0.79)·재활용 사각형(+1.70)도 늘어난 사각형 밖이라 ≥ pad다. 그래서 **생성·재활용 직후 발의 깊이 여유 ≥ pad ≥ |ΔH|·cotθ + 맞춤 오차** → 발이 화면에 들어오지 않는다.
- 검산(계산, 세션 임시 스크립트로 돌림·보관 안 함, 같은 계산은 시험 TV2가 맡음 — 이동 가능한 중심 격자 81×81 × 방향 360개, 생성·재활용 모두 `ClampToDiamond` 적용, 가로로 화면 안인 점의 최소 깊이 여유):

| R(m) | pad(m) | 화면 | 이동 한계 L1(m, 지금 → 새) | 최소 여유 생성 / 재활용(m) | pad 이상? |
|---|---|---|---|---|---|
| 0 | 0 | 1920×1080 | 10.75 | 0.157 / 0.020 (반례는 0.010) | — (지금 상태) |
| 0.3 | 0.561 | 1920×1080 | 10.75 → 10.19 | 0.658 / 0.742 | 예 |
| **0.5** | **0.908** | 1920×1080 | 10.75 → **9.84** | 1.051 / 0.943 | 예 |
| 0.5 | 0.908 | 2340×1080 | 8.56 → 7.65 | 0.923 / 0.923 | 예 |
| 0.5 | 0.908 | 2520×1080 | 7.62 → 6.72 | 0.930 / 1.145 | 예 |
| 0.5 | 0.908 | 2048×1536 | 14.22 → 13.31 | 0.963 / 1.002 | 예 |
| 1.0 | 1.774 | 1920×1080 | 10.75 → 8.98 | 1.956 / 1.871 | 예 |

- **R 0.5m를 규칙 값 그대로 쓰는 것은 안 된다**(초안 D-T1 (가) 철회). 기복이 있으면 크기와 상관없이 여유가 필요하다. 대가: 1920×1080 이동 한계 −0.91m, 생성 거리 +0.6m쯤(계산). 화면 설계 13절 순서로 G5·G7·웨이브 목표를 재고 데이터로 맞춘다(E7).
- 버린 안(r001):
  - **평평 띠**(마름모 가장자리 띠를 높이 0으로): 여유는 원정대 중심 높이에 대한 차 ΔH로 정해진다. 가장자리만 평평해도 중심이 언덕 위면 |ΔH| = H(c)라 막지 못한다. 막으려면 들판 전체가 평평해야 한다.
  - **생성 여유 데이터(`spawnMargin`)만 키우기**: 당겨진 점은 마름모 변에 붙어 `spawnMargin`과 상관없이 여유 0이 된다. 이동 한계를 줄이는 쪽(=보이는 범위 깊이 여유)만 닫힌다.
  - `fieldEdgeMargin` > 0: 당겨진 점이 화면 쪽으로 더 들어와 나빠진다(화면 설계 3.6).
  - 자리마다 규칙 범위 바꾸기: 규칙이 뷰 자료에 기대게 됨.
- **몸(머리)까지 화면 밖에 두려면**(D-T7): 화면 아래쪽에서 생긴 몬스터는 발이 화면 밖이어도 몸이 위로 솟아 보인다. 지금도 R = 0에서 생긴다(위 하한 0). 몸까지 막으려면 `pad += b/sinθ`(b = 가장 큰 몬스터 머리 높이, 화면 m; `MonsterViewFactory.GetHeadHeight`, 측정은 H4)가 필요하다. b = 0.8m이면 +1.6m(이동 한계 −1.6m 더)라 게임 동작 변화가 크다. **권장 (가): 이번에는 발 기준을 지키고, 몸 기준은 "높이 0 기준선보다 늘지 않음"으로 본다.** (나)는 몸 기준까지 고치는 것(별도 디렉터 결정).
- 데이터 계약 시험 **TV1**(F-01: 배열에서 계산): 실제 `field_height.json`의 걷는 땅 기복 `ReliefWithin(32)`, `view_rules.json`의 `terrainDepthPad`, `tiltDegrees`로 `pad ≥ relief·cotθ + 1/(48·sinθ)`. 누가 pad를 지우거나 생성기가 기복을 키우면 걸린다.
- 판정 진입점 **`SpawnFootVisibleCount`·`SpawnBodyVisibleCount`**(새, `SpawnVisibleCount`를 둘로):
  - 대상: **생성 또는 재활용으로 자리가 바뀐 그 시뮬레이션 걸음의 몬스터.** 재활용을 알아내려고 `Monster`에 사실 칸 `PlacementVersion`(int)을 더하고 `MonsterSpawnService.Spawn`·`MonsterRecycleService.Tick`이 자리를 정할 때 1 올린다(규칙 동작은 바뀌지 않는 기록 칸 — 2절 예외로 적음). `WorldPresenter`는 뷰를 갱신한 뒤 지난 값과 다르면 그 몬스터를 검사한다.
  - 발: 뷰 뿌리(땅 위 맞춘 발)의 뷰포트 좌표가 [0,1]² 안이면 센다. **기대 0.**
  - 몸: **실제 활성 몸 렌더러**(`MonsterView.BodyRenderer`, 그 프레임 스프라이트)의 `bounds` 8모서리 뷰포트 사각형(`OcclusionMath.ProjectBounds`)이 화면과 겹치면 센다. **기대: 같은 흐름·시드의 높이 0·pad 0 기준선 이하**(D-T7 (가)). **(fix-r001, N-01)** 관측 배선(`PlacementVersion`, 두 진입점, 흐름 ⑨)은 H4에서 만들고 기준선도 H4에서 잰다(`output/hd2d-terrain/spawn-baseline.json`). H5는 기준선 파일을 확인한 뒤에만 높이·pad를 켠다.
  - 들판 밖 테(L1 32~36): 늘어나는 깊이는 R·cotθ ≤ 0.87m(R 0.5)로 테 폭 4m 안이다.

### 3.7 바닥에 붙는 그림 (r001 DR-03, fix-r001·fix-r002 DR-03 다시 씀)

**깊이 비교는 같은 화면 광선 위에서 한다(fix-r001).** 판 위 점 P(땅보다 세로로 Δy 아래)와 같은 화면 픽셀에 보이는 땅은 P에서 카메라 쪽으로 광선을 따라 올라간 지형 교점 Q다. 카메라 forward = (0, −sinθ, cosθ)이므로 광선을 t만큼 거슬러 가면 높이는 t·sinθ 오르고 z는 t·cosθ 줄어든다. 광선 방향 땅 기울기를 s(z 방향 경사)라 하면 `t = Δy / (sinθ + s·cosθ)` = **필요한 눈 깊이 치우침**이다. 최악은 s = −S(최대 경사, 카메라 쪽으로 내려가는 땅)라 분모가 `sinθ − S·cosθ = 0.5 − 0.2·0.866 = 0.327`(계산, S < tanθ여야 함 — S 0.2 < 0.577). 초안·r001판의 "Δy·sinθ"는 방향이 거꾸로였다.

- **검토 반례 재현(계산, fix-r002 정정 — 띄움 조건을 나눔)**: H(z) = 0.04·min(|z|, 1), 판 가운데 z = −0.01의 접평면(기울기 −0.04) 위 z = 0.48 점(가운데에서 0.49m), 그 자리 땅 기울기 s = +0.04, 땅 0.0192. **띄움 없음**: 판 y = −0.0192 → Δy 0.0384 → 같은 광선 깊이 차 0.0384/(0.5 + 0.04·0.866) = **0.07182389m**. **띄움 0.001(지금 발 그림자 값)**: 판 y = −0.0182 → Δy 0.0374 → **0.06995348m**(검토 원문 값). 어느 쪽이든 r001판 0.022와 지금 `HD2D_ContactShadow`·`HD2D_FootShadow`의 깊이 자르기 0.04를 넘는다.
- **다시 유도한 상한**: Δy ≤ r·κ(꼭짓점 공유 곡률 κ, 3.2-6)이고 분모 ≥ sinθ − S·cosθ이므로 `치우침 ≥ r·κ/(sinθ − S·cosθ)`. 작은 판(r 0.5): 0.04/0.327 = **0.122m**. 큰 판 격자(간격 0.25, 처짐 Δy ≤ 0.125·κ = 0.01): **0.031m**. 반례 0.0718 < 0.122라 덮지만, **0.122m는 너무 크다**: 그림자가 그 깊이 안쪽 앞에 있는 다른 것을 덮는다. 400마리가 붙어 서는 무리에서 뒤 몬스터의 발 그림자가 앞 몬스터의 발(평지에서 눈 깊이 0.122 = 땅 z 약 0.14m 차이)을 덮는 일이 흔해진다.
- **결정: 작은 판에는 깊이 치우침을 쓰지 않는다. 대신 "배경 패스의 바닥 그림 자리"에서 깊이 시험 없이 그린다.**
  - 지금도 발 그림자·접촉 그림자는 배경 층(`HD2DBackgroundRig.BackgroundLayer`, `FootShadowView`·`MonsterView.cs:113`·`UprightContactShadow`)에서 배경 카메라가 그린다. 이 배경 패스 안 그리기 순서를 **땅 무리 → 바닥 그림 → 그 밖의 3D**로 고정한다.
    - 땅 무리(렌더 큐 1990): 땅 격자, 물, 장식 턱, 다리 — D-2로 깊이를 땅 높이로 쓰는 것들.
    - 바닥 그림자(큐 1995, 그림자 곱셈 재질): 발 그림자, 접촉 그림자, 투사체·적 탄 그림자, 울타리 발 그림자.
    - 바닥 효과(큐 1996, 효과 재질): 폭발 범위, 돌진 선, 치유·파동 고리, 바닥 발동(발광 포함). 두 재질의 정책은 아래 표(fix-r002 DR-03-B).
    - 그 밖의 3D(큐 2000 이상): 나무·덤불·바위·돌담·풀·꽃·천막·울타리·표지판. 깊이 시험을 그대로 한다.
  - 왜 맞나: 바닥 그림은 땅(무리) 위에 있으므로 그 픽셀에서 땅보다 앞의 것은 모두 나중에 그려지는 3D이거나(깊이 시험을 통과해 바닥 그림을 덮음) 본 패스의 대원·몬스터·효과(배경 합성 뒤에 그려짐)다. 땅보다 뒤의 3D는 땅 깊이에 막힌다. 언덕 마루가 바닥 그림을 가리려면 경사가 tanθ(0.577)를 넘어야 하는데 S 0.2라 생기지 않는다. 그래서 **경사·곡률과 상관없이 치우침 0으로 관통이 없다.** 판이 땅에서 세로로 벗어나는 r·κ(≤ 0.04m)는 화면 자리 차 ≤ 0.04·cosθ = 0.035m(1.7텍셀)로만 남는다.
  - 대원·몬스터가 바닥 그림을 가리는 관계는 지금과 같다(본 패스가 나중).
  - **(fix-r002, DR-03-A) 깊이 텍스처를 읽지 않는다.** 지금 두 그림자 셰이더는 `SampleSceneDepth`로 받는 면 깊이를 읽어, 접점에서 0.4m 밖이면 깊이 차 0.04m 넘는 조각을 버린다(`HD2D_ContactShadow.shader:34`, `HD2D_FootShadow.shader:37`, 발 그림자는 y 0 평면 기준 `:34`). 배경 카메라의 `_CameraDepthTexture`는 URP가 **불투명 큐(≤ 2500)를 다 그린 뒤** 깊이를 복사해 만든다(배경 카메라 `requiresDepthTexture = true`, `HD2DBackgroundRig.cs:53`). 그래서 ① 큐 1995·1996은 불투명 구간이라 그 시점에 이번 프레임 깊이 텍스처가 없다(지난 프레임 값이나 빈 값), ② 있더라도 땅 무리만이 아니라 큐 2000 이상 3D까지 들어간 깊이다 — 어느 쪽이든 받는 면 판정에 쓸 수 없다(깊이 복사 시점은 URP 설정에 따라 앞 깊이 패스일 수도 있다 — 추측. 어느 경우든 ②는 같다). **결정: 두 그림자 셰이더에서 깊이 텍스처 읽기와 0.4m·0.04m 자르기를 지운다.** 그 자르기가 하던 일(그림자가 받는 면이 아닌 곳에 떨어지지 않게)은 새 순서가 맡는다: 땅보다 앞에 있는 3D는 나중에 그려져 그림자를 덮는다. 남는 `_Contact.z` 반지름 자르기(접촉 그림자 길이 상한)와 실루엣 알파 자르기, 턱 마스크 조건(`_Contact.w`)은 깊이와 무관하므로 그대로 둔다.
  - 그래서 조각이 살아남는 조건은 깊이와 무관하다: 실루엣 알파 ≥ 0.5, 반지름 상한 안, 턱 마스크 조건. 검토 반례 점(띄움 0.001, 깊이 차 0.06995348)은 살아남아야 한다(시험 TC4가 셰이더와 같은 조건을 옮긴 순수 함수로 확인 + 셰이더 원문에 `SampleSceneDepth`가 없음을 확인).
  - 평평한 자료에서: 땅 위 그림자는 지금도 땅 바로 위라 깊이 시험·0.04 자르기를 늘 통과했으므로 그림이 같을 것으로 **본다**. 이것은 B0~B6 통과 전까지 조건부 주장이다(9절).
  - **수직 턱·다리는 위 증명 밖이다**(경사 증명은 걷는 땅의 연속 경사에만 성립). 땅 무리에 장식 턱·다리를 넣으면 턱 뒤 그림자가 턱 앞면 위에 보일 수 있다. 지금은 깊이 자르기 때문에 그림자만 턱에 가려진다. **H4 완료 조건**: B2(대원이 턱·다리 뒤와 위를 지남)를 기준과 비교하고, 다르면 턱·다리를 큐 2000으로 옮긴 판으로 B2를 다시 찍어 같아짐을 확인한다(그때 턱 윗면 위 그림자는 `_Contact.w` 턱 마스크 조건이 지금처럼 정한다). 어느 판을 골랐는지와 B2 쌍을 핸드오프에 적는다.
- **큰 판(폭발 범위·돌진 선·치유/파동 고리·바닥 발동)**: 바닥 효과 자리(큐 1996)에 둔다(`ConformedDecal`, 배경 층). 격자로 땅에 붙이는 것은 관통 때문이 아니라 **모양이 규칙의 바닥 원·선분과 같게 보이게** 하려는 것이다. 치우침 없음.
- **(fix-r002, DR-03-B) 그림자 재질과 효과 재질을 나눈다.** 지금 그림자 셰이더는 알파만 읽고 고정 색 (.44,.43,.39)을 `Blend DstColor Zero`로 곱한다(`HD2D_FootShadow.shader:10,39`, `HD2D_ContactShadow.shader:10,36`). 이것을 효과에 쓰면 치유 고리·폭발 예고·발광 발동의 색과 빛이 사라진다. r001·fix-r001판의 "배경이 HDR이 아니면" 예외는 **지운다**: 배경 색 대상은 이미 `ARGBHalf`이고(`HD2DBackgroundRig.cs:75-77`) 합성(`HD2D_BackgroundComposite.shader:24`)이 색을 그대로 넘기므로, 배경 패스에서 그린 발광도 본 패스 Bloom을 받는다.

| 재질 | 바탕 | 쓰는 것 | 큐 | 깊이 | 섞기·색 | 스텐실 |
|---|---|---|---|---|---|---|
| `HD2D_GroundShadow`(그림자 곱셈) | `HD2D_FootShadow`·`HD2D_ContactShadow`에서 깊이 자르기를 뺀 것 | 발 그림자, 접촉 그림자, 투사체·적 탄 그림자, 울타리 발 그림자 | 1995 | `ZTest Always`, `ZWrite Off`, 깊이 텍스처 안 읽음 | `Blend DstColor Zero`, 고정 색 (.44,.43,.39), 실루엣 알파 자르기 | 지금처럼 `Ref 1, Comp NotEqual, Pass Replace` — 겹친 그림자를 한 번만 어둡게 |
| **(fix-r003)** 바닥 효과 짝 재질 `HD2D_EffectGround_{Boom,Heal,Pulse,Warn}_Terrain` | **새 셰이더 없음.** 기존 `HD2D_EffectGlow` 셰이더 그대로 쓰고, 그 셰이더에 속성 한 칸 `[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest`(기본 4 = LEqual)를 더해 `ZTest LEqual` → `ZTest [_ZTest]`로만 바꾼다. 조각 계산(알파 0.5 미만 버림, sRGB 밝기 ≥ 0.96·채도 < 0.12면 `_CoreIntensity`, 아니면 `_BodyIntensity`, `HD2D_EffectGlow.shader:43-53`)은 한 글자도 바꾸지 않는다. 기존 재질은 기본값이라 동작이 같다 | 폭발 범위·돌진 선(지금 `_Warn`), 치유 고리(`_Heal`), 파동 고리(`_Pulse`), 바닥 폭발(`_Boom`) | 1996(재질 `m_CustomRenderQueue`, 원래 2440) | `_ZTest` = 8(Always), `_ZWrite` 0(원래와 같음), 깊이 텍스처 안 읽음(원래도 안 읽음) | 원래와 같음: 알파 클립(블렌딩 없음, 출력 알파 1), 색 = 텍스처 × 꼭짓점 색 × 코어/몸통 강도. **강도 값은 원래 재질에서 그대로 복사**: `_Boom` 1.6/1, `_Heal` 1.6/1(`HD2D_EffectGround_Heal.mat:32`), `_Pulse` 2.2/1(`HD2D_EffectGround_Pulse.mat:32`), `_Warn` 1/1(측정, `082c8ee`) | 쓰지 않음(셰이더에 스텐실 없음 — 원래와 같음) |

  - **(fix-r003) 짝 재질은 원래 재질의 복사본**이고 다른 칸은 `_ZTest`·큐 두 개뿐이다. 원래 재질(`HD2D_EffectGround_*`, 큐 2440·`ZTest LEqual`)은 지우지 않고 그대로 둔다 — 되돌릴 때 참조만 원래로 바꾸면 된다(9절). `BurstView.effectMaterials`와 `MonsterView`(돌진 선·폭발 범위)는 `ConformedDecal`에 짝 재질을 넘긴다.
  - **판정 소비 계약 유지**: 효과 연속 캡처(`PlayableProbe.cs:1360`)는 고른 렌더러의 `sharedMaterial`에서 `_CoreIntensity`를 읽어 `> 2`이면 코어 픽셀을 센다. 3.11 이행 뒤 고른 렌더러는 `IEffectFrameSource.ActiveRenderer`(바닥 발동이면 `ConformedDecal`의 MeshRenderer)이고, 그 재질은 짝 재질이라 `_CoreIntensity`가 원래 값(파동 2.2 등)과 같다 → 같은 판정. 이 줄의 읽는 대상을 `selected.sharedMaterial`에서 `source.ActiveRenderer.sharedMaterial`로 옮기는 것 말고 식은 바꾸지 않는다.
  - 배경 패스는 낮은 해상도(배율 n)라 바닥 효과도 텍셀 단위로 그려진다. 도트 그림이라 화면 텍셀과 맞는다. 차이는 B4·B5와 H5 ⑤⑥ 캡처로 본다.
  - 시험 TC5(fix-r003): 원래 재질과 짝 재질에 같은 입력을 넣은 픽셀 출력 비교(7절).
- 버린 안(fix-r001·fix-r002): 그림자 셰이더 하나로 효과까지(색·발광 소실, fix-r002), 깊이 자르기를 땅 높이 기준으로 고쳐 유지(검토 계산 0.05125m — 그래도 버려짐, 그리고 깊이 텍스처 시점 문제가 남음), 작은 판 깊이 치우침 0.122m(무리 겹침을 깸), 작은 판까지 격자 붙임(400마리 그림자 메시를 매 프레임 다시 만듦 — 치우침 문제는 붙여도 꼭짓점 사이 처짐 때문에 남음), URP Decal Projector(렌더러 기능 추가, 7단계 전 판단 불가).

| 그림 | 지금(파일:줄, `082c8ee`) | 바뀌는 방식 |
|---|---|---|
| 대원·몬스터 발 그림자 판 | `FootShadowView.cs:30-31`(뿌리 아래 Euler(90,0,0), 0.001, 배경 층), 셰이더 깊이 자르기 `HD2D_FootShadow.shader:29-37` | 뿌리(땅 위 발) 아래, 그 자리 삼각형 법선으로 기울임, 띄움 0.001 그대로, `HD2D_GroundShadow` |
| 대원 접촉 그림자 | `UprightContactShadow.cs:51,60-61,76`(수평면 투영), 셰이더 깊이 자르기 `HD2D_ContactShadow.shader:28-34` | 발 점·땅 법선 평면에 투영, `SetGround(normal)`, `HD2D_GroundShadow`(깊이 자르기 삭제, fix-r002) |
| 투사체 그림자 | `ProjectileView.prefab` `Shadow`(:105-121, 로컬 y 0.001, 수평) | 법선 기울임, 배경 층으로 옮김, 바닥 그림 자리 |
| 적 탄 그림자 | `EnemyShotView.prefab` `Shadow`(:95-111) | 같음 |
| 울타리 발 그림자 | `FieldView.cs:286` | 뿌리를 땅 위, 바닥 그림 자리 |
| 대원 실제 그림자 | `UprightShadowCaster` | 바꾸지 않음(그림자 지도) |
| 몬스터 돌진 선·폭발 범위 | `MonsterView.cs:89-94` | `ConformedDecal` 2개(간격 0.25m, 띄움 0.002), 배경 층, `HD2D_EffectGround_Warn_Terrain`(fix-r003) |
| 바닥 발동 | `BurstView.cs:27-33`(**y = 0.001 절대값**) | `ConformedDecal` 자식, 절대 y 삭제, 배경 층, 그 발동 재질의 `_Terrain` 짝(fix-r003) |
| 세운 판 배경 발 그림자(`UprightFootShadow`) | `FieldView.cs:150` | 모델 배경에서는 안 씀. `supportY`에 땅 높이만 |

### 3.8 셰이더·깊이·평평한 자료에서의 복귀 (r001 DR-05, fix-r001 DR-03)

- **땅 깊이 기준**(`HD2D_FieldSurface.shader:49`의 `float3(o.w.x,0,o.w.z)`): uv2.x = 그 자리 땅 높이 `href`로 `float3(w.x, href, w.z)`. 땅 격자 `href = 꼭짓점 y`, 턱 `href = 턱 바닥 땅 높이`(D-2 유지). uv2가 없는 다리는 0으로 읽힌다 — 높이 0 구역이라 맞다(TW1, 추측 11절).
- **물 구멍**(`:55`)·**턱 옆면 띠·진단**(`:98,115-122`)의 `i.w.y` → `i.w.y − href`. 평평한 자료에서 href = 0이라 지금 식과 같다.
- **외곽선**(K19): 경사 ≤ 0.2·곡률 ≤ 0.08에서 이계 차분이 낮은 해상도 한 칸에 약 0.006(계산)이라 땅에 선이 생기지 않는다. 바꾸지 않고 H5 캡처로 본다.
- **렌더 큐**(fix-r001): 땅 무리 재질(`HD2D_Field_Ground`·`Water`·`Bank`, `HD2D_Prop_Bridge`)의 큐를 1990으로, 바닥 그림자 재질 `HD2D_GroundShadow`를 1995, 바닥 효과 짝 재질(`HD2D_EffectGlow` + `_ZTest` Always, fix-r003)을 1996으로, 둘 다 `ZTest Always`·깊이 텍스처 안 읽음(3.7 표, fix-r002). 평평한 자료에서 그림이 같다는 것은 B0~B6 통과 전까지 조건부라 되돌리기 범위를 9절에 적었다. 되돌리기 대상이 아니다. 재질 자산 4개의 큐 값과 바닥 그림 셰이더 2개가 바뀐다 — r001판 "재질 자산 무변경"을 이 범위로 고친다.
- **세운 판 발 잘림 치우침(fix-r001 다시 유도)**: 세운 판은 카메라와 나란한 평면이라 판 전체 깊이 = 발 깊이다. 옆으로 오르는 경사 S에서 발 옆(가로 w) 땅은 Δy = S·w 높고, 같은 광선 위 땅 교점은 판보다 `S·w/(sinθ − S·cosθ)` 앞에 온다. 여기에 낮은 해상도 땅 깊이 반 칸(평지 0.018m)을 더한다.

```
_HD2DActorDepthBias = grid.IsFlat ? 0 : grid.MaxSlope·w/(sinθ − grid.MaxSlope·cosθ) + 0.018
w = 발 불투명 반폭(대원·몬스터 그림의 맨 아래 불투명 행 반폭 최댓값, H3에서 그림에서 잼, 상한 0.15m)
```

  S 0.2, w 0.15 → **0.110m**(계산; w 0.3이면 0.202). `WorldPresenter.Initialize`가 한 번 `Shader.SetGlobalFloat`. `HD2D_SpriteLit`만 쓴다(대원·몬스터·투사체·세운 효과 공통 — 서로의 앞뒤는 같은 치우침이라 그대로). 대가: 대원·몬스터가 0.110m 안쪽 앞에 있는 3D(작은 돌·풀·꽃, 줄기 밑동) 앞에 그려진다. 앞 3D가 사람을 가리는 큰 경우는 가림 디더(`FieldOcclusionPresenter`)가 이미 3D를 흐리게 한다. H5 캡처 ③④로 본다. **평평한 자료에서 0** → 이전 동작.
- r001판의 `_HD2DSmallDecalDepthBias`·`_HD2DLargeDecalDepthBias`와 fix-r001판의 "빛나는 바닥 발동 0.031" 예외는 **없앤다**(fix-r002: 배경 패스가 HDR이라 예외가 필요 없음).
- 땅 조명: 메시 법선은 격자 중앙 차분, 위치는 삼각형 선형.

### 3.9 3D 소품·식물이 땅에 서는 법

| 종류 | 높이 | 근거 |
|---|---|---|
| 나무·덤불·큰 바위(모델) | `MinUnder(x, z, 발자리 반지름)` + `placement.height` | 경사에서 한쪽이 뜨지 않게 |
| 작은 돌·풀·꽃 | `Sample(x, z)` | 작아서 한 점 |
| 작은 식물 4m 묶음(`CombineSmallScenery`, `FieldView.cs:229-250`) | 바꾸지 않음. 묶기 전 뿌리를 높이에 두면 `localToWorldMatrix`로 그대로 들어감 | `082c8ee` |
| 돌담 토막(1m) | 토막마다 `MinUnder(0.5)` | 계단식, 줄 방향 경사 ≤ 0.1 |
| 천막·깃발·울타리·표지판·다리 | `Sample` + `placement.height`(천막·다리는 높이 0 구역) | 3.2-4 |
| 장식 턱 메시 | 꼭짓점마다 + `Sample`, 바닥 꼭짓점 −0.02 | 턱 칸 0.125m |
| 물 | 지금 값(물 둘레 0) | TW1 |
| 가림 디더 | 바꾸지 않음(렌더러 경계·뷰포트 깊이) | `FieldOcclusionPresenter.cs:29-60` |

### 3.10 DI 등록 (`App/GameSceneLifetimeScope.cs`)

| 추가·변경 | 자리 | 비고 |
|---|---|---|
| `TerrainHeightGrid`를 Scoped로(`LoadJson<FieldHeightData>("Data/Field/field_height")` → `TerrainHeightGrid.FromMillimetres`) | 데이터 묶음(`field_layout` 줄 옆) | 새 서비스 없음 |
| 받는 쪽 생성자 | `WorldPresenter`, `CombatWorldPresenter`, `DamageNumberPresenter`, `MonsterHpBarPresenter`, `EnemyShotPresenter` | 뷰는 Presenter가 값·격자를 넘김 |
| `ViewRulesData` 새 칸 `terrainDepthPad` | `view_rules.json` | H5에서 값. H0~H4는 0 |
| 규칙 서비스 | 바꾸지 않음(`PlacementVersion` 올리기 한 줄씩만, 3.6) | — |

- 순환 없음(`TerrainHeightGrid`는 아무것도 주입받지 않음).

### 3.11 효과 판정·연속 캡처의 대상 (r001, DR-02)

- 문제(검토, `082c8ee`): `PlayableProbe.cs:669,678`의 `IsEffectVisible(view.BodyRenderer, …)`(:810-815)과 `PlayableEffectSampler`(:1328 `FindObjectsByType<SpriteRenderer>`)·`PlayableEffectSequenceSampler`(:1275~, `Matches`는 :1389)는 `SpriteRenderer`의 `sprite.name`·`color.a`·`bounds`를 읽는다. 바닥 발동을 `ConformedDecal`(MeshRenderer)로 옮기면 판정 대상이 사라지거나 꺼진 SpriteRenderer를 본다. `art-insert-flows.json`의 FX10(:388-395 `heal_area`)·`FXSEQ-ground_boom`(:510~)이 이 경로다.
- 결정: 효과 뷰(`BurstView`, `ProjectileView`, `EnemyShotView`)가 **`IEffectFrameSource`**를 구현한다. 세운 그림이면 `ActiveRenderer` = 몸 SpriteRenderer, 바닥 발동이면 = `ConformedDecal`의 MeshRenderer. `SourceSprite` = 그 프레임의 원본 스프라이트(붙인 판에 텍스처를 준 것), `FrameNumber`·`Alpha`·`IsShown`(렌더러 켜짐·활성).
- 이행(H4에서 바닥 그림 변경과 **같은 커밋**):
  - `IsEffectVisible(IEffectFrameSource, spriteName)`: 이름 비교는 `SourceSprite.name`, 알파는 `Alpha`, 뷰포트는 `ActiveRenderer.bounds.center`.
  - `IsProjectileAwayFromParty`(:795~806): `ActiveRenderer.bounds` + 땅 위 발.
  - 두 샘플러: `FindObjectsByType<SpriteRenderer>` 대신 효과 뷰 목록(`ProjectileViewRegistry`의 투사체·발동 + 적 탄)에서 `IEffectFrameSource`를 모은다. `Matches`·`FrameNumber(sprite)`·`ReadPixels(sprite)`는 `SourceSprite`를 쓴다. 캡처 사각형은 `ActiveRenderer.bounds`.
  - **판정 파일의 기대식은 바꾸지 않는다**(FX1~FXHIT2, FXSEQ-*). H4에서 `art-insert-flows.json` 효과 단계 전부를 1회 돌려 기대값 그대로 통과를 본다.
- 몬스터 돌진 선·폭발 범위는 지금 판정 진입점이 읽지 않는다(grep 확인: `PlayableProbe.cs`에 `blastArea`·`dashLine` 없음). 뷰가 `IEffectFrameSource`를 낼 필요는 없고 시각 확인은 캡처로 한다.

## 4. 상태 소유권

| 상태 | 주인 | 쓰는 쪽 |
|---|---|---|
| 땅 높이 | `field_height.json` → `TerrainHeightGrid`(Scoped, 읽기 전용) | 뷰 쪽 Presenter, `FieldView` 굽기, `PlayableProbe` |
| 대원·몬스터 바닥 좌표 | 그대로 | 그대로 |
| **(r001) 몬스터 자리 바뀜 기록** | `Monster.PlacementVersion`(규칙 서비스가 자리를 정할 때 1 올림) | `WorldPresenter`(판정 진입점 계산만) |
| 카메라 위치 | 그대로 `TiltedCameraRig`. 바라보는 점에 높이 | 그대로 |
| 보이는 범위(규칙용) | 그대로 `WorldPresenter.UpdateViewExtent`가 계산. **(r001) 깊이에 `terrainDepthPad`** | 생성·재활용·이동 제한 |
| **(r001) 깊이 치우침 전역 값** | `WorldPresenter.Initialize`가 격자에서 계산해 한 번 넣음(세운 판 하나, fix-r001) | `HD2D_SpriteLit` |

## 5. 결정과 버린 안

| # | 결정 | 정한 값 | 버린 안과 이유 |
|---|---|---|---|
| T1 | 높이를 넣는 자리 | 그리는 쪽 한 변환점 | 규칙 좌표 3D화 — 규칙·판정 경로 다시 맞춤 |
| T2 | 표본 | 삼각형 선형, 메시와 같은 대각선 | 쌍선형 — 메시와 어긋남 |
| T3 | 자료 형식 | 별도 JSON, mm 정수, 1m 칸, 요약 칸 없음(r001) | 16비트 PNG, `field_layout.json`에 넣기, 요약 메타데이터(어긋날 수 있음) |
| T4 | 걷는 땅의 단 | 경사 띠 평지 | 걷는 땅 절벽 |
| T5 | **(r001) 보이는 범위** | 깊이 여유 `R·cotθ + 1/(48 sinθ)` | 규칙 값 그대로(반례로 불성립), 평평 띠, `spawnMargin`만 키우기(3.6) |
| T6 | 카메라 높이 | 그대로 따라감, 지연 없음 | 지연 — `CameraError` |
| T7 | **(r001) 맞춤** | 땅 위 맞춤(맞춘 x·z에서 높이를 다시 풂) | 맞추기 전 높이 사용(0.003m 뜸), x·z만 맞춤 |
| T8 | 바닥 그림 | 작은 것 기울임, 큰 것 격자 붙임, **둘 다 배경 패스 바닥 그림 자리(`ZTest Always`, fix-r001)** | 띄움 0.02 고정(관통, r001), 크기·곡률 치우침 0.122m(무리 겹침을 깸, fix-r001), 작은 판까지 격자(매 프레임 400개 메시), Decal Projector |
| T9 | 방향 고르기 | 바닥 이동량 | 화면 이동 — 언덕에서 그림이 튐 |
| T10 | 발 잘림 | 전역 깊이 치우침 `S·w/(sinθ − S·cosθ) + 0.018`(광선 기준, fix-r001), 평평하면 0 | 땅 깊이 평평하게 — 언덕 가림 없음, 재질 값 고정 — 롤백 불가(r001) |
| T11 | seed 8 지키기 | 마지막 층 + 따로 된 난수 + 바이트 비교 | 새 시드 |
| T12 | **(r001) 되돌리기** | 평평한 `field_height.json` + `terrainDepthPad` 0 → 높이·치우침·띄움·보이는 범위가 이전과 같음(B0~B6 회귀로 증명) | 코드 되돌리기 |
| T13 | **(r001) 효과 판정 대상** | `IEffectFrameSource` | 꺼진 SpriteRenderer에 스프라이트만 남기기 — 판정이 그리지 않는 것을 봄 |
| T14 | **(r001) 투사체 방향** | 국소 접선 하나(0.25m) | 지난 프레임 차 — 첫 프레임·풀 재사용에서 값 없음 |

## 6. 소비처 표 (`082c8ee`, 시안 폴더 `Effect/HD2D/`·`Editor/HD2D/` 제외)

확인 명령(구현 시작 때 다시 돌림):
`grep -rn --include='*.cs' -e "IsoProjection\." -e "TiltedViewMath\." -e "Euler(90" -e "supportY" -e "\.001f" -e "BodyRenderer" -e "FindObjectsByType<SpriteRenderer>" Assets/Game/02.Scripts Assets/Game/Tests | grep -v "/HD2D/"`, `grep -n "w\.y\|float3(o.w.x" Assets/Game/03.Resources/Shaders/HD2D/HD2D_FieldSurface.shader`, `grep -n "m_Name: Shadow" -A15 Assets/Game/03.Resources/Resources/Prefab/*.prefab`.

| # | 소비처(파일:줄) | 지금 | 바꿀 것 | 단위 |
|---|---|---|---|---|
| 1 | `IsoProjection.cs:6-9` | 2인자 판 | H0 격자 판 추가, **H3 2인자 판 삭제**(DR-06) | H0·H3 |
| 2 | `TiltedViewMath.cs` | `SnapToTexels` | 그대로 두고 `WorldToScreenPlane` 추가. 땅 위 맞춤은 `TerrainHeightMath.SnapOnTerrain` | H0 |
| 3 | `TiltedCameraRig.cs:44` | `SnapToTexels(focus)` | **(fix-r001) `SnapScreenPlane(focus)`로 바꿈** — 땅 위 맞춘 초점에 항등(TS3 반례) | H3 |
| 4 | `WorldPresenter.cs:125` 바라보는 점 | y 0 | `FloorToWorldSnapped(center, …, grid)` | H3 |
| 5 | `WorldPresenter.cs:256-268` 보이는 범위 | `Footprint ÷ s` | 깊이 + `terrainDepthPad ÷ s`(3.6) | H5(H0~H4는 pad 0) |
| 6 | `WorldPresenter.cs:284` 대원 | 높이 없음 | 땅 위 맞춤 + 법선 | H3·H4 |
| 7 | `WorldPresenter.cs:409` 몬스터 | 높이 없음 | 땅 위 맞춤 + 법선 + 붙인 판 + `PlacementVersion` 검사 | H3·H4·H5 |
| 8 | `WorldPresenter.cs:355,444` 이동 방향 | 바닥 이동량 | **그대로**(T9) | — |
| 9 | `CombatService.cs:409` 공격 방향 | 바닥 방향 | **그대로** | — |
| 10 | `CombatWorldPresenter.cs:241` `World()` | 높이 없음 | 땅 위 맞춤. `:82,90,114,137,142,154-155,160,176-189,196` 시작·끝·연결·발동 위치가 따라감 | H3 |
| 11 | `CombatWorldPresenter.cs:242-243` `Screen()` | 바닥 방향 → 화면 | 투사체 = 국소 접선(F-03), 연결선 = 끝 − 시작 | H3 |
| 12 | `ProjectileView.cs:36-48` | 뿌리 `SnapToTexels(몸 높이 점)` | 뿌리 = 땅 위 점(이미 맞춤, `:43`은 `SnapScreenPlane`으로 — fix-r001), 몸 높이는 세운 자식(프리팹 `Upright` 0.5m 그대로), 방향 = 접선 하나로 그림·반전 | H3 |
| 13 | `BurstView.cs:15-33` | 바닥 발동 절대 y 0.001, `:28` `SnapToTexels` | `:28` → `SnapScreenPlane`(H3). `ConformedDecal`(바닥 그림 자리), 절대 y 삭제, `IEffectFrameSource`(H4) | H3·H4 |
| 14 | `DamageNumberPresenter.cs:104,122` | 발 앵커 | 땅 위 맞춤 | H3 |
| 15 | `MonsterHpBarPresenter.cs:239` | 같음 | 같음 | H3 |
| 16 | `EnemyShotPresenter.cs:42` | 같음 | 같음 + 법선 | H3·H4 |
| 17 | `MemberView.cs:92-94` + `FootShadowView.cs:30-31` | 그림자 판 수평 | 법선 기울임 | H4 |
| 18 | `UprightContactShadow.cs:51,60-61,76` | 수평면 투영 | 땅 평면 투영 | H4 |
| 19 | `MonsterView.cs:89-94,122` | 수평 판 | `ConformedDecal` 2개 + 그림자 기울임 | H4 |
| 20 | **(r001) `ProjectileView.prefab` `Shadow`(:105-121) + `ProjectileView.cs:8`** | 로컬 0.001 수평 | 법선 기울임, 배경 층·바닥 그림 자리(3.7) | H4 |
| 21 | **(r001) `EnemyShotView.prefab` `Shadow`(:95-111) + `EnemyShotView.cs:8,26-31`** | 같음 | 법선 기울임, 배경 층·바닥 그림 자리, `IEffectFrameSource` | H4 |
| 22 | `FieldView.cs:78` 땅 | 4꼭짓점 다이아몬드 | 격자 메시, uv2 | H2 |
| 23 | `FieldView.cs:86-87` 턱 | y 0 바닥 | 꼭짓점 + 높이, uv2 | H2 |
| 24 | `FieldView.cs:110,118` 물 | 절대 높이 | **코드 그대로**(물 둘레 0), TW1 | H5(확인) |
| 25 | `FieldView.cs:128` 카드 | `placement.height` | + `Sample` | H2 |
| 26 | `FieldView.cs:184` 3D 모델 | `placement.height` | + `MinUnder` | H2 |
| 27 | `FieldView.cs:214` 돌담 | 첫 토막 높이 | 토막마다 + `MinUnder(0.5)` | H2 |
| 28 | `FieldView.cs:266,286` 소품·울타리 그림자 | `placement.height`, 0.004 | + `Sample`, 그림자는 바닥 그림 자리(H4) | H2·H4 |
| 29 | `FieldView.cs:229-250` 작은 식물 묶음 | 뿌리 행렬로 묶음 | **그대로**(뿌리가 높이에 있으면 자동) | — |
| 30 | `FieldView.cs:408` `AddDecor` | y 0.001 | **그대로**(실행 중 호출처 없음, 시험만) | — |
| 31 | `UprightFootShadow.cs:14-17,45,66` | `supportY` | 호출(`FieldView.cs:150`)에서 + `Sample` | H2 |
| 32 | `HD2D_FieldSurface.shader:49` 평평한 깊이 | y 0 | `href` | H2 |
| 33 | 같은 셰이더 `:55` 물 구멍 | 절대 y | 상대 y | H2 |
| 34 | 같은 셰이더 `:98,115-122` 턱 띠·진단 | 절대 y | 상대 y | H2 |
| 35 | `HD2D_SpriteLit.shader` | 치우침 없음 | 전역 `_HD2DActorDepthBias`(3.8) | H3 |
| 36 | **(r001·fix-r001·fix-r002)** `HD2D_FootShadow`(:10,29-37)·`HD2D_ContactShadow`(:10,28-36) → `HD2D_GroundShadow`(깊이 자르기 삭제, 큐 1995), `HD2D_EffectGlow`에 `_ZTest` 칸 + 효과 짝 재질 4개(큐 1996, 강도 복사, fix-r003), 땅 무리 재질 4개(`HD2D_Field_Ground`·`Water`·`Bank`, `HD2D_Prop_Bridge` 큐 1990) | 깊이 텍스처 자르기, 투명 큐, 그림자 곱셈만 | 3.7 표 | H4 |
| 37 | `HD2D_BackgroundOutline.shader` | 깊이 경계 | **그대로**, H5 캡처 확인 | — |
| 38 | `FieldOcclusionPresenter.cs`, `OcclusionMath.cs` | 렌더러 경계 | **그대로** | — |
| 39 | `CardSnapMath.cs` | y 포함 화면 세로 | **그대로** | — |
| 40 | `PlayableProbe.cs:196` `CameraError` | 기대 y 0 | 높이 포함 | H3 |
| 41 | **(r001)** `PlayableProbe.cs:665-683,795-806,810-815` 효과 가시성·투사체 판정 | `BodyRenderer`(SpriteRenderer) | `IEffectFrameSource`(3.11) | H4 |
| 42 | **(r001)** `PlayableProbe.cs:1275~1400` 두 샘플러 | `FindObjectsByType<SpriteRenderer>`, `Matches`, `FrameNumber(sprite)` | 효과 뷰 목록 + `SourceSprite`·`ActiveRenderer`(3.11) | H4 |
| 43 | `PlayableProbe.cs:574,1334` 그 밖 뷰포트 판정 | transform·경계 | 42와 겹치는 1334 말고 **그대로** | — |
| 44 | `PlayableProbe` 새 진입점 | — | **(fix-r001, N-01) H4**: `SpawnFootVisibleCount`·`SpawnBodyVisibleCount`(3.6). **H5**: `TerrainFootError`(7절 정의), `PartyGroundHeight` | H4·H5 |
| 45 | **(r001)** `Monster`·`MonsterSpawnService.cs:152`·`MonsterRecycleService.cs:86` | — | `PlacementVersion++`(기록 칸) — **(fix-r001, N-01) H4로 앞당김** | H4 |
| 46 | `GameSceneLifetimeScope.cs` | — | 격자 등록 | H2 |
| 47 | `FieldLayoutBaker.cs` | 레이아웃·길·마스크 복사 | + `field_height.json` | H2 |
| 48 | `Tools/map_pcg/` | 높이 없음 | 지형 층(3.2) | H1 |
| 49 | `WorldPresenter.BuildField` | 타일맵 | **그대로** | — |
| 시험 | `IsoProjectionTests.cs:17`, `DamageNumberMotionTests.cs:118,124`, `CombatEffectTests.cs:220`, `TiltedViewMathTests.cs:22-26`, `FieldViewBuildTests`, `FieldLayoutTests`, `BankMeshMathTests`, `UprightContactShadowTests`, `MonsterViewStateTests`, `MemberViewSpriteTests`, `FieldModelBuildTests`, `MonsterSpawnTests`·`MonsterRecycleTests`(기록 칸) | 2인자 변환·수평 판·SpriteRenderer 기대 | 격자 판(`Flat`), 땅 위 맞춤, 기울임·붙인 판, `IEffectFrameSource` | 각 단위 |

- **수**: 49행 중 바꾸는 곳 **38곳**, 바꾸지 않거나 확인만 11곳(3·8·9·24·29·30·37·38·39·43·49).
- H3 완료 조건: `grep -rn "IsoProjection.FloorToWorld" Assets/Game/02.Scripts`의 모든 결과가 격자를 넘긴다.

## 7. 새 EditMode 시험 사례 (algorithm-tester 대상)

| # | 대상 | 입력 | 기대 |
|---|---|---|---|
| TH1 | `Sample` 꼭짓점 | 3×3 임의 값 | 저장값(1e-6) |
| TH2 | `Sample` 삼각형 안 | h00=0, h10=1, h01=2, h11=4. (0.75,0.25), (0.25,0.75) | 1.5(아래 삼각형: 0 + 0.75·1 + 0.25·3), 2.0(위: 0 + 0.75·2 + 0.25·2) |
| TH3 | 이어짐 | 대각선·칸 경계 양쪽 1e-5 | 차 < 1e-5 |
| TH4 | 메시 일치 | `TerrainMeshMath` 메시 삼각형 무게중심 높이 vs `Sample`(고정 시드 200점) | 차 < 1e-5 — **메시 접지 오차** |
| TH5 | 법선 | 0.2x 평면 | (−0.2,1,0) 정규화 |
| TH6 | 격자 밖 | 원점 밖 | 가장자리 값 |
| TH7 | `MinUnder` | 0.2x 평면, (5,5), r 0.5 | 0.9 |
| TD1 | **(r001, F-02)** 길이 불일치 | width 3, depth 3, 배열 8개 | `ArgumentException` + 오류 로그 |
| TD2 | **(F-02)** 크기·칸 | width 1 / cell 0 | `ArgumentException` |
| TD3 | **(F-02)** 불변 | 만든 뒤 원본 배열을 바꿈 | `Sample` 값 그대로 |
| TD4 | **(F-01)** 계산 속성 | 알려진 배열 | `MaxSlope`, `MaxVertexCurvature`, `ReliefWithin`, `IsFlat` 기대값 |
| TS1 | 맞춤 평지 회귀 | `Flat`, (0.013, 0.03), sin .5 | `SnapToTexels((0.013,0,0.03))` 결과 (1/48, 0, 1/24)와 같음 |
| TS2 | 땅 위 맞춤 | 0.2x+0.1z 격자, 여러 점 | 화면 가로·세로 1/48 정수배(1e-5), **y' = Sample(x', z')(1e-6)** |
| TS3 | **(fix-r001, DR-04) 카메라 재맞춤 항등** | H = 0.2z, 원래 (0, 0.02) → `SnapOnTerrain` (0, 0.00618930, 0.03094649)를 `TiltedCameraRig.SetFocus`에 넣음 | rig transform에서 복원한 초점 z = 0.03094649(1e-6), 화면 세로 = 1/48 정수배(1e-5). 비교용 옛 식(`SnapToTexels`)은 z 0.04166667, 화면 세로 0.02619342 = 1.257텍셀 → 실패해야 함. 대원 발과 초점의 화면 좌표 차가 1/48 정수배 |
| TS4 | **(r001, DR-04) 검토 반례** | H = 0.2z, (0, 0.02) | 맞춘 점 높이 오차 0(1e-6). 비교용 "맞추기 전 높이" 식은 0.002948 |
| TS5 | 삼각형 넘김 | 해가 이웃 삼각형에 있는 점 | 반복 ≤ 4, 결과 TS2 조건 |
| TP1 | 평면 투영 회귀 | 법선 up, 높이 0 | 지금 `supportY` 투영과 같음 |
| TP2 | 기운 평면 | 법선 (−0.2,1,0) | 결과가 평면 위 |
| TC1 | 붙인 판 꼭짓점 | 0.2x 평면, 가운데 (2,0,3), 2×1, 요 30°, 간격 0.25 | 꼭짓점 y = Sample + 띄움, x·z = 돌린 평평한 판, UV 네 귀 |
| TC2 | **(fix-r001)** 큰 판 모양 | 꼭짓점 공유 기울기 차 κ 0.08 등성이, 간격 0.25 | 꼭짓점 x·z = 평평한 판을 돌린 자리, y = `Sample` + 0.002. 처짐 Δy ≤ 0.01 |
| TC5 | **(fix-r003, DR-03-B) 효과 짝 재질 = 원래 재질 출력** | `HD2D_EffectGlow` 조각 식을 옮긴 순수 함수 `EffectGlowMath.Shade(rgba, core, body)`(셰이더 식과 한 줄씩 대조하는 주석)로, 원래 재질 값과 짝 재질 값을 각각 넣어 같은 입력 픽셀 비교: 코어 픽셀(sRGB (1,1,1,1), (0.97,0.96,0.9,1)), 몸통 픽셀(채도 높음 (0.4,0.9,0.5,1), 밝기 0.95 흰색), 알파 경계(a 0.49 / 0.5 / 0.51), 꼭짓점 색 알파 곱 경계 | 모든 입력에서 출력(버림 여부·색) 같음. 코어는 `_CoreIntensity` 배(치유 1.6, 파동 2.2), 몸통은 `_BodyIntensity` 배, a < 0.5 버림 — 원래와 같은 결과. 재질 대조: 짝 재질의 셰이더 = `HD2D_EffectGlow`, `_CoreIntensity`·`_BodyIntensity`·`_ZWrite` = 원래, `_ZTest` = 8, 큐 1996. 셰이더 원문 대조: 조각 함수 본문이 `082c8ee`과 같음, `ZTest [_ZTest]`, 기본 4 |
| TC3 | 평지 회귀 | `Flat` | 띄움 0.001, 지금 눕힌 판과 같은 사각형 |
| TC4 | **(fix-r001·fix-r002, DR-03) 관통 — 실제 조각 생존** | ① 셰이더 조건을 옮긴 `ShadowFragmentSurvives(silhouetteAlpha, distanceFromContact, contactRadius, bankMask, isContactOnBank)`에 검토 반례 점(가운데 z −0.01, 점 z 0.48, 접점에서 0.49m, **띄움 0.001**, 같은 광선 깊이 차 **0.06995348**) ② 같은 점 **띄움 없음**(깊이 차 0.07182389) ③ 꼭짓점 둘레 ±0.04 판(r 0.5)·무작위 격자 500점 ④ 셰이더 원문 `HD2D_GroundShadow`(효과 쪽 원문 대조는 TC5) | ①② 깊이 차와 상관없이 살아남음(true) — 옛 조건(0.4m 밖 & 깊이 차 > 0.04 → 버림)을 옮긴 비교 함수는 둘 다 false로 실패 재현 ③ 모든 점 생존, `RayGroundDepthGap` ≤ 유도 상한 0.122(근거 검산) ④ `SampleSceneDepth`·`_CameraDepthTexture` 없음, `ZTest Always` 있음. 실제 화면 생존은 H5 ③⑤⑥⑩ 캡처(경사 위 그림자 끊김 없음)로 본다 |
| TB1 | **(r001, DR-05) 평평하면 0** | `Flat` | `ActorDepthBias = 0` |
| TB2 | **(fix-r001)** 값 | 경사 0.2, w 0.15 / 0.3 | 0.110 / 0.202(3.8 광선 식). 무작위 옆 경사 200점에서 발 옆 땅의 `RayGroundDepthGap` ≤ 식 값 − 0.018 |
| TF1 | 변환 | `FloorToWorld(floor, s, grid)` | y = `Sample`, `Flat`이면 지금 값 — **H0에서 오버로드와 함께**(DR-06) |
| TV1 | **(r001, DR-01)** 데이터 계약 | 실제 `field_height.json`(배열에서 기복 계산)·`view_rules.json` | `terrainDepthPad ≥ relief·cotθ + 1/(48 sinθ)` |
| TV2 | **(fix-r001, DR-01) 당긴 점 여유 — 중심을 먼저 새 한계로** | 1920×1080, pad 0 / 0.907692. **절차: ① pad별 이동 한계 L = 32 − 반폭 − (반깊이 + pad)를 구하고 ② 시험 중심을 `ClampToDiamond(중심, L)`으로 먼저 옮긴 뒤 ③ 반 크기 (반폭, 반깊이 + pad)로 생성(원 + `ClampToDiamond(…, 32)`)·재활용(사각형 + 1.70 + 당김) 점을 계산한다. 옛 중심을 그대로 쓰지 않는다.** 사례: (a) pad 0, 중심 (−7.99656, −2.75344) → 당긴 생성점 (−17.98645, −14.01355), 깊이 여유 0.01011(반례 재현) (b) pad 0.907692 → L 9.842308, 중심이 (−7.542714, −2.299594)로 옮겨짐(1e-5), **(a)와 같은 생성 각도(228.24999°, 3.9837138 rad — (a)의 당긴 점을 내는 각)** → 생성점 **(−17.95879056, −14.04120944)**(1e-5, 검토 원문과 같음; fix-r002 정정 — fix-r001판의 (−17.924, −14.076)은 당긴 점 쪽 방향으로 잘못 계산한 값이라 같은 방향 결과가 아니고 **별도 입력**으로만 둠), 상대 x −10.416 → **가로로 화면 밖** (c) 새 이동 범위 안 중심 격자 41×41 × 방향 360개 | (a) 0.0101 (b) 가로 밖 (c) 가로로 화면 안인 생성·재활용 점의 깊이 여유 ≥ pad(1e-4) |
| TW1 | 물·다리 평평 | 실제 레이아웃 물 점·다리 + 높이 | 둘레 1.5m `Sample` = 0(1e-3) |
| C6 | `CameraError` 높이 | 중심 높이 0.3, 화면 설계 C1·C2 순서 | 오차 ≤ 0.0147 |
| N2 | 피해 숫자 높이 | 높이 0.25 몬스터 → 생성·상승·풀 재사용(높이 −0.1 자리) | 앵커 = 땅 위 맞춘 발, 이전 높이 안 남음 |
| E1 | **(r001, DR-02)** 효과 원천 | 세운 발동 / 바닥 발동 / 투사체 / 적 탄 | `ActiveRenderer`가 켜진 실제 렌더러, `SourceSprite` = 그 프레임 스프라이트, 바닥 발동의 SpriteRenderer는 원천이 아님 |
| P1 | **(r001, F-03)** 투사체 방향 | 0.2z 경사에서 대각 비행 첫 프레임·풀 재사용 | 접선 각 = 34.0°(±0.1), 그림 고르기·반전이 같은 값을 씀 |

**`TerrainFootError` 정의(r001, DR-04)**:
- 대상: 활성인 대원 뷰(`MemberView`, 쓰러짐 포함)와 활성 몬스터 뷰(`MonsterViewRegistry.Views`)의 **뿌리 transform**(= 땅 위 맞춘 발 점). 몸 자식의 대기·피격 1px 오프셋은 의도한 세운 축 오프셋이라 재지 않는다.
- 좌표: 월드 미터.
- 식: `TerrainFootError = max |root.position.y − TerrainHeightMath.Sample(grid, root.position.x, root.position.z)|`.
- 뜻: **배선 오차**(뷰가 땅 위 맞춤을 거쳤는가). 기대 ≤ 0.001(맞춤이 높이를 다시 풀어 이론값 0, 부동소수 여유).
- **메시 접지 오차**(그려진 땅 = `Sample`)는 이 진입점이 아니라 EditMode TH4로 본다(< 1e-5). 배경 도트화 패스의 낮은 해상도 깊이로 생기는 화면 차이는 캡처(H5 ③)로 본다. 셋을 섞어 보고하지 않는다.

생성기(`selftest.py`): 3.2 반례 + 결정성 + 앞 층 불변 + 내보낸 배열 재계산.

## 8. 작업 단위 (순서대로 하나씩, 에디터 하나)

- 구현은 Codex 실행기(`codex-step.py --model gpt-6.1-sol --effort medium`). 단위마다 커밋 하나(지정 파일만).
- **높이 0에서 배선부터**: H2~H4는 `field_height.json`을 평평한 판으로, `terrainDepthPad` 0으로 둔다. 치우침 전역 값도 0이다(3.8). 그래서 각 단위 뒤 화면이 같아야 하고, 이것을 **높이 0 회귀 묶음**으로 확인한다. 실제 높이는 H5에서 켠다.
- **(r001, DR-05) 높이 0 회귀 묶음**(새 `Tools/playable/hd2d-terrain-baseline-flows.json`, 1920×1080, 고정 시드·시간 멈춘 프레임). **H2 시작 전에 한 번 찍어 기준으로 두고**, H2·H3·H4 끝에 다시 찍어 비교한다:
  - B0 시작 화면 / B1 원정대 발이 3D 덤불·바위 앞뒤에 겹친 자리(가림 디더 포함) / B2 대원이 다리 위·장식 턱(0.36m) 위를 지나는 자리(D-2) / B3 투사체와 적 탄이 소품 앞을 지나는 프레임(그림자 포함) / B4 바닥 발동(치유 범위·바닥 폭발)이 소품과 겹친 프레임 / B5 폭발 예고 몬스터(폭발 범위·돌진 선)가 소품 옆 / B6 피해 숫자·HP 막대가 앞 몬스터와 겹친 프레임
  - 판정: 기준과 픽셀 차. 다른 픽셀은 화면의 0.1% 이하이고 대원·몬스터·효과 경계 상자 안에서는 0개. 넘으면 그 단위의 결함이다(땅 삼각형 분할로 생기는 래스터 차는 땅 상자 밖에서만 허용). 실행기와 디렉터가 기준·결과 쌍을 직접 연다.
- 확인은 **그 단위에서 바뀐 범위만**. 판정 경로 전체·EditMode 전체는 H6에서 한 번. **성능 측정 없음**.

| 단위 | 하는 일 | 파일(추정 수) | 완료 조건 | 그 단위 확인 |
|---|---|---|---|---|
| **H0 순수 계산·API 추가** | `TerrainHeightGrid`(검증·계산 속성), `TerrainHeightMath`(`SnapOnTerrain`, 치우침 식 포함), `TerrainMeshMath`, `GroundDecalMath`, `TiltedViewMath.WorldToScreenPlane`, `FieldHeightData`, 평평한 `field_height.json`, **`IsoProjection` 격자 판 추가(DR-06, 2인자 판은 남김)**. 연결 없음 | 새 .cs 5, 바뀐 .cs 2, json 1, 새 시험 4 (약 12) | 컴파일 0. 7절 TH·TD·TS·TP·TC·TB·TF·TV2 통과. 기존 `TiltedViewMathTests`·`IsoProjectionTests` 그대로 통과 | 컴파일. `-testFilter "TerrainHeightMathTests;TerrainHeightGridTests;TerrainMeshMathTests;GroundDecalMathTests;TiltedViewMathTests;IsoProjectionTests"`. Play 없음 |
| **H1 생성기 지형 층** | 3.2 전부(파이썬만). seed 8 높이는 `output/map-pcg/seed-8/export/`에만 | 7 | `selftest.py` 전부 PASS. 앞 층 해시 같음. 지형 지표(최대 경사·꼭짓점 곡률·기복·시작 창 |H|·언덕 수)를 `metrics.json`에(내보낸 배열에서 계산) | `selftest.py`, `run.py seeds 8`, 해시 비교, `terrain-compare.png`·`topdown.png` 직접 봄 |
| **H2 땅·배경 배선(높이 0)** | 높이 0 회귀 기준 찍기(B0~B6, 코드 변경 전) → DI 등록, `FieldView`(6절 22~28·31), 셰이더 `href`·물 구멍·턱 띠(32~34), `FieldLayoutBaker` | 약 9 | 컴파일 0. B0~B6 기준과 같음 | 컴파일. `-testFilter "FieldViewBuildTests;FieldLayoutTests;BankMeshMathTests;FieldModelBuildTests;TerrainMeshMathTests"`. 회귀 묶음 1회 |
| **H3 좌표 계약·대원·몬스터·효과·표시·카메라(높이 0)** | `IsoProjection` 2인자 판 삭제 → 6절 3·4·6·7·10·11·12·13(`:28` 맞춤)·14·15·16·40 이행, **`SnapScreenPlane`로 rig·발동·투사체 재맞춤 교체(fix-r001)**, 세운 판 전역 치우침(35), 발 불투명 반폭 w 측정, 시험 이행 | 약 16 | 컴파일 0. grep 확인. B0~B6 같음 | 컴파일. `-testFilter "IsoProjectionTests;DamageNumberMotionTests;CombatEffectTests;MonsterHpBarRuleTests;TiltedCameraRigTests;TiltedViewMathTests;TerrainHeightMathTests"` + C6·N2·P1·TS3·TB2. 판정 `f2-movement-flows.json`·`f6-feedback-flows.json` 1회씩. 회귀 묶음 1회 |
| **H4 바닥 그림·그림자·효과 판정 대상·생성 관측(높이 0)** | `ConformedDecal`, 바닥 그림 자리(큐·`ZTest Always`, 6절 36), `IEffectFrameSource`, 6절 13·17~21·28 + **판정 이행 41·42를 같은 커밋**(DR-02). **(fix-r001, N-01) 생성 관측 배선: `Monster.PlacementVersion`(45), `SpawnFootVisibleCount`·`SpawnBodyVisibleCount`(44), `hd2d-terrain-flows.json`의 ⑨ 단계를 이 단위에서 만든다.** 가장 큰 몬스터 머리 높이 b 측정(D-T7) | 약 17 | 컴파일 0. B0~B6 같음 — **B2(턱·다리)가 다르면 턱·다리를 큐 2000으로 옮긴 판에서 B2를 다시 찍어 같아야 완료**(fix-r002). `art-insert-flows.json` 효과 단계 기대값 그대로 통과. **높이 0·pad 0에서 ⑨를 돌려 몸 기준선을 `output/hd2d-terrain/spawn-baseline.json`에 기록**(발 수도 함께 — 높이 0에서도 마름모 모서리 생성으로 0이 아닐 수 있음, 그 값도 기록) | 컴파일. `-testFilter "GroundDecalMathTests;MonsterViewStateTests;MemberViewSpriteTests;UprightContactShadowTests;CombatEffectTests;MonsterSpawnTests;MonsterRecycleTests"` + E1·TC2·TC4·TC5. 판정 `art-insert-flows.json` 1회(FX·FXSEQ 단계), `hd2d-terrain-flows.json` ⑨만 1회. 회귀 묶음 1회 |
| **H5 실제 높이 켜기·지형 판정** | 하위 순서: ① H4 기준선 파일이 있는지 확인(없으면 높이 0·pad 0으로 ⑨를 먼저 잼) ② 디렉터 결정 반영 → seed 8 `field_height.json` 복사(굽기 메뉴), `terrainDepthPad`(6절 5) ③ `TerrainFootError`·`PartyGroundHeight`(44), 지형 흐름 나머지 단계, TV1·TW1 | 약 6 | 컴파일 0. 지형 흐름 통과: `TerrainFootError ≤ 0.001`, `SpawnFootVisibleCount == 0`, `SpawnBodyVisibleCount ≤ 기준선`(D-T7 (가)), 언덕 위 `PartyGroundHeight ≠ 0`. **G1~G7과 웨이브 목표 통과**(범위 값이 바뀌므로). 기준서 K21 r8(디렉터) | 컴파일. `-testFilter "TerrainDataContractTests;FieldViewBuildTests;FieldLayoutTests;MonsterSpawnTests;MonsterRecycleTests"`. 지형 흐름 1회. **`playable.py --until 7` + `wave_targets.py --metrics <기록>` 1회**(벗어나면 E7 순서로 데이터 조정 뒤 다시). 캡처 묶음 직접 봄 |
| **H6 마감** | 고치지 않는다 | 0 | 1절 완료 조건 전부 | 판정 경로 전체 1회(G1~G8), EditMode 전체 1회, `selftest.py` 1회, C10 + 지형 묶음 다시 찍어 직접 봄. 마감 리뷰 묶음 1회(병렬): validator-client, spec-reviewer(이 문서·기준서 K21 r8), algorithm-tester(7절 대상 + 생성기 경사·곡률 완화). FAIL만 수정 확인 |

**H5 지형 캡처 묶음**(`hd2d-terrain-flows.json`, 1920×1080): ① 시작 화면(지형 전 C10 시작 캡처와 나란히), ② 가장 높은 언덕 위, ③ 언덕 옆 가로 경사에서 멈춤(발 잘림), ④ 몬스터 무리가 언덕을 넘어옴, ⑤ 폭발 예고 몬스터가 경사 위, ⑥ 치유 고리가 경사에 걸침, ⑦ 단 가장자리 경사 띠, ⑧ 이동 연속 8장(언덕 넘기, 카메라 세로 떨림), **⑨(r001) 원정대를 이동 한계 네 모서리 쪽(검토 반례 같은 자리 포함)에 두고 생성·재활용이 일어나는 30초 — 발 0, 몸 기준선 이하**, **⑩(r001) 투사체·적 탄 그림자가 경사 위**.

## 9. 위험과 되돌리기

| # | 위험 | 깨질 수 있는 것 | 대응 | 되돌리기 |
|---|---|---|---|---|
| 1 | 발이 경사에서 뜨거나 묻힘 | 모습 | 땅 위 맞춤(TS2·TS4), 메시 일치(TH4), 전역 치우침. 캡처 ③ | 최대 경사 데이터 |
| 2 | 바닥 그림이 경사에 묻힘 | 예고 읽기 | 바닥 그림 자리(땅 무리 뒤·다른 3D 앞, `ZTest Always` — fix-r001), 광선 기준 근거 TC4. 캡처 ⑤⑥⑩ | 큐 값 되돌리기 |
| 3 | 몬스터가 단을 넘음 | 뛰어오름 | 걷는 땅에 절벽 없음 | — |
| 4 | 카메라 세로 출렁임 | 멀미·떨림 | 연속 높이, κ 0.08, 땅 위 맞춤. 캡처 ⑧ | R 줄이기 |
| 5 | **(r001)** 화면 밖 생성·재활용이 화면 안에 보임 | 갑자기 나타남 | 깊이 여유 pad(3.6), TV1·TV2, 진입점 발·몸 두 개. 캡처 ⑨ | R 줄이기 |
| 6 | **(r001)** pad로 이동 한계·생성 거리가 바뀜 | G5·G7·웨이브 목표 | H5에서 `--until 7` + 웨이브 목표, E7 순서로 데이터 조정 | pad 0 + 평평한 자료 |
| 7 | **(r001)** 효과 판정이 다른 렌더러를 봄 | FX·FXSEQ 오판 | `IEffectFrameSource`, 판정 이행을 바닥 그림 변경과 같은 커밋, 기대값 유지 | 커밋 단위 |
| 8 | **(r001)** 높이 0 단계에서 화면이 달라짐 | 회귀를 늦게 앎 | 전역 치우침이 평평하면 0, 바닥 그림 자리 순서는 평평하면 같은 그림, B0~B6 픽셀 비교(B2가 턱·다리 쪽 그림자 차이를 잡음, 3.7 추측) | 커밋 단위 |
| 9 | 생성기 지형 층이 앞 층을 바꿈 | seed 8 구성 | 따로 된 난수 + 마지막 층 + 해시 비교 | 층 끄기 |
| 10 | 3D 단계가 `FieldView`·셰이더를 바꾸는 중 | 줄 번호·충돌 | 3D 단계 마감 뒤 시작, grep 다시 | — |
| 11 | 사람이 플레이하는 에디터에 명령 | 테스트 실행기 오류 | `test-before-claim.md` | — |
| 12 | 비용 | 프레임 | 재지 않음(7단계). 개수만 핸드오프 | 칸 크기 데이터 |

- **전체 되돌리기(r001, fix-r002 조건부로 고침)**: 평평한 `field_height.json` + `terrainDepthPad` 0이면 높이 0, 전역 치우침 0, 보이는 범위가 이번 단계 전 값으로 돌아간다. **"데이터만으로 같은 화면"은 B0~B6이 통과한 단위까지만 성립하는 조건부 주장이다.** B0~B6이 실패하면(특히 B2 턱·다리, B3·B4·B5 그림자·효과) 데이터만으로는 복구되지 않고 아래까지 되돌린다: ① 바닥 그림자 재질을 원래 `HD2D_FootShadow`·`HD2D_ContactShadow`(투명 큐·깊이 자르기)로 ② 바닥 효과를 원래 효과 재질(`HD2D_EffectGround_*`, 남겨 둠)·본 패스·SpriteRenderer로(`ConformedDecal`·배경 층 이동 되돌림, `IEffectFrameSource`는 남아도 같은 대상) ③ 땅 무리 재질 4개 큐 값 ④ 그림자·효과 렌더러의 층(배경 층 이동). 모두 H4 커밋 단위라 커밋 되돌리기로 된다(디렉터). 남는 차이: 땅이 4꼭짓점에서 격자 메시로 바뀐 것(같은 평면, 래스터 차만), 효과 판정이 `IEffectFrameSource`를 거치는 것(같은 기대값).

## 10. 디렉터 결정이 필요한 것

**디렉터 확정(2026-11-18 18:0x, r001 반영 뒤 — 사용자 원문 "걷는 땅도 높낮이"·기준서 r8과 대조)**: D-T1 (가) R 0.5m + pad 0.908(1920×1080 이동 한계 10.75 → 9.84m, 데이터만). r8의 "언덕으로 안 읽히면 1.0m" 갈래는 (다) R 1.0 + pad 1.774(이동 한계 8.98m)로 H5 캡처 판독 뒤에만. D-T7 (가) 발 기준 유지, 몸은 높이 0 기준선 이하. D-T2~D-T6 그대로.


| # | 결정 | 보기 | 권장 |
|---|---|---|---|
| D-T1 | **(r001) 기복 크기와 깊이 여유** | (가) R 0.5m + pad 0.908(이동 한계 1920×1080 10.75 → 9.84m) / (나) R 0.3m + pad 0.561(→ 10.19m) / (다) R 1.0m + pad 1.774(→ 8.98m) / ~~규칙 값 그대로~~(반례로 불가) | **(가)**. 캡처에서 언덕이 안 읽히면 (다), 웨이브 목표 조정이 커지면 (나). 셋 다 데이터만 바뀜 |
| D-T2 | "단" | (가) 걷는 땅은 경사 띠 평지, 장식 턱 0.36m 통과 / (나) 걷는 땅 절벽 + 규칙 충돌 | **(가)** |
| D-T3 | 높이 칸 | 1m / 0.5m | **1m** |
| D-T4 | 기준서 K21 | 걷는 땅 기복(R, 최대 경사 0.2, 곡률 0.08, 단 0.3~0.45 경사 띠)을 r8 행으로 | 디렉터가 H5 전에 |
| D-T5 | 시작 시점 | 3D 배경 교체 단계 마감 커밋 뒤 | 그대로 |
| D-T6 | 최대 경사 | 0.2 / 0.25 | **0.2**, 길 0.12 |
| D-T7 | **(r001) 몸까지 화면 밖** | (가) 발 기준 지킴 + 몸은 높이 0 기준선 이하(지금도 있는 모서리 몸 노출은 이번에 고치지 않음) / (나) `pad += b/sinθ`로 몸까지(b = 가장 큰 몬스터 머리 높이, H4에서 잼; b 0.8이면 이동 한계 −1.6m 더) | **(가)**. (나)는 게임 동작 변화가 커서 따로 판단 |

## 11. 추측으로 남긴 것

- 추측: R 0.5m 언덕이 장식 턱·툰 명암과 함께 "언덕"으로 읽힌다. 확인 H5 ②⑦, 대안 D-T1 (다).
- 추측: pad 0.908로 G5·G7·웨이브 목표가 범위 안이거나 `spawnMargin` 조정으로 돌아온다. 확인 H5.
- 추측: 세운 판 전역 치우침 0.110m(fix-r001 광선 식)가 앞 3D(작은 돌·풀·줄기 밑동)와의 가림을 눈에 띄게 바꾸지 않는다. 확인 H5 ③④. 크면 w(발 반폭)를 그림에서 다시 재거나 최대 경사를 0.15로(데이터).
- 추측(fix-r002): 배경 카메라 깊이 텍스처는 불투명 큐 뒤 복사로 만들어진다(설정에 따라 앞 깊이 패스일 수도). 결정(깊이 텍스처 안 읽음)은 어느 경우든 같다.
- 턱·다리(수직 면)는 경사 증명 밖이다(fix-r002). B2가 다르면 큐 2000으로 옮겨 B2를 다시 찍는 것이 H4 완료 조건이다(3.7).
- 추측: 셰이더 uv2가 없는 다리 모델은 높이 기준 0으로 읽힌다. 확인 H2 회귀 B2. 아니면 다리에 uv2를 굽는다.
- 추측: 평평한 격자 메시와 4꼭짓점 땅의 래스터 차는 땅 상자 밖 0.1% 안이다. 확인 H2 회귀 묶음.
- 계산 값(3.6 표는 임시 스크립트 격자 검산(TV2가 이어받음) — 반례 한 점은 직접 재현 0.01011m, 3.7·3.8은 식)은 실제 화면에서 H5로 본다.

## 12. 검토 반영 (design-reviewer r001, VERDICT FAIL, 기준 `082c8ee`)

| 지적 | 확인한 사실 | 고친 내용 | 위치 |
|---|---|---|---|
| DR-01 생성 여유가 `ClampToDiamond` 보정을 빠뜨림 | 반례를 직접 재현: 중심 (−7.99656,−2.75344), 당긴 점 (−17.98645,−14.01355), 가로 −9.989, 깊이 여유 **0.01011m**(계산). 당긴 점은 마름모 변 위라 이동 한계에서 여유 하한이 0 | 규칙 범위 깊이에 `terrainDepthPad = R·cotθ + 1/(48 sinθ)`(카메라·발 맞춤 오차 포함). 이동 가능 중심 전체 × 방향 × 생성·재활용(당김 포함) 검산 표. 초안 D-T1 (가)(규칙 값 그대로) 철회 → 새 D-T1 보기 셋. 평평 띠·`spawnMargin`만 키우기는 왜 안 되는지. 몸 기준은 D-T7. 진입점을 발·몸 둘로, 대상은 실제 몸 렌더러 경계, 재활용 순간까지(`PlacementVersion`) | 3.1 A3, 3.6 전체, 3.10, 4, 5 T5, 6절 5·7·44·45, 7절 TV1·TV2, 8절 H5·⑨, 9절 5·6, 10절 D-T1·D-T7 |
| DR-02 `ConformedDecal`로 옮기면 효과 판정·연속 캡처가 SpriteRenderer를 읽어 어긋남 | `PlayableProbe.cs:669,678,795-815`, 샘플러 `:1275~1400`(`FindObjectsByType<SpriteRenderer>` :1328, `Matches` :1389), `art-insert-flows.json` FX10(:388)·FXSEQ-ground_boom(:510) | `IEffectFrameSource`(실제 활성 렌더러·원본 스프라이트·프레임·알파). 판정·두 샘플러 이행을 H4 바닥 그림 변경과 같은 커밋, 기대값 유지, H4에서 `art-insert-flows.json` 효과 단계 1회 | 3.3, 3.11 새 절, 5 T13, 6절 13·21·41·42, 7절 E1, 8절 H4 |
| DR-03 투사체·적 탄 그림자 누락, 작은 그림자 처짐 0.03 > 띄움 0.02 | 프리팹 `Shadow` 로컬 y 0.001·Euler(90)·배율 (1,2,1)(`ProjectileView.prefab:105-121`, `EnemyShotView.prefab:95-111`) | 두 그림자를 소비처에(기울임). 곡률 제한을 "꼭짓점 공유 삼각형 쌍 κ 0.08"로 바꿔 r ≤ 0.5 판의 처짐 상한을 `r·κ`로 닫음. 띄움 대신 크기·곡률에서 계산한 깊이 치우침. 반례 TC4(꼭짓점 둘레 ±0.04, r 0.5 / r 0.6 거부) | 3.2-6, 3.7, 3.8, 6절 20·21·36, 7절 TC4, 8절 ⑩ |
| DR-04 `TerrainFootError` 정의 없음, 맞추기 전 높이로 0.002948m | 반례 식 확인(H = 0.2z, z 0.02) | 땅 위 맞춤 `SnapOnTerrain`(맞춘 x·z에서 높이를 삼각형 안 선형식으로 다시 풂, 이웃 삼각형 넘김 ≤ 4). 진입점 정의(대상 = 뿌리 transform, 월드 m, 식, 기대 ≤ 0.001). 배선 오차(진입점)·메시 접지 오차(TH4)·화면 차(캡처) 분리 | 3.1 A4, 3.4, 3.5, 7절 TS2·TS4·TS5와 정의 문단 |
| DR-05 높이 0이어도 치우침 0.05·띄움 0.02가 남아 복원 주장 불성립 | 초안 3.8이 재질 값 고정 | 치우침·띄움을 재질이 아니라 격자에서 계산한 전역 값으로(평평하면 0, 띄움 0.001). 재질 자산 무변경. 높이 0 회귀 묶음 B0~B6(소품과 겹친 발·효과·투사체·숫자, 다리·턱 위)을 H2 전 기준 → H2·H3·H4 비교. 되돌리기 문단 다시 씀 | 2절 재질 행, 3.8, 5 T10·T12, 7절 TB1·TB2, 8절 머리·H2~H4, 9절 8·되돌리기 |
| DR-06 H0 TF1이 H2 오버로드를 부름 | 초안 단위 순서 모순 | 격자 오버로드 추가를 H0로, 2인자 판 삭제·소비처 이행은 H3 | 3.3, 3.4, 6절 1, 8절 H0·H3 |
| F-01 요약 메타데이터 대신 배열 재계산 | — | `relief`·`maxSlope` 칸 삭제, 생성기 검사·TV1·계산 속성이 내보낸 배열에서 계산 | 3.2, 3.3, 7절 TD4·TV1 |
| F-02 실패 계약·불변 배열 | — | 길이·크기·칸 검사 + 오류 로그 + 예외, 복사 보관 | 3.2, 7절 TD1~TD3 |
| F-03 투사체 방향 두 점 | — | 국소 접선 하나(0.25m), 그림·반전에 같은 값 | 3.4, 5 T14, 6절 11·12, 7절 P1 |

- 수정 확인은 같은 검토자에게 한 번 받는다. 결과 파일은 `design-review-fix-r001.md`.

### fix-r001 — VERDICT: FAIL (Codex, 11-18 17:3x, `design-review-fix-r001.md`)

DR-02·DR-05·DR-06은 해결 판정. 남은 필수 4건과 후속을 반영했다.

| 지적 | 확인한 사실 | 고친 내용 | 위치 |
|---|---|---|---|
| DR-01 TV2가 옛 중심을 그대로 씀 | 옛 중심 (−7.99656,−2.75344)은 pad 0.907692의 새 한계 9.842308 밖. 새 한계로 당기면 (−7.542714, −2.299594)(계산 재현), 같은 방향 생성점 상대 x −10.38 → 가로 밖 | TV2 절차: pad별 한계 → 중심 먼저 당김 → 늘어난 반 크기로 생성·재활용. 반례 수치를 사례 (a)(b)로, 새 범위 격자 사례 (c) | 7절 TV2 |
| DR-03 치우침을 Δy·sinθ로 환산(방향 틀림) | 같은 광선 위 땅 교점까지 거리 `Δy/(sinθ + s·cosθ)`. 검토 반례 재현 0.0718(원문 0.069953, 평면 높이 읽기 차) > 0.022 | 광선 식으로 다시 유도: 작은 판 상한 0.122, 큰 판 0.031. 0.122는 무리 겹침(뒤 몬스터 그림자가 앞 몬스터 발을 덮음)을 깨므로 **치우침을 버리고 배경 패스 "바닥 그림 자리"(땅 무리 큐 1990 → 바닥 그림 1995 `ZTest Always` → 다른 3D 2000+)로**. 세운 판 치우침도 광선 식으로 0.110. TC4·TC2·TB2를 광선 기준으로 | 3.1 A5, 3.3, 3.7 전체, 3.8, 2절 재질 행, 5 T8·T10, 6절 20·21·28·36, 7절 TC2·TC4·TB1·TB2, 9절 2·8·되돌리기, 11절, 13절 4·5 |
| DR-04 rig가 땅 위 맞춘 초점을 평지 방식으로 다시 맞춤 | `TiltedCameraRig.cs:44` `SnapToTexels` → z′ 0.03094649를 0.04166667로, 화면 세로 1.257텍셀(계산 재현). `BurstView.cs:28`·`ProjectileView.cs:43`도 같은 재맞춤 | `SnapScreenPlane`(y 유지·z 풀기·항등)으로 셋 교체, TS3에 반례 | 3.3, 3.5, 6절 3·12·13, 7절 TS3, 8절 H3, 13절 5b |
| N-01 H4 기준선 측정 불가 | 진입점·`PlacementVersion`·흐름이 H5에 있었음 | 관측 배선과 흐름 ⑨를 H4로 앞당기고 기준선 파일을 H4에서 기록. H5 하위 순서 ① 기준선 확인 ② 높이·pad ③ 나머지 | 3.6, 6절 44·45, 8절 H4·H5 |
| 후속: `:3` 문구, 단계 브리핑 기준 HEAD | — | 상태 줄을 "결정 확정"으로, 단계 브리핑 기준을 `082c8ee`로 | 머리, 단계 브리핑 |

- 수정 확인 2회차 결과 파일은 `design-review-fix-r002.md`.

### fix-r002 — VERDICT: FAIL (Codex, 11-18 17:4x, `design-review-fix-r002.md`)

DR-01·DR-04·N-01은 해결 판정. 남은 DR-03 두 건과 정정을 반영했다.

| 지적 | 확인한 사실 | 고친 내용 | 위치 |
|---|---|---|---|
| DR-03-A 그림자 셰이더의 깊이 자르기가 남음 | `HD2D_ContactShadow.shader:28-34`·`HD2D_FootShadow.shader:29-37`이 `SampleSceneDepth`로 받는 면 깊이를 읽고 접점 0.4m 밖·깊이 차 0.04 초과 조각을 버림. 반례 깊이 차 0.06995348(띄움 0.001)/0.07182389(띄움 없음) → 버려짐. 큐 1995는 불투명 구간이라 이번 프레임 깊이 텍스처가 없고, 있어도 3D까지 든 깊이 | 깊이 텍스처 읽기와 0.4m·0.04m 자르기 삭제(새 `HD2D_GroundShadow`). 남는 조각 조건(실루엣 알파·반지름·턱 마스크)은 깊이와 무관. 깊이 텍스처 생성 시점 명시. TC4를 셰이더 조건을 옮긴 순수 함수의 조각 생존 + 셰이더 원문 검사로 | 3.3, 3.7, 3.8, 6절 36, 7절 TC4, 11절 |
| DR-03-B 그림자 곱셈 셰이더로 효과까지 그림 | 그림자 셰이더는 알파만 읽고 고정 색을 `Blend DstColor Zero`로 곱함(`:10,36/39`). 배경 RT는 `ARGBHalf`(`HD2DBackgroundRig.cs:75-77`), 합성은 색 그대로(`HD2D_BackgroundComposite.shader:24`) | `HD2D_GroundShadow`(곱셈·스텐실·1995)와 `HD2D_GroundEffect`(색·알파·`_Emission` HDR·1996·스텐실 안 씀) 분리, 정책 표. "배경 비HDR" 예외·0.031 치우침 예외 삭제. TC5 | 3.7 표, 3.8, 5 T8 그대로, 6절 36, 7절 TC5, 13절 5 |
| 정정: DR-01 생성점 | (a)의 당긴 점을 내는 각 228.24999°로 새 중심에서 계산하면 (−17.95879056, −14.04120944) — 검토 원문과 같음(계산 재현). fix-r001판의 (−17.924, −14.076)은 당긴 점 쪽 방향으로 잘못 계산 | TV2 (b) 기대를 원문 점으로, 옛 값은 별도 입력으로만 | 7절 TV2 |
| 정정: 깊이 차 두 수치 | 띄움 없음 0.07182389, 띄움 0.001 0.06995348 | 두 값을 띄움 조건과 함께 반례 재현·TC4에 | 3.7, 7절 TC4 |
| 정정: 평평한 자료 복원 | B0~B6 통과 전에는 조건부 | 조건부로 고치고 실패 시 복구 범위(그림자·효과 재질, 본 패스 이동, 큐, 층) 명시 | 9절 되돌리기 |
| 정정: 수직 턱·다리 | 경사 증명 밖 | B2 기준 비교와, 다르면 큐 2000 이동 뒤 B2 재확인을 H4 완료 조건으로 | 3.7, 8절 H4, 11절 |

- 수정 확인 3회차 결과 파일은 `design-review-fix-r003.md`.

### fix-r003 — VERDICT: FAIL (Codex, 11-18 17:5x, `design-review-fix-r003.md`)

DR-03-A와 정정 4건은 해결 판정. 남은 1건을 반영했다.

| 지적 | 확인한 사실 | 고친 내용 | 위치 |
|---|---|---|---|
| DR-03-B 단일 `_Emission`으로는 발광 보존 불가, 알파 블렌딩도 원래와 다름 | `HD2D_EffectGlow.shader:43-53`(알파 0.5 버림, sRGB 밝기·채도로 코어/몸통, 두 강도), 바닥 효과 재질 4개(`HD2D_EffectGround_Boom` 1.6/1, `_Heal` 1.6/1, `_Pulse` 2.2/1, `_Warn` 1/1, 모두 큐 2440·`_ZWrite` 0, 측정) | 새 효과 셰이더를 없앰. 기존 `HD2D_EffectGlow`에 `_ZTest` 속성 한 칸만 더하고(기본 LEqual — 기존 재질 동작 같음), 짝 재질 4개는 원래 재질 복사 + `_ZTest` Always·큐 1996. 조각 계산·알파 클립·강도 값 그대로. TC5를 원래/짝 재질 같은 입력의 코어·몸통·알파 경계 출력 비교로. `PlayableProbe.cs:1360` `_CoreIntensity` 소비는 읽는 렌더러만 `ActiveRenderer`로, 식 그대로 | 3.3, 3.7 표·짝 재질 문단, 3.8, 6절 36, 7절 TC5, 9절 되돌리기, 13절 5 |
| F-01 턱·다리 큐 2000일 때 윗면 위 그림자·효과 | — | 단계 지시 확인 항목(B2에서 뒤쪽·윗면 모두) | 단계 지시 |
| F-02 물 위 덧그리기 | — | 단계 지시 확인 항목(B·H5 캡처에서 물 위 그림자·효과) | 단계 지시 |

- 수정 확인 4회차 결과 파일은 `design-review-fix-r004.md`.


## 13. design-reviewer에 넘길 요약

1. 무엇: 2D 바닥 좌표 규칙은 그대로, 그리는 쪽에 땅 높이 `H(x,z)`. 높이는 PCG 마지막 층(seed 8, 따로 된 난수)이 `field_height.json`(1m, mm 정수, 요약 칸 없음)으로 내고, `TerrainHeightMath.Sample`(메시와 같은 삼각형 선형)이 굽기와 모든 소비처에 같은 값을 준다.
2. 좌표 계약: H0에서 격자 판 추가, H3에서 2인자 판 삭제와 소비처 이행. 그리는 위치는 땅 위 맞춤(화면 텍셀 정수배 + 높이 재계산, 배선 오차 0).
3. 게임 동작: 기복이 있으면 규칙 범위 깊이에 `terrainDepthPad = R·cotθ + 1/(48 sinθ)`가 필요하다(당긴 생성점 반례). R 0.5 → pad 0.908, 이동 한계 −0.91m. G5·G7·웨이브 목표 재측정 + 데이터 조정. 몸 기준은 기준선 이하(D-T7).
4. 셰이더: 땅 평평한 깊이 기준을 꼭짓점 땅 높이(uv2)로, 물 구멍·턱 띠를 상대 높이로. 세운 판 깊이 치우침은 같은 화면 광선 기준 식 `S·w/(sinθ − S·cosθ) + 0.018`(0.110m)이고 격자에서 계산한 전역 값(평평하면 0).
5. 바닥 그림(fix-r001·fix-r002): 작은 것 기울임, 큰 것 격자 붙임, **배경 패스에서 땅 무리(큐 1990) → 그림자 곱셈 재질(1995, 스텐실로 겹침 한 번) → 효과 짝 재질(1996, 기존 `HD2D_EffectGlow` 계산·코어/몸통 강도·알파 클립 그대로, `_ZTest`만 Always — fix-r003) → 다른 3D(2000+)**, 둘 다 `ZTest Always`, **깊이 텍스처를 읽지 않음**(지금 그림자 셰이더의 0.4m·0.04m 깊이 자르기 삭제 — 불투명 큐 시점엔 이번 프레임 깊이 텍스처가 없고, 있어도 3D까지 든 깊이). 검토 반례(띄움 0.001, 깊이 차 0.06995348) 조각이 살아남는지 TC4로. 수직 턱·다리는 증명 밖 — B2로 확인, 다르면 큐 2000. 배경 RT는 ARGBHalf라 발광 예외 없음.
5b. 텍셀 계약(fix-r001): 카메라·발동·투사체의 재맞춤을 `SnapScreenPlane`(y 유지, z 풀기, 이미 맞춘 점에 항등)으로 바꿔 카메라 초점도 같은 화면 평면 텍셀 계약을 따른다.
6. 효과 판정: `IEffectFrameSource`로 실제 렌더러·원본 스프라이트를 내고, 판정·샘플러 이행을 같은 커밋에서. 기대값 그대로.
7. DI: Scoped `TerrainHeightGrid` 하나, 뷰 쪽 Presenter 5개만 받음. 규칙 서비스는 기록 칸 `PlacementVersion` 올리기만.
8. 확인하고 싶은 위험: (a) pad 식이 생성·재활용·당김·맞춤 오차를 모두 닫는가, (b) 높이 0 회귀 묶음(B0~B6)이 H2~H4 회귀를 잡기에 충분한가, (c) `_FlatDepth` 뜻 변경이 D-2를 지키는가(다리 uv2 추측), (d) `IEffectFrameSource` 이행이 FX·FXSEQ 기대를 그대로 두는가, (e) 단위 순서가 컴파일을 늘 녹색으로 유지하는가.
9. 단위: H0 순수 계산·API 추가 → H1 생성기 → H2 땅·배경(높이 0) → H3 좌표 계약·카메라(높이 0) → H4 바닥 그림·효과 판정(높이 0) → H5 실제 높이·pad·지형 판정·G1~G7·웨이브 목표 → H6 마감. 성능 측정 없음.
