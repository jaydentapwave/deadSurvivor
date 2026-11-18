# F4 몬스터 5종과 웨이브 수치 — 설계 브리핑

- 상태: 구현 기준 (설계 검토 FAIL → 수정 확인 FAIL: D2 해소, D1 대기 시간은 기획서 A절·AC7 충돌로 열린 질문)
- 규모: L2 — 새 데이터 스키마(`Monster/`, `Spawn/`, 웨이브 네 칸), `MonsterModel` 상태 확장(몬스터 행동 상태·적 투사체·웨이브 셈), DI 등록 추가, F1 임시 칸 삭제
- 이번에 쓸 수 있게 될 산출물·완료 조건: 기획서 `plan.md` "클라 변경" 중 장면·프리팹을 뺀 전체(코드·데이터·테스트·경로·검사). 완료 조건은 기획서 AC1~AC20. 이 실행(worktree, 에디터 없음)은 파이썬 검사로 AC2·AC3·AC4·AC14·AC15를 확인하고, 나머지는 합친 뒤 루트 실행이 에디터로 확인한다.
- 요구와 제외 범위: 기획서 적용안 A~G. 제외 — 몬스터 그림, F2 이동·진형, F3 아이템 발동, F5 가방, F6 몬스터 HP·피해 숫자, HUD View·Presenter, 장면 `Main.unity`·프리팹 YAML(루트 실행 몫)
- 관련 기획: `docs/features/f4-monsters/plan.md`(승인), `notes.md`, F1 `docs/features/f1-skeleton/plan.md` E·H절, 게임 기획서 9절(M2·M3·M5·M7)

## 기존 코드와 데이터 소유처 (기준 커밋 92b4a56)

- `MonsterModel`(일반 클래스): 살아 있는 적 목록. 쓰는 쪽 `MonsterService`(생성·이동·제거), `BasicWeaponService`(HP 깎기)
- `MonsterService`: 임시 적 한 종류의 생성·이동·접촉. `RunService.Step`이 ③ `TickSpawn(dt, elapsed, isBurningTime, burningStart)`, ④ 뒤 `RemoveDead()`(처치 수), ⑤ `TickMove(dt)`, `BeginRun(seed)`, `BeginWave(waveSeconds, scale)`, `ClearMonsters()`를 부른다. `WorldPresenter`가 `SetViewHalfExtent`, HUD·진입점이 `AliveCount`
- `PlaceholderCombatData`(`Run/placeholder_combat.json`): `memberSpots`(F2), `weapon`(F3), `enemy`·`spawnCount`·`spawnMargin`·`burningIntervalScale`(F4가 지움)
- `WorldPresenter`(F2가 고침): 적 그림 하나를 `_combatData.Enemy`의 반지름·색으로 만들어 `MonsterViewFactory.Create(id, sprite, null)`에 넘긴다
- `MonsterView` 프리팹: `Resources/Prefab/MonsterView`(루트에 SpriteRenderer 하나), `MonsterViewFactory`가 `ResourceService.Load` + `PoolManager.Get`

## 변경할 파일과 변경 전 → 후

데이터(`Assets/Game/03.Resources/Resources/Data/`)
- 새 `Monster/{slime,mushroom,boar,rock,puffer}.json`, `Spawn/spawn_rules.json` — 값은 기획서 데이터 스키마 표
- `Wave/wave01~10.json` — `spawnCount`·`hpScale`·`damageScale`·`typeWeights` 추가(대표 1·3·6, 나머지 복사)
- `Run/placeholder_combat.json` — 네 칸 삭제

데이터 형(`Features/Monster/Data/`): `MonsterData`(+ `MonsterContactData`·`MonsterProjectileData`·`MonsterChargeData`·`MonsterBombData`, `behavior` 문자열은 `ISerializationCallbackReceiver.OnAfterDeserialize`에서 `MonsterBehavior`로 한 번 풀고 못 풀면 오류 로그), `MonsterBehavior`·`MonsterPhase` 열거형, `TypeWeightData`, `SpawnRulesData`(+ `SpawnHeadingData`·`SpawnRecycleData`). `WaveData`에 네 칸, `PlaceholderCombatData`에서 네 필드·`PlaceholderEnemyData` 삭제.

상태 소유 — `MonsterModel`(일반 클래스, R3 없음. 1/30초마다 바뀌는 값이라 F1 `PartyModel`과 같이 Presenter가 프레임마다 읽음)
- 몬스터 목록, 적 투사체 목록(`EnemyShot`: 번호·좌표·방향·속도·피해·이동 거리·최대 이동·명중 거리·색·IsAlive)
- 보이는 바닥 반 크기(`ViewHalfExtent`, `MonsterService.SetViewHalfExtent`가 씀)
- 이번 웨이브 셈: 정규 생성 수, 버닝 추가 수, 행동별 정규 생성 수. **`Clear()`(몬스터·투사체 비우기)는 셈을 지우지 않는다** — 웨이브 끝·판 끝에 `RunService`가 `ClearMonsters()` 뒤 `SetPhase`를 하므로 기록기가 그 순간 셈을 읽는다. 셈은 다음 `BeginWave`에서만 0
- 생성 상태(목표 수·간격·시계·버닝 각·Bomb 무리 셈과 각), 재배치 상태(대기 시계, 지난 원정대 중심, 원정대 이동 방향)
- `Monster`: 번호, `MonsterData` 참조(행동·수치), 좌표, HP·최대 HP, 피해(웨이브 배수 곱함), 공격 대기, `MonsterPhase`, 상태 시계, 돌진 방향, 무리 방향, 밖 시간, 이번 돌진에 맞은 대원 목록. 생성자 `Monster(int id, MonsterData data, Vector2 position, float hp, float damage, float attackClock)`(F1의 3인자 생성자를 바꿈 — 테스트만 영향)
- 쓰는 쪽: 아래 몬스터 서비스들 + `BasicWeaponService`(HP, 그대로). 나머지(Presenter·진입점·기록기)는 읽기만

서비스(`Features/Monster/Service/`, 모두 Scoped)
- `MonsterRepository`(Registry/): `Data/Monster` 전부 읽어 id 사전, `Data/Spawn/spawn_rules` 읽기. `Get(id)`는 없으면 오류 로그 후 null
- `MonsterSpawnService`(MonsterModel, PartyModel, MonsterRepository, RunRulesData): 생성 전용 난수 두 갈래(`BeginRun(seed)`에서 `new Random(seed)`로 두 시드를 뽑아 유형·자리 `System.Random` 둘), `BeginWave(WaveData, scale)`, `Tick(dt, elapsed, isBurningTime, burningStart)`(정규 → 버닝), `PickHeadingAngle()`(자리 난수, 재배치도 씀). 몬스터 번호는 판이 바뀌어도 이어서 늘림(F1 규칙). 행동별 셈은 정규 생성만(M3 데이터 판정 = `typeWeights` 기준과 같게)
- `MonsterRecycleService`(MonsterModel, PartyModel, MonsterRepository, MonsterSpawnService): `BeginWave()`(지난 중심 = 지금 중심, 방향 0, 대기 0), `UpdatePartyHeading()`(중심 변화, 길이 < 1e-4면 0), `Tick(dt)`(기획서 B절 재배치. 오래된 순 정렬은 같은 값이면 목록 순서 유지. 옮긴 몬스터는 밖 시간 0, 대기 = max(minAttackDelay, 대기), 무리 방향 0 — 예전 `Recycle`과 같음)
- `MonsterBehaviorService`(MonsterModel, PartyService, `IReadOnlyList<IMonsterBehavior>`): 산 몬스터를 목록 순서로 — 대기 −= dt, 표적 = `PartyService.FindNearestAlive`, 없으면 건너뜀, 행동에 맞는 `IMonsterBehavior.Tick(monster, target, dt)`. 행동 객체는 `CanHandle(MonsterBehavior)`로 고름(생성 때 열거형 → 행동 배열 표로 한 번 만듦)
- `IMonsterBehavior`(Interface/) + `Service/Behavior/`: `ContactBehavior`(Contact·Armored), `ProjectileBehavior`(MonsterModel에 투사체 추가), `ChargeBehavior`(집결: MonsterModel 목록에서 후보, PartyService 피해·표적), `BombBehavior`(무리: MonsterModel, 폭발: PartyModel 대원 + PartyService 피해). 규칙은 기획서 A절 표 그대로, 예전 `UpdateEnemy`·`Rally`·`BomberMove` 순서를 따른다. 자폭은 점화 끝에 `Hp = 0`으로 두고 ⑤의 정리에서 빠진다(④의 처치 셈을 거치지 않아 처치 수에 안 듦)
- `EnemyShotService`(MonsterModel, PartyModel, PartyService): 이동·선분 명중(첫 산 대원 하나)·최대 이동
- `MonsterService`(F1, 이름·호출 자리 유지): 생성자를 (MonsterModel, MonsterSpawnService, MonsterRecycleService, MonsterBehaviorService, EnemyShotService)로 바꾸고 안쪽을 위임. `TickMove(dt)` = 원정대 방향 갱신 → 재배치 → 행동 → 투사체 → 죽은 몬스터·사라진 투사체 정리(기획서 A절 걸음 순서). `BeginWave`는 시그니처를 `BeginWave(WaveData waveData, float enemyCountScale)`로 바꾼다(`RunService` 한 줄: `waveData.Duration` → `waveData`) — 웨이브 배수·가중치가 필요해서
- `Utils/WeightedPick.Pick(IReadOnlyList<TypeWeightData>, double roll)` → monster id, `Utils/SegmentMath.Distance(a, b, p)` — 순수 static

표현
- `MonsterView`: SerializeField `bodyRenderer`(루트, F1) + `dashLineRenderer`·`blastAreaRenderer`(자식 둘, 새 칸 → 프리팹에 루트 실행이 넣음). `Initialize(bodySprite, lineSprite, areaSprite, dashLength, blastRadius, unitScale)` — 표시 둘 끔, 폭발 원 크기·위치(바닥 원 → 아이소 타원, 원 스프라이트 피벗이 아래라 −반높이 올림). `UpdateState(MonsterPhase, Vector2 dashDirection)` — Windup이면 선 켜고 방향·길이(`IsoProjection.FloorToWorld`) 맞춤, Fuse면 원 켬, 그 밖은 둘 다 끔. `UpdateView(worldPosition)` 그대로
- `MonsterViewFactory`: `Create(Monster monster, Transform parent)` — 몬스터 데이터 id별 몸통 마름모(F1과 같은 크기 식: 반지름 × 2 × unitScale × PPU, 색 = `color`)를 `PlaceholderSpriteFactory`로 한 번 만들어 둠, 선(흰 반투명 상자, 피벗 왼쪽 가운데)·원(흰 반투명 원) 스프라이트 하나씩 공유
- `WorldPresenter`(F2 파일, 최소 수정 — 아래 "기준 문서와 다른 점"): `_monsterSprite` 필드와 `_combatData.Enemy`로 그림을 만드는 3줄 삭제, `Create(monster.Id, _monsterSprite, null)` → `Create(monster, null)`. `_combatData` 필드·생성자 인자는 F2 충돌을 줄이려고 그대로 둠
- `EnemyShotView`(SpriteRenderer 하나, `Initialize(sprite)`, `UpdateView(worldPosition)`), `EnemyShotViewFactory`(PoolManager.Get, 색별 작은 원 스프라이트, 생성자 인자로 프리팹·부모 Transform — LifetimeScope SerializeField 두 칸을 `WithParameter`로 넘김), `EnemyShotViewRegistry`(번호 → View)
- `MonsterStatePresenter : ITickable`(산 몬스터 중 View가 있는 것에 `UpdateState`), `EnemyShotPresenter : ITickable`(산 투사체 View 동기화, `IsoProjection`)

DI(`GameSceneLifetimeScope`, 더하기만): `MonsterRepository`, `MonsterSpawnService`, `MonsterRecycleService`, `MonsterBehaviorService`, 행동 4개(`.As<IMonsterBehavior>()`), `EnemyShotService`, `EnemyShotViewFactory`(`WithParameter(enemyShotPrefab).WithParameter(enemyShotParent)`), `EnemyShotViewRegistry`, 진입점 `MonsterStatePresenter`·`EnemyShotPresenter`. SerializeField `enemyShotPrefab`(EnemyShotView), `enemyShotParent`(Transform). 의존 방향: Presenter → Service/Factory → Model·Repository. 행동 객체는 서비스를 부르지 않고 `PartyService`(F1)만 부름 — 순환 없음(`PartyService`는 몬스터를 모름)

판정(`App/`, 모두 `#if UNITY_EDITOR`)
- `PlayableProbe`: 기획서 E절 멤버 10개 추가. `PlayableBot`에 읽기 전용 `Mode` 속성 하나 추가(기록기가 봇 깃발을 읽으려고. 동작 변경 없음)
- `PlayableMetricsRecorder`(MonoBehaviour, `StartWaveMetrics`·`UseRealTimeAtWave`가 없으면 만든다): `RunModel.Phase` 구독. Battle 진입 때 `RunCount`가 바뀌었으면 새 판(시드·배율은 `RunSettingsModel`, 기록 시작 웨이브 = 지금 웨이브). Battle에서 바뀌는 순간 그 웨이브 {끝까지 갔나 = Lose가 아님, 정규 수, 데이터 기대 수 = round(spawnCount × 배율), 버닝 수, 행동별 수} 추가, 봇 깃발 OR, 파일 전체 다시 쓰기(JsonUtility). `Update`에서 전투 중 살아 있는 적 최대 수, 마지막 웨이브 버닝 타임이고 `Time.timeScale == 1`이면 `unscaledDeltaTime` 표본. `UseRealTimeAtWave(w)`는 다음 Battle 진입 때 Wave == w면 timeScale 1·runInBackground true(한 번)
- 기록 파일: `{runs:[{seed, enemyCountScale, firstWave, botModes, outcome, wavesSurvived, maxAliveEnemies, waves:[{wave, reachedEnd, spawned, expected, burningSpawned, contact, projectile, charge, armored, bomb}], burning:{p95Ms, meanMs, maxMs, samples, maxAlive}}]}`

도구
- `Tools/check_data.py`: `check_monsters(root, waves, problems)` 함수 하나 + 호출 한 줄, 자체 시험 6종 추가, placeholder 네 칸 검사 삭제
- `Tools/wave_targets.py`: 기획서 D절(목표표는 파일 머리, 종료 코드 0/1/2, `--waves`, `--require`, `--metrics`, `--self-test`)
- `Tools/playable/route.json`: G5·G7만 기획서 E절 표대로. `Tools/playable/f4-monsters-flows.json`: E절 흐름 표

## 재사용 후보와 신설 이유
- 생성·이동은 F1 `MonsterService`의 규칙(간격 식, 버닝 낼 수 식, 접촉 거리 여유)을 그대로 옮겨 나눈다. 표적 찾기·피해는 F1 `PartyService.FindNearestAlive`·`ApplyDamage`를 쓴다
- 그림 풀·등록부는 F1 `MonsterViewFactory`·`MonsterViewRegistry` 짝을 투사체에 같은 방식으로
- 행동 인터페이스는 기획서가 정한 구조(행동 4 구현). 선분 거리는 돌진·투사체 두 곳이 써서 Utils로

## 실패·생사·전환 경계
- 산 대원이 없으면 몬스터는 멈춤(표적 null). 쓰러진 대원은 표적·피해·명중 대상이 아님
- 자폭 점화 중 무기로 HP 0 → ④에서 처치로 빠짐(폭발 없음, 처치 +1). 점화 끝 폭발 → `Hp = 0` → ⑤ 정리(처치 셈 없음)
- 웨이브 끝·판 끝·로비: `ClearMonsters`가 몬스터·투사체를 비움(셈은 남김). 다음 웨이브 `BeginWave`가 셈·시계·각·무리 셈·재배치 상태를 0으로
- 배율 0: 정규 목표 0 → 버닝 추가도 0(MA12)
- 데이터 오류(없는 몬스터 id): `MonsterRepository.Get`이 오류 로그 후 null → 생성하지 않고 오류 로그(데이터 검사가 막는 경우)

## 데이터 호환성과 복구
저장 없음(A9). 데이터 칸 추가·삭제는 같은 커밋의 데이터 검사가 막는다. 되돌리기는 커밋 되돌리기.

## 최소 필수 검증
- 이 실행: `check_data.py`(+ `--self-test`), `wave_targets.py --waves 1,3,6`·전체·`--self-test`, `playable.py --check`(두 경로), AC14 grep, algorithm-tester(WeightedPick·SegmentMath·생성 순서 실제 실행, 행동 상태 전환·재배치 hand-trace)
- 루트 실행: 컴파일, EditMode(`MonsterDataTests`·`MonsterSpawnTests`·`MonsterBehaviorTests`·`MonsterRecycleTests`·`MonsterViewStateTests` + F1 테스트 기대 변경 허용 목록), `playable.py --until G2`, 두 흐름 경로, 캡처

## 기존 테스트 대조(기획서 "기존 테스트")
- `PlaceholderCombatTests`: 임시 적 칸을 쓰는 준비 코드 → slime 데이터로 만든 몬스터. 손으로 놓는 몬스터는 `TestRunContext.CreateSlime(id, position)`(HP = slime `hp` 20, 피해 = `damage` 5, 공격 대기 0 — F1 임시 적 값과 같음)이라 기대값이 그대로다. 실제 한 판을 돌리는 `TenSecondsWithDefaultSettingsKillsAtLeastOne`만 웨이브 1 데이터(HP 20 × 0.7)로 돌고 기대 문장(처치 ≥ 1)은 그대로. `BurningTimeSpawnsExtra` → 버닝 추가가 slime·boar이고 정규 수에 안 듦. `CountSpawnsInWave`는 `BeginWave(waveData, scale)`로. `SameSeedSameResultAcrossFrameSplits` 기대 그대로
- `DataLoadTests.RunRulesAndPlaceholderCombatLoad`: `Enemy.Color`·`SpawnCount` 기대 → `spawn_rules`의 70·0.5, 웨이브 1 `spawnCount` 98
- `TestRunContext`(시험 도우미): 새 서비스 구성, `PlaceKillingContact`는 slime 데이터로 몬스터를 만듦. `RunServiceTests`는 이 도우미만 써서 기대 그대로
- 목록 밖에서 기대가 바뀌는 테스트는 없다(F1 테스트 전부 grep 확인)

## 기준 문서와 다른 점
- 클라 변경 "F1 `WorldPresenter`는 F2가 고치므로 건드리지 않는다" — `enemy` 칸 삭제(G절)로 `WorldPresenter`의 그림 만들기 3줄과 `Create` 호출 1줄을 고쳐야 컴파일된다. 그 4줄만 고친다
- `RunService`(목록에 없음) — `BeginWave` 호출 인자 한 줄
- `PlayableBot` — 읽기 전용 `Mode` 속성 한 줄
- 적 투사체 프리팹 연결: 기획서는 "LifetimeScope의 프리팹 참조 칸(F1 MonsterView 프리팹 연결 방식을 따른다)"인데 F1 MonsterView는 Resources 경로로 읽는다. 장면 항목(부모 오브젝트·LifetimeScope 프리팹 참조)을 따라 LifetimeScope SerializeField 두 칸으로 한다

## 알려진 환경 차단과 재개 조건
에디터 없음(F3가 루트 에디터 사용) → 컴파일·테스트·플레이·장면/프리팹 연결은 합친 뒤 루트 실행. `.meta`는 에디터가 만든다.

## 미정 사항
- 흐름 경로 LOAD·SHOW의 시드·배율은 출발값(기획서 E절). 에디터 없이 잴 수 없어 출발값 그대로 두고 루트 실행이 조정

## 설계 검토 결과와 해소한 지적
- 1회(Codex design-reviewer) VERDICT: FAIL. 필수 2:
  - D1 돌진 집결 수·대기: A절 "자신 + rallyRadius 안의 ... 다른 Charge를 가까운 순 최대 rallyMax마리" = 자신 포함 최대 rallyMax + 1 = 16(AC7과 같음, 예전 `Take(15)` + 지도자). 대기는 A절·예전 코드대로 Recover **들 때** `cooldown`(2초)이고 매 걸음 준다 → 회복(0.8초)이 끝난 뒤 남은 대기 약 1.2초. AC7 "회복 뒤 2초 동안 다시 모이지 않는다"는 "Recover에 든 뒤 2초"로 읽어 테스트(`ChargeCooldownAfterRecover`)를 쓴다. 기획서를 고칠 수 없으므로 열린 질문으로 올린다
  - D2 F1 테스트 준비: 위 "기존 테스트 대조"대로 손으로 놓는 몬스터는 F1 임시 적과 같은 값(HP 20·피해 5·대기 0)으로 만든다. 기대값 변경 없음
- 수정 확인(같은 검토자 1회) VERDICT: FAIL — D2 해소, D1 집결 수(16) 해소, D1 대기 시간 미해소: "AC7을 재해석해 테스트하는 것은 충돌 해소가 아니다. 회복 종료부터 2초를 보장하거나 승인권자 결정으로 A절·AC7을 일치시켜야 한다"
  - 처리: 기획서(승인본)를 고칠 수 없고 이 실행은 사람에게 물을 수 없다. 구현은 A절 본문·데이터 스키마(`cooldown` 2)·예전 코드(`DSGame.Combat.cs` Recover 진입 때 `e.cd = 2`)가 일치하는 쪽(Recover **진입** 때 대기 = `cooldown`)을 따르고, 핸드오프 열린 질문으로 올린다. 답이 "회복 끝부터 2초"면 `ChargeBehavior`의 대기 설정 한 줄을 Recover 끝으로 옮기고 테스트 기대 하나를 바꾸면 된다. 규칙상 3회째 검토는 열지 않는다
- 권고 S1(AC12): 테스트는 `RunService.AdvanceFrame` 경로(고정 걸음으로 쪼갬)로 버닝 구간이 있는 웨이브 6까지 돌려 본다
- 권고 S2(기록 시드): 실제 시드는 `RunService`가 내놓지 않는다(F1 코드를 넓히지 않음). 기록에는 설정 시드를 적고 0이면 "시작 시각 시드"라는 뜻으로 둔다
