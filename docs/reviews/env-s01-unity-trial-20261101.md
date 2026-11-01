# ENV-S01 돌벽 — Unity 배치·크기·가림·충돌 확인

2026-11-01 · L1 · 시험 후보 배치/실제 게임 동작 확인. 사용자 요청에 따라 GPT-6.1 Sol high 서브에이전트가 설계·좌표·구현·실제 관측/화면을 독립 검토하고 메인이 Unity 조작과 수정·실행을 담당했다.

## 사용할 결과

- [저장 시험 씬](../../Assets/DeadSurvivor/Scenes/ENV_S01_WallTrial_r001/ENV_S01_WallTrial.unity): Unity에서 열고 Play → 캐릭터 선택 → 스쿼드 출전 → WASD 이동. 기존 028 대표 구역의 카메라/게임 UI/캐릭터/정상 Update를 사용한다. 이 씬은 전투를 비활성화해 벽 이동·가림을 확인하는 후보이며 최종 맵이나 빌드에 자동 채택하지 않았다.
- [돌벽 프리팹](../../Assets/DeadSurvivor/Art/Environment/ENV_S01/r001/Wall.prefab), [URP 재질](../../Assets/DeadSurvivor/Art/Environment/ENV_S01/r001/Stone.mat), [시험 충돌 Layout](../../Assets/DeadSurvivor/Scenes/ENV_S01_WallTrial_r001/Layout.json).
- [4인 실제 게임 화면](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/four-front-fixed.png), [단일 정면](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/front-fixed.png), [후면](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/back-fixed.png), [끝단 우회](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/bypass-fixed.png).
- [원본 생성·비용 기록](env-s01-first-generation-20261101.md). 이번 작업은 기존 모델을 사용했으며 추가 Tripo 호출/크레딧 소비는 없다.

## 결과와 범위

| 항목 | 실제 결과 |
|---|---|
| Unity 실측 | 길이X 2.40 × 높이Y 0.50 × 깊이Z 0.60, 2,893삼각형, 메시/재질 각1, BaseColor/Normal 각1K |
| 배치 | world(-22.8, .003, 29.6), 중앙바닥 피벗, 지면 겹침 방지 .003 높이 보정 |
| 기존 화면 기준 | 카메라55도·ortho6 유지, 캐릭터 표시높이.72. Unity 단위이며 실제 미터/전체 자산 예산으로 해석하지 않음 |
| 충돌 | 앞·뒤·좌·우 지속 입력으로 접근 후 정지, 끝단 우회, 4인 정면 진입 PASS |
| 실제 가림 | 정면 몸통 가림 결함 발견 후 수정. 필요한 때 벽 alpha.22, 벗어나면 원본 재질/alpha1 복구. 후면·옆에서는 캐릭터 식별 가능 |
| 입력/관측 | 7사례·1,200연속 관측 프레임, 실제 가상 키보드 → 정상 Update 경로. 사례마다 20해제 프레임에서 입력0·파티 중심 정지 확인 |
| 독립 계산 | center/actual/reserved 4,740구간. 반경.16, 최소 벽 거리.1600416으로 충돌 여유.00004 충족 |
| 저장/불변 | 시험 씬 저장 후 재개방하여 Play. 기존 028/018 씬·프리팹·Layout과 생성 원본 보존. 아래 자동 저장 복구 참조 |
| 최종 회귀·복구 | 컴파일·규칙·실제 입력/프레임·다중 해상도 화면·콘솔·복구 PASS. 최종 gate는 마감 기록 참조 |
| 미실행 | 전투·적 AI·공격 차폐의 새 벽 인수, 전체 맵/G2/G3/G5, 최종 미술 A5, 빌드/실기기 성능 |

[Unity 자산 실측](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/authoring.json) · [독립 이동 계산](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/movement-analysis.json) · [서브에이전트 검토](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/independent-review.json) · [마감 기록](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/completion.json).

## 충돌 연결

원래 게임은 PhysX Collider가 아니라 DSMapSession.Geometry → DSGame.PlanPartyMotion → DSMapMotion.Propose/SafeSweep 경로로 이동을 제한한다. 모델에 Collider만 추가하지 않고, 사본 Layout의 먼 OB-064를 돌벽 위치로 옮겼다. 다른63개 C는 그대로다. 기존 OB-064 시각 요소는 사본에서 숨겼고 새 돌벽 renderer를 가림 목록에 등록했다.

시험 C는 px X265..295/Y206.25..213.75, world X[-24,-21.6]/Z[29.3,29.9]다. 높이는 2D 충돌 계산에 사용하지 않으며, 스키마상 OB-064의 기존 height_logical16을 유지했다. 실제 외형 높이.5는 FBX renderer 실측으로 확인했다. 이 사본은 원래 MAP007/018 C 채택이나 전수 경로 검증을 대신하지 않는다.

각 사례는 시작 위치만 안전한 pose로 준비하고, 그 뒤는 가상 키보드를 실제 InputSystem에 전달해 정상 게임 Update로 이동했다. 모델 앞뒤·옆의 정지 지점은 몸체 반경을 포함한 예상 경계와 맞았다. 기록된 모든 사례에서 시작→첫 프레임 및 이후 프레임 번호가 연속이었다. 독립 검토자는 메인의 분석 함수를 실행하지 않고 별도 구간-직사각형 거리 계산으로 같은 결과를 확인했다.

## 발견한 가림 문제와 수정

[수정 전 정면](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/front-single.png)은 발끝과 HP바만 남고 몸통이 대부분 가려졌다. [DSWorld.Map.cs](../../Assets/DeadSurvivor/Runtime/DSWorld.Map.cs)의 focus가 feet + worldUp*.36으로 설정되어 카메라 방향으로 기울어진 실제 sprite 중심을 놓치고 있었다.

focus 한 줄을 feet + worldUp*.01 + cameraUp*.36으로 맞췄다. 벽의 높이·충돌 범위·카메라·기존 alpha.22는 유지했다. 수정 후 정면 단일/4인 모두 캐릭터를 읽을 수 있고, 후면과 양옆은 벽이 불투명해도 캐릭터가 보인다. 실제 뒤로 이동한 walk-away 사례에서 alpha.22→1 및 원본 Stone.mat 객체 참조 복구를 확인했다. 강제로 focus를 벽 안에 넣은 시험을 실제 가림 증거로 사용하지 않았다.

## 검증·복구 기록

첫 front-single.json은 창 비활성 상태에서 가상 입력이0으로 읽힌 시험 도구 설정 문제였다. 파일의 PASS_OBSERVATION은 관측 수집 완료일 뿐 동작 PASS가 아니다. 원본 InputSettings를 복제해 시험 동안만 background IgnoreFocus/게임 입력 전달을 적용하고, 외부 입력 취소·장치/원본 설정 복구를 넣었다. front-single-r2는 실제 충돌 관측에는 성공했지만 가림 문제를 드러낸 수정 전 결과다. 최종 판정에는 *-fixed 7사례만 사용한다.

입력 해제 검사는 게임을 끄기 전에 정상 Update20프레임을 관측했다. 파티 중심이 멈추며 뒤따르는 캐릭터는 정상적으로 예약 위치에 수렴한다. 그 뒤 가상 장치·입력 설정을 복구하고 제가 시작한 Play만 종료했다.

Unity가 기존 글꼴 캐시와 MeadowTufts의 doubleSidedGI를 자동 저장한 부작용을 작업 전 SHA로 발견했다. 기존 사용자 변경을 초기화하지 않고, 글꼴은 정확히 같은 시작 SHA의 Git blob을 찾아 복구했으며 재질은 doubleSidedGI만 원래0으로 되돌렸다. [복구 근거](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/auto-save-recovery.json). 기존 파일 중 의도한 변경은 DSWorld.Map의 한 줄뿐이다.

자산·씬·가림 변경에 대해 기존 all --screens를 수행했다. 첫 회귀도 PASS였지만, 자동 저장 파일을 시작 상태로 복구하면서 실행 의존 지문이 달라져 최종 상태에서 한 번 다시 실행했다. 이는 문서 수정 때문에 반복한 검사가 아니다. 최종 보고서와 gate는 마감 기록을 따른다. 전수 topology/전체 Python/부하 시험은 새로 실행하지 않았다.

[설계·완료 기준](../design/env-s01-unity-trial.md). 실행 시작/첫 사용 가능/종료 시각과 파일 지문은 마감 기록 및 command 원자료에 기록한다. 기록하지 않은 내부 작업별 소요 시간은 미측정이다.

최종 회귀: [20261101-232844-4484d0b8 PASS](../../output/verification/20261101-232844-4484d0b8/summary.json). [원래 장면/파일 복구](../../output/asset-pipeline/env-s01/r001/unity-trial-20261101/restoration.json) PASS. 4:3 최대 장착 전투 화면을 포함해 실제 캡처를 확인했다.
