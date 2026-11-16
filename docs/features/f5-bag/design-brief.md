# F5 가방과 정비, 전리품 — 설계 브리핑

- 상태: 구현 기준
- 규모: L2 — 새 상태 소유권(`BagModel`: 가방·보관함), F3 배치 정보 자리 교체(`IItemPlacementInfo` 등록), 데이터 스키마 추가(`Grade/`·`Bag/`·`Loot/`, 아이템 `lootWeight`), DI 등록 추가
- 이번에 사용할 수 있게 될 산출물·완료 조건: 기획서 [plan.md](plan.md) "클라 변경"의 코드·데이터·테스트·흐름 경로·데이터 검사(정식 채택). 장면 `Main.unity`의 오브젝트·SerializeField 연결은 이 실행에서 넣지 않고(에디터 없음) 합친 뒤 루트 실행이 넣는다. 이 실행의 완료 = 코드·테스트 작성 + 파이썬 검사(AC2·AC4 일부) + algorithm-tester 실행 검증. 컴파일·EditMode·플레이(AC1·AC3·AC5~AC21의 실행)는 루트 실행
- 요구와 제외 범위/후속 항목: plan A~E절, H절. 제외 — 장면·프리팹 편집, 아이템 아이콘 그림, F2 이동·진형 자리, F3 발동 규칙, F4 몬스터, F6 피드백, 나머지 아이템 24종 데이터
- 관련 기획 및 사용자 확정 사항: plan.md(승인 2026-11-15), F1 plan E·G·H절, F3 plan B2·B3·C·E절, gdd.md S5·S6·8절

## 기존 코드와 데이터 소유처 (시작 커밋 e175cee에서 읽음)

- `PartyModel`/`PartyMember`: 장착 목록 `_equippedItemIdList`(읽기 `EquippedItemIdList`, 쓰기 `Equip(id)` 더하기만). 쓰는 쪽 `PartyService.CreateParty`(시작 아이템 1개). `IsAlive = Hp > 0`
- `PartyService.CanChangeEquipment(memberIndex)` = 살아 있음(F3 E절)
- `RunService.StartRun(seed, scale)`: `runSeed`는 지역 변수(시드 0이면 `Environment.TickCount`), `RunModel`에 시드 읽기 없음. 순서 `CreateParty → CombatService.ResetRun → MonsterService.BeginRun(runSeed)`. `FinishWave` 정비 전환은 `SetCompletedWave(wave); SetPhase(Prep)`. `ReturnToLobby`는 `ClearParty` 등
- `CombatService.ResetRun/PrepareWave`: 장착 목록으로 칸(`BattleItemSlot`) 만들고 `EquipmentStatService`로 능력치. `PrepareWave`는 장착 목록이 사본과 다른 대원만 칸 새로(쿨 0)
- `EquipmentStatService`(일반 클래스, 생성자 `ItemRepository, CombatRulesRepository, IItemPlacementInfo`): `Calculate(member, character, equippedIds, actionSlot)`, `IsBonusApplied`, `IsBonusActive`, `GetPower/GetPeriod/GetRange`. 배치 정보를 생성자로 받는다 → **미리보기는 오버로드 없이 복사본 배치 정보로 새 인스턴스를 만들어 같은 계산을 부른다**(D절 "필요할 때만" 줄은 쓰지 않음)
- `IItemPlacementInfo`(F3) + `NoBagPlacementInfo`, 등록 `builder.Register<IItemPlacementInfo, NoBagPlacementInfo>(Lifetime.Scoped)`
- `ItemData`(JsonUtility + `OnAfterDeserialize` enum 변환), `ItemCell {x, y}`, `ItemRepository`(폴더 전체, 시험용 목록 생성자 있음), `CombatRulesRepository`(`LoadJson` 한 번) — 새 저장소 3개가 같은 방식
- 정비 화면: `PrepPresenter`(Phase==Prep 구독, 대원 줄), `RevivePresenter`(F3, `UI_PrepView.ReviveViews`), `UI_PrepView`(SerializeField + `UpdateUI`). 진입점: `PlayableProbe`(에디터 전용 정적, `Resolve<T>`), `PlayableBot`(AutoNext만, Move·Equip 경고 후 무시)
- 시험: `TestRunContext`(실제 데이터 + 선택적 시험 아이템 저장소·배치 정보로 서비스 묶음, `RunService` 생성), `CombatTestItems`(1칸 시험 아이템 JSON 생성)

## 변경할 파일과 변경 전 → 후 동작

### 데이터 (새 파일 3, `03.Resources/Resources/Data/`)
- `Grade/grades.json` `{grades:[{id,name,color,mult}×5]}`, `Bag/bag_rules.json` `{bagWidth,bagHeight,startCell{x,y},autoEquip{hpWeight,shieldWeight}}`, `Loot/loot_rules.json` 7값 — 값은 plan 데이터 스키마 표
- `ItemData`에 `[SerializeField] private float lootWeight = 1f;` + `LootWeight`(칸 없는 파일 = 1, 아이템 파일 무변경)

### 가방 `Features/Bag/`
- `Data/GradeData.cs`(`GradeData` + 파일 형 `GradeTableData{grades[]}`), `Data/BagRulesData.cs`(`BagRulesData` + `AutoEquipData`; `startCell`은 기존 `ItemCell` 재사용)
- `Registry/GradeRepository.cs`(`GradeDataList`, `LastGrade` = 수 − 1), `Registry/BagRulesRepository.cs`(`Rules`) — `CombatRulesRepository`와 같은 `LoadJson` 한 번
- `Model/BagItem.cs`: `BagItem`(class) `Uid`(읽기), `ItemId`(읽기), `Grade`·`Rotation`·`X`·`Y`(set), `Clone()`
- `Model/BagModel.cs`(`IDisposable`): 대원별 가방 목록 `List<List<BagItem>>`, 보관함 목록, 다음 `Uid`, 마지막 지급 `Uid` 목록, `ReactiveProperty<int> _version`(노출 `ReadOnlyReactiveProperty<int> Version`). 읽기 `MemberCount`, `GetBag(member)`, `StashList`, `LastGrantedUidList`, `FindItem(uid, out member)`(member −1 = 보관함). 쓰기 메서드(`Reset(memberCount)`, `AddToBag`, `RemoveItem`, `AddToStash`, `TakeUid`, `SetLastGranted`, `IncrementVersion`)는 `BagService`만 부른다. `CreateCopy()` = 아이템까지 깊은 복사(미리보기·자동 장착 계획용, `using`으로 Dispose)
- `Model/BagFailReason.cs` enum: `None`(성공), `Locked`, `OutOfBag`, `Overlap`, `CannotMerge`, 그리고 기획서가 이유를 정하지 않은 실패용 `InStash`(보관함 아이템을 보관함으로 해제), `NotPrep`(정비 아님), `NotFound`(없는 Uid — 에러 로그)
- `Model/BagTarget.cs`: 놓을 대상 `readonly struct BagTarget { Kind(Cell·StashItem·StashEmpty), Member, X, Y, Uid }` + 정적 생성 `Cell/StashItem/StashEmpty`(불변 값 형 factory). 화면·진입점·봇이 같은 대상을 만든다
- `Utils/BagRules.cs`(public static, 상태 없음):
  - `GetCells(IReadOnlyList<ItemCell> shape, int rotation, List<Vector2Int> result)` — 회전 수만큼 (x,y)→(−y,x), 최소 x·y를 0으로
  - `GetPlacedCells(shape, rotation, x, y, result)` — 위 + (x, y)
  - `Fits(model, member, uid, shape, rotation, x, y, itemRepository, width, height, out BagFailReason)` — 밖이면 `OutOfBag`, 같은 가방의 다른 Uid 칸과 겹치면 `Overlap`
  - `FindItemAt(model, member, x, y, itemRepository)` — 그 칸을 덮는 아이템(없으면 null)
  - `IsAdjacent(shapeA, a, shapeB, b)` — 서로 다른 Uid, |Δx|+|Δy| = 1 쌍
  - `CanMerge(a, b, lastGrade)` — 다른 Uid, 같은 id, 같은 등급, 등급 < lastGrade
- `Service/BagService.cs`(유일한 `BagModel` 작성자):
  - 규칙 적용(주어진 모델에, 장착 목록·Version 안 건드림): `TryDrop(model, uid, member, x, y, rotation, out reason)`, `TryMerge(model, sourceUid, targetUid, out reason)`, `TryUnequip(model, uid, out reason)`, `TryApply(model, uid, rotation, BagTarget, out reason)`(plan C절 "적용" 대응: Cell→Drop, StashItem이고 `CanMerge`→Merge, StashItem·StashEmpty이고 집은 것이 가방→Unequip, 그 밖 실패), `CanPlace(model, uid, member, x, y, rotation)`(빈 칸 놓기만 — 합성으로 가는 곳 제외. 진입점·자동 장착이 씀)
  - 잠금(A5): 끌고 온 아이템이 가방이면 그 주인, 넣을 대원, 합성 대상이 가방이면 그 주인 → `PartyService.CanChangeEquipment`
  - 실제 조작: `Drop/Merge/Unequip/Apply(...)` = 정비(Prep) 확인(아니면 경고 + `NotPrep`) → `Try*(_bagModel, …)` → 성공이면 모든 대원 장착 목록 = 가방 목록 id 순서(A2, `PartyMember.SetEquippedItemIdList` 새 메서드) → `IncrementVersion`
  - `ResetRun()`(A6): 보관함·지급 목록 비움, Uid 1부터, 대원마다 가방 비우고 장착 목록 아이템을 `startCell`부터 (y, x) 순으로 첫 들어가는 칸에 회전 0·등급 0. 장착 목록은 쓰지 않는다. `Clear()`: 가방·보관함 비움. `AddLoot(itemIdList)`: 등급 0·회전 0으로 보관함 끝, 마지막 지급 Uid 목록 = 이번 것. 셋 다 Version +1
- `Service/BagPlacementInfo.cs : IItemPlacementInfo`(A3): 생성자 `BagModel, GradeRepository, ItemRepository`. slot 밖이면 에러 로그 후 1·false
- `Service/BagPreviewService.cs`: `Preview(uid, rotation, BagTarget)` → 복사본에 `BagService.TryApply` → 결과(성공·이유, 칠할 대원·칸, 변화 줄 목록). `CalculateSheet(BagModel)` → 대원마다 전체 능력치 + 발동 아이템(Uid별) 피해·회복량·주기·사거리·관통·갈래·연쇄. 계산은 `new EquipmentStatService(itemRepo, combatRulesRepo, new BagPlacementInfo(그 모델, …))`로 F3 계산 그대로. 줄 형식 `[개선] 레나 단궁 피해 15.6 → 18.72`(주기는 작아지면 개선). 이전·이후 한쪽에만 있는 발동 아이템은 `[개선] <대원> <아이템> 발동 추가` / `[감소] … 발동 빠짐` 한 줄(plan이 정하지 않은 표현 — 추정)
- `Service/AutoEquipPlanner.cs`(E절, 상태 없음): `GetReviveMemberList()`(쓰러진 대원 순), `Plan()` → 복사본 위에서 합성(보관함 앞부터, 대상 = 산 대원 가방(대원·목록 순) → 보관함 다른 아이템, 되면 처음부터) → 놓기(보관함 앞부터, 산 대원 × 회전 0~3 × y × x 중 `CanPlace`인 곳마다 복사본에 놓아 원정대 점수, 최고(같으면 먼저 찾은 곳)가 지금 점수보다 크면 놓음) 단계 목록 `BagPlanStep{Kind(Merge·Drop), Uid, TargetUid, Member, X, Y, Rotation}`. 점수 = plan E절 식(`CalculateSheet` 값, 자기 회복 주기 = `healPeriod ÷ (1 + 가속)` — F3 `CombatService.GetHealPeriod`와 같음). 계획은 부활 뒤 상태를 전제로 하므로 봇은 부활 → `Plan()` 순서로 부른다
- `Service/AutoEquipPlanner.cs` 생성·연결(DR-01): 생성자 `BagModel, BagService, BagPreviewService, BagRulesRepository, PartyModel, PartyService, GradeRepository, CombatRulesRepository, ItemRepository`, `GameSceneLifetimeScope`에 `builder.Register<AutoEquipPlanner>(Lifetime.Scoped)` 한 줄(plan D절 표에 없는 등록 줄 — 핸드오프 "기준 문서와 다른 점"에 적음). 소비자는 판정 봇(`PlayableBot`, `PlayableProbe.Resolve<AutoEquipPlanner>()`)과 EditMode 시험뿐
- 의존 방향(순환 없음): `RunService → BagService, LootService, CombatService`; `LootService → BagService, BagModel, PartyModel, ItemRepository, LootRulesRepository`; `BagService → BagModel, RunModel(정비 확인은 RunModel.Phase를 직접 읽음 — RunService에 기대지 않는다), PartyModel, PartyService, ItemRepository, GradeRepository, BagRulesRepository`; `BagPlacementInfo → BagModel, GradeRepository, ItemRepository`; `EquipmentStatService → IItemPlacementInfo`; `BagPreviewService → BagModel, BagService, ItemRepository, CharacterRepository, CombatRulesRepository, GradeRepository, PartyModel`; `AutoEquipPlanner → (위)`; `BagPresenter → BagModel, BagService, BagPreviewService, RunModel, PartyModel, CombatModel, ItemRepository, GradeRepository, CharacterRepository, CombatRulesRepository, PoolManager, UI_PrepView`. 어느 서비스도 Presenter·RunService를 받지 않는다
- `Presenter/BagPresenter.cs`(진입점, `IInitializable, IDisposable`): 상태 집은 Uid(−1), 회전, 미리보기 대상(nullable), 상세 보기, 설명 Uid, 결과 줄. 구독: `BagModel.Version`, `Phase == Prep`(집기 해제 + 다시 그림), `CombatModel.ReviveCount`(어둡게 표시 갱신), 회전·선택 취소·상세 버튼, 가방·보관함 View 입력. 공개 처리 메서드 `OnItemPressed(uid)`, `OnTargetTapped(BagTarget)`, `OnItemDragged(BagTarget?)`, `OnItemDropped(BagTarget?)`, `OnRotateClicked`, `OnCancelClicked`, `OnDetailClicked`(plan C절 표). 그리기는 View마다 `UpdateUI(...)` 한 번. 보관함 항목 View가 모자라면 `PoolManager.Get`(템플릿 = `UI_StashView` 안 항목 하나)으로 늘린다
- `View/UI_BagView`(칸 16개 SerializeField, 칸에 (대원, x, y) 지정, 입력 사실을 R3 `Subject`로 위로), `UI_BagCellView`(배경·테두리·글, `IPointerDownHandler/IDragHandler/IPointerUpHandler`), `UI_StashView`(항목 목록·빈 곳 탭 `IPointerClickHandler`), `UI_StashItemView`(이름·등급 색·작은 모양 4×4·"새"), `UI_ItemInfoView`(설명 글, 미리보기 글(스크롤), 상세 버튼). 입력 대상 찾기 = `PointerEventData.pointerCurrentRaycast.gameObject.GetComponentInParent<UI_BagCellView/UI_StashItemView/UI_StashView>()`
- 입력 분기 계약(DR-02). View는 사실만 올린다: `Pressed(source)`(아이템이 있는 칸·보관함 항목을 누름), `Tapped(target)`(드래그 없이 뗌 = 클릭, 빈 칸·보관함 빈 곳 포함), `Dragged(target?)`(드래그 중 손가락 밑 대상), `Dropped(target?)`(드래그를 끝내고 뗌 — `PointerEventData.dragging`이 참일 때만). Presenter의 전달:
  - `Pressed(source)`: 아무것도 안 집었으면 `OnItemPressed(source.Uid)`, 이번 누름이 집은 누름(`_isPressPicking`)이라고 기억. 이미 집었고 누른 것이 집은 아이템이면 이번 누름 = 집은 것을 끄는 누름. 그 밖(집은 상태에서 다른 아이템을 누름)은 **집기를 바꾸지 않는다**(다른 대상 탭이 될 누름)
  - `Tapped(target)`: 이번 누름이 집은 누름이면 아무것도 안 함(집기만 끝). 그 밖에 집은 상태면 `OnTargetTapped(target)` — 같은 대상이 미리보기 중이면 적용, 아니면 그 대상으로 미리보기. 집지 않은 상태의 빈 곳 탭은 무시
  - `Dragged(target?)`: 이번 누름이 집은 아이템을 끄는 누름일 때만 `OnItemDragged(target?)`(대상 있으면 미리보기, 없으면 미리보기 지움)
  - `Dropped(target?)`: 이번 누름이 집은 아이템을 끄는 누름일 때만 `OnItemDropped(target?)` — 대상 있으면 바로 적용, 없으면 취소(원래 자리, 집기 해제). 그 밖의 드래그 끝은 무시
  - 진입점·봇은 `OnItemPressed` → (`OnRotateClicked`×n) → `OnItemDropped(target)`을 직접 부른다(사람 길의 드래그 놓기와 같은 처리)
  - AC20 사람 확인 인계 항목: 첫 대상 탭 = 미리보기만(상태 그대로), 같은 대상 두 번째 탭 = 적용, 드래그 중 미리보기, 대상 위에서 놓기 = 적용, 빈 곳(대상 없음)에 놓기 = 취소, 회전 버튼, 선택 취소 버튼

### 전리품 `Features/Loot/`
- `Data/LootRulesData.cs`, `Registry/LootRulesRepository.cs`
- `Service/LootService.cs`: `ResetRun(runSeed)` → `new System.Random(unchecked(runSeed + lootSeedOffset))`. `GrantWaveLoot(completedWave)`: 후보(`lootWeight` > 0, id ordinal 순) → 가중치 표 한 번(`GetWeight(item, completedWave)` B3 배율, 살아 있는 대원만) → 수 `Next(min, max+1)` → `PickIndex(weightList, rng.NextDouble())` 반복 → `BagService.AddLoot`. 후보 0개면 경고 후 없음. 대상 맞음 판정(`attack` = 회복 둘·지속 뺀 방식)은 `EquipmentStatService`의 private 판정과 같은 규칙을 LootService 안에 private으로 둔다(공유 파일 F3 `EquipmentStatService`를 D절 밖으로 고치지 않기 위해 — 합친 뒤 하나로 합칠 후보로 보고)

### 공유 파일(D절 줄만)
- `GameSceneLifetimeScope`: 데이터 `GradeRepository`·`BagRulesRepository`·`LootRulesRepository`, 모델 `BagModel`, 서비스 `BagService`·`BagPreviewService`·`LootService`, `IItemPlacementInfo` → `BagPlacementInfo`, 진입점 `BagPresenter`(AsSelf) — 줄 추가·한 줄 교체만
- `RunService`: 생성자에 `BagService`, `LootService` 인자·필드. `StartRun`: `CreateParty` 뒤 `_bagService.ResetRun(); _lootService.ResetRun(runSeed);`(F3 `CombatService.ResetRun` 앞 — 등급 배수가 가방을 읽음). `FinishWave`: `SetPhase(Prep)` 바로 앞 `_lootService.GrantWaveLoot(wave)`. `ReturnToLobby`: `ClearParty` 옆 `_bagService.Clear()`
- `RunModel`: 바꾸지 않음(시드는 `RunService`가 지역 `runSeed`를 `LootService.ResetRun`에 직접 넘김 — D절 "없을 때만"의 조건이 필요 없음)
- `EquipmentStatService`: 바꾸지 않음(위 미리보기 방식)
- `UI_PrepView`: `[SerializeField] UI_BagView[] bagViews; UI_StashView stashView; UI_ItemInfoView itemInfoView; Button rotateButton; Button cancelButton;` + 읽기 속성
- `PartyModel.cs`(`PartyMember`): `SetEquippedItemIdList(IEnumerable<string>)` 한 메서드 추가 — A2 "BagService가 장착 목록을 다시 쓴다"에 필요한 쓰기 길이 없어서(D절 표에 없는 파일 — 핸드오프에 적음)
- `PlayableProbe`: H절 읽기·조작 멤버, `PlayableBot` `SupportedModes`에 `Equip` + `Update` 앞에 정비 화면마다 한 번 장착(부활 → 계획 실행, 진입점은 `BagPresenter` 처리 메서드만 부름). `LastPlacedMember`·놓은 아이템 id는 진입점 private static(에디터 전용)
- `Tools/check_data.py`: Grade·Bag·Loot 검사, 아이템 `lootWeight`, `--self-test` 4종 추가
- `Tests/EditMode/TestRunContext.cs`(시험 도우미): `RunService` 생성자가 바뀌어 `BagModel`·저장소·`BagService`·`LootService`를 만들고 넘긴다. 선택 인자 `bagModel` 추가(배치 정보 시험이 같은 모델을 쓴다). 기본 배치 정보는 지금처럼 `NoBagPlacementInfo`(F3 시험 기대 그대로)

## 재사용 후보와 신설 이유
- 미리보기 계산: F3 `EquipmentStatService`를 복사본 배치 정보로 새로 만들어 재사용(오버로드 불필요)
- `ItemCell`을 `startCell` 형으로 재사용. 새 저장소는 `CombatRulesRepository` 방식 복제
- 대상 형 `BagTarget`(plan 목록 밖 새 파일 1개): 화면·진입점·봇·서비스(`TryApply`)·미리보기 5곳이 같은 대상을 쓴다
- 대상 맞음 판정은 F3 private 함수와 같은 내용을 LootService에 한 번 더 둔다(위 이유)

## 실패·취소·생사·전환 경계
- 정비가 아니면 모든 실제 조작 거부(`NotPrep`, 경고 로그). 미리보기는 정비 화면에서만 열리므로 단계 확인 없이 규칙·잠금만
- 쓰러진 대원: 집기 거부(설명 칸 문구), 그 가방으로 넣기·꺼내기·합성·해제 `Locked`. 부활(`ReviveCount`) 뒤 다시 그림
- 실패하면 모델이 바뀌지 않는다: `Try*`는 모든 확인을 먼저 하고 마지막에만 쓴다
- 판 시작: `BagService.ResetRun`이 지난 판 가방·보관함을 다 지움. 로비: `Clear`. Presenter는 Prep 진입마다 집기·미리보기를 푼다
- 전멸·승리 웨이브: `FinishWave`의 승리 분기·`EndRun`은 지급 줄을 지나지 않는다

## 데이터 호환성과 복구 방법
- 저장 없음(gdd A9). 아이템 파일에 `lootWeight` 칸이 없어도 1. 새 폴더 3개 — `Item/`에는 아이템만(F1·F3 `LoadAllJson`)
- 되돌리기: 등록 한 줄을 `NoBagPlacementInfo`로 돌리면 배치 정보 계산(등급 배수·인접)만 F3 구현으로 돌아간다. 전리품 지급·가방 조작·장착 목록 쓰기까지 되돌리려면 F5 커밋 전체를 되돌린다

## 최소 필수 검증
- 순수 계산(회전·Fits·인접·합성 판정·지급 가중치·가중치 뽑기·자동 장착 계획): algorithm-tester 실제 실행(이 실행)
- 데이터: `check_data.py`·`--self-test`(이 실행). 경로 형식: `playable.py --check`, `--check --route f5-bag-flows.json`(이 실행). `route.json`·F1·F3 흐름 무변경 `git diff --exit-code e175cee`
- EditMode(AC3·AC7~AC16 테스트 7파일), 컴파일, 흐름 경로(AC17~AC19), 화면비(AC21): 합친 뒤 루트 실행

## 초기 실제 연결
- 정상 사례: 적 수 배율 0으로 출발 → 웨이브 1 끝 → `FinishWave`가 `GrantWaveLoot(1)` → 보관함 3~5 → `PlaceFirstStashItem`(Presenter `OnItemPressed`→`OnItemDropped`) → `BagService.Drop` → 장착 목록 갱신 → 다음 웨이브 `PrepareWave`가 그 대원 칸을 새로 만든다 → `HasBattleItem` 참. 흐름 경로 `PREP`·`PLACE`·`NEXT`(루트 실행)

## 검사 확대/도구 신설 조건
- 새 도구 없음. `<합성 시드>`는 전리품 난수(.NET `System.Random` 시드 규칙)를 파이썬으로 같은 식으로 계산해 고르고, 그 계산 스크립트를 증거 폴더에 남긴다(게임 코드 아님). 루트 실행의 `PREP2`가 실제로 확인한다

## 알려진 환경 차단과 재개 조건
- 이 worktree에는 에디터가 없다(디렉터 지시). 컴파일·EditMode·Play·장면 연결은 브랜치를 합친 뒤 루트 실행. `.meta`는 그때 에디터가 만든다

## 미정 사항 및 진행에 미치는 영향
- 없음(기획서 BA1~BA9). 추정으로 정한 것은 위에 "추정"으로 표시하고 핸드오프 claims에 inferred로 적는다

## 설계 검토 결과와 해소한 지적
- 1차(design-reviewer, Codex): FAIL — 필수 DR-01(자동 장착 계획기 생성·DI 연결 누락), DR-02(탭·드래그 입력 분기 계약 누락). 후속 DR-03(BagItem 변경 가능 객체 노출), DR-04(복구 설명 범위)
- 해소: DR-01 → 위 `AutoEquipPlanner` 생성자·등록 줄·소비자와 전체 의존 방향 추가, `BagService`는 `RunModel`을 직접 읽음. DR-02 → 위 "입력 분기 계약"과 AC20 인계 항목. DR-04 → "데이터 호환성" 문장 고침. DR-03 → 후속 목록(쓰기 메서드는 `BagService`만 부르는 규칙으로 두고, 읽기 전용 인터페이스는 이번에 만들지 않음 — 한 번만 쓸 추상화 금지 지시)
- 수정 확인(design-reviewer, Codex): PASS — DR-01·DR-02·DR-04 해소, 새 결함 없음. 후속: 처음 눌러 집은 누름도 바로 드래그할 수 있게(구현에서 반영 — 집은 누름은 끄는 누름으로도 친다), DR-03 유지
