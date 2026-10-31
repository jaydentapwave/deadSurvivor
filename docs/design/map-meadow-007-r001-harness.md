# MAP-MEADOW-007 r001 — 4. 하네스 실행 계약

상태: 설계, 미구현. [근거대장](map-meadow-007-r001-evidence.md) E04~13, [기존 단계별 필수 목록](맵-워크플로우-하네스-설계.md), [사례군/FI 원본](맵-하네스-사례명세.md)을 함께 적용한다. E11의 데이터 검사/실제 프레임 시험 구분을 사용하며 별도 패키지 설치나 Unity 조작은 하지 않았다.

## 책임과 데이터 흐름

| 역할 | 소유/입출력 | 실패 처리 |
|---|---|---|
| 준비 자료 | 007 layout은 원본 픽셀 V/C/U·통로 후보만 소유. 실제 게임 자산으로 승격은 별도 개정 | UNKNOWN C, 단위/기준 누락은 실행 인수 BLOCKED |
| 게임 | DSGame이 입력·Center·실제 캐릭터·Formation/Pending·HP·웨이브·시간 소유(E04~07) | 시험기가 게임 공식을 재구현하여 성공으로 대체 금지 |
| 이동/지면 질의 어댑터(향후) | 게임이 후보 위치/구간을 전달하고 유효성/최소 여유/최초 접촉 수신. 같은 개정 C로 플레이어·후속 적을 검사 | null/NaN/오래된 개정은 실패, 자동 통과 금지 |
| 실행기(향후) | 사전 자료 고정→격리 초기화→실제 입력 전달→관찰→종료→복구 | 직접 좌표를 매 프레임 설정하는 것은 입력 재생 아님 |
| 관찰기/판정기(향후) | 실제 입력 전/후 게임 상태와 dt·step ID·이벤트·구간을 기록, 종료 후 기준과 비교 | 게임이 계산한 성공 flag만 믿지 않고 원자료 대조. 누락/중복 step 검출 |
| 기록/복구(향후) | 독립 실행 ID·원자료·스크린샷 지문·종료/복구 상태 저장 | 취소/충돌 때도 미완료 표시. 사용자 기존 씬/상태를 무차별 덮어쓰지 않음 |

## 실행 묶음 필수 필드

`run_id, package_revision, layout_sha256, reference_png_sha256, code_hashes, policy_revision, threshold_revision, scenario_ids, seed, unity_version, platform, dt_schedule, viewport_pixels, UI_rects, camera_parameters, scale_s, scale_k, body_shapes, initial_state, input_steps, expected_end, cleanup_scope`.

각 관찰 스텝: `step_id, input, frame_unscaled_dt, config_speed, dt_requested, dt_applied, state, paused, center_before/after, slots_actual_before/after, slots_target, reserved_slots, hp, formation, pending_formation, swept_contacts, boundary_distance, progress, simulation_time, wall_time, observer_sequence`. 필드명은 구현용 확정 API가 아니라 필요한 정보 계약이다. 빈 필드를 기본값0으로 채워 실행 준비로 취급하지 않는다.

판정 출력: 사례별 `status, reason, first_bad_step, observed/expected, missing_evidence, artifact_hashes, cleanup_status`. 기존 FAIL/BLOCKED/NOT_RUN/PASS 우선순위 유지. 존재하는 그림을 실제 실행 캡처 필드에 넣으면 FAIL.

## 입력 조리법

- **HR-ROUTE:** RT의 순/역 경유점을 s로 변환. 안전 시작 원에서 모든 실제/예약 슬롯을 초기화하고 초기 오차·충돌을 확인. 먼저 CAL01 기준으로 생성한 입력열을 동결한다. 경유점 추종으로 입력열을 만드는 보정 실행과 그 입력열을 고정 재생하는 판정 실행을 구분한다. `input=clampMagnitude(nextTarget-center,1)` 같은 피드백 실행만 두 번 하는 것을 동일 입력 재생이라고 부르지 않는다. 각 입력 스텝의 벡터/dt를 저장하고 실제 도달 여부는 별도로 판정한다.
- **HR-CONTINUOUS:** RT006 정방향→RT018 정방향→RT008 역방향. 공통 접점 좌표는 (285,900),(330,950). 중간 초기화·좌표 보정·진형 재설정 금지. 역방향은 전체 연속열을 반대로 정의. 중앙 진입과 다른 출구 이탈을 모두 관찰한다.
- **HR-STOP-REVERSE:** 안전한 지정 중간 지점까지 고정 입력→0입력 hold_steps→반대 입력. 전/후 추종 구간 모두 기록. hold_steps/도착 조건이 없으면 BLOCKED.
- **HR-STATE:** 기준 지형의 안전한 경계/통로 위치를 먼저 등록. 사망 슬롯, 현/예정 진형, HP/장비 초기값, 이벤트 순서를 지정하고 선택/부활/출발은 실제 게임 진입점을 통해 호출. 존재하지 않는 위치/유효성 fixture는 만들었다고 가정하지 않음.

[추가 사례 CSV](map-meadow-007-r001-cases.csv)는 RT018 24개 + 원형 연속 24개 + 상태 순서 시나리오 8행이다. 48개는 파티/진형/방향 조합, 8행은 아직 위치·전환 진형별 조합을 펼쳐야 할 시나리오다. 합계56행을56회 실행 또는 전체 사례 수라고 부르지 않는다. 기존005의372개는 그대로 보존하며 상호 참조한다.

## 상태 순서 시나리오의 기대값

| 조리법 | 순서 | 기대 |
|---|---|---|
| REVIVE_THEN_CHANGE | 사망→정비→현 슬롯 부활→예정 선택→출발 | 부활 위치는 현 진형. 이후 전환의 실제 구간도 안전 |
| CHANGE_THEN_REVIVE | 정비→예정 선택→현 슬롯 부활→출발 | 부활로 선택 시 검사가 무효화되지 않도록 출발 재검사 |
| CHANGE_CANCEL | 예정 선택→취소→출발 | 현 진형·슬롯 보존, 임의 위치 변경0 |
| INVALID_PENDING_KEEP_CURRENT | 넓은 예정 진형이 벽과 겹침 | 선택 거절, 유효한 이전 선택 유지, 현 진형 출발 가능 |
| INVALID_NEXTWAVE_NO_MUTATION | 출발 직전 조건 변경 | 검사 실패 시 Wave/시간/적/장비/HP 불변. 현 유효 진형 대안은 사용자 선택으로 수행 |
| DEAD_SLOT_RESERVED | 통로 진입 중 동료 사망 | 죽은 슬롯을 지워 좁은 길 통과 금지, 현재 예약 부활 위치 유효 |
| STOP_REVERSE_SWEEP | 사선/곡선에서 정지→반전 | 끝점 밖이어도 사이 관통 검출, 진형 회전/워프 없음 |
| PREP_TIME_FROZEN | 정비에서 입력·선택·부활·취소 | 전투 이동·전투 타이머 정지, 정비 실제시간 별도 |

## 하네스 인수 및 복구

1. G2에서 알려진 정상 도형과 FI-01~10 **전체**를 검사한다. FI03의 얇은 벽은 E09가 지적하는 스텝 간 누락을 구체화한 실패주입이다. 출처가 FI의 정확한 수치를 정해 주는 것은 아니므로 fixture 치수는 구현 시 단위와 함께 고정한다.
2. 정상 대조군도 같은 입력/관찰/판정 경로를 사용한다. 의도한 오류 종류와 다른 예외로 멈추면 탐지 성공이 아니다. 정상 도형에서 기대 관통0과 도달을 동시에 확인한다.
3. G2의 재현/복구 증거가 확보된 뒤 G3 실제 후보 맵의 전 필수 사례를 실행한다. 작은 도형 시험은 실제 맵 증거가 아니다.
4. 복구 전에는 원래 씬, 실행 주체, 직접 변경한 설정/입력/카메라/시간배율을 스냅샷한다. 격리 시험 씬/상태 사용을 우선한다. 종료 시 직접 만든 자원만 제거하고 직접 바꾼 값을 복원·대조한다.
5. 프로세스 종료로 복구 불가 시 interrupted 기록을 남기고 다음 사전 확인에서 잔여 변경을 조사. 결과를 PASS로 마감하지 않으며 사용자 기존 플레이 상태를 추측해 복원하지 않는다.
6. 모든 JSON/CSV/PNG/SVG와 코드·기준값 지문을 실행 묶음에 포함한다. 기존 local fingerprint가 확장자에 따라 빠뜨리는 자료는 별도 manifest로 보완한다. 문서 검사만으로 런타임 지문 완전성을 보장하지 않는다.
