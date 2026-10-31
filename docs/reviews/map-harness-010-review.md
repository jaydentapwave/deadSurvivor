# MAP-HARNESS-010 H1 기초 구현 검토

2026-10-31 KST · L2 · [구현 범위와 판정 기준](../design/map-harness-h1-foundation.md)

이번에는 설계에서 실제 코드로 넘어가 **입력·도형 검사와 독립 충돌 판정의 기초**를 구현했다. H1 전체, Unity 맵 실행 하네스, 최종 3D 맵 완료를 뜻하지 않는다. 외부 생성 도구는 [CLI/API 기본 방식](../decisions/3d-cli-workflow.md)으로 결정했다. API 잔액은 재조회 시 0이었고 새 생성·구매·공개 업로드는 하지 않았다.

## 구현 내용

- `Tools/Verify/map_io.py`: 중복 키·비유한 수를 거부하는 JSON 읽기, 상대 경로 경계, 상위 경로를 포함한 심볼릭 링크 거부, 1MB 입력 한도.
- `Tools/Verify/map_oracle.py`: 단순 다각형 유효성·방향 정규화와 원형 몸체의 전체 이동 선분 검사. 중심점과 몸체 반경을 구분하고, 얇은 벽 관통·오목 경계 이탈 후 재진입·오목 장애물의 빈 부분을 판정한다. 정확한 유리수 위상 계산과 거리 구간 증명으로 PASS/FAIL/BLOCKED를 구분한다.
- `Tools/Verify/map_check.py`: 기존 009 예제의 분석 정답 8개를 시험하는 `oracle-selftest`. 각 관찰값·기대값, 실행 시각, 입력·모듈 SHA256과 NOT_RUN인 Unity 범위를 새 폴더에 기록한다.
- `Tools/Verify/run.py`: 기존 local/all에 `map_oracle_foundation`을 추가했다. 기존 pre-commit/Stop gate가 실행하는 local에서도 이 검사를 수행하며 실패를 전파한다. 전체 request 실행 명령인 `run.py map`을 구현한 것은 아니다.
- `test_map_foundation.py`: 불량 입력, 안전해 보이는 두 끝점 사이의 벽 관통, 몸체 침투, 접촉/epsilon, 회전·역방향·시작 꼭짓점 변경, 독립 직사각형 정답 비교 등 23개 시험.

지원 범위는 좌표 절댓값 1024 이하, 양수 반경/epsilon 1e-12~1024다. 미지원 입력을 통과시키지 않으며 hole/multiple boundary·지나치게 작은 비영 길이는 BLOCKED다. witness의 여유 값은 특정 지점의 침투 증거이며 전 구간의 최솟값으로 주장하지 않는다.

## 독립 검토와 수정

| 검토 | 최초 지적 | 반영 및 최종 상태 |
|---|---|---|
| map010_design | 수치 범위·underflow 처리 근거 부족 | 지원 범위와 구간 증명을 설계에 명시, PASS |
| map010_math | 좌표 500 제한은 기존 500.1 반례를 배제 | 1024로 수정. 구현의 교차 분할·signed gap·구간 계산과 분석 근거 검토 PASS |
| map010_validator | 해시를 구하며 입력을 제한 없이 먼저 읽음; 입력 오류 보고서 누락 | 해시도 안전한 제한 읽기를 사용하고, 출력 가능한 입력 실패는 보고서에 저장. 재현 시험 추가, PASS |
| map010_validator | 앞선 FAIL을 뒤의 BLOCKED 입력 오류가 덮음 | FAIL 우선 보존 및 재현 시험 추가, PASS |
| map010_validator 후속 | 기존 hook 시험의 임시 저장소가 실제 설치 이후 Unity 자산 변경에 의존 | 임시 복제본 정책만 설치 직후 fixture로 구성. 실제 정책/gate/gate_support는 유지. 범위·36개 시험·현재 지문 재검토 PASS |

세 독립 역할은 읽기 전용으로 검토했고 메인이 구현·수정·실행했다. validator-client 최종 판정은 **스펙 준수 PASS / H1 기초 동작 계약 PASS**다. 실제 Unity 맵 실행/G2 성공으로 확대하지 않았다.

## 실행 결과와 근거

| 검사 | 상태 | 근거·범위 |
|---|---|---|
| I0 현재 코드/데이터 대조 | PASS | [24개 파일 비교](../../output/verification/map-h1-i0-20261031/source-comparison.json). 차이 없음, 자동 동기화 없음 |
| 최신 단위·hook 통합 | PASS | [36개 시험](../../output/verification/map-h1-unit-20261031-141312-a630f43b/summary.json), [실행 로그](../../output/verification/map-h1-unit-20261031-141312-a630f43b/stderr.txt). 14:13:12 시작, 기초 23개+hook 13개. 검사기 11개 지문 기록 |
| 독립 기하 분석 CLI | PASS | [8개 분석](../../output/verification/map-oracle-20261031-141241-597f7106119a4331969f592ec3876ff9/map/summary.json). 실제 Unity 상태 NOT_RUN |
| 기존 게임 회귀 | PASS | [all --screens](../../output/verification/20261031-141344-c3389907/summary.json), 14:13:44 시작. 현재 검사기 지문·소스 안정성, 컴파일, 695 규칙, 실제 입력, 프레임 6→54, 3비율 24상태 캡처·자동 검사, 콘솔 오류0, 정리 PASS |
| 실제 화면 시각 확인 | PASS | 메인이 [1280×720 정비](../../output/verification/20261031-141344-c3389907/unity/prep-1280x720.png), [1024×768 최대 장착 전투](../../output/verification/20261031-141344-c3389907/unity/battle-full-1024x768.png)를 확인. 24장 전부의 수동 검수로 주장하지 않음 |
| H1 전체 request/원자료 인수·집계·FI04/05/06/09 | NOT_RUN | 후속 구현 필요 |
| H2~H4 실제 맵 입력·이동 해결기·복구·G2 전체 | NOT_RUN | 기존 게임 회귀와 별개 |
| 최종 3D 맵·새 Tripo CLI 생성·실기기 | NOT_RUN | 이번 구현 범위 밖 |

실제 Unity 대상은 `/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor`, 6000.4.3f1이다. 기존 게임 씬·중지 상태·미저장 변경 없음·background=false를 확인한 뒤 회귀를 실행했다. 회귀 뒤 Play와 background를 복구했으며, 자동 tick은 이전 작업에서 이미 활성화된 16ms 설정을 유지했다. 게임 소스·씬·배치·바위 자산을 새로 변경하지 않았고 회귀 캡처만 새 실행 폴더에 생성했다.

최초 통합 단위 실행은 [33개 중 hook fixture 1개 FAIL](../../output/verification/map-h1-unit-20261031-141009-2438475c/summary.json)이었다. 임시 복제본에 실제 설치 baseline을 복사한 상태에서 현재 Unity 자산이 달라 완전한 새 증거를 요구한 것이 원인이다. 임시 시험만 설치 직후 상태로 구성하도록 고쳤다. 운영 정책의 baseline은 갱신하지 않았고, 실제 프로젝트는 위 새 Unity 회귀 증거를 사용한다. 런타임 변경·오래된/불완전한 증거 거부 시험은 유지했다.

최종 `/usr/bin/python3 Tools/Verify/gate.py check`의 상태·시각·보고서 지문은 [최종 실행 기록](../../output/verification/map-h1-final-20261031/evidence.json)에 남긴다. gate의 분석/준비 자료 PASS는 NOT_RUN인 실제 맵/G2를 승격하지 않는다.

다음 구현은 H1 전체 데이터 인수·원자료/결과 집계와 실패 주입을 완료한 뒤, H2의 격리 시험 씬·게임 입력/관찰 경로로 연결하는 순서다. Tripo API 잔액은 새 모델 생성의 조건이며 이 맵 코드 구현의 선행 조건은 아니다.
