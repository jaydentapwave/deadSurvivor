# EFFICIENCY-013 구현 검토

2026-10-31 KST · L2 · [설계](../design/efficiency-013.md).

기본 도구 출력을 요약하고, 독립 코드 검토 후 통합/최종 전체 검사 순서를 적용하며, 자산 원본/Blender 처리의 재사용 범위를 분리했다. 전체 보고서·실패 원자료·종료 코드·기존 gate/hook/baseline과 L1/L2 독립 검토 의무는 유지했다. 모델·전역 설정·게임 규칙 변경은 없다.

## 독립 검토

`/root/eff013_design` 사전 설계 PASS. `/root/eff013_validator`의 첫 구현 검토에서 아래3건을 받아 수정했고 코드 재검토는 스펙/동작 계약 모두 PASS다. 메인이 구현했으며 검토자는 로컬 파일을 읽기 전용으로 확인했다.

| 지적 | 수정·검증 |
|---|---|
| V13-01 기존 screens.checks 문자열 배열에서 요약 예외 | dict/list 체크 구조 처리. 기존 실제 all 보고서와 동일 배열 형태 회귀 시험 |
| V13-02 PASS 하위의 unity_status/visual_review_status NOT_RUN 누락 | 명시적 범위 상태를 별도 요약 행/문제 수에 포함. 상위 판정은 재판정하지 않음 |
| V13-03 null 처리 의존성 값이 RAW_ONLY 승계되거나 예외 | 중첩 dict·절대 경로·SHA-256 형식 검증. null/상대 경로/잘못된 해시6개 반례 |

## 실제 증거

- [단위 시험](../../output/efficiency-013/unit-results.json): 자산37개, Verify45개(기존 gate/맵 시험 및 요약9개) PASS. 실행 전후 소스 지문 동일. 초기 중간 시험은 현재 증거로 사용하지 않는다.
- [실제 바위 재사용](../../output/efficiency-013/cache-integration.json): 과거 schema1 원본만 새 schema2로 승계하여 최초 Blender 처리1회, 다음 새 job은 처리본까지 재사용하여 Blender0회. raw/processed 모든 파일 지문 동일, 과거 job 변경0, 추가 생성0.
- [재사용 바위 Unity 결과 위치](../../output/efficiency-013/unity-transfer.json): 저장 프리팹 계약·실제 프레임·카메라 캡처·콘솔·상태 복구·기존 파일/원본/Unity 도구 지문 PASS. 원본 바위의 TRIANGLE_BUDGET FAIL은 의도된 불량 탐지로 별도 보존했다.
- [최종 게임 all --screens](../../output/verification/20261031-151718-d33326c1/summary.json): 코드 검토/통합 뒤1회 실행하여 컴파일·695규칙·입력·3비율24화면·콘솔·복구·소스/대상 안정성 PASS.
- [동일 보고서 출력 비교](../../output/efficiency-013/output-comparison.json): 기존 stdout 4,505 → 새 요약 694 UTF-8바이트(84.6% 감소). 전체 JSON은 그대로 보존했고 미실행 범위도 표시한다. 실제 토큰 절감률은 측정하지 않았다.
- 최종 local/gate 및 종합 근거는 `output/efficiency-013/final-checks.json`에 최종 상태·시간·경로·해시로 기록한다. 최종 실행 후 독립 검토자가 증거를 확인한다.

## 재사용 경계와 한계

schema2 처리 지문은 동결 spec/원본에 더해 공통 판정기와 처리기 코드, Blender 실행 파일 경로/바이트를 확인한다. Unity 코드/별도 표시 코드 변경은 처리 결과를 무효화하지 않는다. 공통 pipeline 변경은 보수적으로 재처리를 요구한다. 처리 의존성이 정상 형식으로 달라지면 raw만 승계하고, 손상/불완전/잠금/복사 중 변경은 BLOCKED다. 과거 schema1 지문을 다시 쓰지 않는다.

Unity 결과는 자동 재사용하지 않는다. 새 실행의 Unity 도구 지문을 첫 명령 전에 고정하고 종료 시 재확인한다. 자산 처리 캐시를 게임/맵 PASS로 승격하지 않는다. 최종 미술·실제 API·모바일 성능·A5/G5는 이번 변경의 NOT_RUN 범위다. 게임 기하 계산을 바꾸지 않아 별도 algorithm-tester 대상은 없다.

절약 측정은 동일 보고서의 예전 stdout 표현과 새 기본 요약의 UTF-8 바이트, 실제 Blender 호출 수를 비교한다. 실제 모델 토큰 사용량/과금은 계측하지 않았으며 바이트 감소율을 토큰 절감률로 표현하지 않는다.
