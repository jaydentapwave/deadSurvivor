# 검사 누락 방지 — 프로젝트 로컬 hook

- 상태: 구현·검증 중. 규모 L2: 검사 완료 판정과 지문/실행 경계를 신설.
- 사용자 요구: 검사 생략을 막도록 실제 설정. 전역 설정/회사 연동 금지 유지.
- 범위: `.codex/hooks.json` Stop hook, `.githooks/pre-commit`, 공통 `Tools/Verify/gate.py`, 통합 검사/실패주입 시험, 로컬 설명·검증 기록. Unity 게임 코드/씬 변경 없음.
- 설치 전 상태: Git core.hooksPath=/dev/null, 프로젝트 Codex hooks 없음. CLI0.163.0의 hooks 기능 true. 기존 run.py local과 map007 audit는 수동 실행.
- 공식 근거: https://learn.chatgpt.com/docs/hooks (2026-10-30 직접 확인). 프로젝트 hook 발견 위치, Stop JSON decision:block, 변경 hook 신뢰 검토 필요. Stop은 실패 시 후속 작업을 이어가게 하는 장치이며 사용자 중단을 막는 보안 경계가 아님.
- 설계: 종료/커밋 때 공통 gate가 local→맵 준비 audit→입력/출력 일관성 검사를 직접 실행. 기존 PASS 로그만 보고 통과하지 않는다. 실패·시간초과·빈/불완전 결과·실행 중 소스 변경은 차단. hook 입력은 cwd/event만 읽고 대화 원문/회사 자료를 열지 않는다. 결과는 output/verification 내부.
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
