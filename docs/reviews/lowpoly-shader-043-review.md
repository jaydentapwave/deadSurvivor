# 043 r001 검증 기록 — 공용 로우폴리 셰이더와 팔레트

- 날짜·범위: 2026-11-02 03:00~ KST. [설계](../design/lowpoly-shader-043.md)의 0~3단계. 4단계(파이프라인 통합)는 범위 밖. Claude 메인이 구현했고, Unity 에디터는 042 세션과 메시지로 순서를 나눠 썼다(042: 03:10~03:20·03:18, 043: 03:19~03:33).
- 결과 폴더(Git 제외): `output/lowpoly-shader-043/r001/`. **최종 증거 묶음은 `bundle-final/`**(현재 소스 19개·현재 캡처와 호출 기록 58개·비교 시트 4장·`metrics.json`의 지문 82개 `evidence.sha256`). 빌드 기록 `build.json`, 현재 캡처 `captures/`. 이전 시트·지문 목록·캡처는 `history/`와 `captures/attempt*/`에 이력으로 보존한다(043-V8).
- 사용할 수 있게 된 것: [Map043_LowPolyShader](../../Assets/DeadSurvivor/Scenes/Map043_LowPolyShader_r001/Map043_LowPolyShader.unity) — 038 대표 구역과 같은 배치를 공용 셰이더 1개·팔레트 1장(재질 프리셋 5개)으로 다시 칠한 **시험 후보** 장면. 정식 채택은 사용자 판단.

## 변경 파일

| 경로 | 내용 |
|---|---|
| `Assets/DeadSurvivor/Art/LowPoly043/Shaders/DSLowPolyPalette.shader` | 셰이더 `DeadSurvivor/LowPolyPalette`. 패스 4개(UniversalForward·ShadowCaster·DepthOnly·DepthNormals) |
| `Assets/DeadSurvivor/Art/LowPoly043/Palette/palette-r001.png`·`.json` | 8×4 팔레트(64×32, Point, mip 없음)와 칸별 출처 |
| `Assets/DeadSurvivor/Art/LowPoly043/Materials/` | 프리셋 Ground·Soil·Path·Stone·Foliage |
| `Assets/DeadSurvivor/Art/LowPoly043/Meshes/Meshes043.asset` | 037·018 생성 메시 117개의 팔레트 UV 사본(원본 불변) |
| `Assets/DeadSurvivor/Art/LowPoly043/Props/{stone,shrub}/` | 재칠 FBX·프리팹·재칠 기록 |
| `Assets/DeadSurvivor/Scenes/Map043_LowPolyShader_r001/` | 038 장면 사본 |
| `Tools/Art/palette_extract_043.py`, `repaint_tripo_043.py`, `build_lowpoly_043.cs`, `probe_lowpoly_043.cs`, `probe_mesh_043.cs` | 추출·재칠·구성/캡처·조사 |
| `docs/design/lowpoly-shader-043.md`, 이 문서 | 설계·기록 |

## 단계별 결과

| 단계 | 결과 | 근거 |
|---|---|---|
| 0 팔레트 | 32칸 모두 채움, 칸 내부 단색 검사 PASS. 행: 풀·흙·돌·물/강조 | `palette/palette.json`(status `PROPOSED_PENDING_USER_APPROVAL`), `palette/board.png`. 입력 `day.png` SHA `37d0937c…` = 041 r002 기록과 같음 |
| 1 셰이더 v0 + 재칠 | 컴파일 오류 0, 메시지 0, SRP Batcher 호환(서브셰이더 3개 모두 코드 0, URP Lit과 같음). 활성 렌더러 119/119가 043 셰이더 사용. Tripo 20개 교체(크기 차 0.0), 생성 메시 117개 변환, 제외 1(꺼진 시험 바위) | `build.json`, `captures/call-SrpCheck.json` |
| 1 색 보정 | 해가 비추는 지면 렌더 색 `#4e9a2e`, 목표 칸3 `#4f9c32` | angle 캡처 두 구역 중앙값 |
| 2 형태 표현 | 높이 그라디언트·상면 이끼 적용. 화면 거리에서는 효과가 작다 | `compare-angle-stages.png` |
| 3 전역 효과 | 구름 그림자·지면 노이즈·그림자 색(낮), 색 보정·그림자 색(밤). 전역 값을 끄면 밤이 진한 초록으로 남는 것을 비교로 확인 | `compare-night.png` |
| 3 바람 | **r001 기록 정정(043-V3):** 처음 적은 "4,779px, 관목 영역에 한정"은 재현되지 않았다. 두 캡처가 서로 다른 Unity 호출이라 시간이 달랐고 움직이는 구름 그림자가 켜져 있었다(Codex 대조: 32,070px가 다름, 빈 지면 포함). 재검증 결과는 아래 "검토 지적 해소" 절 | `captures/attempt6-pre-review/043-wind-*` |

## 불변식

| 검사 | 상태 | 근거 |
|---|---|---|
| 배치·충돌·레이아웃 | PASS | 렌더러 위치·회전·크기·활성 상태 불변, Collider 0→0, 레이아웃 SHA `bdce2bf8…` 불변, 가림 대상 108개 재연결 |
| 037/038/018 원본 | PASS(복구 후) | 311개 파일 SHA가 작업 전 기록과 일치(`sources-before.sha256` = `sources-after-restore.sha256`). 아래 사고 참고 |
| 캡처 조건 | PASS | `038-base-overview.png` SHA `b42a0643…`가 041 입력으로 쓴 038 캡처와 바이트 일치 → 같은 카메라 조건 |
| 전역 설정 오염 | 확인 필요 | 아래 마감 검사의 `git status`에서 `PrototypeFont.asset` 등 무관 파일 변경 여부를 본다 |

## 실패와 재시도 (기록 보존)

1. **빌드 r001 실패(BLOCKER_LOST):** 옛 Tripo 인스턴스를 지운 뒤 가림 목록을 바꾸려 해서 참조를 잃었다. 저장 전 안전장치가 막았다. 이 실행이 만든 파일 37개는 지문을 기록하고(`cleanup-032422.json`) 삭제한 뒤, 순서를 고쳐 r002로 성공했다.
2. **037 식생 재질 3개 부작용:** r001 실행 중(03:23:53) Unity가 `FoliageDeep/Mid/Tips.mat`의 `m_DoubleSidedGI`를 0→1로 다시 저장했다(038 때와 같은 현상). `git checkout`으로 HEAD 바이트를 되돌리고 강제 재임포트 후 dirty 아님을 확인했다.
3. **흙 패치 쪼개짐:** 잘라내기 기준 0.5 → 0.15(`captures/attempt1-cutoff050/`).
4. **구름 그림자 약함:** `attempt2-weak-cloud/`.
5. **셰이더 재임포트 누락:** 에디터 밖에서 고친 셰이더가 다시 임포트되지 않은 채 캡처됐다. 강제 임포트로 해결(`attempt3-stale-shader-import/`).
6. **높이 기준 버그:** 물체 공간 Y가 Tripo FBX에서 높이가 아니라 관목·석재가 어두워지고 바람이 0이었다. 피벗 기준 월드 높이로 수정(`attempt4-objectspace-height-bug/`).
7. 밤 색 보정 추가 전 캡처: `attempt5-pre-grade/`. 이 시점의 셰이더 SHA는 `2d979043…`였고, 이후 검토 수정으로 두 번 더 바뀌었다(아래). 최종 셰이더 SHA는 `88fc428b…`이며 `bundle-final/evidence.sha256`이 원본이다.

## 키아트 대비 남은 차이 (냉정한 평가)

- 색 계열과 밝기는 키아트에 가까워졌다. 038의 어둡고 탁한 화면에서 확실히 벗어났다.
- **내용물 부족이 큰 차이 중 하나다.** 키아트의 풀 질감·꽃·덤불·나무 밀도가 없다. 지면 노이즈와 구름 그림자가 평평함을 덜어주지만, 키아트의 붓질 같은 풀 표현에는 못 미친다. 원인 순위는 측정하지 않았다(043-V6).
- 접촉 음영 차이도 남은 요인이다. r001 셰이더는 켜져 있는 SSAO를 반영하지 않았고, 비교 캡처는 카메라가 그림자 거리(50) 밖(150)이라 그림자가 없었다(043-V4). 둘 다 아래에서 수정·재촬영했다.
- 흙 패치가 납작한 오려낸 조각처럼 보인다. 원본 037 흙 메시의 모양과 이번 꼭짓점 알파 잘라내기(가장자리를 또렷하게 만듦)가 함께 만든 결과다.
- 2단계 효과(그라디언트·상면 이끼)는 게임 카메라 거리에서 거의 보이지 않는다.
- 밤 화면은 키아트 밤보다 파랗고 청록 기운이 적다. 값 조정은 사용자 판단 대상이다.
- 발광(팔레트 알파)은 구현했지만 발광 칸이 없어 화면 검증은 NOT_RUN이다.

## 검사 상태

| 검사 | 상태 | 비고 |
|---|---|---|
| 셰이더 컴파일·SRP Batcher | PASS | 위 단계 1 |
| 장면 구성 불변식 | PASS | 위 불변식 |
| 같은 조건 비교 캡처(낮·밤) | PASS(캡처) | 시각 채택은 사용자 |
| 독립 검토(Codex, validator-client) | r002 FAIL → r003 FAIL(V7·V8) → r004 동작 계약 PASS, 스펙 FAIL(V9) → V9 수정 | 아래 "독립 검토"·"재검토" 절. 회귀·gate 뒤 같은 검토자가 최종 증거 확인 |
| 전체 화면 회귀 `run.py all --screens` | PASS | 042/044 세션이 합의대로 두 작업이 끝난 뒤 1회 실행(04:10:46~04:12:31). `output/verification/20261102-041046-e9dbfcbd/summary.json`: docs·data·map_oracle_foundation·unity·source_stability PASS. 043 최종 Assets와 044의 `DSUI.cs` 변경을 함께 포함. 실행은 다른 세션이 했고, 043은 summary와 아래 gate 재사용 판정으로 확인했다 |
| `gate.py check` | PASS | 04:13:05 `output/verification/gate-20261102-041305-ed69994b/gate.json`. 실행 0·재사용 5(docs·data·map_oracle_foundation·map_preparation·runtime_evidence). runtime_evidence는 위 회귀 summary를 현재 지문으로 재사용 |
| 2D 스프라이트 밤 가독성(Play) | NOT_RUN | 범위 밖, 게임 연결 때 |
| 사용자 채택(팔레트·셰이더 방향) | 대기 | |

## 독립 검토 (Codex, validator-client 계약)

- r001(03:35): 실행 실패. `-i`가 여러 값을 받아 프롬프트까지 이미지 인자로 소비했다. 검토 아님(`codex-review-r001-argfail.log`).
- r002(03:35:56~03:39:22, `codex exec -s read-only`, 이미지 4장 첨부): **스펙 준수 FAIL, 동작 계약 FAIL**. 원문 `codex-review-r002.md`.
  - 정상으로 확인된 것: URP 17.4 API(Built-in 혼용 없음), 4개 패스의 같은 정점 변형·잘라내기, 화면 공간 그림자 경로, 클러스터 루프, CBUFFER, 팔레트 행 방향(추출기→Blender→Unity→셰이더), `DSHeight01`, 전역 값 0에서 꺼짐, 원본 311개·증거 31개 지문, 가림 목록 108개, 정리 37개가 소유 경로 안.

| ID | 분류 | 지적 | 조치 |
|---|---|---|---|
| 043-V1 | 필수 | 그림자 좌표를 항상 정점에서 계산. 4캐스케이드에서 경계를 가로지르는 큰 삼각형(지면은 2개)의 좌표가 틀림 | URP `SimpleLitForwardPass`와 같이 `REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR` / `MAIN_LIGHT_CALCULATE_SHADOWS` 분기 |
| 043-V2 | 필수 | 바람 비교 첫 실행에 복구가 없어 실패 시 재질·전역 값이 남음. 선택 복구 없음 | 출력 경로를 수정 전에 검사, `finally`에서 재질·전역 값·장면·선택 복구. 실패 경로를 의도적으로 실행해 확인 |
| 043-V3 | 필수 | 바람 수치가 재현되지 않음(위 정정) | 한 번의 호출 안에서 두 렌더, 시간 의존 전역 값 끔, 관목 화면 영역 기록 후 영역 밖 변화 0 확인 |
| 043-V4 | 필수 | 캡처 거리 150 > 그림자 거리 50이라 그림자 미검증, Tripo 뒷면 렌더 없음, 회귀·gate 미실행 | 거리 25 그림자 확인 캡처(038·043), Blender 앞·뒷면 렌더(`tripo-front-back-038-vs-043.png`), 회귀·gate는 042/044와 합의한 시점에 1회 |
| 043-V5 | 후속 | `_ADDITIONAL_LIGHTS_VERTEX` 미구현, `_LIGHT_LAYERS` 검사 없음 | 조명 레이어 검사 추가(주광·추가광). 정점 추가광 키워드도 픽셀 단위로 처리하고 설계에 지원 범위 명시. 단일 방향광 장면이라 해당 경로의 화면 검증은 NOT_RUN |
| 043-V6 | 후속 | "가장 큰 차이는 내용물" 단정, SSAO 미반영 | 표현 완화. SSAO(`_SCREEN_SPACE_OCCLUSION`)를 URP와 같은 방식으로 반영 |

## 검토 지적 해소 (03:40~ 수정, 03:42~03:43 Unity 재실행)

수정한 셰이더를 강제 재임포트(오류 0, 메시지 0)한 뒤, 이전 캡처를 `captures/attempt6-pre-review/`로 옮기고 **모든 단계를 최종 셰이더로 다시 찍었다**. SRP Batcher 호환(코드 0)을 다시 확인했다.

| ID | 결과 | 근거 |
|---|---|---|
| 043-V1 | 코드 수정 완료. 그림자 확인 화면은 아래 V4 | 셰이더 `DSFrag`의 그림자 좌표 분기(원본 `SimpleLitForwardPass.hlsl` 82~88·185줄과 같은 조건) |
| 043-V2 | PASS | 출력 위치에 같은 이름의 폴더를 만들어 **설정 변경 후** 쓰기를 실패시켰다(`UnauthorizedAccessException`). 직후 `StateCheck`: 관목 바람 0.02(저장값), dirty 아님, 전역 값 전부 0, 활성 장면은 받은 그대로 Map038. `captures/call-WindProof-failtest.json`, `call-StateCheck-afterfail.json` |
| 043-V3 | PASS | 한 번의 호출 안에서 바람 0·0.2 두 장(`043-windproof-w000/w020.png`), 시간 의존 전역 값 전부 0. 차이 임계 0 기준 7,154px, 그중 관목 화면 영역 안 7,154px, **밖 0px**(임계 12: 4,719px, 밖 0). 영역은 관목 렌더러 경계(+0.25)를 같은 카메라로 투영한 것이며 겹침 그림 `043-windproof-overlay.png`로 정렬을 확인했다. 에디터 실시간은 두 렌더 사이 0.47→0.61초로 흘렀지만 시간 의존 효과가 꺼져 있어 결과에 영향이 없다. 화면 밖 관목 1개(OB-006)는 영역이 이미지 밖이다 |
| 043-V4 | 화면 증거 완료, 회귀·gate 미실행 | 거리 25(그림자 거리 안) 캡처 `038-shadow-d25-*`, `043-shadow-d25-*`. 같은 호출·같은 카메라에서 태양 그림자 켬·끔(03:47:27~33, `ShadowPair038/043`): 켬일 때 어두워진 픽셀(임계 12) 038 39,620 / 043 41,963, 밝아진 픽셀 0 / 64. 차이 마스크 `captures/*-shadowpair-mask.png`와 `compare-shadowpair.png`에서 두 장면의 그림자 모양이 거의 같고 캐스케이드 경계 끊김은 보이지 않았다(눈 판정). Tripo 앞·뒷면 렌더 `tripo-front-back-038-vs-043.png`(Blender, 팔레트 텍스처). 그림자 설정은 `PC_RPAsset` 기준 거리 50·캐스케이드 4이며, 스크립트의 `QualitySettings` 값(40·2)은 URP가 쓰지 않는 기본 품질 값으로 판단해 근거로 쓰지 않았다 |
| 043-V5 | 코드 수정 완료 | 주광·추가광에 `IsMatchingLightLayer`. `_ADDITIONAL_LIGHTS_VERTEX`도 픽셀 단위로 처리. 이 장면에는 추가광·조명 레이어가 없어 화면 검증은 NOT_RUN |
| 043-V6 | 수정 완료 | 표현 완화(위 절). SSAO를 `GetScreenSpaceAmbientOcclusion`으로 직접광·환경광에 반영 |

- 알려진 한계: 038과 같은 캡처 조건을 유지하려고 `Camera.CopyFrom(ViewCamera)`를 그대로 쓴다. 이 때문에 카메라 거리만 바꿔도 화면이 약간 밀린다(원인 미조사). 거리가 다른 캡처끼리는 픽셀 비교를 하지 않고, 같은 거리 안의 켬·끔 쌍으로만 수치 비교한다.
- 이번 재실행에서 `git status`에 `Foliage*.mat`·`PrototypeFont.asset` 변경은 없었다. `ProjectSettings/EditorBuildSettings.asset` 변경은 044 세션의 작업이다.

## 재검토 r003과 최종 해소

- r003(03:48:03~03:50:56, 같은 계약·같은 검토자 역할): V1·V2·V3·V5·V6 **해소**, V4는 회귀·gate만 남음. 수치(바람 7,154/밖 0, 그림자 쌍 39,620·42,027)를 원자료로 재현했다. 임계 "12"는 `Σ|RGB 차이| > 12`다. 038의 60,013px는 거리 150↔25 전체 변화량이라 그림자 수치로 쓰지 않는다. 새 필수 지적 2개. 원문 `codex-review-r003.md`.

| ID | 분류 | 지적 | 조치와 결과 |
|---|---|---|---|
| 043-V7 | 필수 | 추가광 루프가 SSAO 직접광 감쇠 없는 `GetAdditionalLight`를 씀 | URP `RealtimeLights.hlsl` 257~265줄과 같은 AO 오버로드 `GetAdditionalLight(i, inputData, shadowMask, ao)`로 교체(두 루프). 점광원 켬·끔 확인(밤, 같은 호출): 밝아진 픽셀 138,493(임계 12), `bundle-final/compare-pointlight.png`. 활성 렌더러 `PC_Renderer`는 Forward+(`m_RenderingMode: 2`)라 **클러스터 경로만** 화면 확인했고 비클러스터 경로는 NOT_RUN. 추가광 접촉부 AO 감쇠는 정적 대조로만 확인 |
| 043-V8 | 필수 | 재촬영 뒤 비교 시트·지문 목록을 갱신하지 않음 | V7 수정 뒤 셰이더를 재임포트하고 **모든 캡처를 최종 셰이더로 다시 찍었다**(03:53:27~03:54:00, 14회, `captures/attempt7-pre-V7/`에 이전 보존). `Tools/Art/evidence_043.py`가 현재 캡처와 현재 소스만으로 시트·수치·지문 목록을 만든다 |

- 재촬영 중 발견한 도구 결함: 바람 확인의 관목 화면 영역이 Game 뷰 크기를 따라가서(044가 화면비 캡처로 Game 뷰를 바꾼 뒤) 가로로 약 0.69배 찌그러졌다. 렌더는 같았고 영역 계산만 틀렸다. 뷰포트 좌표로 계산하도록 고치고 바람 확인만 다시 찍었다(`attempt8-rect-gameview-bug/`, 잘못된 묶음은 `history/bundle-final-rect-bug/`).

### 최종 수치 (`bundle-final/metrics.json`)

| 확인 | 결과 |
|---|---|
| 바람(같은 호출, 시간 의존 전역 값 0) | 변화 7,091px, 관목 영역 밖 0px. 임계 12: 4,627px, 밖 0px |
| 그림자 켬·끔(거리 25) | 038: 어두워짐 39,620 / 밝아짐 0. 043: 어두워짐 41,933 / 밝아짐 69 |
| 점광원 켬·끔(밤) | 밝아짐 138,493px, 영역 (488,0)~(1305,639) |
| 셰이더 | 재임포트 오류 0·메시지 0, SRP Batcher 코드 0(서브셰이더 3개), 최종 SHA `88fc428b…` |
| 상태 복구 | 매 실행 뒤 `StateCheck`: 관목 바람 0.02, dirty 아님, 전역 값 0, 활성 장면 Map038 |

## 재검토 r004

- r004(03:55:37~03:57:17): **동작 계약 PASS**(수정 코드와 제출 증거 범위), 스펙 준수 FAIL(V9), 전체 마감 BLOCKED(회귀·gate NOT_RUN). V7 코드 해소, V8 지문 83/83·비교 시트 4장 픽셀 일치·`metrics.json` 전 항목 재현. 원문 `codex-review-r004.md`.

| ID | 분류 | 지적 | 조치 |
|---|---|---|---|
| 043-V9 | 필수 | 최종 묶음에 재촬영 전 수동 분석 `captures/shadowpair-diff.json`(043 값 167,660/42,027/41,963/64 = 이전 PNG 기준)이 섞임 | 파일을 `captures/attempt7-pre-V7/shadowpair-diff-0347.json`으로 옮기고 묶음을 다시 만들었다(이전 묶음 `history/bundle-final-v9-stale-json/`). `evidence_043.py`는 이제 캡처 이름 규칙(`038-…`·`043-…`·`call-…`) 밖의 파일이 있으면 묶음을 만들지 않고 멈춘다. 임시 파일을 넣어 멈춤을 확인했다. 최종 지문 82개 |
| 043-V10 | 후속 | 머리말의 소스 개수 22 → 실제 19 | 정정 |

## 최종 증거 확인 r005와 마감

- r005(04:13:35~04:15:15, 같은 검토자 역할): **스펙 준수 PASS(시험 후보 범위), 동작 계약 PASS(제출 증거 범위)**. V9·V10 해소, 지문 82/82 일치, `metrics.json` 전 항목 재현. 회귀 원자료 151개 해시와 `gate_before == gate_after == 현재 실행 context`를 확인했고, 회귀가 현재 043 Assets(셰이더 SHA `88fc428b…`, `Art/LowPoly043/` 37개, 장면 폴더 2개)와 현재 `DSUI.cs`를 포함한 소스로 실행됐다고 판정했다. 원문 `codex-review-r005.md`.
- 043-V11(필수 마감): 04:13:05 gate 뒤 문서가 바뀌었으므로 기록을 끝낸 뒤 gate를 다시 실행한다. 런타임 지문은 같아 Unity 회귀는 다시 하지 않는다.
- 마감 gate 결과는 이 문서를 고치지 않도록 `output/lowpoly-shader-043/r001/completion.json`에 남긴다(038과 같은 방식).

### 결론

- **사용할 수 있게 된 것:** 공용 셰이더 `DeadSurvivor/LowPolyPalette`, 팔레트 r001, 재질 프리셋 5개, 시험 장면 `Map043_LowPolyShader`, 낮·밤·그림자·점광원 비교 증거.
- **시험 후보이며 정식 채택이 아니다.** 사용자 판단 대기: 팔레트 32색, 셰이더 방향, 밤 색 보정 값.
- **NOT_RUN으로 남는 것:** 비클러스터 추가광 경로, 추가광 AO 감쇠의 화면 확인, 발광 화면, 2D 스프라이트의 밤 가독성(Play), 게임 장면 연결, 4단계 자산 파이프라인 통합(L2), 실기기 성능.
