# HD-2D 다리·천막 Tripo 생성 기록 (2026-11-18)

- 범위: `/3d-art-produce` 5~7단계 중 **생성 + 감면 + FBX/GLB 정리 + 크기·바닥 피벗**. 유니티 가져오기·장면 배치·도트 질감은 다음 단계(5단계)가 한다. 생성 질감은 버렸다(모양만 씀).
- 설정: `3dkit.hd2d.config.json`(`KIT3D_CONFIG`로 가리킴, 프로필 lowpoly = `smart_low_poly=true`, `face_limit` 자산 값), 카탈로그 `docs/art/asset-catalog-hd2d.json`. 예전 `3dkit.config.json`·`docs/art/asset-catalog.json`은 고치지 않았다.
- 유료 승인 원문(예산 `hd2d_props`): 2026-11-18 사용자 "다리 천막 관련해서는 나한테 묻질말고 작업진행해줘", "그냥 상한 없앨테니 계속해서 해줘". 사용자 상한 없음 — 카탈로그 `cap: 1000`은 도구 형식(정수 필수)·오작동 방지용이다.
- 기준서: r6.1 승인 기록 OK(디렉터가 다시 기록) 뒤 실행. 근거 K28, 명세 `output/assets/hd2d-bridge/spec.md`, `output/assets/hd2d-tent/spec.md`.

## 결과

| 항목 | 다리 `hd2d_bridge` | 천막 `hd2d_tent` |
|---|---|---|
| Tripo task | `583c3121-15a4-4945-8c67-cc2e92e899d5` | `faadfee1-15a3-4923-9c05-9dab33a6b134` |
| 모델 | v3.1-20260211, seed 20261118, face_limit 600 | 같음, face_limit 500 |
| 크레딧 | 40 (`credits_consumed`) | 40 |
| 잔액 | 2675 → 2595 (두 건 합계 80) | |
| 삼각형 | 생성 1036 → 감면 588 (목표 약 600) | 생성 927 → 거울 복사 656 → 감면 490 (목표 약 500) |
| 크기(m, 폭 X × 길이 Y × 높이) | 1.80 × 4.00 × 0.75 | 1.744 × 2.32 × 1.60 |
| 배율 | 축마다(높이 0.865배 눌림 — K28 "높이 0.75m 이하") | 세 축 같은 배율(높이 1.6m에 맞춤, 모양 비율 유지) |
| 피벗 | 바닥 사각형 가운데, 가장 낮은 점 y=0 | 같음 |
| 앞 방향 | 긴 축 = Unity Z(대칭) | 출입구 = Unity +Z |
| 재질·UV | 없음(질감 버림) | 없음 |
| 결과 파일 | `output/assets/hd2d-bridge/post/model.fbx`, `model.glb`, `post.json`, `preview_*.png` | `output/assets/hd2d-tent/final2/model.fbx`, `model.glb`, `planar.json`, `preview_sheet.png` |
| 다른 계열 판독 | PASS(필수 수정 없음) | FIX → 수정 확인 PASS |

- 축: FBX는 키트 설정(`axis_forward=-Z`, `axis_up=Y`, `FBX_SCALE_ALL`), Blender -Y = Unity +Z. Unity에서 실제 방향·크기는 5단계 가져오기 때 확인(미확인).
- 원본: `output/assets/hd2d-*/tripo/raw/model.glb`, 생성사 미리보기 `raw/preview.png`, 일지 `tripo/journal.json`.

## 후처리

1. `output/assets/hd2d-props-post/post_prop.py`(Blender): GLB 합치기 → 재질·UV·정점색 제거 → 겹친 정점 합치기 → 평면 녹이기(2·4·6도) → 접기 감면(목표 이하) → 삼각형화 → 긴 축을 Y로 → 크기 → 바닥 가운데 피벗 → FBX/GLB + 앞·3/4·위 미리보기.
2. 천막만: 생성 원본 한쪽 옆면에 큰 계단 모양 접힘(원본에서 이미 있음, `final`의 `raw_sheet.png`에서 확인) → `mirror_half.py keep=pos`로 좋은 쪽 반을 거울 복사 → 다시 감면(`final/`).
3. 천막만: 판독 지적(옆면 큰 쐐기형 꺾임) → `planar_tent.py`로 옆면 천 정점을 최소제곱 평면으로(평균 이동 0.03m, 최대 0.085m) → `final2/`.
- 다리는 감면에서 널빤지 홈이 거의 사라졌다(위에서 희미한 줄 2개). 홈은 도트 질감이 맡는다(명세 2절 "디테일은 질감").

## 판독 (Codex gpt-6.1-sol medium, 읽기 전용)

- 마감 `output/assets/hd2d-props-post/review/hd2d-props-tripo-r001.md`: 다리 PASS — 평평한 바닥판·턱보·기둥 6·양 끝 받침 확인, 구멍·떠 있는 조각 없음. 천막 FIX — 넓은 천 면에 큰 쐐기형 꺾임(필수), 선택: 바닥을 더 정사각형에, 장대 끝 각짐 줄이기.
- 수정 확인 `hd2d-props-tripo-fix-r001.md`: 천막 PASS — 꺾임 사라짐, 새로 깨진 것 없음.

## 직접 열어 본 그림

- 생성사 미리보기 `output/assets/hd2d-bridge/tripo/raw/preview.png`, `output/assets/hd2d-tent/tripo/raw/preview.png`
- 다리 `output/assets/hd2d-bridge/post/preview_sheet.png`(앞·3/4·위), `output/assets/hd2d-props-post/review/bridge.png`(컨셉과 나란히)
- 천막 `output/assets/hd2d-tent/post/preview_sheet.png`, `output/assets/hd2d-tent/post/extra_sheet.png`, `output/assets/hd2d-tent/post/raw_sheet.png`(원본 4방향 — 접힘 확인), `output/assets/hd2d-tent/mirror_compare.png`, `output/assets/hd2d-tent/final/preview_sheet.png`, `output/assets/hd2d-tent/final2/preview_sheet.png`(최종 6방향)

## 남은 것(5단계)

- Unity 가져오기(`KIT3D_CONFIG=3dkit.hd2d.config.json assets.py unity/check` 또는 HD-2D 배경 코드 경로), 실제 방향·크기·바닥 접점 확인, 배치·게임 각도 캡처.
- 도트 질감·외곽선 칠(시안 F8 방식). 천막은 천·장대 색 나누기 방법 미정(명세 7절).
- 천막 바닥 1.74×2.32m는 K28의 2.0×2.0(추정)과 다르다 — 모양 비율을 지키려고 같은 배율로 맞췄다. 배치 때 정사각형이 필요하면 X 1.15배(왜곡 15%)로 늘릴 수 있다.
- 다리 높이 0.865배 눌림으로 받침·기둥이 컨셉보다 조금 낮고 뭉툭하다(판독: 형태를 해치지 않음).
- 생성 단계 일지의 `credits_spent`(다리 80)는 예산 누계로 보인다. 요청별 실제 사용은 생성사 `credits_consumed` 40씩, 잔액 차이 합계 80과 일치.

## 시범: 나무·덤불·큰 바위 3D (2026-11-18, 크레딧 0)

- 사용자 원문: "원래 hd2d, 2.5d할떄 기본적으로 캐릭터를 제외하곤 모두 3d로 제작되는거아니야?" → 선택 **"나무까지 전부 3D"**(캐릭터·몬스터·효과 빼고 모두 3D. 나무·덤불은 도트 질감 3D(잎 덩어리)). 유료 생성 원문 "그냥 상한 없앨테니 계속해서 해줘". 이번 시범 상한 **200크레딧**(디렉터가 정함, 예상 3건 약 120).
- 카탈로그 `docs/art/asset-catalog-hd2d.json`: 예산 `hd2d_3d_pilot`(cap 200, 위 원문), 에셋 `hd2d_pilot_tree`(face_limit 600)·`hd2d_pilot_bush`(300)·`hd2d_pilot_boulder`(300), 상태 `trial`.
- **Tripo 생성 안 함 — 관문 막힘**: 생성 도구의 기준서 승인 검사가 `SPEC_CHANGED_AFTER_APPROVAL`(승인 기록 r6 해시 d504525b… ≠ 지금 기준서 62e9cd90…, r6.2·r6.3 개정 뒤 재승인 없음). 3d-art-produce 2절에 따라 에이전트가 넘기지 않았다. 견적·동결·제출·잔액 조회 모두 하지 않음 → **쓴 크레딧 0**, 잔액 미조회(마지막 기록 2595).
- 대신 크레딧 0 경로(Blender 직접 제작 + 도트 질감)로 시범을 끝냈다: `output/assets/hd2d-3d-pilot/notes.md`.

| 항목 | 나무 | 덤불 | 큰 바위 |
|---|---|---|---|
| 컨셉(Codex 내장, 무료) | `tree/concept.png`(A) | `bush/concept.png`(A) | `boulder/concept.png`(A) |
| 삼각형 [측정] | 472(≤600) | 300(≤300) | 70(≤300) |
| 크기 폭×깊이×높이 m [측정] | 1.505×1.098×1.690 | 1.140×0.915×0.745 | 1.141×0.791×0.797 |
| 텍셀 밀도 [측정] | 46.9/m | 46.8/m | 47.6/m |
| 팔레트 | #b3ca7b·#5c8d73·#49746b + 줄기 #774c3b·#5c493c | 잎 3색 | #d8d3a9·#9d9778·#403d2a |
| 판독 style(Codex/Claude) | 3/3 | 3/3 | r1 3/2("로우폴리 면") → r2 4/2 |
| 모델 | `tree/dot/model.fbx` | `bush/dot/model.fbx` | `boulder/dot/model.fbx` |

- 다시 Tripo로 만들려면: 기준서 재승인 기록(`approve.py record`, 사용자 원문) 뒤 `KIT3D_CONFIG=3dkit.hd2d.config.json assets.py gen estimate|freeze|dryrun|submit|fetch hd2d_pilot_<이름>`.
