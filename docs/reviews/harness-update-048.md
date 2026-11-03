# 048 하네스 설계 갱신 — 047 H1~H13과 046 놓친 것 반영

- 사용자 요청(2026-11-03 10시대): "3번을 우선 작업 진행해줘" — [현재 상태](../현재상태.md) 다음 할 일 3번 "하네스 설계 갱신: 047 기록의 H1~H13와 046 놓친 것을 모아 워크플로우·도구에 반영".
- 범위: 도구·hook·정적 검사·워크플로우 문서. 게임 런타임 코드와 장면은 바꾸지 않았다(047 미커밋 변경은 그대로 둠).
- 규모: L1(검사·hook·워크플로우 변경). 검증은 도구 자체 시험 + 새 Play 도구의 실제 연결 1회 + 짧은 Play(변경한 실행기 시험).

## 반영 내용

| ID | 바꾼 것 | 파일 |
|---|---|---|
| H1 | Tripo 동작은 연속 시연 동작일 수 있다는 점, 받자마자 `clip_scan_047.cs`로 손 속도·골반 높이를 숫자로 보고 잘라 쓰는 단계 | [자산 워크플로우](../workflows/ai-3d-assets.md) "Tripo 동작 받기" |
| H2 | 전체 Play 차단 hook이 실제 실행 위치만 본다. heredoc 본문, 따옴표 안의 띄어쓰기 있는 문장은 제외. 변수 값, `bash -c` 문자열, 따옴표 친 경로 한 단어는 계속 본다 | `Tools/Hooks/claude_guard.py` `executed_text`, [핵심 규칙](../workflows/core-rules.md) hook 표 |
| H3 | 장면 소유 빌더 하나, 다른 빌더 결과 다시 붙이기, 저장 뒤 다시 열어 확인 | [장면 빌더 공통 규칙](../workflows/scene-builders.md) 1·2절 |
| H4 | 부작용 파일(`PrototypeFont.asset`)이 실행 전 깨끗했으면 실행 뒤 `git checkout`으로 되돌림. 이미 바뀌어 있던 파일은 건드리지 않고 알림 | `Tools/Harness/protect.py`, `tier-policy.json` `side_effect_files`, 빌더 규칙 4절 |
| H5 | 화면·측정용 Play 도구 하나: 장면, 스쿼드, 웨이브 건너뛰기, HP 고정, 캡처 수·간격, 공격 동작 표본, 밤, 성능. Play 중이면 시작하지 않음 | `Tools/Harness/play_probe.py`, 핵심 규칙 1절 |
| H6 | 짧은 Play가 `Map046_Full`에서 공격 레이어 표본을 한 번 본다(첫 공격이 나올 때까지 최대 30회, 0.1초 간격) | `Tools/Proto/keyart_045.py` `attack_sample` |
| H7 | 순간 효과는 연속 캡처나 상태 숫자로 확인 | 핵심 규칙 2절, `play_probe.py --captures --interval` |
| H8 | 유료 동작 단계는 승인 원문을 journal `user_approval`에 쓴 뒤 제출. 승인 문구가 없으면 거부 | `Tools/Proto/tripo_045.py` `step_auth` |
| H9 | 시간·토큰을 사용자 요청 단위로 자동 분할. Codex 토큰은 세션 시간 안에 쓰인 로그만 | `Tools/Harness/session_time.py`, AGENTS |
| H10 | 맥락 30만 토큰 단계 추가: "새 큰 작업이면 인수인계 후 새 세션 먼저" 한 번 알림(25만 알림은 그대로) | `tier-policy.json` `new_session_tokens`, hook, 핵심 규칙 4절 |
| H11 | 정적 검사: 게임 코드의 Burst 색이 모두 `DSMapFx` 대응표에 있는지, 색이 상수인지 | `Tools/Verify/run.py` `refs_check`, gate `refs` |
| H12 | 시각 변경이 큰 단계는 마감에 성능 한 줄(정해진 측정 명령) | 핵심 규칙 1절 |
| H13 | 정적 검사: 빌드 설정 장면 GUID와 장면 `.meta` GUID 일치. 빌더는 덮어써 GUID 유지 | `refs_check`, gate `refs`, [gate 문서](../workflows/verification-gates.md), 빌더 규칙 3절 |
| 046 | 빌더에만 반영돼 있던 교훈(높이 순서, 팔레트 확인, 후처리 함께 옮기기, 밀도 숫자 비교, 예산 "개당 × 배치 수", 정면 확인)을 문서로 모음 | 빌더 규칙 5절, [046 놓친 것](../design/map-full-046.md#놓친-것) 반영 칸 |

## 이번에 찾은 결함

| ID | 분류 | 내용 | 처리 |
|---|---|---|---|
| 048-F1 | 도구(기록 손실) | Tripo 도구가 사용자 승인 문구를 `authorization` 키로 저장했다. 비밀값 가리기 규칙이 이 키를 HTTP 인증 정보로 보고 값을 가려, 045~046의 모든 `request-frozen.json`에 승인 원문 대신 `[REDACTED]`가 남았다. H8 작업 중 시험에서 발견 | 키를 `user_approval`로 바꿈, 시험 사례 추가(`selftest_tripo_045.py` "frozen request keeps the approval text"). 이미 쓴 요청 파일은 해시 검사 대상이라 고치지 않았다. 승인 원문은 코드의 `AUTH*` 상수에 있다. 047 `attack` 단계 journal에는 승인 문구를 보충하고 보충 표시를 남김 |
| 048-F2 | 미확정 | `Map046_Full` 짧은 Play 1회째에서 스쿼드 C 이동 확인 중 `NOT_IN_BATTLE`로 FAIL(스쿼드 B·공격 표본은 PASS). 캡처 때(전투 5초)는 4명 모두 살아 있었다. 실행기가 실패 시점 게임 상태를 남기지 않아 원인을 나눌 수 없었다 | 실행기에 실패 시점 상태 기록(`state_at_error`)을 더하고 한 번 다시 실행 → PASS, 재현 안 됨. 원인 UNKNOWN(045 2단계의 1회 정체 FAIL과 같은 처리) |

## 검증

| 검사 | 상태 | 근거 |
|---|---|---|
| hook 자체 시험 | PASS 44/44(새 사례 20: heredoc·따옴표·`bash -c`/`-lc`·명령 치환·셸로 넘기는 heredoc·30만 단계) | `/usr/bin/python3 Tools/Hooks/claude_guard.py selftest` |
| tier 자체 시험 | PASS | `Tools/Harness/tier.py selftest` |
| Tripo 도구 시험 | PASS(새 사례 4: 승인 문구 보존·기록·거부·동작별 승인) | `Tools/Proto/selftest_tripo_045.py` |
| protect 자체 시험 | PASS 9/9(복구 사본, index만 바뀐 경우, 실행 중 커밋된 경우, 같은 초 두 번 복원 포함) | `Tools/Harness/protect.py selftest` |
| session_time 자체 시험 + 이 세션 실제 집계 | PASS 7/7, 실제 세션 요청 2개 분할 확인 | `Tools/Harness/session_time.py --selftest`, `--markdown` |
| play_probe 자체 시험 | PASS 10/10(장면 열기 응답 실패 뒤 복원, 거부 때 기존 상태 보존: 가짜 Unity) | `--selftest` |
| play_probe 실제 실행 ① 7웨이브·HP 고정·공격 표본 20·캡처 3·성능 20초 | PASS(83초). 영웅 4명 모두 공격 동작 표본(브루노 slash 10, 한스 slash 6, 레나 shoot 13, 유키 shoot 7), 캡처 3장 투사체 11~15, 부작용 파일 변경 0, 새 콘솔 오류 0 | `output/play-probe/20261103-104513-w7perf` |
| play_probe 실제 실행 ② 046과 같은 측정 순서 | PASS(52초). 7웨이브 적 86: 그리기 호출 401, set-pass 59, 삼각형 29.2만, 그림자 124, 편집기 177fps, 생성 159·실패 0 | `output/play-probe/20261103-104702-w7perf046protocol` |
| `refs` 정적 검사(현재 프로젝트) | PASS: 빌드 장면 2개 GUID 일치, Burst 색 8개 모두 대응, 경고 0 | `python3 Tools/Verify/run.py refs` |
| gate 단위 시험 | PASS 26건(새 사례: `refs` 의존 선택 5종, GUID 불일치·대응 없는 색·상수 아닌 색·형식이 깨진 장면 행·빈 목록 검출) | `cd Tools/Verify && /usr/bin/python3 -m unittest test_gate test_gate_scope` |
| 짧은 Play(Map046_Full) 1회째 | FAIL: 스쿼드 C `NOT_IN_BATTLE`(048-F2). 공격 표본 PASS(레나 shoot 1), 스쿼드 B PASS, 부작용 파일·콘솔 오류 0 | `output/keyart-target-045/r001/run-6470747c453b` |
| 짧은 Play(Map046_Full) 2회째 | PASS(74초): 공격 표본(유키 shoot 1), 스쿼드 B·C 이동·정지, 남은 표시 0, 부작용 파일·콘솔 오류 0. 048-F2는 재현되지 않음 → 원인 UNKNOWN, 다음 실패 때 `state_at_error`로 판단 | `output/keyart-target-045/r001/run-e55c1553c283` |
| 짧은 Play(Map046_Full) 3회째, 검토 수정 뒤 | PASS(75.7초): 공격 표본·스쿼드 B·C, 부작용 파일 복원 단계 실행(변경 없음) | `output/keyart-target-045/r001/run-ad829968e85e` |
| 문서 링크 | PASS | `python3 Tools/Verify/run.py docs` |
| 커밋 전 gate | NOT_RUN(커밋 요청 전) | — |
| 독립 검토 r001(Codex, 10:54~10:57) | FAIL 필수 5건 → 모두 수정(아래 표) | `output/harness-048/harness-update-048-r001.md` |
| 독립 검토 r002(Codex, 마감, 11:03~11:04) | FAIL 필수 1건(4건 해소): 같은 초의 두 복원이 복구 사본 경로를 공유해 첫 사본을 덮어씀 → 호출마다 고유 경로 + 기존 사본 덮어쓰기 금지(`xb`), 회귀 사례 추가, protect 시험 9/9 PASS. **주제 검토 상한(2회) 도달로 3회째 검토는 하지 않음**(사용자 승인 필요). 이 마지막 수정은 독립 검토 미확인 | `output/harness-048/harness-update-048-r002.md` |

## 독립 검토 r001 지적과 처리

| # | 지적 | 처리 |
|---|---|---|
| 1 | hook: `bash -lc '...'`, `echo "$(...)"`, `bash <<EOF` 본문의 실행을 놓치고, `bash -c 'echo "..."'` 안의 문장을 막음. 앞 셋은 이번 변경 전에는 막히던 것(회귀) | `-c`가 붙은 결합 옵션·절대 경로 셸 인식, 큰따옴표 안 `$(...)`·백틱 유지, 셸로 넘기는 heredoc 본문 유지, `-c` 본문은 다시 분석. 회귀 사례 7개 |
| 2 | protect: 실행 도중 누가 같은 파일을 고치면 그 내용이 사라짐. 시작 때 index를 따로 보지 않음 | 덮어쓰기 전 `output/hook-state/protect-backup/<시각>/`에 복구 사본, 사본을 못 만들면 덮어쓰지 않음. 작업 트리·index·HEAD를 따로 확인, 실행 중 HEAD·index가 바뀌면 손대지 않음 |
| 3 | play_probe: 장면 열기 응답이 실패하면(편집기는 이미 열었을 수 있음) 장면·품질 복원을 건너뜀 | 편집기가 상태를 저장하기 전에 거부하는 경우(Play 중, 저장 안 된 장면, 이전 실행 미정리, 다른 프로젝트)만 정리 생략, 그 밖의 실패는 정리 |
| 4 | 짧은 Play가 부작용 파일을 되돌린다는 문서와 달리 연결되지 않음 | `keyart_045.py` 짧은·전체 실행에 `protect.snapshot/restore` 연결(보호 파일 비교 전에 복원, 결과는 `side_effect_files`) |
| 5 | refs: 형식이 깨진 빌드 장면 행(31자리 GUID, 공백 경로)을 조용히 건너뜀 | `m_Scenes` 항목을 모두 읽고 경로·GUID 형식 오류를 FAIL. gate 의존성은 안전한 경로만 쓰고, 빌드 설정 파일 자체가 입력이라 어떤 수정이든 다시 실행 |

## 추가한 검사와 뺄 수 있는 검사 (핵심 규칙 5절)

- 추가: gate 정적 `refs`(파일 몇 개 정규식, 1초 미만), 짧은 Play 공격 표본(`Map046_Full`만, 첫 공격까지 보통 1~3초).
- 뺄 후보: 근거 있는 후보 없음. 짧은 Play 항목별로 실제 결함을 잡았는지 기록을 모으지 않아 판단할 수 없다. 다음 단계 회고에서 항목별 검출 기록을 보고 다시 판단한다(후속).
- 대체: 임시 측정 스크립트 5종(046·047, 작업 폴더 밖)은 `play_probe.py`로 대체. `Tools/Proto/session_time_045.py`는 045 기록용으로 남기고 새 집계는 `Tools/Harness/session_time.py`.

## 놓친 것

| ID | 분류 | 문제 | 원인 | 처리 | 워크플로우 갱신 후보 | 반영 |
|---|---|---|---|---|---|---|
| 048-1 | 도구 | 승인 문구 가림(048-F1)이 045 이후 두 단계 동안 아무도 몰랐다 | 기록 파일을 다시 읽어 내용을 확인하는 시험이 없었다 | 시험 사례 추가 | 기록을 쓰는 도구는 "쓴 것을 다시 읽어 원문이 남았는지" 시험한다 | `selftest_tripo_045.py` |
| 048-2 | 측정 | play_probe 첫 측정은 046과 측정 순서가 달라(대기·표본·캡처 뒤 측정) 적 수가 163으로 늘어 비교할 수 없었다 | 측정 순서를 정해 두지 않음 | 046과 같은 순서로 다시 잼 | 성능 비교는 정해진 한 명령으로 잰다 | 핵심 규칙 1절, 빌더 규칙 5절 |
| 048-4 | 도구 | hook 범위를 좁히면서(H2) 예전에 막던 실행(`bash -lc`, 명령 치환, 셸 heredoc)을 놓치게 됐다. 독립 검토가 찾음 | 시험 사례를 관찰된 한 경우(heredoc 문서 편집)에만 맞춤 | 회귀 사례 추가 | 검사를 좁힐 때는 예전 검사가 잡던 경우를 시험 사례로 먼저 적는다 | 이 기록 |
| 048-5 | 문서 | 빌더 규칙 문서에 "짧은 Play 실행기도 부작용 파일을 되돌린다"고 썼지만 코드는 연결 전이었다. 독립 검토가 찾음 | 문서를 코드보다 먼저 씀 | 코드 연결 | 도구 동작을 적는 문장은 연결·시험한 뒤 쓴다 | 이 기록 |
| 048-3 | 도구 | 짧은 Play 실패 때 게임 상태가 남지 않아 게임 결함인지 검사 결함인지 나눌 수 없었다 | 실행기가 오류 문자열만 남김 | `state_at_error` 기록 | — | `keyart_045.py` |

## 후속 (이번에 하지 않음)

- `build_monsters_046.cs`·`build_fx_047.cs`가 자기 자산 폴더를 지우고 다시 만들어 재질·프리팹 GUID가 바뀐다. 지금은 같은 빌더가 장면 참조를 다시 연결해 결함은 아니다(빌더 규칙 3절).
- 글꼴 정적 아틀라스 전환(H4 근본 해결): 게임 자산 변경이라 사용자 결정 뒤.
- `clip_scan_047.cs` 대상 FBX 경로가 상수다. 다른 동작을 볼 때 상수를 바꿔 쓴다.
- 짧은 Play 항목별 결함 검출 기록(빼기 후보 판단용).

## 시간·토큰

`/usr/bin/python3 Tools/Harness/session_time.py --markdown`(11:08 기준). 요청 2가 이 작업이다(요청 1은 현재 상태 정리).

| # | 시작 | 소요(분) | 활동(분) | 도구 호출 | 출력 토큰 | 캐시 읽기 | 캐시 생성 | 맥락(끝) | 하위 에이전트 | Codex |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 10:32 | 0.7 | 0.7 | 5 | 4,178 | 255k | 48k | 70k | - | - |
| 2 | 10:35 | 29.6 | 27.6 | 122 | 123,876 | 25.8M | 272k | 342k | - | 2회 153k |

합계: 벽시계 32.7분, 활동 28.3분, 도구 호출 127회, 이미지 1회, 백그라운드 7건, 출력 128,054, 캐시 읽기 26.1M, 캐시 생성 319k, 하위 에이전트 0, Codex 2회 153,260

- 많이 든 곳: 캐시 읽기(맥락 7만 → 34만에서 도구 호출 120여 회, H10과 같은 원인), 독립 검토 2회(Codex 15.3만 토큰, 실제 결함 6건 발견), Unity 실행 5회(play_probe 2, 짧은 Play 3: 약 6분, 모두 백그라운드).
- 개선 후보: 이 작업 시작 때 맥락이 이미 7만이었고 끝에 34만이었다. 새 도구 3개 + 검사 2개 + 문서 7개를 한 세션에서 했다. 도구별로 세션을 나누면 캐시 읽기가 줄지만 검토·시험 왕복이 늘어 이득은 측정 전이다.
