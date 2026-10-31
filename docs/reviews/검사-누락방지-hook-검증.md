# 검사 누락 방지 hook 검증

범위: 프로젝트 로컬 검사기/설정. 게임 코드·씬·Unity 실행 없음.

- 설계 리뷰: hook_design, HG01 런타임 증거 필수성/HG02 역사 지문 보존/HG03 인덱스 범위 보완 후 PASS.
- 실패주입 회귀: [2026-10-31 12:15:18 KST 결과](../../output/verification/hook-tests-20261031-121518/summary.json),13개 시험 메서드 PASS. [상세 로그](../../output/verification/hook-tests-20261031-121518/tests.txt). 내부 반복과 격리 Git/Stop 호출 포함. Unity 런타임 검증이 아님.
- 초기 fixture 실패2회: 문서가 참조하는 ignored 과거 증거 누락과 macOS 임시 경로 canonicalization 문제. 복제 fixture만 보완 후 재실행. 실제 검증 생략으로 해결하지 않음.
- 실제 프로젝트 gate: [2026-10-31 10:42:14 KST PASS](../../output/verification/gate-20261031-104214-7c935cd7/gate.json). 현재 파일로 local+151맵검사+활성 근거/출력+런타임 변경 여부 검사.
- Git hooksPath=.githooks 로컬 설정 확인. 정상/실패 실제 커밋은 격리된 임시 저장소에서만 수행. 사용자 저장소의 커밋/스테이징/작성자 정보 변경 없음.
- Codex Stop 정의 작성, 입력/출력 계약 및 차단 직접 시험 PASS. [native 발견 조회](../../output/verification/hook-discovery.json)는 로컬 app-server proxy 접근 불가로 BLOCKED. **실제 데스크톱 hook 신뢰/발동은 NOT_RUN**이며 직접 호출 시험으로 대체하지 않음.
- 전역 설정/신뢰 DB/우회 플래그 변경 없음. 프로젝트 .codex 및 .git 로컬 설정 작성에만 요청된 권한을 사용.
- 최종 독립 검토: hook_validator 코드/문서 PASS. 실제 Unity 대상 동시 변경 지적 해결과 과거 결과 재사용 차단 확인. 최종 파일 지문 검사는 후속 gate.json에 기록한다.

- 독립 검토의 실제 Unity 대상 동시 변경 지적을 수정: 검사 시작/종료 대상 지문 비교 및 실패주입 시험 PASS. 이전 보고서를 새 검사 결과로 반환하는 경우도 차단 시험 PASS.
