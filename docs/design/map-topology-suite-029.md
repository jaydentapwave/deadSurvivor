# 029 — 12개 파티·진형 연결성 진단 집계

- 상태: 독립 설계·코드·계산 검토 PASS, 49단위 시험 PASS 후 실제12조합 실행 중. L2: 판정 파일/봉인·상위 집계 계약을 추가한다.
- 요구: 전체 작업의 미완료 N1을 계속한다. N2 실제 빌드·테스트는 사용자 실행으로 제외한다. 028 Editor 체크포인트는 보존한다.
- 원본: [027](map-topology-027.md), [024 N1](map-next-steps-024-plan.md), [026](map-full-026.md). 전체 G2/G3 완료와 분리한다.
- 메인이 구현한다. 독립 설계/계산/코드 검토는 읽기 전용이다. 외부 서비스·Unity 조작·원본 C·씬·hook·baseline 변경 없음.

## 확인한 입력과 문제

현재 native 수치 프로필은 [027/r002](map-topology-runtime-profile-027.r002.json)다. 전체64개 C와 경계·12개 진형을 원본과 대조했다. 새 진단에서 N2/F1 및 N3/F0가 각각 단일 성분으로 인증됐지만 12조합 전체 결과는 아니다. 출력/최종 소스 검사 실패 뒤 geometric_connectivity=PASS가 남을 수 있는 보고 결함을 수정한다. 과거 결과를 덮지 않는다.

## 변경과 소유

1. `map_topology.py`: 계산은 그대로 유지한다. 최종 결과가 PASS가 아니면 승인용 연결/성분 수를 차단하고 계산 중 후보는 unaccepted 필드로만 보존한다. 출력3종(decomposition/incidences/unresolved)의 SHA와 크기를 최종 소스/예산 검사 후 봉인한다. 봉인 실패·요약 기록 실패는 PASS를 남기지 않는다.
2. `map_topology_suite.py`/시험: N1-F0부터 N4-F2까지 12개 ID를 실행 전에 manifest에 동결한다. producer·oracle/io·프로필/raw·원본/layout·native 의존 SHA를 고정한다. 새 폴더에서 최대2개 Python 자식만 병렬 실행하며 각 진단의 wall budget과 부모 timeout을 분리한다. Unity 자원을 취득하지 않는다. timeout 시 계산 자식만 종료하고 partial을 보존하며 BLOCKED다.
3. 상위 judge는 중복/누락/빈 집합/이전 프로필·오래된 코드·원자료 변조·미해결/미봉인 결과를 거부한다. 현재 조합별 count/formation, V(V-1)/2의 쌍 완전성, E-V+beta0-rank(d2) 수식, 소스·artifacts SHA를 대조한다. 조건을 소급 바꾸지 않는다.

## 상태와 인수

- 역할 `TOPOLOGY_SUITE_DIAGNOSTIC_029`; 각 자식은 `TOPOLOGY_DIAGNOSTIC_ONLY`. G2/G3/실제 추종·전환·경로·화면은 NOT_RUN이다.
- `certification_status`(모든 incidence 인증/원자료 유효)와 `connectivity_status`(12개 모두 한 성분 및 P01 안전)를 구분한다. 전체 status는 FAIL > BLOCKED > NOT_RUN > PASS다. 수학적으로 인증된 불연결도 진단 FAIL로 보존하며 누락/미해결을 0성분으로 대체하지 않는다.
- 이전 결과 재사용은 이번 첫 실행에서 하지 않는다. 동일 기하의 조합도 12개 모두 새로 실행해 입력/증거 집계를 확립한다. 향후 캐시에는 같은 cell/radius/코드 signature 증명이 필요하다.
- standalone judge는 새 실행으로 가장하지 않고 검증 시각·원래 실행 시각을 구분한다. 실행 중 입력·코드가 바뀌면 봉인/집계를 거부한다.
- 부분 실행/파일쓰기 실패/프로세스 종료는 원자료와 이유를 보존한다. 이 순수 Python 작업의 정리를 Unity 외부 복구/FI08 성공으로 부르지 않는다.

## 시험·복구

late source/output failure 뒤 PASS 필드 잔류, 12행 누락/중복/0건, 다른 count/formation/profile, 변조/누락 artifact, 잘못된 완전성/성분 수, 늦은 소스 변경, timeout 상태를 반증한다. 단위→독립 검토→실제12조합→독립 근거 검토→local/필요한 현재 all/gate 순서다. 계산 알고리즘을 바꾸지 않아도 검증기 지문이 바뀌므로 필요한 최종 all 증거는 새로 확보한다.

## TS029-01~03 보완

- TS029-01: 부모 run UUID·case ID·각 launch nonce를 실행 전 manifest에 고정한다. 자식 launch JSON은 부모 manifest 경로/SHA·프로필·count/formation·deadline을 연결하고 자식 인수/요약 inputs에 두 파일의 SHA를 넣는다. 부모는 stdout·실제 자식 PID/exit와 invocation을 대조하며 중복 자식 디렉터리/이전 실행을 거부한다.
- TS029-02: decomposition/incidences/unresolved의 정확한 파일명·바이트 수·SHA를 고정한다. 소스 재검사와 마지막 budget 검사를 통과한 뒤 `completion_marker=SEALED`를 넣은 summary를 atomic rename으로 **마지막** 공개한다. 실패 시 연결성/P01/승인 성분 수를 차단하고 계산 중 값은 unaccepted에만 남긴다. 부모는 exit0·일치하는 launch·SEALED·세 파일 모두를 요구한다. timeout/비정상 종료/누락 summary는 BLOCKED 또는 FAIL이며 후보값으로 PASS를 복구하지 않는다.
- TS029-03: 부모는 원본에서 피복/frame을 재생성해 decomposition/cell SHA와 비교하고, exact AABB의 모든 쌍·중복 없는 incidence ID·positive edge의 모든3-clique를 전개해 완전성을 확인한다. 그래프의 beta0와 GF2 rank를 별도 집계하여 수식/성분 수를 대조한다. 원판-다각형 incidence의 기하 판정은 고정 SHA의 검토/반례 시험된 027 kernel을 신뢰한다는 경계를 명시한다. 해시만으로 기하 정답을 독립 증명했다고 부르지 않는다. 불연결은 certification PASS/connectivity FAIL, 증거 불량/미해결은 connectivity BLOCKED다.

모든 자식 종료 후 최종 봉인 전에 부모의 고정 의존을 다시 검사한다. 독립 judge는 기존 실행 요약을 덮지 않고 새 판정 파일에 원래 실행 시각과 재판정 시각을 분리한다.
