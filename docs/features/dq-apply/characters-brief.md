# DQ 적용 8단계 — 캐릭터·몬스터 구현 전 설계 브리핑

- 상태: **L2 / design-reviewer 대기, 구현하지 않음**.
- 기준 커밋: `1c780d043a2555b89b3b149f0477de488cb19637`, 브랜치 `claude/pack-expedition-2d-dq-chars`.
- 실행 위치: 현재 `night-chars` 작업 트리. 원본 프로젝트는 허용된 `output/hd2d-dq`·앞 단계 outbox만 읽는다.
- 적용 스킬: studio-kit `25d-art-apply`, 로컬 `ds-workflow`, Unity `using-unity`. 리뷰는 이번 사용자 실행 규칙에 따라 Claude가 담당하며 Codex는 호출하지 않는다.

## 1. 목표·규모·현재 코드 근거

새 대원 4명과 몬스터 5종의 채택 그림을 Main 한 판에 연결한다. 게임 규칙·세이브·공격 주기·피해 시점·기존 판정 기대식은 유지한다. 배경·빛이 예전 상태인 현재 작업 트리에서 캐릭터만 확인한다.

기존 구조를 확장하며 Animator나 별도 게임 상태 머신은 만들지 않는다.

| 기존 진입점 | 직접 읽은 사실 | 확장 방향 |
|---|---|---|
| `Effect/Facing/SpriteFacing.cs` | 다섯 방향 enum + 반전, 없는 방향의 가까운 것 선택 | 방향 계산 유지, 명시된 데이터 별칭을 먼저 해소 |
| `Features/Party/View/MemberView.cs` | 방향·걷기·공격 표 지원, 걷기 고정 fps, 대기 정지 그림, 쓰러짐 정지 그림 | 장별 시간 재생과 대기·쓰러짐 추가 |
| `Features/Party/Data/CharacterData.cs` | `MemberMotionData`는 fps·타격 프레임을 게임 공격 시계에도 제공 | 이 계약과 기존 데이터는 변경하지 않음, 시각 전용 클립 데이터 분리 |
| `Effect/Motion/MemberAttackMotion.cs` | CombatService 소유, Duration/HitSeconds/Elapsed/HasHit 공개 | 읽기만 함. 타격·발사를 애니메이션 이벤트로 다시 발생시키지 않음 |
| `Features/Field/Presenter/WorldPresenter.cs:270~305, 520~595` | 대원 클립 주입, 몬스터 alive만 갱신, 사라진 id는 즉시 Release | 클립 주입 확장, 몬스터 사망 표시의 별도 수명 필요 |
| `Features/Monster/Factory/MonsterViewFactory.cs` | 등록·풀 반환과 방향 캐시 소유 | 활성 등록 해제와 표시 종료 후 풀 반환을 구분 |
| `Core/Resource/SpriteAtlasService.cs` | GetSprite 복제를 이름 캐시, `_downright`→`_front`, `_upright`→`_back` 호환 | DQ 전용 atlas 경로·키로 로드; 기존 HUD/아이템 공급 유지 |

**L2 근거**: 몬스터가 모델 목록에서 제거된 뒤 쓰러짐을 재생해야 한다. 기존에는 살아 있는 모델의 등록 수명과 뷰의 풀 수명이 같다. 이를 활성 등록과 사망 표시 수명으로 나누는 상태 소유권 변경이므로 구현 전 설계 리뷰 대상이다. 단순 그림 치환으로 L1 처리하지 않는다.

## 2. 리소스·데이터 계약

- 생성물 예정 위치: `Assets/Game/03.Resources/Art/HD2D/DQAppliedCharacters/`. 대원/몬스터별 고정 격자 PNG 시트 한 장씩(총 9장)에 **실사용 프레임만** 넣는다. 채택 정지 그림·누운 그림도 같은 시트에 포함한다. 비교 시트·폐기 프레임·GIF·작업 중간물은 Assets에 넣지 않는다.
- 개체 종류별 SpriteAtlas를 `Assets/Game/03.Resources/Resources/Art/DQ/`에 둔다. 한 종의 정지·동작은 같은 atlas 한 페이지에 배치한다. 실제 pack 결과에서 한 texture인지 확인한다. 회전·tight packing 끔, padding 4, Point, 무압축, mip 없음. 최대 크기는 실제 채택 프레임 수·캔버스에 맞춰 정하고 다중 페이지면 실패로 본다.
- 시트 임포트: Sprite Multiple, FullRect, PPU 48, Point, 무압축 RGBA32(모바일 override 포함), mip 없음, sRGB, Clamp. 72×72 그림의 위에서 잰 발 `(36,64)`는 Unity 피벗 `(36/72,8/72)`로 변환한다. 몬스터도 원본별 canvas/발 앵커를 읽어 같은 방식으로 바꾼다. 불투명 상자 바닥을 프레임마다 피벗으로 쓰지 않는다(점프·쓰러짐이 흔들림).
- **기존 세운 판 sharedMaterial을 그대로 사용**한다. 개체/프레임별 Material 복제 없음. atlas texture와 SpriteRenderer만 바뀐다. `HD2D_SpriteLit.shader`, 배경·빛·후처리·지형 prefab/material은 수정하지 않는다.
- `Resources/Data/dq_character_clips.json`(예정)과 시각 전용 직렬화 타입 `Effect/Motion/Data/DQClipData.cs`(예정): `actorId, atlasPath, clips[{id, frameKeys[], durationsMs[], loop, eventFrame, pivotPx, sourceClip, quality}], directionBindings, phaseBindings`.
- `eventFrame`은 0 기반. 시간은 ms 양수, frameKeys와 길이 동일, loop는 원본/채택 판독 계약을 사용. 주석/출처 파일에 원본 PNG·JSON 해시, 선택 판과 명시 대체를 남긴다. 원본 경로만으로 채택을 추측하지 않고 `confirm-*.md` 최종 표를 전수 대조한다.
- 캐시 키는 atlas 경로와 sprite 이름의 쌍이다. 기존 SpriteAtlasService의 이름만 캐시하는 계약에 DQ 키를 무턱대고 추가하지 않는다. DQ sprite 이름도 actor/action/direction/frame을 포함해 유일하게 한다.
- 런타임 데이터 로드는 기존 ResourceService를 사용하고 scope 종료에 sprite 복제를 정리한다. DI의 게임 상태 소유권은 바꾸지 않는다.

## 3. 채택 판과 방향 매핑

판정 원본: 허용 읽기 루트 `output/hd2d-dq/judge-night/summary.md` 및 `confirm-hero-{bruno,lena,mira,bolt}.md`, `confirm-monsters-{a,b}.md`의 마지막 표. 단순히 최신 폴더가 있으면 쓰는 정책보다 이 표가 우선이다.

| 논리 방향 | 대원 소스 | 몬스터 소스 |
|---|---|---|
| Down | down | front (명시 별칭) |
| DownRight | front | front |
| Right | right | right |
| UpRight | upright | upright |
| Up | up | up |

왼쪽 세 방향은 반전한다. 수직 방향 반전은 기존 SpriteFacing 정책을 유지한다. 몬스터 4원화를 5칸 논리 표에 명시 매핑하고, 명시 대체와 예상 밖 누락을 구분한다.

| 명시 예외 | 채택/대체 |
|---|---|
| bruno attack_down | fix1, 다른 클립은 confirm 최종 표 |
| lena attack_down/up | fix1. fix2 up은 쓰지 않음 |
| lena attack_upright/front | 각각 fix2 attack_right / fix1 attack_down. 별칭으로 데이터에 기록 |
| lena idle_right | confirm 최종 표의 fix2 사용, 허리 투명 구멍은 약함으로 기록. 이 단계는 그림 품질 수정하지 않음 |
| rock contact_right | confirm 조건대로 구멍 있는 r2 대신 원본 사용. 원본과 비교 근거/선택 해시 기록 |
| boar fall_front | 사용 금지. 모든 방향 r2 fall_right, 왼쪽 반전 |
| boar 예고 라인 | r2 파일만 반입. 화면 연결은 9단계 |
| 약함인 나머지 클립 | 채택 표대로 사용, 품질 표와 디렉터 확인 필요에 남김 |

`FacingFallbackCount`는 명시 매핑 후 예상 밖 누락 횟수라는 기존 취지로 0을 확인한다. 대체를 숨기려고 카운터를 리셋하지 않는다. 명시 별칭은 별도 표/개수로 보고하고 모든 logical 방향 슬롯의 실재 sprite를 검사한다. 기존 probe 이름/형은 유지한다.

## 4. 재생·공격·사망 수명

### 대원

- 표시 우선순위: 쓰러짐 → 살아 있는 공격 → 이동 walk24 → idle12. 피격 번쩍임·1px 몸 밀림은 기존 경로로 합성하고 이동·피격 변환이 새 프레임에 이중 적용되지 않게 한다.
- 장별 시간의 누적합에서 `t < 누적 끝`인 프레임을 고른다. loop는 총 시간으로 modulo, nonloop는 마지막 장 유지. 음수 시간은 0, 큰 delta는 건너뛴 경계를 안전하게 처리한다.
- 이동 속도는 게임 값을 유지한다. 걷기 위상은 기존 이동 시계를 이용하되 방향 전환 시 같은 정규화 위상을 유지하고 정지하면 대기 시계를 시작한다. 기존 hop/대기 오프셋 중 새 그림에 이미 든 변환은 중복하지 않는다.
- 공격 시각 시계는 **기존 MemberAttackMotion을 읽기만** 한다. 기존 `HitSeconds`에서 채택 clip `eventFrame`이 보이도록 타격 전 구간/뒤 구간을 각각 시간 매핑한다. 전 구간은 원본 duration 비율을 유지해 HitSeconds에 맞춘다. 후 구간은 원본 속도로 재생하되 기존 Duration에 도달하면 잘라 끝낸다. 다음 공격이 오면 새 준비로 시작하며, HasHit 이후 회복을 취소하는 기존 규칙은 유지한다. HitSeconds=0, Duration=HitSeconds, 큰 delta 경계는 시험한다. 같은 frame에 현재 피해/발사와 시각 타격 장이 맞아야 한다.
- 공격 클립 데이터의 eventFrame이 실제 피해를 발생시키지는 않는다. CharacterData.Motion/CombatService/MemberAttackMotion의 타이밍·피해를 새 JSON 값으로 바꾸지 않는다.
- 대원 사망 시 방향 고정, fall_right10 이후 lying 유지(왼쪽 반전), 기존 어두워짐·HP 표시 계약 유지. 부활 시 fall/idle/attack 시각 시계를 초기화한다.

### 몬스터

| 게임 상태/사건 | 표시 |
|---|---|
| Approach/Swarm + 이동 | slime/mushroom/rock move, boar/puffer walk (종별 데이터 키) |
| 정지, 공격 없음 | 해당 방향 정지 그림 |
| Windup | boar windup, 기존 준비 시계에 맞춤 |
| Dash | boar dash 반복 |
| Recover | boar recover, 상태 진입 시계부터 |
| Fuse | puffer inflate, 기존 fuse 끝까지 진행·마지막 장 유지 |
| mushroom 실제 사격 | shoot, 기존 발사 사건/상태 변화 관측으로 시작, 발사 시점은 불변 |
| slime/rock 접촉 공격 | contact, 기존 공격 시계의 발동을 관측해 표시, 재피해 없음 |
| 사망 | 종별 fall, 완료 후 pool 반환 |

- 새 MonsterPhase를 추가하지 않는다. 시각 상태·재생 시계는 표시 쪽에만 있다. 공격 신호는 기존 서비스의 실제 발동 자리에서 표시 통지하거나 기존 관측 가능한 시계 변화로 받으며 게임 분기·피해 순서는 그대로 둔다. 동시 여러 공격은 마지막 표시만 채택해도 피해 횟수는 변하지 않아야 한다.
- `WorldPresenter`가 활성 id의 마지막 Monster 참조를 보관해, 목록에서 사라진 원인이 실제 `IsAlive=false`인지 구분한다. 사망이면 마지막 위치·방향/종을 동결한다. 웨이브 clear/reset/씬 해제는 사망으로 만들지 않는다.
- 사망 표시를 시작할 때 활성 MonsterViewRegistry에서는 즉시 제외하되 **같은 view의 풀 반환만 지연**한다. 별도 표시 목록은 WorldPresenter 소유, 공격·스폰·처치·가시 개체 규칙에서 제외된다. 기록은 view/클립/좌표/방향/시각 시간뿐이다. 모델 제거·처치 수를 늦추지 않는다.
- 표시 목록은 running일 때만 진행, pause에서는 정지. fall 마지막 장을 적어도 한 렌더 프레임 보여 준 뒤 풀 반환한다. 종료/재시작/Dispose 때 남은 표시를 즉시 반환한다. 풀 재사용 때 flash/phase/clock/flip/appearance를 초기화한다. 같은 view가 활성·사망 목록 양쪽에 있거나 두 번 반환되지 않게 소유권 이동을 단일 메서드로 묶는다.

## 5. 이행·변경 범위와 순서

1. 채택 표/원본 JSON 전수 조사 → 실사용 manifest와 시트·JSON 작성. clip metadata 대조 불일치를 출력.
2. 순수 프레임 선택/시간 매핑, 데이터 유효성 검사와 EditMode 시험.
3. 대원 표시·초상 연결, 짧은 Play. 먼저 대원 걷기·대기·공격·쓰러짐 완료 상태로 단위 커밋.
4. 몬스터 표시·수명 연결과 관련 시험, 짧은 Play → 단위 커밋.
5. 마감 관련 EditMode 묶음·기본 playable 1회·연속 캡처·마감 리뷰 요청.

이행 코드 예정: `Assets/Game/02.Scripts/Editor/MainSceneDQCharactersMigration.cs`, 메뉴 **`DeadSurvivor/DQ/Apply characters`**. 시트 importer/atlas/클립 데이터·필요한 Main/prefab 직렬화 참조는 이 메뉴로만 바꾼다. 이름·GUID를 유지하며 같은 입력으로 두 번 실행해 대상 해시 동일을 확인한다. 비대상 dirty 장면은 저장하지 않는다. 대상 파일 사전 백업과 실패 복구를 기존 DQ screen 이행 패턴으로 구현한다.

표시 소비처: WorldPresenter/MemberView, MonsterViewFactory/MonsterView, SpriteAtlasService의 DQ 전용 로드, LobbyPresenter/FormationPresenter/BattleHudPresenter의 초상 키. UI 틀·위치·크기는 9단계다. 기존 sprite 데이터 키는 유지하고 새 그림으로 해소해 세 소비처에 예전 초상이 남지 않게 한다.

이번에는 공유 시각 목록·기준서·5단계 코드 계약을 바꾸지 않고 이 브리핑의 기준과 추후 구현/검증 기록으로 8단계 행을 추적한다. 6·7단계 소유 파일과 `night-apply` 에디터는 접근하지 않는다.

## 6. 수용 기준과 최소 확인

| ID | 수용 기준 | 판정 |
|---|---|---|
| C8-01 | confirm 최종 표와 모든 실사용 클립/대체·약함·해시 대응, 안 쓰는 그림 반입0 | 데이터 전수 대조, 메뉴 두 번 해시 동일 |
| C8-02 | PPU48·Point·무압축·발 피벗, 종당 단일 texture·sharedMaterial 유지 | 실제 임포트/pack/renderer 확인, 성능 계측 없음 |
| C8-03 | 4명 walk24/idle12/attack7/fall10+lying·8논리방향, 예상 밖 fallback0 | 관련 EditMode + 실제 probe |
| C8-04 | 기존 공격 주기/HitSeconds/피해·발사 횟수 불변, 시각 타격 장 같은 frame | 타격 경계/짧은 주기/중단/동시 무기/큰 delta 시험과 실제 발사 로그 |
| C8-05 | 5종 방향/이동/고유행동/쓰러짐, 모델 제거 불변·pool 중복 반환0 | phase mapping·사망/clear/재시작/풀 재사용 시험 |
| C8-06 | 연속 캡처에서 프레임 진행·발/크기/그림자 정렬, 예전 몸/초상0 | 4명×walk/idle/attack 각각2방향 이상+fall, 5종 이동/행동; 본 장 sha256·관찰 |
| C8-07 | 컴파일0·관련 EditMode 양수·기본 판정 경로1회, 3화면비 | 실행 후 시각·소스 지문과 결과 기록 |
| C8-08 | 멱등 메뉴·실패 복구·범위 준수·단위 커밋 | 대상 바이트/GUID와 비대상 diff 대조 |

프레임 계산 시험: 누적 경계 직전/정확히 경계/직후, loop 끝/여러 loop, nonloop 끝, 음수/0 delta, 길이 불일치·空 clip·시간0 거부. 방향8개·경계·명시 별칭·실제 누락 검출을 포함한다. 기존 게임 규칙 시험 기대값은 수정하지 않는다.

실제 캡처는 `output/hd2d-dq/apply/8/`. 본 화면의 정확한 4배 최근접 crop과 연속 장을 직접 열고 비교한다. 단순 이웃 장 동일 여부만으로 실패시키지 않는다(같은 장을 유지하는 정상 구간 존재). 선언한 재생 경계를 넘었을 때 sprite/frame과 실제 그림이 달라지는지를 본다. 게임 이동 거리/속도와 clip 주기·발 착지 위치를 함께 기록해 미끄러짐을 판독한다. 16:9/19.5:9/4:3에서 크기/발/그림자만 이번 범위로 확인한다.

마감 리뷰: `spec-reviewer`, `validator-client`, `algorithm-tester` 한 번, FAIL 묶음 수정 후 해당 리뷰만 수정 확인 한 번. **동작 그림 자체 품질은 그림 단계 몫, 이 단계 판정 밖. 배경6단계·빛7단계·효과/투사체/예고 라인 화면/UI맞춤9단계 몫, 이 단계 판정 밖. 성능 K47은 별도 단계이며 이번 판정 밖.**

5단계에서 넘긴 `hd2d_main_gate.py` 원근 부적합 BLOCKED와 terrain route N9 FAIL은 그대로 보존한다. 이를 새 캐릭터 성공으로 승격하지 않으며 9단계 원근 관문 연결을 이번에 고치지 않는다.

## 7. 기준과 다른 점·이미 결정된 것

- 사용자 지정 walk24/idle12/attack7/fall10+lying가 기준서 3.10의 6~8프레임보다 우선한다. 새 그림을 생성하지 않는다.
- 카메라18°/FOV20, 5단계 좌표 변환과 주역56칸 기준 크기를 유지한다. 미승인 `아트-기준서-r3-개정안.md`는 채택 근거가 아니다.
- 피격 흰 번쩍임+1px 밀림 유지. 대원 장면빛 K54·재질은7단계. UI유지/자리맞춤9단계. 소품무료 새질감·나무3/덤불1은6단계.
- judge-night 표의 대체는 허용된 결정이다. fallback0을 만들기 위해 금지 클립을 넣지 않는다.
- 질문하지 않음, 유료생성/설치/push/PR/merge/외부 전송 없음. 150분 상한, 대원부터 완료. 150분 시점에 몬스터가 남으면 정확히 못 한 목록을 넘긴다.

## 8. 이번 설계 조사 결과·미실행과 다음 진입점

- project-doctor `--root . --host codex --require design.write`: exit0, status configured, findings0. 설정 확인이며 Unity 검증 아님.
- 요청한 `.ai/rules/25d-screen-structure.md`, `25d-render-pitfalls.md`, `25d-ui.md`, `25d-effects.md`는 현재 트리와 studio-kit 검색에서 발견되지 않았다. 설치 파일만 보고 실행했다고 주장하지 않는다. 이번 브리핑은 사용자 지시·기준서·실제 코드로 경계를 명시했고 구현 전 필요한 구조 규칙 복구/대체 근거는 리뷰에서 확인한다. 전역/원본 프로젝트 파일을 가져오지 않았다.
- slime/mushroom/rock의 r2와 원본 durations를 실제 대조: slime contact4방향 `6×70`→`80,80,60,70,90,110`, fall `5×100`→`100,110,130,300`; mushroom shoot_right/upright `6×90`→`90,80,60,80,100,130`, fall `5×100`→`80,70,80,70,80,70,240`; rock contact4방향은 slime과 같고 fall `5×100`→`80,70,70,60,60,60,70,240`. 원본 총시간을 새 그림에 강제로 적용하지 않는다. 채택 PNG 수와 r2 JSON 두 형식(`durations_ms`/`ms`, `event_frame_zero_based`/`event_frame_0based`)을 정규화·대조해 저장한다. 조건부 원본 rock/contact_right는 원본 시간 사용.
- 컴파일/EditMode/Play/캡처/독립 리뷰: **NOT_RUN — 구현 전 설계 관문**. 이전5단계 PASS를 이번 성공으로 쓰지 않음.
- `Tools/Harness/session_time.py --markdown`: FAIL, 기본 jsonl 없음(`max() iterable argument is empty`). 토큰 수는 제공되지 않아 미집계, 추정하지 않음. 이번 조사 시작 후 약 10분 이내(정확한 시작 시각 미수집), 마지막 확인 UTC `2026-11-22T18:39:28Z`; 활동은 지침/코드/판독 조사와 브리핑 작성. 다음 구현부터 시작/종료 시각을 직접 기록한다.
- 놓친 것/워크플로우 갱신 후보: project-doctor configured만으로 작업별 규칙 파일 존재를 보증할 수 없다. 이번 브리핑에 누락 파일을 명시했다. 공용 워크플로우 변경은 이번 범위 밖이므로 미반영. 추가 검사는 장별 시간·풀 수명 경계에 한정하고 기존 고정fps 전용 시각 시험의 중복 실행은 생략 후보(게임 규칙 시험은 유지)다.
- 다음 실행은 design-reviewer 판정 수신 → 필수 설계 수정 → C8-01 실사용 manifest/시트부터. **이 파일은 설계 리뷰 입력이며 완료 핸드오프가 아니다.** 최종 `.director/outbox/20261123-night-1to6-8.md`는 구현 후 마감 리뷰 판정을 받은 뒤 작성한다(이번 사용자 실행 규칙3).
