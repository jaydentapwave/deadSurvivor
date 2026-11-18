# F6 전투 화면과 피드백 기획서

- 상태: 승인 (2026-11-15, 디렉터 확인 — 게임 시작 전 승인 범위 안, director-game autonomy 1절)
- 서버 필요: 아니오
- 관련 문서: [게임 기획서](../../game/gdd.md) F6(S9), 기둥 P3, 가정 A7, 10절(사람 판정), 목표값 M7. 판정 단계 G2(이 기능에서 회귀 통과), G3·G5·G7(피드백이 지나가지만 F2·F5와 콘텐츠가 있어야 닿음 — H절). 앞선 기능 [F1](../f1-skeleton/plan.md)(E·F·G·H절), [F3](../f3-combat/plan.md)(B4 피해·회복, G절), [F4](../f4-monsters/plan.md)(A절 행동·상태, F절). 아트 기준서 K15·2.4절 겹 순서. 참고 분석 문서 없음. 예전 판 코드 `Assets/DeadSurvivor/Runtime/DSGame.Combat.cs`·`DSWorld.cs`(읽기 전용, 규칙만 옮김). 근거는 [notes.md](notes.md)
- 작업 규모: L2(새 데이터 파일 스키마, 피해 적용 지점 두 곳에 기록 추가) — 구현 전 설계 검토

## 목표
몬스터를 때리면 피해 숫자가 떠오르고, 원정대가 맞으면 HP 막대의 깎인 구간이 잠깐 빨갛게 남으며 화면이 약하게 흔들리고 (기기에서) 진동이 온다. 몬스터 HP 막대는 강한 적(중장갑)과 지금 원정대를 공격 중인 적에게만 보인다.
수백 마리가 맞는 장면에서도 피해 숫자 수에 상한이 있어 프레임을 지킨다(M7). 피해 계산은 바꾸지 않는다.

## 참고 분석 요약
예전 판(같은 게임의 3D 구현)에서 옮길 것. 모두 관찰이며 출처는 위 경로다.

- 피해 숫자는 적이 맞을 때만, 맞은 자리 조금 위에 정수(반올림)로 0.65초 떠 있었다. 대원이 맞을 때는 숫자 없이 깜빡임만 있었다 (관찰, `DSGame.Combat.cs` `Hit`·`Damage`)
- 몬스터 HP 막대는 다친 적 모두에게, 머리 위(중장갑은 1.4배 높이)에 그렸다 (관찰, `DSWorld.cs` 190행대) — 새 규칙(A7)과 다르므로 위치만 가져온다
- 흔들림·진동·HP 깎인 구간 강조는 예전 판에 없었다(새 스펙, `docs/game/2d-전환-계획.md` 6행)

## 적용안

### 가져올 것 / 버릴 것 / 바꿀 것
- 가져올 것: 피해 숫자의 대상(몬스터가 받은 피해)·반올림·수명 0.65초, HP 막대의 머리 위 자리
- 버릴 것: 다친 적 모두의 HP 막대(P3 "모든 몬스터 HP 바" 금지), 숫자 색 `#fff2cc`(K15 흰 글자 + 검은 테두리로 바꿈)
- 바꿀 것: 피해 사실은 피해 적용 지점이 모델에 적기만 하고, 그리는 일은 Presenter가 프레임 끝에 한 번에 한다. 세기·시간·상한은 모두 데이터

### 기존 시스템과의 관계
신규 도메인 `Features/Feedback/`. 기존 파일은 피해 적용 지점 두 곳(`PartyService`·`MonsterService`의 피해 적용)에 기록 한 줄씩과 HP 막대 View 두 곳에 칸 하나씩만 더한다. 피해 수치·HP·처치·시드 결과는 바뀌지 않는다.
구현 기준 코드는 F1 + F3 + F4가 합쳐진 상태다(게임 기획서 7절 앞선 기능 F3·F4). F1·F3·F4 코드는 아직 구현 중이라 이 기획서는 그 기획서들의 이름을 기준으로 썼다. 구현은 실제 코드의 이름·시그니처에 맞추고, 다르면 그 차이를 보고에 적는다.

### A. 피해 사실 받기 (계산은 그대로)
- 원정대 피해: F3 B4가 바꾼 `PartyService`의 피해 적용 끝에 `CombatFeedbackModel.AddMemberHit(memberIndex, taken, hpBefore, hpAfter)` 한 줄. `taken` = 방어를 뺀 받음(보호막 흡수 전), `hpBefore`·`hpAfter`는 그 호출 전후 HP. F4의 접촉·중장갑·적 투사체·돌진·자폭이 모두 이 길을 탄다.
- 몬스터 피해: F1 `MonsterService`의 피해 적용(F3가 모든 발동·파동 피해에 부르는 곳) 끝에 `CombatFeedbackModel.AddMonsterHit(monsterNo, amount, x, y)`(`monsterNo` = F1 `MonsterModel`의 몬스터 번호) 한 줄. `amount` = 그 호출로 준 피해 값, `x`·`y` = 맞은 순간의 바닥 좌표. 이미 HP 0인 몬스터에 대한 호출(피해 적용이 무시하는 경우)은 적지 않는다.
- `CombatFeedbackModel`은 이번 프레임에 쌓인 기록 목록 두 개(미리 잡은 `List`)와 이번 판 셈(아래 H절)을 가진 상태 덩어리다. 피드백 Presenter들이 `ILateTickable`(모든 `Step`이 끝난 뒤)에서 읽고 비운다. EventBus는 쓰지 않는다(Service에 EventBus를 넣지 않는 규칙, `.ai/rules/event-bus-rules.md`).
- 피드백 시계: 전투 화면이고 일시정지가 아닐 때만 `Time.deltaTime`만큼 흐른다(배속과 무관, 판정 경로의 `time_scale`은 따른다). 숫자·빨간 구간·흔들림·HP 막대 머무름이 모두 이 시계를 쓴다.
- 지우기: `RunModel.Phase`가 Battle에서 바뀌거나 `RunModel.RunCount`가 바뀌면(R3 구독) 숫자·몬스터 HP 막대·빨간 구간·흔들림을 모두 거두고 남은 기록을 버린다. 새 판이 시작되면 이번 판 셈을 0으로. `RunService`는 고치지 않는다.

### B. 피해 숫자 (V2, 몬스터만)
- 한 프레임 안에서 같은 몬스터가 여러 번 맞으면 합쳐 숫자 하나로 낸다(시간 배율 20이나 연쇄·폭발에서 수를 줄인다). 자리는 그 프레임 첫 기록의 좌표.
- 글: `max(1, round(합))` 정수. 흰 글자(`textColor`) + 검은 테두리(`outlineColor`)(K15). 크기 `fontSizePx`(가상 해상도 픽셀).
- 움직임(`DamageNumberMotion`, 순수 함수): 나이 t(0 → `lifetime`)에 따라 시작 자리에서 위로 `risePx` × (t ÷ lifetime) 올라가고, t ≥ `lifetime × fadeStartRatio`부터 투명해져 끝에서 0. 몬스터를 따라가지 않는다. 시작 자리 = 맞은 바닥 좌표의 월드 위치 + 위로 (몬스터 반지름 × `unitScale` + `offsetPx` ÷ 48) 유닛.
- 상한: 동시에 보이는 숫자는 `maxActive`(출발값 40)개까지. 꽉 차면 가장 오래된 숫자를 거두고 새 숫자에 그 자리를 준다(새 사실이 먼저 보인다).
- 그리기: 월드 공간 `TextMeshPro`(com.unity.ugui 2.0에 들어 있음, 새 패키지 없음), 정렬 순서 60(F1 겹: 효과 50 위, 아트 기준서 2.4 "효과 → 피해 숫자 → UI"). 글꼴은 F1이 복사한 `NotoSansKR.ttf`에서 숫자 0~9만 담은 정적 TMP 글꼴 자산을 만든다. 테두리는 그 글꼴 재질의 Outline. 숫자 글꼴 그림은 아트 단계(기준서 3.3 UI "피해 숫자 글꼴")가 바꾼다.
- 만들기: `DamageNumberViewFactory`가 `PoolManager.Get`으로 꺼내고 Get 때 글·색·투명도·자리를 초기화, `DamageNumberViewRegistry`가 살아 있는 숫자를 만든 순서로 든다.

### C. HP 감소 구간 빨간 강조 (V2 머리 위·하단 HP 막대, 원정대만)
- 대원마다 빨간 구간 상태 `HpLossTrail`(순수 클래스): 빨간 시작 비율 `lossFrom`과 남은 머무름 시간.
  - HP가 줄면(기록의 `hpAfter < hpBefore`): `lossFrom = max(lossFrom, hpBefore ÷ 최대 HP)`(연달아 맞으면 시작점이 높은 쪽에 머문다), 머무름 = `holdSeconds`.
  - 매 프레임: 머무름 > 0이면 줄인다. 0이면 `lossFrom`이 지금 비율 쪽으로 `(1 ÷ shrinkSeconds) × 처음 길이`의 속도로 줄어 지금 비율에 닿으면 끝(빨간 구간 없음).
  - HP가 오르거나(회복·부활) 지금 비율이 `lossFrom` 이상이면 `lossFrom` = 지금 비율(빨간 구간 없음).
  - 보호막이 다 흡수해 HP가 그대로면 빨간 구간은 생기지 않는다(흔들림·진동은 D절대로).
- 그리기: 머리 위 막대(F1 `MemberView`)와 하단 막대(F1 `UI_MemberStatusView`) 각각의 채움 뒤에 빨간 구간 하나. 길이 = `lossFrom − 지금 비율`, 자리 = 지금 비율 끝부터. 색 `hpLoss.color`. 막대 밖으로 번지거나 화면을 덮는 연출은 없다(P3).
- 머리 위: 새 `HpLossBarView`(월드 스프라이트, F1 HP 막대와 같은 스프라이트 방식). 하단: 새 `UI_HpLossView`(uGUI `Image`). 둘 다 `UpdateUI(currentRatio, lossFromRatio)` 하나로 갱신한다.

### D. 흔들림과 진동 (원정대 피격)
- 피격 = 이번 프레임에 `taken > 0`인 원정대 기록이 하나 이상. 한 프레임에 여러 명이 맞아도 한 번이다.
- 흔들림: 지난 흔들림 시작에서 `shake.minInterval`이 지났으면 새로 시작(세기는 쌓지 않고 처음부터). `shake.seconds` 동안 카메라를 정수 픽셀만큼 옮긴다: 세기 = `amplitudePx × (남은 시간 ÷ seconds)`, 방향은 프레임마다 (±1, ±1) 중 하나를 번갈아(결정적, 난수 안 씀), 픽셀을 정수로 반올림해 `1/48` 유닛 배수로(아트 기준서 "정수 픽셀 이동", Pixel Perfect). 끝나면 오프셋 0.
- 카메라에 얹는 법: 새 `CameraShakeView`(카메라 오브젝트의 컴포넌트)가 `ILateTickable` Presenter의 지시로 오프셋을 얹는다. 기준 위치 = 이번 프레임에 다른 쪽(F1 고정, F2 따라가기 `WorldPresenter`)이 카메라를 옮겼으면 그 위치, 아니면 지난 기준 위치. 그래서 카메라를 매 프레임 쓰는 쪽이든 한 번만 쓰는 쪽이든 오프셋이 쌓이지 않는다(순수 함수 `ShakeMath.ResolveBase`로 시험).
- 진동: 지난 진동에서 `haptic.minInterval`이 지났고 `haptic.enabled`면 `HapticService.Play()`. `HapticService`는 이번 판 호출 수를 세고, Android·iOS 기기 실행(`(UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR`)에서만 `Handheld.Vibrate()`를 부른다. 에디터·PC에서는 호출 수 기록만 남는다(이것으로 확인한다). `Handheld.Vibrate`는 세기를 고를 수 없다 — 세기는 기기에서 사람이 본다(FA5).
- 흔들림·진동은 전투 화면에서만. 정비·결과 화면으로 가면 오프셋 0.

### E. 몬스터 HP 막대 (V2, 강한 적·공격 중인 적만)
- 보이는 조건(`MonsterHpBarRule`, 순수 함수) — 살아 있는 몬스터 하나에 대해:
  - 강한 적: 행동이 `monsterHp.alwaysShowBehaviors`(출발값 `["armored"]`, A7·F4 A절 "F6의 강한 적은 이 행동으로 고른다")에 있으면 늘 보인다. 코드에 몬스터 id·행동 이름을 쓰지 않는다.
  - 공격 중(F4 A절 상태로 판정. 가장 가까운 산 대원과의 거리 d):

| 행동 | 공격 중 |
|---|---|
| Contact·Armored | d ≤ `radius + contactGap`(붙어서 때리는 거리) |
| Projectile | d < `projectile.range`(쏘는 거리 안) |
| Charge | 상태 Windup 또는 Dash |
| Bomb | 상태 Fuse |

  - 머무름: 공격 중이 끝난 뒤 `lingerSeconds`(출발값 1초) 동안 더 보인다(붙었다 떨어졌다 할 때 깜빡이지 않게). 산 대원이 없으면 공격 중이 아니다.
- 상한: 동시에 `maxActive`(출발값 40)개까지. 넘으면 강한 적 먼저, 그다음 원정대에 가까운 순. 상한 때문에 못 보인 수는 진입점으로 읽는다.
- 그리기: 새 `MonsterHpBarView`(월드 스프라이트 두 장: 바탕·채움, 폭 `widthPx`·높이 `heightPx`), 자리 = 몬스터 머리 위(몬스터 반지름 × `unitScale` + `offsetPx` ÷ 48 유닛 위), 정렬 순서 55(효과 50 위, 피해 숫자 60 아래). 채움 = HP ÷ 최대 HP(F4 `MonsterModel`). 몬스터 막대에는 빨간 구간이 없다(S9의 빨간 강조는 피격 = 원정대).
- 만들기: `MonsterHpBarViewFactory`(`PoolManager.Get` + 초기화)·`MonsterHpBarViewRegistry`(몬스터 번호 → View). 안 보이게 되거나 몬스터가 사라지면 풀에 돌려준다. 몬스터 View(F4 `MonsterView`)는 고치지 않는다.

### F. 화면과 기능의 경계 (뒤 기능과 겹치는 파일)
이 기능은 아래 파일에 줄·칸만 더한다. F2~F5 구현이 나란히 돌고 나중에 합쳐지므로 그 밖의 줄은 건드리지 않는다.

| 파일 | 이 기능이 더하는 줄·칸 |
|---|---|
| `02.Scripts/App/GameSceneLifetimeScope.cs` | 등록 줄: `FeedbackRulesRepository`, `CombatFeedbackModel`, `HapticService`, `DamageNumberViewFactory`, `DamageNumberViewRegistry`, `MonsterHpBarViewFactory`, `MonsterHpBarViewRegistry`(`Lifetime.Scoped`), 진입점 `DamageNumberPresenter`·`HpLossPresenter`·`HitReactionPresenter`·`MonsterHpBarPresenter`, `CameraShakeView`(`RegisterComponentInHierarchy`), 피해 숫자·HP 막대 프리팹 참조 칸 두 개(F1 MonsterView 프리팹 연결 방식) |
| `02.Scripts/App/PlayableProbe.cs` | H절 멤버와 같은 파일의 에디터 전용 `PlayableFrameSampler`만 더함. 기존 멤버·봇은 바꾸지 않음 |
| `01.Scene/Main.unity` | 카메라에 `CameraShakeView` 컴포넌트, 피해 숫자·몬스터 HP 막대 풀 부모 오브젝트 두 개, 하단 대원 HP 막대마다 `UI_HpLossView` 자식 하나(채움 뒤), LifetimeScope의 프리팹 참조 연결 |
| HUD View `UI_MemberStatusView.cs` | `[SerializeField] UI_HpLossView` 칸 하나와 그것을 돌려주는 읽기 |
| `Party/View/MemberView.cs`(머리 위 HP) | `[SerializeField] HpLossBarView` 칸 하나와 그것을 돌려주는 읽기. 대원 View가 프리팹이면 그 프리팹의 HP 막대에 빨간 구간 자식 하나 |
| `PartyService.cs`·`MonsterService.cs` | 생성자 인자 `CombatFeedbackModel` 하나, 피해 적용 끝의 기록 한 줄(A절). 계산 줄은 그대로 |
| `Tools/check_data.py` | `Feedback/feedback_rules.json` 검사 함수 하나와 호출 한 줄, `--self-test` 틀린 데이터 3종 |
| `Run/placeholder_combat.json`·HUD 상단 `UI_BattleHudView`·`BattleHudPresenter`·`RunService`·`GameEvent.cs`·`route.json`·다른 기능의 흐름 경로 | 고치지 않는다 |

| 화면 | 앞 기능이 넣은 것 | 이 기능이 더하는 것 |
|---|---|---|
| V2 전투 | 원정대 + 머리 위 HP(F1), 하단 HP 막대(F1)와 아이템 칸(F3), 몬스터 5종·상태 표시(F4), 발동 표시(F3) | 몬스터 피해 숫자, 머리 위·하단 HP 막대의 빨간 구간, 카메라 흔들림, 진동, 강한 적·공격 중인 적의 HP 막대 |
| V1·V3·V4 | — | 없음(전투 밖에서는 모두 꺼짐) |

### G. 화면 배치
- 새 UI는 하단 HP 막대 안의 빨간 구간뿐이라 F1 안전 영역 판넬·앵커를 그대로 따른다. 피해 숫자·몬스터 HP 막대는 월드 공간이라 카메라와 함께 화면비를 따른다.
- 흔들림 오프셋(최대 `amplitudePx`, 출발값 2px)은 들판 경계(F2 C절, 카메라에 들판 밖이 보이지 않는 범위)를 넘지 않을 만큼 작다. 넘는 값으로 바꿀 때는 경계 여유를 같이 늘린다(FA7).

### H. 판정 진입점과 경로
- 경로 파일 `Tools/playable/route.json`은 바꾸지 않는다(G1~G8 단계·확인 그대로). 이 기능만으로 통과하는 단계는 G2까지다.

| 단계 | 이 기능이 지나가는 곳 | 이 기능만으로 못 지나가는 이유 | 이 기능의 확인 |
|---|---|---|---|
| G2 | 전투 화면에 피드백이 붙어도 웨이브 1·생존 4·시간이 흐름 | — (통과) | AC2 |
| G3 | 웨이브 1 전투 중 숫자·빨간 구간·흔들림·HP 막대 | 이동 봇(F2), `StashCount`(F5) | 흐름 `HIT`·`HURT`·`MHP` |
| G5 | 전멸까지의 피격 피드백 | G4(F5)를 지나야 이어짐 | 흐름 `HURT`, `RESET` |
| G6·G8 | 새 판에서 피드백 셈·표시가 비어 있음 | G5·G7을 지나야 이어짐 | 흐름 `RESET` |
| G7 | 버닝 타임 프레임 시간 M7(피해 숫자 상한) | 이동 봇(F2), 자동 장착(F5), 웨이브 수치(콘텐츠 대기열) | 흐름 `LOAD`(적 수백 마리 + 피드백에서 프레임 시간) |

- 진입점 `PlayableProbe`에 이 기능이 더하는 것(모두 Model·Registry·Service를 읽기만 한다. 게임 코드는 진입점을 모른다):
  - 읽기: `DamageNumberCount`(지금 보이는 수), `DamageNumberShownTotal`(이번 판 낸 수), `DamageNumberPeak`(이번 판 최대 동시 수), `DamageNumberMaxActive`(데이터 상한), `MemberWithHpLoss`(빨간 구간이 보이는 가장 앞 대원, 없으면 −1), `HpLossRatio(int member)`(빨간 구간 길이 비율), `ShakeCount`(이번 판 흔들림 시작 수), `IsShaking`, `HapticCount`(이번 판 `HapticService.Play` 수), `MonsterHpBarCount`, `AlwaysShownHpBarCount`(강한 적 규칙으로 보이는 수), `MonsterHpBarRuleViolations`(지금 보이는데 E절 조건 밖인 막대 수 + 조건 안이고 상한에 걸리지 않았는데 안 보이는 몬스터 수), `FrameMsP95SinceMark`(표시 뒤 `Time.unscaledDeltaTime` 95백분위 ms, 표본 없으면 −1), `EnemyPeakSinceMark`(표시 뒤 살아 있는 몬스터 최대 수).
  - 조작: `MarkFrameStats()`(같은 파일의 에디터 전용 `PlayableFrameSampler`가 표본을 새로 모은다).
- 흐름 경로 `Tools/playable/f6-feedback-flows.json`(같은 장면·진입점, `--route`로 지정, `time_scale` 1, `scene`·`size`·`sizes`는 `route.json`과 같게). 식은 `PlayableProbe.` 접두어를 줄였다. F4 멤버 `UseRealTimeAtWave`를 쓴다.

| 단계 | do | wait / expect | capture |
|---|---|---|---|
| `L1` | — | `Screen == "Lobby"` | |
| `HIT` | `UseRunSettings(<피격 시드>, <피격 배율>); PressDepart();` | `Screen == "Battle" && Wave == 1 && DamageNumberCount >= 1`(timeout 30) / `DamageNumberCount <= DamageNumberMaxActive && MonsterHpBarRuleViolations == 0` | `all`(V2 숫자·막대) |
| `HURT` | — | `MemberWithHpLoss >= 0`(timeout 30) / `HpLossRatio(MemberWithHpLoss) > 0 && ShakeCount >= 1 && HapticCount >= 1 && HapticCount <= ShakeCount + 1` | 1장(빨간 구간) |
| `MHP` | — | `MonsterHpBarCount >= 1`(timeout 30) / `MonsterHpBarRuleViolations == 0` | 1장 |
| `STRONG` | `StopBot(); UseRunSettings(<중장갑 시드>, <중장갑 배율>); UseRealTimeAtWave(5); UnityEngine.Time.timeScale = 20f; PressToLobby(); PressDepart(); StartBot(BotMode.AutoNext);` | `Screen == "Battle" && Wave == 5 && AlwaysShownHpBarCount >= 1`(timeout 120) / `MonsterHpBarRuleViolations == 0` | 1장(중장갑 HP 막대) |
| `PREPCLEAR` | `UnityEngine.Time.timeScale = 20f;` | `Screen != "Battle"`(timeout 60) / `DamageNumberCount == 0 && MonsterHpBarCount == 0 && MemberWithHpLoss == -1 && !IsShaking` | |
| `LOAD` | `StopBot(); UnityEngine.Time.timeScale = 1f; UseRunSettings(<부하 시드>, <부하 배율>); PressToLobby(); PressDepart(); MarkFrameStats();` | `EnemyPeakSinceMark >= 300 \|\| Screen != "Battle"`(timeout 40) / `EnemyPeakSinceMark >= 300 && DamageNumberPeak <= DamageNumberMaxActive && FrameMsP95SinceMark >= 0 && FrameMsP95SinceMark <= 16.7f` | 1장 |
| `RESET` | `UnityEngine.Time.timeScale = 1f; UseRunSettings(1, 1f); PressToLobby(); PressDepart();` | `Screen == "Battle" && Wave == 1` / `DamageNumberCount == 0 && DamageNumberShownTotal == 0 && ShakeCount == 0 && HapticCount == 0 && MonsterHpBarCount == 0 && MemberWithHpLoss == -1` | |
| `LOBBY` | `PressToLobby();` | `Screen == "Lobby"` | |

- `<피격 시드>`·`<피격 배율>`은 웨이브 1에서 대원이 맞으면서도 시간 끝까지 버티는 값, `<중장갑 …>`은 웨이브 5에 닿는 값, `<부하 배율>`은 웨이브 1에 살아 있는 몬스터가 300마리 이상 쌓이는 값(출발값 4)이다. 구현이 실제 전투(F3·F4)로 찾아 파일에 적는다(F1 `<패배 배율>`과 같은 방식). 확인 식은 바꾸지 않는다.
- `LOAD`의 16.7ms는 M7과 같은 기준을 피드백이 붙은 적 300마리 이상 장면에 쓴 것이다. M7 자체(10웨이브 버닝 타임)는 G7이 잰다.

## 데이터 스키마
위치는 F1과 같은 `Assets/Game/03.Resources/Resources/Data/`. 검사 명령은 F1의 `python3 -B ./tools/check_data.py`를 넓힌다.

| 위치 | 이름 | 필드 | 타입 | 키/기본값 | 쓰는 쪽 → 읽는 쪽 |
|---|---|---|---|---|---|
| JSON `Feedback/feedback_rules.json` | 피드백 규칙(1개, 새 폴더) | `damageNumber{lifetime, risePx, fadeStartRatio, offsetPx, fontSizePx, maxActive, textColor, outlineColor}` | float > 0, float ≥ 0, float 0~1, float, float > 0, int 1~200, string(#rrggbb), string(#rrggbb) | 0.65, 12, 0.6, 4, 8, 40, #ffffff, #000000 | 데이터 → `FeedbackRulesRepository` → `DamageNumberPresenter`·`DamageNumberViewFactory` |
| (같은 파일) | 빨간 구간 | `hpLoss{holdSeconds, shrinkSeconds, color}` | float ≥ 0, float > 0, string(#rrggbb) | 0.4, 0.35, #d8433c | → `HpLossPresenter` |
| (같은 파일) | 흔들림 | `shake{amplitudePx, seconds, minInterval}` | int 0~4, float ≥ 0, float ≥ 0 | 2, 0.15, 0.25 | → `HitReactionPresenter` |
| (같은 파일) | 진동 | `haptic{enabled, minInterval}` | bool, float ≥ 0 | true, 0.3 | → `HitReactionPresenter`·`HapticService` |
| (같은 파일) | 몬스터 HP 막대 | `monsterHp{alwaysShowBehaviors, lingerSeconds, maxActive, widthPx, heightPx, offsetPx, fillColor, backColor}` | string[](F4 `behavior` 다섯 중), float ≥ 0, int 0~400, int > 0, int > 0, float, string, string | ["armored"], 1, 40, 16, 2, 4, #d8433c, #2a1f1f | → `MonsterHpBarPresenter`·`MonsterHpBarViewFactory` |
| 세이브 | 없음 | | | 저장 없음(A9) | |
| 서버 | 없음 | | | | |

- 검사: 칸 모두 있음, 위 범위, 색 형식, `alwaysShowBehaviors`가 F4 행동 다섯 중 하나이고 중복 없음. `--self-test` 틀린 데이터 3종: `maxActive` 0, 다섯 밖 행동, 색 형식 틀림.
- `shake.amplitudePx`를 0으로, `haptic.enabled`를 거짓으로 두면 그 연출이 꺼진다(사람 판정 뒤 조정용).
- 실행 중 상태(저장 안 함): `CombatFeedbackModel`(이번 프레임 원정대·몬스터 피해 기록, 이번 판 셈: 낸 숫자 수·최대 동시 수·흔들림 수), 대원별 `HpLossTrail`, 흔들림 시계, 몬스터별 마지막 공격 중 시각, `HapticService` 호출 수.

## 클라 변경
경로는 `Assets/Game/` 기준.

피드백 도메인 (새 폴더 `02.Scripts/Features/Feedback/`)
- `Data/FeedbackRulesData.cs`, `Registry/FeedbackRulesRepository.cs` — JSON 형과 읽기(`ResourceService.LoadJson`)
- `Model/CombatFeedbackModel.cs` — A절 기록·셈
- `Service/HapticService.cs` — D절 진동(기기에서만 `Handheld.Vibrate`, 호출 수 셈)
- `Utils/DamageNumberMotion.cs`, `Utils/HpLossTrail.cs`, `Utils/ShakeMath.cs`(오프셋·기준 위치), `Utils/MonsterHpBarRule.cs` — 순수 계산(EditMode 시험 대상)
- `Presenter/DamageNumberPresenter.cs`(B절), `Presenter/HpLossPresenter.cs`(C절), `Presenter/HitReactionPresenter.cs`(D절 흔들림·진동), `Presenter/MonsterHpBarPresenter.cs`(E절) — 모두 `ILateTickable`, A절 "지우기" 구독을 각자 가짐
- `View/DamageNumberView.cs`(TextMeshPro), `View/HpLossBarView.cs`, `View/UI_HpLossView.cs`, `View/MonsterHpBarView.cs`, `View/CameraShakeView.cs`
- `Factory/DamageNumberViewFactory.cs`, `Factory/MonsterHpBarViewFactory.cs`(`PoolManager.Get` + 초기화), `Registry/DamageNumberViewRegistry.cs`, `Registry/MonsterHpBarViewRegistry.cs`

자산
- `03.Resources/Prefabs/Feedback/DamageNumber.prefab`, `MonsterHpBar.prefab`(F1 MonsterView 프리팹 폴더 규칙을 따른다)
- `03.Resources/Font/DamageNumber SDF.asset` — `NotoSansKR.ttf`에서 숫자 0~9 정적 TMP 글꼴, Outline 재질
- `03.Resources/Resources/Data/Feedback/feedback_rules.json`

겹치는 파일 — F절 표의 줄·칸만

판정
- `02.Scripts/App/PlayableProbe.cs` — H절 멤버, `PlayableFrameSampler`
- `Tools/playable/f6-feedback-flows.json` — H절 흐름 경로(새 파일)

테스트 (`Tests/EditMode/`)
- `DamageNumberMotionTests.cs`, `HpLossTrailTests.cs`, `ShakeMathTests.cs`, `MonsterHpBarRuleTests.cs`, `CombatFeedbackRecordTests.cs`(피해 적용이 기록을 남기고 값은 그대로), `HapticServiceTests.cs`(간격·꺼짐·셈), `FeedbackDataLoadTests.cs`

## 서버 변경과 API 계약
없음. 게임 기획서: 서버 없음, 저장 없음(A9). 화면 연출이라 재화·랭킹·치팅 방지가 걸리지 않는다.

## 수용 기준
확인 방법: 자동 테스트(EditMode·명령), 에디터(컴파일·에디터 명령으로 상태 확인), 플레이(플레이 모드 동작·화면), 사람.
EditMode 테스트 명령: F1이 정한 `unity test --platform editmode --assembly Game.Tests.EditMode`(구현 단계가 이 레포의 unity-cli 형식으로 적는다).

| 번호 | 기준 (확인할 수 있는 문장) | 확인 방법 | 확인 절차 |
|---|---|---|---|
| AC1 | 새 코드·자산·데이터를 넣은 뒤 컴파일 에러가 0개이고, 데이터 검사가 지금 데이터에서 OK, 틀린 피드백 데이터 3종에서 각각 exit 1이다 | 에디터 | `python3 -B ./tools/editor_ready.py` 후 Editor.log에 `error CS` 없음. `python3 -B ./tools/check_data.py`, `python3 -B ./tools/check_data.py --self-test` |
| AC2 | 판정 경로 G2까지 통과 | 플레이 | `python3 -B ./tools/playable.py --until G2` |
| AC3 | 판정 경로 파일이 G1~G8을 번호 그대로 갖고 형식 검사가 통과하며, 이 기능이 `route.json`을 바꾸지 않았다 | 자동 테스트 | `python3 -B ./tools/playable.py --check`, `git diff --stat -- Tools/playable/route.json` 0줄 |
| AC4 | 진입점에 H절 멤버가 모두 있고 흐름 경로 `f6-feedback-flows.json`의 `L1`~`LOBBY` 단계가 모두 통과한다 | 플레이 | `python3 -B ./tools/playable.py --route tools/playable/f6-feedback-flows.json` |
| AC5 | 피드백이 피해 계산을 바꾸지 않는다: 같은 시드·같은 걸음에서 기록을 받는 판과 F1·F3·F4 기존 시험의 HP·처치 값이 같고, 원정대 피해 한 번마다 기록 하나(`taken`·전후 HP 일치), 몬스터 피해 한 번마다 기록 하나(준 값·좌표 일치), 이미 쓰러진 몬스터에 대한 호출은 기록이 없다 | 자동 테스트 | `CombatFeedbackRecordTests`, 기존 `PartyServiceTests`·`CombatDamageTests`·`MonsterBehaviorTests`·`CombatDeterminismTests` 통과 |
| AC6 | F1 흐름 경로가 피드백이 붙은 뒤에도 파일 변경 없이 통과한다 | 플레이 | `python3 -B ./tools/playable.py --route tools/playable/f1-flows.json`, `git diff --stat -- Tools/playable/f1-flows.json` 0줄 |
| AC7 | 피해 숫자: 같은 프레임에 같은 몬스터가 3번 맞으면 숫자 하나(합의 반올림, 최소 1)이고, 나이 0에서 위치 0·불투명, `lifetime`에서 `risePx`만큼 위·투명 0, `fadeStartRatio` 전에는 불투명이다. 동시 수가 `maxActive`면 새 숫자가 가장 오래된 숫자를 대신한다 | 자동 테스트 | `DamageNumberMotionTests`(합치기·반올림·움직임·상한 교체) |
| AC8 | 빨간 구간: HP 100/100에서 30을 잃으면 빨간 구간 0.7~1.0이 `holdSeconds` 동안 그대로이고, 그 뒤 `shrinkSeconds` 안에 0이 된다. 머무는 중 다시 20을 잃으면 0.5~1.0이 되고 머무름이 처음부터다. 회복·부활하면 바로 없어지고, 보호막이 다 흡수해 HP가 그대로면 생기지 않는다 | 자동 테스트 | `HpLossTrailTests` |
| AC9 | 흔들림·진동: 피격이 `minInterval` 안에 여러 번 와도 흔들림 시작·진동 호출은 한 번씩이고, 흔들림 오프셋은 늘 1/48 유닛의 정수배이며 크기 ≤ `amplitudePx`, `seconds` 뒤 0이다. 카메라를 매 프레임 다시 쓰는 경우와 한 번만 쓰는 경우 모두 흔들림이 끝나면 카메라가 원래 기준 위치로 돌아온다. `haptic.enabled`가 거짓이면 호출 수가 늘지 않고, 에디터에서는 `Handheld.Vibrate`를 부르지 않고 호출 수만 센다 | 자동 테스트 | `ShakeMathTests`, `HapticServiceTests` |
| AC10 | 몬스터 HP 막대 규칙: 중장갑은 거리와 상관없이 보이고, 접촉형은 붙었을 때만, 사격형은 사거리 안일 때만, 돌진형은 Windup·Dash 중에만, 자폭형은 Fuse 중에만 보이며, 공격이 끝난 뒤 `lingerSeconds` 동안 더 보인다. 그 밖의 몬스터(멀리 있는 다친 슬라임 등)는 보이지 않는다. 후보가 `maxActive`를 넘으면 강한 적 먼저, 다음은 가까운 순이다 | 자동 테스트 | `MonsterHpBarRuleTests`(행동 5종 × 공격 중·아님, 머무름, 상한 순서) |
| AC11 | 전투 화면에서 맞은 몬스터 위에 흰 글자·검은 테두리 피해 숫자가 떠오르고, 맞은 대원의 머리 위·하단 HP 막대에 빨간 구간이 보이며, 중장갑과 공격 중인 몬스터에만 HP 막대가 있고 멀리 있는 몬스터에는 없다. 화면을 덮는 연출은 없다 | 플레이 | 흐름 `HIT`·`HURT`·`MHP`·`STRONG` 캡처를 열어 항목별로 확인하고 본 캡처 목록을 보고에 적음 |
| AC12 | 적이 300마리 이상인 장면에서 동시에 보이는 피해 숫자가 `maxActive`를 넘지 않고 에디터 프레임 시간 95백분위가 16.7ms 이하다(M7 기준, 살아 있는 몬스터 최대 수와 함께 기록) | 플레이 | 흐름 `LOAD` 단계 통과, 보고에 `FrameMsP95SinceMark`·`EnemyPeakSinceMark` 값 |
| AC13 | 전투를 벗어나면(정비·결과·로비) 피해 숫자·몬스터 HP 막대·빨간 구간·흔들림이 모두 없어지고, 새 판은 피드백 셈 0으로 시작한다 | 플레이 | 흐름 `PREPCLEAR`·`RESET` 단계 통과 |
| AC14 | 노치 기기에서 하단 HP 막대(빨간 구간 포함)가 가려지지 않고, 16:9·19.5:9·21:9·4:3 가로 화면에서 피해 숫자·HP 막대가 잘리거나 HUD와 겹쳐 읽을 수 없게 되지 않는다 | 플레이 | 흐름 `HIT`의 `capture: "all"` 4장을 열어 봄 + Device Simulator 노치 기기 1대(가로) 전투 캡처 |
| AC15 | 피격 연출(빨간 구간·흔들림) 세기가 맞는지, 피해 숫자가 무리 속에서 읽히는지 사람이 플레이해서 판정하고, 고칠 값은 `feedback_rules.json`만 바꿔 맞춘다(게임 기획서 10절) | 사람 | 에디터에서 한 판 플레이 → 의견을 값으로 옮김. 판정 전에는 "사람 판정 대기"로 보고 |
| AC16 | 기기에서 원정대가 맞을 때 진동이 오고 너무 잦거나 세지 않다 | 사람 | 기기 빌드(게임 기획서 12절 나중)가 생긴 뒤 Android·iOS 각 1대에서 확인. 그 전에는 "기기 미확인"으로 보고하고 에디터 호출 기록(AC4 `HURT`의 `HapticCount`)만 적는다 |

### 기존 테스트
기대가 바뀌는 기존 테스트 없음 — 이 기능은 피해·HP·처치·시드 결과를 바꾸지 않고 기록만 더한다. 건드리는 흐름의 테스트 `PartyServiceTests`(F1 AC12 받는 피해), `PlaceholderCombatTests`(F1 AC13, F3·F4가 옮김), `CombatDamageTests`(F3 AC11), `MonsterBehaviorTests`(F4 AC5~AC8)의 마지막 기대를 기획서 문장 기준으로 대조했다. 테스트 파일은 아직 레포에 없다(F1 구현 중, 2026-11-15 확인) — 구현 단계는 실제 파일의 마지막 기대를 다시 대조하고, 달라지는 것이 나오면 멈추고 보고한다.
- `PartyService`·`MonsterService` 생성자에 인자가 하나 늘어 그 테스트들의 준비 코드(생성 줄)는 바뀐다. 기대값은 바꾸지 않는다.

기대 변경 허용(이 목록에 있는 것만 구현 단계가 기대값을 고칠 수 있다):

| 테스트 | 지금 기대(끝 상태·값) | 새 기대 | 이유(기획서 절) |
|---|---|---|---|
| (없음) | | | |

## 열린 질문
없음. 작은 갈림길은 아래 가정으로 정했다(승인 때 함께 확인).

### 가정
| 번호 | 가정 | 틀리면 바뀌는 것 |
|---|---|---|
| FA1 | 피해 숫자는 몬스터가 받은 피해에만 낸다(예전과 같음). 원정대 피해는 빨간 구간·흔들림·진동으로 보인다. 회복 숫자는 S9에 없어 내지 않는다 | B절, AC7·AC11 |
| FA2 | "공격 중"은 F4 상태로: 접촉·중장갑 = 붙음, 사격 = 사거리 안, 돌진 = Windup·Dash, 자폭 = Fuse. 끝난 뒤 1초 머무름 | E절, AC10 |
| FA3 | 빨간 강조는 원정대 HP 막대(머리 위·하단)에만. 몬스터 HP 막대에는 없다 | C·E절 |
| FA4 | 흔들림 = 카메라 정수 픽셀 오프셋 2px·0.15초·0.25초 간격, 원정대가 맞을 때만(몬스터를 때릴 때는 없음) | D절, 데이터 |
| FA5 | 진동은 Unity 기본 `Handheld.Vibrate()`(세기 고정, 새 패키지 없음). 기기에서 너무 세면 약한 진동(기기별 네이티브)은 후속 항목으로 올린다 | D절, AC16 |
| FA6 | 피해 숫자 동시 상한 40·몬스터 HP 막대 상한 40(출발값), 꽉 차면 오래된 숫자를 대신한다. 숫자 글꼴은 TMP(ugui 2.0 내장) 숫자 전용 자산 | B·E절, AC12 |
| FA7 | 흔들림 2px는 F2 들판 경계 안이라 들판 밖이 보이지 않는다. F2 흐름의 `CameraError` 확인 단계는 적 수 배율 0이라 피격·흔들림이 없어 영향이 없다 | D·G절 |
| FA8 | 구현 기준 코드는 F1 + F3 + F4가 합쳐진 상태(게임 기획서 7절 앞선 기능). 피해 사실은 F1 피해 적용 지점 두 곳에서 받는다 | A절 |
