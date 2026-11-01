# 036 — 원본 기반 대표 구역의 지면·길·초기 잔해 단면

2026-11-02 · L1 · [설계](../design/reference-ground-036.md) · [제작 전 실패 원인 메모](reference-drift-retrospective-20261102.md).

## 현재 사용할 수 있는 결과

별도 Unity 장면 [Map036_ReferenceGround_r003](../../Assets/DeadSurvivor/Scenes/Map036_ReferenceGround_r003/Map036_ReferenceGround.unity)에서 지면·흙·연속 자갈길과 여섯 낮은 잔해 단면 후보를 볼 수 있다. `Assets/DeadSurvivor/Scenes/Map036_ReferenceGround_r003/Map036_ReferenceGround.unity`를 열고 Play 후 네 캐릭터로 출발하면 WASD로 확인할 수 있다. 전투는 비활성화한 시험 장면이다.

- [실제 게임 — 풀밭에서 자갈길로 이동](../../output/reference-ground-036/20261102-r001/grass-to-road-final-game.png)
- [실제 게임 — 자갈길을 따라 이동](../../output/reference-ground-036/20261102-r001/path-traversal-final-game.png)
- [원본과 같은 범위의 위쪽 렌더](../../output/reference-ground-036/20261102-r001/top-r003.png)
- [비교 원본](../art/maps/references/VerdantMeadowsFullMap.png)

이번 결과는 **지면·길·초기 잔해 단면 후보의 실제 게임 연결**이다. 원본 세부 미술 충실도와 사용자 최종 채택은 미완료다. 신규 Tripo 호출·추가 Tripo 크레딧 사용은 0이다.

## 원본과 제작 입력

원본은 VerdantMeadowsFullMap.png, SHA-256 `84bb8b2502061af81a620b18f158479d963eed7070e1779c97f4dc059b5da5e9`이다. 원본의 x100..520/y100..360 영역을 선택했다. 위쪽 렌더는 1680×1040, 90도, 중심 원본 좌표(310,230), ortho10.4로 이 범위를 표시한다. Play 입력 증거와 구분되는 실제 Unity 메시의 편집 상태 렌더다.

배치와 충돌은 기존 Map018_r001/Layout.json을 그대로 참조한다. OB-001..006의 여섯 윤곽과 RT-003의 길 중심선을 사용했으며, 035 생성 그림에서 좌표를 다시 추출하지 않았다. 기존 윤곽은 관찰 후보 외피이며 원본 실측이나 최종 채택 데이터가 아니다.

원본을 직접 입력한 내장 image_gen으로 [풀밭 재질](../art/maps/reference-ground-036/r001/meadow-albedo.png)과 [흙 재질](../art/maps/reference-ground-036/r001/soil-albedo.png)을 제작했다. [풀밭 프롬프트](../art/maps/reference-ground-036/r001/prompt.txt)·[흙 프롬프트](../art/maps/reference-ground-036/r001/soil-prompt.txt)·[입력/결과 지문](../art/maps/reference-ground-036/r001/generation.json)을 보관했다. 생성 결과는 각 1254×1254이고 Unity import 최대 크기는 1024다. 전체 풍경 이미지를 바닥에 붙인 방식이 아니라, 반복 재질과 실제 메시로 구성했다.

## 적용과 보정

| 구분 | 적용 | 아직 남은 차이 |
|---|---|---|
| 풀밭 | 원본 풀밭 세 지점의 색을 확인해 밝은 황록색을 낮추고 어두운 녹색으로 보정 | 균질한 반복 재질, 원본의 다양한 식생 부족 |
| 흙 | 원본에서 관찰한 큰 흐름과 잔해 주변에 질감·투명 경계 적용 | 크고 분리된 패치와 외곽 띠가 우세. 원본의 잘게 끊긴 얼룩/잔디 혼합은 부족 |
| 길 | 기존 RT-003 위에 크기가 다른 자갈 372개를 비정렬 배치 | 원본보다 돌 사이 베이지 지면 노출이 많음 |
| 잔해 | 기존 윤곽 안의 녹색 상부와 얇은 석재 테두리, 낮은 측면 | 다각형 외곽과 단면이 단순함. 깨진 내부 석재·여러 층·작은 어두운 식생은 후속 |

단면 기준 높이는 .22/.29/.36m다. 녹색 면은 기준 +.003m, 돌림 최고점은 기준 +.07m 이하로 최대 .43m다. 이 수치는 **제작 시험 제안**이며 원본에서 실측한 높이가 아니다. 녹색 내부도 기존 충돌 외피 안에 있으며 통과 가능한 방으로 만들지 않았다.

첫 r001은 단색 흙 조각과 규칙적인 작은 돌 배열이 미달이었다. r002에서는 흙 질감/경계와 돌 bevel을 적용했지만 황록색 지면과 5줄 반복이 남았다. r003에서 색과 비정렬 자갈 배치를 보정했다. [r001 화면](../../output/reference-ground-036/20261102-r001/top-before-review.png)·[r002 화면](../../output/reference-ground-036/20261102-r001/top-r002.png)과 각 격리 장면은 비교 이력으로 보존한다.

## 기능과 독립 검토

실제 정상 Update에 임시 키보드 입력을 넣어 네 캐릭터를 이동시켰다. 풀밭→자갈길 44관찰프레임(24입력/20해제), 길을 따라 50프레임(30입력/20해제), 각각 3.36m/4.20m 이동했다. 해제 입력은 0이고 입력 설정과 기존 키보드를 복구했다. 모든 관찰 구간에서 기존 production SafeSweep은 안전을 반환했다. **독립 충돌 oracle이나 전체 맵 경로 시험은 아니다.** [입력 요약](../../output/reference-ground-036/20261102-r001/movement-summary.json).

r002 예비 입력은 정상 동작을 확인했으나 이동 시간이 길어 대표 구역 밖까지 진행했다. 이를 최종 대표 화면으로 사용하지 않고 r003의 두 짧은 입력/새 캡처로 확인했다. 기존 이동·가림·충돌 코드와 Layout/C는 수정하지 않았으며 시각 장식에 Collider를 추가하지 않았다.

GPT-6.1 Sol high 독립 검토가 코드·계산·원본·r001~r003 화면·최종 실제 게임/입력 기록을 검토했다. 부분 생성 실패 시 소유 파일 기록(036-R1), 흙 fade 가장자리의 winding(036-R2)을 지적해 수정했다. 후보의 실제 연결은 PASS 권고이며 위의 원본 미술 차이를 미완료로 남겼다. [독립 검토 기록](../../output/reference-ground-036/20261102-r001/independent-review.json).

## 마감 근거와 한계

복원된 최종 소스의 [Unity 전체 화면 회귀](../../output/verification/20261102-002755-fe54a2d4/summary.json)는 PASS다. 컴파일·규칙·실제 프레임/입력·다중 해상도 화면·콘솔·복구·소스 안정성을 확인했다. [기존 파일/편집 상태 복구](../../output/reference-ground-036/20261102-r001/restoration.json)는 기존 649파일의 바이트와 원래 Map023 장면·선택·배경 실행 상태 일치를 확인했다. 최종 gate 및 지문은 [마감 기록](../../output/reference-ground-036/20261102-r001/completion.json)·[파일 목록](../../output/reference-ground-036/20261102-r001/manifest.json)을 따른다.

마감 중 폰트 캐시의 자동 변경을 발견했다. 첫 전체 화면 회귀는 PASS였지만 작업 전 폰트 바이트를 복구하면서 시각 입력 지문이 달라져, 이를 최종 근거로 승계하지 않고 복원된 지문에서 한 번 재실행했다. [복구와 재실행 이유](../../output/reference-ground-036/20261102-r001/font-restoration.json)를 보존했다. 전체 경로 조합·새 범용 하네스·빌드/실기기 성능 검사는 추가하지 않았다.

다음 미술 작업은 이 구역에서 잔해의 깨진 단면·내부 분할·작은 식생을 다듬고, 흙과 돌길의 밀도를 원본에 더 가깝게 맞추는 일이다. 현재 후보를 최종 자산 승인이나 전체 맵 완성으로 승격하지 않는다.
