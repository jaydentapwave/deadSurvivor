# 032 — 실제 공유 맵 질의의 몸체·fixture 바인딩

- 상태: L2 설계/코드/계산 검토, 핵심9사례·cold·최종 게임 회귀 PASS. [검증](../reviews/map-body-binding-032-review.md). N2 실제 빌드·성능 테스트는 사용자 실행으로 제외한다.
- 요구: [v1 데이터 계약](map-harness-data-contract.md)의 G2 .25/1 circle을 실제 정상 명령/이동 경로에 전달한다. 기존8 반경을 요청값으로만 바꿔 기록하거나 특별 Simulate 우회로 증거를 만들지 않는다.
- 원본: [009](map-harness-implementation.md), [030](map-normal-observer-030.md), [031](map-snapshot-031.md). 메인이 구현하고 독립 검토자는 읽기 전용이다.

## 현재 경계

DSGame.PlanPartyMotion, DSMapPartyRules.CanStart/CanTransition의8은 개발 기본값이다. DSMapSession.Bind는018 데이터만 받으며 Data.world_scale을 매번 읽는다. Awake에서 MapSession이 없으면 legacy로 들어가므로 ‘명시 MapRequired인데 최초 binding이 없다’는 상태를 표현하지 못한다. 031/r001 snapshot은 disabled·body8 진단만 받는다. 이 구현을 전체v1/G2로 부르지 않는다.

## 한 상태 소유처와 실제 경로

1. 불변 `DSMapBodyProfile`을 추가한다. 기본 프로필은1~4인 모두 슬롯/중심8이며 기존 씬의 동작을 유지한다. G2 프로필은 등록한 슬롯 수와 각 circle 반경을 복사·고정한다. `center radius=max(slot radii)`를 파생 정책으로 명시한다. .25/1은 같은 logical_fixture 단위이며 image scale을 곱하지 않는다. 모든 수치 유한·양수·선언한 좌표 범위 이내다. public getter가 내부 배열을 반환하지 않는다.
2. DSMapSession이 Bind에서 Geometry/body profile/world scale/image scale/초기 center/elevation과 참조를 고정한다. Runtime의 이동·시작·진형 전환·부활 질의는 이 바인딩만 사용한다. Data나 public 설정의 사후 변동으로 실제 질의 단위가 바뀌지 않게 한다. 기존 정적 CanStart/CanTransition 호출은 default8로 위임하는 호환 overload를 유지한다.
3. `PlanPartyMotion`은 실제 `DSMapMotion.Propose`에 고정한 슬롯 반경/중심 반경을 넘긴다. StartRun과 MapAllows의 진형/부활도 같은 geometry/profile을 받는다. 슬롯 수 불일치·미지원 shape·invalid radius는 legacy/default8로 조용히 대체하지 않고 거부한다.
4. 명시 MapRequired 초기화 intent를 추가한다. 기존 MapSession 있는 씬은 현재처럼 MapRequired로 승격하고, legacy 기본값은 유지한다. 명시 required인데 session/geometry가 없거나 Bind가 실패하면 UI/World/legacy 초기화 없이 disabled terminal을 기록한다. 소유 cold-bootstrap 대조에서 actual 초기화 실패를 검증한다.

## G2 초기화와 소유권

G2 geometry는 boundary/obstacles를 깊은 복사한 DSMapGeometry로 만든다. 기존018 Data나 저장 씬을 덮지 않는다. pending fixture 초기화는 실제 owned root·game·세션이 inactive/disabled이고 Awake/Bind 전일 때 한 번만 등록한다. run/nonce/PID/PLAY marker 및 실제 root instance를 대조한다.

필요하면 DSMapMotionVerification의 기존 case root 생성/해제 경계에 프로젝트 SessionState의 case root instance ID를 기록·해제하여 pre-Awake 초기화도 실제 등록 root와 연결한다. 문자열 이름·nonce만 맞는 임의 root를 승인하지 않는다. 다른 root/중복·설정 후 변경·두 번 Bind·바인딩 누락은 거부하고 기존 finally cleanup을 유지한다. 런타임에는 이 G2 초기화 API를 제공하지 않는다.

fixture의 좌표계/geometry hash/profile/실제 s/k 또는 logical-only 표기와 초기화 사건을 별도 manifest에 남긴다. 초기화 뒤 실제 body/geometry를 export하여 요청과 대조한다. 테스트 중 State 문자열이나 현재 위치를 직접 쓰는 명령을 만들지 않는다. 1~4인 위치는 실제 StartRun에서 생성한다.

## first 032 진단 범위와 표시

역할은 BODY_PROFILE_BINDING_DIAGNOSTIC_032다. strict fullv1 parser·전체FI·708경로 인수는 후속이며 G2 전체 PASS로 합산하지 않는다. 기존 정상 Update/큐와 actual device/input scope/cleanup을 재사용한다. 관찰 요청에 geometry/profile을 추가하되 실제 export된 값과 각 명령/이동의 native 원자료를 대조한다.

G2 표시 어댑터는 소유 transient root 안의 실제 fixture geometry로 floor/벽을 만들고 원본018의 렌더러는 이 root에서만 숨긴다. 모델이 논리 collider의 원본이 되지 않는다. logical circle 프로필은 분석 도형임을 명시하고 실제 camera/viewport/capture를 기록한다. 최종 G3 외형·캐릭터 크기 채택으로 해석하지 않는다.

031/r001은 기존 body8 계약을 보존한다. 새 fixture/profile을 잘못된8 header로 내보내지 않고 명시 BLOCKED한다. 추가 Game/Session field는 고정 coverage와 제외 사유를 갱신하되 기존 snapshot의 의미를 바꾸지 않는다. full profile snapshot을 소비하려면 별도 개정/수락 검증을 먼저 수행한다. immutable source code/numeric profile은 새 native 개정으로 캡처하고 이전 증거를 현재 성공으로 재사용하지 않는다.

## 검증

- EMPTY: radius.25/1 각각 정상 실제1스텝 center7, actual4.2; 전체 슬롯/중심/예약 sweep을 독립 oracle로 판정한다.
- DIVIDER x[3.4,3.6]: .25/1의 서로 다른 clearance를 실제 공유 이동에서 관찰한다. solver alpha를 그대로 기대값으로 복제하지 않고 벽을 뚫지 않음·양의 진행·더 큰 안전 후보 부재를 검증한다.
- NARROW x[-60,60]:4인 균형/쐐기 수락, 횡대 거부, 거부 원자성·후속 취소·예약 슬롯/생사/부활을 대조한다. 실제 domain 전환과 명령 큐를 쓴다.
- 기본8의12인원/진형·030 정상/기대 오류·기존695규칙/3화면비를 새 소스로 확인한다.
- missing cold binding/잘못된 초기화 token/root/재바인딩/요청 배열 사후 변경/슬롯 수 불일치/비유한 반경/미지원 body를 반증한다. cleanup 실패는 기대 오류 성공으로 상쇄하지 않는다.

설계 검토→구현→단위/독립 코드·계산 검토→native→현재 회귀/local/gate·증거 검토 순서다. 원본007/018/021~028 자산·hook·baseline·전역 설정·외부 서비스는 바꾸지 않는다.

## BB032-01~04 보완

- BB032-01: default8은 고정4배열을 노출하는 것이 아니라1~4인 질의에 대응하는 불변 정책이다. G2만 requested slot count를 고정한다. 인원 불일치는 StartRun의 Center/uid/Team/장비/난수 변경 전에 거부한다. 죽은 슬롯도 프로필 순서/반경을 유지하며 살아 있는 인원 수로 다시 압축하지 않는다.
- BB032-02: 전 소비자는 같은 binding을 읽는다. DSGame.Run의 CanStart/StartCenter, MapMotion의 실제/예약/중심 radius, MapSession의 전환/부활, MapCombat의 MapLine/Navigator/EnemyDisplace/spawn/path, Combat의 shot terrain sweep, DSMapSession.World/Frame과 DSWorld.Map의 논리 ViewWidth/ViewHeight 및 camera/elevation/worldscale을 모두 연결한다. 기본 질의 반경8과 적/shot 자체 반경은 혼동하지 않는다. public 설정/Data의 사후 변경은 실제 bound 값을 바꾸지 않는다. 검증 하네스는 별도로 원본/manifest 무결성 변경을 FAIL/BLOCKED로 기록할 수 있으며 이를 runtime 재바인딩으로 취급하지 않는다. geometry 배열은 현재처럼 복사본만 반환한다.
- BB032-03: hierarchy inactive 상태에서 root→session→disabled game을 구성한다. 외부 소유 관리에 실제 root instance를 등록한 뒤 pending fixture를 한 번 등록하고, 마지막 activation에서 Awake가 같은 game/session/root/run/nonce/PID를 재확인해 소비한다. enabled=false만으로 Awake를 막았다고 가정하지 않는다. 바인딩 시도는 UNBOUND/PENDING→BOUND 또는 FAILED이며 실패는 재사용/재시도 불가다. pending은 성공·실패 모두 소비/해제되고 생성된 source/display 물체는 기존 owned finally에서 정리한다. 필요한 case root ID는 기존 관리에만 추가하고 별도 parallel lock은 만들지 않는다.
- BB032-04: EMPTY의 center7/actual4.2는1인/offset0/dt.05/speed1/initial actual=center=0/무장애·충분한 Duration을 전제로 한다. DIVIDER는 같은1인 직선 fixture에서 analytic clearance와 실제 진행을 비교하고, 최대성은 명시된257후보 집합 내 독립 판정으로 한정한다. 모든 방향/연속 alpha의 전역 최적성을 주장하지 않는다.032 전용 profile-aware 판정기/요청 개정을 만들고030/031 body8 판정 결과를 작은 radius의 성공으로 재사용하지 않는다.

## 사용자 검증 범위 조정 (2026-11-01)

사용자 지시에 따라 이번032는 변경과 직접 관련된 최소 검사만 실행한다. .25/1 EMPTY·DIVIDER, 4인 NARROW 수락/거부·부활, 기본8 smoke, 실제 device 입력/프레임/캡처, missing binding·설정 소유권 단위 반례, 독립 코드/계산 검토, 필수 최종 all --screens/local/gate로 제한한다. 변경 없는 topology12 전수·기존030 전 사례·전체 Python suite·과거 메타데이터 반증 행렬은 반복하지 않는다. 반경 상한은 실제 sweep kernel과 동일한1024이고 시작 중심 좌표는 기존018/028과 같은±16384다. N2 실제 빌드·성능은 사용자 실행으로 계속 제외한다.
