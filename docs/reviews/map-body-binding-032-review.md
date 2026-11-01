# 032 몸체 프로필 바인딩 검증

- 범위: N1의 `BODY_PROFILE_BINDING_DIAGNOSTIC_032`. 전체 v1/G2/G3 인수와 구분한다. N2 실제 빌드·성능 시험은 사용자 실행으로 제외한다.
- 설계: [032](../design/map-body-binding-032.md). 메인이 구현했고 James 설계, Socrates 코드, Hubble 계산 검토를 읽기 전용으로 진행했다.
- 사용자 최신 지시: 변경에 반드시 필요한 검증으로 제한한다. 기존 topology12·030 전체·전체 Python suite 및 과거 메타데이터 반증 행렬을 반복하지 않는다.

## 변경

불변 몸체 프로필을 실제 시작·이동·진형 전환·부활에 연결했다. 기본8은1~4인과 기존 정적 호출을 유지하며, fixture는 고정 슬롯 수와 각 반경, 중심=max 정책을 사용한다. geometry/world scale/camera/elevation/start/visibility/combat 설정은 Bind에서 고정한다. 명시 RequireMap 초기화 실패는 legacy World/UI 초기화 전에 중단한다.

Editor fixture는 inactive 소유 root·run/nonce/PID와 실제 GameObject ID를 확인하고 한 번만 준비·소비한다. 준비 후 설정 변경·재바인딩을 거부하며 표시용 메시/재질은 생성 직후 소유 cleanup에 등록한다. 기본 JSON 요청은 새 useFixture가 false인 경우 기존 경로를 사용한다. epsilon은 kernel과 동일한 double로 전달한다.

031 snapshot은 default8 schema를 보존한다. RequireMap은 일회성 초기화 intent이고 내보내는 mapRequired가 실제 모드를 대표하므로 game scalar에 중복 추가하지 않는다. Session의 bound/bindingAttempted/pendingFixture는 coverage에 명시한다. bound의 동일 설정은 기존 binding·reference witness로 검증되며 pending은 소비 후 null이다. 새 fixture는 031 export를 명시 거부한다. 이 변경은 작은 반경 full snapshot 지원이 아니다.

## 독립 검토와 수정

좌표 범위를 기존 맵의±16384와 맞추고 반경 허용 상한을 kernel의1024와 일치시켰다. Pending authoring 설정·disabled 재검증, 생성 중 실패 자원 정리를 수정했다. 판정기는 원자료의 반경과 geometry를 요청과 대조한 뒤 독립 oracle로 초기/관측/257후보를2epsilon 기준으로 검사한다. 검사 ID 목록도 정확히 대조한다. 출력 경로는 기존031 ancestor symlink 방어를 최초 쓰기 전에 재사용한다.

## 실행 결과

2026-11-01 21:10:00~21:10:16 KST 핵심 실행 PASS. 소스별 SHA256은 아래 summary/manifest의 sources에 고정했다. 초기 실행 실패는 삭제하지 않고 output/map-full-026/body-032-*에 보존한다. 기본 요청 JSON 오인, GameObject/Transform ID 불일치, float epsilon 경계 실패를 실제 실행에서 발견하여 수정했고 각 실패 실행 cleanup은 PASS였다.

전체 v1 strict parser·active full snapshot·외부 supervisor/FI·708 실제 경로와 전체 G2/G3는 NOT_RUN이다. N2는 USER_RUN_NOT_RUN_BY_AGENT다. 기존 topology r003은032 코드 변경 전의 역사적 증거이며 현재 성공으로 재사용하지 않는다.

## 최종 근거

- 핵심 native: [summary](../../output/map-full-026/body-032-75deefecb539498bac95706f57cf81f8/summary.json) —9사례·20프레임·72독립 sweep, cold missing binding, default031 export 호환 및 fixture 거부 PASS.
- EMPTY는 두 반경 모두 center7/actual≈4.2. DIVIDER는 .25→3.14453125, 1→2.37890625에서 정지했다. NARROW 쐐기 수락·횡대 거부/후속 취소·부활 PASS.
- 실제 [device capture](../../output/map-full-026/body-032-75deefecb539498bac95706f57cf81f8/device.png)를 확인했다. 입력 scope9개·원래 자산/설정·씬·선택·background 복구 PASS. 신규 console error0.
- 관련 Python 검사16+26=42 PASS. 전체 Python suite는 사용자 범위 조정에 따라 NOT_RUN.
- 컴파일 error0/warning0. [최종 all --screens](../../output/verification/20261101-211023-4b071d09/summary.json) PASS. [원래 Map023 복구](../../output/map-full-026/regression-032-14f7e206d5cd4ba78207d97d9cd64a8f/restored.json) PASS.
- Socrates는 원자료9사례·72sweep을 재판정하고 소스/산출물/입력 journal 지문 및 device capture를 확인하여 bounded PASS를 보고했다. Hubble도 EMPTY 분석값·DIVIDER 다음257후보의 차단·전환/거부/부활·cold 근거를 확인하여 blocking 없음으로 판정했다.
- 최종 local/gate는 이 문서 갱신 후 실행하며 개별 보고서는 output/verification에 기록한다. 문서/준비 PASS를 전체 맵 인수로 승격하지 않는다.
