# 데드 서바이버 작업 진입점

[AGENTS.md](AGENTS.md)가 공통 규칙 원본이다. 먼저 읽고 [개발 워크플로우](docs/workflows/development.md)를 적용한다. 이 프로젝트의 사람용 문서도 로컬 Markdown이다. 외부 문서 서비스는 사용하지 않는다.

Claude의 역할 정의는 `.claude/agents/`에 있으며 공통 역할 계약은 `docs/workflows/`에 있다. [핵심 규칙](docs/workflows/core-rules.md)을 먼저 읽는다. 프로젝트 hook(`.claude/settings.json`)은 효율 가드 세 가지만 둔다: 이유 없는 전체 Play 차단, 같은 주제 3번째 Codex 검토 차단, 긴 대화 알림(2026-11-02 사용자 승인). 검사 누락 방지 gate는 자동 실행하지 않으므로 문서에 적힌 검사를 직접 실행한다.
