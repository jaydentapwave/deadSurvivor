# 040 검증 기록 — Codex→Claude 인수인계와 공통 작업 선호

- 날짜·범위: 2026-11-02 KST. L1 문서 변경([브리핑](../design/agent-handover-040.md)). 게임 코드·데이터·hook·gate 기준 변경 없음.
- 변경 파일: `AGENTS.md`(작업 방식 선호 절), `docs/개발환경.md`(에이전트 도구 연결 표, gate 문단 한 문장), `docs/workflows/development.md`(Claude 역할 인식 확인 결과), 이 기록과 브리핑.
- 적용 요구: 사용자 "한곳에서 계속 바라보게끔"(공통 원본), 서브에이전트 모델·effort 메인 위임.
- 실행 환경: macOS, Claude Code 메인. 독립 검토는 Codex `codex exec -s read-only`(모델 gpt-6.1-sol, 세션 `01a1982f-8b07-7041-a05f-27c6692224f4`). 검토 원문은 로컬 `output/agent-handover-040/codex-review-r001.md`(Git 제외, SHA256 `75f8e67d8411bdfe3217e6a63c6ca528935fbfc0672eed797312e93bb449caa1`).

| 검사 | 상태 | 근거 | 한계·다음 조치 |
|---|---|---|---|
| gate(문서 변경 후) | PASS | `gate-20261102-023642-7414a562`: docs RUN, data·map_oracle_foundation·map_preparation·runtime_evidence REUSE | REUSE는 새 실행 PASS가 아님 |
| 독립 검토(validator-client 계약, 교차 모델) | PASS | 스펙 준수 PASS, 동작 계약 PASS. 필수 수정·후속 제안 없음. 기존 규칙과 충돌 없음, Tripo CLI 0.8.1·`output/tripo-state`·Unity CLI 1.0.0-beta.15·Blender 존재, Stop hook 신뢰 기록이 옛 Documents 경로에만 있음, 새 앵커 2개 유효를 확인 | Claude 쪽 역할 인식과 `~/Documents` EPERM은 검토자가 재현하지 않음. 메인이 이 세션에서 직접 확인함 |
| Unity·런타임 | NOT_RUN | 문서 변경이라 해당 없음 | — |
| Claude Unity 플러그인 갱신 | BLOCKED | 공식 마켓플레이스 최신이 0.1.6-beta. Unity 공식 저장소 마켓플레이스 추가는 Claude Code 자동 모드 보호가 거부함 | 사용자가 직접 실행하면 해소. 그전까지 [개발 환경](../개발환경.md#에이전트-도구-연결)의 새 명령 이름을 사용 |
| Codex 프로젝트 Stop hook 자동 발동(Desktop 경로) | NOT_RUN | 신뢰 기록이 옛 경로에만 있음 | 사용자가 Codex `/hooks`에서 재신뢰하면 확인 가능 |

에이전트별 메모리의 중복 기록은 이 공통 원본으로 옮긴 뒤 정리했다.
