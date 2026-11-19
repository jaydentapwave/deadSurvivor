# 지형 H0~H6 마감 리뷰 브리핑 — attempt 4

구현 기준 커밋은 `e521d0b`(3D 배경 PR #4 마감), H5는 `971a61622a573dabe06e3413dfd4de93cc5f1d8e`다. H6 문서 커밋 이후 최종 HEAD·실행 시각·원시 결과는 `.director/outbox/20261118-hd2d-terrain-1.md`와 `.director/evidence/20261118-hd2d-terrain/1/4/`를 본다. 이 문서는 리뷰 결과가 아니다. Codex는 리뷰 에이전트를 실행하지 않았고 외부 Claude 세션의 독립 리뷰를 요청한다.

## 요청할 검토

| 에이전트 | 기준·범위 |
|---|---|
| validator-client | 설계 1절 완료 조건·8절 H0~H6. Game/HD2D.View 어셈블리 경계, DI와 생명주기, 격자 전달 누락, 효과·그림자 배선, PlayableProbe 관측의 실제 렌더러/PlacementVersion 사용. 명령 관문·캡처 근거와 주장 대조. |
| spec-reviewer | `design-brief.md` 전체, `docs/art/아트-기준서.md` K21 r8, 지시문 D-T8~D-T11, 설계 6절 소비처 49행, H0~H6 순서와 범위, 높이 0 B0~B6 회귀 기준 ≤0.1%·대원/몬스터/효과 상자 안 0. |
| algorithm-tester | 설계 7절 TH1~TH7·TD1~TD4·TS1~TS5·TP/TC/TB/TF·TV1/TV2·TW1 전부, `TerrainHeightGrid/Math`, `TerrainMeshMath`, `GroundDecalMath`, `TiltedViewMath`, 생성기 `Tools/map_pcg/terrain.py`의 삼각형 법선·곡률/경사 완화와 반올림 후 검사. 새 반례를 독립적으로 확인. |

공통 확인 파일: `Assets/Game/02.Scripts/Effect/HD2DView/Utils/`, `Features/Field/`, `Features/Combat/`, `App/PlayableProbe.cs`, `GameSceneLifetimeScope.cs`, `Assets/Game/03.Resources/Shaders/HD2D/`, 바닥 효과 짝 재질·프리팹, `Assets/Game/Tests/EditMode/*Terrain*Tests.cs`, `GroundDecalMathTests.cs`, `TiltedViewMathTests.cs`, `Tools/playable/hd2d-terrain-{baseline-flows,flows}.json`, `hd2d-map-c10.json`, `Tools/map_pcg/{terrain,selftest,run}.py`. 실제 파일·행은 attempt 4 소비처 표 참조.

## 디렉터가 확정한 계약

- **D-T8로 바뀐 계약:** `TerrainHeightGrid.FromMillimetres`는 HD2D.View의 순수 생성기이므로 실패 시 `ArgumentException`만 던진다. Game 로딩 경계에서 `GameLogger.LogError` 뒤 재던진다. 순수 TD1과 Game 로딩 시험을 분리했다. 공통 어셈블리 추가 없음.
- **D-T9:** 평지는 H1 `26a53cb`와 같은 메시(uv2 없음), `_TERRAIN_HEIGHT` OFF 셰이더 식·varying. 비평지만 새 경로. H2~H4 B0~B6 각 0픽셀 차이는 attempt 3 기록이며 현재 실행으로 가장하지 않는다. 강제 ON·높이 0 B0 진단은 7,576픽셀, 0.3653549383%, actor/effect 상자 0. 차이는 좌상단 다리 두 타일에 집중했다.
- **D-T10:** `spawn-baseline.json`의 CLI 시간 관측 18회를 보존. 새 `spawn-baseline-exact30.json`은 frozen start·seed 8·네 모퉁이 각 7.5초·captureDeltaTime 1/30의 동일 N9 흐름으로 평지·pad 0을 두 번 측정: 26/26 → 기준 26. attempt 3 진단 22회에는 관측 전 CLI 지연 동안 진행한 시간이 있었으므로 이번에는 Depart 직후 시계를 멈추고 같은 초기 상태에서 관측을 시작했다. 지형도 정확히 같은 시작 방식. 판정식은 발 0·몸 ≤평지, pad 0.907692·spawnMargin 70 그대로. H5 최종 발 0·몸 21·TerrainFootError 0.
- **D-T11:** C10 목표는 `현재 한계 L − (10.75 − 옛 목표)`; 1920×1080 L=9.842308. 대각 목표는 같은 L1 한계 안쪽 거리와 방향을 보존한다. 10.5/10.74 판정 위치는 각각 L−0.25/L−0.01로, 그 밖의 항목·문턱·허용 오차는 유지했다. 20단계와 네 방향 가장자리 촬영을 보존했다. `TiltedViewMath.RemapBoundaryPoint`, `PlayableProbe.TerrainCaptureTarget/Threshold`, C10 MOVE/EDGE 단계 확인.

## 화면·수치 해석의 한계

- 최종 지형 흐름은 T4 전후 실제 `MonsterService.TickMove` 60회(2초)를 추가한 34단계. 8마리 이동 최대 3.6386075m·높이 변화 0.2406582m·발 오차 0. 파티/카메라를 고정한 촬영 fixture이며 최고 언덕 fixture도 정상 파티 이동 한계 밖이다. 이를 플레이어가 정상 이동으로 최고 언덕에 도달했다는 증거로 쓰지 않는다.
- 정상 이동 30Hz 연속 8장은 실제 PartyGroundHeight 0.0076746→0.01144055m의 작은 변화다. R 0.5m의 언덕이 충분히 읽히는지 G20은 디렉터가 판독한다. R 1.0으로 임의 변경하지 않았다.
- 불투명 발 반폭 w는 실제 그림에서 최대 0.625m, 실제 생성 지형 경사에 따른 전역 actor bias 0.1284885m. 최대 몬스터 머리 높이 b=1.104167m. `_BackgroundCard`는 팔레트 플래그여서 bias 제외 조건에서 제거했다(대원 재질 값은 그대로).
- F-01: 평지 B2 차이 0이라 턱·다리를 queue 2000으로 옮기는 조건이 발생하지 않았다. F-02: 연못·개울 위 그림자/효과를 캡처했다. 원판처럼 덧그려지는 인상은 남으며 선택 후속안은 물 마스크로 바닥 그림을 자르는 것이다. 필수 조건으로 승격하지 않는다.
- 땅 삼각형 5,192개(평지 2개), 경사 warning 붙인 판 2·heal 1. 개수만 기록했고 그리기 수·삼각형 수 성능 계측이나 300마리 장면을 실행하지 않았다.

## 이 단계 판정 밖

G09 효과 ON 번짐, G11 배경 `[미보정]`, 풀밭 얼룩 반복·강 경계 선·대원 배낭 명도는 **6단계 몫**. G19 성능은 **7단계 몫**. 이 항목으로 이번 수정 반복을 열지 않는다. G20은 디렉터 판독이다. 독립 리뷰에서 FAIL이면 영향 범위만 고쳐 같은 확인을 다시 하고, 같은 지적 수정 2회 실패 상한을 따른다.

## H6 명령 결과 (2026-11-19 13:24~13:34 KST)

컴파일 errors0/warnings0, 전체 G1~G8 8/8(154.4초), 전체 EditMode606/606(실패·건너뜀0), selftest45/45, C10 20/20(233.5초), 지형34/34(105.9초), 웨이브42개 FAIL0. 최종 지형 발0·몸21≤26·TerrainFootError0. C10 10장·지형 각 fixture·정상보행8장·시작 화면 쌍을 직접 열었다.

전체 검증 뒤 초기 clean 재질15개의 Unity 직렬화 차이(renderQueue/keyword/새 셰이더 기본 속성)가 생겨 diff와 파일을 `a4-h6-material-serialization*`로 보존하고 H5 HEAD 바이트로 복원했다. 깊이/띄움/빛 값을 바꾸지 않았다. FieldView 공유 재질 상태 저장·복원 생명주기는 validator-client가 함께 확인한다. 다른 세션 dirty 파일은 보존했다. 에디터 Play false로 종료했고 복구 명령은 사용하지 않았다.
