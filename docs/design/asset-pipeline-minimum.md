# 바위 한 개 최소 자산 파이프라인

ASSET-PIPELINE-012/r001 · 2026-10-31 · L2 최소 구현 · 독립 설계 검토 후 구현

사용자 요구: 최소 자산 계약 검사와 생성 작업 기록·복구를 구현하고 기존 바위 한 개로 전체 경로를 검증한다. [011 설계](ai-3d-asset-harness.md)의 부분 구현이며 A5 영구 승인/전체 gate/맵 연결은 이번 범위가 아니다.

## 구현 범위

- 프로젝트 `Tools/Assets`에 독립 Python CLI, Blender 처리 스크립트, Unity 실행용 C# 스크립트와 단위 시험을 둔다. 기존 게임 코드/맵 C/hook/baseline은 변경하지 않는다.
- source_mode=existing, provider=tripo-studio, 과거 task `21300150-1f34-4e8d-ae0b-a0ca509bf443`와 원본 ZIP 포함7파일의 해시·출처를 계약에 기록한다. API task로 오인하지 않는다. 유료 제출 명령은 구현하지 않는다.
- job.json의 UUID·계약/도구/원본 지문·단계·이벤트를 한 파일에 원자 저장하고 작업별 파일 잠금을 사용한다. 원본 복사 후 의도적으로 종료했다가 같은 job으로 재개하여 원본 재사용과 단계 복구를 검증한다. 완료 산출물의 해시가 바뀌면 조용히 재사용/덮어쓰지 않고 차단한다.
- 추가로 이미 수락한 Tripo API task의 읽기 전용 `task get` 조회 어댑터를 지원한다. Studio job에 호출 금지. 단위 시험은 가짜 CLI 응답으로 timeout/오류/작업 ID 불일치/재조회/URL 비기록을 확인한다. 실제 API 조회/다운로드·새 생성은 NOT_RUN으로 분리한다.
- 원본 FBX와 텍스처를 보존하고 Blender 5.2.2에서 새 처리본 FBX로 내보낸다. 경량화·가로/깊이 최대2단위·바닥 원점을 맞추고 삼각형/크기/원점 및 전후 캡처를 기록한다.
- 개인 Unity에서 새 개정 후보 폴더에만 임포트한다. URP/Lit basecolor/normal 연결, 실제 저장 후 재로드한 프리팹의 메시·재질·텍스처·크기·원점·금지 컴포넌트를 검사한다. 검사기는 가져오기 성공과 규격 통과를 분리한다.
- 격리 시험 씬에서 실제 Play 프레임/화면·콘솔을 확인하고 원래 씬·선택·background·auto-tick 상태를 복구한다. 사용자가 이미 Play 중이거나 미저장 씬이면 차단한다. 기존 폴더/원본/게임 씬을 덮어쓰지 않는다.

## 시험 규격과 완료 기준

이번 수치는 게임 최종 규격이 아닌 **파이프라인 시험 규격**이다: 삼각형 ≤10,000(약50만 원본에서 경량화 작동을 확인하는 시험 목표), 메시/재질 각1, texture 최대2048, 가로/깊이 최대2 Unity 단위(기존 전송 시험 크기 재사용), 바닥/가로중심 오차0.01, 허용 shader URP/Lit, Collider/Rigidbody/스크립트/Animator 없음. 기준은 처리 전에 고정하며 초과 시 기준을 완화하지 않고 FAIL을 보존한다. 실제 미술/모바일 성능/맵 배치/상업 이용 승인으로 승격하지 않는다.

검사 하한도 고정한다: 삼각형은 양의 정수, bounds의 각 축은 유한하고 >0, `abs(max(X,Z)-2)≤0.01`, `abs(minY)≤0.01`, `abs(centerX/Z)≤0.01`이어야 한다. 0삼각형·NaN·퇴화 축·0.02단위·음수 바닥/중심 오차 반례를 단위 시험에 넣는다.

최소 명령은 init(계약/기존 작업 기록), resume(원본 복사/처리 재개), status(지문/단계 재확인), query-task(API로 기록된 수락 ID만 읽기 조회), Unity 인수 실행이다. 미래 미구현 명령을 안내하지 않는다. [실행 사용법](../../Tools/Assets/README.md)과 [검증 기록](../reviews/asset-pipeline-012-review.md)을 따른다.

소유 경계: 원본은 기존 output/tripo-studio-test/source 읽기 전용, 작업별 출력·기록은 이 프로젝트 output/asset-pipeline, 후보 Unity 자산만 승인된 개인 대상 안에 쓴다. 기존 Tools/Verify의 all --screens와 gate를 재사용하되 최소 파이프라인 시험은 별도 결과로 남긴다. 상태 기록은 원본 게임의 DSGame/PrototypeData나 맵 request v1를 변경하지 않는다.

실패/복구: 잠금은 OS 소유 파일 잠금, job 갱신은 임시파일 fsync 후 교체. 처리 중 종료는 완료 표시가 없는 고유 attempt 출력만 남기고 새 attempt로 재개한다. 완료 산출물 지문 불일치나 입력/도구 개정 변경은 BLOCKED. Unity 후보 폴더 충돌은 중단하고 사용자 파일 삭제/자동 덮어쓰기 금지. 시험 씬 변경 전 복구할 상태를 프로젝트 output에 기록하고 finally에서 소유 상태만 복구한다. 복구 실패는 품질 통과와 별도 FAIL/BLOCKED다.

Unity 변경 전 프로젝트/PID·run ID·원래 씬/선택/background·auto-tick 현재 enable/interval 및 저장된 SessionState 값을 소유 journal에 기록한다. 읽을 수 없으면 설정을 변경하지 않고 BLOCKED. 미완료 실행 표시가 남아 있으면 다음 실행은 새 Unity 작업을 차단한다. 호스트 강제 종료에 대한 자동 복구는 이번 최소 범위 밖이며, 소유 기록과 현재 상태의 수동 확인·복구 기록 후에만 해제한다. 정상 경로는 finally 복구 후 실제 상태를 재조회하여 원상태와 비교한다.

검증 계획: 독립 design-reviewer → 구현 → 계약 불량/경로 이탈/원본 변조/중단 재개/중복 잠금/CLI timeout 등 단위 시험 → 기존 바위 복사 후 재개·Blender 경량화·Unity 실측/실제 프레임/캡처 → 실제 바위 원본·후보의 규격 비교(원본 예산 초과 검출 포함) → 개인 Unity 기존 게임 all --screens → validator-client → 지적 해소 → 최종 gate. 계산은 기존 Blender modifier와 Unity 실측만 사용하고 게임 기하 알고리즘은 바꾸지 않는다.

미완료 범위: 유료 생성 제출·live API 재개/원격 다운로드·정확히 한 번 과금·전체 A5 등록·AFI 전수·G5·모바일 성능. 실패한 실제 단계는 숨기지 않고 원인/복구·재시험을 새 실행으로 남긴다.

변경 전 개인 Unity의 `Assets/DeadSurvivor`, `ProjectSettings`, `Packages` 파일을 해시와 함께 백업한다. 미저장 자산도 시작을 차단한다. 셰이더 컴파일 자체의 dirty 플래그는 편집 가능한 직렬화 자산과 구분한다. Play 중 소유권 조회는 씬 편집 전용 API를 사용하지 않는다. 마지막 의존/기존 파일/원본 지문 검사 자체에 오류가 생겨도 각각 FAIL/BLOCKED로 기록하고 요약 파일을 저장한다.
