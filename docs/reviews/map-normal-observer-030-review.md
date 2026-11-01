# 030 — 정상 맵 Update/명령 큐 관찰 검증

- 범위: NORMAL_MAP_FRAME_OBSERVER_030. 018 개발 맵·몸체8·전투 비활성의 정상 Update 연결이다. 026 최상위 특별 분기와 구분하며 fullv1/전체 N1·G2/G3·708경로·외부 supervisor/FI 전체 완료가 아니다.
- [설계](../design/map-normal-observer-030.md)는 독립 검토 후 구현했다. 메인이 구현하고 validator/algorithm은 읽기 전용으로 코드·실제 증거를 독립 검토했다. 최종 bounded 코드/계산/증거 검토 PASS, 잔여 지적 없음.

## 구현과 실제 결과

정상 AdvanceNormalMapFrame의 기존 큐를 사용한다. 시작/RecordTime 직후/명령 전후/Simulate 전후/Render+UI 뒤를 관찰하고 game frame/Simulate 카운터를 별도로 기록한다. 고정 입력은 수집 MoveInput와 supplied dt만 바꾸며 원래 clamp·상태 검사·큐·시뮬레이션·렌더 경로를 유지한다. VerificationRun의 통계 생략은 등록된 활성 observer 프레임에서만 해제한다.

[최종 native 실행](../../output/map-full-026/normal-030-ca39d0f836e049b2bdc017d805a30fce/summary.json) PASS/cleanup PASS. 정상21사례·238관측 프레임·독립 sweep1,197회와 기대 오류 검출3사례를 구분한다. 12인원/진형, pause/prep, FIFO·기하/상태 거부·후속 취소, 실제 사망/부활, dt0/최소/최대clamp, 고정 재현, Dynamic Keyboard DEVICE_SMOKE를 포함한다. 24입력 scope 전부 RESTORED, 원래 장치/설정·대상 파일 불변을 검증했다. [실제 장치 화면](../../output/map-full-026/normal-030-ca39d0f836e049b2bdc017d805a30fce/device.png)을 직접 확인했다.

부정3건은 외부 큐 제출이 진행 중 명령을 방해하는 경우, 등록 뒤 직접 도메인 호출, 직접 Simulate다. raw complete=false와 정확한 실패를 보존하고 terminal 뒤 실제 state/frame/Simulate/events가 더 변하지 않음을 확인했다. 진행 중 Pause의 부분 적용을 rollback 성공으로 부르지 않는다. 예외 명령의 전체 envelope/초기 nonce·event/frame anchor와 부분 상태를 대조한다. 콘솔은 해당 부정 시험의 정확한 예상 오류1건만 허용한다.

실제시간은 native float32 비교로 원본 설정과 대조하고 모든 명령/시뮬레이션/렌더 뒤 clock/history 불변을 검사한다. 정상 입력 수집에서 MoveInput의 반올림 표시 문자열만 변할 수 있으며, 유일 행을 요구하고 실제 벡터는 tape/device와 이동 수식·전체 sweep으로 따로 검사한다.

## 실패 이력과 회귀

- 첫 실행 [d6861f1d](../../output/map-full-026/normal-030-d6861f1d2d7c44d683aadd5c08b478a6/summary.json) FAIL/cleanup PASS: native float32 .49와 원본 JSON double의 비교 차이로 TEST_CONTROLS_LEDGER 오판.
- 둘째 [c3e20c33](../../output/map-full-026/normal-030-c3e20c3319c14aae8fda3513f85944e3/summary.json) FAIL/cleanup PASS: 정상 MoveInput 수집을 phase 오염으로 오판. 해당 단계의 유일 MoveInput 표시 행 외 불변을 검사하도록 수정하고 반례를 추가했다.
- 수정 뒤 [정상21사례](../../output/map-full-026/normal-030-b7cc3038941948a7b88cbb3dd11d596b/summary.json) PASS를 얻고, 부정3건·최종 봉인을 보강한 뒤 위 fresh24사례를 다시 실행했다. 이전 실패를 덮지 않았다.
- 현재 관련 Python26시험 PASS: ledger·전 단계 clock 변조·input 단계 변조·초기 anchor·전체 normalization→기하 checker·명령 후속 취소·fault partial/메타데이터 변조를 포함한다. 전체 Python 검사의 legacy Documents 접근 차단은 별도 환경 결과로 기록한다.
- [현재 전체 회귀](../../output/verification/20261101-200019-d1f03342/summary.json) PASS: 컴파일·695규칙·실제 프레임/입력·3화면비33검사·27캡처·정리. native DEVICE 화면 및 회귀1024×768/1560×720 전투 화면을 직접 확인했다.
- [원래 Map023/선택/백그라운드 복원](../../output/map-full-026/regression-030-ee0a8eac12364e5887a3680a66c5c941/restored.json) PASS.
- [새 native 프로필/누수 반례](../../output/map-full-026/profile-030-4a5d2fe733ca4aeab14b76a6c349e63b/summary.json) PASS: [027/r003](../design/map-topology-runtime-profile-027.r003.json) 전체 기하/수치/catalog는r002와 동일하고, Map030Keyboard가 남아 있으면 실제 Ownership 장치 집계가 증가하는 것을 확인한 뒤 정확한 probe만 제거했다. 원래 설정·장면은 유지했다.

## 남은 경계

StartRun/ReturnLobby tape, 시뮬레이션 중 웨이브 종료 경계, Enemy/Shot/Effect가 존재하는 snapshot은 첫030에서 지원하지 않으며 BLOCKED/거부한다. [031 전체 snapshot 기반](../design/map-snapshot-031.md), fullv1 parser·작은 반경의 실제 공유 이동·감독/FI·708사례를 계속 구현한다. N2 실제 빌드/부하/성능 시험은 사용자가 실행하므로 하지 않는다. N3 Editor 체크포인트는 변경 전 지문의 과거 증거로 보존했다.

## 030 체크포인트 최종 상태

- [local](../../output/verification/20261101-200256-631bb529/summary.json) PASS, [gate](../../output/verification/gate-20261101-200256-cab4da7e/gate.json) PASS.
- [전체 Python 재실행](../../output/map-full-026/python-030-708b3d3b7d124d46b2b5dc2533df8270/summary.json)은288개 중286통과·기존legacy 경로 EPERM2개로 BLOCKED다. stderr 원문을 보존했고 전체 Python PASS로 보고하지 않는다. bounded030·현재 Unity 회귀/gate와 이 환경 차단을 구분한다.
- 이후031 소스 변경 전 현재84개 코드 지문과 검증 증거를 `output/map-full-026/checkpoint-030.json`에 봉인한다. 이 체크포인트를 후속 변경의 현재 검증으로 자동 재사용하지 않는다.
