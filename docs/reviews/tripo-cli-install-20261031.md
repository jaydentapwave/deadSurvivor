# Tripo CLI 설치 오류 복구

- 범위: 사용자가 제시한 `tripo login` 실행 파일 없음 오류 복구. 기존 설치 안내 이행이며 신규 자동화 워크플로우/게임 구현이 아닌 L0 환경 준비 작업이다.
- 실행 시각: 2026-10-31 13:20:44 KST. 이번 실행 결과이며 과거 성공 로그를 재사용하지 않았다.
- 원인: 조사 시 `output/tools`와 `output/tools/tripo`가 존재하지 않았다. 이전 설치 명령이 실행됐는지 또는 실패했는지는 확인되지 않았다.
- 변경: 프로젝트 내부 `output/tools/tripo`에 공식 npm 패키지 `tripo-cli@0.8.1` 설치. 의존성 11개, 설치 스크립트 실행 생략. npm 캐시도 `output/tools/npm-cache`에 보관했다.
- 환경: macOS, Node.js 25.9.0, npm 11.12.1. 전역 Codex/Claude 설정 및 Unity 프로젝트 변경 없음.
- 근거: [공식 CLI 안내](https://developers.tripo3d.ai/en/docs/cli), npm registry의 `version`, `bin`, `engines`, `dist.integrity` 조회. 현재 패키지의 실행 명령은 `tripo`이다.
- 검사 원자료: [설치 검사 JSON](../../output/verification/tripo-cli-install-20261031.json).
- 설치 잠금 파일 SHA-256: `abc1f95a7906c0dcb6d3497155ddc0c9d904e8c8bb4940e1f709b3dc33895c71`. 패키지 메타데이터/실행 파일 지문은 원자료에 포함했다.

| 검사 | 상태 | 근거 및 한계 |
|---|---|---|
| npm 설치 | PASS | 종료 코드 0, 패키지 0.8.1 설치 |
| 실행 파일 | PASS | `--version` → `0.8.1`, 종료 코드 0 |
| 로그인 명령 지원 | PASS | `login --help` 정상. 실제 인증 성공을 의미하지 않음 |
| 계정 인증·API 통신·잔액 | NOT_RUN | 사용자가 터미널에서 계정 지역 선택 및 브라우저 인증 필요 |
| 모델 생성·비용 발생 작업 | NOT_RUN | 생성 요청 없음 |
| Unity·맵·하네스 실행 | NOT_RUN | 이번 설치 범위 밖 |

로그인 때 `TRIPO_HOME`을 프로젝트의 `output/tripo-state`로 지정하고 `TRIPO_ANALYTICS_ENABLED=0`, `TRIPO_NO_UPDATE_CHECK=1`을 적용한다. 이 환경 변수는 검사 프로세스에만 적용했으며 사용자의 터미널 설정을 변경하지 않았다. `output`은 기존 Git 제외 경로다. 로그인 후 같은 환경에서 `tripo doctor`로 실제 연결을 확인해야 한다.

## 계정 연결 추가 확인 — 2026-10-31 13:30 KST 기록

사용자가 국제 서비스 계정을 선택한 후 브라우저 인증을 진행했고, 확인 요청에 따라 기존 인증 요청을 재개했다. 위 표의 인증 NOT_RUN은 설치 직후 상태다. 이번에 `login --region ov --no-wait --no-open`은 `logged_in`, `doctor --json`은 `ok=true`를 반환했다. 별도 LLM 설정 없이도 CLI 명령 실행이 가능하다.

| 검사 | 상태 | 근거 및 한계 |
|---|---|---|
| 계정 인증 | PASS | 국제 서비스 `ov`, 로그인 완료 |
| API 통신 | PASS | `doctor`의 API reachability 정상 |
| 설정 저장·권한 | PASS | `output/tripo-state/config.json` 존재 및 0600 확인. 자격 증명 내용은 보고서에 복사하지 않음 |
| API 잔액 조회 | PASS | 사용 가능 0크레딧, 처리 중 예약 0크레딧 |
| 유료 모델 생성 준비 | BLOCKED | API 크레딧 없음. 인증 실패와 구분 |
| 모델 생성·Unity·맵 실행 | NOT_RUN | 생성 요청 및 Unity 변경 없음 |

[연결 확인 기록](../../output/verification/tripo-cli-auth-20261031.json)에 이번 결과와 설치 잠금 파일 지문을 기록했다. 사용자 Terminal의 폴더 접근 오류는 해결 여부 미확인이지만 Codex 실행 환경의 인증·API 접근은 직접 검증했다. 로그인 출력은 기본 저장 경로를 표시했으나 설치 코드의 `TRIPO_HOME` 처리와 실제 프로젝트 설정 파일 존재를 확인했다.
