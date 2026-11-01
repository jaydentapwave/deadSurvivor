# 028 대표 구역 미술 — 구현·검증 기록

2026-11-01. 현재 상태: 대표 구역 Editor 구현·최종 비교·기존 게임 회귀·독립 검토 PASS. 최종 local/gate 결과는 아래 체크포인트를 따른다. 전체 프로젝트 완료나 G2/G3/G4/G5 인수 기록이 아니다.

## 요구와 결과 범위

사용자는 N2 실제 빌드·테스트를 직접 수행하므로 실행하지 않고 다음 단계로 진행하도록 지시했다. N3 대표 구역의 별도 씬·자체 메시/재질·Editor 동작 검증을 수행한다. 이전 N1 미완료는 유지한다. 성능 수치나 실행 파일 PASS는 만들지 않는다.

현재 후보는 `Assets/DeadSurvivor/Scenes/Map028_ArtExperimental_r004/Map028_Art.unity`, 비교 기준은 같은 폴더의 `Map028_Basic.unity`다. 저장 씬은 일반 Play에서 로비→4명 선택→출전으로 실행한다. 이동은 WASD/방향키다. Build Settings·기존 게임 기본 씬은 변경하지 않았다.

설계: [028](../design/map-art-028.md). 구현 전 설계 검토와 구현 후 코드/기하 검토를 독립 에이전트가 읽기 전용으로 수행했다. 메인이 코드·Unity 작업과 지적 수정을 담당했다.

## 보존과 자산

- 007/018 Layout, 원래 018/021~023 씬·프리팹·기존 TEST 바위 원본을 보존한다. StaticGeometry/RockHolder 바인딩 root는 유지하고 대표 영역의 Renderer만 교체한다.
- 영역은 원본 픽셀 x140..420/y105..340, OB-001~005다. RT-001/003은 인접한 참조 경로이며 이번 검사의 실제 RT 통과 사례로 승격하지 않는다.
- C는 기존 64개 후보를 그대로 사용한다. 새 Collider는 없다. 돌 덩어리는 원본 다각형 안쪽에 배치하고 지면 장식에는 안전 여유를 적용했다.
- 새 표현은 자체 생성 메시·재질이다. 외부 생성·유료 요청·패키지 설치·게시·커밋은 수행하지 않았다. 미술 씬에서는 TEST 바위 Renderer만 끈다. 외부 자산 최종 채택/A5로 승격하지 않는다.
- 팔레트·높이·형상은 시험 제안이며 최종 사용자 미술 승인으로 취급하지 않는다. 기존 캐릭터·UI·카메라 s4/55도/ortho6을 유지한다.
- 기본/미술 씬을 재개방하여 Layout SHA·참조·시작점·조명 직렬화를 확인했다. 기존 의존 파일 SHA와 새 생성 파일의 최종 저장 후 SHA를 대조했다.

r004 생성 근거: `output/map-art-028/build-9d058e13d9bd468a85ffc8628dc52a3a/created-files.json`. 생성은 씬/자산 authoring이며 실행 파일 빌드가 아니다.

## 초안과 수정 이력

| 버전 | 시각 확인과 처리 |
|---|---|
| r001 | 긴 삼각 부채꼴과 원형 얼룩이 두드러져 미채택. 최초 journal의 재질 7개 해시는 최종 SaveAssets 이전 값이므로 인수 근거에서 제외한다. 과거 파일을 덮어쓰지 않았다. |
| r002 | 작은 셀의 돌이 줄무늬 기둥처럼 보여 미채택. 최종 저장 후 해시 갱신·검사를 적용했다. |
| r003 | 둥근 모서리와 낮은 높이를 적용했으나 반복 기둥 느낌이 남았다. 기존 결과 보존. |
| r004 | 셀을 넓히고 낮은 풀·불규칙한 마른 지면·원본 C 안의 접지 면을 추가했다. 현재 대표 구역 후보이며 전체 맵 최종 미술이 아니다. |

초기 동결 비교 r001의 기계 검사는 PASS였으나, 직접 화면 확인에서 초기 리셋 직후 HUD 글자가 비어 있었다. Editor 검증 코드에서 UI.Tick/Canvas/TMP 갱신을 추가했다. 이 결과는 최종 화면 근거로 사용하지 않는다. 미실행 r002 프로필은 개정 식별 코드 변경 전 자료로 보존한다. 수정된 최종 비교는 후속 개정으로 새로 실행한다.

## 입력 원본과 시각 비교

`docs/art/maps/references/VerdantMeadowsFullMap.png`(1195×1110)를 직접 확인했다. [03 원본 분석](../design/3D배경-VerdantMeadows-구조분석-03.md), [시각 절차](../workflows/reference-design.md)를 따른다. 원본은 구조 참조 이미지이며 원작의 높이·충돌·실제 카메라는 확정하지 않았다. 원본과 55도 화면의 픽셀 일치를 주장하지 않는다. 기본 도형/미술끼리는 같은 위치·카메라·해상도·파티/장비·seed·초기 타이머로 비교한다.

| 비교 항목 | 관찰 |
|---|---|
| 외곽/충돌 | 전체 C·경계 데이터 불변. 이 화면은 전맵 외곽 인수가 아니다. |
| 지역 밀도/형태 | 곡선 두 군집·둥근 군집·열린 공간을 유지하고 돌의 작은 덩어리와 면 명도를 추가했다. 원본 식생/텍스처의 상세 복원은 아니다. |
| 연결/빈 공간 | 새 장식은 비충돌 표현이며 지면 장식의 충돌 윤곽 간격을 검사했다. RT 전체 통과·전체 자유 지면 연결은 미실행이다. |
| 표시/가독성 | 일반 게임 각도에서 파티·체력 바·적·공격 표현을 확인한다. 원본의 모든 물체가 한 캡처에 담겼다고 주장하지 않는다. |
| 조명/접지 | 새 씬의 동일한 방향광/ambient, 원본 C 안의 접지 면을 적용했다. 접지 면을 실제 물리 그림자/성능 증거로 부르지 않는다. |

`craft-game-floors`의 art-direction/scene-lighting/placeholder-assets/visual-scorecard와 Unity 6000.4 [Light.shadows 문서](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Light-shadows.html)를 참고했다. 기존 카메라·UI 표현을 유지하기 위해 공용 post-processing/URP 설정은 변경하지 않았다. 최종 화면의 환경 팔레트·스타일·깊이만 평가하며 오디오/메뉴/게임 전체 평점을 만들지 않는다.

## 기하·도구 검증

- 독립 시각 메시 검사 PASS: `output/map-art-028/mesh-export-a2068734c86448668849444b826dc924.audit.json`.
- 저장된 돌 5개, 3,660개 삼각형·투영 선분 6,100개를 검사했다. Native float32 bit와 JSON 복원의 일치, 모든 정점, 오목 경계를 가로지를 수 있는 선분의 정확한 교차/구간, 3D 비퇴화 삼각형을 대조했다.
- native exporter는 명시적 신뢰 의존이며 코드 SHA를 고정했다. 실제 렌더러의 정확한 메시 집합/자산 경로/단위 world transform도 별도로 검사한다. 이 결과는 시각 메시의 C 내부 포함이며 G2/G3가 아니다.
- Python 맵 시험 207개 PASS. 최초 실행에서는 과거 topology r001의 소스 지문 변경이 탐지되어 1개 ERROR였고, 이를 숨기지 않았다.
- 연결성 프로필 r002를 Unity에서 읽기 전용으로 새 수집하고 전체 runtime payload가 r001과 같음을 확인했다. 바뀐 의존은 owned 조명 처리의 Editor 코드 한 파일이다. 과거 프로필/결과는 보존하고 strict source/numeric/catalog 거부 검사를 유지했다. gate baseline은 변경하지 않았다.

## 실제 Editor 비교의 인수 경계

비교 프로필에는 초기 파티·장비·위치/생사·비직렬화 타이머·난수 trace·카메라, 생성 seed, 사례 label과 소스/자산 지문을 실행 전에 동결한다. 정지 캡처는 elapsed0·적/투사체0이다. 첫 render는 warmup으로 버리고 HUD가 포함된 화면을 사용한다.

실제 Dynamic 입력 D와 해제 후 native Update 관찰, 시간/프레임 진행·정지, 일반 전투 최소5초와 적·공격/효과/처치 관측을 검사한다. 이는 10웨이브 전체 런이나 최대 부하 시험이 아니다. 독립 이동 검사는 관측된 전후 점을 잇는 선분이며 모든 실제 프레임의 경로 증명이 아니다.

가림 발생/재질 복원은 synthetic focus fixture다. 실제 캐릭터 가독성은 게임 화면에서 별도 확인한다. 자연 가림 경로 전체를 실행했다고 주장하지 않는다. 조명 설정 변조를 의도적으로 주입하여 소유권 guard가 거부함을 확인한 뒤 복원한다.

부모 실행기는 자식 검증기를 강제 종료하지 않는다. 자식의 finally가 소유 설정/장치를 정리하며, 실패/미복원 pending이 있으면 다음 단계를 막고 BLOCKED로 보존한다. 원래 Map023 씬과 선택·백그라운드 설정을 복원한다. 외부 입력 설정 journal은 현재 bytes·SHA·RESTORED·장치 복원을 다시 확인한다.

## 남은 범위

N2 전체 런·부하·실행 파일은 사용자 직접 실행 대상으로 NOT_RUN이다. N3 최종 스타일 채택과 실제 미술 포함 player-build 성능, 전체 맵 G5는 미완료다. N1 full-map/G2/G3도 별도 미완료이며 이번 대표 구역 결과로 완료 처리하지 않는다. 이 기록의 local/all/gate 결과는 아래 최종 실행 뒤 추가한다.


## 최종 실행 결과와 화면

- 동결 프로필: [028/r004](../design/map-art-028-profile.r004.json). 208개 입력 파일·78개 코드/검사기 지문을 고정했다.
- 실제 비교 PASS: `output/map-art-028/accept-f6abe17a888d4889a7b656b2573ed415/summary.json`. 기본11/미술13 검사, 동일 초기 상태/카메라, 독립 관측 선분10개, 실제 이동·정지·5초 이상 일반 전투와 정리 PASS. 현재 코드·파일·보존된 원자료와 입력 설정 journal을 독립 검토자가 재대조했다.
- [최종 기본 화면](../../output/map-art-028/accept-f6abe17a888d4889a7b656b2573ed415/stage-28/fixed-start-draft.png) · [최종 미술 화면](../../output/map-art-028/accept-f6abe17a888d4889a7b656b2573ed415/stage-29/fixed-start-draft.png) · [전투 화면](../../output/map-art-028/accept-f6abe17a888d4889a7b656b2573ed415/stage-29/game.png). 파일명 draft는 수집기 이름이며 최종 보존본은 이 실행의 SHA를 따른다. 초기 HUD의 WAVE/시간/4명 정보와 전투 화면의 적·공격을 직접 확인했다.
- 현재 `all --screens` PASS: `output/verification/20261101-185809-d1c723c8/summary.json`. 컴파일·695개 규칙·실제 프레임/입력·초기 로드·3종 화면 크기33개 검사·화면 캡처·정리 PASS. 실제 기기/실행 파일 성능이 아니다.
- 원래 Map023 씬/선택/백그라운드 복원 PASS: `output/map-art-028/regression-273305bdaca14794aa158c08bccd72be/restored.json`.
- 환경 프리팹 팔레트 검사 PASS: `output/map-art-028/palette-e896a827b1f445caa5d888c640e6b5ba/summary.json`. 기본 Material/오류 Shader 없음. 활성 Renderer376·메시 삼각형 합9,306은 저작 단계의 진단 개수이며 draw call/CPU/GPU/실제 프레임 성능이 아니다. 런타임 UI 전체의 기본 글꼴 검사를 대체하지 않는다.
- 첫 `all --screens` FAIL은 보존: `output/verification/20261101-185247-4338a1c2/summary.json`. 컴파일/규칙/입력은 PASS였으나 Simulator의 잔류 Device.Screen shim 때문에 화면 크기가 불일치했다. 소유 진단 `output/map-blockout-018/screen-diagnostic-8e0b7bca92634d48b69a4aae16f83ee6/summary.json`에서 명시적 Disable 전후와 복원 PASS를 확인했다. `DSScreenVerification`에서 기존 Restore와 동일한 API로 일시 중지하도록 고쳤다. 런타임 DSUI나 크기 assertion·gate 기준은 완화하지 않았다. 현재 실행에서 전체 화면 검사가 PASS했다.

환경 시안의 주관적 비교 점수(1~5, 사용자 최종 미술 승인 아님):

- palette discipline: 3/5 — 이름/색이 정해진 지면·돌·풀 재질, 환경 기본 Material/오류 Shader 없음.
- style uniformity: 3/5 — 긴 부채꼴/기둥 초안2/5에서 넓은 모서리 절삭 돌로 수정. 픽셀 캐릭터와 단순 3D 배경의 기존 혼합 방향을 유지했다.
- lighting depth: 3/5 — 따뜻한 광원·다른 면 밝기·접지 면. 원본의 풍부한 질감과 실제 전역 그림자 품질까지 완성한 것은 아니다.
- silhouette: 3/5 — 기존 캐릭터 표현을 유지하며 게임 각도에서 4인과 체력 바를 구별할 수 있다.
- UI intentionality: 3/5 — 빈 HUD 초안을 수정하고 기존 폰트·배치·녹색 팔레트를 확인했다. UI 재설계는 하지 않았다.
- feedback visibility: 3/5 — 전투 프레임에 적과 밝은 투사체가 보인다. 전체 전투 연출 완성도를 평가하지 않는다.

최종 실행 시각·현재 소스 지문·local/gate 결과·로컬 코드 복구 사본은 [028 체크포인트](../../output/map-art-028/final-checkpoint.json)에 기록한다. 이 문서의 결과는 대표 구역 Editor 시안 범위이며 N1 전체 인수·N2 사용자 빌드·N3 실행 파일 성능·전체 맵 G5를 완료로 바꾸지 않는다.
