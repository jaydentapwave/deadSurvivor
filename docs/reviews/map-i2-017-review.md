# MAP-I2-017 이동 핵심·실제 프레임 검증

2026-10-31 · L2 · [설계와 사용법](../design/map-i2-017.md). 구현 범위는 MOVEMENT_FIXTURE_ONLY다. 최종 숲 맵·full map v1·명령 큐·I3 상태 전환·전체 FI/G2·외부 watchdog·물리 기기는 NOT_RUN이다.

## 구현과 검토

불변 다각형/circle sweep과 257개 alpha 후보를 구현하고 DSGame 실제 Update→Simulate에 연결했다. Center·생존자·사망 예약을 검사하며 alpha0 추종/전 후보 보류를 구분한다. 기존 Legacy 이동과 장비 처리 순서를 유지했다. Python은 도형/실제 선분·동결한 초기 배치·추종/보류/진행을 독립 판정한다. 시험 벽 윤곽은 표시 보조이며 최종3D/PhysX Collider 채택이 아니다.

메인이 구현하고 `/root/map017_design`·`map017_algorithm`·`map017_validator`가 읽기 전용으로 검토했다. 설계/코드/실행 근거 검토 PASS. 지적과 해소:

- D017-01/02: OS lock·pending·reload 소유권/충돌, Center 독립 sweep 추가.
- 계산 검토: 범위 밖 후보도 탐색 계속, 비단조/사망146·얇은 벽36·범위54/256 분석값, 실제 추종 불변식·반전 중간 도달 확인.
- V01701: 0입력 벽 앞 정답을 불가능한 -58부근에서 -72로 수정.
- V01702: 관측 offset 역산 대신 생산 FormationOffset catalog를 사전 동결하고 초기 actual/예약·공통 변위·추종/보류 검증.
- V01703: 응답 성공 bool 대신 실제 소유권/phase와 ready 재연결 후 정리.
- V01704: 캡처 호출 전 PLANNED intent 기록, 미확인/변조 생성물은 무삭제 BLOCKED+pending 보존. 확인된 이미지/meta 정리·전체 대상/소스 검사 이후 pending 제거.

## 현재 실행 근거

- [Python 시험](../../output/map-i2-017/python-unit-tests-verified.json)·[원본 로그](../../output/map-i2-017/python-unit-tests-verified.log): **103개 PASS**, 새 이동 판정/잠금/복구 음성 시험27개 포함. 전후 소스 지문 동일.
- [최종 Unity 실행](../../output/map-i2-017/3cd52b046b9a4bbda5212b07695d1c1c/summary.json): **PASS**, 16:33:53~16:34:11 KST. Unity 분석/자료 시험63개, 실제 프레임42사례/1,340스텝, 재현12쌍, 독립 sweep7,491회. 프레임4→4734, 가상 Keyboard·콘솔·부모 정리 PASS.
- 포함: 1~4인×3진형 정상 이동2회, 얇은 벽/Center 관통 주입 검출, 4인 벽 정지, 오목/띠/경계, 0입력 추종, 비단조/alpha0/보류/사망 예약, 반전, 무효 초기, dt clamp/잔여 시간 clip.
- [준비 응답 손실](../../output/map-i2-017/9ea4522bb3e441de960b34fde1a207bf/summary.json)과 [Play 응답 손실](../../output/map-i2-017/ed367554500d4de986e848b04a72dc9f/summary.json): 최종 소스 **PASS**. 씬/선택/background 동일 복원, owner/표식/장치0, Play 정지, 기존 대상 바이트 불변.
- 독립 검토자가42사례를 재판정하고12쌍과현재/증거 지문을 대조했다. 별도 계산 검토는1,340스텝 불변식을 재계산했고 최대 추종 오차 .000012285<허용 .00003을 확인했다. NONMONOTONIC/DEAD Center3.9921875, THIN -8.0625, REVERSE 중간84.0000076/최종≈0과 분석값이 일치했다. 관통 주입은 **관측 FAIL/탐지 시험 PASS**로 구분한다.
- [벽 앞 파티](../../output/map-i2-017/3cd52b046b9a4bbda5212b07695d1c1c/wall-party.png)와 [가상 키보드 이동](../../output/map-i2-017/3cd52b046b9a4bbda5212b07695d1c1c/movement.png)을 메인/독립 검토자가 직접 열어 배치·벽 앞 정지·화면 경계를 확인했다. 캡처 후속 프레임과 마지막 관측 프레임을 구분했다.

[기존 게임 all --screens](../../output/verification/20261031-163532-bf181428/summary.json)는 새 검사기/런타임 지문에서 PASS다. 컴파일·695규칙·실제 프레임/입력·3개 화면 크기24화면·콘솔·정리와 소스 안정성을 확인했다. 최종 local/gate는 마감 기록 `output/map-i2-017/final-checks.json`에 결합한다. 추가 Tripo 생성/크레딧 소비는 없다.

## 보존한 실패 이력

CLI fence/목록 반환 형식의 초기 실패, SceneSetup 역직렬화 실패를 보존하고 필드 DTO/왕복 시험으로 수정했다. unsaved scene 이름이 reload에서 사라진 실패는 독립 검토 후 같은 PID·owner/nonce·단일 빈 씬·장치0/OS lock/pending을 확인한 한정 회복을 수행했다. 이후 이름 대신 일반 root 표식과 구성/씬 설정을 검증한다. 실패 기록은 소급 PASS로 바꾸지 않았다.

첫 전체 실행 [e20d8b…](../../output/map-i2-017/e20d8b642cb74b4ca2ce698e2e7321f6/summary.json)은 이동 검사는 통과했으나 Assets 캡처 생성으로 전체 BLOCKED였다. 원래 파일 변경0을 확인하고 사전 부재/해시/복사본이 일치한 자체 이미지2개·meta만 정리해 이전 지문을 복구했다. 최종 실행은 캡처 생명주기까지 포함한 새 PASS다. 부모 finally 정리 PASS를 전체 CLEAN/외부 watchdog PASS로 확장하지 않는다.
