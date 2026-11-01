# MAP-TOPOLOGY-027 r001 — 전체 자유 지면 성분 인증 설계

- 날짜: 2026-11-01. 규모 L2(독립 기하·판정 자료 계약). 상태: 구현 전 검토.
- 요구: [024 N1](map-next-steps-024-plan.md)의 전체 자유 지면 조사를 [026](map-full-026.md)에 연결한다. 거친 격자 연결이나 근사 성분 수 일치만으로 작은 고립 영역을 누락하지 않는다.
- 산출물: `Tools/Verify/map_topology.py`, `test_map_topology.py`, 실행마다 새 output/map-full-026/topology-<id>/의 입력/소스 지문·분해·incidence 증거·미해결 목록·성분 수.
- 메인이 구현·실행한다. design-reviewer/algorithm-tester/validator-client는 읽기 전용 검토다. 런타임·007/018 원본·씬·hook·baseline 변경 없음. 외부 설치/서비스 없이 Python Fraction을 사용한다.

## 수학적 근거와 이 작업의 적용 추론

[Edelsbrunner/Harer, Computational Topology](https://webhomes.maths.ed.ac.uk/~v1ranick/papers/edelcomp.pdf)의 III.2(유한 닫힌 볼록 집합의 nerve)와 V.4(Alexander duality)를 참고한다. 원문 자료의 정리를 아래 유한 원판-다각형 구성공간에 적용하는 부분은 에이전트 설계 추론이며 구현/시험으로 검토한다. 원문 전체를 복사하거나 프로젝트 정보를 외부에 제출하지 않는다.

각 고정 파티/진형의 중심과 모든 예약 슬롯 offset(사망 포함)에 대해, 장애물과 경계 외부를 -offset 이동하고 R=bodyRadius+2×runtimeEpsilon만큼 팽창한다. 그 합집합 U의 여집합이 기하적으로 안전한 중심 공간 F다. 실제 뒤처진 몸체의 추종 상태 연결과 진형 전환은 별도 실제 sweep/전환 사례다.

1. 입력 단순 다각형과 경계 외부를 정확한 유리수 vertical slab으로 닫힌 볼록 사다리꼴/삼각형/선분/점의 합집합으로 분해한다. 모든 정점 x를 절단점으로 사용하고 slab 중점의 정확한 교차 순서를 판정한다. 원본 경계 선분/점도 보존해 수직 변·퇴화 부분을 빠뜨리지 않는다. 포함된 중복 cell 제거에는 정확한 포함 증거가 필요하다.
2. 각 볼록 cell과 닫힌 반경R 원판의 Minkowski 합은 볼록이다. 유리수 원 위 점(Pythagorean parametrization)의 내접 다각형과 인접 접선 교점의 외접 다각형을 생성한다. 원 포함/포함됨을 제곱 거리·접선 부등식으로 검증하고, 각각의 Minkowski 합은 정확한 convex hull로 만든다.
3. 모든 cell 쌍과 세 변이 있는 모든 삼중 교집합을 인증한다. 내접 교집합의 유리수 witness는 실제 교집합의 존재를 증명한다. 외접 교집합이 정확히 비면 실제도 비다. 불명확하면 정해진 분할 단계로 정밀화하고, 마지막에도 불명확하면 BLOCKED다. 점·선분 접촉을 빈 교집합으로 버리지 않는다. AABB 제외도 정확한 유리수 경계로만 수행한다.
4. 인증한 nerve의 V/E/삼각형을 사용해 GF(2) 경계행렬 rank를 계산한다. `beta1 = E - V + beta0 - rank(d2)`이고 beta0는 실제 nerve 그래프 성분 수다. 더 높은 simplex는 H1에 영향을 주지 않으므로 삼중 incidence까지만 필요하다. 미해결 incidence가 있으면 전체 성분 수 PASS 불가다.
5. compact planar U의 beta1은 그 여집합의 유계 성분 수다. 아래 frame 대응이 성립할 때 이 수가 실제 F의 성분 수다. beta1=0을 연결 성공으로 해석하지 않는다. beta1=1과 안전한 P01을 확인해야 비어 있지 않은 기하 공간 전체 연결을 주장할 수 있다.

## 유한 외부 frame 대응

모든 이동된 경계/장애물 정점과 R보다 충분히 큰 여백을 포함한 공통 직사각형 B를 동결한다. 각 이동된 경계 Pi에 대해 **B에서 Pi 내부를 뺀 닫힌 외부**를 먼저 분해하고 그 cell들을 팽창한다. 바깥을 사각 띠 하나로 대체하지 않아 오목한 외곽의 금지 지면도 포함한다.

B 안의 점이 Pi 내부에 있으면 가장 가까운 Pi 외부는 B 안의 Pi 경계에 있다. 이미 Pi 외부인 점은 그 자체가 분해 외부에 속한다. 그러므로 B 내부에서 이 유한 팽창과 실제 무한 외부의 팽창이 일치한다. 모든 Pi와 장애물은 B 경계에서 R보다 멀리 있으므로 B 밖의 합집합은 공통 convex `B+diskR`의 바깥 띠를 채운다. B 밖에 인공 유계 구멍을 만들지 않고, 유일한 무계 여집합은 `B+diskR` 바깥이다. 따라서 유계 여집합 성분은 B 안의 실제 F와 일치한다. 구현은 이 margin·coverage 조건을 검사하며 frame 크기 반복 일치만으로 이를 대신하지 않는다.

## 입력·성공·실패 경계

- 대상 전체64개 C와 PERIM-01, 12개 인원/진형. 입력은 원본 ID·좌표/hash와 수치 표현을 동결한다. 007 후보 진단과 우리 채택 C 인수를 구분한다.
- epsilon은 C# float를 double로 승격한 실제 표현을 IEEE754 변환으로 재현해 Fraction에 고정한다. 소수 .001을 임의의 정확 유리수로 대체하지 않는다. 각 offset도 실제 카탈로그와 독립 명세를 대조한다.
- 준비/실행 역할은 TOPOLOGY_DIAGNOSTIC_ONLY, 전체 G2/G3 인수는 NOT_RUN이다. 인증이 성공해도 실제 경로/정비/생사/화면 검증을 대체하지 않는다.
- 출력은 cell별 원본 OB/경계/offset, 분해 hash, 내외접 해상도, 각 edge/triangle witness 또는 empty 증거, GF2 rank, 미해결 위치/대상, 자원 제한 이유다. pair/triangle/메모리/실행 시간 한도 초과는 BLOCKED다.
- 단위 반례: 무장애 경계(성분1), 완전 분할벽(2), 작은 고립 영역, 격자 사이 얇은 연결/벽, 점/선 접촉, 삼각형 nerve의 구멍 유무, 네 simplex의 H1=0, 오목 경계와 frame 외부 오목부, 퇴화 cell, 실제 epsilon 표현.
- 독립 사전 설계/계산 검토 → 분석 단위/local → 독립 코드/계산 검토 → 전체 후보 진단 → 실행 증거 검토 → 문서/local/gate. 증거를 위조하는 수정이나 미해결 삭제는 금지한다.

## T027 사전 검토 보완

- T027-01: exact-cover 검증을 별도 함수로 둔다. 각 원본 polygon의 모든 vertex-x 사이 열린 slab에서 정렬된 교차점/inside-outside 구간과 생성 cell의 단면 합집합을 정확히 대조한다. 모든 절단선 x에서도 원본 경계 선분·꼭짓점과 생성 cell의 닫힌 단면이 일치해야 한다. 점/선분 cell을 보존하며 퇴화 다각형을 면적0이라는 이유로 누락하지 않는다. 실패는 COVERAGE_FAIL이다.
- T027-02: frame B는 모든 shifted polygon/obstacle의 extrema와 실제R보다 큰 양의 rational margin으로 정한다. `minMargin>R`, 모든 Pi/obstacle의 B 내부 포함과 ∂B의 네 닫힌 변 전수 피복을 exact assertion으로 요구한다. 제시한 B 내부 동등성과 B 밖 convex 띠 성질의 가정이 성립하지 않으면 성분 수를 승인하지 않는다.
- T027-03: 좌표는 C# Vector2 float의 binary32 값으로 먼저 동결하고, 실제 offset의 binary32·bodyRadius와 epsilon의 binary32→double 승격·double 연산 `r+2*epsilon` 순서를 별도 profile로 보존한다. Python Fraction은 그 실제 이진값의 정확한 비율을 사용한다. 원본 pixel→logical float 연산도 같은 곱/뺄셈 순서를 선언하고 생산 카탈로그와 독립 카탈로그를 대조한다. 수치 프로필이 없으면 BLOCKED다.
- T027-04: 가능한 모든 cell쌍 목록을 정확한 AABB broadphase에서 생성하고 제외 쌍/검사 쌍 수가 V(V−1)/2와 일치하는 완전성 집계를 남긴다. 인증된 edge 그래프의 모든 3-clique를 펼쳐 검사하며 예상/완료/미해결 count/hash가 같아야 한다. 분해·disk/hull·pair/triple 후보 생성·판정·GF2 reduction·출력 전 단계에 deadline/메모리·출력 한도를 적용한다. 초과/예외/부분 결과는 BLOCKED 또는 FAIL이며 누락을 비교집합으로 간주하지 않는다.


## 028 뒤의 현재 소스 프로필

[런타임 프로필 r002](map-topology-runtime-profile-027.r002.json)는 owned 조명 Editor 보완 후 Unity에서 읽기 전용으로 다시 수집했다. r001과 전체 runtime payload(64개 C·12개 진형·수치)가 같고, 달라진 의존 해시는 DSMapMotionVerification.cs 하나다. 과거 r001 및 실행 결과는 보존하며 현재 실행에서 재사용한 성공으로 승격하지 않는다. 소스 지문·수치·catalog 부정 대조군은 유지한다. 전체 N1/G2/G3는 별도 미완료다.
