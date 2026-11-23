# 9단계 놓친 것과 워크플로우 보완

- 2026-11-23: 경계 부근 rooty0 표본을 높은 턱 수신으로 취급할 위험을 spec 리뷰가 잡았다. `light-transition-matrix.md`에 구역/높이 미확인 상태를 고정했고, 실제 bank mesh 받침 fixture로 30프레임 수신0을 재현했다. **반영**: 위치 분류와 실제 받침 높이를 별도로 남긴다. 수신0은 통과가 아니다.
- 높은 bank 실제 깊이만 비교하는 설계가 보이는 opaque 가림을 깨는 10/11/12 반례를 design 리뷰가 잡았다. 승인된 설계에 color-pass ownership/reference-depth와 동률 검사를 넣었다. **반영**: `raised-bank-design-brief.md` RB-02. 구현/GPU 증거는 아직 없음.
- 0.26/.5/.7초 경계는 단순 float 나눗셈의 반올림 때문에 첫 시험에서3건 실패했다. 원본800000μs를 기준으로 정규화 후 경계를 정수로 고정해 같은 기대식13/13 통과. **반영**: `DQTelegraphTests`의 정확한 경계 입력을 보존한다.
- 공격 별도 효과와 몸+효과 비교 폴더를 혼동한 pack을 발견했다. 실제 반입 전 `effect_*/slash_*`만 선택하도록 고쳤다. **반영**: `dq_attack_fx_pack.py` 출처 SHA/상대 경로와 native72×72 alpha 검사를 남긴다.
- 전체 시험에서 scene unload와 ambient Tick의 순서 창에서 파괴된 ParticleSystem 접근을 발견했다. Unity null 비교를 첫 접근 앞에 두고 실제 파괴 객체 시험 및 기존 Main lifecycle integration을 다시 실행했다. **반영**: 관련 두 시험에 final tick 포함. 판정식 완화/로그 무시 없음.
- 효과 fixture 첫 실행은 JSON Vector3.normalized 순환 직렬화로 기록만 실패했다. float[]로 바꿔 다시 캡처한다. **반영**: 증거 객체에 Unity getter 전체를 직렬화하지 않고 필요한 스칼라/배열만 쓴다.
- 비용 큰 전체 EditMode/한 판은 마감1회씩 실행했다. 뒤 수정은 실패한 통합 시험1건·Ambient 관련11건만 재실행했다. **검사 빼기 후보**: 결과가 없는 새 검사는 추가하지 않았고, 높은 비용의 전체 반복 대신 닿는 묶음을 유지한다.

시간: 시작22:37:08Z, 종×지형/턱 설계 단위22:37~23:07(상한30분), 효과·예고·메뉴/시험23:07~23:34, 전체시험/실제전투/전체경로·범위대조23:34부터. 종료 시각/나머지 활동은 최종 검증 기록에서 보완한다. 토큰 사용량은 호스트가 제공하지 않아 **미확인**이며0으로 적지 않는다. `Tools/Harness/session_time.py --markdown`은 세션jsonl이 없어 ValueError(max empty)로 실행 불가였다. 하위 에이전트0, 유료0, 성능측정0.

- 효과 표본 ROI 화면비를 고친 뒤 heal 첫 장이 수신0이었다. 원인을 확인하니 fixture가 실제 HealHeight(56/48) 대신 바닥에 중앙 피벗을 두어 묻혔다. production 높이에 맞추고, 물/소품 뒤 가림과 풀 대비를 혼동하지 않도록 앞쪽 풀 구역으로 고정 grid를 옮겼다. **반영**: `DQFinishEffectsVerification`의 camera.aspect 저장·복원과 키별 실제 높이. 이 수정을 게임 결함 수리로 세지 않는다. 향후 fixture는 producer의 앵커를 직접 소비하는 진입점이 있으면 그것을 우선한다.

- 최종 콘솔 조회가 Play 종료 시 UI/렌더러/카메라의 파괴 후 Tick 예외를 추가로 찾았다. 개별 뷰 가드만으로는 수명 순서를 닫지 못했다. 기존 `LifetimeScope.DisposeCore`/`EntryPointDispatcher.Dispose`를 읽어, 소유자는 그대로 두고 Unity quit에서 이미 있는 정리를 먼저 부르는 L1 수정으로 범위를 고정했다. 조기 정리→실제 HUD/대원 파괴→4프레임 및 자동 ExitPlayMode로 검증한다. **반영 후보**: 한 판 중 오류 수와 Play 종료 뒤 오류를 따로 조회해 마지막 단계에서도 빠뜨리지 않는다.

- 새 종료 통합 시험 첫 실행은 EnterPlayMode 이전에 만들어진 assertion lambda의 display class가 도메인 리로드 뒤 복원되지 않아 17행에서 실패했다. 기존8단계 통합 시험에 적힌 같은 주의를 새 fixture에서 놓쳤다. 게임 실패로 세지 않고, 캡처 지역 변수를 EnterPlayMode 이후 helper iterator 안에서만 만들도록 바꾼다. **반영**: `DQSceneExitIntegrationTests.VerifyAfterEnter` 구조.

- 종료 시험 둘째 실행의 실패는 예외가 아니라 정상 `[RunService] Run 1 seed 7 enemyCountScale 0` 로그를 `NoUnexpectedReceived`가 미예상으로 판정한 것이었다. 시작 로그 한 줄만 정확히 Expect하고, 파괴 뒤의 예상하지 못한 로그/예외 검사는 그대로 둔다. 로그 무시/console clear는 하지 않는다. 종료 결함의 수정 실패로 세지 않고 fixture 실패를 별도로 보존한다.

## 시간 상한과 중단

시작22:37:08Z, 마지막 구현 중 시계를 다시 읽은01:19:38Z에는162분30초였다. 130분 상한(00:47:08Z)을32분30초 넘겼으며, 20분 간격 시계 확인을 놓친 실행 결함이다. 상한을 늘린 것으로 해석하지 않는다. 즉시 미검증 연속 fixture 확장을 현재 커밋의 정적 fixture로 되돌렸고, 이후는 최종 컴파일·기존 마감 관문·종료 확인·기록만 한다. 15효과의 엔진 연속/전수 심 판정은 미완료로 남긴다. **반영 후보**: 긴 추론 구간도 실행 시간에 포함하고, 다음 구현 전 무조건 경과 시간을 확인한다. 워크플로우 문서로 이 항목에 반영했다. 토큰 값은 호스트 미제공으로 미확인이다.
