# F3 자동 전투 — 설계 브리핑

- 상태: 구현 기준
- 규모: L2 — 데이터 스키마 확장(아이템·캐릭터·전투 규칙 파일), 대원 상태 소유권 추가(`PartyMember` 보호막·유효 방어·최대 HP 갱신, 새 `CombatModel`), DI 등록 변경(`BasicWeaponService` 삭제, 전투 서비스·진입점 추가)
- 이번에 사용할 수 있게 될 산출물·완료 조건: 기획서 [plan.md](plan.md) 클라 변경 전체, AC1~AC20(정식 채택). 판정 경로 `route.json` 무변경, `--until G2` 통과, F1 흐름 통과, 새 흐름 `f3-combat-flows.json` 통과
- 요구와 제외 범위/후속 항목: 기획서 B~H절. 제외 — F2 이동·진형, F4 몬스터 종류, F5 가방·등급·인접 실제 계산(이 기능은 `NoBagPlacementInfo`만), F6 피격 연출·피해 숫자, 아이템 24종 데이터(콘텐츠 대기열)
- 관련 기획 및 사용자 확정 사항: plan.md(승인 2026-11-15), F1 plan.md E·G·H절, gdd.md S2·S7·C2·C4
- 기존 코드와 데이터 소유처(F1, 시작 커밋 886c1f9에서 읽음):
  - `RunService`(한 판 규칙, `RunModel` 유일 작성자) → `Step` ④에서 `BasicWeaponService.Tick`, `BeginWave`에서 `ResetClocks`
  - `PartyModel`/`PartyMember`(일반 클래스, `Hp`·`Position`·`WeaponClock` 공개 set, `MaxHp` 읽기 전용) — 쓰는 쪽 `PartyService`(만들기·피해)·`BasicWeaponService`(쿨)
  - `MonsterModel`/`Monster`(`Hp` 공개 set, 목록 순서 = 생긴 순서, `RemoveDead`가 순서 유지) — 적 피해는 F1 기본 무기가 `monster.Hp -=`로 직접 깎고 처치 수는 `RunService`가 `RemoveDead()`로 센다. `MonsterService`에 피해 메서드는 없다
  - 데이터: `ResourceService.LoadJson`·`LoadAllJson`(JsonUtility), `ItemRepository.Get(id)` 이미 있음, `CharacterRepository`
  - 화면: `BattleHudPresenter`(ILateTickable)가 `UI_MemberStatusView.UpdateUI(..., itemNameList)`로 이름 1칸, `PrepPresenter`가 정비 진입 때(`Phase == Prep` 구독) 대원 줄 그리기(비공개 `OnPrepEntered`), `WorldPresenter`(ILateTickable)가 적 그림 풀 동기화(`MonsterViewFactory` + `MonsterViewRegistry` + `PoolManager.Get`, 프리팹 `Resources/Prefab/MonsterView`)
  - 판정: `PlayableProbe`(에디터 전용 정적, 컨테이너에서 Resolve)

## 변경할 파일과 변경 전 → 후 동작

### 데이터
- `Item/<id>.json` 4개에 칸 추가, 새 4개(`axe`·`wand`·`censer`·`ironshield`) — 값은 plan C1 표
- `Character/<id>.json` 4개에 `sprite: ""`
- 새 `Combat/combat_rules.json` — plan 데이터 스키마 표의 14값
- `Run/placeholder_combat.json`에서 `weapon` 칸만 삭제, `PlaceholderCombatData`의 `weapon` 필드·`PlaceholderWeaponData` 클래스 삭제
- `ItemData`: 문자열 칸(`mode`·`baseEffect`·`bonusEffect`·`bonusTarget`)을 `ISerializationCallbackReceiver.OnAfterDeserialize`에서 enum으로 바꿔 둔다(모르는 값은 `GameLogger.LogError`, `check_data.py`가 먼저 막음). enum: `ItemMode`(8), `ItemEffectKind`(None + 14), `ItemBonusTarget`(Any·Attack·발동 방식 7 — `ItemMode.cs` 안에 함께 둔다. 기획서 파일 목록에 없는 형 하나, 아래 "재사용 후보" 참고). `ItemCell {x, y}` 배열
- `CharacterData`: `sprite` 칸

### 전투 (새 `Features/Combat/`)
- `CombatRulesData` + `CombatRulesRepository`(`ResourceService.LoadJson`으로 한 번 읽어 `Rules`로 줌)
- `IItemPlacementInfo { float GetGradeMultiplier(int memberIndex, int slotIndex); bool IsAdjacent(int memberIndex, int slotA, int slotB); }`, `NoBagPlacementInfo`(1, false)
- `CombatStats`(struct, 능력치 묶음: Attack·Defense(유효)·MaxHp·Heal·Haste·Range·Radius·Shield(상한)·StartShield·Pierce·ExtraShots·Chain)
- `EquipmentStatService`(순수): `CombatStats Calculate(int memberIndex, CharacterData character, IReadOnlyList<string> equippedIds, int actionSlot)` — `actionSlot = -1`이면 대원 전체 계산(B3). 추가 효과 적용 판정 `IsBonusApplied(memberIndex, equippedIds, sourceSlot, actionSlot)`: 효과가 비면 거짓, 조건 없음(대상 `Any`이고 인접 불필요)이면 참, 조건 있으면 `actionSlot ≥ 0`이고 source ≠ action이고 대상이 맞고(Attack = 회복 둘·지속을 뺀 방식) 인접 조건이면 `IsAdjacent`. 지속 효과용 `IsBonusActive(memberIndex, equippedIds, sourceSlot)` = 조건 없음이거나 그 대원의 발동 칸 하나라도 `IsBonusApplied`(예전 `BonusState == 활성`). 더하기·큰 값 하나·정수(배수 안 곱함) 분류는 plan B3
- `CombatModel`(일반 클래스 — 1/30초마다 바뀌는 값이라 R3로 감싸지 않음, F1 `PartyModel`과 같은 방식. 부활 수만 내부 `ReactiveProperty<int>`, 외부 `ReadOnlyReactiveProperty<int> ReviveCount`로 노출해 정비 화면이 다시 그린다. `CombatModel : IDisposable`이 그것을 Dispose):
  - `MemberCombatState`(대원 번호 순): 대원 전체 능력치, 주기 보호막 채움 값(B6 — 상한 `Shield`와 따로, 웨이브 시작에 `IsBonusActive`인 `shield` 추가 효과 값 × 배수 중 큰 값으로 계산해 둠. 예: 시작 보호막 30·주기 20이면 상한 30, 소진 8초 뒤 20), 장착 목록 사본(웨이브 시작 비교용), 회복·보호막·나눔 시계, 나눔 제공자(−1 없음)·주기, `BattleItemSlot` 목록(아이템 id·`ItemData`·남은 쿨·마지막 주기·발동 표시 남은 초·그 칸을 위한 능력치·파동 칸 여부)
  - `ProjectileState` 목록(번호, 위치, 방향, 속도, 피해, 사거리, 날아간 거리, 관통, 폭발 여부·반경·목표 위치, 맞힌 적 번호 `HashSet<int>`)
  - `BurstState` 목록(번호, 위치, 반경, 남은 초) — 범위 발동 고리 표시용, 걸음 시간으로 줄어든다
  - 아이템 id별 이번 판 발동 수 `Dictionary<string,int>`, 부활 수(R3), 마지막 부활 대원(−1)
- `CombatService`(유일한 `CombatModel` 작성자, `PartyMember.Shield`·`Defense`·`MaxHp`도 여기서 씀):
  - `ResetRun()`: 대원마다 상태 새로(칸 = 장착 목록, 쿨 0), 발동 수·부활 수 0, 마지막 부활 −1, 능력치 계산 → `MaxHp`·`Defense` 쓰고 `Hp = MaxHp`
  - `PrepareWave()`: 장착 목록이 사본과 다르면 그 대원 칸만 새로(쿨 0), 같으면 쿨 유지. 능력치 다시 계산 → `MaxHp`·`Defense`, `Hp = min(Hp, MaxHp)`. 살아 있으면 `Shield = StartShield`. 시계 0, 제공자 −1. 투사체·고리 비움
  - `Step(dt)`: ① 나눔 제공자 다시 고르기(예전 `RefreshProviders`, 바뀌면 나눔 시계 0) ② 대원 순서대로 산 대원만: 발동 표시 −dt → 지속 효과(B6: 자기 회복 while 시계 ≥ 주기, 나눔, 보호막 상한·8초 채움, 파동 칸) → 장착 순서대로 발동 칸(B5) ③ 투사체 이동·명중(관통·폭발) ④ 고리 남은 초 −dt
  - 적 피해는 F1처럼 `monster.Hp -= damage`(처치 수는 그대로 `RunService`의 `RemoveDead`). 명중 반지름은 `MonsterService.GetRadius(Monster)` 한 줄 추가(F1은 `enemy.radius`, F4가 유형별로 바꿈)
  - 같은 거리 적: 목록 순서(생긴 순서)로 돌며 `<` 비교 → 먼저 생긴 적. 회복 대상: 안정 정렬(HP 비율 오름차순, 같으면 편성 순서)
  - `ClearProjectiles()`: 투사체·고리 비움
  - `ReviveMember(i)`: `Hp = MaxHp`, `Shield = 0`, 시계 0, 그 대원 칸 쿨·마지막 주기·발동 표시 0, 부활 수 +1, 마지막 부활 대원 = i (조건 확인은 하지 않음 — `ReviveService`만 부른다)
- `ReviveService`: `CanRevive(i)` = `RunModel.Phase == Prep` && 대원 쓰러짐. `Revive(i)`: 안 되면 경고 로그 + false(상태 무변경). 되면 `CombatService.ReviveMember(i)` + true. 상태를 직접 쓰지 않는다

### 필드별 작성 권한(DR-01)
| 상태 | 쓰는 곳 |
|---|---|
| `PartyMember.Hp` | `PartyService.CreateParty`(시작값)·`ApplyDamage`(피해)·`Heal`(회복), `CombatService.ResetRun`(= 최대)·`PrepareWave`(최대로 자름)·`ReviveMember`(= 최대) |
| `PartyMember.Shield` | `PartyService.ApplyDamage`(흡수만큼 줄임), `CombatService`(`PrepareWave` 시작 보호막, `Step` 상한 자르기·주기 채움, `ReviveMember` 0) |
| `PartyMember.MaxHp`·`Defense` | `PartyService.CreateParty`(캐릭터 값), `CombatService.ResetRun`·`PrepareWave`(B3 값) |
| `PartyMember.Position`·장착 목록 | F1 그대로(`PartyService`) |
| `CombatModel` 전부 | `CombatService`만 |
| `Monster.Hp` | `CombatService`(피해), F1 생성 그대로 |

### 생성자 의존(DR-06, 순환 없음)
- `CombatRulesRepository(ResourceService)`, `NoBagPlacementInfo()`, `CombatModel()`
- `EquipmentStatService(ItemRepository, CombatRulesRepository, IItemPlacementInfo)`
- `CombatService(CombatModel, PartyModel, PartyService, MonsterModel, MonsterService, ItemRepository, CharacterRepository, EquipmentStatService, CombatRulesRepository)`
- `ReviveService(RunModel, PartyModel, CombatService)`
- `RunService(…, PartyService, CombatService, MonsterService)` — `CombatService`는 `RunService`·`RunModel`을 모른다
- `BattleItemListPresenter(RunModel, PartyModel, CombatModel, ItemRepository, ResourceService, UI_BattleHudView)`, `RevivePresenter(RunModel, PartyModel, CombatModel, ReviveService, UI_PrepView)`, `CombatWorldPresenter(CombatModel, RunRulesData, PlaceholderSpriteFactory, ProjectileViewFactory, ProjectileViewRegistry)`, `ProjectileViewFactory(PoolManager, ResourceService, ProjectileViewRegistry, Transform)`, `PrepPresenter`에 `CombatModel` 추가
- `PartyService`(G표 줄만): `ApplyDamage`가 `member.Defense` 사용 + 보호막 먼저 흡수(B4), `Heal(member, amount)`(산 대원만, 최대 HP까지), `CanChangeEquipment(i)`. `CreateParty`가 `Defense = def`, `MaxHp = hp`로 시작(전투 계산 전에도 F1 피해 시험이 같은 값)
- `PartyMember`: `WeaponClock` 삭제, `Shield`·`Defense` 추가, `MaxHp`를 set 가능으로
- `RunService`(G표 줄만): 생성자 `BasicWeaponService` → `CombatService`, `StartRun`에 `ResetRun()`, `BeginWave`의 `ResetClocks()` → `PrepareWave()`, ④ `Tick` → `Step`, `FinishWave`·`EndRun`·`ReturnToLobby`에 `ClearProjectiles()`
- `BasicWeaponService.cs`(+`.meta`) 삭제

### 화면
- `UI_BattleItemView`(칸): 바탕 Image, 이름 Text(아이콘 Image가 있으면 아이콘), 상태 Text, 쿨 막대 RectTransform(anchorMax). `UpdateUI(string label, Sprite icon, BattleItemState state, float remainingSeconds, float barRatio)`, `SetVisible(bool)`. 쓰러짐이면 어둡게·막대 숨김, 지속 효과면 막대 숨김, 준비/쿨 색 다름
- `UI_MemberStatusView`: `Text[] itemSlotTexts` → `UI_BattleItemView[] battleItemViews` + 읽기, `UpdateUI`에서 아이템 이름 인자 삭제 → `BattleHudPresenter`의 이름 목록 만들기·`ItemRepository` 의존 삭제(G표 밖이지만 이름 1칸 삭제에 따라오는 줄)
- `BattleItemListPresenter`(ILateTickable): 전투 화면일 때 대원마다 칸 수만큼 `UpdateUI`, 나머지 칸 숨김. 상태 판정 `GetItemState(member, slot)`(plan F 순서) 공개 — 판정 진입점도 이것을 읽는다. 아이콘은 `icon`이 비지 않으면 `ResourceService.Load<Sprite>(icon)`(처음 한 번 캐시)
- `UI_ReviveView`: "쓰러짐" 글 + 부활 버튼, `UpdateUI(bool isDown)`, `ReviveButton`. `UI_PrepView`에 `UI_ReviveView[] reviveViews` + 읽기
- `RevivePresenter`(IInitializable, IDisposable): 정비 진입과 부활 수 변경 때 `UpdateUI`, 버튼 → `OnReviveClicked(i)` → `ReviveService.Revive(i)`. `PrepPresenter`는 부활 수 구독 한 줄을 더해 대원 줄(HP)을 다시 그린다
- `CombatWorldPresenter`(ILateTickable): 투사체·고리 번호별 그림 동기화(새 번호는 공장에서 꺼내고 사라진 번호는 풀로). 그림은 `PlaceholderSpriteFactory`의 원(투사체)·반투명 마름모(바닥 위 원의 아이소 모양 근사, 고리)를 처음 한 번 만들고 고리는 반경에 맞춰 크기 조절. 정렬 순서 50
- `ProjectileView`·`BurstView`(SpriteRenderer 하나씩, 프리팹 `Resources/Prefab/ProjectileView`·`BurstView`), `ProjectileViewFactory`(`PoolManager.Get`, 부모 = 장면 `CombatEffects`, `WithParameter`로 넘김), `ProjectileViewRegistry`(번호 → 그림, 투사체·고리 따로)
- `Main.unity`: 대원 하단 칸마다 `UI_BattleItemView` 16칸(F1 이름 칸을 바꿈), 정비 대원 자리마다 `UI_ReviveView`, `CombatEffects` 오브젝트. 에디터가 연결돼 있으므로 에디터 명령(스크립트 eval)으로 고치고 저장한다. 하단 칸이 상태 글·막대를 담도록 하단 판넬 높이를 조금 키운다(안전 영역 판넬 기준 그대로)
- `GameSceneLifetimeScope`: G표 등록 줄 + `[SerializeField] Transform combatEffectRoot` 한 칸

### 판정
- `PlayableProbe`: H절 읽기·조작 멤버(Model·Presenter·Service를 읽고 부르기만, HP·Phase·Outcome 대입 없음). 봇 무변경
- `Tools/playable/f3-combat-flows.json`: H절 표 그대로. `<부활 시드>`·`<부활 배율>`은 에디터 명령으로 서비스만 만들어 웨이브 1을 돌려 "1~3명 쓰러지고 나머지 끝까지" 값을 찾음
- `Tools/playable/f1-flows.json`: `LOSE` 배율 값 하나만 필요하면 바꿈. 시도 2 예외(디렉터 답 2026-11-16): `P1` 단계 `timeout` 45→120 — 에디터 백그라운드 속도 때문. 단계·확인·expect는 그대로. `LOSE` 배율은 바꾸지 않았다
- `Tools/check_data.py`: 아이템 새 칸·허용 값·shape(1~16칸, 0..3, 겹침 없음, 상하좌우 연결)·개수 4~40·방식 8가지 모두 있음, 캐릭터 `sprite`·시작 아이템 passive 아님, `combat_rules.json`, placeholder `weapon` 검사 삭제, `--self-test` 5종 추가

### 테스트
- 새: plan 클라 변경 "테스트" 8개 파일. 시험 아이템은 테스트 안에서 JSON 문자열을 `JsonUtility.FromJson<ItemData>`로 만든다(실제 읽기 경로 그대로). 배치 정보는 시험용 가짜 `IItemPlacementInfo`
- `TestRunContext`: `BasicWeaponService` → `CombatRulesRepository`·`CombatModel`·`EquipmentStatService`·`CombatService`·`ReviveService`. 시험 아이템을 받는 `ItemRepository`가 필요해 `ItemRepository`에 시험용 생성자(목록을 받는)를 하나 더할지 → 아래 미정 1
- 기존 시험 기대 변경은 plan "기대 변경 허용" 표 안에서만: `PlaceholderCombatTests`의 기본 무기 시험 2개(`DeadMemberDoesNotAttackAndIsNotTargeted`, `AliveMemberAttacksNearestMonsterInRange`) → 같은 문장을 아이템 발동으로 확인하도록 `ItemActivationTests`로 옮김, `SameSeedSameResultAcrossFrameSplits`는 그대로 둠(이제 아이템 전투로 돈다), `DataLoadTests.RunRulesAndPlaceholderCombatLoad`의 `Weapon.Range` 줄 삭제

### 시도 2 보완(설계 확인 DR2-01): 아이템 칸의 두 시계
- `BattleItemSlot.RemainingCooldown` = 발동 쿨(발동 아이템, B5), `PulseCooldown` = 파동 쿨(추가 효과 pulse, B6). 둘은 따로 돈다. 쓰는 곳은 `CombatService`만이다
- 하단 칸 표시(`DisplayCooldown`)는 발동 아이템이면 발동 쿨, 지속 아이템이면 파동 쿨이다. 마지막 주기·"발동!" 표시는 지속 아이템의 파동일 때만 파동이 쓴다
- 경계: 새 판·장착 목록이 바뀐 대원·부활은 두 시계를 모두 0으로 한다(`ResetCooldown` 또는 칸 새로 만들기). 같은 장착으로 웨이브를 넘으면 둘 다 이어 간다
- 투사체는 한 걸음 이동을 남은 사거리로 자른다(사거리 밖 선분을 지나지 않음). 폭발 도달 판정은 예전 판 순서(이동한 뒤 거리 < 이번 걸음 이동 + 5, 디렉터 답 5)

## 재사용 후보와 신설 이유
- 적 찾기·피해: F1 `BasicWeaponService.FindNearestAliveInRange`를 `CombatService`로 옮김(파일 삭제)
- 그림 풀: `MonsterViewFactory`/`Registry` 패턴 그대로 따름(투사체·고리는 다른 프리팹·수명이라 같은 공장에 넣지 않음)
- `ItemBonusTarget` enum: `bonusTarget`이 "", "attack", 발동 방식 이름 중 하나라 `ItemMode`만으로 표현할 수 없다. 새 파일 대신 `ItemMode.cs`에 함께 둔다
- 고리 수명 0.25초·점 고리 반경 25(단일 회복·연쇄 명중)는 표시 전용 상수로 `CombatService`에 둔다(규칙 데이터 표에 없는 값, 예전 `Burst` 값)

## 실패·취소·생사·전환 경계
- 쓰러진 대원: 칸 걸음 없음(쿨 정지), 발동 표시도 줄지 않음, 회복·나눔 대상/제공자 제외. 날아가는 투사체는 계속
- 부활: 정비 화면이 아니거나 산 대원이면 경고 + false, 상태 무변경
- 웨이브 끝·판 끝·로비: 투사체·고리 비움. 웨이브 시작 투사체 비움. 판 시작은 `ResetRun`이 모두 새로
- 한 걸음 안에서 같은 적을 여러 칸이 때려 HP < 0이 되면 이후 칸은 `IsAlive`로 건너뜀(폭발·파동도 산 적만)
- `RunService.AdvanceFrame`의 고정 간격은 그대로 — 전투는 `Step(simStep)` 안에서만 돈다(B8)

## 데이터 호환성과 복구 방법
- 저장 없음. 데이터 파일은 git으로 되돌린다. JsonUtility는 없는 칸을 기본값으로 읽으므로 F2·F4·F5가 같은 파일에 칸을 더해도 깨지지 않는다

## 최소 필수 검증
- 컴파일 0 에러(`editor_ready.py` + Editor.log)
- EditMode: AC7~AC15 시험 + 기존 시험(기대 변경 표 밖 기대는 무변경). AC15 시험은 시작 장비 외에 연쇄·보호막(조건 없는 `shield`·`startShield` 시험 아이템)·회복·폭발·관통 투사체를 실제로 일으키고 처치·HP·보호막·발동 수를 비교한다
- `check_data.py`, `--self-test`(AC2·AC3)
- `playable.py --check`(두 흐름 파일), `--until G2`(AC5), `--route f1-flows.json`(AC6), `--route f3-combat-flows.json`(AC16~AC18)
- 캡처 직접 열기(AC19·AC20) + Device Simulator 노치 가로 1장
- 리뷰: spec-reviewer → validator-client → algorithm-tester

## 초기 실제 연결
- Play → 출발 → 전투에서 장검·단궁·화염 지팡이가 발동(발동 수 증가), 하단 칸 "준비/n.n초/발동!" → 웨이브 1 끝 정비 → 쓰러진 대원 부활 → 웨이브 2

## 검사 확대/도구 신설 조건
- 새 도구 없음. 흐름 경로 하나와 데이터 검사 칸만 기획서대로 더한다

## 알려진 환경 차단과 재개 조건
- 같은 컴퓨터에 hamster-mart 에디터가 떠 있다 → 모든 Unity CLI 명령에 `--project-path` 지정

## 미정 사항 및 진행에 미치는 영향
1. (결정) 시험용 아이템 주입: `ItemRepository`에 `ItemRepository(IEnumerable<ItemData>)` 생성자 하나, VContainer가 `ResourceService` 생성자를 고르도록 `[Inject]` 표시
2. (결정) `BattleItemListPresenter`·`CombatWorldPresenter`는 기획서가 `ITickable`이라고 적었지만, 전투 걸음이 `BattlePresenter`(ITickable)에서 돌고 F1 화면 갱신(`BattleHudPresenter`·`WorldPresenter`)이 `ILateTickable`이라 같은 프레임 순서를 맞추려고 `ILateTickable`로 한다

## 설계 검토 결과와 해소한 지적
- 1회차 FAIL(Codex): DR-01 작성자 충돌 → `ReviveService`는 조건만, 상태는 `CombatService.ReviveMember` + 필드별 작성 권한 표. DR-02 → `ReadOnlyReactiveProperty` 노출·Model Dispose. 후속 DR-03(목록 생성자 채택), DR-04(ILateTickable 동의), DR-05(채움 값 따로 둠), DR-06(생성자 의존·AC15 구성 명시) 반영
- 시도 2 설계 확인 PASS(Codex, a8ed065..3912865): 작성 권한·스키마·DI 위반 없음. 후속 DR2-01(두 시계 명시)·DR2-02(AC6 timeout 예외 기록)를 이 문서에 반영
- 2회차 수정 확인 PASS(Codex): DR-01·DR-02 해소, 새 순환·충돌 없음. 후속 DR-03 문서 정합성 → 미정 1을 결정으로 통일
