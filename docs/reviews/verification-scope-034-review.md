# VERIFICATION-SCOPE-034 검토 기록

2026-11-01 · 사용자 요청에 따라 필수 검사 재실행 조건과 gate를 개선한다.

## 재검토 결과

불필요한 반복은 마감 local+gate 내부 local, 변경 없는 H1/MAP007 준비 재검사, 무관한 Tools/Verify 파일 변경으로 인한 Unity 전체 회귀였다. 정적 검사를 네 묶음으로 분리해 같은 의존성의 검증된 원자료를 재사용한다. 계산 파일 내용 변경은 기존 전체 화면 근거가 유효할 때 현재 기본 Unity 회귀로 인수한다.

경로/소스 안정성·인덱스 일치, 현재 runtime 지문, 컴파일/규칙/실제 입력·프레임/기본 캡처·복구는 유지한다. UI·씬·자산·설정·추가/삭제·알 수 없는 변경은 전체 화면을 요구한다. 설치 baseline, hook 정의, 게임 코드와 수집기 run.py/unity_check.py/gate_support.py는 변경하지 않는다. 필수 검사가 없으면 실행하거나 BLOCKED이며 성공으로 대체하지 않는다.

## 독립 검토와 시험

- 사전 설계: GPT-6.1 Sol medium 독립 design-reviewer PASS, 필수 지적 없음.
- 구현 검토: 수집/판정 혼재로 인한 의존 누락 지적을 반영해 수집 원문을 보존하고 gate_evidence로 판정을 분리했다. 034-VC-01 해소를 같은 독립 검토자가 확인했고 구현 코드·스펙 재검토 PASS, 추가 필수 지적 없음. 최종 실행 근거 확인 결과는 아래 로컬 마감 기록에 함께 보존한다.
- 관련 단위/격리 Git hook 시험: [r002](../../output/verification-scope-034/tests-r002.json) 25개 PASS(실패/오류 0). Stop 최초/재진입, 실제 격리 pre-commit 차단, 중간 소스/인덱스 변경, 선택/재사용/손상/실패/기본·화면 조건을 확인했다. r001도 통과했으며 r002는 034-VC-01의 import/수집 지문 변경에 따른 재실행이다.
- 실제 첫 gate: [cold 실행](../../output/verification/gate-20261101-222256-322700e4/gate.json) PASS. 정적 네 묶음 RUN, 기존 Unity 원자료 REUSE. 새 Unity 시험으로 표시하지 않았다.
- 마감 문서 변경 후 gate의 실제 상태·실행/재사용 목록과 원자료는 [마감 기록](../../output/verification-scope-034/final-checks.json)에 보존한다. 기대 동작은 docs만 RUN, 나머지 세 정적 검사와 runtime 근거 REUSE다. 무변경 warm 전체 재사용은 위 격리 시험으로 확인했으므로 실제 세 번째 동일 실행을 추가하지 않는다.
- Unity 신규 실행·전수 topology·전체 Python suite: NOT_RUN. 이번 변경의 대상이 아니며 실제 수집 경로를 변경하지 않았다.
- 전체 N1/G2/G3/G4/G5·실기기·Tripo 생성 인수: 이번 작업으로 완료하지 않는다.

[설계](../design/verification-scope-034.md) · [현재 gate 규칙](../workflows/verification-gates.md) · [실행 증거](../../output/verification-scope-034/final-checks.json).

시간/토큰 절감률은 미측정이다. 실제 효과는 검사별 RUN/REUSE와 원자료 경로로 확인한다.

## 적용 범위와 사용

마감 명령은 기존과 같은 /usr/bin/python3 Tools/Verify/gate.py check다. 실행/재사용한 검사를 짧게 출력하며 원자료와 이유는 gate.json에 남긴다. 코드와 문서 변경을 직접 적용했고 커밋·업로드는 수행하지 않았다. 기존 미커밋 게임 작업은 시작 지문 대비 불변을 확인해 마감 기록에 남긴다. 이 개선은 gate가 이미 담당하던 검사의 실행 범위 조정이며 전체 맵·자산 승인 하네스 구현을 추가하지 않는다.
