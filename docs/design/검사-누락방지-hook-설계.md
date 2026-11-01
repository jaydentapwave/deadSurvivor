# 검사 누락 방지 — 프로젝트 로컬 hook

- 상태: 구현·검증 중. 규모 L2: 검사 완료 판정과 지문/실행 경계를 신설.
- 사용자 요구: 검사 생략을 막도록 실제 설정. 전역 설정/회사 연동 금지 유지.
- 범위: `.codex/hooks.json` Stop hook, `.githooks/pre-commit`, 공통 `Tools/Verify/gate.py`, 통합 검사/실패주입 시험, 로컬 설명·검증 기록. Unity 게임 코드/씬 변경 없음.
- 설치 전 상태: Git core.hooksPath=/dev/null, 프로젝트 Codex hooks 없음. CLI0.163.0의 hooks 기능 true. 기존 run.py local과 map007 audit는 수동 실행.
- 공식 근거: https://learn.chatgpt.com/docs/hooks (2026-10-30 직접 확인). 프로젝트 hook 발견 위치, Stop JSON decision:block, 변경 hook 신뢰 검토 필요. Stop은 실패 시 후속 작업을 이어가게 하는 장치이며 사용자 중단을 막는 보안 경계가 아님.
- 설계: 종료/커밋 때 공통 gate가 local→맵 준비 audit→입력/출력 일관성 검사를 직접 실행. 기존 PASS 로그만 보고 통과하지 않는다. 실패·시간초과·빈/불완전 결과·실행 중 소스 변경은 차단. hook 입력은 cwd/event와 Stop 재호출 여부만 읽고 대화 원문/회사 자료를 열지 않는다. 결과는 output/verification 내부.
- 대상 제한: 스크립트 위치로 개인 root 확인. hook cwd가 이 프로젝트 밖이면 외부 쓰기 없이 거부. 다른 저장소에 설정하지 않음.
- 런타임: 설치 시 현재 소스 상태를 '변경 감지 기준'으로 기록하되 검증 완료로 표시하지 않음. 이후 UnityMigration/UnityArtExport 변경은 현재 지문의 Unity all --screens 검증 증거 없으면 BLOCKED. hook이 Editor를 자동 시작/동기화하지 않음. 실제 대상 프로젝트 검사와 동일 지문이어야 함. 맵 런타임 하네스 미구현은 계속 미검증.
- Git: 해당 저장소의 hooksPath만 `.githooks`로 변경. 커밋/스테이징/원격 변경 없음. pre-commit은 검사 전후 작업 파일과 인덱스를 확인해 검사한 파일과 커밋 파일이 다르면 거부. 부분 스테이징은 보수적으로 차단하며 자동 git add 하지 않음.
- Codex: 프로젝트 Stop 정의만 작성. 신뢰 DB나 우회 옵션으로 승인 절차를 건너뛰지 않음. 실제 발견/신뢰/발동이 확인되지 않으면 '설정됨, 활성화 확인 필요'로 보고. 전역 수정 없이 가능한 범위 수행 후 필요한 사용자 UI 동작을 안내.
- 상태·복구: PASS/FAIL/BLOCKED와 검사별 원자료·시각·소스 지문. Stop에서 stop_hook_active=true라도 실패를 통과시키지 않음. 반복 블록 시 해결 불가를 사용자에게 알릴 수 있으나 성공 영수증은 발급하지 않음. 사용자의 직접 중단은 존중. 설치 전 Git 설정 보존, 문제 시 명시적 복구 지침.
- 재사용: run.py local, map007 audit 재사용. audit는 FAIL일 때 종료코드도 실패하도록 보완. run.py Unity 결과에 새 검증 지문을 연결하는 최소 메타데이터 추가.
- 검증: 격리 fixture에서 정상/누락/실패/timeout/변경후옛증거/실행중변경/인덱스불일치 및 hook JSON 응답 검사. 실제 repo에는 명령 형태로 gate 수행, 실제 Git 커밋은 하지 않음. 독립 검토 후 지적 수정.
- 미정/한계: Codex 신뢰를 최종 사용자가 UI에서 확인해야 할 수 있다. hook 비활성화/--no-verify 등 명시적 우회를 절대 막는 보안 시스템은 아님. 독립 리뷰·시각 판단의 진실성을 기계 검사로 대체하지 않음.
- HG-01 보완: Unity 증거는 옵션/대상/소스·검사기/대상 프로젝트 전후 지문, 필수8결과, 비어 있지 않은 화면 목록과 실제 캡처/로그 파일 지문을 확인한다. 대상과 대응을 검증하지 못한 아트 변경은 계속 BLOCKED이며 새 PASS로 덮지 않는다.
- HG-02 보완: MAP007 역사적 inputs.json은 보존한다. 활성 맵 검사 의존성은 별도 gate 정책에 고정하며 원본 PNG/현재 layout/도면/CSV를 검사한다. 이전 코드·문서 지문은 현재 코드와 달라져도 과거 기록으로 남기고 현재 런타임 증거로 재사용하지 않는다.
- HG-03 보완: Git 입력 집합은 tracked+untracked(ignored 제외)이며 출력/캐시를 제외한다. 커밋 시 전체 집합의 index 내용/파일모드/추가/삭제/링크를 대조하고 전후 index 지문을 비교한다. 신설 검사기/hook/맵 파일 미스테이징도 거부한다. 부분 스테이징을 자동으로 고치지 않는다.
- 설계 검토: 1차 CHANGE_REQUIRED(HG-01~03) → 보완 재검토 PASS.

## CH-HG-LOOP-20261101 — Stop 재호출 알림 제한

- 사용자 요구: 병행 작업에서 같은 BLOCKED 메시지가 반복해서 뜨지 않게 한다.
- 규모 L1. 게임/맵/검증 합격 기준·baseline·공통 훅 정의는 그대로 두고 Stop 응답 제어와 회귀 시험·설명만 수정한다.
- 원인: Stop의 `decision=block`이 자동 후속 프롬프트를 생성하는데 기존 코드는 `stop_hook_active`를 무시해 동일 차단을 무한 재요청했다.
- 공식 근거: [OpenAI Hooks — Stop](https://learn.chatgpt.com/docs/hooks), 2026-11-01 직접 확인. `stop_hook_active`는 해당 턴이 이미 Stop에 의해 이어졌는지 나타낸다.
- 동작: 최초 Stop은 기존처럼 검사하고 실패 시 한 번 block. 유효한 재호출(`stop_hook_active=true`)도 모든 검사를 실행하고 실제 FAIL/BLOCKED를 새 gate.json에 보존하지만 응답은 빈 JSON으로 내보내 자동 재개와 반복 알림을 요청하지 않는다. PASS로 승격하지 않는다.
- 실패/취소 경계: 잘못된 입력/event/cwd/flag 타입 또는 검사 실행·기록 자체 예외는 계속 block. 재호출에서 검사 결과가 기록된 경우에만 후속 알림을 억제한다. 사용자가 막힘을 보고한 후 같은 소스를 검증 중인 다른 에이전트의 작업을 반복 실행하도록 강제하지 않는다.
- 변경 파일: Tools/Verify/gate.py, test_gate.py, 기존 설계/워크플로우 설명, 별도 리뷰 기록. .codex/hooks.json/.githooks/pre-commit/전역 설정/신뢰 DB 변경 없음.
- 검증: 최초 실패 block, 재호출 실행/새 실패 기록/무알림, 재호출 PASS, malformed flag, 기록 실패, 실제 격리 Git 커밋 실패·성공. 기존 stale evidence·기준·원자료·인덱스 검사를 유지한다.
- 호환: flag가 없는 입력은 최초 호출로 취급한다. check/pre-commit의 실패 종료 코드와 검사 목록은 바뀌지 않는다.
- 기존 ‘재진입도 실패를 통과시키지 않음’은 검사 결과 보존과 커밋/수동 검사 실패를 뜻한다. 사용자 요청에 따라 재진입 자동 대화 재개는 이 절의 새 정책을 적용한다.
- 독립 구현/증거 검토는 validator-client가 읽기 전용으로 진행한다. 실제 Unity 전체 회귀의 필요 여부는 기존 gate가 현재 입력 지문으로 판단한다.


### CH-HG-LOOP-20261101 r002 — 정상 Stop의 기록 전용 응답

첫 호출 block을 남긴 r001은 사용자의 새 질문이 끝날 때도 최초 알림을 다시 생성했다. ‘안 뜨게’ 요청을 충족하도록 정상적으로 검사를 실행·기록한 Stop은 최초/재호출 모두 `{}`로 응답한다. 실제 FAIL/BLOCKED·원자료·현재 지문 검사와 수동 check/pre-commit의 실패 종료 코드는 유지한다. `continuation_requested=false`, `notification_policy=record_only`를 영수증에 기록한다. 입력이나 검사 기록 자체가 잘못된 예외는 계속 차단한다. 앞 절의 최초 알림 유지 정책은 이 개정으로 대체한다. 훅 정의와 신뢰/전역 설정은 변경하지 않는다.
