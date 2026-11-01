# MAP-FULL-026 r001 진행·검증 기록

- 날짜: 2026-11-01. 사용자 요청: 024 N1~N3 전체 진행, 진행 여부 재질문 없이 계속. 성능 목표: 사용자가 현재 개발 Mac 선택.
- 규모: L2. [실행 브리핑](../design/map-full-026.md). 이 기록은 진행 중이며 전체 완료 보고가 아니다.
- 기존 미커밋 024/025 문서와 사용자 장면·입력·씬 파일을 보존했다. 커밋·업로드·유료 생성·회사 서비스 연결 없음.

## 현재 구현

- `map_full.py`: 별도 026 요청 preflight와 봉인된 증거 무결성 검사. full map v1 전체 인수기 아님. prepare PASS도 execution NOT_RUN / acceptance BLOCKED를 명시.
- `DSGame.MapFrame.cs`, `DSMapFullVerification.cs`, `map_frame_run.py`: 소유 실제 Update 입력·명령·시간·전후 상태·RNG/비직렬화 타이머 관찰과 독립 이동 판정. OWNED_FRAME_ADAPTER_ONLY.
- 일시정지 UI/키보드를 RequestPause 공통 진입점으로 연결했다. 일반 맵 전체 명령 큐 이행은 후속이다.
- DSMapSession 자료 바인딩의 고정4인 시작 검사를 제거했다. 실제 StartRun의 선택 인원별 안전 검사는 유지한다.
- 개발 Mac 환경과 측정 전 [프로필](../design/map-full-026-mac-profile.json) 작성. 부하 단계/성능·실제 빌드는 아직 미실행.

## 검증과 실패 보존

| 검사 | 상태 | 실제 근거와 범위 |
|---|---|---|
| 독립 설계 | PASS(부분 단위) | James: strict 기반과 OWNED_FRAME_ADAPTER_ONLY 사전 검토. 전체 G2/N1~N3 인수 아님 |
| 독립 코드 검토 | PASS(부분 단위) | Socrates: 초기5개 무결성 지적과 프레임/cleanup 지적 반영 후 bounded code-review PASS. 최종 실행 증거 확인은 후속 |
| 독립 계산 검토 | 지적 반영 | Hubble: 초기진형·적용dt·body길이·alpha0/0입력·FIFO·RNG 지적 반영. RecordTime/전체 연결/G2는 제외 유지 |
| 기반 단위 | PASS | map_full 41개 반례. 중첩 manifest/링크/용량/unknown coverage/거대수/봉인 후 치환 포함 |
| 프레임 판정기 단위 | PASS | map_frame 16개 분석/반례. 적용dt·정체·추종·0입력·부당한 hold·누락/잘린 예약·오래된 nonce 등 |
| 맵 관련 Python 시험 | PASS | 157개 시험 실행. 마지막0입력 반례2개 추가 전 수치이며 후속 재실행에서 구분 |
| 최초 실제 진단 | BLOCKED | `output/map-full-026/frames-bcc1a38e12bd46ed98d0d00b5f178383/summary.json`: INVALID_FRAME_PATH, cleanup PASS |
| 두 번째 실제 진단 | FAIL | `output/map-full-026/frames-49fef658ae2942a1965fc1d4755d7f00/summary.json`: STATE_CONTAMINATION, cleanup PASS. 렌더가 ViewWidth/Height를 바꾸는 관측 순서 수정 |
| 수정 후 실제 진단 | PASS(부분) | `output/map-full-026/frames-1aaddc9eefbe4908a44bc07f2aeda45c/summary.json`:19사례232프레임1152독립sweep·재생/정리 PASS. 이후 Python 판정기 강화 전 기록 |
| 실제 화면 직접 검토 | PASS(해당 캡처) | 같은 run의 device.png. 4인/지형/기존 HUD와 하단 장비 목록 확인. 전체 가림·3화면비 맵 인수 아님 |
| 기존 게임 회귀 첫 시도 | BLOCKED | `output/verification/20261101-165939-2462aebe/summary.json`: Map023 장면이 열려 있어 기본 gameplay 장면 전제 미충족 |
| 기존 게임 전체 회귀 | PASS | `output/verification/20261101-170009-a0631c5f/summary.json`: compile/frame/load/695규칙/입력/화면/정리. 원래 Map023 clean 장면 복원 확인 |
| 최종 판정기 실제 진단 | PASS(부분) | `output/map-full-026/frames-28bd072435fc4f6396d1fad110084c30/summary.json`: 현재 소스19사례232프레임1152독립sweep·재생/정리 PASS |
| 전체 G2/G3/N1·N2·N3 | NOT_RUN | partial/diagnostic 결과를 전체 단계 인수로 승격하지 않음 |

현재 실행 시각·소스/도구/대상·원자료 SHA-256은 각 manifest/summary/target-before/after에 보존한다. 컴파일 과정의 일시 syntax FAIL은 수정했으며 현재 all의 compile PASS와 역사적 compilationFailed 캐시를 구분한다. 검사/hook/baseline 변경 없음.

## N1 자료 진단

`output/map-full-026/route-diagnostic.json`: 원본29개 경로 ×12파티/진형348행의 읽기 전용 점유/직선 질의. 82행 불안전. RT002/005, LC02/10은 실제 경로 보완 필요. RT009 diagnostic과 RT012~015 boundary_required는 도착 일괄 합격 대상으로 바꾸지 않는다. 이 수치는 실제 프레임·연결성 결과가 아니다.

전체 공간 구성공간 인증, 일반 맵 전체 큐/관찰 계약, full map v1 어댑터, 전체 FI01~10·외부 watchdog/복구가 남아 있다. 이후 G3 → G4/부하/Mac Player → 대표 구역 미술/영향 재검증 순서를 유지한다.

## MQ026 정상 맵 명령 큐 진행

- James의 MQ-01~04 사전 설계 PASS 후 `DSGame.MapCommands.cs`와 UI/키보드 제출·정상 Update 소비·직접 도메인/Simulate guard를 연결했다. LegacyNoMap 동기 동작은 유지한다.
- Socrates의 예외 기록·용량 사전 확보·등록된 객체 identity 지적을 반영하고 독립 코드 PASS를 받았다.
- 실제 Unity `output/map-blockout-018/playable-22-3415a9c88de84ef3bdf83f1195c1d6d8/summary.json` PASS:54개 큐 반례/불변식, 부분 변경 예외의 before/partial-after와 terminal 거부, 용량 부족 시 상태 변경 전 거부, 실제 프레임/시간·캡처·콘솔·원상 복구 확인.
- 이 큐 단위 PASS는 전체 G2/FI01~10·G3/N1 완료가 아니다. 큐 변경 후 기존Map22/23,19프레임 사례와 전체 게임 회귀를 새로 실행한다. 앞 절의 이전 소스 PASS를 현재 변경의 최종 인수 근거로 재사용하지 않는다.
