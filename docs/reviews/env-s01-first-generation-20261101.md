# ENV-S01 첫 돌벽 — 기준 이미지와 Tripo 모델 1개

2026-11-01 · 기존 생성 기준 안의 단일 품질시험 · 생성/로컬 정리 완료. 사용자 요청대로 GPT-6.1 Sol high 서브에이전트가 디자인·크기와 결과 비교를 맡고 메인이 이미지 생성/Tripo 호출·파일 정리를 수행했다. 신규 하네스·워크플로우 코드·게임 코드 변경은 없다.

## 결과

| 항목 | 실제 결과 |
|---|---|
| 첫 품목 | 낮은 직선 돌벽·긴 잔해 ENV-S01/r001 |
| 목표 크기 | 길이X 2.40 × 높이Y 0.50 × 깊이Z 0.60 Unity 단위 기준 |
| 허용 범위 | X 2.20–2.60, Y 0.44–0.56, Z 0.52–0.68 |
| 모델 실측 | 저장 GLB Y-up 좌표 2.4000001 × 0.50 × 0.6000000, 중앙바닥 피벗. Unity에서 측정한 값은 아님 |
| 기하/재질 | 2,893삼각형, 메시1·재질1, UV 있음 |
| 텍스처 | 최종 BaseColor/Normal 각1024². 원본2048² 보존 |
| 재질 정리 | metallic0, roughness0.9. 최종 GLB는 ORM 대신 상수값 사용 |
| Tripo | 생성 요청1건, 실제40크레딧, 잔액1000→960, 예약0 |
| 판정 | 단일 모델 제작·로컬 치수·파일·시각 비교 PASS |
| 미실행 | FBX 재가져오기, Unity 임포트·실측·가림·충돌·성능, 맵 적용·A3~A5 채택 |

크기 근거는 현재 캐릭터 표시높이0.72와 021 배율0.02, 기존 장애물 높이0.32/0.48/0.64다. 이번 돌벽 높이는 캐릭터 표시높이의 약69%다. 실제 인간의 신장·원작 실측값이나 전체 게임 공통 예산으로 해석하지 않는다.

## 이미지·모델

- [기준 이미지](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/reference.png)
- [실제 모델 앞면](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/delivery/preview-front.png) · [뒷면](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/delivery/preview-back.png)
- [크기 정리 GLB](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/delivery/env-s01-wall-sized.glb) · [FBX](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/delivery/env-s01-wall-sized.fbx)
- [Tripo 원본 GLB](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/raw/model.glb)
- [Blender 미리보기 장면](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/delivery/env-s01-preview.blend): 조명·카메라·바닥을 포함하는 확인용 장면. 내보낸 GLB/FBX에는 돌벽만 포함한다.

## 입력·제작 방식·권한

사용자가 첫 돌벽의 이미지·크기를 정하고 Tripo로 모델1개 생성하라고 명시했다. 이 요청을 새 단일 소품 이미지의 생성/Tripo 업로드·단일 유료 생성 허용 근거로 사용했다. 공개 게시·공유 링크·Git 업로드는 하지 않았다.

[기존 028/r004 자체 제작 화면](../../output/map-art-028/accept-f6abe17a888d4889a7b656b2573ed415/stage-29/fixed-start-draft.png)을 실제 이미지 생성 입력으로 전달하되, 돌의 큰 면·무광·회녹 팔레트만 참고하도록 지정했다. 원작 지도나 코드·키·다른 참고 자료는 Tripo에 업로드하지 않았다. Tripo에는 새로 만든 단일 소품 이미지 한 장만 전달했다.

기준 이미지는 native image_gen으로 제작했다. [최종 프롬프트](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/reference-prompt.txt), 원본/입력 해시, 독립 설계 근거는 [요청 계약](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/request-frozen.json)에 보존한다. 이미지는1672×941이며 문자가 없는 단일 소품이다. 4~5큰 돌 제안에서 실제 그림은 약5~6개와 약간 더 따뜻한 색·높이 변화가 나왔다. 독립 검토에서 이번 시험에 허용했다.

## 실제 생성·보존

- Tripo CLI0.8.1, 국제 API, image-to-model, H3.1 v3.1-20260211 / texture v3.5-20260815.
- 표준 geometry/texture, texture·PBR·smart_low_poly·UV·delight 활성, face_limit3000, quad/auto_size/generate_parts/autofix 비활성.
- geometry/texture seed134035, 후보1개. 공급자가 실제 크기를 보장한 것으로 취급하지 않는다.
- [비용 없는 실행 계획](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/dry-run.json)에서 단일 생성40크레딧·경고 없음 확인 후 제출.
- task ID: 17d0b7d1-373b-4e2b-87d9-66f4aaba4f29. 제출 전 journal/잠금을 남기고 수락 ID 저장 후 동일 task를 watch/download했다. 추가 생성·유료 변환·리깅·재텍스처 없음.
- [제출/완료 기록](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/journal.json), [공급자 결과](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/result.json), [비용/잔액](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/balance-after.json).

원본은 덮어쓰지 않았다. Blender5.2.2에서 장축을X로 정렬하고 바닥중앙 원점과 목표 치수로 축별 정규화했다. 추가 리메시 없이2,893삼각형을 유지했다. 텍스처는 원본2K를 보존하고 전달본1K로 축소했으며, 돌 재질에 맞춰 금속도0/거칠기0.9를 적용했다. 저장한 GLB를 다시 가져와 측정·렌더했다. FBX는 별도 유료 API 변환 없이 로컬에서 내보냈으며 재가져오기 검증은 수행하지 않았다.

## 비교·검증과 한계

기준 이미지, 공급자 프리뷰, 저장 GLB의 앞뒤 렌더를 직접 확인했다. 낮고 긴 연속 덩어리와 큰 윗면·측면·끝단은 유지된다. 바닥판·식생·떠 있는 조각·큰 관통구멍은 보이지 않는다. 최종 모델은 기준 이미지보다 모서리가 둥글고 면이 부드러우며 작은 균열이 줄었다. 이번 품질시험에서 허용할 차이이며 사용자 최종 미술 승인은 아니다. 카메라·조명이 달라 색상은 정성 비교다.

GPT-6.1 Sol high 서브에이전트가 저장 GLB의 POSITION/index/UV와 내장 PNG 헤더를 독립적으로 읽어 치수·피벗·삼각형·재질·1K 텍스처를 대조했고 필수 지적 없음으로 보고했다. [독립 검토](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/independent-review.json) · [로컬 실측](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/delivery/metrics.json) · [파일 지문](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/manifest.json).

최종 문서/선택 gate 상태와 경로는 [마감 기록](../../output/asset-pipeline/env-s01/r001/20261101-first-generation/completion.json)에 보존한다. 생성·로컬 확인 범위에 맞는 검사만 수행하며 전체 Python·Unity·전수 topology는 새로 실행하지 않는다. 이후 실제 게임 적용 시 기존 C와 카메라/캐릭터 가림·재질 표현을 확인해야 한다.

## 후속 Unity 확인

[돌벽 Unity 배치·크기·캐릭터 가림·충돌 확인](env-s01-unity-trial-20261101.md)을 수행했다. 위 NOT_RUN은 생성 작업 당시 범위이며 후속 실제 결과는 별도 기록을 따른다.

## 원본 불일치 피드백 후 미술 기준 변경

사용자 피드백으로 [035 원본 직접 대조 기준](reference-reset-035-review.md)을 새 후보로 마련했다. 이 돌벽은 생성/전송/기능 시험 원본으로 보존하며, 신규 잔해의 형태·재질 기준에서는 제외한다.
