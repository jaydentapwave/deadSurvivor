# 031 — 전체 상태 DTO 기반의 제한 진단

- 범위: SNAPSHOT_FOUNDATION_ONLY_031 / SOURCE_DTO_UNIT_DIAGNOSTIC_031. 등록된 소유 disabled game에서 관찰하는 기반이다. active Update·fullv1 실행기·전체 N1/G2/G3·FI 전체 인수와 구분한다.
- [설계](../design/map-snapshot-031.md)는 독립 검토 PASS 후 구현했다. 메인이 구현하고 독립 design/validator/algorithm 검토는 읽기 전용으로 수행했다. 030 combat guard를 유지하며 새 exporter의 이름으로 이를 해제하지 않았다.

## 구현

전용 DTO가 Survivor/Gear 비직렬화 위치/타이머, Enemy/Shot/Effect의 모든 필드, 순서 있는 목록·집합, owner/피격 슬롯, live/ghost enemy, 경로·navigation cache, 전체 Config/History 및 명시된 Game scalar·protocol을 복사한다. 모든 중첩 mutable 값을 독립 복사하고 null/빈 문자열은 별도 tag로 보존한다.

필드 이름·타입 coverage를 고정하고 새로운 미분류 필드는 차단한다. 원본 Data/정의/Equipment/Layout/session의 내용과 원래 객체·중첩 참조를 고정한다. mutable 소유 경로의 별칭은 거부하고 owner/dashHits/route key는 명시 entity ID/slot 참조로 해소한다. 삭제된 enemy를 가리키는 Shot.hits의 과거 ID는 합법적으로 보존한다.

RNG는 seed를 불변 보관하고 private trace의 복사본만 반환한다. 지원 API·실행 프레임워크 API 집합을 확인하고 bytes API는 상태 전진 전에 거부한다. 같은 seed의 독립 RNG는 허용하고 공유 인스턴스는 거부한다. trace는 별도 Random 재생과 비교한다. 이는 고정 코드/API 경계에서 seed+trace의 충실성을 확인하며 임의 reflection 공격에 대한 암호학적 RNG 내부 상태 인증을 주장하지 않는다.

source/DTO의 entity·collection·누적 hits/route/cache·Unicode/비유한 값·출력/시간 상한을 검사한다. JSON escape의 보수적 상한도 직렬화 전에 예약하여 큰 출력 할당을 차단한다. 제한 초과는 잘린 DTO로 반환하지 않는다.

## 실제 결과

[최종 native 진단](../../output/map-full-026/snapshot-031-9588da7371cc47a5878e3f4068ccfc24/summary.json) PASS/cleanup PASS. native source 대 DTO 필드341개·검사436개와 기대 거부29개를 확인했다. 두 RNG, 각 타입의 nonzero 값·비직렬화 상태·목록 순서·빈/nullable 참조·ghost route·과거 shot ID·비어 있지 않은 protocol·중첩 분리·원본 객체 교체·공유 참조·잘못된 thread·자원 상한을 포함한다.

첫 캡처의 멱등적 source 변경을 놓치지 않도록 DTO exporter를 호출하지 않는 별도 source witness를 캡처 전후에 뒀다. 논리 managed Game/Data/map/cache/protocol/Random private state의 값과 path별 ReferenceEquals를 비교한다. observer context·CLR projection/monitor cache·Unity render internals·전체 프로세스 상태 불변의 증거는 아니다. 모든 field/value의 일치는 native unit checker를 신뢰하는 경계이며 Python이 native 객체 전체를 직접 읽었다고 주장하지 않는다.

[격리 사본 반례21건](../../output/map-full-026/snapshot-031-9588da7371cc47a5878e3f4068ccfc24/evidence-counterexamples.json)도 기대 검출을 통과했다. 누락 필드·잘못된 참조/순서·null 변경·구형 unit을 새 wrapper에 삽입·소유권/instance/scope·RNG·미완료·witness 변조를 원본 보존 사본에서 검증했다. 새 native FI 실행이나 전체 G2 FI 인수로 합산하지 않는다.

Native는 동일 write-buffer의 SHA/길이/run/nonce/instance receipt를 반환한다. 부모는 그 bounded buffer만 parse/hash/보존하고 내부 owner·queue·input journal을 연결한다. pending 해제 전·최종 봉인 시 동일 bytes를 다시 대조한다. 첫 출력 이전부터 symlink/상위 이탈을 거부한다. 관련 Python schema·parent 반례24개 PASS다.

[새 소스의030 재실행](../../output/map-full-026/normal-030-e04e0b16a0f048afbac5883eea2bbb1d/summary.json)도 정상21/기대 오류3·238프레임·1,197sweep·입력24scope 복원·cleanup PASS다. 기본8 이동/전환 질의 코드가 유지된 제한 진단이며 .25/1의 실제 연결은 [032](../design/map-body-binding-032.md)에서 별도 구현한다.

처음 [031 native 진단](../../output/map-full-026/snapshot-031-4ca5e6508bf542618c68247515ff12b4/summary.json)은 당시280필드/371검사/거부28건을 통과한 이전 개정이다. 현재의 source witness·확장 field/value/출력 예약 및 사본 반례가 포함된 새 실행과 구분해 보존했다.

현재 전체 회귀·local/gate·최종 증거 검토는 이어서 기록한다. 전체 Python의 legacy Documents 접근 오류는 기존 별도 BLOCKED이며 이를 이 기반 진단의 PASS로 덮지 않는다. N2 실제 빌드·부하·성능 시험은 사용자 실행으로 제외한다.

## 현재 회귀와 마감

- [현재 all --screens](../../output/verification/20261101-205614-0aec87fb/summary.json) PASS: 컴파일·695규칙·실제 입력/프레임·3화면비33검사·27캡처·정리. [원래 Map023/선택/백그라운드 복원](../../output/map-full-026/regression-031-257628f1e60b4fc4b574e2f228e3ae3f/restored.json) PASS.
- [전체 Python312 재실행](../../output/map-full-026/python-031-a618148883ec4b0eb8d9a64a1f26af83/summary.json)은310통과·동일legacy Documents portal EPERM2건으로 BLOCKED다. 오류명/원문을 보존했고 전체 Python PASS로 바꾸지 않았다.
- 독립 validator/algorithm의 최종 native 증거 검토 PASS. 정상030/DTO031의 현재 source와 모든 artifact/receipt/input journal SHA·복구를 대조했다. 전체 회귀·최종 local/gate 근거는 별도로 확인한다.

body8 실제 공유 이동/시작/전환 코드의 SHA는030 인수 시점과 동일하며 현재030 재실행도 통과했다. [연속성 근거](../../output/map-full-026/body031-continuity-e141317b3fce4820b1a9d3c64f2e78a4/summary.json)는031의8 header가 현재 코드에 대응함을 확인하며 새 sweep/작은 몸체 증거로 합산하지 않는다.

- [최종 local](../../output/verification/20261101-205839-e8a2a20b/summary.json) PASS, [gate](../../output/verification/gate-20261101-205847-9b268a5d/gate.json) PASS. 현재 DEVICE 화면과1560×720 최대 장착 회귀 캡처를 직접 시각 확인했다.
-031 마감의 코드89개·증거는 `output/map-full-026/checkpoint-031.json`에 보존한다. 이후032 소스가 바뀌면 이 결과는 변경 전의 역사 증거이며 현재 성공으로 자동 재사용하지 않는다.
