# 044 검증 기록 — 현재 형태 프로토타입 본편 (3D 맵 + 2D 캐릭터)

- 날짜·범위: 2026-11-02 KST, L1. [브리핑](../design/proto-main-044.md). 메인은 Claude, 독립 검토는 Codex(gpt-6.1-sol, 읽기 전용)가 맡았다. 같은 시간대에 043 세션(공용 로우폴리 셰이더)과 Unity 사용 순서를 메시지로 조율했다.
- 변경:
  - 새 장면 `Assets/DeadSurvivor/Scenes/Proto044/Proto044_Main.unity`: Map038 복제 + `CombatEnabled` 0→1, guid `35710fee915754d108ad84323d316791`
  - `ProjectSettings/EditorBuildSettings.asset`: 빌드 장면 [Proto044_Main, DeadSurvivor]
  - `Assets/DeadSurvivor/Runtime/DSUI.cs`: 비전투 화면 전체 배경판(`PageBackdrop`)
  - 검증 도구 `Assets/DeadSurvivor/Editor/DSProtoMain044Verification.cs`, `Tools/Proto/build_proto_main_044.cs`, `Tools/Proto/proto_main_044.py`
- 원자료(Git 제외): `output/proto-main-044/r001/`. 최종 정상 실행 `run-2091b5ab890a`(145초, 실행 소스 SHA256은 `report.json`의 `sources`).

## 최종 판정

| 요구(브리핑 완료 조건) | 상태 | 근거 |
|---|---|---|
| 1. 빌드 첫 장면이 3D 맵 본편 | PASS | 빌드 설정과 장면 메타의 guid 일치. 2D 장면은 두 번째로 유지 |
| 2. 로비 → 10웨이브 → 승리 → 결과 → 재편성 → 재출전 | PASS(제한) | 실제 UI 버튼으로 진행했다. 웨이브마다 실제 프레임을 2~5초 돌려 적 생성·전투 진행을 확인한 뒤, 나머지 시간은 큐 명령 `DebugFinish`로 넘겼다. **자연 완주(435초)·밸런스 증거가 아니다** |
| 3. 전멸 → 패배 → 결과 → 재편성 | PASS(제한) | 파티 HP를 0으로 **주입**한 뒤 실제 프레임이 전멸을 판정했다 |
| 4. 터치 조이스틱 | PASS | 가상 `Touchscreen` 장치 이벤트로 MovementInput을 오른쪽으로 끌었다. 조이스틱 (1,0), 파티 x가 +437 이동, 손을 뗀 뒤 드리프트 0. 실기기·멀티터치는 NOT_RUN |
| 5. 1280×720·1560×720·1024×768 화면 | PASS | 화면마다 게임 뷰를 실제로 그 크기로 바꾸고, `DSScreenVerification.Check`로 렌더 타깃·캔버스·레이아웃 크기와 버튼이 화면 안에 있는지 확인한 뒤 캡처했다(15/15). 비전투 12장은 가장자리 픽셀이 배경색과 일치 |
| 맵 명령 오류·새 콘솔 오류·보호 파일 변경 | 없음 | 보호 파일: ProjectSettings, 파이프라인 설정, 폰트, Map037 Foliage, 장면, 입력 액션. 실행 전후 SHA256 비교 |
| 2D 본편 회귀 `run.py all --screens` | PASS | `output/verification/20261102-041046-e9dbfcbd/summary.json`(04:10:46~04:12:31): compile·frames·initial_load·695 rules·interaction·capture·screens. 044 런타임 변경과 043 Assets 최종본을 포함한다 |
| gate | PASS | `gate-20261102-042040-65596a1f`: docs RUN, 나머지 재사용(런타임 증거는 위 회귀) |

웨이브별 생성 수(2초 시점)는 실행마다 5~23마리였다. 파티 위치(터치 시험 후 이동)와 맵 경계에 따라 생성 각도가 거부되기 때문이다(브리핑의 알려진 한계). 웨이브는 시간으로 끝난다.

## 구현 중 실패와 수정 (원자료는 같은 폴더의 이전 `run-*`)

| 단계 | 실패 | 원인과 수정 |
|---|---|---|
| 선발 | `SUBMIT_REJECTED` | 맵 명령 큐는 정해진 출처만 받는다. 시험 출처 `TEST`로 바꿨다 |
| 로비 | 프레임 1에서 정지 | 무인 CLI에서 편집기 포커스가 없어 Play가 멈췄다. 실행 동안만 `set_run_in_background` 덮어쓰기를 켠다 |
| 터치 | 좌표 JSON 키 오류 | `Rect`의 JsonUtility 필드 이름 차이. 명시적 JSON으로 바꿨다 |
| 터치 | 장치에 터치가 기록되지 않음 | 기본 입력 설정이 포커스 없는 게임 뷰의 포인터를 버리고 백그라운드 장치를 끈다. 기존 `DSOwnedInputScope`와 같은 방식으로 설정 복제본(`AllDeviceInputAlwaysGoesToGameView`, `IgnoreFocus`)을 터치 동안만 설치하고 원래 객체로 되돌린다 |
| 화면 | 비전투 화면 가장자리에 맵이 보임 | 고정 1600×900 페이지가 캔버스를 다 덮지 못했고, 2D에서는 같은 색 카메라 배경 때문에 보이지 않았다. `DSUI`에 전체 배경판을 추가했다 |

## 독립 검토 (Codex)

**1차(r001, 04:01~04:02): 스펙 BLOCKED, 동작 계약 FAIL**

- R1: 캡처 도구의 `--width/--height`는 게임 뷰 텍스처를 늘리거나 줄여 저장할 뿐이라 화면비 검증이 아니다.
  - 메인이 놓친 결함이다. `DSScreenVerification` Resize/Check/Restore로 실제 해상도를 검증하도록 바꿨다.
- R2: 예외가 나면 입력 설정이 복구되지 않는다.
  - 정리 단계를 각각 독립적으로 시도하게 바꿨다. 입력 범위 안에서 강제 실패(`touch_inside`)를 내서 7개 정리 단계가 모두 실행되는 것을 확인했다.
- R3: 장면 구성이 일부만 복구된다.
  - 전체 SceneSetup 행을 저장하고 복원한 뒤 비교한다. OpenMain 이전부터 복구를 시도한다.
- R4: 실행 소스와 보호 파일 증거가 부족하다.
  - 실행 전후 SHA256을 비교하고, 판정 함수를 분리했다. 판정 단위 시험 8건 PASS. 콘솔 조회가 실패하면 BLOCKED다.
- R5: 회귀와 gate가 NOT_RUN이다.
  - 위 회귀와 gate를 실행했다.

**2차(r002, 04:13~04:14): 스펙 PASS, 동작 계약 FAIL**

- R1~R3 해소. R4 판정 결함(콘솔 조회 실패 시 PASS 가능)과 Resize 부분 실패 복구는 수정했다.
  - Resize 부분 실패는 실제로 강제(`resize_fail`)해 게임 뷰 크기가 RESTORED 되는 것을 확인했다.
- "시작값이 true여도 끝에 false로 바꾼다"는 지적은 **CLI 의미를 다르게 읽은 것**으로 판단한다.
  - `set_run_in_background --enable false`는 "덮어쓰기 종료, 프로젝트 자체 값 사용"이다(CLI 응답 원문).
  - 시작값 true 사례에서 끝값이 true로 돌아오는 것을 확인했다.
  - 지적에 따라 한때 "기록값으로 다시 설정"으로 바꿨더니, 오히려 덮어쓰기 표시가 `ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json`에 남았다. 043 세션의 gate를 잠시 막았고, 덮어쓰기를 다시 켰다 꺼서 HEAD와 같게 되돌렸다.
  - 최종 코드는 `--enable false`를 쓰고, 복원된 값이 시작값과 같은지 검사한다. 이 파일도 보호 대상에 넣었다.
- 후속 제안(복원 비교에 `isSubScene` 포함)은 반영하지 않았다. 프로브가 Assets 안에 있어서, 반영하면 방금 PASS한 회귀를 다시 돌려야 한다. 다음 개정에서 처리한다.

**3차(r003, 좁은 범위): 스펙 준수 PASS, 동작 계약 PASS.**

- R4 판정은 해소됐다(8건 재실행 일치).
- Resize 부분 실패 복구도 해소됐다(`run-d155847fb7d4` RESTORED).
- 백그라운드 설정 지적은 **검토자가 철회**했다. `EditorPipelineConfig.json`이 HEAD와 같다는 것도 확인했다.
- 최종 정상 실행의 소스 SHA256 7개가 현재 파일과 일치한다. 2D 회귀 시점의 Assets·ProjectSettings 지문도 일치하므로 Unity 회귀를 다시 돌릴 필요는 없다.
- 남은 일은 문서 정리 뒤 최종 gate뿐이다.

## 알려진 한계와 미실행

- 맵 명령 기록 상한 4096(브리핑 참조). 연속 수십~백여 판에서 세션이 멈출 수 있다. 이번에는 고치지 않았다.
- 대표 구역 밖은 블록아웃 외형이다. 소품에는 충돌이 없다.
- NOT_RUN: 435초 자연 완주, 실기기 터치·성능·빌드(사용자 담당), 사람 플레이 밸런스(사용자 담당).

## 사용자 결정 대기

- 대표 구역이 10웨이브에 충분한지(직접 플레이)
- 전체 맵 미술 확장
- 카메라 55도·정사영 6의 최종 채택
- 커밋 시점
