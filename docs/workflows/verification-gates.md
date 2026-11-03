# 검사 누락 방지 hook

개인 프로젝트 로컬 설정. Git 커밋 전과 Codex 응답 종료 시 동일한 검사기를 사용한다. 실제 게임/맵 하네스를 대신하지 않으며 전역 설정·외부 서비스 연결은 없다.

## 설치 상태와 실제 차단

| 지점 | 설정/동작 | 상태 |
|---|---|---|
| Git 커밋 직전 | `.githooks/pre-commit` → `Tools/Verify/gate.py pre-commit` | 이 저장소 core.hooksPath=.githooks 활성화. 이전값 /dev/null |
| Codex 응답 종료 | `.codex/hooks.json`의 Stop → `gate.py stop` | 정의 작성·직접 호출 시험 완료. **앱의 hook 신뢰 및 실제 발동은 별도 확인 필요** |
| 직접 최종 검사 | `/usr/bin/python3 Tools/Verify/gate.py check` | 현재 파일을 매번 검사하고 새 gate.json 기록 |
| Claude 도구 실행 전·프롬프트 제출 | `.claude/settings.json` → `Tools/Hooks/claude_guard.py` | 2026-11-02 설치. 효율 가드(전체 Play·검토 반복·긴 대화)이며 이 gate와 별개다. [핵심 규칙](core-rules.md) |

Codex 프로젝트 hook은 사용자가 현재 정의를 검토·신뢰해야 실행된다. 프로젝트 신뢰와 hook 정의 신뢰는 구분된다. 프로젝트 폴더에서 Codex CLI를 열고 `/hooks`로 프로젝트 Stop 항목을 검토/신뢰한다. 변경된 정의는 재검토 대상이다. 신뢰 DB를 편집하거나 우회 플래그를 사용하지 않았다. 현재 대화가 새 정의를 다시 읽었다고 가정하지 않는다. 근거: [OpenAI 공식 hook 문서](https://learn.chatgpt.com/docs/hooks), Where Codex looks / Review and trust / Stop. 확인2026-10-30.

2026-11-01 사용자의 ‘안 뜨게’ 요청에 따라 정상적으로 실행·기록된 Stop은 최초/재호출 모두 빈 JSON으로 응답한다. 자동 후속 프롬프트를 만들지 않으며 PASS/FAIL/BLOCKED 결과는 gate.json에 그대로 남긴다. 직접 `check`와 Git pre-commit은 기존처럼 실패를 반환하고 커밋을 차단한다. 입력 오류·실행/기록 자체 예외는 계속 차단한다. Stop 신뢰 여부와 사용자 직접 중단은 별개다.

## 변경 범위에 따라 수행하는 검사

매번 현재 입력·경로·실행 권한과 검사 전후 안정성을 확인한다. pre-commit은 전체 입력과 Git 인덱스의 내용/모드/추가/삭제 및 검사 중 인덱스 불변도 확인한다. 나머지는 다음 의존성이 바뀌었거나 유효한 근거가 없을 때 실행한다.

| 검사 | 새 실행이 필요한 경우 | 같은 입력의 근거가 유효한 경우 |
|---|---|---|
| 문서 링크 | Markdown 집합/내용, 링크 대상 존재 상태, 검사기 변경 | 원래 결과 재확인 |
| 콘텐츠 ID/참조 | PrototypeData 또는 검사기 변경 | 재사용 |
| 장면·효과 참조(`refs`, 048) | 빌드 설정, 빌드 장면 `.meta`·존재 여부, `DSGame*.cs`, `DSMapFx.cs` 또는 검사기 변경. 빌드 장면 GUID 일치(047-E11)와 Burst 색 → `DSMapFx` 대응(047 H11)을 본다 | 재사용 |
| H1 기초 8개 | 기하 분석 모듈·fixture 또는 검사기 변경 | 재사용 |
| MAP007 준비 151항목+내보내기 지문 | 활성 layout/참조/사례·SVG/PNG·manifest·출력기·005 등록표·정책 변경 | 재사용 |
| Unity 기본 회귀 | 실제 runtime/템플릿 또는 Unity 수집기 변경 | 현재 지문과 원자료를 새 판정기로 재확인 |
| 다중 해상도 화면 | UI·씬·자산·데이터·설정·파일 추가/삭제·미분류 변경, 유효한 화면 근거 없음 | 아래 제한 안에서 기존 화면 근거 사용 |

정적 검사는 검사기·선택기·Python 환경도 의존성에 포함한다. 최초 실행에는 정적 다섯 묶음(048에서 `refs` 추가)을 실행한다. 이후 문서만 변경하면 문서 검사만 다시 실행한다. 매 마감의 별도 local, 변경 없는 H1/MAP007 재실행은 필수가 아니다. local은 전체 정적 진단이 필요할 때 명시적으로 실행하는 명령이다. 전수 topology·전체 Python·부하 시험은 현재 변경/인수 단계와 관련될 때 선택하며 자동 추가하지 않는다.

정적 근거는 새 형식(schema 2)의 성공 gate에서만 가져온다. 검사별 의존 맵과 원본 보고서 SHA/내용, MAP007 감사 원자료를 다시 확인한다. 결과 누락·손상·빈 결과 또는 동일 의존성의 최신 실패가 있으면 해당 검사를 새로 실행한다. 검사 중 입력이 변한 gate는 재사용 근거가 될 수 없다. 과거 결과를 수정하거나 baseline을 갱신하지 않는다.

Unity 기본 회귀는 컴파일·규칙·실제 프레임 증가·입력·기본 캡처·콘솔·복구를 유지한다. 기본 회귀만으로 인수할 수 있는 변경은 기존 계산 파일 DSGame.Combat/EquipmentCombat/Stats, EquipmentRules, DSMapGeometry/Motion/Navigation의 **내용 변경**이다. 파일 추가/삭제나 .meta 변경은 해당하지 않는다. 유효한 전체 화면 원자료가 있어야 하고, 그 실행과 현재 runtime의 차이가 이 목록 안에만 있어야 한다. 현재 기본 회귀 자체는 전체 현재 runtime 지문과 일치해야 한다. 그 외에는 전체 화면이 필수다. 기본 회귀 명령은 run.py all, 전체 화면은 run.py all --screens다.

[핵심 규칙](core-rules.md)의 검증 3단계와 증거 재사용(`Tools/Harness/tier.py`, `tier-evidence.json`)은 작업 중·단계 마감에 실행기를 고르는 기준이다. **커밋 전 gate의 Unity 범위는 이 문서가 원본이다.** gate는 `tier-evidence.json`을 읽지 않는다. 그래서 텍스처·재질 같은 시각 파일만 바뀐 경우에도 위 조건에 해당하면 `all --screens`가 필요하다. 짧은 Play 캡처나 사용자 확인은 gate의 화면 근거를 대신하지 않는다. gate에서 시각 증거를 재사용하는 기능은 아직 없다(후속, 사용자 결정 뒤). 커밋 전 실행은 `DS_FULL_REASON="커밋 전 gate"`를 붙인다(Claude hook).

Unity 증거의 수집 의존성은 run.py·unity_check.py·gate_support.py·Tools/project_paths.py다. 무관한 진단/시험 파일 수정 때문에 게임을 재실행하지 않는다. 판정기 변경은 기존 원자료를 현재 판정기로 다시 검사하며, 수집기나 runtime 변경은 새 실행을 요구한다. 수집 당시 before/after 전체 context 일치, 현재 실행 의존성, 필수 원자료 해시는 유지한다. 최신 현재 실행의 실패와 같은 시각 입력의 최신 화면 실패를 이전 PASS로 덮지 않는다. 필요한 증거가 없으면 BLOCKED이며 hook은 Unity를 실행하거나 동기화하지 않는다.

Git은 미스테이징 파일이나 부분 스테이징도 보수적으로 거부한다. 검사기가 임의로 git add 하지 않는다. 최초 커밋에서도 작동한다. 사용자 커밋 요청 시 의도한 전체 입력과 인덱스를 정리한 뒤 커밋한다.

## 범위와 근거 보존

- `gate-policy.json`의 runtime_baseline은 설치 시점의 **변경 감지 기준**이다. 게임 검증 PASS나 신규 구현 완료를 뜻하지 않는다. 검사를 통과하려고 기준을 현재 값으로 자동 갱신하지 않는다.
- 실제 Unity 대상은 허용된 개인 경로의 Assets/DeadSurvivor·ProjectSettings·Packages만 읽는다. 회사 프로젝트를 읽거나 변경하지 않는다.
- UnityArtExport 변경은 대상 자산과의 대응 검증이 아직 없으므로 새 all PASS만으로 통과하지 않는다. 별도 검증 어댑터를 구현/검토하기 전까지 BLOCKED다.
- 과거 MAP007 inputs.json과 과거 리뷰는 역사적 기록으로 보존한다. 현재 코드가 달라졌다는 이유로 과거 지문을 덮어쓰지 않는다. 이전 지문을 새 실행 성공 근거로 사용하지 않는다.
- 최종 시각 확인·사람 플레이·독립 리뷰 내용의 진실성은 기계 검사로 증명하지 못한다. 기존 L1/L2 독립 검토 의무는 그대로다.
- 다른 도구/Claude에는 자동 종료 hook을 설치하지 않았다. Claude에는 2026-11-02 효율 hook만 설치했다(종료 시 gate 실행 아님). Git hook은 같은 저장소의 일반 Git 커밋에 적용된다.

## 결과와 재실행

매 실행은 `output/verification/gate-<시각>-<ID>/gate.json`에 상태·호출 모드·검사 원자료 경로/해시·입력 지문을 기록한다. 각 검사에 action RUN/REUSE와 선택 이유·의존 지문을, 최상위에 executed/reused 목록을 남긴다. REUSE는 새 실행 PASS가 아니다. 실패/시간초과/빈 결과는 PASS가 아니다. RUN으로 시작한 검사에서 기존 보고서를 새 결과처럼 반환하면 거부한다. Stop 재진입 플래그가 있어도 검사 실패를 PASS로 바꾸지 않는다. 정상적으로 기록된 Stop은 최초/재호출 모두 빈 JSON으로 응답해 자동 대화 재개·알림을 요청하지 않는다. check/pre-commit 실패 차단, 입력/기록 오류 차단은 유지한다. 해결 불가하면 BLOCKED 근거를 설명하고 사용자의 중단을 존중한다.

현재 준비 도면을 고친 경우 공통 layout에서 SVG를 출력한 뒤 [PNG 내보내기](../art/maps/map-meadow-007-r001/export_png.cjs)를 실행하여 render-manifest.json을 같이 갱신한다. 출력용 런타임은 작업환경의 Node와 sharp를 사용한다. 검사 때문에 그림을 자동으로 덮어쓰지는 않는다.

## 복구와 한계

### 예정된 AI 3D 자산 연결 — 현재 미구현

[자산 하네스](../design/ai-3d-asset-harness.md)의 P4에서 자산/처리기/Unity 대상/맵 binding 변경 감지와 현재 지문 증거 인수를 별도로 구현한다. 자산 승인·복구·시각 검토·필수 AC/AFI가 누락되거나 지원하지 않는 어댑터이면 BLOCKED여야 한다. 새 hook/명령은 이번 문서 변경으로 설치되지 않는다.

현재 gate는 위 자산 계약을 검사하지 않는다. 이번 문서 PASS를 자산 PASS로 보고하지 않으며, 기존 UnityArtExport 변경 차단과 baseline은 유지한다. 향후 원본→실제 대상→프리팹/의존성→검증 화면의 대응을 구현·독립 검토·정상/실패 시험하기 전에는 차단을 해제하지 않는다. gate가 유료 생성/패키지 설치/Unity 동기화를 자동 수행하도록 설계하지 않는다.

### 수동 복구 범위

설정 문제로 복구가 필요할 때는 사용자가 원인을 확인한 뒤 이 저장소 hooksPath를 이전값 `/dev/null`로 돌리고 프로젝트 Stop 정의를 비활성화할 수 있다. 자동 실패 복구로 hook을 끄지 않는다. 전역 설정을 건드릴 필요는 없다. `--no-verify`, 명시적 hook 비활성화, 검사기 자체 수정까지 막는 보안 시스템은 아니다.

[설계](../design/검사-누락방지-hook-설계.md) · [검증 기록](../reviews/검사-누락방지-hook-검증.md).

후속 수정: [Stop 반복 알림 제한 설계](../design/검사-누락방지-hook-설계.md#ch-hg-loop-20261101--stop-재호출-알림-제한) · [검토 기록](../reviews/stop-hook-loop-20261101.md). OpenAI [Stop 입력/응답 문서](https://learn.chatgpt.com/docs/hooks) 확인2026-11-01.

2026-11-01 [034 변경 범위 선택 설계](../design/verification-scope-034.md) · [검토/실행 기록](../reviews/verification-scope-034-review.md). 현재 재실행 조건은 이 문서가 원본이며 과거 hook 설계/검증의 매번 local·MAP007·전체 화면 규칙을 대체한다.
