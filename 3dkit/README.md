# 3dkit

AI 에이전트(Claude·Codex)가 Unity 프로젝트에서 **원하는 3D 자산을 만들고 게임 화면에서 확인하는** 공통 워크플로우와 도구 모음이다. 어느 프로젝트에든 넣어 설정 파일 하나만 바꿔 쓰는 것이 목표다.

- 출처: 개인 프로젝트 Deadsurvivor(Unity 6000.4 URP, 로우폴리 3D, Tripo·Codex로 만든 AI 미술)에서 2026-10-29~11-03에 실제로 쓰며 다듬은 규칙과 도구.
- **단계: 디벨롭(개발 중).** 사용자(2026-11-03): "따라서 지금은 아직 3dkit은 디벨롭 단계야". 완성 판단은 사용자가 한다.
- 상태(2026-11-03, v0): **문서와 참고 구현만 있다.** 실행 도구(`tools/`)와 Unity 패키지(`unity/`)는 아직 없고, 첫 사용 프로젝트에서 [하네스 설계도](workflows/harness-blueprint.md) 단계(H0~H4)대로 만든다. 두 번째 프로젝트에서 설정만 바꿔 돌아가야 "범용"이라고 부른다.

## 구성

| 폴더 | 내용 |
|---|---|
| `workflows/` | [핵심 규칙](workflows/core-rules.md)(만드는 일 먼저, 검증 3단계, 검토 상한, 단계별 새 세션), [개발 워크플로우](workflows/development.md)(L0/L1/L2), [하네스 설계도](workflows/harness-blueprint.md), [에셋 규격](workflows/asset-standard.md), [Tripo 생성 기준](workflows/tripo-generation-standard.md), [성능 절차](workflows/performance.md), [빌더 규칙](workflows/scene-builders.md), [시각 설계 절차](workflows/reference-design.md), [도구 연결](workflows/tools.md), 독립 검토 역할 계약 3개 |
| `lessons/` | [교훈](lessons/교훈-이식.md): 이전 프로젝트에서 실제로 겪은 실패 → 원인 → 기본 규칙 |
| `templates/` | 인수인계·결정·검증·설계 양식, `project/`(새 프로젝트 루트에 복사할 AGENTS·CLAUDE·현재 상태·설정·검토 역할 틀) |
| `reference/` | 이전 프로젝트의 실제 도구 코드(실행용 아님). [안내](reference/README.md) |
| `tools/` *(예정)* | Python 도구: `guard`(Claude hook), `probe`(측정 Play), `protect`, `session_time`, `play_check`, `tier`, `assets`(에셋 정식 경로), `gate` |
| `unity/` *(예정)* | Unity 쪽 Editor C#을 담은 로컬 UPM 패키지(측정 클래스, 에셋 가져오기) |

## 프로젝트에 넣기

완성 전(v0~v1)에는 폴더를 복사해서 쓴다. 최종 형태는 아래 로드맵의 하나의 에이전트다.

```bash
cd <프로젝트 루트>
cp -R <3dkit 폴더> 3dkit
cp 3dkit/templates/project/AGENTS.md 3dkit/templates/project/CLAUDE.md .
mkdir -p .claude docs && cp -R 3dkit/templates/project/.claude/agents .claude/
cp 3dkit/templates/project/현재상태.md docs/
cp 3dkit/templates/project/3dkit.config.json .
```

그다음 `AGENTS.md`의 `<…>` 칸(이름·경로·원격·저장 경계)과 `3dkit.config.json`을 채운다. 에이전트에게 "CLAUDE.md를 읽고 하네스 설계도 H0부터 진행해줘"라고 하면 된다.

## 로드맵

사용자 목표(2026-11-03): "어익후 올린다는 이야기를 한건 아냐야, 우리가 만든 3d kit을 완성시킨다음에 하나이ㅡ 에이전트로 이식시키는게 목표야"

| 단계 | 내용 | 완료 조건 |
|---|---|---|
| v0 (지금) | 문서·교훈·양식·참고 구현 | — |
| v1 키트 완성 | [하네스 설계도](workflows/harness-blueprint.md) H0~H3을 `tools/`·`unity/`에 실제로 구현. 중심은 에셋 정식 경로(생성 → 감량 → 가져오기 → 규격 대조 → 캡처) | 한 프로젝트에서 에셋 1종이 처음부터 끝까지 도구로 만들어짐 |
| v2 하나의 에이전트 | 워크플로우·도구·hook을 에이전트 하나로 묶음(형태는 그때 정함. 후보: Claude Code 플러그인 = 스킬 + 에이전트 정의 + hook + 도구, Codex용 AGENTS·스킬) | 에이전트에게 "이런 3D 자산을 만들어줘"라고 하면 승인 지점에서만 사람에게 묻고 끝까지 감 |
| v3 이식 검증 | 다른 프로젝트(시험용이라도)에 넣어 설정만 바꿔 동작 | 두 번째 프로젝트에서 같은 결과 |
| 그다음 | 3dkit을 studio-kit(`git@github.com:jaydentapwave/studio-kit.git`)에 반영 | 사용자가 3dkit 완성을 판단할 때 |
| 아주 나중 | studio-kit으로 새 프로젝트에서 게임 제작 | — |

사용자 정리(2026-11-03): "우선 우리가 새롭게 만든 키트를 3dkit이라고 한다면, 이 3dkit을 완성이 되었다고 판단될떄쯤, studio=kit에 업데이트를 할거야, 그러면 아주 나중에 stuio-kit을 활용해서 새로운 프로젝트에서 게임을 제작할거야"

## 규칙

- 3dkit의 원본은 한 곳이다. 완성 전에는 키트를 완성하는 프로젝트 안의 `3dkit/` 폴더, 저장소로 옮긴 뒤에는 그 저장소다. 여러 곳에 복사해 따로 고치지 않는다.
- 3dkit 문서는 특정 프로젝트의 경로·이름·저장 경계를 담지 않는다. 그런 값은 각 프로젝트의 `AGENTS.md`와 설정 파일에 둔다.
- 비밀 값(API 키 등)은 넣지 않는다. 키는 각 프로젝트의 `.env.local`(Git 제외)에 둔다.
