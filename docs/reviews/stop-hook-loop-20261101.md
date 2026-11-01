# Stop 반복 알림 제한 검토

- 날짜: 2026-11-01 / L1.
- 사용자 요청: 같은 프로젝트 필수 검사 BLOCKED 메시지가 반복 표시되지 않도록 조정.
- 원본: [CH-HG-LOOP-20261101](../design/검사-누락방지-hook-설계.md), [검증 gate](../workflows/verification-gates.md), [OpenAI Hooks](https://learn.chatgpt.com/docs/hooks).
- 변경 범위: Stop 재호출 응답·회귀 시험·로컬 설명. 게임 코드·.codex/hooks.json·Git hook·합격 기준/baseline·전역 설정·신뢰 DB는 수정하지 않는다.
- 최초 실패는 기존 block, 재호출도 모든 검사/실패 기록을 유지하고 자동 후속 프롬프트만 억제한다. 검사·기록 자체 실패는 계속 차단한다.
- 범위별 결과: Stop 응답 수정의 독립 코드·회귀/직접 호출 증거 검토 PASS. 전체 Unity 회귀 FAIL·현재 gate BLOCKED는 아래 별도 기록한다. 실제 앱의 다음 Stop 수신은 NOT_RUN이다.

## 실행과 독립 검토

- local PASS: `output/verification/20261101-170537-e9ce0c94/summary.json`.
- 회귀15개 및 실제 임시 Git/Stop 호출 PASS: `output/verification/hook-tests-20261101-170732/summary.json`, tests.txt. 최초 실패 block, 재호출 검사/새 실패 영수증과 빈 응답, 정상 응답, 잘못된 flag/event/cwd, 기록 실패, 커밋 거부·허용, 과거/변조 증거·인덱스 거부를 확인했다.
- 최초 회귀 실행은 임시 fixture가 실제 Desktop 대상 지문을 읽어 동시 변경에 영향받은 실패였다. fixture 복사본의 대상만 해당 임시 프로젝트로 분리해 재현성을 보완했다. 실제 gate_support.py·baseline은 변경하지 않았다.
- play_validator 독립 코드/회귀 증거 검토 PASS. 기준/실패 기록/수동·커밋 차단 유지, 재호출 응답 제한을 확인했다.
- 전체 회귀 첫 시도: Snapshot 읽기 중 Unity CLI socket hang up으로 BLOCKED. 장면 전환 전이어서 쓰기/원상 복구 대상 없음. 동일 PID2106 Editor ready를 확인했으며 현재 전체 검사 증거는 아직 미확보 상태다. 복구된 연결에서 전체 회귀를 한 차례 재실행했고 그 결과는 아래 최종 범위별 결과를 따른다.
- 실제 앱의 다음 Stop 이벤트 수신·표시는 NOT_RUN. 실제 훅 정의/신뢰 저장소를 수정하거나 자동 신뢰하지 않았으며, 이번 변경은 같은 명령이 읽는 프로젝트 스크립트에 적용된다.

## 최종 범위별 결과

- 연결 복구 뒤 전체 게임 회귀: `output/verification/20261101-170924-9d61cea8/summary.json` FAIL. 컴파일·규칙·입력·프레임·기본 캡처는 PASS였지만 화면 비율 검사에서 `GameView mismatch RT=1280x720 layout=(3088,1440) ... native=841x1773`가 발생했다. 이 Stop 응답 수정으로 게임 화면 문제를 해결했다고 주장하지 않는다. 원래 장면/선택 복원은 `output/environment-preproduction-doc/all-context/restored.json` PASS다.
- 실제 개인 프로젝트의 유효 재호출 입력 직접 실행: `output/environment-preproduction-doc/stop-hook-real-response.json` PASS. 실제 검사 상태 BLOCKED와 새 gate.json을 유지한 채 stdout은 `{}`, `continuation_requested=false`였음을 확인했다. 네이티브 앱의 다음 이벤트 수신 시험과 구분한다.
- 첫 알림은 한 번 유지하며 같은 Stop의 재호출 알림을 제한한다. 실패를 기록하지 않거나 PASS로 바꾸는 방식이 아니다. 수동/커밋 검사의 실패 차단과 검사 자체 오류의 차단은 유지된다.
- 현재 전체 프로젝트 gate는 Unity/현재 지문 증거 미충족으로 BLOCKED일 수 있다. 이번 L1 기능의 코드·15회귀·직접 Stop 검사는 PASS, 전체 Unity 회귀는 위 FAIL로 각각 보고한다. 자동 알림 제한을 전체 프로젝트 인수 완료로 보고하지 않는다.

최종 독립 증거 검토: play_validator가 현재 코드 지문·15회귀·실제 재호출 실패 기록/빈 응답·수동 gate 차단·Unity 실패와 복원 분리를 확인해 알림 제한 범위 PASS. 전체 프로젝트 완료/PASS는 주장하지 않는다.


## r002 보완 — 최초 알림도 자동 재개하지 않음

r001의 최초 block 정책은 일반 질문 종료에도 다시 경고를 띄우므로 사용자의 ‘안 뜨게’ 요청을 충분히 충족하지 못했다. r002는 정상 검사/기록 뒤 최초·재호출 모두 `{}`를 반환하고 실제 실패를 보존한다. check/pre-commit 및 잘못된 입력/기록 실패 차단은 유지한다. 앞선 ‘최초 알림 한 번 유지’는 r001 이력이며 현재 동작은 이 절을 따른다. 현재 개정의 회귀·독립 검토 결과는 별도로 확인한다.

### r002 실제 결과

- 15개 회귀/격리 Git·Stop 시험 PASS: `output/verification/hook-tests-20261101-171614/summary.json`.
- 실제 프로젝트 직접 호출 PASS: `output/environment-preproduction-doc/stop-hook-real-r002.json`. 최초/재호출 모두 새 BLOCKED 영수증을 보존하고 `{}`, `continuation_requested=false`, `notification_policy=record_only`를 확인했다. 실제 앱 UI 시험으로 확대하지 않는다.
- local PASS: `output/verification/20261101-171649-72a8c710/summary.json`.
- 수동 gate BLOCKED 유지: `output/verification/gate-20261101-171649-e16bfda2/gate.json`. 최신 전체 Unity 증거 부재를 조용한 Stop 응답으로 PASS 처리하지 않았음을 확인했다.
- play_validator가 r002 코드/스펙·15회귀를 독립 확인해 알림 응답 범위 PASS. 원본 훅 정의·Git hook·baseline·gate_support.py는 변경하지 않았다.
