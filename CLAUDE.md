# 데드 서바이버 작업 진입점

공통 작업 규칙(저장 경계, 작업 방식)은 [AGENTS.md](AGENTS.md)에 있다. Codex·Claude가 함께 따르는 원본이므로 세션을 시작하면 먼저 읽는다. 이 프로젝트의 사람용 문서도 로컬 Markdown이다. 외부 문서 서비스는 사용하지 않는다.

## 지금 방향: 2D로 전환 (2026-11-15 사용자 결정)

- 사용자 원문: "데드서바이버 프로젝트를 2d게임으로 한번 변경을해볼래고해, 이전에 만든것들은 그냥 냅두고 시스템만 가져갈거야".
- **새 작업**: 2D 게임의 코드·장면·리소스는 `Assets/Game/`에 아래 studio-kit 규칙(Stack, Folder Rules, `.ai/rules/`)으로 새로 만든다. 기능 작업은 studio-kit 디렉터 흐름(`director-game`, `director-run`)을 따른다. 아트는 `/art-direction` 또는 `/art-reference` → `/art-spec` → `/2d-art-produce` 순서다.
- **예전 작업물은 참고용**: `Assets/DeadSurvivor/`, `Assets/Scenes/`, 3D 장면·모델, `3dkit/`, `NewProjectKit/`, 3D 기록 문서(`docs/design`, `docs/reviews`, `docs/art`의 기존 파일)는 그대로 두고 고치지 않는다. 장비·진형·웨이브 같은 시스템은 예전 코드(`Assets/DeadSurvivor/Runtime/`)와 기획(`docs/`)을 읽어 새 구조로 옮긴다.
- [핵심 규칙](docs/workflows/core-rules.md)의 0·3·4·5·6절(만드는 일 먼저, 검토 주제당 2회, 단계마다 새 대화, 규칙 늘리기만 하지 않기, 사용자가 먼저 본다)은 2D 작업에도 적용한다. 1·2절의 Play 실행기·증거 도구(`Tools/Harness`, `Tools/Proto`, `Tools/Verify`)는 예전 장면용이다. 2D 확인은 `.ai/rules/test-before-claim.md`, `.ai/rules/playable-check.md`, `Tools/editor_ready.py`, `Tools/playable.py`를 따른다.

## 예전 3D 작업에만 적용

- [개발 워크플로우](docs/workflows/development.md)와 `docs/workflows/`의 역할 계약, 맵 하네스, 에셋 경로(`3dkit/tools/assets.py`)는 예전 3D 작업(`Assets/DeadSurvivor`)의 절차다.
- Claude hook(`.claude/settings.json`): 예전 효율 가드 세 가지(`Tools/Hooks/claude_guard.py`: 이유 없는 전체 Play 차단, 같은 주제 3번째 Codex 검토 차단, 긴 대화 알림, 2026-11-02 사용자 승인)는 그대로 돈다. 2026-11-15부터 studio-kit hook(`.claude/hooks/`: git 보호, 규칙 주입, 검토 판정 기록, 새 파일 검증 등)이 함께 돈다.
- Git pre-commit 연결(`core.hooksPath=.githooks` → `Tools/Verify/gate.py pre-commit`)은 2026-11-15에 끊었다. 사용자 결정 "2D에선 끄기 (추천)": 이 gate는 예전 3D 장면의 전체 Play(`all --screens`)와 모든 파일 스테이징을 요구해 2D 커밋을 매번 막았다. 파일(`.githooks/`, `Tools/Verify/`)은 그대로 두며, 예전 3D 작업을 다시 할 때만 `git config core.hooksPath .githooks`로 다시 잇는다. 2D 확인은 컴파일·EditMode 테스트·`tools/playable.py`.

<!-- studio-kit:project-start -->
## Studio Kit project preflight

Before implementation, at a new session, and whenever the task changes from analysis to design,
Unity or server work, run from this project root:

`python3 .ai/studio-kit/project-doctor.py --root . --host claude`

Add `--require unity-client.implement`, `--require server.implement` or `--require design.write`
for the next stage, even if its directory has not been created yet. This read-only check needs
only python3 (its YAML and schema libraries are bundled in .ai/studio-kit/lib).
Exit 1 means setup is incomplete; exit 2 means the audit could not run. Neither is a pass.
Resolve setup gaps before implementation, preserving existing rules and architecture.
Use director-run for multi-stage feature work; keep stage state and handoffs even when executing
manually. Do not claim a skill, hook, review or runtime check ran based on installed files alone.
Claude hooks do not enforce Codex sessions: run preflight and verification explicitly there.
If the director/required skills are unavailable, report that limitation and establish an explicit
manual plan and stage records; never silently substitute direct implementation for the workflow.
Small edits and read-only questions do not require a new director run.
<!-- studio-kit:project-end -->

## Overview
데드 서바이버 — 2D 파티 생존 전투. 영웅 최대 4명이 진형을 짜고 10개 웨이브의 몬스터를 버티며, 웨이브 사이 정비에서 장비를 얻어 배치·합성한다. Goal: ship a complete playable game.
플랫폼: 모바일 AOS/iOS. 서버 없음(기기 안 저장). 기획 원본: 레포 루트의 `docs/` (기능별 기획서는 `docs/features/<기능>/plan.md`)

## Stack
- Unity 6000.4.3f1 C#
- VContainer (DI)
- R3 (reactive state — 읽기 `.CurrentValue`, 노출 `ReadOnlyReactiveProperty<T>`)
- UniTask (async; Coroutine only inside MonoBehaviour)

## Architecture (project-specific)

<!-- 프로젝트 고유 아키텍처를 여기 기술. 참고 패턴:
     - gameflow-state-machine: GameFlowService(IStartable·ITickable) + BaseGameState + RequestStateChange 콜백
     - di-reactive-stack: 씬당 LifetimeScope 1개, Lifetime.Scoped 등록 -->

- 렌더 파이프라인·카메라·입력 방식: URP 17.4, Input System 1.19(프로젝트에 이미 있음). 2D 카메라·시점은 2D 기획 확정 후 기술
- 핵심 루프: (2D 게임 기획서 확정 후 한 줄 요약. 예전 판: 로비 → 출전 → 10웨이브 전투, 웨이브 사이 정비·보상 → 승리·패배 → 재편성)

## Core Infrastructure (규칙이 전제하는 것)
아래는 `.ai/rules`와 검증 에이전트가 있다고 가정하는 기반 코드다. 없으면 기능 작업 전에 `Core/`·`App/`에 먼저 만든다.
- 씬별 `LifetimeScope` (VContainer 등록 지점, 예: `GameSceneLifetimeScope`) — `di-patterns.md`
- `EventBus` + `GameEvent` 정의 — `event-bus-rules.md`
- `PoolManager` — `creation-patterns.md`
- `GameLogger` + `Const.IsLogEnabled` — `error-handling.md`

## Folder Rules
Base folders: `Assets/Game/01.Scene`, `Assets/Game/02.Scripts`, `Assets/Game/03.Resources`

Top-level under `Assets/Game/02.Scripts/`:
- `App/` — 부팅·LifetimeScope·Scene 전환
- `Core/` — Cross-cutting infra (Const, Event, Network, Resource, UI, Utils, ...)
- `Features/` — 게임 로직 도메인
- `UI/` — Scene-level UI orchestrator. 도메인 popup은 `Features/<도메인>/Presenter/` + `View/`
- `Effect/` — 연출·애니메이션
- `Editor/` — 에디터 도구

Tests: `Assets/Game/Tests/EditMode/`(에디터 테스트 어셈블리), `Assets/Game/Tests/PlayMode/`(필요할 때). 순수 로직은 EditMode 테스트로 둔다.

Within each feature folder: classify by responsibility (View · Model · Service · Presenter · Controller · Factory · Registry · Event · Interface · Data · Utils · GameState).

Create missing folders if needed. Always specify target file path.

2D 스프라이트 원본은 `Assets/Game/03.Resources/Art/`(정책·임포트 예외는 그 폴더의 `CLAUDE.md`), 아트 문서는 `docs/art/`(기준서 `아트-기준서.md`, 레퍼런스 `reference/`).

## Git Workflow (brief)
- Branch: `main` ← `epic/{name}` ← `claude/{name}` (working branch)
- PR target: `claude/{name}` → `epic/{name}` (match by suffix); merge immediately after creation
- `epic → main` merge requires explicit user instruction
- Stage only specific files — never `git add .` (enforced by hook)

## Editor Readiness
- 에디터 확인·되살리기: 레포 루트에서 `python3 -B ./tools/editor_ready.py` (꺼졌거나 멈췄으면 다시 열고, 컴파일 오류가 0개인지 본다. Unity CLI 필요)
- 대기열(`queue.yaml`)의 검사가 에디터에 기대면 이 명령을 `ready`에 적는다. 닫을 때 저장하지 않은 씬 변경은 버려진다.

## 모델·추론 강도와 리뷰 담당 (2026-11-15 사용자 결정)

사용자 원문: "디렉터를 제외하고는 웬만하면 medium으로 설정을 변경해주고 앞으로도 그렇게 해줘", "리뷰 담당을 codex로 변경을해줘", "codex 모델은 gpt 6.1 sol로 변경해줘".

- 디렉터(사람과 대화하는 세션)만 사용자 설정(현재 Opus 5.5 xhigh) 그대로다. 그 밖은 모두 추론 medium.
- 2026-11-17 사용자 "서브에이전트도 동일하게 opus5.5 meduem으로": 단계 세션·대기열 항목은 `Tools/claude_medium.sh`(= `claude --model claude-opus-5-5 --effort medium`, 세션 안 하위 에이전트도 `CLAUDE_CODE_SUBAGENT_MODEL=claude-opus-5-5`), 리뷰 중계 4종 `model: opus`. 디렉터가 직접 부르는 하위 에이전트도 `model: opus`, `effort: medium`.
- Claude 단계 세션: `claude -p ... --effort medium`. 대기열 러너는 `workqueue.py run ... --claude "$PWD/Tools/claude_medium.sh"`(Codex 실행기의 리뷰 세션도 이 값을 쓴다). 하위 에이전트 정의에는 `effort: medium`.
- Codex는 어디서 부르든(이미지 생성, 검토, 구현 실행기) 모델 `gpt-6.2-astra`, 추론 medium: `codex exec -m gpt-6.2-astra -c 'model_reasoning_effort="medium"' ...`. 실행기는 `codex-step.py --model gpt-6.2-astra --effort medium`, 대기열은 `--model gpt-6.2-astra --effort medium`. `~/.codex/config.toml`(다른 프로젝트와 같이 쓰는 설정)은 바꾸지 않았다.
- **리뷰 담당 4종(design-reviewer, spec-reviewer, validator-client, algorithm-tester)은 Codex가 한다.** `.claude/agents/<이름>.md`는 받은 브리핑을 `Tools/codex_review.py`로 Codex에 넘기고 답을 그대로 돌려주는 중계다(Sonnet, medium). Codex가 따르는 역할 지침은 `.ai/reviewers/<이름>.md`(원래 에이전트 본문). 판정 기록 훅은 중계 답의 `VERDICT:` 줄을 그대로 기록한다. studio-kit을 다시 깔거나 올리면 `.claude/agents/`가 덮어써질 수 있으니 이 절대로 되돌린다.
- 리뷰가 이미 다른 모델 계열(Codex)이므로 디렉터의 "다른 모델 계열 마감 검증"(director-run 5절 3-2)은 따로 돌리지 않는다.
- **2026-11-23 사용자 "6.2로 변경을해주고 너가판단했을떄 cluade opus 5.5가 더 잘할것 같은 부분은 그쪽에게 맡기도록하고 문서로 남겨줘"**: Codex 모델은 `gpt-6.2-astra`(그 전 `gpt-6.1-sol`). 누가 무엇을 맡는지의 원본은 [모델 역할 분담](docs/workflows/모델-역할-분담.md)이다. 요점: Codex가 만든 것의 마감 리뷰(spec-reviewer·validator-client·design-reviewer)는 Opus 5.5가 직접 한다(리뷰 에이전트가 `DIRECTOR_MADE_BY=codex`를 보고 가른다. algorithm-tester는 늘 Codex). 기획서·기준서, 화면 판독(두 모델), 눈으로 맞추는 화면 품질 단계, 사용자에게 보이는 글은 Opus 5.5 몫이다. 위 줄들의 "리뷰 4종은 Codex"는 Claude가 만든 것에만 해당한다.
- **구현·생산은 Codex가 한다(2026-11-17 사용자 "너 구현할떄 이제 codex를 최대한 활용해줘").** 디렉터가 띄우는 구현 단계는 `codex-step.py --model gpt-6.2-astra --effort medium`(director-run `references/runner.md` "Codex로 실행하는 단계"), 대기열은 `workqueue.py run ... --executor codex --model gpt-6.2-astra --effort medium`(Aether 항목도 — Codex에 Aether MCP가 연결돼 있다). 리뷰 세션·기획·아트 방향 결정은 그대로 Claude(리뷰 4종은 Codex 중계).

## Subagent Routing

원칙: **읽기는 분산, 쓰기는 집중.** 구현·수정(쓰기)은 메인 에이전트가 단일 컨텍스트에서 직접 수행한다. 서브에이전트는 조사(읽기)와 검증(fresh context)에만 사용한다.

### Main (default)
- 계획·구현·버그 수정·리뷰 지적사항 반영은 전부 메인이 직접. 구현 컨텍스트를 가진 쪽이 수정한다.
- 접근이 불확실한 작업은 plan mode로 탐색/계획을 분리한 뒤 구현.

### Explore (조사 위임)
- 광범위한 코드베이스 조사·다방향 검색이 필요할 때. 요약만 메인 컨텍스트로 돌아온다.

### validator-client (검증 위임 — fresh context 적대 리뷰)
- 아키텍처·DI 등록·레이어 계약에 영향 주는 변경 후 호출. diff를 요구사항 대비 검증 + VContainer 순환 의존 추적.
- 기능 하나에 마감 리뷰 한 번: 구현·명령 관문이 끝난 뒤 `spec-reviewer`·`algorithm-tester`(해당 시)와 한꺼번에 병렬로 부른다. 지적을 모아 메인이 고치고, FAIL이었던 리뷰만 수정 확인. 그래도 남으면 묻지 않고 고쳐서 다시 확인한다(`AGENTS.md` "판정에 못 미치면 다시 돈다", 2026-11-17 사용자 지정). 단 같은 지적의 수정이 2회 실패하면 멈추고 사용자에게 보인다(같은 절 "반복 상한", 2026-11-18). 순수 리네이밍·로컬 편집은 생략.

### design-reviewer (구현 전 설계 검토 위임)
- 구조가 크게 바뀌는 L2에서만 설계 브리핑을 쓴 뒤 호출: 첫 기능(Core 기반·DI 뼈대·판정 진입점), 이미 있는 시스템의 상태 소유권·DI 구조·세이브 형식 변경, 렌더 파이프라인 설정 변경. 새 도메인 폴더를 더하고 공유 파일에 등록 줄·칸만 더하는 기능은 부르지 않는다(짧은 브리핑을 마감 리뷰에 넘긴다). 규모 분류는 `.ai/rules/work-levels.md`.
- 필수 지적을 해소한 뒤 구현한다. 수정은 메인이 직접 수행.

### algorithm-tester (실행 검증)
- **알고리즘 코드**(loop·branch·state machine·generator·shuffle·sort·packing) 변경 후 마감 리뷰 묶음 안에서 호출.
- 순수 C# 로직은 **실제 실행으로 검증** (standalone 추출 → 실행 → 3-5 케이스 결과 비교). Unity API에 얽힌 코드만 hand-trace 폴백.
- FAIL → 메인이 수정 → tester 수정 확인 한 번. 사용자 보고 시 케이스별 결과 제시.
- **Skip** for View/DI-only/rename/comment changes.

### 대량 기계 작업 (예외적 쓰기 위임)
- 독립적·기계적 반복 작업(수십 파일 일괄 마이그레이션 등)만 병렬 위임 (파일 충돌 시 worktree 격리). 설계 판단이 필요한 구현은 위임하지 않는다.

### 기능은 하나씩
- 기능 하나의 구현(코드·테스트)과 에디터 확인(컴파일·EditMode·Play·판정 경로·캡처)은 레포 루트의 한 실행에서 끝낸다. 에디터가 하나라 기능을 나란히 구현하지 않는다(worktree에서 코드만 쓰고 에디터 확인을 미뤄 합치지 않는다). 나란히 돌려도 되는 것은 기획서와 그림 생산처럼 에디터를 쓰지 않는 일뿐이다.

### Release audit
- 릴리즈 전 pre-build 체크리스트(빌드 설정, 광고·결제 키, 로그 비활성화, 버전 코드 등)를 병렬 에이전트로 나눠 검증하는 것을 권장한다.

## Output Rules
On every implementation/modification, provide:
1. 변경 파일 경로 목록
2. 무엇을 왜 바꿨는지 핵심 요약 (전체 코드 재출력 금지 — diff는 도구가 보여준다)
3. 실행한 검증 결과 (테스트·케이스 비교 등)

Keep it short and concrete.

---

## Shared Rules (always loaded)

@.ai/rules/code-style.md
@.ai/rules/error-handling.md
@.ai/rules/scope-discipline.md
@.ai/rules/verify-existing-code.md
@.ai/rules/test-before-claim.md
@.ai/rules/art-core-rules.md

> Context-specific rules (Architecture / DI / Reactive / Creation / EventBus / Data-driven content / Git workflow / Art / 2D) are injected on demand by `.claude/hooks/inject-context-rules.py` when the prompt mentions related keywords.
