# MAP-HARNESS-009 구현 설계 검토

2026-10-31 KST. L2 구현 설계 작성 및 독립 검토. 실제 도구/게임 코드 구현·Unity 조작은 수행하지 않았다.

대상: [구현 설계](../design/map-harness-implementation.md), [데이터 계약](../design/map-harness-data-contract.md), [분석 시험 자료](../design/map-harness-fixtures.json). 규칙 원본은 007 MP/하네스 및 기존 G2~G5 필수 목록이다.

## 독립 검토와 수정

| ID | 발견한 문제 | 반영 | 재확인 |
|---|---|---|---|
| HD-01 | UI가 상태를 직접 변경해 단일 프레임 관찰을 우회할 수 있음 | UI/단축키 변경 대상, 공통 큐 cut·순서·중복/nonce, 기존 동기 호출 호환성 명시 | harness009_design PASS |
| HD-02 | Play reload 전후 메모리 reference/token이 유효하지 않을 수 있음 | journal/marker 기반 재바인딩·소유권·종료 reload 복구와 중복 콜백 처리 | harness009_design PASS |
| HD-03 | 맵 미사용과 맵 데이터 누락의 의미 충돌 | LegacyNoMap/MapRequired 분리 및 누락/중간 binding 소실 차단 | harness009_design PASS |
| AM-01 | 중심 거리와 몸체 여유가 모호하고 epsilon 경계가 다름 | d/g/G 정의, 반경 포함 oracle 반례, solver 2epsilon과 oracle epsilon, witness와 최소 여유 구분 | harness009_math PASS |

위 최초 지적은 설계 단계에서 확인한 문제다. 구현 버그를 고쳤거나 런타임 시험이 통과했다고 보고하지 않는다. 선택적 단위 명료화도 반영해 replay 위치 오차와 시간 오차를 분리했다.

## 실행·검토 상태

| 검사 | 상태 | 근거/범위 |
|---|---|---|
| 로컬/실제 대상 소스 대조 | PASS | [24개 파일 바이트 비교](../../output/verification/map-harness-design-20261031-130446/source-comparison.json), 실행 시각과 각 SHA256 포함. Editor 현재 상태/컴파일은 미확인 |
| 초안 예제 산술 | PASS | [8개 정적 확인](../../output/verification/map-harness-arithmetic-20261031-130837/summary.json). 실제 solver/oracle/Unity 실행 아님. 후속 수정 전 기록 |
| 초안 gate | PASS | [새 실행](../../output/verification/gate-20261031-130825-a6a7184c/gate.json). 문서/준비 자료 검사 |
| 독립 설계 | PASS | harness009_design, 최초 CHANGE_REQUIRED 후 HD01~03 해소 |
| 독립 계산/판정 설계 | PASS | harness009_math, 최초 AM01 FAIL 후 수정 계약 재확인. 실제 계산 실행은 NOT_RUN |
| 최종 문서 validator-client | PASS | harness009_validator: 스펙 준수 PASS / 문서상 동작 계약 PASS, 필수 수정 없음 |
| Unity 컴파일/실제 입력/프레임/하네스 G2 | NOT_RUN | 이번 범위는 설계. G2 전체 구현·검증 필요 |
| 실제 맵 G3~G5 | NOT_RUN | 실제 C·수치·입력열 보정 후 별도 인수 |

최종 보고서에는 현재 설계/시험 자료 지문을 남긴다. 이전 게임 PASS·과거 MAP007 지문을 현재 하네스 실행 증거로 사용하지 않는다.

최종 설계 예제/소스 재확인: [19개 정적 검사 PASS](../../output/verification/map-harness-design-final-20261031-131045/summary.json). 중심 거리·반경을 뺀 몸체 여유·epsilon 경계·얇은 벽 관통과 24개 소스/대상 불변을 확인했고 현재 핵심 설계 세 파일의 지문을 남겼다. [실행 경계 결정](../decisions/map-harness-execution-boundary.md)도 기록했다.

최종 문서 반영 후 `/usr/bin/python3 Tools/Verify/gate.py check`를 다시 실행한다. 최종 gate.json은 해당 실행 시각·전체 입력 지문을 소유하며 문서 검토 PASS와 실제 하네스 실행 결과를 분리한다.
