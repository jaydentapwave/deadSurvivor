# 작업 진입점

[AGENTS.md](AGENTS.md)가 공통 규칙 원본이다. 먼저 읽고 [핵심 규칙](3dkit/workflows/core-rules.md)과 [현재 상태](docs/현재상태.md)를 읽는다. 사람용 문서도 로컬 Markdown이며 외부 문서 서비스는 쓰지 않는다.

Claude의 독립 검토 역할 정의는 `.claude/agents/`에 있고, 역할 계약 본문은 `3dkit/workflows/`에 있다. 공통 워크플로우·도구는 `3dkit/`이 원본이다.

프로젝트 hook(`.claude/settings.json`)은 [하네스 설계도](3dkit/workflows/harness-blueprint.md) H0 단계에서 만든다. 만들기 전까지는 핵심 규칙의 검토 상한·전체 Play 조건·새 세션 기준을 직접 지킨다.
