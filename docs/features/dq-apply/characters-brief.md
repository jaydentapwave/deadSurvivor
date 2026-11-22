# DQ 적용 8단계 — 캐릭터·몬스터 구현 전 설계 브리핑

- 상태: **L2 / 설계 수정 확인 PASS, 구현 초안 작성·Unity 검증 차단**.
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

- 표시 우선순위: 쓰러짐 → 미표시 실제 타격 기록 → 살아 있는 공격 → 이동 walk24 → idle12. 피격 번쩍임·1px 몸 밀림은 기존 경로로 합성하고 이동·피격 변환이 새 프레임에 이중 적용되지 않게 한다.
- 장별 시간의 누적합에서 `t < 누적 끝`인 프레임을 고른다. loop는 총 시간으로 modulo, nonloop는 마지막 장 유지. 음수 시간은 0, 큰 delta는 건너뛴 경계를 안전하게 처리한다.
- 이동 속도는 게임 값을 유지한다. 걷기 위상은 기존 이동 시계를 이용하되 방향 전환 시 같은 정규화 위상을 유지하고 정지하면 대기 시계를 시작한다. 기존 hop/대기 오프셋 중 새 그림에 이미 든 변환은 중복하지 않는다.
- 일반 공격 재생의 시각 시계는 **기존 MemberAttackMotion을 읽기만** 한다. 단, 실제 타격 장 보존은 아래 별도 기록 계약이 맡는다(현재 시계만 폴링하지 않음). 기존 `HitSeconds`에서 채택 clip `eventFrame`이 보이도록 타격 전 구간/뒤 구간을 각각 시간 매핑한다. 전 구간은 원본 duration 비율을 유지해 HitSeconds에 맞춘다. 후 구간은 원본 속도로 재생하되 기존 Duration에 도달하면 잘라 끝낸다. 다음 공격이 오면 일반 재생은 새 준비로 시작하며, HasHit 이후 회복을 취소하는 기존 규칙은 유지한다. 다만 미표시 실제 타격 기록을 새 준비가 덮지 못한다. HitSeconds=0, Duration=HitSeconds, 큰 delta 경계는 시험한다. 같은 frame에 현재 피해/발사와 시각 타격 장이 맞아야 한다.
- **DR-01 — 실제 타격의 표시 전용 기록**: 공격 시작마다 증가하는 `attackId`를 부여한다(게임 시계·PendingActionList와 독립인 표시 식별자). `CombatService.Step`의 기존 `AttackMotion.Advance(deltaSeconds)`가 true인 자리에서 해당 공격의 id·대원 id·방향(반전 포함)·시뮬레이션 걸음 번호를 지역 값으로 먼저 복사한다. 기존 ResolveAction 반복과 Clear의 순서/횟수는 유지하고, 실제 처리한 action이 있으면 그 묶음의 표시 통지를 발행한다. 같은 걸음의 StepAction이 새 공격을 시작하기 전에 기록이 확정되어야 한다. 타격 시점·피해량·발사 코드는 기록을 읽거나 기다리지 않는다. 공격 식별자·통지 타입은 표시 전용이며 기존 세이브/마스터데이터 계약에 추가하지 않는다.
- 전달은 기존 EventBus와 `Core/Event/GameEvent.cs`에 정의할 표시 통지를 사용한다. WorldPresenter가 초기화 때 구독해 대원별 미표시 목록을 보관하고 Dispose 때 해제한다. 데이터는 값 스냅샷이며 현재 AttackMotion 참조를 기록 대신 저장하지 않는다. 수신 중 MonoBehaviour나 sprite를 갱신하지 않는다. 리스너 오류가 ResolveAction/StepAction 흐름을 끊지 않도록 표시 핸들러 내부에서 예외를 기록·격리한다.
- **렌더까지 보존**: WorldPresenter.LateTick에서 미표시 타격 기록이 있으면 현재 `IsPlaying`/`Elapsed`나 새 공격 준비보다 먼저 기록의 방향과 채택 `eventFrame`을 MemberView에 적용한다. 몸·flash 모두 같은 sprite/flip으로 적용하며 그 프레임의 이후 일반 재생이 덮지 못한다. 게임 카메라가 그 적용 장을 렌더한 뒤에만 해당 batch를 소비 확인한다(URP endCameraRendering에서 대상 게임 카메라·프레임 번호·적용 batch id를 확인). LateTick 호출만으로 소비하지 않고, SceneView/다른 카메라/중복 capture가 소비하지 않는다. 렌더되지 않은 동안에는 보존한다. 콜백은 구독 수명과 함께 해제한다.
- 한 렌더 사이 여러 시뮬레이션 걸음에서 타격한 경우 기록은 모두 보존하고 실제 공격 id·방향·걸음 번호를 진단에 남긴다. 한 몸은 한 렌더에 한 장만 낼 수 있으므로 **같은 렌더 batch의 가장 최근 실제 타격** 방향/장으로 합성하고 batch의 다른 id도 합성됨으로 명시해 소비 확인한다. 다음 렌더에 과거 타격을 뒤늦게 하나씩 재생하지 않는다. 서로 다른 방향 타격을 모두 같은 한 장으로 보여 줬다고 보고하지 않는다. 같은 공격에 합류한 여러 무기는 같은 공격 id의 한 타격이며 피해·발사 건수는 각각 기존 경로로 처리된다. 새 준비만 있고 새 실제 타격이 없으면 이전 타격 기록이 그 첫 렌더에서 보이며, 다음 렌더부터 현재 준비/회복을 따라간다.
- 대원이 같은 걸음에 쓰러지면 쓰러짐 우선은 유지하고 미표시 타격을 `suppressed-by-death`로 기록해 제거한다. 사망 몸에 산 공격을 덮지 않는다. 부활/Run 재시작/웨이브 정리/Dispose는 표시 기록을 이전 생명/판으로 넘기지 않고 정리 사유를 남긴다. 공격 자체가 유효하게 처리됐다는 게임 결과를 되돌리지 않는다.
- **DR-03 — eventFrame 경계**: 공격 clip은 `0 <= eventFrame < frameKeys.Length`를 검증하고 범위 밖이면 반입 실패다. eventFrame=0이면 타격 전 구간 길이가0이므로 준비 중 첫 장을 유지하고, 실제 타격 기록이 같은 장을 렌더까지 고정한다. 마지막 장이면 타격 뒤 구간 길이가0이며 마지막 장을 고정한 뒤 현재 공격 상태로 돌아간다. HitSeconds=0 또는 Duration=HitSeconds인 경우 0으로 나누지 않고 해당 빈 구간을 건너뛰며 기록이 타격 장을 보증한다.
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

- **DR-02 — 몬스터 갱신 단일 경로**: 기존 `MonsterStatePresenter.Tick`은 계속 `MonsterView.UpdateState(phase, dashDirection)`를 호출해 예고 표시와 최신 phase/dash snapshot만 갱신한다. 현재 UpdateState 안의 몸 ApplySprite 호출은 제거하고 최종 몸 sprite를 쓰지 않게 한다. WorldPresenter.LateTick이 새 view 생성 → 최신 모델 phase를 같은 UpdateState로 동기화(처음 생성돼 Tick에 없던 view 포함) → 위치·방향/공격 통지 전달 → 표시 시계와 최종 clip/frame 선택 → 몸/flash 적용 → 그림자·projection 순서로 한 번 마감한다. 동일 phase의 UpdateState 중복 호출은 상태 진입/clock reset을 만들지 않는다. 이 순서는 기존 Tick/LateTick 구분을 이용하며 DI 실행 순서를 새로 바꾸지 않는다.
- phase 진입 감지와 phase 재생 시계는 MonsterView 표시 상태 한 곳에서 소유한다. UpdateState는 이전 snapshot과 비교해 실제 phase 변화 때만 진입을 표시하고, LateTick이 running delta로 시계를 한 번 진행한다. 기존 예고 line/ring 갱신과 준비 시작 시각은 이 동일 진입을 사용하며 pause에서는 진행하지 않는다. 실제 타격 통지는 phase clock을 재시작시키지 않고 시각 one-shot만 덮는다. 사망 표시는 활성 상태 경로와 분리하고 경고를 끈다. 9단계 예고 그림 교체를 여기서 하지 않는다.
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
| C8-03 | 4명 walk24/idle12/attack7/fall10+lying(Bruno는 디렉터 04:00 확정대로12)+8논리방향, 예상 밖 fallback0 | 관련 EditMode + 실제 probe |
| C8-04 | 기존 공격 주기/HitSeconds/피해·발사 횟수 불변, 시각 타격 장 같은 frame | 타격 경계/종료 동시 통과/같은 걸음 재시작/여러 걸음-한 렌더/소비 확인/동시 무기 시험과 실제 발사 로그 |
| C8-05 | 5종 방향/이동/고유행동/쓰러짐, 모델 제거 불변·pool 중복 반환0 | phase mapping·사망/clear/재시작/풀 재사용 시험 |
| C8-06 | 연속 캡처에서 프레임 진행·발/크기/그림자 정렬, 예전 몸/초상0 | 4명×walk/idle/attack 각각2방향 이상+fall, 5종 이동/행동; 본 장 sha256·관찰 |
| C8-07 | 컴파일0·관련 EditMode 양수·기본 판정 경로1회, 3화면비 | 실행 후 시각·소스 지문과 결과 기록 |
| C8-08 | 멱등 메뉴·실패 복구·범위 준수·단위 커밋 | 대상 바이트/GUID와 비대상 diff 대조 |

프레임 계산 시험: eventFrame -1/N 거부, 0/N-1 타격 장, HitSeconds=0 및 Duration=HitSeconds의 빈 구간, 누적 경계 직전/정확히 경계/직후, loop 끝/여러 loop, nonloop 끝, 음수/0 delta, 길이 불일치·空 clip·시간0 거부. 방향8개·경계·명시 별칭·실제 누락 검출을 포함한다. 기존 게임 규칙 시험 기대값은 수정하지 않는다.

DR-01 재현 시험은 별도로 고정한다: (1) 한 delta가 HitSeconds·Duration을 모두 넘어 IsPlaying=false여도 다음 게임 카메라 렌더에 기록된 타격 장 표시, (2) 같은 Step에서 타격 처리 후 새 Start가 방향을 바꿔도 이전 타격 id/방향이 첫 렌더에 표시, (3) 렌더 사이 여러 Step의 서로 다른 공격은 기록 누락0·최신 실제 타격 장·batch 합성 내역 확인, (4) 같은 attackId의 여러 무기는 피해/발사 건수 불변·표시1회, (5) SceneView/같은 frame 추가 카메라/게임 카메라 미렌더에서는 조기 소비0, 게임 카메라 렌더 후 batch 소비1회, (6) 사망·부활·재시작·Dispose 이후 기록/콜백 잔재0. 순수 시험으로 기록 및 선택 계산을, 실제 연속 캡처와 attackId/frame 로그로 렌더 보존을 확인한다. DR-02는 동일 phase 2회 전달 시 clock reset0·한 LateTick당 진행1회·예고와 몸이 같은 phase인지를 시험한다.

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

### 설계 수정 확인 1회 — DR-01~03

- 수신한 design-reviewer 판정 원문: `VERDICT: FAIL` (job `20261123-034204-4e566c`, read-only 67초). 이 판정은 아직 해소 확인 전이며 PASS로 기록하지 않는다.
- DR-01 필수 지적 수용: 타격 직후 시계 종료/재시작으로 이벤트가 사라지는 경우를 최초 설계가 놓쳤다. 원인은 현재 AttackMotion 시계만 보면 충분하다고 가정한 것. 값 기록→LateTick 적용→게임 카메라 렌더 확인의 보존 계약과 경계 시험을 추가했다.
- DR-02·DR-03 후속 제안도 같은 설계 수정에서 반영: MonsterStatePresenter와 WorldPresenter/View 역할·순서, eventFrame 범위와 첫/마지막 장의 빈 시간 구간을 명시했다.
- 이미 모인 열린 지적(수정/재리뷰 이유로 삼지 않음): design-reviewer — 범위 밖: 기존 원근 관문 BLOCKED와 terrain N9 FAIL 보존. 증거 없음: 이전 리뷰는 파일 수정/Unity 컴파일/EditMode/Play/캡처 없이 설계·코드 정적 대조만 수행.
- 이번 수정도 문서만 변경. 컴파일/EditMode/Play/캡처는 NOT_RUN(구현 전 관문), 문서 일관성과 diff 공백 검사만 수행한다. `.ai/rules/fix-and-recheck.md`도 현재 작업 트리에 없으므로 이번 사용자 재시도 지시와 AGENTS의 반복 상한을 직접 따른다.
- 워크플로우 갱신 후보: 공격 그림 동기화 설계에는 현재 시계 외에 실제 사건의 값 스냅샷과 렌더 소비 시점을 적는다. 이 작업의 브리핑/검증 계획에 반영했고 공용 규칙 변경은 범위 밖으로 미반영. 추가 검사는 위6개 경계에 한정하며 기존 고정fps 시각 시험의 중복 실행은 생략 후보 유지.
- 수정 활동: 관련 코드 정적 대조·브리핑 편집·문서 검사. 확인 UTC `2026-11-22T18:43:55Z`부터 수행, 종료 시각은 커밋 확인 명령 기록. 토큰 집계 도구의 jsonl 부재는 그대로이며 실제 토큰 미제공.

### 설계 판정 수신과 구현 상태

- design-reviewer 최신 evidence: `VERDICT: PASS` (수정 확인1회, job `20261123-034539-34753d`). DR-01·02·03 해소, 필수 수정 없음. 최초 FAIL 기록은 이력으로 보존한다.
- 후속 제안 DR-02-T 수용: 다음 EditMode/Play 확인 목록에 Tick 이후 처음 생성된 view의 첫 렌더, pause 동안 phase 시계 정지를 포함한다. 아직 실행하지 않았다.
- 9종 실사용 시트·데이터, 메뉴, 표시 연결, 타격 기록·사망 풀 분리, EditMode 시험 초안을 작성했다. 상태/한계/다음 명령은 `characters-verification.md`가 원본이다. 이 문서의 계획을 완료 주장으로 읽지 않는다.
- Unity Editor Software Terms 동의 UI와 명령 대기로 실행 관문이 막혔다. 동의하지 않았고 패키지/전역 설정도 바꾸지 않았다. 사용자 부재에서 법적 동의를 대신할 수 없어 partial 인계한다. 구현 후 마감 리뷰 3종은 아직 요청하지 않았다.


## 9. 디렉터 피드백1 반영과 마감 리뷰 범위 (r2)

- 04:00 피드백: 차단 창은 night-chars의 Main 저장 확인이었다. 약관 창은 프로젝트 없는6000.6.7f1이었다. 앞 검증 문서의 약관 차단 추론은 철회한다. 실제6000.4.3f1 대상 `editor_ready.py`로 재개했다. 약관 수락0.
- 메뉴가 unrelated dirty scene을 거부하고 자신이 다루는 Main의 `SaveOpenScenes()`를 완료한다. Play/시험 전 dirty=false를 읽었다.
- 첫 임포트에서 NPOT 기본 축소 크기로 slice Y를 계산한 결함 수정: importer의 원본 크기 사용. 최종 반복/예외 복구는 `r2/migration-final-check.json`.
- Bruno fall12는 확정된 차이다. C8-03의 원래10장 계약을 임의 완화한 것이 아니며 최종 채택표를 그대로 적용했다.
- 추가 관찰용 probe는 실제 production view를 읽고 EndOfFrame에서 캡처한다. 게임 모델/피해/phase 쓰기 없음. 기존 Bot/RunSettings 입력으로 자연 승리·패배와 조용한 발 동작을 분리 관찰했다.
- 최종 구현·명령 근거는 `characters-verification.md`와 `output/hd2d-dq/apply/8/r2/`에 기록한다. 마감 검토는 spec-reviewer, validator-client, algorithm-tester. design-reviewer의 `VERDICT: PASS`는 설계 수정 확인에서 이어 받으며 구현 PASS로 쓰지 않는다.
- 배경·빛은6·7단계 몫, 이 단계 판정 밖. 효과·투사체·예고 라인 화면 배치·UI 맞춤·원근 관문 연결은9단계 몫, 이 단계 판정 밖. 성능과 동작 원본 자체 품질도 판정 밖.
