# F3 자동 전투와 아이템·캐릭터 데이터 기획서

- 상태: 초안
- 서버 필요: 아니오
- 관련 문서: [게임 기획서](../../game/gdd.md) F3(S2, S7, C2, C4), 판정 단계 G2(이 기능에서 통과), G3~G8(규칙이 지나가지만 F2·F4·F5가 있어야 통과 — H절). 앞선 기능 [F1 기획서](../f1-skeleton/plan.md)(승인, E·G·H절과 데이터 스키마). 참고 분석 문서 없음. 예전 판 코드 `Assets/DeadSurvivor/Runtime/DSGame.EquipmentCombat.cs`·`DSGame.Combat.cs`·`EquipmentRules.cs`·`DSGame.Inventory.cs`·`DSUI.BattleItems.cs`와 예전 값 `Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json`(읽기 전용, 규칙만 옮김). 긴 근거는 [notes.md](notes.md)
- 작업 규모: L2(데이터 스키마 확장, 대원 상태 소유권 추가) — 구현 전 설계 검토

## 목표
원정대 4명이 장착한 아이템이 저마다의 쿨타임대로 자동 발동해 가장 가까운 적을 치거나 다친 동료를 회복하고, 방어·보호막이 받는 피해를 줄인다. 쓰러진 동료는 발동이 멈추고 정비에서 무료로 부활한다.
F1의 임시 기본 무기·방어 공식·쓰러짐을 이 규칙으로 바꾸고, 아이템·캐릭터 데이터를 F1과 같은 파일·같은 키로 넓힌다. 아이템 32종 중 대표 8종을 넣고 나머지 24종은 콘텐츠 대기열이 파일 하나씩 채운다.

## 참고 분석 요약
예전 판(3D, 같은 게임의 이전 구현) 코드에서 옮길 규칙. 모두 관찰이며 출처는 위 경로다(줄 번호는 notes.md).

- 아이템마다 쿨 시계가 따로 돈다. 쿨이 0 이하이고 대상이 있을 때만 발동하고, 발동하면 쿨 = 간격 ÷ (1 + 가속). 대상이 없으면 쿨이 0 이하로 남아 다음 걸음에 다시 찾는다 (관찰, `TickAction`)
- 피해 = 아이템 피해 × 등급 배수 × 캐릭터 공격 배수 × (1 + 공격 증가). 회복량은 공격 배수·공격 증가를 곱하지 않는다. 사거리 = 사거리 × 42 × (1 + 사거리 증가) (관찰, `WeaponPower`·`TickAction`)
- 발동 방식 7가지: 근접 부채꼴(반각 50°)·휩쓸기(반각 90°)·투사체(속도 440, 관통, 갈래)·폭발 투사체(반경 75 × (1 + 반경 증가))·연쇄(3 + 추가 연쇄 번, 다음 적은 150 안)·단일 회복(HP 비율이 가장 낮은 다친 동료)·범위 회복(사거리 안 다친 동료 모두) (관찰, `TickAction`·`Simulate` 투사체 부분)
- 효과는 기본 효과(늘 적용)와 추가 효과로 나뉜다. 추가 효과에 대상 방식이나 인접 조건이 있으면 인접한 맞는 발동 아이템에만, 없으면 늘 적용한다. 방어 상한 0.8, 가속 상한 1, 최대 HP = 캐릭터 HP + HP 증가 (관찰, `EquipmentRules.Stats`·`BonusApplies`)
- 지속 효과: 회복 증가는 3 ÷ (1 + 가속)초마다 자기 회복, 나눔은 다른 동료가 가장 회복이 빠른 나눔 제공자의 회복을 같은 주기로 받음, 보호막은 8초마다 채움, 시작 보호막은 웨이브 시작에 채움, 파동은 효과 간격마다 주변 적에게 피해 (관찰, `TickEquipment`·`RefreshProviders`·`NextWave`)
- 받는 피해 = 피해 × (1 − 방어), 그 뒤 보호막이 먼저 흡수 (관찰, `Damage`)
- 쓰러진 대원은 아이템이 돌지 않고(쿨 멈춤), 회복·나눔 대상에서 빠지며, 정비에서 장비를 바꿀 수 없다. 정비에서 부활하면 HP 최대, 보호막·시계 0, 아이템 쿨 0. 부활은 비용·횟수 제한이 없다 (관찰, `Simulate`의 `hp <= 0` 건너뜀, `EquipmentRules.Drop`·`Merge`·`Unequip`의 "사망자 장비 잠금", `Revive`)
- 전투 중 아이템 칸 상태 글: 비활성(쓰러짐) / 지속 효과(쿨 없음) / 발동!(0.18초) / n.n초 / 준비, 쿨타임 막대 = 남은 쿨 ÷ 마지막 주기 (관찰, `DSUI.BattleItems.cs`)

## 적용안

### 가져올 것 / 버릴 것 / 바꿀 것
- 가져올 것: 위 계산식·발동 방식·지속 효과·보호막·쓰러짐·부활 규칙과 수치 전부(게임 기획서 A1·A5와 사용자 "예전 값으로 시작"). 아이템 32종의 이름·발동 방식·칸 모양·효과 값(아래 D절 대표 8종과 C절 대기열 24종)
- 버릴 것: 맵 시야 검사(`MapLine`, A10 큰 장애물 없음), 아이템을 만들 때 쿨에 넣던 0~0.2초 난수(새 판의 결정성을 위해 0으로 시작), 상태 문자열(`"활성"`·`"비활성"`), 예전 `DSGame` 한 덩어리 구조, 효과 문자열 색
- 바꿀 것: 계산은 Service, 상태는 Model, 하단 목록과 부활은 Presenter + `UI_` View로 나눈다. 규칙 상수(42, 50°, 440 …)는 데이터 파일 `Combat/combat_rules.json`으로 뺀다. 아이템의 등급 배수와 인접 여부는 가방(F5)이 알려 주는 자리(B절 `IItemPlacementInfo`)로 받는다

### 기존 시스템과의 관계
확장. F1이 만든 `Character/<id>.json`·`Item/<id>.json`·`CharacterRepository`·`ItemRepository`·`PartyModel`·`PartyService`·`RunService.Step`의 ④번 자리(기본 무기)를 넓히거나 바꾼다. 새 전투 상태(아이템 칸별 쿨·투사체)는 `Features/Combat/`에 새로 만든다. F1 코드는 아직 구현 중이라 이 기획서는 F1 기획서의 이름·구조를 기준으로 썼다. 구현은 실제 F1 코드의 이름에 맞추고, 다르면 핸드오프에 적는다.

### A. F1 임시 규칙에서 바뀌는 것 (F1 E절)

| F1 임시 규칙 | F3 규칙 | 지우는 것 |
|---|---|---|
| 기본 무기 하나(`BasicWeaponService`, 13·0.7초·336) | 장착 목록의 아이템마다 B·C절 규칙으로 발동 | `BasicWeaponService.cs`, `placeholder_combat.json`의 `weapon` 칸과 `PlaceholderCombatData`의 그 필드, `PartyModel`의 기본 무기용 멤버 쿨 시계 |
| 받는 피해 = 피해 × (1 − 캐릭터 `def`) | 받는 피해 = 피해 × (1 − 유효 방어), 그 뒤 보호막이 먼저 흡수(B4) | — |
| HP 0 = 쓰러짐: 공격하지 않고 표적에서 빠짐 | + 발동·쿨 정지, 회복·나눔 대상 제외, 장비 잠금, 정비 무료 부활(E절) | — |

- `placeholder_combat.json`은 이 기능이 `weapon` 칸만 지운다. 다른 칸(`memberSpots` F2, 적·생성 F4)이 남아 있으면 파일을 지우지 않는다(마지막으로 칸을 지우는 기능이 파일을 지운다).
- `RunService.Step` 순서(F1 C절)의 ④ "기본 무기"가 "④ 전투 진행 `CombatService.Step(dt)`: 대원 순서대로 지속 효과 → 장착 목록 순서대로 발동 → 투사체 이동·명중"으로 바뀐다. ①②③⑤⑥⑦은 그대로다.
- 적에게 주는 피해는 F1 기본 무기가 쓰던 적 피해 처리(`MonsterService`의 피해 적용, 처치 수 증가 포함)를 그대로 부른다. 투사체 명중에 필요한 적 반지름은 적 목록이 주는 값을 쓴다(F1은 임시 적 `enemy.radius` 하나, F4가 유형별). F1 `MonsterService`에 적 번호 → 반지름 읽기가 없으면 그 읽기 하나를 더한다.

### B. 능력치와 발동 (S2)

B1. **장착 목록과 아이템 칸**: 대원의 장착 목록은 F1처럼 아이템 id 목록이다(F5가 가방에서 만든다). 목록의 순서 번호(0부터)가 "아이템 칸"이고, 칸마다 남은 쿨·마지막 주기·발동 표시 시간을 `CombatModel`이 가진다. 같은 아이템을 두 개 들면 칸이 둘이다.

B2. **배치 정보 자리(F5가 채움)**: 계산은 `IItemPlacementInfo`(Interface)에서 두 값을 받는다 — `GetGradeMultiplier(memberIndex, slotIndex)`(등급 배수), `IsAdjacent(memberIndex, slotA, slotB)`(가방에서 붙어 있는가). 이 기능은 기본 구현 `NoBagPlacementInfo`(배수 1, 인접 없음)를 등록한다. F5는 등록 한 줄을 가방 구현으로 바꾼다. 계산식은 F5에서 바뀌지 않는다.

B3. **능력치 계산** `EquipmentStatService` — 대원 하나와 "어느 발동 아이템을 위한 계산인가"(없으면 대원 전체)를 받아 능력치 묶음을 낸다.
- 장착한 모든 아이템의 기본 효과 값 × 등급 배수를 효과 종류별로 더한다.
- 추가 효과: `bonusTarget`이 비어 있고 `bonusNeedsAdjacent`가 거짓이면 늘 적용. 아니면 그 계산의 발동 아이템이 (자기 자신이 아니고) 대상 방식에 맞으며(`attack` = 회복 두 가지를 뺀 발동 방식 전부, 그 밖에는 같은 방식), `bonusNeedsAdjacent`면 B2가 인접이라고 할 때만 적용. 대원 전체 계산에서는 조건 있는 추가 효과를 넣지 않는다.
- 효과 종류: 더하기 — `attack`(공격 증가), `def`(방어), `hp`(최대 HP), `heal`(자기 회복량), `haste`(가속), `range`(사거리 증가), `radius`(폭발 반경 증가). 큰 값 하나 — `shield`(보호막 상한·주기 채움 값), `startShield`(시작 보호막). 정수·큰 값 하나·등급 배수 안 곱함 — `pierce`(관통), `extraShots`(갈래), `chain`(추가 연쇄). 동작 — `pulse`(파동), `share`(나눔).
- 마무리: 유효 방어 = min(`defCap` 0.8, 캐릭터 `def` + 방어 합), 가속 = min(`hasteCap` 1, 가속 합), 최대 HP = 캐릭터 `hp` + HP 합, 보호막 상한 = max(`shield`, `startShield`).

B4. **피해·회복·주기**
- 발동 피해 = `power` × 등급 배수 × 캐릭터 `atk` × (1 + 공격 증가). 회복 방식(`heal`·`healArea`)의 회복량 = `power` × 등급 배수.
- 주기 = `interval` ÷ (1 + 가속). 사거리 = `range` × `rangeUnit`(42) × (1 + 사거리 증가). 모두 그 발동 아이템을 위한 계산(B3)으로 낸다.
- 받는 피해(F1 `PartyService` 피해 적용을 바꿈): 받음 = 피해 × (1 − 유효 방어), 흡수 = min(보호막, 받음), 보호막 −= 흡수, HP = max(0, HP − (받음 − 흡수)). HP가 0이 되면 쓰러짐(E절).
- 회복(`PartyService`에 더함): 쓰러지지 않은 대원만, HP = min(최대 HP, HP + 양).

B5. **발동 아이템 한 칸의 걸음** (쓰러지지 않은 대원만)
1. 남은 쿨 −= dt. 0보다 크면 끝.
2. 대상을 찾는다. 공격 방식: 사거리 안 살아 있는 적 중 가장 가까운 적(같으면 먼저 생긴 적). 회복 방식: 사거리 안(자기 포함) 쓰러지지 않고 HP < 최대 HP인 동료를 HP 비율 오름차순(같으면 편성 순서).
3. 대상이 없으면 쿨을 그대로 두고 끝(다음 걸음에 다시 찾는다).
4. 남은 쿨 = 마지막 주기 = 주기, 발동 표시 = `firedSeconds`(0.18), 이번 판 발동 수 +1.
5. 방식별 동작:

| 발동 방식 `mode` | 동작 |
|---|---|
| `melee` 근접 | 대상 쪽 방향에서 반각 `meleeHalfAngle`(50°) 안, 사거리 안 살아 있는 적 모두에게 피해 |
| `sweep` 휩쓸기 | 같은 규칙, 반각 `sweepHalfAngle`(90°) |
| `projectile` 투사체 | 1 + 갈래 발을 대상 방향 기준 (n − 갈래 × 0.5) × `projectileSpread`(0.12 rad) 각도로 쏜다. 속도 `projectileSpeed`(440), 날아간 거리가 사거리면 사라짐. 한 걸음의 이동 선분에서 적 반지름 안에 든 적에게 한 번씩 피해, 맞힌 수 > 관통이면 사라짐 |
| `explosion` 폭발 | 대상 위치로 투사체 하나. 대상 위치까지 거리 < 이번 걸음 이동 + `explosionTriggerGap`(5)이거나 사거리를 다 날면 터져, 반경 `explosionRadius`(75) × (1 + 반경 증가) 안 적 모두에게 피해 |
| `chain` 연쇄 | 대상부터 `chainBaseHits`(3) + 추가 연쇄 번까지, 마지막 맞은 적에서 `chainHopRange`(150) 안 아직 안 맞은 살아 있는 가장 가까운 적으로 옮겨 가며 피해 |
| `heal` 단일 회복 | 대상 목록의 첫 동료 회복 |
| `healArea` 범위 회복 | 대상 목록의 동료 모두 회복 |
| `passive` 지속 | 발동 없음(B6의 효과만) |

B6. **지속 효과의 걸음** (쓰러지지 않은 대원마다, B5보다 먼저)
- 자기 회복: 회복 증가 > 0이면 회복 시계 += dt, 시계 ≥ `healPeriod`(3) ÷ (1 + 가속)마다 자기 회복.
- 나눔: 받는 대원마다 "나눔 제공자" = 나눔 효과가 적용되는 다른 살아 있는 대원 중 회복 증가 ÷ 자기 회복 주기가 가장 큰 대원. 제공자나 그 주기가 바뀌면 나눔 시계 0. 나눔 시계가 제공자 주기에 닿을 때마다 제공자의 회복 증가만큼 회복. 제공자는 걸음마다 다시 고른다.
- 보호막: 보호막 = min(보호막, 보호막 상한). 주기 채움 값(추가 효과 `shield`가 적용되는 아이템 값 × 등급 배수 중 큰 값) > 0이면 보호막 시계 += dt, `shieldPeriod`(8)에 닿을 때마다 보호막 = max(보호막, 채움 값). 0이면 보호막 시계 0.
- 파동: 추가 효과 `pulse`가 적용되는 아이템 칸마다 남은 쿨 −= dt, 0 이하면 쿨 = 그 아이템 `effectInterval`, 대원 둘레 `effectRadius` 안 살아 있는 적 모두에게 `bonusValue` × 등급 배수 피해(공격 배수 안 곱함). 대상이 없어도 돈다. 이 칸은 하단 목록에서 쿨이 있는 칸으로 보인다.

B7. **판·웨이브 시작** (`RunService`가 부르는 자리에 한 줄씩)
- 한 판 시작(`StartRun`): 대원마다 아이템 칸을 장착 목록으로 새로 만들고(남은 쿨 0), 이번 판 발동 수·부활 기록 0, 최대 HP를 B3으로 계산해 HP = 최대 HP.
- 웨이브를 열 때(웨이브 1 포함, `NextWave`): 장착 목록이 지난 웨이브와 다른 대원은 아이템 칸을 새로 만들고(남은 쿨 0), 같은 대원은 남은 쿨을 이어 간다. 능력치(B3)를 다시 계산해 `PartyModel`의 유효 방어·최대 HP를 고치고 HP = min(HP, 최대 HP). 살아 있는 대원의 보호막 = 시작 보호막, 회복·보호막·나눔 시계 0. 투사체를 비운다.
- 웨이브가 끝날 때와 로비로 갈 때 투사체를 비운다(F1이 적을 비우는 자리에 한 줄).
- 웨이브 사이에 HP는 회복되지 않는다(R2: HP는 회복·보호막·부활로만 돌아온다).

B8. **결정성**: 이 기능은 난수를 쓰지 않는다. 같은 시드면 F1 고정 간격(`simStep`)과 배속·시간 배율에 상관없이 처치 수·HP가 같아야 한다(F1 AC11을 이 기능의 전투로도 지킨다).

### C. 아이템 데이터 (C2)

C1. 이 기능이 넣는 대표 8종: 시작 아이템 4종(F1 파일에 칸 추가) + 발동 방식마다 하나 이상(`sweep` 도끼, `chain` 완드, `healArea` 향로, `passive` 철제 방패). 수치는 예전 값.

| id | 이름 | `mode` | `shape` (x, y) | `power` | `interval` | `range` | 기본 효과 | 추가 효과(조건) |
|---|---|---|---|---|---|---|---|---|
| sword | 장검 | melee | (0,0)(1,0)(2,0) | 22 | 1.0 | 3 | — | — |
| axe | 전투 도끼 | sweep | (0,0)(0,1)(0,2)(1,2) | 35 | 1.8 | 3.5 | — | — |
| shortbow | 단궁 | projectile | (0,0)(1,0)(2,0) | 13 | 0.7 | 8 | — | — |
| firestaff | 화염 지팡이 | explosion | (0,0)(0,1)(0,2)(1,2) | 24 | 1.7 | 7 | — | — |
| wand | 번개 완드 | chain | (0,0)(1,0)(2,0) | 17 | 1.4 | 7 | — | — |
| relic | 치유 성물 | heal | (0,0)(1,0)(2,0)(1,1) | 12 | 2 | 6 | — | — |
| censer | 성스러운 향로 | healArea | (0,0)(0,1)(0,2)(1,2) | 6 | 2.5 | 4 | — | — |
| ironshield | 철제 방패 | passive | (0,0)(1,0)(0,1)(1,1) | — | — | — | def 0.08 | def 0.08(조건 없음) |

C2. **콘텐츠 대기열이 넣을 나머지 24종**(이 기능이 끝난 뒤 파일 하나에 아이템 하나씩. 코드 수정 없이 데이터만). 값은 예전 값이고 모두 B절 규칙으로 동작한다. "인접"은 `bonusNeedsAdjacent: true`, 괄호 안 대상은 `bonusTarget`.

| id | 이름 | `mode` | `shape` (x, y) | 피해·간격·사거리 / 기본 효과 | 추가 효과(조건) |
|---|---|---|---|---|---|
| crossbow | 쇠뇌 | projectile | (0,0)(1,0)(2,0)(1,1) | 32 · 1.6 · 10 | — |
| chainmail | 쇠사슬 갑옷 | passive | (0,0)(1,0)(2,0)(1,1) | hp 45 | hp 30 |
| helmet | 철제 투구 | passive | (0,0)(1,0) | def 0.03 | startShield 15 |
| whetstone | 숫돌 | passive | (0,0) | attack 0.06 | attack 0.14(attack, 인접) |
| gauntlet | 전투 장갑 | passive | (0,0)(0,1)(1,1) | haste 0.08 | haste 0.18(attack, 인접) |
| banner | 수호 깃발 | passive | (0,0)(0,1)(0,2)(1,2) | def 0.04 | shield 20 |
| quiver | 화살통 | passive | (0,0)(1,0) | haste 0.05 | haste 0.15(projectile, 인접) |
| pierce | 관통 화살촉 | passive | (0,0)(1,0) | attack 0.05 | pierce 1(projectile, 인접) |
| split | 갈래 화살촉 | passive | (0,0)(0,1)(1,1) | attack 0.05 | extraShots 1(projectile, 인접) |
| feather | 매의 깃털 | passive | (0,0) | range 0.08 | range 0.2(projectile, 인접) |
| scope | 사냥꾼의 조준경 | passive | (0,0)(1,0)(2,0) | attack 0.08 | attack 0.22(projectile, 인접) |
| leather | 가죽 조끼 | passive | (0,0)(0,1)(1,1) | hp 30 | hp 20 |
| crystal | 마력 수정 | passive | (0,0) | attack 0.06 | attack 0.14(attack, 인접) |
| spellbook | 마법서 | passive | (0,0)(1,0)(0,1)(1,1) | attack 0.06 | radius 0.4(explosion, 인접) |
| ring | 집중의 반지 | passive | (0,0) | haste 0.04 | haste 0.1(attack, 인접) |
| hat | 마법사 모자 | passive | (0,0)(1,0)(2,0)(1,1) | attack 0.05 | chain 2(chain, 인접) |
| orb | 수정 구슬 | passive | (0,0)(1,0)(0,1)(1,1) | attack 0.08 | pulse 10(`effectInterval` 3, `effectRadius` 100) |
| robe | 마법 로브 | passive | (0,0)(0,1)(1,1) | def 0.04 | shield 18 |
| scripture | 성전 | passive | (0,0)(1,0)(0,1)(1,1) | heal 4.8 | share 1 |
| bell | 축복의 종 | passive | (0,0)(0,1)(1,1) | heal 3.3 | share 1 |
| rosary | 기도 묵주 | passive | (0,0)(1,0) | heal 2 | share 1 |
| amulet | 수호 부적 | passive | (0,0) | heal 1 | share 1 |
| vestment | 성직자의 의복 | passive | (0,0)(1,0)(2,0) | heal 3 | share 1 |
| lantern | 성역의 등불 | passive | (0,0)(0,1)(0,2)(1,2) | heal 4.4 | share 1 |

- 대기열 항목의 완료 조건 출발값: 파일 하나 추가, `python3 -B ./tools/check_data.py` OK, 위 표의 값과 같음, EditMode 테스트 통과. 아이콘(`icon`)은 비워 두고 아트 대기열(아트 기준서 3.3 "아이템 아이콘 32")이 채운다.
- 전리품 가중치(어느 아이템이 얼마나 나오는가, A2)는 F5(S6)가 정한다. 이 기능의 데이터에는 가중치 칸이 없다.

### D. 캐릭터 데이터 (C4)
- 4명(브루노·레나·미라·볼트)은 F1이 만든 파일 그대로이고, 이 기능은 값을 바꾸지 않고 `sprite` 칸(그림 키, 비면 F1 임시 도형)만 더한다. 그림을 그리고 이 키를 읽는 일은 아트 단계(`/2d-art-produce`, 기준서 3.3 캐릭터 행)가 한다.
- `hp`·`atk`·`def`가 이 기능에서 의미를 얻는다: `hp` = 최대 HP의 바탕, `atk` = 공격 피해 배수(회복에는 안 곱함), `def` = 유효 방어의 바탕(B3).
- 시작 아이템은 발동 방식이 `passive`가 아니어야 한다(데이터 검사).

### E. 쓰러짐과 무료 부활 (S7)
- 쓰러짐(HP 0): 그 대원의 아이템 칸은 걸음이 돌지 않아 남은 쿨이 멈춘다. 회복 대상·나눔 제공자·나눔 받는 대원에서 빠진다. 적의 표적에서 빠지는 것은 F1 그대로. 이미 날아가는 투사체는 계속 난다.
- 장비 잠금: `PartyService.CanChangeEquipment(memberIndex)` = 쓰러지지 않음. 정비에서 장착 목록을 바꾸는 쪽(F5)이 바꾸기 전에 부른다. 이 기능은 규칙과 시험만 만든다(가방 조작은 F5).
- 무료 부활 `ReviveService.Revive(memberIndex)`: 지금 화면이 정비(Prep)이고 그 대원이 쓰러져 있을 때만 된다(아니면 경고 로그를 남기고 거짓을 돌려준다). 되면 HP = 최대 HP, 보호막 0, 회복·보호막·나눔 시계 0, 그 대원의 모든 아이템 칸 남은 쿨·마지막 주기·발동 표시 0(쿨타임 초기화), 이번 판 부활 수 +1, 마지막 부활 대원 기록. 비용과 횟수 제한은 없다.
- 부활하지 않고 다음 웨이브를 누르면 그 대원은 쓰러진 채 다음 웨이브에 들어간다(살아 있는 대원이 있으면 판은 이어진다, F1 규칙).

### F. 화면 (V2 하단 목록, V3 부활)

| 화면 | F1이 넣은 것 | 이 기능이 더하는 것 |
|---|---|---|
| V2 전투 하단 | 캐릭터별 HP 막대와 아이템 목록 틀(4열 최대 16칸, 시작 아이템 이름 1칸) | 칸마다 `UI_BattleItemView`: 아이콘(`icon`이 비면 이름, 칸에 안 들어가면 줄임), 상태 글, 쿨타임 막대. 칸 수 = 그 대원의 아이템 칸 수(16 넘는 칸은 F5가 만들지 않는다) |
| V3 정비 | 판넬, 웨이브 n 완료, 대원 4명 자리(이름·HP), 다음 웨이브 버튼 | 쓰러진 대원 자리에 "쓰러짐" 표시와 부활 버튼(`UI_ReviveView`). 살아 있는 대원 자리에는 보이지 않는다. 누르면 그 자리 HP가 최대로 바뀌고 버튼이 사라진다 |

- 상태 글(`BattleItemState`, 위가 먼저): 쓰러짐 → `Disabled` "비활성"(아이콘 어둡게, 막대 숨김) / 쿨 없는 칸(`passive`이고 `pulse`가 없음) → `Passive` "지속 효과"(막대 숨김) / 발동 표시 중 → `Fired` "발동!" / 남은 쿨 > 0 → `Cooling` "n.n초" / 그 밖 → `Ready` "준비".
- 쿨타임 막대 = 남은 쿨 ÷ 마지막 주기(마지막 주기가 0이면 지금 주기), 0~1로 자름. 준비 색과 쿨 색을 나눈다.
- `BattleItemListPresenter`(`ITickable`)가 전투 화면일 때 매 프레임 `CombatModel`을 읽어 칸마다 `UpdateUI(...)`를 한 번 부른다. 부활은 `RevivePresenter`가 정비 화면이 열릴 때와 부활 뒤 `UI_ReviveView.UpdateUI(...)`를 부르고, 버튼 처리 `OnReviveClicked(memberIndex)` → `ReviveService.Revive`. 부활 뒤 대원 자리의 HP 표시가 바로 바뀌게 F1 정비 화면이 HP를 다시 그리는 길에 잇는다(구현이 F1 코드에서 그 길을 확인해 한 줄 잇는다).
- 전투 월드의 발동 표시는 임시 도형: 투사체는 작은 원(풀에서 꺼냄), 근접·휩쓸기·폭발·연쇄·회복은 0.25초짜리 고리 하나. F1 `PlaceholderSpriteFactory`의 도형을 쓰고 정렬 순서는 효과 겹(50, F1 F절). 효과 그림은 아트 단계(기준서 3.3 효과 10), 피해 숫자·피격 연출은 F6(S9)다.
- 보호막 양은 이 기능에서 화면에 보이지 않는다(V2 칸 목록에 없음). 판정 진입점으로만 읽는다.

### G. 화면과 기능의 경계 (뒤 기능과 겹치는 파일)
이 기능은 아래 파일에 줄·칸만 더한다. F2·F4·F5·F6 구현이 나란히 돌고 나중에 합쳐지므로 그 밖의 줄은 건드리지 않는다.

| 파일 | 이 기능이 더하거나 바꾸는 줄·칸 |
|---|---|
| `02.Scripts/App/GameSceneLifetimeScope.cs` | 등록 줄: `CombatRulesRepository`, `CombatModel`, `IItemPlacementInfo` → `NoBagPlacementInfo`, `EquipmentStatService`, `CombatService`, `ReviveService`, `ProjectileViewFactory`, `ProjectileViewRegistry`, 진입점 `BattleItemListPresenter`·`RevivePresenter`·`CombatWorldPresenter`. `BasicWeaponService` 등록 줄 삭제 |
| `02.Scripts/App/PlayableProbe.cs` | 읽기·조작 멤버(H절 목록)만 더함. 봇(`PlayableBot`)은 바꾸지 않음 |
| `01.Scene/Main.unity` | 하단 대원 목록마다 `UI_BattleItemView` 16칸(F1의 이름 1칸 대신), 정비 대원 자리마다 `UI_ReviveView`, 투사체·고리 풀 부모 오브젝트 |
| HUD View `UI_MemberStatusView.cs` | `[SerializeField] UI_BattleItemView[]` 칸 하나와 그 칸들을 돌려주는 읽기. F1의 이름 1칸 표시 삭제 |
| `UI_PrepView.cs`(대원 자리) | `[SerializeField] UI_ReviveView[]` 칸 하나 |
| `Run/placeholder_combat.json` | `weapon` 칸 삭제만 |
| `RunService.cs` | Step ④의 호출 대상 교체, B7의 시작·끝 한 줄씩 |
| `PartyModel.cs`·`PartyService.cs` | 대원별 `Shield`·유효 방어 칸 추가, 기본 무기 쿨 시계 삭제, 피해 공식 교체, `Heal`·`CanChangeEquipment` 추가 |
| `Tools/check_data.py` | 아이템·캐릭터 새 칸과 `combat_rules.json` 검사, `--self-test` 틀린 데이터 5종 추가 |

### H. 판정 경로와 진입점
- 경로 파일 `Tools/playable/route.json`은 바꾸지 않는다(G1~G8 단계·확인 그대로). 이 기능만으로 통과하는 단계는 G2까지다.

| 단계 | 이 기능이 지나가는 곳 | 이 기능만으로 못 지나가는 이유 | 이 기능의 확인 |
|---|---|---|---|
| G2 | 전투가 아이템 발동으로 돈다 | — (통과) | AC5 |
| G3 | 시작 장비로 웨이브 1을 버팀(M4) | 자동 이동 봇(F2), 보관함 수 `StashCount`(F5) | 흐름 `B1`·`FIRE`·`PREP`(실제 아이템으로 웨이브 1이 정비까지 감) |
| G4 | "넣은 아이템이 그 캐릭터의 발동 목록에 있다" — 발동 목록 = 이 기능의 아이템 칸 | 보관함·가방 넣기(F5) | 진입점 `HasBattleItem`을 이 기능이 채우고 흐름 `B1`이 시작 아이템으로 확인. F5의 `IsPlacedItemActive`가 이것을 부른다 |
| G5 | 실제 전투 피해로 전멸·패배 | G4를 지나야 이어짐(F5) | 흐름 `DOWN`·`END`, F1 흐름 `LOSE`(AC6) |
| G6·G8 | 새 판에서 HP·쿨·부활 기록이 처음으로 | G5·G7을 지나야 이어짐 | 흐름 `RESTART` |
| G7 | 실제 전투로 10웨이브 승리(M1·M6) | 이동 봇(F2), 자동 장착 봇(F5), 몬스터 수치(F4) | 수치 판정은 F4·F5 뒤 경로 전체에서 |

- 진입점 `PlayableProbe`에 이 기능이 더하는 것(모두 Model·Presenter를 읽고 부르기만 한다):
  - 읽기: `MemberHp(int)`, `MemberMaxHp(int)`, `MemberShield(int)`, `IsMemberAlive(int)`, `BattleItemCount(int)`, `HasBattleItem(int member, string itemId)`(전투 아이템 칸에 그 id가 있는가), `ItemState(int member, int slot)`(`BattleItemState` 이름), `FireCount(string itemId)`(이번 판 그 아이템의 원정대 전체 발동 수), `FirstDownMember`(쓰러진 대원 중 가장 앞 번호, 없으면 −1), `CanRevive(int)`, `LastRevivedMember`(없으면 −1).
  - 조작: `PressRevive(int member)` = `RevivePresenter.OnReviveClicked`(부활 버튼 `onClick`과 같은 길).
- 흐름 경로 `Tools/playable/f3-combat-flows.json`(같은 장면·진입점, `--route`로 지정, `time_scale` 1). 식은 `PlayableProbe.` 접두어를 줄였다.

| 단계 | do | wait / expect | capture |
|---|---|---|---|
| `L1` | — | `Screen == "Lobby"` | |
| `B1` | `UseRunSettings(<부활 시드>, <부활 배율>); PressDepart();` | `Screen == "Battle" && Wave == 1` / 대원 0~3이 각각 `BattleItemCount(i) == 1`, `HasBattleItem(0, "sword") && HasBattleItem(1, "shortbow") && HasBattleItem(2, "firestaff") && HasBattleItem(3, "relic")`, `ItemState(i, 0) == "Ready"` | |
| `FIRE` | — | `FireCount("sword") >= 1 && FireCount("shortbow") >= 1 && FireCount("firestaff") >= 1` | `all`(V2 하단 목록) |
| `DOWN` | — | `AliveCount <= 3` / `FireCount("relic") >= 1 && ItemState(FirstDownMember, 0) == "Disabled"` | 1장 |
| `PREP` | `UnityEngine.Time.timeScale = 20f;` | `Screen == "Prep" && CompletedWave == 1` / `AliveCount >= 1 && AliveCount <= 3 && CanRevive(FirstDownMember)` | `all`(V3 부활 버튼) |
| `REVIVE` | `UnityEngine.Time.timeScale = 1f;` 대원 0~3 중 `CanRevive(i)`인 대원마다 `PressRevive(i);` | `AliveCount == 4` / `MemberHp(LastRevivedMember) == MemberMaxHp(LastRevivedMember) && ItemState(LastRevivedMember, 0) == "Ready" && !CanRevive(LastRevivedMember)` | 1장 |
| `NEXT` | `PressNextWave();` | `Screen == "Battle" && Wave == 2` / `AliveCount == 4 && ItemState(LastRevivedMember, 0) != "Disabled"` | |
| `END` | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.AutoNext);` | `Screen == "Result"` | |
| `RESTART` | `StopBot(); UnityEngine.Time.timeScale = 1f; UseRunSettings(1, 1f); PressToLobby(); PressDepart();` | `Screen == "Battle" && Wave == 1` / `AliveCount == 4 && HasOnlyStartItems && FireCount("sword") == 0 && LastRevivedMember == -1`, 대원 0~3이 각각 `MemberHp(i) == MemberMaxHp(i) && ItemState(i, 0) == "Ready"` | |

- `<부활 시드>`·`<부활 배율>`은 구현이 "웨이브 1 중 대원 1~3명이 쓰러지고 나머지는 시간 끝까지 버티는" 값을 찾아 파일에 적는다(F1 `<패배 배율>`과 같은 방식, 같은 시드면 결과가 같다 — B8). F4의 몬스터가 합쳐져 결과가 달라지면 합치는 쪽이 이 두 값만 다시 고른다.

## 데이터 스키마
위치는 F1과 같은 `Assets/Game/03.Resources/Resources/Data/`. 검사 명령은 F1의 `python3 -B ./tools/check_data.py`를 넓힌다.

| 위치 | 이름 | 필드 | 타입 | 키/기본값 | 쓰는 쪽 → 읽는 쪽 |
|---|---|---|---|---|---|
| JSON `Item/<id>.json` | 아이템(이 기능 8개, 대기열 뒤 32개). F1 칸 `id`·`name` 그대로 | `mode` | string | `melee`·`sweep`·`projectile`·`explosion`·`chain`·`heal`·`healArea`·`passive` 중 하나 | 데이터 → `ItemRepository` → `EquipmentStatService`·`CombatService`·`BattleItemListPresenter` |
| | | `shape` | object[] `{x, y}` | 칸 1~16개, 0 ≤ x, y ≤ 3, 겹침 없음, 상하좌우로 이어짐. 좌표 뜻(그릴 방향·회전)은 F5가 정한다 | 데이터 → F5 가방 |
| | | `power`, `interval`, `range` | float, float(초), float(칸) | `passive`면 0. 그 밖에는 모두 > 0 | 데이터 → B4·B5 |
| | | `baseEffect`, `baseValue` | string, float | `""` 또는 `attack`·`def`·`hp`·`heal`·`haste`·`range`. 비지 않으면 값 > 0, 비면 0 | 데이터 → B3 |
| | | `bonusEffect`, `bonusValue` | string, float | `""` 또는 기본 효과 6가지 + `shield`·`startShield`·`radius`·`pierce`·`extraShots`·`chain`·`pulse`·`share`. 비지 않으면 값 > 0(`pierce`·`extraShots`·`chain`은 정수) | 데이터 → B3·B6 |
| | | `bonusTarget` | string | `""`(대상 제한 없음) 또는 `attack` 또는 발동 방식 이름(`passive` 제외). `bonusEffect`가 비면 `""` | 데이터 → B3 |
| | | `bonusNeedsAdjacent` | bool | 기본 false | 데이터 → B3(B2 인접) |
| | | `effectInterval`, `effectRadius` | float(초), float | `pulse`면 둘 다 > 0, 아니면 0 | 데이터 → B6 파동 |
| | | `icon` | string | 기본 `""`(아이콘 없음, 이름 표시). 아트 단계가 채움 | 데이터 → `UI_BattleItemView` |
| JSON `Character/<id>.json` | 캐릭터(4개). F1 칸 `id`·`order`·`name`·`role`·`hp`·`atk`·`def`·`startItem`·`color` 그대로, 값 그대로 | `sprite` | string | 기본 `""`(F1 임시 도형). 아트 단계가 채움 | 데이터 → 아트 단계 |
| JSON `Combat/combat_rules.json` | 전투 규칙(1개) | `rangeUnit`, `defCap`, `hasteCap`, `healPeriod`, `shieldPeriod`, `meleeHalfAngle`, `sweepHalfAngle`, `projectileSpeed`, `projectileSpread`, `explosionRadius`, `explosionTriggerGap`, `chainBaseHits`, `chainHopRange`, `firedSeconds` | float(`chainBaseHits`만 int) | 42, 0.8, 1, 3, 8, 50, 90, 440, 0.12, 75, 5, 3, 150, 0.18. 모두 > 0, `defCap` ≤ 1 | 데이터 → `CombatRulesRepository` → `EquipmentStatService`·`CombatService` |
| JSON `Run/placeholder_combat.json` | 임시 전투(F1) | `weapon` | — | **삭제**(A절) | — |
| 세이브 | 없음 | | | 저장 없음(게임 기획서 A9) | |
| 서버 | 없음 | | | | |

- 검사 명령에 더하는 것: 위 형식·허용 값, 아이템 개수 범위 4~40(개수 출력), 발동 방식 8가지가 모두 한 번 이상 있음, 캐릭터 `startItem`이 `passive`가 아님, `combat_rules.json` 값. `--self-test`에 틀린 데이터 5종: 모르는 `mode`, `interval` 0인 발동 아이템, 4×4 밖이거나 끊긴 `shape`, 모르는 효과 종류, `passive` 시작 아이템. 각각 exit 1이어야 한다.
- 실행 중 상태(저장 안 함): `PartyModel`에 대원별 `Shield`·유효 방어(F1 HP·최대 HP는 그대로, 최대 HP는 B3 값). `CombatModel` — 대원별(능력치 묶음, 회복·보호막·나눔 시계, 나눔 제공자와 주기, 아이템 칸 목록[아이템 id, 남은 쿨, 마지막 주기, 발동 표시, 발동 아이템을 위한 능력치]), 투사체 목록(위치, 방향, 속도, 피해, 사거리, 날아간 거리, 관통, 폭발 반경·목표 위치, 맞힌 적 번호), 이번 판 아이템 id별 발동 수, 이번 판 부활 수, 마지막 부활 대원.

## 클라 변경
경로는 `Assets/Game/` 기준. 어셈블리는 F1의 `Game`·`Game.Editor`·`Game.Tests.EditMode` 그대로. 새 이벤트 없음(모두 콜백·직접 호출).

전투 (S2) — `02.Scripts/Features/Combat/`
- `Data/CombatRulesData.cs` — `combat_rules.json` 형
- `Registry/CombatRulesRepository.cs` — 규칙 파일 읽기(F1 `WaveRepository`와 같은 방식)
- `Interface/IItemPlacementInfo.cs` — 등급 배수·인접 자리(B2)
- `Service/NoBagPlacementInfo.cs` — 기본 구현(배수 1, 인접 없음). F5가 등록을 바꾼다
- `Model/CombatModel.cs`, `Model/BattleItemState.cs`(enum), `Model/CombatStats.cs`(능력치 묶음), `Model/ProjectileState.cs`
- `Service/EquipmentStatService.cs` — B3 능력치 계산(순수 계산, 상태 안 바꿈)
- `Service/CombatService.cs` — `ResetRun`·`PrepareWave`·`Step(dt)`·`ClearProjectiles`, B5·B6·B7
- `Service/ReviveService.cs` — E절 부활
- `Presenter/BattleItemListPresenter.cs`(`ITickable`), `Presenter/RevivePresenter.cs`, `Presenter/CombatWorldPresenter.cs`(`ITickable`: 투사체·고리 보기 위치 맞추기)
- `View/UI_BattleItemView.cs`, `View/UI_ReviveView.cs`, `View/ProjectileView.cs`, `View/BurstView.cs`
- `Factory/ProjectileViewFactory.cs`(`PoolManager.Get` + 초기화, 고리도 같은 공장), `Registry/ProjectileViewRegistry.cs`

아이템·캐릭터 (C2, C4)
- `02.Scripts/Features/Item/Data/ItemData.cs` — 칸 추가(`ItemCell {x, y}` 포함), 문자열 `mode`·효과를 읽을 때 enum으로(모르는 값은 에러 로그. 데이터 검사가 먼저 막는다)
- `02.Scripts/Features/Item/Data/ItemMode.cs`, `ItemEffectKind.cs` — enum
- `02.Scripts/Features/Item/Registry/ItemRepository.cs` — id로 찾기(F1에 없으면 더함)
- `02.Scripts/Features/Party/Data/CharacterData.cs` — `sprite` 칸
- `03.Resources/Resources/Data/Item/` — `sword`·`shortbow`·`firestaff`·`relic`(F1 파일에 칸 추가), `axe`·`wand`·`censer`·`ironshield`(새 파일)
- `03.Resources/Resources/Data/Character/` — 4개 파일에 `sprite: ""`
- `03.Resources/Resources/Data/Combat/combat_rules.json` — 새 파일

F1 파일에서 바꾸는 줄 — G절 표. 그리고 `02.Scripts/Features/Combat/Service/BasicWeaponService.cs` 삭제(`.meta` 함께).

판정
- `02.Scripts/App/PlayableProbe.cs` — H절 멤버
- `Tools/playable/f3-combat-flows.json` — 새 흐름 경로
- `Tools/check_data.py` — 검사 추가(데이터 스키마 아래)

테스트 (`Tests/EditMode/`)
- `CombatStatTests.cs`, `ItemActivationTests.cs`, `PassiveEffectTests.cs`, `CombatDamageTests.cs`, `ReviveTests.cs`, `CombatWaveTests.cs`, `CombatDeterminismTests.cs`, `CombatDataLoadTests.cs`. 시험용 아이템은 테스트 안에서 만든 `ItemData`를 쓰고(데이터 파일에 기대지 않음), 데이터 시험만 실제 파일을 읽는다. 배치 정보는 시험용 가짜 `IItemPlacementInfo`로 배수·인접을 준다.

## 서버 변경과 API 계약
없음. 게임 기획서: 서버 없음, 저장 없음(A9). 재화·랭킹·치팅 방지가 걸리는 것이 없다.

## 수용 기준
확인 방법: 자동 테스트(EditMode·명령), 에디터(컴파일·에디터 명령으로 상태 확인), 플레이(플레이 모드 동작·화면), 사람.
EditMode 테스트 명령: F1과 같은 `unity test --platform editmode --assembly Game.Tests.EditMode`(실행기 형식은 구현 단계가 이 레포의 unity-cli 형식으로 적는다). 흐름 경로 명령: `python3 -B ./tools/playable.py --route tools/playable/f3-combat-flows.json`.

| 번호 | 기준 (확인할 수 있는 문장) | 확인 방법 | 확인 절차 |
|---|---|---|---|
| AC1 | 새 코드·데이터를 넣은 뒤 컴파일 에러가 0개이고, 기본 무기 코드가 남아 있지 않다 | 에디터 | `python3 -B ./tools/editor_ready.py` 후 Editor.log에 `error CS` 없음. `git grep -n BasicWeaponService -- Assets/Game` 0줄, `placeholder_combat.json`에 `weapon` 칸 없음 |
| AC2 | 데이터 검사 명령이 지금 데이터에서 OK(exit 0)이고, F1의 틀린 데이터 4종과 이 기능의 틀린 데이터 5종(모르는 `mode`, `interval` 0인 발동 아이템, 4×4 밖이거나 끊긴 `shape`, 모르는 효과 종류, `passive` 시작 아이템)에서 각각 exit 1이다 | 자동 테스트 | `python3 -B ./tools/check_data.py`, `python3 -B ./tools/check_data.py --self-test` |
| AC3 | 아이템 파일이 C절 표의 대표 8종(시작 4종 + 도끼·완드·향로·철제 방패)이고 값이 표와 같으며, 발동 방식 8가지가 모두 한 번 이상 있다. 캐릭터 4개 파일에 `sprite` 칸이 있고 F1 값(이름·직업·HP·공격 배수·방어·시작 아이템)이 그대로다. 전투 규칙 파일 값이 데이터 스키마 표와 같다 | 자동 테스트 | `CombatDataLoadTests`(실제 파일을 읽어 대조), `python3 -B ./tools/check_data.py`의 개수·방식 출력 |
| AC4 | 판정 경로 파일 `Tools/playable/route.json`이 바뀌지 않았고(G1~G8 단계·확인 그대로), 흐름 경로 파일의 형식이 맞다 | 자동 테스트 | `git diff --exit-code <시작 커밋> -- Tools/playable/route.json`, `python3 -B ./tools/playable.py --check`, `python3 -B ./tools/playable.py --check --route tools/playable/f3-combat-flows.json` |
| AC5 | 판정 경로 G2까지 통과 | 플레이 | `python3 -B ./tools/playable.py --until G2` |
| AC6 | F1 흐름 경로가 이 기능의 전투로도 통과한다(바꿔도 되는 것은 `LOSE`의 `<패배 배율>` 값 하나) | 플레이 | `python3 -B ./tools/playable.py --route tools/playable/f1-flows.json`, `git diff`로 그 파일의 변경이 배율 값 한 곳뿐인지 확인 |
| AC7 | 능력치 계산: 단궁만 든 레나(공격 배수 1.2)의 피해 15.6·주기 0.7초·사거리 336. 집중의 반지(가속 0.04, 인접 아님)를 더하면 주기 0.7 ÷ 1.04. 숫돌이 단궁과 인접이면 피해 13 × 1.2 × 1.2 = 18.72, 인접이 아니면 13 × 1.2 × 1.06 = 16.536. 등급 배수 1.4를 주면 단궁 피해 21.84이고 관통·갈래·추가 연쇄 값은 배수를 곱하지 않는다. 캐릭터 방어 0.15에 방어 합 0.95인 대원의 유효 방어는 0.8(상한), 가속 합 1.2는 1로 잘린다. 브루노에 쇠사슬 갑옷 값(hp 45, 조건 없는 추가 hp 30)의 시험 아이템을 주면 최대 HP 475다. 조건 있는 추가 효과는 대원 전체 계산에 들어가지 않는다 | 자동 테스트 | `CombatStatTests` |
| AC8 | 발동 방식: 근접은 대상 방향 반각 50° 안 사거리 안 적만, 휩쓸기는 반각 90° 안 적만 맞힌다. 투사체는 관통 0이면 한 마리, 관통 1이면 두 마리를 맞히고 사거리를 날면 사라지며, 갈래 1이면 두 발이 ±0.06 rad로 나간다. 폭발은 반경 75(반경 증가 0.4면 105) 안 적 모두를 맞힌다. 연쇄는 3번(추가 연쇄 2면 5번), 150보다 먼 적으로는 옮겨 가지 않고 같은 적을 두 번 맞히지 않는다. 단일 회복은 HP 비율이 가장 낮은 다친 동료 하나를, 범위 회복은 사거리 안 다친 동료 모두를 회복하고 HP가 가득 찬 동료는 고르지 않는다 | 자동 테스트 | `ItemActivationTests` |
| AC9 | 쿨타임: 간격 1.0초·0.75초인 두 아이템을 든 대원이 대상이 늘 있는 채 0.125초 걸음 48번을 돌면 각각 6번·8번 발동한다(서로의 쿨에 영향 없음). 사거리 안에 대상이 없으면 발동하지 않고 쿨이 0 이하로 남았다가 대상이 들어온 첫 걸음에 발동한다. 가장 가까운 적이 같은 거리면 먼저 생긴 적을 고른다 | 자동 테스트 | `ItemActivationTests` |
| AC10 | 지속 효과: 회복 증가 3인 대원은 3초마다 3 회복한다. 나눔 아이템을 든 대원이 둘이면 받는 대원은 회복이 더 빠른 쪽의 회복을 그 주기로 받고, 제공자가 쓰러지면 다른 제공자로 바뀐다. 보호막 20 아이템은 웨이브 시작 8초 뒤 보호막을 20으로 채운다. 시작 보호막 15 아이템은 웨이브를 열 때 보호막 15를 준다. 파동 아이템은 3초마다 반경 100 안 적에게 10씩 피해를 준다 | 자동 테스트 | `PassiveEffectTests` |
| AC11 | 받는 피해: 유효 방어 0.15인 대원이 피해 10을 받으면 HP가 8.5 준다. 보호막 5가 있으면 보호막이 0이 되고 HP는 3.5 준다. HP 3에서 피해 10을 받으면 HP 0, 쓰러짐이다. 회복은 최대 HP를 넘지 않는다 | 자동 테스트 | `CombatDamageTests` |
| AC12 | 쓰러짐: 쓰러진 대원의 아이템 칸은 걸음이 지나도 남은 쿨이 줄지 않고 발동하지 않는다. 쓰러진 대원은 회복 대상·나눔 제공자·나눔 받는 대원이 아니다. `CanChangeEquipment`가 쓰러진 대원에서 거짓, 산 대원에서 참이다 | 자동 테스트 | `ReviveTests` |
| AC13 | 부활: 정비 화면에서 쓰러진 대원을 부활하면 HP = 최대 HP, 보호막 0, 모든 아이템 칸 남은 쿨 0이고 마지막 부활 대원이 그 대원이다. 같은 판에서 두 번째로 쓰러져도 다시 무료로 부활된다. 전투 화면에서의 부활과 산 대원의 부활은 거절되고 경고 로그가 남으며 상태가 바뀌지 않는다 | 자동 테스트 | `ReviveTests` |
| AC14 | 판·웨이브 시작: 새 판은 모든 아이템 칸 남은 쿨 0, 발동 수·부활 기록 0, HP = 최대 HP다. 웨이브를 열 때 장착 목록이 그대로인 대원은 남은 쿨이 이어지고, 바뀐 대원은 그 대원의 칸만 남은 쿨 0으로 새로 만들어진다. 최대 HP가 줄면 HP가 최대 HP로 잘리고, 웨이브 사이에 HP는 회복되지 않는다. 웨이브 끝과 로비로 갈 때 투사체가 0개다 | 자동 테스트 | `CombatWaveTests` |
| AC15 | 같은 시드·같은 장착으로 프레임을 1/60초 60번으로 주든 1초 1번으로 주든 처치 수·대원 HP·보호막·발동 수가 같다(투사체·연쇄·회복 포함) | 자동 테스트 | `CombatDeterminismTests.SameSeedSameResultAcrossFrameSplits` |
| AC16 | 플레이에서 출발하면 대원마다 시작 아이템 하나가 발동 목록(아이템 칸)에 있고 준비 상태이며, 장검·단궁·화염 지팡이가 실제로 발동한다. G4의 발동 목록 확인에 쓰는 진입점 `HasBattleItem`이 전투 아이템 칸을 읽는다 | 플레이 | 흐름 경로 `L1`·`B1`·`FIRE` 단계 통과 |
| AC17 | 플레이에서 대원이 쓰러지면 그 칸이 비활성이 되고, 웨이브 1이 끝나 정비 화면에서 쓰러진 대원만 부활할 수 있으며, 부활하면 4명이 HP 최대·준비 상태로 웨이브 2를 시작한다. 치유 성물이 실제로 발동한다 | 플레이 | 흐름 경로 `DOWN`·`PREP`·`REVIVE`·`NEXT` 단계 통과. 승패·HP를 써 넣는 호출 없음(진입점 코드에서 HP·`Phase`·`Outcome` 대입이 없는지 확인) |
| AC18 | 한 판이 끝난 뒤 로비로 → 출발하면 새 판의 대원 4명이 HP 최대, 시작 아이템만, 아이템 칸 준비, 발동 수 0, 부활 기록 없음으로 시작한다 | 플레이 | 흐름 경로 `END`·`RESTART` 단계 통과 |
| AC19 | 전투 화면 하단에 대원마다 아이템 칸이 아이콘 또는 이름·상태 글(준비·n.n초·발동!·지속 효과·비활성)·쿨타임 막대로 보이고 쓰러진 대원의 칸은 어둡다. 정비 화면에는 쓰러진 대원 자리에만 "쓰러짐"과 부활 버튼이 보인다. 투사체와 범위 발동 고리가 바닥 위에 보인다 | 플레이 | 흐름 경로 `FIRE`·`DOWN`·`PREP`·`REVIVE` 캡처를 열어 항목별로 확인하고 본 캡처 목록을 보고에 적음 |
| AC20 | 노치 기기에서 하단 아이템 칸과 부활 버튼이 가려지지 않고, 16:9·19.5:9·21:9·4:3 가로 화면에서 하단 칸·부활 버튼이 잘리거나 겹치지 않는다 | 플레이 | 흐름 경로 `FIRE`·`PREP`의 `capture: "all"` 4장씩을 열어 봄 + Device Simulator 노치 기기 1대(가로) 캡처 |

### 기존 테스트
이 기능이 건드리는 흐름의 기존 테스트는 F1이 만드는 테스트뿐이다. 레포에는 아직 NUnit 테스트가 없고(F1 기획서 "기존 테스트", 2026-11-15) F1 코드는 구현 중이라 이 기획서를 쓸 때 테스트 파일을 열어 보지 못했다. 그래서 어떤 테스트도 "그대로 통과"라고 적지 않는다. 아래는 F1 기획서의 수용 기준 문장(AC10~AC13)을 이 기획서의 새 규칙과 대조한 결과다. 구현 단계는 F1 테스트 파일을 열어 마지막 기대를 이 표와 대조하고, 표에 없는 기대가 바뀌어야 하면 고치지 말고 멈춰 보고한다.

- 기대가 바뀌지 않을 것으로 본 것(구현이 열어서 확인): `RunServiceTests`(웨이브 시간·승패·버닝 타임·일시정지·배속 — 전투 계산과 무관), `PartyServiceTests`의 편성 순서·시작 HP·장착 목록(시작 아이템에 HP 효과가 없어 최대 HP가 같다)과 받는 피해(시작 아이템에 방어·보호막 효과가 없어 피해 × (1 − 방어)와 같은 값), `CoreTests`, `IsoProjectionTests`, `SafeAreaLayoutTests`.

기대 변경 허용(이 목록에 있는 것만 구현 단계가 기대값을 고칠 수 있다):

| 테스트 | 지금 기대(끝 상태·값) | 새 기대 | 이유(기획서 절) |
|---|---|---|---|
| `PlaceholderCombatTests` 중 기본 무기를 쓰는 시험(F1 AC13 "10초를 돌리면 처치 1 이상", "HP 0인 대원은 공격하지 않는다") | `BasicWeaponService`가 기본 무기 값(13·0.7초·336)으로 쏴서 나온 처치·공격 | 같은 문장을 시작 아이템 4종의 발동으로 확인(처치 1 이상, 쓰러진 대원은 발동 0). 기본 무기 클래스·`weapon` 칸 참조를 지우고 시험을 `ItemActivationTests`로 옮겨도 된다 | A절 — 기본 무기 대체 |
| `PlaceholderCombatTests.SameSeedSameResultAcrossFrameSplits` | 기본 무기로 돈 처치 수·HP가 프레임 나누기와 상관없이 같음 | 기대 문장은 같고 전투가 아이템 발동으로 돈다. `CombatDeterminismTests`로 옮겨도 된다 | A절, B8 |
| `DataLoadTests`의 `placeholder_combat.json`·아이템 읽기 부분 | `weapon` 칸이 읽힘, 아이템은 `id`·`name`만 | `weapon` 칸 없음, 아이템에 C절 칸이 있음 | A절, 데이터 스키마 |
| F1 `check_data.py --self-test`의 아이템 개수 검사 | 아이템 개수 ≥ 4 | 개수 범위 4~40(틀린 데이터 4종은 그대로 exit 1) | 데이터 스키마 |

## 열린 질문
사람이 정할 것은 없다. 작은 갈림길은 아래 가정으로 정했다(승인 때 함께 확인).

- 구현 단계가 확인할 것(사람 질문 아님): F1 테스트 파일을 열어 위 "기존 테스트" 대조를 마친다. 표에 없는 기대가 바뀌면 멈추고 보고한다.

### 가정
| 번호 | 가정 | 틀리면 바뀌는 것 |
|---|---|---|
| CA1 | 발동 방식은 공격 5(근접·휩쓸기·투사체·폭발·연쇄) + 회복 2(단일·범위) + 지속 1, 모두 8가지. 대표 데이터는 방식마다 하나: 시작 4종 + 도끼·완드·향로·철제 방패 | C절, AC3 |
| CA2 | 등급 배수와 인접은 F5가 `IItemPlacementInfo`로 준다. 이 기능에서는 배수 1·인접 없음이라 조건 있는 추가 효과(숫돌·화살통 등)는 F5 전까지 켜지지 않는다 | B2, F5 |
| CA3 | 조건 없는 추가 효과는 늘 적용한다(예전 규칙. 예: 철제 방패 방어 0.08 + 0.08) | B3, 아이템 수치 감각 |
| CA4 | 남은 쿨은 웨이브 사이에 이어지고, 장착 목록이 바뀐 대원과 부활한 대원만 0으로 돌아간다. 새 아이템의 첫 쿨 난수(예전 0~0.2초)는 버린다 | B7, E절 |
| CA5 | 부활은 비용·횟수 제한이 없고 정비 화면에서만 된다. 판정 봇(`PlayableBot`)은 부활하지 않는다 — G5 "가만히 버틴다"의 뜻을 바꾸지 않기 위해서. G7 봇이 부활을 쓸지는 자동 장착 봇을 만드는 F5가 정한다(이 기능은 `PressRevive`를 준다) | E절, H절, M5·M6 |
| CA6 | 웨이브 사이에 HP는 회복되지 않는다(R2, 예전과 같음) | B7, M5·M6 난이도 |
| CA7 | 거리·각도·속도는 예전 단위 그대로(게임 기획서 A5). 맵 시야 검사는 버린다(A10 큰 장애물 없음) | B5, F2의 거리 감각 |
| CA8 | 한 걸음 안에서 아이템 발동과 투사체 이동이 적 이동(⑤) 앞에 온다(예전은 투사체가 적 뒤) | A절, B5 |
| CA9 | 전투 월드의 발동 표시는 임시 도형(투사체 원, 범위 고리)이고, 보호막 양은 화면에 보이지 않는다. 효과 그림은 아트 단계, 피격 연출은 F6 | F절 |
| CA10 | 캐릭터 데이터에는 `sprite` 칸 하나만 더한다. 걷기·공격·쓰러짐 클립 이름은 아트 단계가 이 키 아래에서 정한다 | D절, 아트 단계 |
