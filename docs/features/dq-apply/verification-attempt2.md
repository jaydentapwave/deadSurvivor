# attempt2 구현 검증 기록 (승인 기준 아님)

## 14. 구현 검증 현황 — attempt2 (최종 판정 아님)

- 설계 수정 확인: 사용자 전달 design-reviewer job `20261123-014417-e35458`, `VERDICT: PASS`. 구현·Unity 검증 PASS를 뜻하지 않는다. FR-01의 과거 중지 문구는 위에 이력으로 표시했다.
- 구현 진입점: `MainSceneDQScreenMigration.Apply` / 메뉴 `DeadSurvivor/DQ/Apply Main screen`. 새 렌더러는 `DQ_MainRenderer.asset`. 기존 TiltedView 이행 메뉴는 같은 진입점으로 연결한다. 옛 Background/Effects 이행 메뉴는 폐기 메시지로 중단한다.
- `PerspectiveFrame`은 투영/역투영/입력 Jacobian/그림 스냅의 단일 계산 계약이다. `TiltedCameraRig.Frame`을 hit shake 적용 후 갱신하고 WorldPresenter 뒤 LateTick에서 가림을 갱신한다. 게임의 이동·스폰 논리 범위는 기존 ViewRules 반크기 계산으로 보존하고, 실제 화면 범위는 `FloorViewBounds`로 분리한다. Probe 경계는 실제 서비스 한계/scoped override를 사용한다.
- 기본 경로 첫 유효 실행 `playable-main-r3`: 8/8 PASS. 그 이전 2회는 Unity Search 기본 인덱스 예외로 Play 진입 실패했다. 저장된 원시 실패를 보존했고, Unity 자체 기본 인덱스 생성 API로 로컬 캐시를 복구했다. 뒤따른 렌더링/봇 입력 보완의 최종 실행은 별도 증거를 남긴다.
- 전용 경로 `playable-terrain`: 33/34 PASS, N9_RESULT FAIL. 기대식·baseline·경로 변경0. 원시 방향별 발 `[32,4,4,0]`, 몸 `[32,7,4,0]` (북/동/서/남). 전체 발40/몸43, 발-바닥 오차0. CSV와 JSON은 `output/hd2d-dq/apply/5/terrain-spawn-current*`에 동결했다.
- **새 열린 결함 A-05**: 동·서 노출은 북쪽 예외로 덮을 수 없다. 현재 카메라·필드·스폰 규칙의 조합에서 발생한 실제 FAIL이며 완성 판정으로 승격하지 않는다. `A05-direction-failure.json`에 해당 행과 좌표가 있다. 방향 분류·카운터·알파를 바꿔 관측을 숨기지 않는다. 카메라/게임 규칙을 유지한 추가 해결의 승인 범위와 실제 기하를 마감 리뷰에 넘긴다.
- 북쪽 표시 연출은0.30초 fog-color→원색, 알파/HP/충돌/위치 변화0. 가림 띠는 `north-spawn-mask-plan.json`의6단계 자리 요구이며 실제 숲/능선 성공과 다르다.
- 관련 EditMode 89/89 PASS(중간 이력 포함 원시 JSON 저장). Unity 저장 파일의 YAML 공백은 자동 직렬화이며 코드 공백 검사와 분리해 기록한다. 이 문서는 독립 마감 리뷰 또는 최종 핸드오프를 대신하지 않는다.

- 최종 기본 경로 `playable-main-final/report.json`: 8/8 PASS,145초. `editmode-final.json`: 89/89 PASS. `recompile-final-status.json`: completed/errors0. 두 번 이행의 최종 대상 hash 동일은 `migration-idempotence-indicator-final.json`에 있다.
- 직접 판독한 캡처 경로/sha256/관찰은 `opened-main-images.json`; 시작·이동·실제 이동 한계9.842309m 도달 후 경계×3화면비, 이동8프레임. 실제 camera/alpha bbox는 각 `*-before.json`; 추가 `metadata-final-*`는 view/projection matrix·near/far·pixelRect도 포함한다. sourceHeight48에서94~96px,texel1.96~2.00px@1080이며 새56칸 그림 완료를 뜻하지 않는다.
- `appearance-observed.json`은 게임 상태 변경 없는 실제 Editor update 관측131개, 경과0→0.3000366초와 shader `_Appearance`0→1이 일치한다. 첫 관측~완료 실시간차0.2805초. 북쪽 가림 숲/능선은 아직 없다(6단계).
- 추가 미실행: near-plane 교차 가림 실제 프레임, 사망/부활 가림 복구 전용 프레임, 최대 흔들림 스폰, 피격 몸/HP 동시 프레임, 이행 중간 실패 주입. 단위 시험·기본 한 판으로 이 전용 반례까지 모두 PASS라 하지 않는다. 마감 리뷰에 이 한계를 넘긴다.
- 네이티브 Play/TMP가 자동 직렬화한 비대상 font/material은 원래HEAD로 복원했고 변경에 포함하지 않는다. ProjectSettings 배경 실행 임시 override는 runner가 종료 시 복원했다. Editor auto tick은 시작 시 이미 enabled,이 실행에서 interval16ms로 갱신되었으며 이전 interval을 확보하지 못해 완전 복원이라 보고하지 않는다. Play stopped/backgroundfalse로 종료한다.

- 마지막 가림 보완: near 평면을 통과하는 AABB는 남은8꼭짓점뿐 아니라12변의 near 교차점까지 투영한다. clip 앞 생존꼭짓점만 쓰던 경우보다 가림 범위를 작게 잡는 결함을 수정했다. 뒤쪽 전체 bounds는 빈 rect. 회귀시험 추가 후 `editmode-near-final.json`90/90 PASS, `recompile-near-status.json`errors0. 기본8단계/캡처는 이 마지막 근평면 교차 계산 보완 직전 수집이며, 게임 입력·규칙·일반 앞쪽 bounds 경로는 그 뒤 바뀌지 않았다. 과거89/89를현재90으로덮어쓰지않고둘다보존한다.
