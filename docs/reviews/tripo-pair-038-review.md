# 038 — 원본 기준 관목·부서진 석재 Tripo 생성과 Unity 배치

2026-11-02 · L1 · [설계](../design/tripo-pair-038.md) · [037 이전 화면](reference-detail-037-review.md)

## 사용할 수 있는 결과

사용자 요청에 따라 **관목1종과 부서진 석재1종을 Tripo로 실제 생성**했다. GPT-6.1 Sol high 에이전트가 원본 분석·기준 이미지·실제 모델·배치의 독립 검토를 맡고, 메인이 이미지 생성·Tripo 요청·후처리·Unity 구현과 실행을 담당했다.

[Map038_TripoPair_r003](../../Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003/Map038_TripoPair.unity)에 관목4개·석재16개를 배치했다. 037의 원본 선정 영역 x100..520/y100..360, 여섯 잔해와 길을 함께 보는 **대표 구역 분위기 후보**다. 전체 지도 미술 확대·최종 자산 채택·성능 인수는 아니다. 이 장면을 열고 Play 후 네 캐릭터로 출발하면 WASD로 확인할 수 있다. 전투는 비활성 상태다.

- [실제 게임 화면](../../output/tripo-pair-038/20261102-r001/behind-remnant-final-game.png)
- [대표 구역 전체 — 55도](../../output/tripo-pair-038/20261102-r001/overview-framed-r003.png)
- [같은 원본 영역 — 상면](../../output/tripo-pair-038/20261102-r001/top-r003.png)
- [관목 실제 모델 앞면](../../output/tripo-pair-038/20261102-r001/shrub/delivery/preview-front.png) · [뒷면](../../output/tripo-pair-038/20261102-r001/shrub/delivery/preview-back.png)
- [석재 실제 모델 앞면](../../output/tripo-pair-038/20261102-r001/stone/delivery/preview-front.png) · [뒷면](../../output/tripo-pair-038/20261102-r001/stone/delivery/preview-back.png)

상면·55도 비교는 저장된 실제 Unity 메시의 편집 상태 렌더다. 캐릭터가 있는 화면은 정상 Play의 실제 입력 관찰 후 캡처다. Blender 앞뒤 모델 화면에는 비교용 바닥·조명이 있으며 전달 모델에는 포함되지 않는다.

## 생성과 입력

[실제 원본](../art/maps/references/VerdantMeadowsFullMap.png), SHA256 `84bb8b2502061af81a620b18f158479d963eed7070e1779c97f4dc059b5da5e9`를 native imagegen의 입력으로 직접 전달했다. 028·첫 돌벽·037 자체 메시를 새 이미지의 외형 기준으로 사용하지 않았다. 관목은 넓고 짧은 잎과 낮은 암녹색 군집, 석재는 낮고 부서진 회녹색 파편과 부분 이끼를 기준으로 했다. 높이와 보이지 않는 뒷면은 원본 실측이 아닌 제작 제안이다.

관목의 넓은 후광처럼 보인 RGB는 외곽 샘플의 alpha가0임을 확인했다. 038-I1은 실제 불투명 배경으로 판정하지 않았으며 원본 그대로 제출했다. 두 [기준 이미지·프롬프트·요청 기록](../../output/tripo-pair-038/20261102-r001)은 보존했다.

Tripo에는 새 단일 소품 이미지 각1장만 업로드했다. 사용자의 두 모델 제작 지시를 생성·입력 업로드 허용 근거로 삼았다. 지도 원본·코드·키를 Tripo에 전달하지 않았으며 공유 링크·게시·Git 업로드는 하지 않았다.

| 항목 | 관목 | 석재 |
|---|---|---|
| task | b3054c30-9d29-40d3-badd-c994a3365b24 | 33bb8c7e-1e29-4f5d-b5fa-d4fc87ea8fc0 |
| 생성 요청 | 1개 | 1개 |
| 요청 face_limit / 공급자 원본 삼각형 | 2500 / 8300 | 1500 / 2504 |
| Unity 최종 삼각형 | 8300 | 1500 |
| Unity 실측 X×Y×Z | 1.0×.4000001×.90 | 1.4×.320000023×.55 |
| 메시/재질·UV | 각1·UV 있음 | 각1·UV 있음 |
| 최종 텍스처 | BaseColor/Normal 각1K | BaseColor/Normal 각1K |
| 바닥 원점 | 중앙 바닥 | 중앙 바닥 |

설치 CLI0.8.1, H3.1 `v3.1-20260211`, texture `v3.5-20260815`, standard geometry/texture, texture/PBR/smart_low_poly/delight/UV를 사용했다. 시드는138038/138039다. 유효 옵션은 dry-run과 반환 기록을 따르며 provider face_limit이 최종 삼각형 수를 보장하지 않았음을 실제 결과로 기록한다. [공식 옵션](https://developers.tripo3d.ai/en/docs/generation-image-to-model/standard)은2026-11-02 확인했다.

[잔액 전](../../output/tripo-pair-038/20261102-r001/balance-before.json) 960 → [후](../../output/tripo-pair-038/20261102-r001/balance-after.json) 880, 예약0. 실제 합계80크레딧이다. 추가 모델 생성·유료 리메시/변환/재텍스처는 없었다. 제출 전 journal, 수락된 task ID, 동일 task의 다운로드 기록을 보존했다.

## 후처리와 독립 검토

원본 GLB를 보존하고 Blender에서 장축·중앙 바닥·목표 치수를 맞췄다. 원본2K 텍스처의 전달본은1K로 정리하고 무광 금속도0/거칠기.9를 적용했다. 저장 GLB와 FBX를 다시 가져와 실측했고 Unity에서도 저장 프리팹을 다시 열어 실측했다.

관목2500 경량화본은 잎이 삼각 쐐기로 변하고 틈이 커졌다. **038-M1 CHANGE_REQUIRED**에 따라 원본8300 기하를 유지했다. 원래 생성 요청은 불변이며, [별도 전달 계약 r002](../../output/tripo-pair-038/20261102-r001/shrub/delivery-contract.json)에 이번4개배치 후보 한정9000상한과 이유를 남겼다. 독립 검토에서 둥근 잎과 군집 회복 PASS를 확인했다. 정식 성능 기준 완화나 성능 통과 기록은 아니다.

석재는 로컬2504→1500 경량화 뒤 낮고 불규칙한 형상·이끼가 유지되는 것을 앞뒤 비교로 확인했다. 경량화 중 사용되지 않는 꼭짓점 때문에 GLB와 FBX 치수가 달랐던 중간본은 보존하고, 해당 꼭짓점을 정리해 최종 두 형식의 치수를 일치시켰다.

## Unity 배치

기존 관목 네 위치를 새 모델로 교체하고 짧은 파편16개를 잔해 내부·끝에 나누었다. 원본 64군집 전체에 적용한 것이 아니다. 기존 녹색 상부·길·지면·Layout과 게임 코드는 보존했다. 새 모델은 시각 소품이며 Collider를 추가하지 않았다.

처음의 world AABB는 회전한 소품을 과대평가해 좁은 구역에서 중단됐다. 최종은 identity 프리팹의 전체 bounds를 실제 Y회전·균일 크기로 변환한 사각형 **네 변 전체**를 기존 C 안에서 검사한다. 검사 공차나 C를 바꾸지 않았다. r002에서 실제로 좁았던 OB-004 끝의 석재 하나는 r003에서75도/.7배로 배치했다. 나머지 보정·원본 좌표·prefab·크기·회전·C 연결은 [build bindings](../../output/tripo-pair-038/20261102-r001/build.json)에 있다. 실패r001/r002의 소유 자산과 기록은 보존한다.

최종20개 배치는 C 안에 있고 바닥은 상부면보다 약.012 낮아 접지한다. 관목4개·석재16개, 실제 배치된 새 기하 합계57,200삼각형이다. 이는 성능 측정값이 아니다. 프리팹은 Transform/MeshFilter/MeshRenderer만 포함한다.

## 실제 Play 관찰

[입력 요약](../../output/tripo-pair-038/20261102-r001/movement-summary.json)에 정상 Update 관찰을 기록했다.

- 풀밭 접근44프레임/.645m, 길을 따른 이동50프레임/.541m. 첫 사례명 grass-to-road는 재사용 도구 이름이며 이번 기록으로 길 전체 횡단을 주장하지 않는다.
- 짧은 뒤접근44프레임/.443m는 입력 중 계속 이동했다. 이를 경계 정지 성공으로 보고하지 않았다.
- 뒤접근을120입력+20해제=140프레임으로 연장해 .569m 후 마지막5개 입력 프레임의 중심 정지를 확인했다. 실제 시뮬레이션 시간4.818초, 시작 안내문이 사라진 화면을 캡처했다.
- 관찰 구간의 production SafeSweep은 모두안전, 해제입력0, 임시 키보드·입력 설정 복구 PASS. 독립 충돌 oracle·전체 경로 인수는 아니다.
- 기존 잔해 렌더러3개가alpha.22로 전환됐고 네 캐릭터가 보인다. 신규20개 소품은 이 자세에서 캐릭터와 겹치지 않아alpha1을 유지했다. **새 소품의 투명화 발동을 실관찰했다고 주장하지 않는다.**
- [마지막 콘솔](../../output/tripo-pair-038/20261102-r001/console-final.json) 오류0.

## 마감과 남은 차이

Unity 재가져오기가 기존 식생 재질3개의 양면 조명 플래그만0→1로 바꾼 것을 시작 SHA로 특정했다. Unity API로 원복하여 [기존 재질 복구](../../output/tripo-pair-038/20261102-r001/existing-material-restoration.json)의 바이트 일치를 확보했다. 폰트와 기존1182파일을 먼저 복구한 뒤 [최종 all --screens](../../output/verification/20261102-014822-667f2123/summary.json)를 한 차례 실행해 컴파일·규칙·정상 프레임/입력·다중 화면·소스 안정성 PASS를 확인했다. [최종 복구](../../output/tripo-pair-038/20261102-r001/restoration.json)에서1182파일 불변·원래 Map023 장면/선택/배경 실행 복구·임시 키보드0을 확인했다. gate와 독립 최종 증거 검토는 [마감 기록](../../output/tripo-pair-038/20261102-r001/completion.json)을 따른다.

밝고 정돈된 기존 외곽, 석재의 직사각 반복, 관목의 노란 끝은 원본과의 차이로 남는다. 사용자 요청에 맞춰 두 소품이 합쳐진 분위기를 보는 단계이며 전체 미술 완성·전체 지도 확대·새 소품 모든 가림 자세·빌드/실기기 성능·A5최종채택은 NOT_RUN 또는 미완료다.
