# DQ 적용 8단계 — 구현 초안과 검증 차단 기록

상태: **PARTIAL**. 게임에 새 화면이 적용돼 한 판이 된다고 아직 주장하지 않는다. 기준은 `characters-brief.md` C8-01~08. 실행 위치는 `night-chars`이며 다른 작업 트리/원본 게임 소스는 읽거나 쓰지 않았다.

## 구현한 범위

- `Tools/dq_character_pack.py`: confirm 최종 표를 읽어 9종의 실사용 1,406장만 9장 PNG 시트로 묶음. lena 공격 두 방향의 명시 대체와 rock/contact_right 원본 예외를 반영. 폐기·미리보기·효과 PNG는 몸 시트에 넣지 않음.
- `Data/DQ/dq_character_clips.json`: 방향 표, 장별 시간, loop, eventFrame, 출처·약함, MonsterPhase 매핑. `DQClipData`, `DQClipMath`, `DQMotionLibrary`가 소비함. 기존 CharacterData.Motion·마스터데이터·세이브·판정 경로 기대식은 수정0.
- 메뉴 `DeadSurvivor/DQ/Apply characters` / `MainSceneDQCharactersMigration.Apply`: PPU48·Point·무압축·발 피벗으로 multiple sprite 임포트와 9개 atlas 생성. 기존 세운 판 재질 유지. Main/프리팹/배경/조명/shader 저장 변경0. 메뉴 실패 주입 진입점은 있으나 복구 시험 미실행.
- 대원: 기존 방향·이동 거리 시계 확장, idle/walk/attack/fall/lying, 피격 정지 장+기존 flash/1px 밀림. SpriteAtlasService가 기존 대원/몬스터 정지 그림 키를 DQ로 해소하므로 로비/진형/BattleHud 초상도 연결 예정(실제 UI 미확인).
- 실제 타격: 기존 공격 시점 그대로 값 기록→WorldPresenter LateTick 우선 적용→대상 카메라 endCameraRendering 후 소비. 여러 무기·걸음은 batch 최신 실제 타격 장으로 합성. 게임 시계와 피해 순서는 유지함(런타임 동등성 시험 미실행).
- 몬스터: 기존 phase 전달과 최종 몸 sprite 선택 분리, 이동·고유 phase·실제 contact/shoot 통지·사망 표시. 활성 등록 해제와 풀 반환을 나눔. 사망 표시 목록/게임 카메라 렌더 확인/재시작 정리 경계는 아직 실검증하지 않음.
- `DQCharacterMotionTests`: 데이터/시간 경계/공격 eventFrame/타격 보존/실제 CombatService 큰 걸음 시험 초안. 컴파일 시점에는 오류0이었지만 테스트 결과는 없음.

## 실제 실행 결과

근거 폴더: `output/hd2d-dq/apply/8/`. 복사 동결 증거는 인계의 artifacts를 참조한다.

| 관문 | 결과 | 실제 근거·한계 |
|---|---|---|
| project-doctor design.write / unity-client.implement | PASS | configured/findings0; 설정 확인만 |
| 실사용 시트 재생성 멱등·원본 픽셀/해시 일치 | PASS | `pack-check.json`: 재실행 출력 동일, 1,406장 전체 crop==원본 RGBA, JSON 참조/시간 일관성. Unity 시험 아님 |
| 중간 컴파일 4회 | PASS(중간 소스만) | compile-data/heroes/monsters/tests.json errors0. 이후 피격 장 고정·발 이동 시계·사망 렌더 확인·부활 시계 정리를 수정했으므로 **현재 소스 컴파일 관문은 BLOCKED** |
| 이행 메뉴 첫 실행 | PASS(1회 실행만) | migrate-first.json success=true/applied. 실제 atlas 페이지·임포트·두 번 반복/예외 복구 판정은 미완료 |
| 최종 컴파일0·관련 EditMode | BLOCKED | test-motion.json INTERRUPTED. UI의 Unity 약관 미동의 및 명령 대기. 시험 개수/통과 수 추정하지 않음 |
| 짧은 Play·연속 캡처 | BLOCKED | play-start.json EDITOR_TIMEOUT, command did not run. script eval도 대기 timeout. 본 게임 캡처0·직접 연 본 게임 장0 |
| 기본 tools/playable.py 판정 경로1회 | NOT_RUN | Play 진입 차단 뒤 같은 실패를 반복하지 않음. 기대식 수정0 |
| design-reviewer | PASS | `VERDICT: PASS`, job 20261123-034539-34753d. 구현 전 설계만 |
| spec-reviewer / validator-client / algorithm-tester | BLOCKED | 명령 관문 미완료로 마감 리뷰 요청 전. 자체 점검을 독립 리뷰로 쓰지 않음 |
| 성능·그림 품질·9단계 FX/UI | 범위 밖 | 측정/재생성0. 기존 원근 Main 관문 BLOCKED·terrain N9 FAIL도 유지 |

## 명시된 기준과 차이·못 한 것

- **bruno fall_right**: confirm 최종 표와 4-bruno 인계 모두12장(마지막1000ms), 공통 지시는10장+lying. 최신 채택 표 우선 지시에 따라12장을 보존했다. `pack-report.json`에 expected10/actual12를 적었고 C8-03을 PASS로 쓰지 않는다. 디렉터 확인 필요.
- source r2의 slime/contact4·fall, mushroom/shoot_right/upright·fall, rock/contact4·fall 시간은 원본과 다르다. `pack-report.json` differences에 양쪽 배열 보존, r2 PNG 수와 r2 두 JSON 형식·durations를 대조해 사용. rock/contact_right는 허용 원본 시간.
- 약함 클립 전수 표는 `DQAppliedCharacters/source-manifest.json` 및 데이터의 quality/sourceClip로 전달한다. 그림 자체는 손질하지 않았다.
- 메뉴2회 동일/실패복구, 종당 texture1장·sharedMaterial, fallback0 실제 진입점, 대원/몬스터 전 동작 연속, 발/크기/그림자, 예전 몸/초상 잔재0, 3화면비, 기본 한 판, 마감 리뷰는 모두 남음.
- r2 예고 라인 파일 반입은 아직 못 함. 9단계 화면 적용은 이번 범위 밖. 원본 허용 경로 `motions-monsters-r2/boar-telegraph-line/tiles/{start,body,tip}_00~03.png`, `warning_up/frame_00~03.png`, durations/assembly/integration JSON에서 실제 쓰는 것만 다음 반입 때 이행 메뉴로 임포트한다. 비교/GIF/디코딩 산출물은 반입하지 않는다.
- `.ai/rules/25d-*.md`4개와 fix-and-recheck.md 부재는 이전 설계 기록대로 유지. 이 세션에서 임의 설치/전역 설정 변경하지 않음.

## 다음 세션 진입점

1. 디렉터가 Unity 약관 창을 확인한다. 에이전트는 법적 약관을 대신 수락하지 않았다. 두 에디터 중 반드시 night-chars 대상으로 확인한다.
2. 이전 test run 클라이언트는 중단했으나 서버 쪽 시험 종료 여부는 확인하지 못했다. 우선 자기 에디터의 `test status`·Play 여부·모달을 확인하고 미완료 시험을 정리한다. 형제 에디터 조작 금지.
3. `unity recompile --project-path "$PWD" --format json`, 메뉴 Apply2회/실패주입, 새 DQCharacterMotionTests. 실패 원인을 고친 뒤 같은 관문 재실행. 실제 공격/방향/풀 경계와 DR-02-T(새 view 첫 렌더·pause 시계)를 이어 검증한다.
4. 아직 DQ 전용 probe/capture 진입점을 추가하지 않았다. 기존 PlayableProbe 확장으로 실제 fallback과 frame/event 기록을 노출하고, 기존 캡처 도구의 범위 내에서 연속 캡처를 수집한다(새 범용 하네스 금지).
5. 캐릭터 짧은 Play/3화면비와 관련 EditMode 묶음 후 기본 판정 경로1회. 커밋하고 spec-reviewer·validator-client·algorithm-tester를 한꺼번에 요청한다. design PASS 방향은 유지하며 재설계가 아니면 다시 부르지 않는다.
6. 6·7브랜치 통합 후9단계에서 메뉴 재실행. 기존 Main 원근 관문 연결은9단계가 맡는다.

## 놓친 것·시간·토큰

- DR-01: 현재 공격 시계만 보면 타격 뒤 같은 걸음의 새 공격을 놓치는 문제. 값 스냅샷/렌더 소비 계약으로 설계에 반영하고 표시 코드·시험 초안도 작성했다. 실검증은 차단 상태.
- 이번 놓친 것: 테스트가 응답하기 전 Play 명령을 추가 제출했다. Play는 대기 timeout으로 실행되지 않았고, 결과 확인 전에 다음 에디터 변이를 제출하지 않도록 이 검증 기록의 다음 명령 순서에 반영했다. 공용 워크플로우 수정은 범위 밖으로 미반영.
- 환경: unity status ready와 heartbeat가 정상이어도 법적 약관/명령 대기로 Play·시험 성공이 보장되지 않았다. 약관 승인 UI는 관찰만 했고 클라이언트 대기를 Ctrl-C로 중단했다. 이를 게임 시험 FAIL이나 PASS로 바꾸지 않는다.
- 시간: 구현 시작 콘솔 확인 UTC18:47:00, 패킹·데이터18:47~18:50경, 표시 코드/컴파일18:50~18:53경, 시험/모달 원인 확인18:53~18:55경, 최종 정리·정적 패킹 확인18:55~18:56:02 이후 기록. 앞 설계 시작 시각 미수집으로 전체 시간을 추정 합산하지 않는다. 최종 인계 created가 이번 종료 시각이다. 시간 상한에 도달해서 멈춘 것이 아니라 법적 동의/에디터 실행 차단이다.
- `Tools/Harness/session_time.py --markdown`은 기본 jsonl 부재로 실패(앞 기록). 현재 Codex/Claude/하위 에이전트 토큰 실제량 미제공. 하위 에이전트 호출0, 유료 생성/패키지 설치0. 단위별 초 단위 시간/토큰을 임의 수치로 채우지 않았다.
- 추가 검사는 변동 시간·공격/풀 경계로 제한했다. 기존 고정fps 시각 시험 중복 실행을 생략 후보로 남겼으며, 검사 자체 삭제/문턱 완화0.
