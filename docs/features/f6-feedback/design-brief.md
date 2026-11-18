# F6 전투 피드백 설계 브리핑

- 상태: 구현 완료(설계 검토 FAIL → 수정 확인 PASS)
- 규모: L2 — 새 도메인(`Features/Feedback/`), 새 데이터 파일 스키마(`Feedback/feedback_rules.json`), 피해 적용 지점에 기록 줄(상태 소유권), DI 등록
- 기준: 승인 기획서 [plan.md](plan.md)(A~H절, 데이터 스키마, AC1~AC16), 시작 커밋 b27e7f1(F1~F5 합쳐진 상태)

## 이번에 사용할 수 있게 될 산출물·완료 조건
- 전투 화면(V2)에 몬스터 피해 숫자, 원정대 HP 막대(머리 위·하단) 빨간 구간, 카메라 정수 픽셀 흔들림, 진동 호출(에디터는 셈만), 강한 적·공격 중인 적 HP 막대.
- 완료 조건: AC1~AC14 확인(결과는 핸드오프 criteria)(AC15 사람, AC16 기기는 not-run), 기존 EditMode 189개 기대값 그대로 통과.

## 요구와 제외 범위
- 제외: 회복 숫자, 화면을 덮는 연출, 모든 몬스터 HP 막대, 몬스터 막대의 빨간 구간, 기기별 약한 진동(FA5 후속), `RunService`·`GameEvent.cs`·`UI_BattleHudView`·`BattleHudPresenter`·`placeholder_combat.json`·`route.json` 수정.

## 기존 코드와 데이터 소유처 (실제 코드 확인 결과)
- 원정대 피해: `PartyService.ApplyDamage(PartyMember, float)` 한 곳(접촉·중장갑·돌진·자폭·적 투사체가 모두 부름). 기획서와 같음.
- **몬스터 피해: 기획서와 다르다.** `MonsterService`에 피해 적용 메서드가 없고 `CombatService`가 다섯 곳에서 `monster.Hp -= x`를 직접 쓴다(파동 `StepPulses`, 부채꼴 `HitCone`, 연쇄 `HitChain`, 투사체 `StepProjectiles`, 폭발 `Explode`). 다섯 곳 모두 호출 전에 `IsAlive`를 본다.
- 몬스터 번호 = `Monster.Id`, 반지름 = `Monster.Data.Radius`, 상태 = `Monster.Phase`(F4 `MonsterPhase`), 접촉 거리 = `Data.Radius + Data.ContactGap`(+ `ContactBehavior`의 여유 0.001), 사격 사거리 = `Data.Projectile.Range`.
- 카메라: `WorldPresenter.LateTick`이 매 프레임 `Camera.transform.position`을 원정대 중심으로 쓴다.
- 하단 HP 막대: `UI_BattleHudView.MemberStatusViews`(읽기만), 머리 위: `IReadOnlyList<MemberView>`(LifetimeScope 등록 목록).
- 판 상태: `RunModel.Phase`·`IsPaused`·`RunCount`(R3).

## 변경할 파일과 변경 전 → 후
### 겹치는 파일 (줄·칸만)
| 파일 | 변경 |
|---|---|
| `PartyService.cs` | 생성자 인자 `CombatFeedbackModel`. `ApplyDamage` 계산 세 줄 그대로, 앞에 `hpBefore` 한 줄, 끝에 `AddMemberHit(IndexOf(member), received, hpBefore, member.Hp)` 한 줄. `received`(방어 뺀 받음, 보호막 흡수 전) = 기획서의 `taken`. 대원 번호는 `PartyModel.MemberList` 안 위치(private `IndexOf`, 4명) |
| `MonsterService.cs` | 생성자 인자 `CombatFeedbackModel`. 새 메서드 `ApplyDamage(Monster, float amount)`: 이미 쓰러졌으면 아무것도 안 함, 아니면 `monster.Hp -= amount` + `AddMonsterHit(monster.Id, amount, monster.Position, monster.Data.Radius)` |
| `CombatService.cs` (**기획서 F절 표에 없음**) | 다섯 곳의 `monster.Hp -= x;`를 `_monsterService.ApplyDamage(monster, x);`로 바꾼다(같은 값·같은 순서, `_monsterService`는 이미 주입돼 있음). 계산·순서 그대로 |
| `UI_MemberStatusView.cs` | `[SerializeField] UI_HpLossView hpLossView` + 읽기 `HpLossView` |
| `MemberView.cs` | `[SerializeField] HpLossBarView hpLossBarView` + 읽기 `HpLossBarView` |
| `GameSceneLifetimeScope.cs` | F절 등록 줄, 프리팹 칸 2개 + 부모 칸 2개(`EnemyShotViewFactory`와 같은 `WithParameter` 방식) |
| `PlayableProbe.cs` | H절 멤버 + 같은 파일 `PlayableFrameSampler`(에디터 전용 MonoBehaviour) |
| `Game.asmdef` (**F절 표에 없음**) | 참조에 `Unity.TextMeshPro` 한 줄(월드 TMP 피해 숫자, ugui 2.0 안의 어셈블리) |
| `Tools/check_data.py` | 검사 함수 하나 + 호출 + self-test 3종 |
| 시험 준비 코드 `TestRunContext.cs` | `PartyService`·`MonsterService` 생성 줄에 `CombatFeedbackModel` 인자(기대값 변경 없음) |

### 새 파일 (`02.Scripts/Features/Feedback/`)
- `Data/FeedbackRulesData.cs` — `[Serializable]` 묶음 5개(DamageNumber·HpLoss·Shake·Haptic·MonsterHp), private `[SerializeField]` + 읽기 속성(기존 `MonsterData` 방식). 색은 문자열, 읽는 쪽이 `PlaceholderSpriteFactory.ParseColor`.
- `Registry/FeedbackRulesRepository.cs` — `ResourceService.LoadJson<FeedbackRulesData>("Data/Feedback/feedback_rules")`, `Rules` 하나(`CombatRulesRepository`와 같은 모양).
- `Model/CombatFeedbackModel.cs` — 상태만:
  - `MemberHitList`(`MemberHitRecord{MemberIndex, Taken, HpBefore, HpAfter}`), `MonsterHitList`(`MonsterHitRecord{MonsterId, Amount, Position, Radius}`): 미리 잡은 `List`. `AddMemberHit`·`AddMonsterHit`·`ClearMemberHits`·`ClearMonsterHits`.
  - 이번 판 셈: `DamageNumberShownTotal`, `DamageNumberPeak`, `ShakeCount` + 각 셈을 쓰는 메서드와 리셋.
  - `HasPendingHitReaction`: `AddMemberHit`에서 `taken > 0`이면 참, `ConsumeHitReaction()`이 읽고 거짓으로. → 원정대 기록을 두 Presenter(빨간 구간·흔들림)가 읽어도 **비우는 순서에 기대지 않는다**: 기록 목록은 `HpLossPresenter`만 비우고, 흔들림은 이 깃발만 쓴다. 몬스터 기록은 `DamageNumberPresenter`만 비운다.
- `Service/HapticService.cs` — `TryPlay(float now)`: 꺼짐이면 거짓, `now − last < minInterval`이면 거짓, 아니면 `PlayCount++`, `last = now`, 기기(`(UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR`)에서만 `Handheld.Vibrate()`. `ResetRun()`. 간격 판정을 서비스에 둔 이유: `HapticServiceTests`(간격·꺼짐·셈)가 한 클래스로 확인.
- `Utils/DamageNumberMotion.cs`(static 순수): `Merge(IReadOnlyList<MonsterHitRecord>, List<MonsterHitRecord> result, Dictionary<int,int> indexDict)` 같은 번호 합(자리 = 첫 기록), `GetValue(sum) = max(1, floor(sum + 0.5))`, `GetRise(age, rules)` 단위 = `risePx × age/lifetime ÷ 48`, `GetAlpha(age, rules)`, `GetEvictCount(active, max)`.
- `Utils/HpLossTrail.cs`(순수 클래스): `LossFrom`, `Hold`, `Hit(beforeRatio, afterRatio, holdSeconds)`, `Tick(dt, currentRatio, shrinkSeconds)`(HP가 지난 프레임보다 오르거나 `current ≥ lossFrom`이면 즉시 끝, 머무름 → 줄어듦은 남은 dt를 넘김, 줄어드는 속도 = 처음 길이 ÷ shrinkSeconds), `Reset(currentRatio)`, `Length`.
- `Utils/ShakeMath.cs`(static 순수): `GetOffset(remaining, seconds, amplitudePx, step)` → 축마다 `±round(amplitudePx × remaining/seconds)` 픽셀 ÷ 48(방향 (1,1)→(−1,−1)→(1,−1)→(−1,1) 순환, 난수 없음). "크기 ≤ amplitudePx"는 축별 크기로 읽는다. `ResolveBase(current, lastApplied, lastBase)`. `CanStart(now, lastStart, minInterval)`.
- `Utils/MonsterHpBarRule.cs`(static 순수): `IsAttacking(behavior, phase, distance, contactDistance, projectileRange)`(표 E절, 접촉·중장갑은 `ContactBehavior`와 같은 여유 0.001을 더한 `d ≤ radius + contactGap + 0.001` — 공격하는데 막대가 없는 경계를 만들지 않는다(DR-04), 시험에 경계값 포함), `IsCandidate(isStrong, isAttacking, now, lastAttack, linger)`, `SelectShown(List<MonsterHpBarCandidate>, max, HashSet<int> result)`(강한 적 먼저 → 거리 오름차순 → 번호).
- `Presenter/` 넷 — 모두 `ILateTickable, IInitializable, IDisposable`. `Initialize`에서 `Phase`(Battle 아님 → 모두 거둠)·`RunCount`(바뀜 → 거둠 + 이번 판 셈 0) 구독, `Dispose`에서 해제. 피드백 시계 dt = `Phase == Battle && !IsPaused ? Time.deltaTime : 0`(`Time.timeScale`을 따름 = 판정 경로 time_scale, 배속 `SpeedIndex`는 무관).
  - `DamageNumberPresenter`: 몬스터 기록 합치기 → 상한 넘으면 가장 오래된 것부터 거둠 → 만들기 → 나이·자리·투명도 갱신 → 수명 끝 거둠. 셈 `ShownTotal += 낸 수`, `Peak = max(Peak, 보이는 수)`.
  - `HpLossPresenter`: 대원마다 `HpLossTrail`(대원 수만큼 미리). 기록을 대원별 `Hit`에 넣고 비움 → `Tick` → 머리 위 `HpLossBarView.UpdateUI(current, 산 대원이면 lossFrom 아니면 current)`(쓰러지면 머리 위 막대는 F1이 숨기므로 빨간 구간도 숨김), 하단 `UI_HpLossView.UpdateUI(current, lossFrom)`. 진입점용 `MemberWithHpLoss`, `GetLossRatio(member)`.
  - `HitReactionPresenter`: 프레임마다 `ConsumeHitReaction()`을 **한 번** 읽어 피격이면 두 연출을 **따로** 판정한다 — 흔들림은 `ShakeMath.CanStart(clock, lastShakeStart, shake.minInterval)`이면 새로 시작(`ShakeCount++`), 진동은 그와 상관없이 `HapticService.TryPlay(clock)`(자기 `haptic.minInterval`). 흔들림 간격 때문에 진동이 빠지지 않는다(DR-02). 흔들림 중이면 `ShakeMath`로 오프셋, `CameraShakeView`에 기준 + 오프셋을 적용. `WorldPresenter` 뒤에 등록(같은 프레임 LateTick에서 뒤에 돈다). 순서가 바뀌어도 `ResolveBase` 덕분에 쌓이지 않는다. 진입점용 `IsShaking`.
  - `MonsterHpBarPresenter`: 산 몬스터마다 가장 가까운 산 대원 거리 → `IsAttacking`이면 마지막 공격 시각 = 시계, 후보 목록 → `SelectShown` → 보일 것은 등록부에 없으면 만들고 자리·채움 갱신, 안 보일 것·사라진 몬스터는 풀로. 마지막 공격 시각 사전은 사라진 몬스터 번호를 지운다. 진입점용 `AlwaysShownCount`, `CountRuleViolations()`(지금 모델로 후보를 다시 계산해 등록부와 대조), `HiddenByCapCount`.
- `View/` — `DamageNumberView`(`TextMeshPro`, `Initialize(value, color, fontSize, material)`·`UpdateView(position, alpha)`), `HpLossBarView`(월드 `SpriteRenderer` 한 장, `UpdateUI(currentRatio, lossFromRatio)`), `UI_HpLossView`(uGUI `Image` 앵커로 길이, `UpdateUI(currentRatio, lossFromRatio)`, 색은 Presenter가 `Initialize`로 데이터 색), `MonsterHpBarView`(바탕·채움 두 장), `CameraShakeView`(카메라 오브젝트 컴포넌트, `Position` 읽기·`SetPosition`).
- `Factory/` — `DamageNumberViewFactory`·`MonsterHpBarViewFactory`: 생성자에 프리팹·부모(LifetimeScope `WithParameter`), `PoolManager.Get` + 초기화. 피해 숫자 재질은 글꼴 재질을 한 번 복사해 Outline 색을 데이터 값으로 넣고 모든 숫자가 함께 쓴다(숫자마다 재질 사본을 만들지 않는다). 몬스터 막대 그림은 `PlaceholderSpriteFactory.CreateBox`로 한 번.
- `Registry/` — `DamageNumberViewRegistry`(만든 순서 `List`, 가장 오래된 것 = 앞), `MonsterHpBarViewRegistry`(번호 → View, `MonsterViewRegistry`와 같은 모양).

### 자산
- 프리팹: 기획서는 `03.Resources/Prefabs/Feedback/`, F1 규칙은 `03.Resources/Resources/Prefab/`(MonsterView·EnemyShotView 자리). F1 규칙을 따르라는 기획서 문장대로 **`03.Resources/Resources/Prefab/DamageNumberView.prefab`·`MonsterHpBarView.prefab`**에 두고 LifetimeScope 칸으로 연결(EnemyShotView와 같음).
- `03.Resources/Font/DamageNumber SDF.asset`: 에디터 명령으로 `TMP_FontAsset.CreateFontAsset(NotoSansKR)` → 숫자 0~9 추가 → 정적, 재질 Outline 검정. 장면·프리팹·글꼴은 `script eval`로 만든다(YAML 손편집 없음).
- `Resources/Data/Feedback/feedback_rules.json`: 스키마 출발값 그대로.
- 장면 `Main.unity`: 카메라에 `CameraShakeView`, 루트 `DamageNumbers`·`MonsterHpBars`, 하단 `UI_MemberStatusView` HP 막대 안 채움 뒤에 `HpLoss`(Image) 자식, 머리 위 `MemberView`(장면 오브젝트 4개)의 HP 막대 옆에 `HpLoss` 자식 SpriteRenderer(정렬은 채움과 같은 층, 채움 뒤 그려지게 sortingOrder 채움+? — 채움과 겹치지 않는 자리라 같은 값이어도 됨).

## 흐름 경로 `f6-feedback-flows.json` (DR-01)
- 실제 코드: `PressToLobby()` → `RunService.ReturnToLobby()`는 **Result에서만** 된다(그 밖은 경고만, `RunService.cs` 162행). 기획서 H절 표대로만 이으면 `STRONG`(MHP 뒤 Battle)·`LOAD`(PREPCLEAR 뒤 Prep일 수 있음)·`RESET`(LOAD 뒤 Battle)·`LOBBY`(RESET 뒤 Battle)의 `PressToLobby`가 거부돼 새 판이 시작되지 않는다.
- 해결: 기획서 단계(`L1`~`LOBBY`)의 do·wait·expect·capture는 **한 글자도 바꾸지 않고**, 그 앞에 게임 흐름으로 Result까지 가는 연결 단계 넷을 끼운다. 연결 단계는 확인(expect)이 없고 진입점 조작은 이미 있는 것(`Time.timeScale`, `StartBot(AutoNext)`)만 쓴다.

| 끼우는 자리 | id | do | wait |
|---|---|---|---|
| MHP → STRONG | `HITEND` | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.AutoNext);` | `Screen == "Result"`(timeout 180) |
| PREPCLEAR → LOAD | `STRONGEND` | `UnityEngine.Time.timeScale = 20f;`(봇은 STRONG의 AutoNext 그대로) | `Screen == "Result"`(timeout 180) |
| LOAD → RESET | `LOADEND` | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.AutoNext);` | `Screen == "Result"`(timeout 180) |
| RESET → LOBBY | `RESETEND` | `UnityEngine.Time.timeScale = 20f; StartBot(BotMode.AutoNext);` | `Screen == "Result"`(timeout 180) |

- 기준 문서와 다른 점(핸드오프에 적음): 기획서 H절 흐름 표에 연결 단계 4개 추가. 기획서 H절 자체의 정정은 이 단계가 고칠 수 없어 열린 질문으로 올린다.
- 후속(DR-03): `LOAD`의 p95는 기획서 식대로 출발 표시 뒤 전체 구간. "300마리 이후 구간만"으로 좁히는 것은 기획서 확인 식을 바꾸는 일이라 후속 제안으로 남긴다. 보고에 표본 수를 함께 적는다.

## 재사용 후보와 신설 이유
- 재사용: `PoolManager`, `ResourceService.LoadJson`, `PlaceholderSpriteFactory`(CreateBox·ParseColor), `IsoProjection.FloorToWorld`, `PartyService.FindNearestAlive`, `EnemyShotViewFactory` 방식(프리팹·부모 주입).
- `EventBus`를 쓰지 않는다(Service에 EventBus 금지 규칙, 기획서 A절).

## 실패·취소·생사·전환 경계
- Battle → Prep/Result/Lobby, 또는 `RunCount` 바뀜: 숫자·몬스터 막대 모두 풀로, 빨간 구간 `Reset(current)`, 흔들림 끝 + 카메라 기준 위치, 남은 기록·깃발 버림. `RunCount` 바뀜이면 이번 판 셈 0(숫자 셈, 흔들림 셈, `HapticService.ResetRun`).
- 일시정지: 시계 0 → 숫자·빨간 구간·흔들림이 멈춘 채 남는다.
- 같은 프레임 로비 → 출발: 구독이 동기로 돌아 거둔 뒤 새 판 시작.
- 몬스터가 같은 프레임에 맞고 쓰러짐: 기록에 자리·반지름이 있어 숫자는 나온다. 막대는 다음 LateTick에 풀로.
- 쓰러진 대원: 하단 막대는 빨간 구간이 줄어들 때까지 보이고, 머리 위는 F1이 막대를 숨기므로 빨간 구간도 숨김. 부활(HP 오름) → 즉시 끝.

## 데이터 호환성과 복구 방법
- 새 파일 하나, 세이브 없음. 값 0(`amplitudePx`), `haptic.enabled = false`로 연출을 끈다. `check_data.py`가 범위·색·행동 이름을 막는다.

## 최소 필수 검증
- 계산 불변(AC5): 기존 `PartyServiceTests`·`CombatDamageTests`·`MonsterBehaviorTests`·`CombatDeterminismTests`·`PlaceholderCombatTests` 기대값 그대로 + `CombatFeedbackRecordTests`(기록 하나씩, 값·좌표 일치, 쓰러진 몬스터 기록 없음).
- 순수 계산(AC7~AC10): 기획서의 시험 파일 이름 그대로.
- 판정 경로: `--until G2`, `f1-flows.json`, 새 `f6-feedback-flows.json`(시드·배율은 실제 전투로 찾음), 캡처 직접 열기 + Device Simulator 노치.

## 초기 실제 연결
- 데이터 → 모델 → 숫자 하나: 웨이브 1 시드 1에서 첫 무기 피해 → `DamageNumberCount ≥ 1` → 캡처.

## 검사 확대/도구 신설 조건
- 새 도구 없음. 진입점 멤버와 `PlayableFrameSampler`(기획서 H절)만.

## 알려진 환경 차단
- 같은 컴퓨터에 다른 프로젝트 에디터가 떠 있다 — 모든 `unity` 명령에 `--project-path`.

## 미정 사항
- 없음(갈림길은 위 "기획서와 다름" 세 곳: 몬스터 피해 기록 자리, `CombatService` 다섯 줄 치환, `Game.asmdef` 참조, 프리팹 폴더).

## 설계 검토 결과와 해소한 지적
- 1차(Codex design-reviewer): FAIL — 필수 DR-01(흐름 경로의 PressToLobby가 Result 밖에서 거부됨) → 위 "흐름 경로" 절로 해소(연결 단계 4개, 기획서 단계 식 무변경). 후속 DR-02(흔들림·진동 간격 독립) → HitReactionPresenter 문장에 명시, DR-04(접촉 여유) → MonsterHpBarRule 문장에 명시·경계 시험, DR-03(부하 구간) → 후속으로 남김.
- 수정 확인(Codex design-reviewer): PASS — DR-01·02·04 설계상 해소, DR-03 후속 유지.
- 구현 중 더한 차이: 피해 숫자·몬스터 HP 막대 자리는 spec-reviewer 지적으로 기획서 식(반지름 × unitScale + offsetPx) 그대로 되돌렸다. 몸통 그림이 발 피벗이라 이 높이는 몸통 위쪽에 걸친다 — 바꿀지는 열린 질문. `HapticService`는 `HapticRulesData`를 직접 받는다(꺼짐 시험용, LifetimeScope가 저장소에서 꺼내 등록).
