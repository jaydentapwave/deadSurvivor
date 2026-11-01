# 031 — 전체 맵 게임 상태 스냅샷 기반

- 상태: bounded 구현/native 진단 PASS, 현재 회귀·마감 진행 중. L2 설계 검토를 통과했다. 030 일반 Update 연결 뒤 남은 full map v1 요구를 진행한다. N2 실제 빌드/부하 테스트는 사용자 실행으로 제외한다.
- 원본: [데이터 계약](map-harness-data-contract.md)의 Snapshot, [009](map-harness-implementation.md)의 명령 원자성/재현, [030](map-normal-observer-030.md)의 비완전 Enemy/Shot 직렬화 제한.
- 현재 `CaptureMapFrameState()`는 Enemy/Shot/Effect가 Serializable이 아니어서 전투를 완전히 기록하지 못한다. 030은 이 객체가 하나라도 있으면 차단한다. 이 제한을 단순히 제거하지 않는다.
- 메인이 구현한다. 독립 설계/계산/구현 검토는 읽기 전용이다. 기존026/030 기록 형식과 저장 씬을 덮지 않는다. 구현 범위는 별도 스냅샷 계약과 native 내용 검증이며 fullv1 parser·실행기·전체 G2/G3 완료는 아니다.

## 변경과 상태 소유

`Runtime/DSGame.MapSnapshot.cs`에 Editor 전용의 `CaptureFullMapSnapshot`을 추가한다. 게임 상태를 작성하지 않는 순수 관찰 함수다. 전용 DTO에서 모든 Survivor/Gear/Enemy/Shot/Effect의 실제 필드를 명시적으로 복사한다. JsonUtility가 지원하지 않는 객체를 빈 구조로 넘기지 않는다. DSGame의 명시 scalar·두 RNG·Config·History·선택/팀/전리품의 순서를 보존한다.

1. Survivor/Gear는 직렬화 필드 외 pos/healClock/shieldClock/flash/shareClock, cd/lastPeriod/activationFlash를 포함한다. 슬롯과 GearID를 식별하며 목록 순서를 유지한다. cells도 실제 36칸 복사한다.
2. Enemy는 id/type/phase/pos/charge/flock/hp/maxhp/damage/cd/flash/phaseTime/rushTime/offscreenTime와 dashHits를 담는다. Shot은 type/pos/dir/target/speed/damage/range/travel/dead/owner/pierce/radius/hits를 담는다. Effect는 pos/life/max/r/color/text를 담는다. owner/dashHits의 Survivor는 슬롯 참조로 해소하고 알 수 없는 참조는 BLOCKED다. nullable owner는 명시 -1로 기록한다.
3. List는 실제 순서를 유지한다. 순서가 없는 HashSet/Dictionary는 명시된 안정 키 순으로만 정렬한다. 중복 entity ID/GearID·잘못된 참조는 차단한다. mapRoutes는 현재 Enemies에서 제거됐으나 다음 스텝까지 남아 있을 수 있으므로 ghost key 객체도 별도 enemy-reference pool에 기록한다.
4. 경로·캐시도 게임 미래 동작을 바꿀 수 있다. mapRoutes의 key/index/target/retry/points, mapEnemyCursor/search/spawn counters를 포함한다. mapNavigators의 radius/map identity와 node/edge cache·LastVisited는 별도 읽기 전용 DTO로 출력한다. 캐시를 관찰하기 위해 갱신하거나 경로 검색을 호출하지 않는다.
5. Config를 전체 복사/hash하고 Data/Items/Monsters/Equipment 원본 참조의 불변성을 동결 SHA와 대조한다. MapSession은 현재 불변 기하·설정 프로필과 연결하고 별도 snapshot의 geometry identity를 남긴다. System.Random 일반 인스턴스는 지원하지 않으며 실제 seed/각 요청·응답 trace/count/hash를 기록한 두 DSMapTraceRandom만 받는다.
6. 메타데이터(소유 root/nonce/인스턴스/frame/observer/queue 계측)와 게임 의미 상태를 분리한다. UI toast·렌더 캐시·초기 로딩 측정·실제시간의 비교 제외 키는 버전으로 명시한다. 실제시간 원자료는 삭제하지 않고 030처럼 별도 ledger/동일 명령 전후 검사를 계속한다. UI 자체의 drag/held 상태까지 검증하는 계약으로 확대하지 않는다.

## 필드 누락 방지

DSData의 다섯 상태 타입 및 DSGame/경로/내비게이션 상태 필드 목록·타입을 명시 계약으로 대조한다. 새 필드가 추가됐는데 DTO나 고정 제외 사유에 없으면 export를 BLOCKED로 한다. 임의 reflection dump·타입명 ToString으로 모르는 상태를 성공으로 직렬화하지 않는다. 제외 목록은 이 작업의 설계 검토 대상이며 이름 접두사로 모든 새 계측을 조용히 제외하지 않는다.

원자성 검사는 같은 명령의 snapshot 의미 값을 비교하고, 재현은 정수/ID/문자열/불리언/목록 순서/RNG를 정확히, 좌표/시뮬레이션 시간은 요청의 단위/출처가 고정된 epsilon으로 비교한다. 아직 fullv1 요청 인수기가 없으므로 이번 단위 진단에 epsilon을 최종 G3 채택값으로 선언하지 않는다.

## 검증과 실패

Native 소유 disabled game에서 실제 nonzero Enemy/Shot/Effect와 비직렬화 Survivor/Gear 타이머를 구성한 뒤 source object→DTO의 모든 필드·참조·집합을 대조한다. capture 전후 게임 상태/캐시/trace가 그대로인지 확인하고 둘 연속 export의 동일성을 검사한다. null owner/빈 집합/ghost route/중복 ID/잘못된 참조/미등록 RNG/누락 필드도 반증한다. 생성/관찰/복구는 소유 root 안에서만 수행한다.

독립 검토 후 단위→native DTO 대조→필요한 실제 정상 전투 프레임 내용 비교→현재 게임 회귀/local/gate를 수행한다. 외부 입력/기록 실패·소스 변경·불완전 타입은 BLOCKED/FAIL로 남기며 빈 배열/0으로 대체하지 않는다. 기존030의 전투 차단은 후속 전체 observer/인수 계약을 연결하고 검증하기 전까지 유지한다.

## SN031-01~04 설계 보완

- SN031-01: 반환 DTO의 모든 중첩 값(Config/History/cells/routes/cache/trace 포함)을 독립 복사한다. Game의 변경 가능한 리스트·배열·요소 참조를 DTO에 남기지 않는다. 캡처 뒤 원본을 바꿔도 이미 반환된 DTO와 직렬화 바이트가 바뀌지 않아야 한다.
- SN031-02: Team의 같은 객체 중복, GearID 중복/같은 Gear의 복수 소유는 이 계약에서 거부한다. enemy pool은 참조 동일성으로 live/ghost를 한 번만 해소하고 서로 다른 객체의 ID 충돌을 거부한다. Shot.hits는 과거 enemy ID 집합이므로 현재 Enemies에서 제거된 ID도 합법적으로 보존한다. dashHits는 현재 Team의 참조로 해소한다.
- SN031-03: DSMapTraceRandom이 생성 당시 seed를 불변 보관하고 private trace만 갱신한다. 외부 조회는 복사본만 반환한다. 현재 사용하는 Next()/Next(max)/Next(min,max)/NextDouble()의 요청·응답과 연속 sequence를 추적하며, bytes 계열 등 미지원 API는 RNG 상태를 바꾸기 전에 명시적으로 거부한다. 실행 프레임워크의 Random API 목록도 확인하여 새 API가 조용히 우회하지 못하게 한다. 캡처 중 seed/trace 재생성이나 원본 RNG 호출은 금지한다. 실제 trace는 독립 복사 또는 해시로 고정된 prefix artifact로 연결하며 단순 타입 일치만으로 내부 상태 완전성을 주장하지 않는다. 모든 수치에서 NaN/Infinity를 거부한다. 정수/키/집합/참조/카운터는 정확 비교하고 위치/시뮬레이션 시간에만 명시 epsilon을 허용한다.
- SN031-04: schema_version=1/document_role=FULL_MAP_SNAPSHOT_031로 별도 기록하며026/030으로 암묵 변환하지 않는다. Unity main thread의 동기 관찰 지점에서만 캡처하고 재진입을 거부한다. native 구성·변조 시험은 소유 disabled game에서만 수행하며 finally의 기존 owned cleanup을 유지한다. entity/cache/trace/직렬화 바이트에 유한한 상한을 두고 초과하면 완전한 DTO를 반환하지 않고 BLOCKED다. 기본 상한과 요청별 상한은 후속 인수기에서 그대로 검증하며, truncation/부분 성공은 없다. 원자료가 커질 때 trace/immutable 콘텐츠의 별도 고정 artifact를 참조해 동일 데이터를 매 snapshot에 반복하지 않는 방식도 명시 버전으로만 사용한다.

## r001 고정 실행 상한과 첫 관찰 지점

첫031 collector는 이미 등록된 소유 disabled game의 RunOwnedMapUnits 안에서만 호출한다. 등록 당시 Unity main-thread ID를 별도 context에 고정하고 다른 thread/재진입은 거부한다. 활성 Update 연결은 후속 소비자 계약 전까지 허용하지 않는다. 전투 객체를 포함한 순수 내용 snapshot 기반을 먼저 검증하며030 combat guard를 유지한다.

r001 상한: Team4, Gear(모든 소유 합계)256, live+ghost Enemy4096, Shot8192, Effect8192, History128, route points총65536, navigation cache 총100000항목, RNG trace 흐름별16384호출, queue32/past event128, DTO UTF-8 32MiB, 동기 capture wall2초. 실제 각각의 값은 유한하며 초과는 BLOCKED/예외이고 잘린 DTO를 반환하지 않는다. 상한을 키우려면 새 계약 개정을 먼저 만든다. RNG prefix를 별도 artifact로 분리하는 최적화는 이 r001에서 구현하지 않는다.

게임 의미 상태와 protocol/관측 메타데이터를 별도 DTO로 둔다. protocol에는 현재 queue/order/ID 집합/nonce/오류/토큰과 사건도 빠짐없이 복사하며, 거부 원자성 비교에서는 protocol 변화만 별도 검증한다. 컴파일러 backing field를 포함한 고정 필드명/타입 분류는 코드에 명시하고, 새 field가 생겨 분류되지 않으면 차단한다. 렌더/UI 객체의 자체 내부 상태는 계약 밖이며 Game 참조의 동일성만 동결한다.

추가 구조 상한: Selected4, navigation 인스턴스256, Config 전체 wave type 합16384(각 wave8192 이하), Shot.hits 전체 합65536이다. 큰 집합 정렬/복사 전에 합계를 검사하며 각 entity 복사 중에도 wall budget을 검사한다. protocol의 기존 command before/after 문자열은 단일1MiB까지 별도 허용하되 총32MiB와 Unicode 유효성 검사를 유지한다. 일반 필드 문자열은4096자 이하이며 null과 빈 문자열은 별도 tagged text로 보존한다.

mutable 소유 경로의 별칭도 보존 정책을 고정한다. owner/dashHits/route key는 명시 entity 참조로 표현하며, 이외 mutable 객체·컨테이너의 중복 소유와 Config→동결 Data 객체 공유는 거부한다. Shot.hits/cells/Config 배열/History/경로·캐시·protocol 컨테이너도 포함한다. 두 RNG는 같은 seed의 독립 객체는 허용하지만 공유 인스턴스는 거부한다. 미지원 RNG API 뒤에는 다음 정상 draw도 별도의 golden RNG와 대조하여 내부 상태가 전진하지 않았음을 검사한다.

직렬화 직전에 JSON escape와 구조/수치의 보수적 출력 상한을 예약한다. 문자열은 UTF-16 단위당 최대6 UTF-8 bytes, single/double은 유한 IEEE 값의 충분한 십진 출력 상한으로 계산한다. 이 안전 상한이32MiB를 넘으면 실제 JSON을 크게 할당하기 전에 BLOCKED다. 따라서 실제 압축되지 않은 출력이32MiB보다 작아 보여도 보수적 예약 상한으로 먼저 차단될 수 있으며 이를 부분 성공으로 보고하지 않는다. NUL 문자열을 가진 다수 Effect가 JSON escape로 팽창하는 반례도 포함한다.

독립 source witness는 첫 캡처 전후의 논리 managed 상태/참조와 Random 내부 상태도 비교한다. observer context·CLR projection/monitor cache·Unity render internals·전체 프로세스 메모리 불변은 범위 밖이다. 실제 결과는 [031 검증 기록](../reviews/map-snapshot-031-review.md)을 따른다.
