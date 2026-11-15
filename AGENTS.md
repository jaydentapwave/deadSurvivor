# 개인 프로젝트 작업 지침

## 저장 경계

- 이 프로젝트는 개인 사이드 프로젝트다. 문서·계획·결정·검증 결과는 현재 프로젝트 폴더 안에만 기록한다.
- 옵시디언/vault, 노션, tapwave-hub 및 회사 문서·이슈·메신저·공용 저장소에 이 프로젝트의 내용, 링크, 요약, 작업 흔적을 남기지 않는다. 해당 서비스를 조회하거나 연동하지 않는다.
- 외부 게시, 공유 링크 생성, 회사 원격 저장소 등록, 회사 패키지/배포/기록 자동화 연결을 하지 않는다.
- 사용자 지정 예외(2026-10-31): `https://github.com/jaydentapwave/deadSurvivor.git`를 이 프로젝트의 Git 원격으로 등록하고 현재 작업을 커밋·업로드한다. 지정 저장소는 공개 저장소다. 이 예외는 해당 Git 저장소에 한정하며 회사 서비스 연동이나 다른 외부 게시를 허용하지 않는다.
- walter_idle는 구조를 읽기 전용으로 참고한 대상이다. 그 프로젝트의 규칙은 여기의 실행 지침이 아니다. 회사 연동 훅·스킬·설정을 그대로 복사하거나 실행하지 않는다. 독립적으로 재작성한 로컬 워크플로우만 사용한다.
- 전역 Codex/Claude 설정을 변경하지 않는다. 개인 프로젝트 규칙은 여기서만 유지한다.
- 실제 Unity 작업 대상은 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`다. 이 경로 밖의 회사 프로젝트에 변경을 가하지 않는다. 현재 환경의 쓰기 권한은 별도로 준수한다.
- 사용자 요청(2026-10-31)으로 Git 루트도 위 Unity 프로젝트 경로로 통일했다. 실제 게임 코드/콘텐츠의 단일 원본은 `Assets/DeadSurvivor`다(예전 3D 판까지). **2026-11-15 2D 전환부터 새 코드·장면·리소스의 원본은 `Assets/Game`이고, `Assets/DeadSurvivor`는 시스템을 옮겨 오기 위한 참고용으로 고치지 않는다**(사용자: "이전에 만든것들은 그냥 냅두고 시스템만 가져갈거야"). `UnityMigration`은 독립 검증 템플릿·원본 아트/라이선스만 보관하며 과거 Runtime/Editor/콘텐츠 사본을 덮어쓰지 않는다. 기존 Documents 경로는 파일 복사 없이 같은 작업 폴더를 가리키는 호환 진입점이다. 일반 symlink 경로 검사를 완화하지 않는다.
- 이 지침은 에이전트의 작업 범위를 제한한다. 운영체제 백업·앱 자체 기록까지 차단하는 보안 장치를 뜻하지 않는다.

## 작업 절차

- 먼저 [핵심 규칙](docs/workflows/core-rules.md)을 읽는다(검증 3단계·검토 상한·단계별 세션·hook, 2026-11-02 사용자 요청).
- README → 현재 상태 → 해당 기획과 코드 → 필요한 검증 순서로 읽는다.
- 할일.md의 확정 방향, 첫 구현 기준안, 미정 수치를 구분한다. 제안이나 테스트 통과 기록을 구현 완료로 승격하지 않는다.
- 문서만 변경하면 링크·일관성을 검사한다. 런타임 변경은 컴파일·규칙 검사, UI/씬 변경은 실제 프레임·입력·캡처도 확인한다.
- Unity 조작에는 unity-cli 스킬을 읽고 대상 프로젝트를 확인한다.
- 기존 작업물을 덮어쓰지 않는다. 두 폴더의 차이를 확인하기 전 자동 동기화를 실행하지 않는다.
- 검증 결과는 PASS/FAIL/BLOCKED/NOT_RUN을 구분하며 실행 시각·소스 지문·근거를 남긴다. 과거 로그를 현재 성공으로 사용하지 않는다.
- 하네스 미구현 또는 실행 불가를 성공으로 보고하지 않는다. 회사 프로젝트의 검증 스크립트를 가져다 실행하지 않는다.

## 작업 방식 선호 — 에이전트 공통

Codex·Claude 등 어느 에이전트를 쓰든 이 절이 원본이다. 에이전트별 메모리에 따로 두지 않는다(2026-11-02 사용자 요청). 근거는 [040 인수인계](docs/design/agent-handover-040.md).

- 사용자에게 하는 답변·보고는 한국어로 쓴다(2026-11-02 사용자 요청). 코드 식별자·파일 경로·명령은 원문 그대로 둔다.
- 단계마다 놓친 것(검토자나 사용자가 찾았거나 뒤늦게 발견한 것)을 그 작업의 검증 기록에 원인과 "워크플로우 갱신 후보"와 함께 남긴다. 작업이 끝나면 후보를 워크플로우 문서에 반영하고 반영 여부를 표시한다(2026-11-02 사용자 요청).
- 전체 작업 시간과 단계·활동별 세부 시간, 토큰 사용량(Claude·Codex·하위 에이전트)을 계속 기록한다. 시간이 오래 걸리면서 효율이 낮은 곳과 토큰을 많이 쓴 곳을 찾아 개선 후보로 남기고, 워크플로우 갱신 때 함께 반영한다(2026-11-02 사용자 요청). 집계 도구는 `Tools/Harness/session_time.py --markdown`(사용자 요청 단위, 2026-11-03)이다. 045의 단계별 기록은 [시간·토큰 기록](docs/reviews/keyart-target-045-time.md)과 `Tools/Proto/session_time_045.py`에 남아 있다.
- 긴 작업은 중간에 진행 상황을 공유한다. 실행 중·완료·중단·대기를 구분해 말하고, 끝난 작업을 계속 진행 중인 것처럼 표현하지 않는다.
- 평가는 냉정하고 솔직하게 한다. 부족한 점을 성과 표현으로 덮지 않는다.
- 세부를 다듬기 전에 전체 느낌을 먼저 확인할 수 있게 진행한다.
- 워크플로우·검사 항목은 다시 넣을 필요가 없다고 확실할 때만 뺀다.
- 검사를 추가할 때 뺄 수 있는 검사도 함께 적는다. 비용이 큰데 결함을 한 번도 잡지 못한 검사는 단계 회고에서 빼기 후보로 사용자에게 제시한다(2026-11-02 사용자 요청).
- 사용자는 여러 에이전트를 동시에 쓴다. 작업 시작과 커밋 전에 Git 상태와 최근 커밋을 확인하고, 다른 세션의 변경을 덮거나 같은 파일을 동시에 고치지 않는다.
- 서브에이전트 모델·effort는 메인이 작업 성격에 맞게 정한다(2026-11-02 위임). 사용자가 지정하면 그 지정을 따른다.
- **2026-11-15 사용자 지정**("디렉터를 제외하고는 웬만하면 medium으로", "리뷰 담당을 codex로", "codex 모델은 gpt 6.1 sol로"): 사람과 대화하는 디렉터 세션 말고는 모두 추론 medium. Codex는 어디서 부르든 `-m gpt-6.1-sol -c 'model_reasoning_effort="medium"'`. 리뷰 담당 4종은 Codex(`Tools/codex_review.py`, 역할 지침 `.ai/reviewers/`). 세부는 `CLAUDE.md` "모델·추론 강도와 리뷰 담당".
- 독립 검토는 가능하면 만든 쪽과 다른 모델 계열에 맡긴다. 예: GPT가 생성한 이미지는 Claude가, Claude가 바꾼 문서·코드는 Codex가 검토한다. 쓸 수 없으면 같은 계열의 독립 서브에이전트로 검토하고 그 사실을 기록한다.
- 도구 위치·명령·에이전트별 차이는 [개발 환경의 에이전트 도구 연결](docs/개발환경.md#에이전트-도구-연결)을 따른다.

## 워크플로우 라우팅

### 2D 전환 작업 (2026-11-15~, studio-kit)

새 2D 작업은 studio-kit 규칙을 따른다. Codex 세션은 Claude 훅의 키워드 주입을 받지 않으므로 이 표에서 규칙을 찾는다.

| 작업 | 규칙·절차 |
|---|---|
| 게임 기능 구현·수정 | 루트 [CLAUDE.md](CLAUDE.md)의 Stack·Folder Rules, `.ai/rules/`(상시: code-style·error-handling·scope-discipline·verify-existing-code·test-before-claim), 작업 규모 `.ai/rules/work-levels.md`, 플레이북 `/director-game`·`/director-run` → `/unity-feature` |
| 한 판 판정 | `.ai/rules/playable-check.md`, `Tools/playable.py`, 에디터 확인 `Tools/editor_ready.py` |
| 모든 아트 작업의 우선 규칙 | `.ai/rules/art-core-rules.md` |
| 아트 작업 전체 | `/art-reference`(방향이 안 정해졌으면 `/art-direction`) → `/art-spec` → `/2d-art-produce` |
| 아트 결정 원본 | `docs/art/아트-기준서.md`(아직 없음, `/art-spec`에서 만든다) |
| 아트 질문 정책 | `.ai/rules/art-decisions.md` |
| 레퍼런스 이미지 기반 설계 | `.ai/rules/art-reference-design.md` |
| 아트 도구(Codex 이미지 생성·검수) | `.ai/rules/art-tools.md` |
| 2D 그림 생성·변형 | `.ai/rules/2d-generation.md`, 플레이북 `/2d-art-produce` |
| 2D 애니메이션 | `.ai/rules/2d-animation.md` |
| 스프라이트 임포트·폴더·이름 | `.ai/rules/2d-asset-standard.md`, `Assets/Game/03.Resources/Art/CLAUDE.md` |
| 사용자에게 그림 보이기 | `.ai/2d-art/tools/sheet.py`만(검증된 그림만 들어간다) |

- 2D 아트 팩에는 Codex 입력 닫기 훅(3d-art의 `guard.py`)이 없다. Codex를 부를 때는 `< /dev/null`과 시간 상한을 직접 붙인다.
- 아래 절과 다음 "검사 누락 방지" 절은 예전 3D 작업(`Assets/DeadSurvivor`)의 절차다. 2D 작업에는 [핵심 규칙](docs/workflows/core-rules.md)의 0·3·4·5·6절만 적용한다.

### 예전 3D 작업

맵 산출물은 [일관성 규격](docs/workflows/map-artifact-contract.md), 검증 설계는 [맵 하네스 명세](docs/design/맵-워크플로우-하네스-설계.md)를 따른다. 현재 맵 하네스는 미구현이며 문서 검사 PASS를 맵 동작 PASS로 보고하지 않는다. 2026-11-02부터 맵 하네스 작업은 보류이고, 콘텐츠 작업은 [핵심 규칙 0절](docs/workflows/core-rules.md)(만드는 일 우선, 검증 최소)을 따른다. 현재 게임 맵은 [046 전체 맵](docs/design/map-full-046.md)이다.

레퍼런스 기반 이미지·맵 설계는 [시각 설계 절차](docs/workflows/reference-design.md)를 따른다. 사용자 요구·관찰·에이전트 제안을 구분하고 실제 원본 입력과 결과 비교를 기록한다.

[개발 워크플로우](docs/workflows/development.md)가 작업 규모별 절차의 원본이다. L1/L2에서는 해당 문서가 지정한 독립 검토 서브에이전트를 사용한다. 구현은 메인이 담당한다. 역할 계약은 docs/workflows에 있으며 회사 프로젝트 규칙은 적용하지 않는다.

프로젝트 실행 스킬: [ds-workflow](.agents/skills/ds-workflow/SKILL.md). 로컬 검사: `python3 Tools/Verify/run.py local`. 이 검사는 Unity 컴파일·플레이 검증을 대체하지 않는다.

## 검사 누락 방지

**2D 작업(`Assets/Game/`)**: 2026-11-15 사용자 결정 "2D에선 끄기 (추천)"으로 Git pre-commit 연결(`core.hooksPath`)을 끊었다. 2D 변경은 이 gate 대신 컴파일·EditMode 테스트·`tools/playable.py`로 확인한다. 아래는 예전 3D 작업(`Assets/DeadSurvivor`)에만 적용한다.

[프로젝트 검증 gate](docs/workflows/verification-gates.md)를 적용한다. 변경 작업 최종 확인은 `/usr/bin/python3 Tools/Verify/gate.py check`로 수행한다. Git pre-commit과 Codex Stop 정의가 같은 gate를 사용하며, Codex 자동 실행에는 hook 신뢰가 필요하다. 실패를 피하려고 hook/기준을 비활성화하거나 baseline을 자동 갱신하지 않는다. gate의 문서/준비 자료 PASS를 실제 Unity·맵 검증 PASS로 해석하지 않는다. Claude 효율 hook(이유 없는 전체 Play·같은 주제 3번째 검토 차단, 긴 대화 알림)은 [핵심 규칙](docs/workflows/core-rules.md)을 따른다.

<!-- studio-kit:project-start -->
## Studio Kit project preflight

Before implementation, at a new session, and whenever the task changes from analysis to design,
Unity or server work, run from this project root:

`python3 .ai/studio-kit/project-doctor.py --root . --host codex`

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
