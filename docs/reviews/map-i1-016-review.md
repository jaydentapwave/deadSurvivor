# MAP-I1-016 로컬 기록 계층 검토

2026-10-31 · L2 · [설계/명령](../design/map-i1-016.md) · [fixture plan](../design/map-i1-016-plan.example.json).

I1의 manifest·고유 실행/nonce·원자 journal·packet 수집·누락/중복/다른 실행/중단·변조 검출을 구현했다. scope는 PROTOCOL_FIXTURE_ONLY다. 기록이 모두 유효해도 collector_status만 PASS이고 overall/Unity/실제 cleanup/일반 맵 사례/FI/축별 결과는 NOT_RUN이다. full map v1 자료 인수·Unity 소유권/reload/driver·실제 입력/이동·충돌/복구는 후속 I2 연동 범위다. I1 합성 관측을 실제 게임 프레임으로 전용하지 않는다.

## 독립 검토

design-reviewer `/root/map016_design`의 D016-01(종료 후 명령/결과 불변), D016-02(OS boot 식별·시계 연속성)을 반영하고 재검토 PASS를 받았다. validator-client `/root/map016_validator`의 V016-01은 pending packet과 임시 파일의 종료 후 변조/삭제 미검출이었다. 존재/해시/크기·packet 계약과 종료 요약의 존재/해시 동결을 추가했고 코드 재검토 PASS다. 종료 marker의 deadline 초과도 collector PASS로 이어지지 않도록 경계 시험을 추가했다. 메인이 구현하고 검토자는 읽기 전용으로 검토했다.

## 실행 근거

- [전체 Verify 시험](../../output/map-i1-016/unit-tests.json):76개 PASS(새 I1 31개 포함), 실행 전후 producer/검사 소스 지문 동일. [원본 로그](../../output/map-i1-016/unit-tests.log).
- 새 I1 시험에는 packet 저장 전/저장 후 ACCEPTED 전 작업자 프로세스의 `os._exit` 종료를 포함한다. 실제 프로세스 종료 후 OS 잠금 해제와 pending 보존·abort·추가 변조 거부를 확인했다. 실제 Unity worker/reload 시험은 아니다. 부팅 변경은 모의 식별값으로 검사하며 실제 OS 재부팅을 했다고 주장하지 않는다.
- [CLI 통합](../../output/map-i1-016/cli-integration.json): 실제 OS boot 식별값을 사용한 별도 Python CLI13회. 정상2사례 collector PASS/overall NOT_RUN(exit2), 다른 nonce 혼입 FAIL, 사례 누락 BLOCKED, 반복 finalize 불변과 종료 후 abort 거부를 확인했다. 모든 packet은 명시적 recording fixture다.
- 전체 JSON/packet/journal/cleanup/요약과 CLI 오류는 실행별 output에 보존한다. OS boot 접근 불가·시간/증거 제한·잠금은 성공으로 바꾸지 않는다. 상주 watchdog·Unity 복구는 미구현이며 cleanup PASS를 만들어내지 않는다.

I1은 Python 도구와 문서 변경이며 DSGame/UnityMigration·실제 씬·자산·패키지·hook/baseline을 수정하지 않았다. 계산/배치 알고리즘 변경이 없어 별도 algorithm-tester는 해당 없고 I2에서 필수 적용한다.

- [기존 게임 전체 회귀](../../output/verification/20261031-160923-5b2345ba/summary.json): 현재 검사기 지문으로 all --screens PASS. 컴파일·실제 프레임/입력·규칙·화면 검사·소스 안정성을 확인했다. 3개 화면 크기의 검사와 캡처를 보존했다. 1560×720 장착 전투와 1024×768 전리품 화면을 직접 열어 배치·글자·화면 경계를 확인했다. 물리 장치 시험은 NOT_RUN이다.
- 독립 검토자는 전체76개 시험·CLI13회와 현재 producer 소스/저장된 증거 해시를 직접 대조해 PASS로 확인했다. 최종 all/local/gate·독립 실행 증거 확인·현재 소스/보고서 지문은 `output/map-i1-016/final-checks.json`에 기록한다.

I1 로컬 기록 계층의 완료와 실제 맵/Unity 실행은 구분한다. 기존 게임 회귀 PASS를 새 맵/G2 PASS로 승격하지 않는다.
