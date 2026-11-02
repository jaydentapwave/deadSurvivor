# 하네스 설계 회고와 적용 — 2026-11-02

- 사용자 요청
  - "우리 하네스 설계의 문제점을 파악해줘, 여태까지 작업한것들의 기록물들을 확인하면서"
  - "위의 고치는 방향순서대로 모두 작업을 진행하고hook까지 작업을 진행해줘"
- 작업 구분: L1(워크플로우·검사 계약 변경, 도구 추가). 게임 런타임 코드는 바꾸지 않았다.
- 결과 규칙: [핵심 규칙](../workflows/core-rules.md)

## 1. 근거 (045 기록, 11-02 19:09 집계)

| 항목 | 수치 | 출처 |
|---|---|---|
| 실제 작업 / 경과 | 10.6h / 17.7h | [시간·토큰 기록](keyart-target-045-time.md) |
| 전체 Play(10웨이브) | 31회, 약 3.5~4h(실제 작업의 35~40%) | `output/keyart-target-045/r001/run-*`(55개) |
| Codex 검토 | 27회(설계 11, 구현 16). 7개 주제 모두 첫 검토 FAIL, 주제당 2~7회 | `output/keyart-target-045/r00*/codex-*review*.md` |
| Claude 토큰 | 응답 1,279회, 응답마다 평균 약 50만 다시 읽음(초반 13만 → 중반 56~79만) = 6.38억 | 세션 기록 |
| 도구 코드 / 게임 코드 | 약 1.9만 줄(Tools 1.57만 + Editor 0.34만) / 5.5천 줄 | 줄 수 집계 |
| 문서 | 설계 102개 5,707줄, 검토 73개 3,137줄 | `docs/design`, `docs/reviews` |
| 놓친 것 | 69건. 가장 많은 유형은 "검사가 증명하지 않은 것을 PASS"(16건) | [045 검증 기록](keyart-target-045-review.md) |

## 2. 문제점

| # | 문제 | 근거 | 왜 설계 문제인가 |
|---|---|---|---|
| H1 | 검증 단계가 "전체" 하나뿐 | 045 실행기의 기본이 10웨이브 전체였다. 짧은 확인은 11-02 19시에 처음 만들었다 | "마감 때만 전체"라는 규칙이 있어도 도구에 다른 선택지가 없었다 |
| H2 | 증거가 너무 쉽게 무효가 됨 | 실행기 지문에 텍스처·재질이 포함됐다. 3단계 v1은 텍스처 수정으로 전체 Play를 8회 돌렸다 | "지문이 바뀌면 다시 실행" 규칙과 거친 지문이 합쳐져 전체 재실행이 규칙상 필수가 됐다. 034의 범위별 재사용은 목표별 실행기에 적용되지 않았다 |
| H3 | 검토가 수렴하지 않음 | 27회, 첫 검토 PASS 0/7 | 규칙상 "근거 있는 미실행 위험·누락 검사도 필수"였다. 검토자는 언제나 새 검사를 요구할 수 있었고, FAIL마다 전체 Play와 재검토가 따라왔다 |
| H4 | 검사가 늘기만 함 | 놓친 것마다 검사가 추가됐다. "확실할 때만 뺀다"만 있고 빼기 절차는 없었다. 새 검사가 다시 틀리기도 했다(S3-22) | 비용은 계속 늘었다. 그런데 사용자 눈에 보이는 결함(S3-21 방향, S1-20 넓은 화면 UI)은 검사가 아니라 눈으로 찾았다 |
| H5 | 규칙이 문서로만 있음 | [11-01 회고](work-delays-retrospective-20261101.md)에서 같은 문제를 찾았고 사용자가 "반드시 필요한 것만 검증"이라고 했다. 규칙도 넣었지만 045에서 반복됐고 같은 말을 다시 들었다 | 기존 hook(pre-commit, Codex Stop)은 "검사 누락 방지"용이다. 과잉 검증은 막지 않는다. 045에서는 커밋이 없어 pre-commit도 한 번도 작동하지 않았다 |
| H6 | 대화 하나를 길게 씀 | 042~045를 한 세션에서 진행했고, 응답마다 다시 읽는 양이 최대 약 79만이었다 | "단계 끝 → 인수인계 → 새 세션" 절차가 없었다 |

메인 에이전트의 판단 문제도 있었다. 규칙은 "마감 때만 전체"였는데, 실행기에 그 모드밖에 없다는 이유로 매번 전체를 돌렸다.

## 3. 적용한 것

| 순서 | 변경 | 파일 |
|---|---|---|
| 1 | 검증 3단계(빠른 확인 / 짧은 Play 기본 / 전체는 마감 때). 045 실행기 기본을 짧은 단계로 바꿨고, 전체는 `DS_FULL_REASON`이 있어야 실행된다 | `Tools/Harness/tier.py`, `Tools/Harness/tier-policy.json`, `Tools/Proto/keyart_045.py`(`run_short`), `Tools/Proto/quick_squads_045.py`(감싸기) |
| 2 | 증거를 게임 동작 / 보이는 것으로 나눠 기록하고 재사용한다(`tier.py plan`) | `Tools/Harness/tier.py`, `output/verification/tier-evidence.json` |
| 2+ | S3-22 수정: 몸 방향은 한 달리기 주기 평균(표본 6개)으로 판정한다. 막히면 반대 방향으로 다시 이동한다 | `Tools/Proto/keyart_045.py`(`move_sample`, `judge_s3b`) |
| 3 | 검토 상한(주제당 설계 1 + 마감 1). 필수 수정은 확인된 결함, 완료 조건 위반, 데이터 손실 위험으로 한정 | `docs/workflows/core-rules.md`, `docs/workflows/development.md` |
| 4 | 단계마다 인수인계 후 새 세션. 긴 대화 알림 | `docs/templates/handoff.md`, hook |
| 5 | 규칙 줄이기: 1쪽 핵심 규칙을 진입점으로 하고, 부딪히던 문장을 고쳤다. 검사를 추가할 때 뺄 후보도 적는다 | `core-rules.md`, `AGENTS.md`, `CLAUDE.md`, `development.md`, `verification-gates.md` |
| 6 | 사용자가 먼저 본다(짧은 Play 캡처, 직접 플레이) | `core-rules.md` 6절 |
| hook | 프로젝트 Claude hook: 이유 없는 전체 Play 차단, 같은 주제 3번째 Codex 검토 차단, 25만 토큰 이상 대화 알림. 오류가 나면 통과시킨다(fail-open) | `.claude/settings.json`, `Tools/Hooks/claude_guard.py` |

5번의 "문서 수 줄이기"(설계 102개, 검토 73개 정리)는 하지 않았다. 과거 기록이라 지우지 않고, 진입점을 1쪽으로 줄이는 데서 멈췄다.

## 4. 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| `tier.py selftest` | PASS 10/10 | 단계 선택, 시각 변경만 있을 때 재사용, 실패·다른 장면 기록 무시, 이유 없는 전체 실행 차단 |
| 실행기 판정 자체 시험 | PASS 17/17 | 달리기 주기 흔들림(52~115°) PASS, 계속 68° 어긋남 FAIL, 표본 누락 FAIL |
| 짧은 Play 실제 실행(s3b) | **PASS**, 65.5초 | `output/keyart-target-045/r001/run-a7a0e48097ea`: 두 스쿼드 판정, 콘솔 새 에러 0, 보호 파일 무변경 |
| 실행기 전체 실행 차단 | PASS | `KEYART045_STAGE=full`을 이유 없이 실행하면 exit 3 |
| `claude_guard.py selftest` | PASS 23/23 | 전체/부하/이전 실행기/`run.py all` 차단과 이유 붙이면 통과, 빈 이유 차단, echo·grep 언급 허용, 검토 1·2회 허용·3회 차단·승인 통과, 절대 경로, 맥락 알림 1회·다음 단계 재알림 |
| hook 실제 작동(이 세션) | PASS | 시험 `echo`가 막혔다. 이 결과로 언급만 있는 명령까지 막는 오탐을 찾아 python 실행만 막도록 좁혔다 |
| 설정 JSON | PASS | `jq -e`로 두 hook 명령 확인 |
| gate (`gate.py check`) | 문서 PASS(RUN), 데이터·맵 3종 PASS(REUSE), **Unity 범위 BLOCKED** | `output/verification/gate-20261102-193033-2ab5a2bc`. BLOCKED 원인은 이번 변경이 아니라 17:33 gate 이후의 s3b 영웅 재빌드(S3-21 방향 수정)다. 새 규칙상 Unity 기본 회귀(`run.py all --screens`)는 커밋 전에 1회 실행한다 |

- NOT_RUN: 전체 Play(이번 변경은 실행기의 짧은 단계와 판정만 바꿨다. 전체 경로는 진입부 차단 말고는 그대로다), 맥락 알림 hook의 실제 프롬프트 발동(다음 프롬프트부터 확인 가능), Codex에서의 동작(Codex는 Claude hook을 쓰지 않는다).
- 효과(시간·토큰 절감)는 다음 작업에서 `session_time_045.py` 같은 집계로 측정한다. 아직 **미측정**이다.

## 5. 남은 것

- **gate 수집기 범위가 문서와 다르다(H2의 한 예).** [034](verification-scope-034-review.md)·[gate 문서](../workflows/verification-gates.md)에는 Unity 증거의 수집 의존성이 `run.py`·`unity_check.py`·`gate_support.py`·`Tools/project_paths.py`라고 적혀 있다. 그런데 `Tools/Verify/gate_support.py`는 `Tools/Verify/` 아래 모든 `.py`를 수집기로 센다. 그래서 처음에 `tier.py`를 `Tools/Verify/`에 두자 기존 Unity 증거가 모두 무효가 됐다. 이번에는 gate 코드를 고치지 않고 새 도구를 `Tools/Harness/`로 옮겼다. gate 코드 수정은 L2이므로 사용자 결정 뒤에 한다.

- 044 실행기(`proto_main_044.py`)에는 짧은 단계가 없다. hook과 정책은 전체 실행으로 보고 막는다. 다시 쓰게 되면 짧은 모드를 추가한다.
- `tier-evidence.json`에는 아직 전체 PASS 기록이 없다. 다음 단계 마감의 전체 실행부터 쌓인다. 그 전까지 `plan`은 "기록 없음 → 짧은 단계(마감이면 전체)"를 낸다.

## 6. 독립 검토 결과 (Codex r001)

- **FAIL, 필수 5건.** 결과: `output/harness-20261102/codex-impl-review-harness-r001.md`
  1. 이름에 "review"가 없는 결과 파일은 3번째 검토로 막히지 않는다.
  2. `baseline044`·오타 모드는 이유 없이 전체 경로로 들어간다.
  3. 짧은 기록이 40건 넘게 쌓이면 전체 PASS가 잘려 나간다.
  4. 캡처 없는 짧은 실행도 시각 증거로 기록된다.
  5. 핵심 규칙과 gate 문서의 마감 조건이 충돌한다.
- Codex가 직접 다시 확인한 것: tier 시험 10/10, 판정 시험 17/17. hook 시험은 Codex 샌드박스에서 임시 폴더 쓰기가 막혀 BLOCKED였다.
- 사용자 요청("더 이상 추가작업은 하지말고 다음단계해야할일에 대해서 문서 작성을 좀 해줘")으로 **수정하지 않고** 종료했다.
- 수정 방향과 순서는 당시 현재 상태의 인수인계에 적었다. 수정과 마감 검토 결과는 7절에 있다.
- 대화 맥락 알림 hook은 사용자 프롬프트에서 실제로 작동했다(약 34만 토큰). 4절의 NOT_RUN 중 이 항목은 PASS로 본다.

## 7. r001 필수 5건 수정과 마감 검토 r002 (2026-11-02 21시, 새 세션)

사용자 요청: "1번부터 쭉 작업을 진행해줘".

| # | 수정 | 위치 |
|---|---|---|
| ① | 결과 파일 이름이 `<주제>-rNNN.md`이면 "review"가 없어도 검토로 센다. 옛 이름(review 포함)도 계속 센다 | `Tools/Hooks/claude_guard.py` `check_review` |
| ② | 실행 모드를 허용 목록(`STAGES_SHORT` short·smoke·purity / `STAGES_FULL` full·load·baseline044)으로 검사한다. 목록 밖 값은 실행 폴더를 만들기 전에 exit 1로 멈춘다. `baseline044`도 이유가 필요하다 | `Tools/Proto/keyart_045.py` 상단·`main`, `tier-policy.json` `full_if` |
| ③ | 기록을 40건으로 자를 때 장면별 마지막 전체 PASS는 남긴다 | `Tools/Harness/tier.py` `trim` |
| ④ | 기록에 `captured`를 넣는다. 시각 증거는 실제 캡처한 PASS만 인정한다. 캡처 없는 실행과 이 필드가 없는 옛 기록은 게임 동작 증거만 된다 | `tier.py` `record`·`plan`, `keyart_045.py`의 `TIER.record` 2곳 |
| ⑤ | 핵심 규칙은 작업 중·단계 마감 기준이고, 커밋 전 gate의 Unity 범위는 gate 문서가 원본이라고 두 문서에 적었다. gate의 시각 증거 재사용은 미지원(후속)이라고 명시했다 | `docs/workflows/core-rules.md` 1·2절·hook 표, `docs/workflows/verification-gates.md` |

| 검사 | 결과 |
|---|---|
| `tier.py selftest` | PASS 15/15 (새 5건: 전체 PASS가 자르기에서 살아남음, 오래된 짧은 기록은 잘림, 캡처 없는 짧은 실행·전체 실행·옛 기록은 시각 증거 아님) |
| `claude_guard.py selftest` | PASS 27/27 (새 4건: `baseline044` 이유 없으면 차단·있으면 통과, `harness-r003.md` 차단, 다른 주제 통과) |
| 실행기 오타 거부 | PASS: `KEYART045_STAGE=ful` → exit 1, 실행 폴더 수 변화 없음(148 → 148). Unity 호출 없음 |
| 정책 JSON, `keyart_045.py` 컴파일 | PASS |
| **Codex 마감 검토 r002** | **PASS**, 새 필수 없음. r001 5건 모두 "고쳐짐". Codex 재확인: tier 15/15, 변경 부위 메모리 시험 13/13, hook 시험은 샌드박스 쓰기 제한으로 BLOCKED. 결과: `output/harness-20261102/codex-impl-review-harness-r002.md` |

- 이 주제의 검토는 상한(설계 1 + 마감 1)에 도달했다. 더 필요하면 사용자에게 먼저 묻는다.
- Play·Unity: NOT_RUN. 도구·문서만 바뀌었으므로 핵심 규칙 1절의 빠른 확인 단계다.
- 후속(필수 아님): 일반 장면 짧은 실행의 정지 표본 미판정, 콘솔 사전 조회 실패 처리, gate의 시각 증거 재사용, gate 수집기 범위(5절).
- 시간·토큰(이 작업): 약 7분(20:54~21:01 KST, 문서 정리 전까지), 도구 호출 43회, Claude 캐시 읽기 266만, 출력 3.0만, Codex 1회 4.7만. 원자료 `output/keyart-target-045/time/time-report-harness-fix-20261102.json`. 이 파일의 `codex` 항목은 집계기가 `output/keyart-target-045` 아래 로그만 세기 때문에 이전 세션 값(31회)이다. 이번 Codex는 `output/harness-20261102/codex-r002.log` 값이다. 이전 세션(실제 작업 10.6h, 캐시 읽기 6.38억, Codex 31회 219만)과는 작업 종류·크기가 달라 절감 효과를 아직 판단할 수 없다. 게임 변경이 있는 다음 작업에서 다시 잰다.
