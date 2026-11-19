# H0~H6 마감 리뷰 수정 확인 r001

세 리뷰 모두 FAIL이므로 `validator-client`, `spec-reviewer`, `algorithm-tester` 모두 수정 확인을 요청한다. 앞 리뷰는 Codex gpt-6.1-sol medium, Claude 중계였고 이번 구현자는 그 리뷰를 실행하지 않았다. 받은 판정 원문은 각각 **`VERDICT: FAIL`**이다. 작업 전 HEAD `b6089d6cfcdd94b12593bcb2569c87bb70ad85e8`, 게임 구현 전체 기준 `e521d0b`. 이번 변경·시험·캡처 원본은 `output/hd2d-terrain/recheck1/`, 사본은 `.director/evidence/20261118-hd2d-terrain/1/4/recheck1/`.

## 승인 설계 기준과 구현 비교 범위 분리

spec-reviewer의 설계 파일 변경 지적을 D-T8~D-T11 되돌리기로 해결하지 않았다. 이 계약은 디렉터 지시문과 대화로 이미 승인됐다. 승인 설계 기준은 **`b6089d6cfcdd94b12593bcb2569c87bb70ad85e8:docs/features/hd2d-terrain-height/design-brief.md`**, Git blob **`4cd12a5d13d7504d15d6c442719684f8e27e8c72`**로 고정한다. 이번 수정에서 그 파일은 바꾸지 않았다.

- spec-reviewer 수정 확인 base = **b6089d6**. 역할 지침 4의 설계 파일 검사는 `git diff b6089d6..HEAD -- docs/features/hd2d-terrain-height/design-brief.md`(빈 결과)를 본다.
- H0~H6 누적 구현 이행은 e521d0b부터의 **코드·데이터·시험·생성기** diff와 승인 설계 b6089d6 판을 대조한다. 승인 설계 자체의 D-T8~D-T11 변경은 이 구현 diff의 미승인 변경으로 세지 않는다. 별도로 보려면 `git diff e521d0b b6089d6 -- docs/features/hd2d-terrain-height/design-brief.md`와 지시문 끝의 각 디렉터 결정을 대조한다.
- 수용 기준 H0~H6, B0~B6, TH1~TH7, TD1~TD4, TS1~TS5, TP1~TP2, TC1~TC5, TB1~TB2, TF1, TV1~TV2, TW1, K21 r8은 유지한다. 설계·테스트 기준을 낮추지 않았다.

## 필수 지적별 수정

| 리뷰·지적 | 수정·확인 방법 |
|---|---|
| validator P1, editor 전용 sampler 두 개가 #endif 밖 | PlayableTerrainWalkSampler/SpawnSampler를 파일의 UNITY_EDITOR 블록 안으로 이동. Editor 컴파일 외에 StandaloneWindows64 Player C# 산출물 Game.dll을 생성하고 Cecil 메타데이터에서 두 sampler/PlayableProbe와 UnityEditor 참조가 없는지 확인. 네이티브 게임 전체 빌드와 구분한다. |
| validator P2, 큐 상속 -1이 2000으로 고정됨 | `FieldView` 저장값을 material.renderQueue 대신 **rawRenderQueue**로 바꿈. raw -1과 명시2017 두 사례에서 비평지 Build→1990→Destroy 후 각각 원래 raw/effective/keyword 복원 시험. 실제 지형 Play 전후 ground/bank/water/bridge의 raw/effective/keyword도 비교한다. 이번에는 재질 바이트 수동 복원으로 닫지 않는다. |
| spec TH1/TH3 | 임의 비평면 3×3의 아홉 꼭짓점과 수평·수직 칸 경계 양쪽 ±1e-5 사례 추가. 기존 2×2 대각·보간 시험도 유지. |
| spec TC4 | 기존 고정 등성이 500점 외에, 고정 난수로 서로 다른 경사/곡률 제한 격자 500개를 만들어 접점 주변 반경0.5m의 평면 투영 조각을 검사. 옛 깊이 자르기 반례와 .122m 상한을 유지. |
| spec TC5 | sampled texture alpha .8 × vertex-colour alpha로 곱한 결과가 .49/.50/.51이 되는 경계를 실제 입력 Color 곱으로 검사. 원래/짝 재질 버림·출력 동일과 코어/몸통 사례 유지. |
| spec H5 ⑧ | (−3,21)에서 +x 방향으로 정상 PartyMoveService 입력을 계속 주며 30Hz 시뮬레이션의 36프레임(1.2초)마다 8장 기록. 오르막→중간 정점→내리막 및 양 끝보다 정점이 .05m 이상 높음 assertion 추가. 253개 연속 시뮬레이션 프레임 전체의 최대 발오차·카메라 오차도 검사. **8장이 인접한 30Hz 프레임이라는 옛 주장은 폐기**하고 명시한 간격의 연속 이동 표본으로 기록. |
| algorithm AT-01 | C# ReliefWithin·Python clipped_relief에서 땅 삼각형을 마름모 네 반평면으로 잘라 교차점 높이까지 min/max에 포함. 제한을 만족하는 .01x 평면의 halfDiagonal=25.5m에서 옛 .50→정답 .51, 마름모 전체가 한 칸 안인 경우 .01과 H0 점 영역도 회귀 시험. Python 최소2×2 curvature의 All-NaN 경고도 유효쌍만 집계하도록 제거. |
| algorithm AT-02 | 화면1e-5텍셀·접지1e-6m 문턱 그대로. far float의 ULP가 텍셀 문턱보다 커서 일반 Newton의 월드 절대오차 문턱과 달랐음. 네 삼각형 반복 뒤 표현 가능한 이웃 z와 기존 접지1e-6m 안의 y를 골라 **실제 float 화면식**을 만족시킴. 구현 검증도 같은 텍셀 문턱을 사용. 이미 정렬된 점의 SnapScreenPlane 항등 유지. 실제 지형 전 좌표영역 145×145점+임의10,000점+리뷰 반례에서 동일 문턱 검사. |

### 언덕 횡단 fixture의 범위

seed8의 가장 큰 언덕은 정상 카메라 중심 이동 한계 밖이다. 정상 이동 한계를 바꾼 게임 결과로 보고하지 않는다. 촬영 중에만 기존 `PartyMoveService.SetViewHalfExtent(Vector2.zero)` API로 fixture 이동 범위를 들판 전체로 놓고, **이동 속도·지형 높이·pad·spawnMargin·규칙 서비스 코드는 바꾸지 않는다**. 각 프레임의 입력/이동/그림/카메라는 실제 서비스를 거친다. 촬영 종료·중단 시 원래 ViewHalfExtent를 복원한다. 넓어진 화면 밖 검은 배경은 이 fixture의 한계다. 기존 C10은 정상 이동 한계 검증으로 따로 유지한다. 이를 플레이어가 정상 이동 한계 안에서 최고 언덕을 넘었다는 증거로 쓰지 않는다.

## 열린 지적·판정 밖 항목

- validator-client: G20의 높이 변화가 잘 읽히는지 디렉터 판독이 남는다. 전체 프레임열·다른 화면비·노치 안전영역은 해당 리뷰에서 화면 확인하지 않았다.
- spec-reviewer: 최종 G20/K21 시각 채택은 디렉터가 판독한다. 발 반폭 문서의 0.15m 상한과 실제 측정0.625m 차이는 문구 정리 후속 제안으로 보존하며 이번에 승인 설계 파일을 임의 수정하지 않았다.
- algorithm-tester: 앞 리뷰는 Unity 전체 EditMode·native Mesh·실제 rig Transform·GPU 픽셀을 실행하지 않았다. 외부 폴더 algorithm-review.md는 경로가 전달되지 않아 이 프로젝트 안으로 복사하지 못했고, 받은 반례/기대값/결과를 이 브리핑과 회귀 시험으로 보존한다.
- 물 위 그림자/효과 원판 인상 F-02는 선택 후속. G09·G11[미보정]·풀 얼룩·강 경계·배낭 명도는6단계, G19 성능은7단계. 이것들로 이번 필수 수정 반복을 열지 않는다.

## 검증 범위

영향이 있는 일곱 EditMode filter만 실행: TerrainHeightGridTests3, TerrainHeightMathTests8, GroundDecalMathTests4, TiltedViewMathTests11, TiltedCameraRigTests3, FieldViewBuildTests7, TerrainDataContractTests4 = **40/40**. Python selftest **48/48**, seed8 재생성 violations0 및 앞6파일/높이 배열 동일. 지형 흐름 전체는 해당 수정이 닿는 34단계만 재실행한다. 앞 H6 전체606/606·G1~G8 8/8·C10 20/20은 b6089d6 시점의 과거 기록이고 새 실행으로 부르지 않는다.

Player compile API 요청은 pipeline server 30초 제한으로 응답 timeout이었으나 실행은 완료돼 새 Game.dll을 생성했다. **명령 응답 성공으로 쓰지 않는다.** 이후 Cecil 메타데이터 확인에서 타입234개·GameSceneLifetimeScope 있음·PlayableProbe/PlayableTerrain* 없음·UnityEditor 참조0을 확인했다. 전체 네이티브 Player 빌드는 NOT_RUN. 첫 메타데이터 직접 참조는 Mono.Cecil/Unity.Burst.Cecil 이름 충돌로 도구 스니펫 컴파일 실패, reflection으로 읽어 해결했다. 이는 프로젝트 C# 컴파일 실패와 구분한다.

## 이번 실행 결과

컴파일 에러0(기존 FindObjects API 등의 obsolete 경고45), 지정 EditMode40/40·실패/건너뜀0. PCG selftest48/48, seed8 score8.51·violations0, 기존6파일 SHA256과 굽힌 높이 바이트 동일. 실제 Player Game.dll 메타데이터에 에디터 전용 타입/UnityEditor 참조0. 지형34/34(117.7초), 발 진입0·몸21≤26·TerrainFootError0. 재질4종의 raw -1·effective queue·키워드 Play 전후 동일, 재질 파일 diff0.

언덕8장(253시뮬레이션 프레임 동안 이동): x−2.947267→10.34155,z21, H 0.1042148→0.397122(5번째)→0.1275317m. 표본 발오차0·표본 카메라 최대0.01196973m. 전체253프레임의 최대도 흐름 판정에서 발≤.001·카메라≤.021을 확인. `hill-crossing-eight.png`에서 전체/발 확대8장을 직접 열었다. 최고언덕·몬스터 이동 전후·경사 예고·치유·투사체·물·N9 등 이번 지형 캡처도 직접 열었다.

AT-02 원 반례(2.1666667,−24.757904): 실제 float 화면 잔차0텍셀·접지오차2.533197e−7m. 임계값1e−5텍셀/1e−6m를 유지했다. G20/K21 시각 채택과 선택 후속 F-02는 닫지 않았다.

놓친 원인과 갱신 후보: editor compile과 non-editor compile을 구분하지 않은 것→Player 산출물 타입/참조 검사로 보완; Material effective/raw 상태 혼동→raw 상속 회귀 및 실제 Play 전후 비교로 보완; 격자 꼭짓점 검사만으로 연속영역 extrema를 증명한 것→clip 경계 반례 추가; 좁은 좌표 범위/프레임 간격으로 언덕 횡단을 주장한 것→전체 좌표 시험 및 실제 오름/정점/내림 CSV로 보완. 공통 킷/워크플로우 파일은 범위 밖이라 수정하지 않았고 이 기능 검증 문서에 반영했다. 추가한 검사는 각각 실제 지적을 잡는다. 기존 전체606/전체플레이를 매 수정마다 반복하는 대신 이번 관련40/지형34만 실행했다.
