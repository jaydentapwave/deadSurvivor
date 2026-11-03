# 하네스 설계도 — 새로 짓되, 증명된 부품만

3dkit의 검증·측정·에셋 도구를 무엇을, 언제, 어떤 순서로 만들지 정한다. 이전 프로젝트 Deadsurvivor의 하네스를 그대로 복사하지 않고 다시 짓는다. 참고 구현은 [reference/](../reference/README.md)에 있다(경로·이름이 이전 프로젝트에 묶여 있어 그대로는 실행되지 않는다). 2026-11-03 작성.

## 목표

3dkit을 어느 Unity 프로젝트에 넣어도 같은 방식으로 **원하는 3D 자산을 만들고**(생성 → 처리 → Unity 가져오기 → 규격 대조 → 캡처), 그 자산이 게임 화면에서 어떻게 보이고 얼마나 무거운지 잴 수 있게 한다. 프로젝트마다 다른 값은 설정 파일 하나에만 둔다.

## 왜 다시 짓는가 (이전 프로젝트에서 확인한 사실)

- 커밋 gate가 검사하던 장면은 옛 2D 장면이었고, 실제 게임은 3D 장면이었다. 검사 대상과 게임이 어긋났다.
- 도구 코드 약 1.97만 줄 중 옛 gate·보류된 맵 하네스(`Tools/Verify`)가 7,902줄, 매일 쓰던 `Tools/Harness`는 885줄이었다.
- 경로·이름이 코드에 박혀 있었다(프로젝트 절대 경로, 게임 C# 타입 이름, 장면 이름, 실행기 이름).
- 반대로 짧은 Play, 측정 도구(`play_probe`), 부작용 파일 되돌리기(`protect`), Claude 효율 hook, 에셋 정식 경로(`assets.py`)는 마지막 단계들(046~050)에서 매번 쓰였다. 짧은 Play와 Play 중 방향 측정은 실제 게임 결함을 잡았다(빌더 재실행 뒤 장면 참조 누락 046-12, 영웅이 이동 방향과 68° 어긋남 S3-21). 이것들을 옮긴다. 자세한 근거는 [교훈](../lessons/교훈-이식.md).

## 원칙

1. **콘텐츠와 함께 키운다.** 아래 단계는 그 단계가 필요한 콘텐츠가 생길 때 만든다. 장면이 없는데 Play 실행기를 만들지 않는다.
2. **core와 프로젝트 설정을 나눈다.** 3dkit 코드는 프로젝트 이름·경로를 모른다. 프로젝트 값은 프로젝트 루트의 `3dkit.config.json` 하나에만 둔다.
3. **3dkit의 원본은 한 곳.** 완성 전에는 키트를 완성하는 프로젝트 안의 `3dkit/` 폴더, 하나의 에이전트로 묶어 저장소로 옮긴 뒤에는 그 저장소다. 여러 곳에 복사해 따로 고치지 않는다.
4. **도구마다 `selftest`가 있다.** Unity 없이 인자·판정 로직을 확인한다.
5. **출력은 짧게.** 한 줄 JSON 요약 + 프로젝트의 `output/<도구>/<시각>-<id>/` 원자료.
6. **측정 도구는 판정하지 않는다.** 판정은 짧은/전체 Play와 gate만 한다.
7. **검사를 더할 때 뺄 것도 적는다**([핵심 규칙](core-rules.md) 5절).

## 배치 (프로젝트 안에서)

```
<프로젝트 루트>/
├── 3dkit/                  ← 키트(완성 전에는 폴더 복사, 완성·에이전트화 뒤 형태를 다시 정함)
│   ├── workflows/ lessons/ templates/ reference/
│   ├── tools/                   ← H0~H4에서 만드는 Python 도구 (아직 없음)
│   └── unity/                   ← Unity 쪽 Editor C#을 담는 로컬 UPM 패키지 (아직 없음)
├── 3dkit.config.json       ← 프로젝트별 값 (유일)
├── AGENTS.md  CLAUDE.md  .claude/
├── docs/                        ← 프로젝트 문서 (현재 상태, 계획, 기획)
└── Assets/  Packages/  ProjectSettings/
```

- Unity 쪽 코드(측정 클래스, 에셋 가져오기 `AssetBuild`)는 `3dkit/unity/`를 로컬 패키지로 둔다. 프로젝트의 `Packages/manifest.json`에 `"com.studiokit.editor": "file:../3dkit/unity"`처럼 연결한다(패키지 이름은 H1에서 정함). 이렇게 하면 `Assets/`에 복사하지 않아도 어느 프로젝트에서든 같은 코드를 쓴다.
- 명령은 프로젝트 루트에서 `python3 3dkit/tools/<도구>.py ...`로 실행한다. 도구는 현재 폴더에서 위로 올라가며 `3dkit.config.json`을 찾는다.

## 프로젝트 설정 파일 (초안)

`3dkit.config.json` 예시(숫자·목록은 예시이며 각 단계에서 정한다). 틀은 [templates/project/](../templates/project/3dkit.config.json).

```json
{
  "project_root": ".",
  "unity_version": "6000.4.3f1",
  "main_scene": "Assets/<게임>/Scenes/<첫 장면>.unity",
  "game_namespace": "<C# 네임스페이스>",
  "art_root": "Assets/<게임>/Art",
  "asset_catalog": "docs/art/asset-catalog.json",
  "side_effect_files": ["<Play·캡처 뒤 바뀌는 파일, 예: 동적 글꼴 캐시>"],
  "full_runners": [{"match": "3dkit/tools/play_check.py", "full_if": "PLAY_STAGE=full"}],
  "review": {"max_rounds_per_topic": 2},
  "context": {"warn_tokens": 250000, "new_session_tokens": 300000, "step_tokens": 100000},
  "evidence": {"gameplay": ["Assets/<게임>/Runtime/**", "**/*.unity", "ProjectSettings/**"],
               "visual": ["**/*.png", "**/*.mat", "**/*.shader", "**/*.shadergraph", "**/*.fbx", "**/*.prefab", "**/*.anim"]},
  "tripo": {"cli": "<tripo 실행 파일 경로>", "home": "output/tripo-state", "key_env_file": ".env.local"},
  "blender": "/Applications/Blender.app/Contents/MacOS/Blender"
}
```

## 단계

| 단계 | 언제 만드나 | 만드는 것 | 참고 구현 | 완료 조건 |
|---|---|---|---|---|
| **H0** | 프로젝트 첫날, 콘텐츠 전(30분 이내) | 설정 파일, `.gitignore`(Library·Temp·output·`.env.local`), Claude hook `tools/guard.py` + `.claude/settings.json`(같은 주제 3번째 Codex 검토 차단, 맥락 25만/30만 알림. 전체 Play 차단은 H2에서 실행기가 생기면 켬) | `reference/tools/Hooks/` | `guard.py selftest` PASS |
| **H1** | 첫 룩데브 장면이 생길 때 | `tools/probe.py`(장면 열기 → Play → N초 대기 → 게임 카메라 캡처 N장(간격) → 성능 중앙값 → 종료 → 정리, Play 중이면 시작 안 함), `unity/` 패키지의 측정 클래스, `tools/protect.py`(부작용 파일 되돌리기), `tools/session_time.py` | `play_probe.py`, `DSKeyArt045Verification.cs`, `protect.py`, `session_time.py` | 룩데브 장면 캡처 1장 + 성능 한 줄 |
| **H2** | 조작 가능한 게임 루프가 생길 때 | 짧은/전체 Play 실행기 `tools/play_check.py`(짧은: 시작 → 이동 → 정지 → 콘솔 → 정리 + 캡처, 전체: 한 판·부하·화면비, `FULL_REASON` 필요, 모드 허용 목록 밖은 거부, 실패 시점 상태 기록), `tools/tier.py`(필요 단계 고르기, 증거 종류별 해시). 게임마다 다른 조작(출전·웨이브 건너뛰기 등)은 프로젝트 쪽 C# 훅으로 받는다 | `keyart_045.py`, `tier.py`, `tier-policy.json` | 짧은 Play PASS 기록 |
| **H3** | 에셋 양산 직전(룩데브에서 규격이 정해진 뒤) | **에셋 정식 경로 `tools/assets.py`** — 3dkit의 중심. 카탈로그(숫자·채택 목록 원본), Tripo 생성(승인 원문·상한 없으면 거부, 잠금·재개 일지, 네트워크 오류 재전송 금지), 감량(Blender), 텍스처 처리, Unity 가져오기(같은 경로 덮어쓰기로 GUID 유지), 규격 대조 `check`, 정면 줄 세우기 `capture` | `reference/tools/Art/` 전부 | 에셋 1종이 `gen → prep → unity → check → capture`를 통과 |
| **H4** | 첫 커밋할 콘텐츠가 생길 때 | 커밋 전 gate `tools/gate.py`: 문서 링크, 컴파일, 정적 참조(빌드 장면 경로·GUID, 누락 참조), `assets.py check`, 현재 게임 동작 지문에 맞는 짧은 Play 증거. 프로젝트의 Git pre-commit에 연결 | 이전 gate의 `refs` 아이디어만. 코드는 새로 | gate PASS로 첫 커밋 |

- H0~H4 밖의 도구는 사용자가 요청할 때만 만든다.
- 각 단계가 끝나면 [도구 연결](tools.md)과 프로젝트의 `docs/개발환경.md`를 고친다.
- 두 번째 프로젝트에 넣어 설정 파일만 바꿔 돌아가면 이식성이 증명된 것이다. 그전에는 "범용"이라고 부르지 않는다.

## 이전 도구에서 옮길 기능과 버릴 기능

| 도구 | 옮길 기능 | 버리거나 바꿀 것 |
|---|---|---|
| `play_probe.py` (336줄) | 장면·HP 고정·캡처 묶음(간격)·밤·공격 표본·성능 중앙값·`--mobile`(모바일 렌더 설정으로 측정)·`--assets`(렌더러 구성 출력)·`--dry-run`·`--selftest`, Play 중이면 시작 안 함 | 게임 C# 타입 이름 → 설정의 `game_namespace`. 게임 전용 옵션(스쿼드·웨이브)은 프로젝트 훅으로 |
| `protect.py` (138줄) | 실행 전 깨끗했던 파일만 되돌림, 원래 바뀌어 있던 파일은 알리기만, 사본 보관 | 글꼴 경로 하드코딩 → 설정의 `side_effect_files` |
| `session_time.py` (219줄) | Claude 세션 기록에서 사용자 요청 단위 시간·토큰 표 | 세션 기록 경로를 현재 프로젝트에서 계산 |
| `tier.py` + `tier-policy.json` | 짧은/전체 선택, 게임 동작·시각 해시 분리, 장면별 마지막 전체 PASS 보존 | 정책을 설정 파일로 합침 |
| `claude_guard.py` (277줄) | 전체 Play 이유 강제, 검토 3회째 차단, 맥락 알림, heredoc·따옴표 문장은 명령으로 안 봄, fail-open, selftest | 옛 실행기 이름 → 설정의 `full_runners` |
| `keyart_045.py` (958줄) + `DSKeyArt045Verification.cs` (399줄) | 짧은/전체 모드 허용 목록, 실패 시점 게임 상태 기록, 정리 단계, 콘솔 오류 판정 | 키아트 045 전용 단계·장면 이름. 새 실행기는 짧은 모드부터 작게 |
| `assets.py` 묶음 (약 1,340줄) | 카탈로그 원본, 예산 승인 가드, 잠금·재개 일지, 재전송 금지, GUID 유지, 규격 대조, 줄 세우기 캡처 | 로우폴리 전용 처리(텍스처 팔레트화 강제, 256 텍스처 상한, 허용 셰이더 목록)는 [에셋 규격](asset-standard.md)의 프로젝트 값으로 |
| 이전 `Tools/Verify/*` (7,902줄) | 없음(아이디어만: 빌드 장면 GUID 대조) | 옛 2D 회귀, 맵 하네스(경로 전수·실패 주입·watchdog), 옛 아트 내보내기 차단 |

## 만들지 않을 것 (사용자 결정 전)

- 맵 경로 전수 검사(이전 708경로), 실패 주입·외부 watchdog, 사례 행렬 확대.
- gate의 시각 증거 재사용(이전에도 후속으로 남김). 필요해지면 사용자에게 묻는다.
- 범용 에셋 파이프라인 전체 설계(이전 A0~A5). 이전에는 설계 문서가 구현보다 훨씬 컸다. H3의 정식 경로 하나로 시작한다.
