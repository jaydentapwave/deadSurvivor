# F5 가방과 정비, 전리품 기획서

- 상태: 승인 (2026-11-15, 디렉터 확인 — 게임 시작 전 승인 범위 안, director-game autonomy 1절)
- 서버 필요: 아니오
- 관련 문서: [게임 기획서](../../game/gdd.md) F5(S5, S6), 판정 단계 G3·G4·G7(이 기능이 진입점 멤버를 채움, 통과는 F2·F4가 합쳐진 뒤 — H절), G2(회귀). 앞선 기능 [F1 기획서](../f1-skeleton/plan.md)(승인, E·G·H절), [F3 기획서](../f3-combat/plan.md)(승인, B2 `IItemPlacementInfo`, C절 `shape`, E절 `CanChangeEquipment`). 참고 분석 문서 없음. 예전 판 코드 `Assets/DeadSurvivor/Runtime/EquipmentRules.cs`·`DSGame.Inventory.cs`·`DSGame.Run.cs`·`DSUI.Equipment.cs`·`DSUI.cs`와 예전 값 `Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json`(읽기 전용, 규칙만 옮김)
- 작업 규모: L2(새 상태 소유권 — 가방·보관함, F3 배치 정보 자리 대체, 데이터 스키마 추가) — 구현 전 설계 검토

## 목표
웨이브가 끝나면 전리품 3~5개가 보관함에 들어오고, 정비 화면에서 그것을 대원별 4×4 가방에 끌어 넣거나 돌리고 합성해 다음 웨이브의 발동 목록과 능력치를 바꾼다(기둥 P2 "강해지는 길은 가방 배치 하나").
F3이 비워 둔 배치 정보 자리(등급 배수·인접)를 실제 가방으로 채우고, 판정 진입점의 보관함·배치·자동 장착 멤버를 채운다.

## 참고 분석 요약
예전 판(3D, 같은 게임의 이전 구현) 코드에서 옮길 규칙. 모두 관찰이며 출처는 위 경로다.

- 가방: 대원마다 4×4 칸. 아이템은 데이터의 칸 모양을 90°씩 돌려 놓는다. 회전 한 번 = 각 칸 (x, y) → (−y, x), 그 뒤 가장 작은 x·y를 0으로 당긴다. 칸이 가방 밖이거나 다른 아이템과 겹치면 놓지 못한다(자기 자신은 겹침에서 뺀다) (관찰, `EquipmentRules.Shape`·`Fits`)
- 인접: 두 아이템의 칸 중 맨해튼 거리 1인 쌍이 하나라도 있으면 인접 (관찰, `EquipmentRules.Adjacent`)
- 놓기: 놓을 칸 밑에 같은 종류·같은 등급의 다른 아이템이 있으면 합성, 아니면 들어갈 때만 옮긴다. 옮긴 아이템은 원래 자리(보관함·다른 가방)에서 빠져 그 가방 목록 끝에 붙는다 (관찰, `EquipmentRules.Drop`)
- 합성: 같은 종류·같은 등급, 서로 다른 두 개, 대상 등급이 최고 등급 아님. 끌고 온 쪽이 사라지고 대상 등급 +1, 대상은 제자리 (관찰, `MergeAllowed`·`Merge`)
- 해제: 가방의 아이템을 보관함으로 보낸다. 보관함은 개수 제한이 없다 (관찰, `Unequip`, `Stash` 목록)
- 장비 잠금: 쓰러진 대원의 가방으로 넣기, 쓰러진 대원의 아이템 꺼내기·합성·해제는 거부 (관찰, `Drop`·`Merge`·`Unequip`의 "사망자 장비 잠금")
- 한 판 시작: 가방 16칸을 모두 열고 시작 아이템을 왼쪽 위(0, 0)에 회전 0으로 놓는다. 보관함을 비운다 (관찰, `StartRun`)
- 등급 5개: 일반 ×1, 고급 ×1.4, 희귀 ×2, 영웅 ×3, 전설 ×4.5. 전리품은 늘 일반 (관찰, `PrototypeData.json` `grades`, `MakeItem`)
- 전리품: 마지막이 아닌 웨이브가 끝나 정비로 갈 때 3~5개(균등)를 가중치로 뽑아 보관함에 넣는다. 가중치는 32종 모두 1. 적응 가중치: 끝난 웨이브 ≤ 3이면 살아 있는 대원에게 추가 효과 대상이 맞는 아이템 ×2, 끝난 웨이브 ≥ 4면 살아 있는 대원이 일반 등급으로 장착한 종류 ×1.5. 가중치 표는 한 번 만들어 그 웨이브의 전리품 전부에 쓴다. 전리품 난수는 생성 난수와 따로 돈다 (관찰, `FinishWave`·`RewardWeights`·`Weighted`, `settings.weights`·`adaptiveDrops`)
- 정비 조작: 아이템을 탭하면 집고(설명 표시), 회전 버튼으로 집은 아이템을 돌리고, 대상 칸을 한 번 탭하면 미리보기, 같은 곳을 다시 탭하면 확정. 드래그는 끄는 동안 미리보기, 놓으면 적용, 대상이 없는 곳에 놓으면 취소. 미리보기는 칸을 초록(가능)·빨강(불가)으로 칠하고, 불가 이유와 대원별 능력치 변화(이전 → 이후, 개선·감소)를 보여 준다 (관찰, `DSUI.cs` 탭·드래그 처리, `DSUI.Equipment.cs` `ShowEquipmentPreview`·`Compare`)

## 적용안

### 가져올 것 / 버릴 것 / 바꿀 것
- 가져올 것: 위 가방·회전·겹침·인접·합성·해제·잠금 규칙, 등급 5개와 배수, 전리품 수·가중치·적응 가중치 규칙과 수치, 탭·드래그 조작과 미리보기 항목
- 버릴 것: 가방 확장 아이템(`expand`, 예전에도 데이터 0개), 6×6 배열 안 1~4 좌표(0~3 좌표로), 아이템을 만들 때 쿨에 넣던 0~0.2초 난수(F3 CA4), 등급의 `armor` 칸(예전 코드가 읽지 않음), 추천 테두리 깜빡임, 문자열 상태·토스트 문장
- 바꿀 것: 규칙은 순수 함수(`BagRules`)와 Service, 상태는 `BagModel`(R3), 화면은 Presenter + `UI_` View. 등급·가방·전리품 수치는 데이터 파일로. 가중치는 아이템 파일의 칸(없으면 1)이라 지금 있는 아이템 파일만으로 돈다

### 기존 시스템과의 관계
확장 + 신규. F1의 장착 목록(`PartyModel`의 대원별 아이템 id 목록)과 F3의 배치 정보 자리(`IItemPlacementInfo`)를 이 기능이 채운다. 가방·보관함·전리품 상태와 규칙은 `Features/Bag/`·`Features/Loot/`에 새로 만든다. F1·F3 코드는 아직 구현 중이라 이 기획서는 두 기획서의 이름·구조를 기준으로 썼다. 구현은 실제 코드 이름에 맞추고, 다르면 핸드오프에 적는다.

### A. 가방과 장착 목록 (S5)

A1. **상태 소유**: 가방·보관함의 원본은 `BagModel` 하나다. 대원마다 가방 아이템 목록(`BagItem`: 이번 판 번호 `Uid`, 아이템 id, 등급, 회전 0~3, 왼쪽 위 칸 x·y)과 보관함 목록(같은 `BagItem`, 칸 x·y는 쓰지 않음)을 가진다. `BagService`만 `BagModel`을 바꾼다. 바뀔 때마다 `BagModel.Version`(R3, 읽기 노출)을 1 올려 화면이 다시 그린다.

A2. **장착 목록 맞추기**: 가방을 바꾼 `BagService`는 같은 호출 안에서 그 대원의 `PartyModel` 장착 목록 = 그 가방 목록의 아이템 id 순서대로를 다시 쓴다. 가방 목록의 순서 번호가 F3의 "아이템 칸" 번호(slot)다. 그래서 정비 중에도 두 목록이 늘 같다. 장착 목록을 쓰는 쪽은 F1의 한 판 시작 줄과 `BagService`뿐이다.

A3. **배치 정보(F3 B2를 채움)**: `BagPlacementInfo : IItemPlacementInfo` —
- `GetGradeMultiplier(member, slot)` = 등급 데이터[가방 목록[slot].등급].`mult`
- `IsAdjacent(member, slotA, slotB)` = `BagRules.IsAdjacent`(두 가방 아이템, A4)
- slot이 목록 밖이면 에러 로그 후 배수 1·인접 아님(F3 계산이 장착 목록과 같은 수의 칸만 묻으므로 정상 흐름에서는 생기지 않는다).
- `GameSceneLifetimeScope`의 `IItemPlacementInfo` → `NoBagPlacementInfo` 등록 한 줄을 `BagPlacementInfo`로 바꾼다. `NoBagPlacementInfo` 클래스는 지우지 않는다(F3 시험이 쓸 수 있다). 계산식(F3 B3·B4)은 바뀌지 않는다.

A4. **가방 규칙** `BagRules`(순수 함수, 상태 없음)
- 칸 모양: 아이템 `shape`(F3 C절, 0 ≤ x, y ≤ 3)를 회전 수만큼 (x, y) → (−y, x)로 돌린 뒤 최소 x·y를 0으로 당긴다. 좌표 뜻: x = 가방 오른쪽, y = 가방 아래(화면에서 위 줄이 y 0).
- 들어감 `Fits`: 돌린 모양의 모든 칸 (x + dx, y + dy)이 0 ≤ … < `bagWidth`(4)·`bagHeight`(4) 안이고, 그 가방의 다른 아이템(같은 `Uid`는 뺀다) 칸과 겹치지 않음.
- 인접 `IsAdjacent`: 서로 다른 두 아이템의 칸 중 |Δx| + |Δy| = 1인 쌍이 있음.
- 합성 가능 `CanMerge`: 서로 다른 `Uid`, 같은 아이템 id, 같은 등급, 등급 < 마지막 등급 번호(등급 데이터 수 − 1).

A5. **조작** (`BagService`, 모두 지금 화면이 정비(Prep)일 때만. 아니면 경고 로그 후 거짓)
- 잠금: 끌고 온 아이템이 가방에 있으면 그 주인, 넣을 가방의 대원, 합성 대상이 가방에 있으면 그 주인 — 모두 `PartyService.CanChangeEquipment(member)`(F3 E절)가 참이어야 한다. 아니면 거짓과 이유 `Locked`.
- 놓기 `Drop(uid, member, x, y, rotation)`: 놓을 칸 (x, y)를 덮는 그 가방의 다른 아이템이 `CanMerge`면 합성(아래)으로 간다. 아니면 `Fits`일 때만, 원래 자리(보관함 또는 가방)에서 빼고 그 가방 목록 끝에 붙인다(같은 가방 안에서 옮겨도 끝으로 간다). 실패 이유: `OutOfBag`(칸이 밖), `Overlap`(겹침), `Locked`.
- 합성 `Merge(sourceUid, targetUid)`: `CanMerge`일 때만. 끌고 온 쪽을 원래 자리에서 빼고, 대상 등급 +1. 대상은 제자리(가방이면 같은 칸·같은 순서, 보관함이면 같은 자리). 실패 이유: `CannotMerge`, `Locked`.
- 해제 `Unequip(uid)`: 가방의 아이템을 빼서 보관함 끝에 붙인다(회전 값은 그대로). 보관함의 아이템이면 아무것도 안 하고 거짓.
- 회전은 집은 동안의 화면 상태다(`BagPresenter`). 놓기에 성공해야 그 아이템의 회전 값이 바뀐다.
- 가방 칸이 16개이고 아이템은 1칸 이상이라 한 대원의 아이템 칸은 16개를 넘지 않는다(F3 F절 하단 목록 16칸).

A6. **판의 시작과 끝** (`RunService`가 부르는 자리에 한 줄씩)
- 한 판 시작(`StartRun`): F1이 장착 목록에 시작 아이템을 넣은 뒤, F3 `CombatService.ResetRun` 앞에 `BagService.ResetRun()`. 보관함을 비우고 `Uid`를 1부터 다시 매기며, 대원마다 가방을 비우고 지금 장착 목록의 아이템을 차례로 `startCell`(0, 0)부터 들어가는 첫 칸(y, x 순으로 훑음, 회전 0)에 일반 등급으로 놓는다. 시작 아이템은 1개라 (0, 0)에 들어간다. 장착 목록은 바뀌지 않는다(F1 `PartyServiceTests` 기대 그대로).
- 로비로 갈 때(`ReturnToLobby`): `BagService.Clear()` — 가방·보관함을 비운다(F1이 적을 비우는 자리에 한 줄).
- 웨이브 사이에 가방이 바뀌면 F3 B7이 웨이브를 열 때 능력치·아이템 칸을 다시 만든다(장착 목록이 다른 대원만 쿨 0). 합성으로 등급만 바뀐 대원은 장착 목록이 같아 쿨이 이어지고, 능력치는 다시 계산된다.

### B. 전리품 지급 (S6)

B1. **언제**: `RunService.Step`의 ⑦에서 웨이브가 끝나 정비(Prep)로 갈 때(마지막 웨이브는 승리로 가므로 지급 없음, 전멸이면 패배라 없음 — F1 B 표 그대로). 정비로 바꾸기 직전에 `LootService.GrantWaveLoot(completedWave)` 한 줄. 1~9웨이브 끝 9번이다(웨이브 수는 F1처럼 파일 수에서).

B2. **난수**: `LootService`는 한 판 시작 때 `new System.Random(unchecked(이번 판 시드 + lootSeedOffset))`로 전리품 난수를 따로 만든다(A11). 이번 판 시드는 F1이 정한 실제 시드(시드 0이면 시각에서 뽑은 값)다. F1 `RunModel`에 그 값의 읽기가 없으면 읽기 하나를 더한다. 생성·전투 난수와 섞이지 않아, 같은 시드면 전투 결과와 상관없이 같은 웨이브 끝의 지급 수가 같다(종류는 B3 적응 가중치가 장착에 따라 바뀐다).

B3. **한 웨이브의 지급**
1. 후보 = `ItemRepository`의 모든 아이템 중 `lootWeight` > 0, id 문자열 순(ordinal). 개수는 파일 수에서 온다(코드에 8·32를 쓰지 않는다).
2. 가중치 = `lootWeight` × 배율. 배율(살아 있는 대원만 본다):
   - 끝난 웨이브 ≤ `earlyWaveMax`(3)이고, 그 아이템에 추가 효과가 있으며, [조건 없는 추가 효과(`bonusTarget` 빔, `bonusNeedsAdjacent` 거짓)이거나 살아 있는 대원 누군가의 장착한 발동 아이템(`passive` 아님)이 `bonusTarget`에 맞음(빔 = 발동 아이템 아무거나, `attack` = 회복 두 방식 뺀 발동, 그 밖 = 같은 방식, F3 B3과 같은 판정)] → `earlyBonusFactor`(2)
   - 그 밖에 끝난 웨이브 ≥ `mergeWaveMin`(4)이고, 살아 있는 대원 누군가가 그 아이템을 일반 등급(0)으로 가방에 가지고 있음 → `mergeFactor`(1.5)
   - 그 밖 1
3. 지급 수 = `rng.Next(minCount, maxCount + 1)`(3~5).
4. 한 번 만든 가중치 표로 지급 수만큼: r = `rng.NextDouble()` × 가중치 합, 후보 순서대로 r −= 가중치, r < 0이 되는 후보(끝까지 가면 마지막 후보). 뽑은 아이템은 일반 등급·회전 0으로 보관함 끝에 붙는다.
5. 후보가 0개면 경고 로그를 남기고 지급하지 않는다.

B4. 가중치 출발값: 모든 아이템 `lootWeight` 1(예전 값). 게임 기획서 A2(전투 도끼·쇠뇌는 전리품으로만)는 시작 아이템이 아닌 모든 아이템에 해당하며, 시작 아이템도 전리품으로 나온다(예전과 같음 — 같은 종류를 합성하는 길).

### C. 정비 화면 (V3)

| 화면 | F1·F3이 넣은 것 | 이 기능이 더하는 것 |
|---|---|---|
| V3 정비 | 판넬, "웨이브 n 완료", 대원 4명 자리(이름·HP), 다음 웨이브 버튼(F1). 쓰러짐 표시·부활 버튼(F3) | 대원 자리마다 4×4 가방(`UI_BagView`), 보관함(`UI_StashView`, 새 전리품 포함 전부), 아이템 설명과 수치 변화 미리보기(`UI_ItemInfoView`), 회전·선택 취소 버튼 |

- 배치(가상 640×360, F1 I절 Canvas 기준): 위쪽에 대원 4명 자리를 가로로 놓고 그 아래에 가방 4개(칸 출발값 22 가상 px, 가방 하나 88×88). 아래 왼쪽 보관함(가로 스크롤 목록), 아래 오른쪽 설명·미리보기, 그 사이 회전·선택 취소 버튼. 진형 3종 자리(F2)는 비워 둔다. 모든 버튼·판넬은 F1 `UI_SafeAreaPanel`에 앵커.
- 가방 칸 그림: 아이템은 모양대로 칸을 칠하고(등급 색 테두리, 등급 데이터 `color`), `icon`이 비면 이름 첫 글자 2자. 쓰러진 대원의 가방은 어둡게. 칸·판넬 그림(양피지·가죽)은 아트 단계(기준서 3.3 UI 행)가 넣는다.
- 보관함 항목: 아이템 이름·등급 색·칸 모양 작은 그림. 이번 정비에서 새로 들어온 것은 "새" 표시(마지막 지급의 `Uid`들).
- 설명(집은 아이템 또는 마지막으로 탭한 아이템): 이름, 등급, 발동 방식, 피해 또는 회복량(× 등급 배수)·간격·사거리(× 42), 기본 효과와 값, 추가 효과와 값·조건(대상 방식, 인접 필요), 지금 상태(가방에 있으면 `활성`·`인접 필요`·`대상 없음`, 보관함이면 `놓는 곳에 따라`).
- 미리보기(놓을 곳을 정했을 때): `BagPreviewService`가 지금 가방·보관함을 복사해 같은 `BagService` 규칙으로 조작을 적용해 본다. 성공이면 놓일 칸 초록, 실패면 그 칸 빨강과 이유 글(잠금·가방 밖·겹침·합성 불가). 성공이면 대원마다 F3 `EquipmentStatService`로 이전·이후를 계산해 바뀐 항목만 줄로 낸다: 최대 HP, 방어, 공격 증가, 자기 회복, 보호막, 그리고 발동 아이템마다 피해·회복량, 주기, 사거리, 관통·갈래·추가 연쇄. 줄마다 "[개선]" 또는 "[감소]"(주기는 작아지면 개선). 바뀐 것이 없으면 "능력치 변화 없음". 간략 보기 3줄 + "추가 변화 n개", 상세 보기 버튼으로 전체(스크롤).
- 미리보기는 복사본에 F3 계산을 부른다. F3 `EquipmentStatService`가 `PartyModel`을 직접 읽게 만들어졌으면, 같은 계산을 (캐릭터, 장착 목록, `IItemPlacementInfo`)를 받아 하는 오버로드 하나를 더한다(계산식은 바꾸지 않음).

**조작 흐름** (`BagPresenter`, 화면 이벤트 이름은 `On…`)

| 지금 | 입력 | 결과 |
|---|---|---|
| 아무것도 안 집음 | 가방·보관함 아이템 탭 또는 누르기 시작 | 집음(설명 표시, 회전 = 그 아이템 회전). 쓰러진 대원의 아이템이면 집지 않고 "쓰러진 대원은 부활한 뒤 장비를 바꿀 수 있다" |
| 집음 | 회전 버튼 | 회전 = (회전 + 1) mod 4, 미리보기 다시 |
| 집음 | 가방 칸·보관함 아이템·보관함 빈 곳을 탭 | 그곳을 대상으로 미리보기. 같은 대상을 다시 탭하면 적용 |
| 집음(드래그 중) | 끌기 | 손가락 밑 대상으로 미리보기 |
| 집음(드래그 중) | 놓기 | 대상이 있으면 적용, 없으면 취소(원래 자리 유지) |
| 집음 | 선택 취소 버튼 | 집기 해제 |

- 적용: 대상이 가방 칸 → `Drop`. 대상이 보관함 아이템이고 `CanMerge` → `Merge`. 대상이 보관함 빈 곳이거나 합성이 안 되는 보관함 아이템이고 집은 것이 가방에 있음 → `Unequip`. 그 밖은 실패. 적용 뒤 집기를 풀고 결과 한 줄(성공 "적용했다" / 실패 이유)을 설명 칸에 보인다.
- 버튼·드래그 처리 메서드(`OnItemPressed`, `OnTargetTapped`, `OnItemDropped`, `OnRotateClicked`, `OnCancelClicked`)는 판정 진입점이 그대로 부를 수 있게 공개한다.
- 입력은 uGUI 이벤트(`IPointerDownHandler`·`IDragHandler`·`IPointerUpHandler`, F1 `InputSystemUIInputModule`)로 받는다. 대상 찾기는 이벤트 레이캐스트로 `UI_BagCellView`·`UI_StashItemView`·보관함 빈 곳을 찾는다.

### D. 화면과 기능의 경계 (뒤 기능과 겹치는 파일)
이 기능은 아래 파일에 줄·칸만 더한다. F2·F4·F6 구현이 나란히 돌고 나중에 합쳐지므로 그 밖의 줄은 건드리지 않는다.

| 파일 | 이 기능이 더하거나 바꾸는 줄·칸 |
|---|---|
| `02.Scripts/App/GameSceneLifetimeScope.cs` | 등록 줄: `GradeRepository`, `BagRulesRepository`, `LootRulesRepository`, `BagModel`, `BagService`, `BagPreviewService`, `LootService`, 진입점 `BagPresenter`. `IItemPlacementInfo` 등록 한 줄을 `NoBagPlacementInfo` → `BagPlacementInfo`로 |
| `02.Scripts/App/PlayableProbe.cs` | H절 읽기·조작 멤버, 봇 `PlayableBot`의 `Equip` 깃발 처리(F1이 "경고 후 무시"로 둔 자리를 채움). 다른 깃발 처리는 건드리지 않음 |
| `01.Scene/Main.unity` | 정비 판넬 안에 가방 4개(`UI_BagView`, 칸 16개씩), 보관함, 설명·미리보기 판넬, 회전·선택 취소 버튼 |
| `UI_PrepView.cs` | `[SerializeField] UI_BagView[]`, `UI_StashView`, `UI_ItemInfoView`, 회전·선택 취소 `Button` 칸 |
| `RunService.cs` | A6의 시작·로비 한 줄씩, B1의 정비 전환 직전 한 줄 |
| `RunModel.cs` | 이번 판 실제 시드 읽기가 없을 때만 그 읽기 하나(B2) |
| `EquipmentStatService.cs`(F3) | C절 미리보기 오버로드가 필요할 때만 하나 |
| `Tools/check_data.py` | 데이터 스키마 아래의 검사와 `--self-test` 틀린 데이터 4종 |
| HUD View(`UI_BattleHudView`·`UI_MemberStatusView`), `Run/placeholder_combat.json` | 변경 없음 |

### E. 자동 장착 봇 (`BotMode.Equip`, 판정용)
G7·M6(이동 + 자동 장착 봇이 시드 3개 중 2개 이상 승리)을 위한 정해진 규칙. 계산은 `AutoEquipPlanner`(순수, EditMode 시험)가 하고, 봇은 계획을 `BagPresenter`의 처리 메서드로 실행한다(사람이 누르는 길과 같다).

정비 화면에 들어온 뒤 첫 프레임에 한 번, `AutoNext`가 다음 웨이브를 누르기 전에 차례로:
1. 부활: 쓰러진 대원마다 F3 `PressRevive`(부활은 무료). `Equip`이 없으면 부활하지 않는다(F3 CA5, G5 그대로).
2. 합성: 보관함 앞에서부터, 그 아이템과 `CanMerge`인 대상을 [살아 있는 대원의 가방(대원 순, 가방 목록 순) → 보관함의 다른 아이템(앞에서부터)] 순으로 찾아 합성. 합성이 하나라도 되면 1번 아이템부터 다시 훑는다. 더 없을 때까지.
3. 놓기: 보관함 앞에서부터 아이템마다, 살아 있는 대원 × 회전 0~3 × y 0~3 × x 0~3 중 `Drop`이 빈 칸 놓기로 성공하는 곳(합성으로 가는 곳은 뺀다)마다 원정대 점수를 계산해 가장 높은 곳에 놓는다. 같으면 앞 대원·작은 회전·작은 y·작은 x. 가장 높은 점수가 지금 점수보다 크지 않으면 보관함에 남긴다.
   - 원정대 점수 = 살아 있는 대원마다 [발동 아이템마다 (피해 또는 회복량 ÷ 주기) 합 + `hpWeight`(0.05) × 최대 HP ÷ (1 − 유효 방어) + `shieldWeight`(0.1) × 보호막 상한 + 자기 회복 ÷ 회복 주기]의 합. 계산은 C절 미리보기와 같은 F3 계산.
4. 꺼내기·바꾸기는 하지 않는다(가방이 차면 남은 것은 보관함에 남는다).

가중치 2개는 `Bag/bag_rules.json`의 `autoEquip`에 둔다. M6이 맞지 않으면 F4가 합쳐진 뒤 이 두 값과 E절 순서를 조정 대상으로 보고한다(게임 수치는 바꾸지 않는다).

## 데이터 스키마
위치는 F1과 같은 `Assets/Game/03.Resources/Resources/Data/`. 검사 명령은 F1의 `python3 -B ./tools/check_data.py`를 넓힌다.

| 위치 | 이름 | 필드 | 타입 | 키/기본값 | 쓰는 쪽 → 읽는 쪽 |
|---|---|---|---|---|---|
| JSON `Grade/grades.json` | 등급(1개 파일, 5등급) | `grades[]` {`id`, `name`, `color`, `mult`} | string, string, string(#rrggbb), float | 배열 순서 = 등급 번호 0~4. common 일반 #aeb9bd 1, uncommon 고급 #93d57c 1.4, rare 희귀 #76b9ef 2, epic 영웅 #c394f3 3, legendary 전설 #ffb75b 4.5. 검사: 1개 이상, `mult` > 0이고 앞 등급보다 큼, id 중복 없음 | 데이터 → `GradeRepository` → `BagPlacementInfo`·`BagRules.CanMerge`(마지막 번호)·`UI_BagView`·`UI_ItemInfoView` |
| JSON `Bag/bag_rules.json` | 가방 규칙(1개) | `bagWidth`, `bagHeight`, `startCell`{x, y}, `autoEquip`{`hpWeight`, `shieldWeight`} | int, int, object, object(float) | 4, 4, (0, 0), {0.05, 0.1}. 검사: 폭·높이 4(게임 기획서 S5 "4×4"), 시작 칸이 가방 안, 모든 아이템 `shape`가 회전 0에서 가방에 들어감, 모든 캐릭터 시작 아이템이 시작 칸에 들어감 | 데이터 → `BagRulesRepository` → `BagRules`·`BagService`·`AutoEquipPlanner` |
| JSON `Loot/loot_rules.json` | 전리품 규칙(1개) | `minCount`, `maxCount`, `earlyWaveMax`, `earlyBonusFactor`, `mergeWaveMin`, `mergeFactor`, `lootSeedOffset` | int, int, int, float, int, float, int | 3, 5, 3, 2, 4, 1.5, 7919. 검사: 1 ≤ min ≤ max, 배율 > 0 | 데이터 → `LootRulesRepository` → `LootService` |
| JSON `Item/<id>.json` | 아이템(F1·F3 칸 그대로) | `lootWeight` | float | 칸이 없으면 1(F3 대기열 24종은 칸 없이 넣어도 된다). 있으면 ≥ 0, 0이면 전리품에 안 나옴. 검사: `lootWeight` > 0인 아이템이 하나 이상 | 데이터 → `ItemRepository` → `LootService` |
| 세이브 | 없음 | | | 저장 없음(게임 기획서 A9) | |
| 서버 | 없음 | | | | |

- `Grade/`·`Bag/`·`Loot/`는 새 폴더다. `Item/` 폴더에는 아이템 파일만 둔다(F1·F3 `ItemRepository`가 폴더의 모든 파일을 아이템으로 읽기 때문).
- `--self-test`에 더하는 틀린 데이터 4종(각각 exit 1): 등급 `mult`가 줄어듦, `minCount` > `maxCount`, 모든 아이템 `lootWeight` 0, 가방에 안 들어가는 시작 아이템(시작 칸 (3, 3)).
- 실행 중 상태(저장 안 함): `BagModel` — 대원별 가방 목록, 보관함 목록, 다음 `Uid`, 마지막 지급 `Uid` 목록, `Version`. `LootService` — 이번 판 전리품 난수. `BagPresenter` — 집은 `Uid`, 집은 동안의 회전, 미리보기 대상, 상세 보기 여부.

## 클라 변경
경로는 `Assets/Game/` 기준. 어셈블리는 F1의 `Game`·`Game.Editor`·`Game.Tests.EditMode` 그대로. 새 이벤트 없음(가방 → 화면은 `BagModel.Version`, 화면 → 가방은 Presenter 직접 호출).

가방 (S5) — `02.Scripts/Features/Bag/`
- `Data/BagRulesData.cs`, `Data/GradeData.cs` — JSON 형
- `Registry/BagRulesRepository.cs`, `Registry/GradeRepository.cs` — 파일 읽기(F1 `WaveRepository`와 같은 방식)
- `Model/BagModel.cs`, `Model/BagItem.cs`, `Model/BagFailReason.cs`(enum: `Locked`·`OutOfBag`·`Overlap`·`CannotMerge`)
- `Utils/BagRules.cs` — A4 순수 함수(칸 모양 회전, `Fits`, `IsAdjacent`, `CanMerge`)
- `Service/BagService.cs` — A5·A6 조작과 장착 목록 맞추기(A2)
- `Service/BagPlacementInfo.cs` — F3 `IItemPlacementInfo` 구현(A3)
- `Service/BagPreviewService.cs` — C절 미리보기(복사본에 적용 → 이전·이후 능력치 줄)
- `Service/AutoEquipPlanner.cs` — E절 계획(순수, 봇과 시험이 씀)
- `Presenter/BagPresenter.cs` — C절 조작 흐름, `BagModel.Version`을 구독해 View마다 `UpdateUI(...)` 한 번
- `View/UI_BagView.cs`, `View/UI_BagCellView.cs`, `View/UI_StashView.cs`, `View/UI_StashItemView.cs`, `View/UI_ItemInfoView.cs`

전리품 (S6) — `02.Scripts/Features/Loot/`
- `Data/LootRulesData.cs`, `Registry/LootRulesRepository.cs`
- `Service/LootService.cs` — `ResetRun(seed)`·`GrantWaveLoot(completedWave)`, B2·B3

아이템
- `02.Scripts/Features/Item/Data/ItemData.cs` — `lootWeight` 칸(기본 1)

데이터
- `03.Resources/Resources/Data/Grade/grades.json`, `Bag/bag_rules.json`, `Loot/loot_rules.json` — 새 파일
- 아이템 파일은 고치지 않는다(`lootWeight` 없음 = 1)

F1·F3 파일에서 더하는 줄 — D절 표.

판정
- `02.Scripts/App/PlayableProbe.cs` — H절 멤버, 봇 `Equip`
- `Tools/playable/f5-bag-flows.json` — 새 흐름 경로
- `Tools/check_data.py` — 데이터 스키마 아래 검사

테스트 (`Tests/EditMode/`)
- `BagRulesTests.cs`, `BagServiceTests.cs`, `BagPlacementInfoTests.cs`, `LootServiceTests.cs`, `BagPreviewTests.cs`, `AutoEquipPlannerTests.cs`, `BagDataLoadTests.cs`. 시험용 아이템은 테스트 안에서 만든 `ItemData`를 쓰고, 데이터 시험만 실제 파일을 읽는다.

## 서버 변경과 API 계약
없음. 게임 기획서: 서버 없음, 저장 없음(A9). 전리품은 한 판 안의 아이템이고 재화·랭킹·치팅 방지가 걸리지 않는다.

## H. 판정 경로와 진입점
- 경로 파일 `Tools/playable/route.json`은 바꾸지 않는다(G1~G8 단계·확인 그대로). 이 기능만으로 통과하는 단계는 G2까지다(회귀).

| 단계 | 이 기능이 채우는 것 | 이 기능만으로 못 지나가는 이유 | 이 기능의 확인 |
|---|---|---|---|
| G3 | `StashCount`(보관함 3~5개) | 자동 이동 봇 `BotMode.Move`(F2)가 없으면 시작 장비로 웨이브 1을 버티지 못할 수 있다(M4) | 흐름 `PREP`(적 없는 판으로 정비까지 가서 보관함 3~5개) |
| G4 | `PlaceFirstStashItem()`, `IsPlacedItemActive` | G3을 지나야 이어진다(F2) | 흐름 `PLACE`·`NEXT` |
| G7 | 봇 `BotMode.Equip`(E절) | 이동 봇(F2), 몬스터 5종·웨이브 수치(F4) | 흐름 `AUTO`(적 없는 판에서 자동 장착이 돌며 10웨이브 끝까지). M6 수치 판정은 F2·F4가 합쳐진 뒤 경로 전체에서 |

- 진입점 `PlayableProbe`에 더하는 것(모두 Model·Presenter를 읽고 Presenter 처리 메서드를 부르기만 한다. `BagModel`을 직접 쓰지 않는다):
  - 읽기: `StashCount`, `BagItemCount(int member)`, `EquippedItemCount`(원정대 전체 가방 아이템 수), `MaxItemGrade`(가방·보관함 전체 가장 높은 등급 번호), `LastPlacedMember`(`PlaceFirstStashItem`이 놓은 대원, 없으면 −1), `IsPlacedItemActive` = `LastPlacedMember` ≥ 0 && F3 `HasBattleItem(LastPlacedMember, 놓은 아이템 id)` && F3 `BattleItemCount(LastPlacedMember) == BagItemCount(LastPlacedMember)`, `HasMergePair`(보관함 아이템과 `CanMerge`인 살아 있는 대원 가방·보관함 아이템이 있음).
  - 조작: `PlaceFirstStashItem()` = 보관함 첫 아이템을 살아 있는 대원 순 × 회전 0~3 × y·x 순으로 처음 빈 칸 놓기가 되는 곳에 `BagPresenter.OnItemPressed` → `OnItemDropped`로 놓음(없으면 경고 로그). `MergeFirstPair()` = `HasMergePair`의 첫 쌍을 같은 길로 합성. 
  - 봇 `Equip`: E절. 한 정비 화면에 한 번, `AutoNext`보다 먼저.
- 흐름 경로 `Tools/playable/f5-bag-flows.json`(같은 장면·진입점, `--route`로 지정, `time_scale` 1). 식은 `PlayableProbe.` 접두어를 줄였다.

| 단계 | do | wait / expect | capture |
|---|---|---|---|
| `L1` | — | `Screen == "Lobby"` | |
| `B1` | `UseRunSettings(<합성 시드>, 0f); PressDepart();` | `Screen == "Battle" && Wave == 1` / `StashCount == 0 && EquippedItemCount == 4` | |
| `PREP` | `UnityEngine.Time.timeScale = 20f;` | `Screen == "Prep" && CompletedWave == 1` / `StashCount >= 3 && StashCount <= 5` | `all`(V3 가방·보관함) |
| `PLACE` | `UnityEngine.Time.timeScale = 1f; PlaceFirstStashItem();` | `LastPlacedMember >= 0` / `BagItemCount(LastPlacedMember) == 2 && EquippedItemCount == 5` | 1장 |
| `NEXT` | `PressNextWave();` | `Screen == "Battle" && Wave == 2` / `IsPlacedItemActive` | |
| `PREP2` | `UnityEngine.Time.timeScale = 20f;` | `Screen == "Prep" && CompletedWave == 2` / `HasMergePair` | |
| `MERGE` | `UnityEngine.Time.timeScale = 1f; MergeFirstPair();` | `MaxItemGrade >= 1` | 1장 |
| `AUTO` | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.Equip \| BotMode.AutoNext);` | `Screen == "Result"` / `Outcome == "Win" && WavesSurvived == 10 && EquippedItemCount >= 8` | `all`(V4) |
| `RESTART` | `StopBot(); UnityEngine.Time.timeScale = 1f; UseRunSettings(1, 1f); PressToLobby(); PressDepart();` | `Screen == "Battle" && Wave == 1` / `StashCount == 0 && EquippedItemCount == 4 && HasOnlyStartItems && MaxItemGrade == 0` | |

- `<합성 시드>`는 구현이 "웨이브 2가 끝난 정비에서 `HasMergePair`가 참"인 시드를 찾아 파일에 적는다(F1 `<패배 배율>`, F3 `<부활 시드>`와 같은 방식. 적 수 배율 0이라 전투와 상관없이 같다). 아이템 파일 수가 바뀌어(대기열) 결과가 달라지면 그 값 하나만 다시 고른다.
- `AUTO`의 `EquippedItemCount >= 8`: 적 없는 판의 9번 지급(27개 이상)에서 봇이 놓은 아이템이 4개 이상이라는 뜻이다. 가방 64칸에 비해 낮게 잡아 대기열로 아이템 종류가 늘어도 맞는다.

## 수용 기준
확인 방법: 자동 테스트(EditMode·명령), 에디터(컴파일·에디터 명령으로 상태 확인), 플레이(플레이 모드 동작·화면), 사람.
EditMode 테스트 명령: F1과 같은 `unity test --platform editmode --assembly Game.Tests.EditMode`(실행기 형식은 구현 단계가 이 레포의 unity-cli 형식으로 적는다). 흐름 경로 명령: `python3 -B ./tools/playable.py --route tools/playable/f5-bag-flows.json`.

| 번호 | 기준 (확인할 수 있는 문장) | 확인 방법 | 확인 절차 |
|---|---|---|---|
| AC1 | 새 코드·데이터를 넣은 뒤 컴파일 에러가 0개이고, `IItemPlacementInfo` 등록이 `BagPlacementInfo`다 | 에디터 | `python3 -B ./tools/editor_ready.py` 후 Editor.log에 `error CS` 없음. `git grep -n "NoBagPlacementInfo" -- Assets/Game/02.Scripts/App` 0줄 |
| AC2 | 데이터 검사 명령이 지금 데이터에서 OK(exit 0)이고, F1·F3의 틀린 데이터와 이 기능의 틀린 데이터 4종(등급 배수 줄어듦, `minCount` > `maxCount`, 모든 `lootWeight` 0, 시작 칸 (3, 3))에서 각각 exit 1이다 | 자동 테스트 | `python3 -B ./tools/check_data.py`, `python3 -B ./tools/check_data.py --self-test` |
| AC3 | 등급 파일이 5등급(일반 1·고급 1.4·희귀 2·영웅 3·전설 4.5)이고, 가방 규칙·전리품 규칙 파일 값이 데이터 스키마 표와 같으며, 아이템 파일에 `lootWeight`가 없으면 1로 읽힌다 | 자동 테스트 | `BagDataLoadTests` |
| AC4 | 판정 경로 파일 `Tools/playable/route.json`이 바뀌지 않았고(G1~G8 단계·확인 그대로), 흐름 경로 파일의 형식이 맞다 | 자동 테스트 | `git diff --exit-code <시작 커밋> -- Tools/playable/route.json`, `python3 -B ./tools/playable.py --check`, `python3 -B ./tools/playable.py --check --route tools/playable/f5-bag-flows.json` |
| AC5 | 판정 경로 G2까지 통과 | 플레이 | `python3 -B ./tools/playable.py --until G2` |
| AC6 | F1·F3 흐름 경로가 이 기능을 넣은 뒤에도 통과한다(경로 파일 변경 없음) | 플레이 | `python3 -B ./tools/playable.py --route tools/playable/f1-flows.json`, `python3 -B ./tools/playable.py --route tools/playable/f3-combat-flows.json`, `git diff --exit-code <시작 커밋> -- Tools/playable/f1-flows.json Tools/playable/f3-combat-flows.json` |
| AC7 | 가방 규칙: 칸 (0,0)(1,0)(2,0)인 모양을 한 번 돌리면 (0,0)(0,1)(0,2), 네 번 돌리면 처음과 같다. 그 모양은 (2, 0)에 회전 0으로 놓을 수 없고(가방 밖) (1, 0)에는 놓을 수 있다. 다른 아이템과 한 칸이라도 겹치면 놓을 수 없고, 같은 아이템을 제자리 근처로 옮길 때 자기 칸은 겹침이 아니다. 옆 칸으로 붙은 두 아이템은 인접, 대각선으로만 닿은 두 아이템은 인접이 아니다 | 자동 테스트 | `BagRulesTests` |
| AC8 | 조작: 보관함 아이템을 빈 칸에 놓으면 보관함에서 빠지고 그 가방 목록 끝에 붙으며 그 대원 장착 목록이 가방 목록 id 순서와 같다. 다른 대원 가방으로 옮기면 두 대원의 장착 목록이 모두 맞게 바뀐다. 해제하면 보관함 끝으로 간다. 가방 밖·겹침·정비 화면이 아님은 거부되고 상태가 바뀌지 않는다 | 자동 테스트 | `BagServiceTests` |
| AC9 | 합성: 같은 id·같은 등급 두 개를 합치면 끌고 온 쪽이 사라지고 대상 등급이 +1이며 대상은 제자리·같은 순서다(가방 위로, 보관함 위로 둘 다). 다른 id·다른 등급·전설(마지막 등급)·자기 자신은 합성되지 않는다. 가방 칸 위에 놓은 아이템이 그 칸 아이템과 합성 가능하면 놓기 대신 합성된다 | 자동 테스트 | `BagServiceTests` |
| AC10 | 장비 잠금: 쓰러진 대원의 가방으로 넣기, 쓰러진 대원 가방의 아이템 옮기기·합성·해제, 쓰러진 대원 가방 아이템을 대상으로 한 합성이 모두 `Locked`로 거부된다. 부활한 뒤에는 된다. 판정은 `PartyService.CanChangeEquipment`를 부른다 | 자동 테스트 | `BagServiceTests` |
| AC11 | 배치 정보: 고급(1) 단궁을 든 레나의 단궁 피해가 13 × 1.4 × 1.2 = 21.84이다. 숫돌이 단궁과 옆 칸이면 레나 단궁 피해 13 × 1.2 × 1.2 = 18.72, 한 칸 떨어지면 13 × 1.2 × 1.06 = 16.536이다(F3 AC7과 같은 값을 실제 가방으로) | 자동 테스트 | `BagPlacementInfoTests`(실제 `BagService`·`BagPlacementInfo`와 F3 `EquipmentStatService`) |
| AC12 | 한 판 시작: 대원마다 가방에 시작 아이템 하나가 (0, 0)·회전 0·일반 등급으로 있고 보관함이 비어 있다. 로비로 갔다 다시 출발하면 지난 판의 가방·보관함이 남지 않는다 | 자동 테스트 | `BagServiceTests.ResetRun`, `BagServiceTests.ClearOnLobby` |
| AC13 | 전리품: 웨이브 1~9가 끝날 때마다 3~5개가 일반 등급으로 보관함에 들어오고 마지막 웨이브와 패배 때는 들어오지 않는다. 같은 시드면 지급 수와 종류가 같다. 후보는 아이템 파일에 있는 것뿐이고(시험용 아이템 3개로 돌리면 그 3개만 나옴) `lootWeight` 0인 아이템은 나오지 않는다 | 자동 테스트 | `LootServiceTests` |
| AC14 | 적응 가중치: 끝난 웨이브 3에서 단궁을 든 살아 있는 대원이 있으면 화살통(추가 효과 대상 투사체) 가중치가 2, 번개 완드만 있으면 1이고, 조건 없는 추가 효과 아이템(철제 방패)은 2, 추가 효과가 없는 아이템(장검)은 1이다. 끝난 웨이브 4에서 살아 있는 대원이 일반 등급 장검을 가지고 있으면 장검 가중치 1.5, 고급 장검만 있거나 그 대원이 쓰러졌으면 1이다. 고정 난수로 가중치 표 [1, 2, 1]에서 1만 번 뽑으면 가운데가 50% ± 2%다 | 자동 테스트 | `LootServiceTests.AdaptiveWeights`, `LootServiceTests.WeightedDrawDistribution` |
| AC15 | 미리보기: 숫돌을 레나 단궁 옆 칸에 놓는 미리보기는 상태를 바꾸지 않고 "[개선] 레나 단궁 피해 15.6 → 18.72"와 공격 증가 줄을 낸다. 겹치는 칸 미리보기는 실패와 `Overlap`을, 쓰러진 대원 가방 미리보기는 `Locked`를 낸다. 주기가 줄면 [개선]이다 | 자동 테스트 | `BagPreviewTests` |
| AC16 | 자동 장착: 보관함에 같은 일반 장검이 있고 브루노가 일반 장검을 들었으면 먼저 합성해 브루노 장검이 고급이 된다. 쓰러진 대원은 먼저 부활한다. 놓기는 원정대 점수가 가장 큰 곳을 고르고(같으면 앞 대원·작은 회전·y·x), 점수가 오르지 않는 아이템은 보관함에 남는다. 같은 입력이면 같은 계획이 나온다 | 자동 테스트 | `AutoEquipPlannerTests` |
| AC17 | 플레이에서 적 없는 판의 웨이브 1이 끝나면 정비 화면에 보관함 3~5개가 보이고(G3의 `StashCount`), 첫 보관함 아이템을 가방에 넣고 다음 웨이브를 누르면 웨이브 2에서 그 아이템이 그 대원의 발동 목록에 있다(G4의 `PlaceFirstStashItem`·`IsPlacedItemActive`) | 플레이 | 흐름 경로 `L1`·`B1`·`PREP`·`PLACE`·`NEXT` 단계 통과 |
| AC18 | 플레이에서 정비 화면의 합성이 실제로 등급을 올리고, 자동 장착 봇(`BotMode.Equip`, G7의 깃발)이 정비마다 보관함 아이템을 가방에 넣으며 10웨이브 끝까지 가고, 로비로 → 출발하면 새 판이 시작 아이템만·보관함 0·등급 0으로 시작한다. 진입점이 `BagModel`·`Phase`·`Outcome`을 직접 쓰지 않는다 | 플레이 | 흐름 경로 `PREP2`·`MERGE`·`AUTO`·`RESTART` 단계 통과. 진입점 코드에서 `BagModel`·`Phase`·`Outcome` 대입이 없는지 확인 |
| AC19 | 정비 화면에 대원별 4×4 가방 4개(아이템이 모양대로 칠해지고 등급 색 테두리), 보관함(새 전리품 "새" 표시), 아이템 설명(이름·등급·방식·수치·기본·추가 효과·조건·상태)과 수치 변화 미리보기(초록·빨강 칸, [개선]·[감소] 줄), 회전·선택 취소 버튼이 보인다 | 플레이 | 흐름 경로 `PREP`·`PLACE`·`MERGE` 캡처를 열어 항목별로 확인하고 본 캡처 목록을 보고에 적음 |
| AC20 | 탭(집기 → 대상 탭 미리보기 → 다시 탭 확정)과 드래그(끄는 동안 미리보기 → 놓으면 적용, 빈 곳에 놓으면 취소), 회전 버튼이 손으로 해도 같은 결과를 낸다 | 사람 | 에디터 Play에서 정비 화면을 손으로 조작(구현 단계는 `OnItemPressed`·`OnTargetTapped`·`OnItemDropped`를 흐름 경로로 부른 결과만 보고하고, 손 조작은 사용자에게 넘긴다) |
| AC21 | 노치 기기에서 가방·보관함·설명·버튼이 가려지지 않고, 16:9·19.5:9·21:9·4:3 가로 화면에서 가방 4개·보관함·설명 판넬이 잘리거나 겹치지 않는다 | 플레이 | 흐름 경로 `PREP`의 `capture: "all"` 4장을 열어 봄 + Device Simulator 노치 기기 1대(가로) 캡처 |

### 기존 테스트
이 기능이 건드리는 흐름의 기존 테스트는 F1·F3이 만드는 테스트뿐이다. 레포에는 아직 NUnit 테스트가 없고(F1 기획서 "기존 테스트", 2026-11-15) F1·F3 코드는 구현 중이라 이 기획서를 쓸 때 테스트 파일을 열어 보지 못했다. 그래서 어떤 테스트도 "그대로 통과"라고 적지 않는다. 아래는 F1·F3 기획서의 수용 기준 문장을 이 기획서의 새 흐름과 대조한 결과다. 구현 단계는 F1·F3 테스트 파일을 열어 마지막 기대를 이 표와 대조하고, 표에 없는 기대가 바뀌어야 하면 고치지 말고 멈춰 보고한다.

- 기대가 바뀌지 않을 것으로 본 것(구현이 열어서 확인): F1 `RunServiceTests`(웨이브·승패 — 전리품 지급은 정비 전환에 한 줄 더할 뿐 단계 전이를 바꾸지 않는다), F1 `PartyServiceTests`(장착 목록은 여전히 F1이 넣고 가방이 그것을 따라 만든다, A6), F3 `CombatStatTests`·`ItemActivationTests`·`PassiveEffectTests`·`CombatWaveTests`·`ReviveTests`(시험용 가짜 `IItemPlacementInfo`를 쓰므로 등록 교체와 무관), F3 `CombatDataLoadTests`(아이템 파일을 고치지 않는다), F1·F3 흐름 경로(AC6).
- 바뀔 수 있는 것: F1 `check_data.py --self-test`의 출력에 이 기능의 새 검사 줄이 늘어난다(틀린 데이터의 exit 1 기대는 그대로).

기대 변경 허용(이 목록에 있는 것만 구현 단계가 기대값을 고칠 수 있다):

| 테스트 | 지금 기대(끝 상태·값) | 새 기대 | 이유(기획서 절) |
|---|---|---|---|
| F1 `DataLoadTests`가 `Data/` 아래 폴더 목록이나 파일 수를 세는 시험이 있으면 그 기대 | F1·F3 폴더(`Character`·`Item`·`Wave`·`Run`·`Combat`)만 | `Grade`·`Bag`·`Loot` 폴더와 파일 3개가 더해짐 | 데이터 스키마 — 새 폴더 |

## 열린 질문
사람이 정할 것은 없다. 작은 갈림길은 아래 가정으로 정했다(승인 때 함께 확인).

- 구현 단계가 확인할 것(사람 질문 아님): F1·F3 테스트 파일을 열어 위 "기존 테스트" 대조를 마친다. F1 `RunModel`의 시드 읽기, F3 `EquipmentStatService`의 입력 모양을 확인하고 D절의 "필요할 때만" 줄을 정한다.

### 가정
| 번호 | 가정 | 틀리면 바뀌는 것 |
|---|---|---|
| BA1 | 보관함은 개수 제한이 없고 버리기·팔기가 없다(예전과 같음, 상점은 게임 기획서 12절 하지 않는 것) | A5, C절 보관함 |
| BA2 | 가중치는 모든 아이템 1(예전 값)이고 시작 아이템도 전리품으로 나온다. 가중치는 아이템 파일 `lootWeight` 칸(없으면 1) | B3·B4, 데이터 스키마 |
| BA3 | 적응 가중치(초반 ×2, 후반 합성 ×1.5)는 예전 규칙 그대로 켠다(게임 기획서 S6 "초반 강화·후반 합성 가중치") | B3, M6 |
| BA4 | 전리품 난수는 시드 + 7919로 따로 만든다(예전처럼 생성 난수와 분리) | B2, 판정 시드 |
| BA5 | 같은 가방 안에서 옮겨도 그 아이템은 목록 끝으로 가 장착 목록 순서가 바뀌고, F3 B7에 따라 그 대원의 쿨이 다음 웨이브에 0이 된다. 합성은 순서를 바꾸지 않아 쿨이 이어진다 | A5, F3 B7 |
| BA6 | 자동 장착 봇은 부활 → 합성 → 점수 최대 빈 칸 놓기만 하고 꺼내기·바꾸기는 하지 않는다. 점수 가중치는 HP 0.05, 보호막 0.1 | E절, M6 |
| BA7 | 정비 화면 가방 칸 출발값 22 가상 px(가방 88×88), 아이콘이 없으면 이름 2자 | C절, 아트 단계 |
| BA8 | 미리보기는 간략 3줄 + 상세 보기(스크롤). 예전의 7줄 쪽 넘기기는 버린다 | C절 |
| BA9 | 판정 흐름의 합성 확인은 적 없는 판의 고정 시드(`<합성 시드>`)로 웨이브 2 끝 보관함에 합성 쌍이 생기게 해 확인한다 | H절, AC18 |
