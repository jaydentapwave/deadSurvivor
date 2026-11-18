# F4 몬스터 5종과 웨이브 수치 기획서

- 상태: 승인 (2026-11-15, 디렉터 확인 — 게임 시작 전 승인 범위 안, director-game autonomy 1절)
- 서버 필요: 아니오
- 관련 문서: [게임 기획서](../../game/gdd.md) F4(S3, C1, C3), 판정 단계 G2(이 기능에서 회귀 통과), G3·G5·G7(이 기능의 규칙이 지나가지만 F2·F5가 있어야 닿음 — 흐름 경로로 확인), 목표값 M2·M3·M5·M7. [F1 기획서](../f1-skeleton/plan.md) E절(대체할 임시 규칙)·G절(화면 경계)·H절(판정 진입점). 참고 분석 문서 없음. 예전 판 코드 `Assets/DeadSurvivor/Runtime/DSGame.Combat.cs`·`DSGame.Run.cs`·`DSData.cs`, 예전 값 `Assets/DeadSurvivor/Resources/DeadSurvivor/PrototypeData.json`(읽기 전용, 규칙만 옮김). 긴 근거는 [notes.md](notes.md)

## 목표
F1의 임시 적 한 종류를 행동이 다른 몬스터 5종(접촉·투사체·돌진·중장갑·자폭)과 웨이브 데이터(적 수·체력 배수·피해 배수·유형 비율)로 바꾼다. 화면 밖에서 나오고 멀어지면 다시 앞에 놓인다.
웨이브 10개·몬스터 5종의 수치는 콘텐츠 대기열이 데이터만 고쳐 채울 수 있게, 이 기능은 스키마·데이터 검사·대표 데이터·목표값 판정기(M2·M3·M5·M7)를 만든다.

## 참고 분석 요약
예전 판(같은 게임의 3D 구현) 코드에서 옮길 규칙. 모두 관찰이며 출처는 `DSGame.Combat.cs`의 해당 메서드다.

- 정규 생성: 웨이브마다 `count × countScale`(반올림)마리를 (웨이브 시간 − 4) ÷ (수 − 1) 간격으로 낸다. 유형은 웨이브의 유형 목록에서 고르게 뽑는다(목록에 같은 유형이 여러 번 들어 있어 사실상 가중치). HP·피해에 웨이브 배수를 곱한다 (관찰, `Simulate`·`Spawn`)
- 생성 방향: 원정대가 멈춰 있으면 무작위, 움직이면 앞 50%·왼쪽 15%·오른쪽 15%·뒤 20%에 ±45° 흔들림 (관찰, `Heading`)
- 자폭형은 6마리마다 새 방향을 뽑고 나머지는 그 방향 ±0.12rad에서 나와 무리로 온다 (관찰, `Spawn`)
- 버닝 타임 추가 생성: 정규 간격의 절반마다, 웨이브 시작 때 뽑은 각과 그 반대쪽을 번갈아, 일반 80%·돌진 20%. 정규 생성 수에 넣지 않는다 (관찰, `Burning`, `Spawn`의 `extra`)
- 재배치: 화면 반폭·반높이 + 300 밖에 있고 원정대 뒤쪽(또는 원정대 정지)인 접근 중 적이 3초 넘게 머물면, 0.5초에 최대 2마리를 화면 + 150 가장자리로 옮긴다 (관찰, `Recycle`)
- 접촉: 거리 > 반지름 + 5면 다가가고, 붙으면 공격 대기가 끝날 때 피해(대기 1초, 중장갑 1.8초). 처음 대기 1초 (관찰, `UpdateEnemy`, `DSData.cs` `Enemy.cd = 1`)
- 사격: 사거리 252. 252 × 0.92보다 멀면 다가가고 252 × 0.65보다 가까우면 물러난다. 사거리 안이면 2초마다 속도 180·최대 이동 378의 투사체, 선분 거리 15 안의 첫 대원에게 맞는다 (관찰, `UpdateEnemy`·`Simulate`)
- 돌진: 280 안이고 대기가 끝나면 300 안의 대기 끝난 돌진형 최대 15마리를 모아 0.8초 준비 → 각자 고정한 방향으로 0.7초 동안 속도 460 돌진(선분 거리 28 안의 대원마다 한 번 피해) → 0.8초 회복, 대기 2초. 붙어도 늘 집결이 먼저라 접촉 공격은 일어나지 않는다 (관찰, `Rally`·`UpdateEnemy`)
- 자폭: 260 안의 다른 자폭형과 응집 0.65·정렬 0.35·분리 1.8(38 안), 대상 300 안에 들면 무리 전체가 1.4초 동안 속도 225로 몰려온다. 65 안이면 1.2초 점화 뒤 90 안의 모든 대원에게 피해를 주고 사라진다(처치 수에 안 듦). 점화 전에 쓰러지면 터지지 않는다 (관찰, `BomberMove`·`UpdateEnemy`)
- 위협 = 살아 있는 적 수(적 투사체는 세지 않음) (관찰, `DSUI.cs` HUD `g.Enemies.Count`)

## 적용안

### 가져올 것 / 버릴 것 / 바꿀 것
- 가져올 것: 위 규칙과 예전 수치 전부(거리 단위 그대로, 게임 기획서 A5), 버닝 타임의 유형 비율, 웨이브 배수 곱하기, 자폭 무리 생성
- 버릴 것: 맵 내비게이션·시야선(`MapLine`·`EnemyAdvance`의 길 찾기), 문자열 유형·상태(`"charger"`, `"fuse"`), 반복되는 유형 목록(100칸) 표현
- 바꿀 것: 유형 목록 → 웨이브 파일의 가중치 배열, 행동 수치 → 몬스터 파일의 칸, 화면 크기 상수(1180×748) → F1 카메라가 알려 주는 보이는 바닥 영역, 원정대 입력 방향 → 원정대 중심의 걸음 사이 변화(F2 입력 모델에 기대지 않는다), 난수 → 한 판 시드에서 만든 생성 전용 두 갈래(유형·자리)

### 기존 시스템과의 관계
확장. F1이 만든 `MonsterModel`·`MonsterService`·`MonsterView`·`MonsterViewFactory`·`WaveData`·`Wave/wave<NN>.json`을 같은 파일·같은 키로 넓힌다. `MonsterService`는 F1의 호출 자리(`RunService.Step` ③ 적 생성, ⑤ 적 이동·공격)와 메서드 이름을 그대로 두고 안쪽을 아래 서비스로 나눈다. F1 코드는 아직 구현 중이라 이 기획서는 F1 기획서의 이름을 기준으로 쓴다. 구현 단계는 실제 F1 코드의 이름·시그니처에 맞추고, 다르면 그 차이를 보고에 적는다.

### A. 몬스터와 행동 (S3, C3)
행동은 코드, 수치는 데이터다. 행동 5종은 `MonsterBehavior` 열거형(`Contact`·`Projectile`·`Charge`·`Armored`·`Bomb`)이고 몬스터 파일의 `behavior`가 하나를 고른다. 코드에 몬스터 id를 쓰지 않는다.

공통: 표적 = 가장 가까운 산 대원(없으면 멈춤). 공격 대기는 매 걸음 줄고 생성 때 `initialAttackDelay`(1초). 피해는 F1 `PartyService`의 피해 적용을 부른다(받는 피해 = 피해 × (1 − 방어), F3이 바꾼다). 몬스터 HP·피해 = 몬스터 값 × 웨이브 `hpScale`·`damageScale`(생성 때 곱함).

| 행동 | 상태 | 규칙 |
|---|---|---|
| Contact(접촉) | Approach | 거리 > `radius + contactGap`이면 `speed`로 다가감. 붙어 있고 대기 ≤ 0이면 피해, 대기 = `contact.interval` |
| Armored(중장갑) | Approach | Contact와 같은 규칙, 수치만 다르다(HP·반지름 크고 느림, 대기 1.8초). F6의 "강한 적"(A7)은 이 행동으로 고른다 |
| Projectile(투사체) | Approach | 거리 > `range × approachRatio`면 다가가고 < `range × retreatRatio`면 같은 속도로 물러남. 거리 < `range`이고 대기 ≤ 0이면 표적 방향 투사체 하나, 대기 = `projectile.interval`. 접촉 공격 없음 |
| Charge(돌진) | Approach → Windup → Dash → Recover | Approach: 거리 > `radius + contactGap`이면 다가감. 거리 < `triggerRange`이고 대기 ≤ 0이면 집결: 자신 + `rallyRadius` 안의 Approach·대기 ≤ 0인 다른 Charge를 가까운 순 최대 `rallyMax`마리가 Windup(`windupSeconds`, 멈춤)에 들고, 각자 그 순간 가장 가까운 대원 쪽을 돌진 방향으로 고정. Dash(`dashSeconds`): 그 방향으로 `dashSpeed` 이동, 이번 걸음에 지나간 선분과 거리 < `dashHitRadius`인 산 대원에게 돌진 한 번에 한 번씩 피해. Recover(`recoverSeconds`, 멈춤) 들 때 대기 = `cooldown`. 접촉 공격 없음(예전과 같음, notes) |
| Bomb(자폭) | Approach ↔ Swarm → Fuse | 이동 방향 = 표적 방향 + `cohesion`·`alignment`·`separation`(무리 = `flockRadius` 안의 Fuse 아닌 다른 Bomb, 분리는 `separationRadius` 안), 걸음마다 `min(1, dt × steerRate)`로 섞음. Approach에서 거리 < `swarmTriggerRange`면 자신과 무리 모두 Swarm(`swarmSeconds`, 속도 `swarmSpeed`), 끝나면 Approach. 거리 < `fuseDistance`면 Fuse(`fuseSeconds`, 멈춤) → 끝나면 `blastRadius` 안의 산 대원 모두에게 피해를 주고 사라진다(처치 수 안 셈). Fuse 끝 전에 HP 0이면 보통 처치(터지지 않음) |

- 적 투사체: 걸음마다 `shotSpeed`로 이동, 지나간 선분과 거리 < `shotHitRadius`인 첫 산 대원에게 피해 후 사라진다. 누적 이동 ≥ `range × shotTravelScale`이면 사라진다. 피해 = 쏜 몬스터의 (배수 곱한) 피해.
- 몬스터끼리 밀어내기는 없다(예전 규칙. Bomb 무리 분리만). 겹쳐 보이는 문제는 사람 플레이 의견으로 받는다.
- 걸음 순서(F1 C절 ⑤ 안): 원정대 이동 방향 갱신 → 재배치 → 산 몬스터를 목록 순서로 행동 → 적 투사체 이동·명중 → HP 0 몬스터·사라진 투사체 정리. F1 C절의 ①~⑦ 순서는 바꾸지 않는다.

### B. 생성과 재배치 (S3, C1)
- 정규 생성 수 = `round(spawnCount × enemyCountScale)`(F1 판 설정). 간격 = `max(0, duration − spawnEndGap) ÷ max(1, 수 − 1)`. 웨이브 시작 때 시계 0, 걸음마다 시계 −= dt, `생성 수 < 수`이고 시계 ≤ 0인 동안 하나 내고 시계 += 간격. 유형 = 웨이브 `typeWeights` 가중치 뽑기(유형 난수).
- 생성 자리: 원정대 중심에서 보이는 바닥 영역 대각선 절반 + `spawnMargin` 거리(F1 규칙 그대로. 보이는 영역은 F1 카메라가 알려 준다). 방향(자리 난수): 원정대 이동 방향이 0이면 무작위 각, 아니면 `heading`의 앞·왼쪽(+90°)·오른쪽(−90°)·뒤 비율로 고르고 `(난수 − 0.5) × jitter`를 더한다. Bomb은 `bomb.groupSize`마리마다 새 방향을 뽑고 나머지는 그 방향 `± groupSpread / 2`.
- 원정대 이동 방향 = 원정대 중심(F1 `MonsterService`가 생성 거리에 쓰던 값)의 지난 걸음 대비 변화. 길이가 1e-4 미만이면 정지. F2가 원정대를 움직이면 그대로 앞쪽 생성이 된다.
- 버닝 타임 추가 생성(F1 `RunModel.IsBurningTime`이 참인 동안, 정규 수 > 0일 때만): 추가 간격 = `max(0.01, 간격 × burningIntervalScale)`, 낼 수 = `min(ceil(burningSeconds ÷ 추가 간격), floor((min(경과, 시작 + burningSeconds − 1e-4) − 시작) ÷ 추가 간격) + 1)`. 방향 = 웨이브 시작 때 뽑은 각 + (추가 번호 % 2) × π + `(난수 − 0.5) × burningSpread`. 유형 = `burningTypeWeights`. 정규 생성 수에 넣지 않고 따로 센다. (`burningSeconds`·시작 비율은 F1 `run_rules.json`)
- 재배치: Approach 상태의 산 몬스터가 원정대 중심 기준 |dx| > 보이는 반폭 + `recycle.outsideMargin` 또는 |dy| > 보이는 반높이 + `outsideMargin`이고, 원정대 정지이거나 (몬스터 − 중심)·이동 방향 < 0이면 밖 시간 += dt, 아니면 0. 재배치 대기가 끝났을 때 밖 시간 ≥ `holdSeconds`인 것을 오래된 순 최대 `maxPerCheck`마리, 생성 방향 규칙으로 뽑은 방향의 (보이는 반폭·반높이 + `placeMargin`) 상자 가장자리로 옮긴다. 옮긴 몬스터는 밖 시간 0, 대기 = `max(minAttackDelay, 대기)`. 옮긴 뒤 `checkInterval` 동안 다시 옮기지 않는다.
- 난수: 한 판 시작 때 F1 시드에서 생성 전용 난수 두 갈래(유형, 자리)를 만들어 판 끝까지 이어 쓴다. 전투·이동 쪽 난수 소비와 섞이지 않아, 같은 시드면 원정대 움직임과 상관없이 웨이브별 생성 수·유형 순서가 같다.
- 웨이브 끝·로비로 갈 때 몬스터·적 투사체를 모두 비운다(F1 B절 "로비로 돌아가면 적을 비운다"와 같은 자리). 다음 웨이브 시작 때 생성 시계·셈·버닝 각·Bomb 무리 셈을 0으로.
- 셈(`MonsterModel`, 이번 웨이브): 정규 생성 수, 버닝 추가 생성 수, 행동별 생성 수. HUD "위협"은 F1의 살아 있는 몬스터 수 그대로이고 5종 모두 들고 적 투사체는 들지 않는다.

### C. 대표 데이터와 콘텐츠 대기열에 남기는 것 (C1, C3)
- 몬스터 5종 = 행동마다 하나, 예전 5종 값(아래 데이터 스키마). 첫 판 개수 5와 같다.
- 웨이브 대표 3개: wave01(일반만), wave03(사격 첫 등장), wave06(5종 모두, 버닝 타임) — 예전 값.
- 나머지 7개(wave02·04·05·07~10)는 F1 파일에 같은 네 칸을 넣되 앞 대표 웨이브 값을 복사한 임시값이다(02 ← 01, 04·05 ← 03, 07~10 ← 06). 한 판이 끝까지 돌게 하는 자리 채움일 뿐이라 판정기가 M2·M3 목표 밖으로 보고하고, 콘텐츠 대기열이 웨이브마다 파일 하나씩 고친다. 예전 값 표는 [notes.md](notes.md) "콘텐츠 대기열용 예전 값".
- 콘텐츠 대기열 쪽 검사(제안): 항목 검사 `python3 -B ./tools/wave_targets.py --waves <N>`, 회귀 `python3 -B ./tools/check_data.py`, `python3 -B ./tools/wave_targets.py --waves <채운 웨이브 목록>`, 전체 경로 뒤 `python3 -B ./tools/wave_targets.py --metrics Logs/playable/latest/wave-metrics.json`.

### D. 목표값 판정기 (M2·M3·M5·M7)
명령 `python3 -B ./tools/wave_targets.py`(Git 경로 `Tools/wave_targets.py`). 목표값은 게임 기획서 9절에서 옮겨 이 파일 머리에 표로 둔다(콘텐츠 대기열은 고치지 않는다). 종료 코드 0 통과, 1 목표 밖 있음, 2 돌릴 수 없음(파일 없음·형식 오류).

| 목표 | 에디터 없이(데이터) | 진입점 실행 기록(`--metrics <파일>`) |
|---|---|---|
| M2 웨이브별 생성 적 수 | 웨이브 n의 `round(spawnCount × defaultEnemyCountScale)`가 목표 `round(98 + (407 − 98) × (n − 1) ÷ (웨이브 수 − 1))`의 ±5% 안 | 적 수 배율 1인 판의 끝까지 간 웨이브마다 실제 정규 생성 수가 같은 목표 ±5% 안 |
| M3 유형 첫 등장 | 행동별로 가중치 > 0인 첫 웨이브가 사격(projectile) 3·돌진(charge) 4·중장갑(armored) 5·자폭(bomb) 6 | 배율 1, 웨이브 1부터 기록한 판에서 그 행동이 실제로 처음 나온 웨이브 |
| M5 가만히 선 원정대가 지는 웨이브 | (잴 수 없음 — 미측정) | 배율 1, 기록 중 봇에 `Move`가 한 번도 없던 진 판의 `버틴 웨이브 + 1`이 2~5 |
| M7 10웨이브 버닝 타임 프레임 시간 | (잴 수 없음 — 미측정) | 배율 1 판의 마지막 웨이브 버닝 타임 동안(1배 시간) 에디터 프레임 시간 95백분위 ≤ 16.7ms. 살아 있는 적 최대 수를 함께 출력 |
| 실행 = 데이터 | — | 모든 기록 판의 끝까지 간 웨이브마다 정규 생성 수 = `round(spawnCount × 그 판 배율)` |

- `--waves 1,3,6`: 고른 웨이브만 본다. M3는 목표 웨이브 t가 고른 목록에 있으면 t에 그 행동이 있는지, 고른 웨이브 중 t보다 앞선 곳에 없는지를 본다.
- `--require M5,M7`: 고른 목표가 미측정이면 실패로 본다(회귀에서 쓴다). 기본은 미측정을 실패로 보지 않고 "미측정"으로 출력한다.
- `--self-test`: 임시 폴더에 틀린 데이터·기록을 만들어 각각 실패로 판정하는지 본다 — 웨이브 2 적 수 목표 +6%, 사격형 첫 등장 웨이브 2, 기록의 생성 수 ≠ 데이터, 가만히 선 판이 웨이브 1에서 짐, 프레임 95백분위 20ms. 맞는 데이터·기록 하나는 통과해야 한다.
- 행마다 `목표 웨이브/행동 값 목표 OK|FAIL|미측정` 한 줄을 출력한다(콘텐츠 대기열의 표 대조).

### E. 판정 진입점과 경로 (F1 H절 채우기)
진입점 `PlayableProbe`(F1 파일)에 더하는 멤버(모두 `#if UNITY_EDITOR`, 게임 코드는 모른다. 기록은 새 파일 `App/PlayableMetricsRecorder.cs`의 에디터 전용 MonoBehaviour가 맡는다):

| 멤버 | 종류 | 뜻 |
|---|---|---|
| `StartWaveMetrics(string path)` | 조작 | 기록기를 만들고 이후 모든 판을 기록한다. 웨이브 끝(`RunModel.Phase`가 Battle에서 바뀌는 순간 구독)마다, 판 끝마다 파일 전체를 다시 쓴다 |
| `UseRealTimeAtWave(int wave)` | 조작 | 다음에 그 웨이브 전투가 시작되면 `Time.timeScale = 1`, `Application.runInBackground = true`(한 번만). M7을 20배 시간으로 재지 않으려고 |
| `AliveBehaviorKinds` | 읽기 | 지금 살아 있는 몬스터의 행동 종류 수 |
| `EnemyShotCount` | 읽기 | 지금 날아가는 적 투사체 수 |
| `SpawnedCountOf(int wave)` | 읽기 | 마지막 기록 판의 그 웨이브 정규 생성 수(기록 없으면 −1) |
| `IsSpawnCountAsData` | 읽기 | 마지막 기록 판의 끝까지 간 웨이브마다 생성 수 = `round(spawnCount × 배율)` |
| `IsSpawnCountNear(int first, int last, float tolerance)` | 읽기 | 마지막 기록 판의 모든 웨이브가 끝까지 갔고 각 생성 수가 `first → last` 직선 목표 ± tolerance 안 (M2) |
| `FirstWaveOf(string behavior)` | 읽기 | 웨이브 1부터 기록한 마지막 판에서 그 행동(`contact`·`projectile`·`charge`·`armored`·`bomb`)이 처음 나온 웨이브, 없으면 0 (M3) |
| `BurningFrameMsP95` | 읽기 | 마지막 기록 판의 마지막 웨이브 버닝 타임 동안 `Time.unscaledDeltaTime` 95백분위(ms), 표본 없으면 −1 (M7) |
| `MaxAliveEnemies` | 읽기 | 마지막 기록 판 전투 중 살아 있는 몬스터 최대 수 |

- 기록 파일(JSON): 판마다 시드, 배율, 기록 시작 웨이브, 기록 중 쓰인 봇 깃발, 승패, 버틴 웨이브, 웨이브마다 {끝까지 갔나, 정규 생성 수, 데이터 기대 수, 버닝 추가 수, 행동별 수}, 마지막 웨이브 버닝 {95백분위·평균·최대 ms, 표본 수, 살아 있는 적 최대 수}. 판정기 `--metrics`가 읽는다.
- 경로 `tools/playable/route.json`에 더하는 것(단계 G1~G8의 번호·순서·기존 확인은 그대로, 빼는 것 없음. 표는 `PlayableProbe.` 접두어를 줄였다):

| 단계 | 더하는 것 | 이유 |
|---|---|---|
| G5 | `do` 앞에 `StartWaveMetrics("Logs/playable/latest/wave-metrics.json");`, `expect` 끝에 `&& WavesSurvived >= 1 && WavesSurvived <= 4` | M5(지는 웨이브 2~5 = 버틴 웨이브 1~4). G6~G8 판도 이 기록에 들어간다 |
| G7 | `do` 끝에 `UseRealTimeAtWave(10);`, `expect` 끝에 `&& IsSpawnCountNear(98, 407, 0.05f) && FirstWaveOf("projectile") == 3 && FirstWaveOf("charge") == 4 && FirstWaveOf("armored") == 5 && FirstWaveOf("bomb") == 6 && BurningFrameMsP95 >= 0 && BurningFrameMsP95 <= 16.7f`, `timeout`을 F1 값 + 65초 이상(최소 180) | M2·M3·M7. 웨이브 10을 1배로 돌려 늘어나는 시간만큼만 늘린다 |

- 이 기능만으로 통과할 수 없는 단계: G3(봇 이동 F2, `StashCount` F5), G4(F5), G5(G4 뒤라 F5가 있어야 닿음), G7(이동 F2·자동 장착 F5, 그리고 M2·M3를 맞추려면 콘텐츠 대기열이 웨이브 10개를 채워야 함), G8(G7 뒤). 이 기능의 규칙·멤버는 흐름 경로 `tools/playable/f4-monsters-flows.json`(같은 진입점, `--route`로 지정, `time_scale` 1, `scene`·`size`·`sizes`는 `route.json`과 같게)으로 확인한다:

| 단계 | do | wait / expect | capture |
|---|---|---|---|
| L1 | — | `Screen == "Lobby"` | |
| LOAD | `StartWaveMetrics("Logs/playable/latest/f4-metrics.json"); UseRunSettings(3, 0.1f); UseRealTimeAtWave(10); UnityEngine.Time.timeScale = 20f; PressDepart(); StartBot(BotMode.AutoNext);` | `Screen == "Result"`(timeout 180) / `Outcome == "Win" && WavesSurvived == 10 && IsSpawnCountAsData && FirstWaveOf("contact") == 1 && BurningFrameMsP95 > 0 && MaxAliveEnemies >= 1 && !IsSpawnCountNear(98, 407, 0.05f)` | true |
| SHOW | `StopBot(); UseRunSettings(13, 0.4f); UseRealTimeAtWave(6); UnityEngine.Time.timeScale = 20f; PressToLobby(); PressDepart(); StartBot(BotMode.AutoNext);` | `Screen == "Battle" && Wave == 6 && AliveBehaviorKinds == 5 && EnemyShotCount >= 1`(timeout 120) | "all" |
| SHOWEND | `UnityEngine.Time.timeScale = 20f;` | `Screen == "Result"`(timeout 120) / `IsSpawnCountAsData` | |
| STAND | `StopBot(); UseRunSettings(5, 1f); PressToLobby(); PressDepart(); StartBot(BotMode.AutoNext);` | `Screen == "Result"`(timeout 120) / `Outcome == "Lose" && WavesSurvived < 10 && IsSpawnCountAsData && FirstWaveOf("contact") == 1` | true |
| LOBBY | `StopBot(); UnityEngine.Time.timeScale = 1f; PressToLobby();` | `Screen == "Lobby"` | |

  LOAD·SHOW의 시드·배율은 출발값이다. 구현이 임시 기본 무기(F1)로 LOAD가 이기고 SHOW가 웨이브 6에 닿는 값을 정해 파일에 적는다(F1의 `<패배 배율>`과 같은 방식). 확인 식은 바꾸지 않는다.

### F. 화면과 기능의 경계 (V2)

| 화면 | F4가 넣는 것 | 다른 곳이 넣는 것 |
|---|---|---|
| V2 전투 | 몬스터 5종 임시 마름모(F1 도형) — 색 = 몬스터 `color`, 크기 = `radius`. 상태 표시: Charge Windup 중 돌진 방향 반투명 선(길이 `dashSpeed × dashSeconds`), Bomb Fuse 중 `blastRadius` 반투명 원. 적 투사체 작은 원(`shotColor`). F1 임시 원·마름모 스프라이트를 늘이거나 반투명으로 써서 새 스프라이트 종류를 만들지 않는다 | 몬스터 그림·돌진 예고·자폭 부풂 그림(아트 갈래, 아트 기준서 K12·K13, 3.3절), 몬스터 HP·피해 숫자·피격 연출(F6: S9) |
| V2 상단 위협 수 | 셈의 뜻만: 살아 있는 몬스터 5종, 적 투사체 제외. 표시 코드(F1 HUD View·Presenter)는 바꾸지 않는다 | |

### G. 대체되는 F1 임시 규칙 (F1 E절)
- "임시 적 한 종류" → A절 5종. "생성: 모든 웨이브 98마리" → B절 웨이브 데이터. "버닝 타임 추가 생성: 한 종류" → B절 `burningTypeWeights`.
- `Run/placeholder_combat.json`에서 `enemy`·`spawnCount`·`spawnMargin`·`burningIntervalScale` 칸을 지운다(`spawnMargin`·`burningIntervalScale`은 같은 값으로 `Spawn/spawn_rules.json`에 옮긴다). `memberSpots`(F2)·`weapon`(F3)은 그대로 둔다. 파일 삭제는 마지막 대체 기능의 몫이다.

## 데이터 스키마
위치는 F1과 같은 `Assets/Game/03.Resources/Resources/Data/`. JSON은 F1 `ResourceService.LoadJson<T>`로 읽으므로 사전 대신 객체 배열을 쓴다. 검사 명령 `python3 -B ./tools/check_data.py`(F1)에 아래 검사를 더한다.

| 위치 | 이름 | 필드 | 타입 | 키/기본값 | 쓰는 쪽 → 읽는 쪽 |
|---|---|---|---|---|---|
| JSON `Monster/<id>.json` | 몬스터(5개, 새 폴더) | `id`, `behavior`, `hp`, `damage`, `speed`, `radius`, `contactGap`, `color`, 행동 칸(아래) | string, string(`contact`·`projectile`·`charge`·`armored`·`bomb`), float > 0, float ≥ 0, float > 0, float > 0, float ≥ 0, string(#rrggbb) | 키 `id` = 파일명. 값은 아래 몬스터 표. 검사: 개수 1~20, id 중복 없음, `behavior` 다섯 중 하나, 행동에 필요한 칸 묶음이 있고 수가 범위 안 | 데이터 → `MonsterRepository` → `MonsterSpawnService`·`MonsterBehaviorService`·`MonsterViewFactory`. 아트 갈래가 같은 파일에 그림 키 칸을 더한다 |
| (같은 파일) | 행동 칸 묶음 | `contact{interval}`(contact·armored), `projectile{range, approachRatio, retreatRatio, interval, shotSpeed, shotTravelScale, shotHitRadius, shotColor}`, `charge{triggerRange, rallyRadius, rallyMax, windupSeconds, dashSeconds, dashSpeed, dashHitRadius, recoverSeconds, cooldown}`, `bomb{fuseDistance, fuseSeconds, blastRadius, swarmTriggerRange, swarmSeconds, swarmSpeed, flockRadius, separationRadius, cohesion, alignment, separation, steerRate, groupSize, groupSpread}` | float(초·거리 > 0, 비율 0~1), `rallyMax`·`groupSize` int ≥ 1, `shotColor` #rrggbb | 행동에 맞는 묶음만 필수. 다른 묶음은 있어도 안 읽는다 | 위와 같음 |
| JSON `Wave/wave<NN>.json` | 웨이브(F1 10개에 칸 추가) | (F1) `id`, `order`, `duration`, `hasBurningTime` + (F4) `spawnCount`, `hpScale`, `damageScale`, `typeWeights[{monster, weight}]` | int ≥ 0, float > 0, float > 0, 배열(string → `Monster` id, float > 0) | 대표값과 임시 복사는 아래 웨이브 표. 검사: 네 칸이 모든 웨이브에 있음, `typeWeights` 1개 이상·같은 몬스터 두 번 없음·가리키는 몬스터 있음·weight > 0 | 데이터 → `WaveRepository`(F1) → `MonsterSpawnService`·판정기. 콘텐츠 대기열이 웨이브마다 고친다 |
| JSON `Spawn/spawn_rules.json` | 생성 규칙(1개, 새 폴더) | `spawnMargin`, `spawnEndGap`, `initialAttackDelay`, `burningIntervalScale`, `burningSpread`, `burningTypeWeights[{monster, weight}]`, `heading{front, left, right, back, jitter}`, `recycle{outsideMargin, holdSeconds, placeMargin, maxPerCheck, checkInterval, minAttackDelay}` | float, float, float, float, float(rad), 배열, float(비율 합 1 ± 1e-6, rad), float·int | 70, 4, 1, 0.5, 0.3, [{slime, 80}, {boar, 20}], {0.5, 0.15, 0.15, 0.2, 1.5708}, {300, 3, 150, 2, 0.5, 1}. 검사: 가리키는 몬스터 있음, `heading` 합 1 | 데이터 → `MonsterSpawnService`·`MonsterRecycleService` |
| JSON `Run/placeholder_combat.json` | 임시 전투(F1) | `enemy`·`spawnCount`·`spawnMargin`·`burningIntervalScale` 칸 삭제 | | 나머지 칸은 F1 그대로 | F1 검사 명령의 이 네 칸 검사도 지운다 |
| 세이브 | 없음 | | | 저장 없음(A9) | |
| 서버 | 없음 | | | | |

몬스터 5종(예전 값, `PrototypeData.json` `monsters`):

| id | behavior | hp | damage | speed | radius | contactGap | color | 행동 칸 |
|---|---|---|---|---|---|---|---|---|
| slime | contact | 20 | 5 | 63 | 18 | 5 | #819363 | contact{interval 1} |
| mushroom | projectile | 25 | 8 | 63 | 18 | 5 | #a4779f | projectile{252, 0.92, 0.65, 2, 180, 1.5, 15, #f2d3ff} |
| boar | charge | 18 | 8 | 105 | 18 | 5 | #eea04c | charge{280, 300, 15, 0.8, 0.7, 460, 28, 0.8, 2} |
| rock | armored | 60 | 12 | 38 | 25 | 5 | #7395b6 | contact{interval 1.8} |
| puffer | bomb | 14 | 14 | 135 | 18 | 5 | #e56e6e | bomb{65, 1.2, 90, 300, 1.4, 225, 260, 38, 0.65, 0.35, 1.8, 7, 6, 0.24} |

웨이브(F4가 넣는 네 칸. `duration`·`hasBurningTime`은 F1 값 그대로):

| 파일 | spawnCount | hpScale | damageScale | typeWeights | 구분 |
|---|---|---|---|---|---|
| wave01 | 98 | 0.7 | 1.0 | slime 100 | 대표 |
| wave02 | 98 | 0.7 | 1.0 | slime 100 | 임시(← 01) |
| wave03 | 167 | 0.924 | 1.2 | slime 80, mushroom 20 | 대표 |
| wave04·05 | 167 | 0.924 | 1.2 | slime 80, mushroom 20 | 임시(← 03) |
| wave06 | 270 | 1.26 | 1.5 | slime 55, mushroom 20, boar 12, rock 8, puffer 5 | 대표 |
| wave07~10 | 270 | 1.26 | 1.5 | slime 55, mushroom 20, boar 12, rock 8, puffer 5 | 임시(← 06) |

- 실행 중 상태(저장 안 함, `MonsterModel` 확장): 몬스터마다 번호, 몬스터 id, 행동, 바닥 좌표, HP·최대 HP, 피해(배수 곱함), 공격 대기, 상태(`MonsterPhase`: Approach·Windup·Dash·Recover·Swarm·Fuse), 상태 시계, 돌진 방향, 무리 방향, 밖 시간, 이번 돌진에 맞은 대원. 적 투사체 목록(좌표, 방향, 속도, 피해, 이동 거리, 최대 이동, 명중 거리). 이번 웨이브 셈(정규·버닝·행동별). 생성 상태(시계, 버닝 각, Bomb 무리 셈과 방향, 재배치 대기, 지난 원정대 중심).

## 클라 변경
경로는 `Assets/Game/` 기준. 새 어셈블리 없음(F1 `Game`·`Game.Tests.EditMode`).

몬스터 (A·B절, `02.Scripts/Features/Monster/`)
- `Data/MonsterData.cs`(+ 행동 칸 묶음 클래스), `Data/MonsterBehavior.cs`(열거형), `Data/MonsterPhase.cs`(열거형), `Data/TypeWeightData.cs`(`monster`, `weight`), `Data/SpawnRulesData.cs` — 새 JSON 형
- `Registry/MonsterRepository.cs` — `Monster/` 읽기·id로 찾기, `Spawn/spawn_rules.json` 읽기
- `Model/MonsterModel.cs`(F1) — 위 실행 중 상태 칸, 적 투사체 목록, 웨이브 셈을 더한다
- `Service/MonsterService.cs`(F1) — F1 호출 자리와 이름 유지, 안쪽은 아래 서비스 호출로 바꾼다. 임시 적 규칙 삭제
- `Service/MonsterSpawnService.cs` — 정규·버닝 생성, 가중치 뽑기, 생성 방향, Bomb 무리, 생성 난수 두 갈래
- `Service/MonsterRecycleService.cs` — 재배치, 원정대 이동 방향
- `Service/MonsterBehaviorService.cs` + `Interface/IMonsterBehavior.cs` + `Service/Behavior/ContactBehavior.cs`(Contact·Armored), `ProjectileBehavior.cs`, `ChargeBehavior.cs`, `BombBehavior.cs` — 행동별 규칙
- `Service/EnemyShotService.cs` — 적 투사체 이동·명중
- `Utils/WeightedPick.cs`, `Utils/SegmentMath.cs`(선분과 점 거리) — 순수 함수
- `View/MonsterView.cs`(F1) — 몬스터 데이터의 색·크기 적용, `UpdateState(phase, direction)`로 돌진 방향 선·폭발 반경 원 켜고 끄기
- `Factory/MonsterViewFactory.cs`(F1) — 초기화 때 몬스터 데이터 넘기기
- `View/EnemyShotView.cs`, `Factory/EnemyShotViewFactory.cs`(`PoolManager.Get` + 초기화), `Registry/EnemyShotViewRegistry.cs` — F1 MonsterView 짝과 같은 방식
- `Presenter/MonsterStatePresenter.cs`(`ITickable`, 모델 상태 → `MonsterView.UpdateState`), `Presenter/EnemyShotPresenter.cs`(`ITickable`, 투사체 모델 → View 좌표, F1 `IsoProjection`). F1 `WorldPresenter`는 F2가 고치므로 건드리지 않는다

웨이브 (C절)
- `02.Scripts/Features/Run/Data/WaveData.cs`(F1) — `spawnCount`, `hpScale`, `damageScale`, `typeWeights` 네 칸 추가만
- `02.Scripts/Features/Run/Data/PlaceholderCombatData.cs`(F1) — `enemy`·`spawnCount`·`spawnMargin`·`burningIntervalScale` 필드와 임시 적 클래스 삭제만

겹치는 파일(다른 기능과 나란히 고친다 — 이 기능이 더하는 줄·칸만)
- `02.Scripts/App/GameSceneLifetimeScope.cs` — 등록 줄 추가: `MonsterRepository`, `MonsterSpawnService`, `MonsterRecycleService`, `MonsterBehaviorService`와 행동 4개, `EnemyShotService`, `EnemyShotViewFactory`, `EnemyShotViewRegistry`, `MonsterStatePresenter`, `EnemyShotPresenter`(`Lifetime.Scoped`, F1 규칙). 적 투사체 프리팹 참조 칸 하나(F1 MonsterView 프리팹 연결 방식을 따른다)
- `02.Scripts/App/PlayableProbe.cs` — E절 멤버 10개 추가만. 기존 멤버·봇 동작은 바꾸지 않는다
- `01.Scene/Main.unity` — 적 투사체 View 부모 오브젝트 하나와 LifetimeScope의 프리팹 참조 연결만
- HUD View·Presenter(`UI_BattleHudView`, `BattleHudPresenter`) — 바꾸지 않음
- `03.Resources/Resources/Data/Run/placeholder_combat.json` — 칸 네 개 삭제만
- `Tools/playable/route.json` — E절 표의 G5·G7 추가만
- `Tools/check_data.py` — 몬스터·생성 규칙·웨이브 네 칸 검사 함수 하나와 호출 한 줄, `--self-test` 틀린 데이터 6종 추가(없는 몬스터를 가리키는 `typeWeights`, 다섯 밖의 `behavior`, 행동 칸 묶음 빠짐, weight 0, 몬스터 id 중복, `typeWeights` 없는 웨이브), placeholder 네 칸 검사 삭제

새 파일(이 기능만)
- `02.Scripts/App/PlayableMetricsRecorder.cs` — 기록기(`#if UNITY_EDITOR` MonoBehaviour)
- `03.Resources/Resources/Data/Monster/slime.json`, `mushroom.json`, `boar.json`, `rock.json`, `puffer.json`, `03.Resources/Resources/Data/Spawn/spawn_rules.json`
- `03.Resources/Resources/Data/Wave/wave01.json`~`wave10.json`(F1 파일) — 네 칸 추가
- 적 투사체 프리팹(F1 MonsterView 프리팹과 같은 폴더)
- `Tools/wave_targets.py` — 판정기(D절)
- `Tools/playable/f4-monsters-flows.json` — 흐름 경로(E절)

테스트 (`Tests/EditMode/`)
- `MonsterDataTests.cs` — 실제 데이터 읽기, 행동마다 몬스터 하나, 웨이브 네 칸
- `MonsterSpawnTests.cs` — 정규 생성, 버닝 추가 생성, 방향, 생성 거리, 실제 데이터 10웨이브, 같은 시드
- `MonsterBehaviorTests.cs` — 행동 5종, 적 투사체
- `MonsterRecycleTests.cs` — 재배치
- `MonsterViewStateTests.cs` — 상태 표시 켜고 끄기

## 서버 변경과 API 계약
없음. 게임 기획서: 서버 없음, 저장 없음(A9). 재화·랭킹·치팅 방지가 걸리는 것이 없다.

## 수용 기준
확인 방법: 자동 테스트(EditMode·명령), 에디터(컴파일·에디터 명령으로 상태 확인), 플레이(플레이 모드 동작·화면), 사람.
EditMode 테스트 명령: `unity test --platform editmode --assembly Game.Tests.EditMode`(F1과 같음. 실행기 명령은 구현 단계가 이 레포의 unity-cli 형식으로 적는다).

| 번호 | 기준 (확인할 수 있는 문장) | 확인 방법 | 확인 절차 |
|---|---|---|---|
| AC1 | 새 코드·자산을 넣은 뒤 컴파일 에러가 0개다 | 에디터 | `python3 -B ./tools/editor_ready.py` 후 콘솔·Editor.log에 `error CS` 없음 |
| AC2 | 데이터 검사가 지금 데이터에서 OK(exit 0)이고, F4가 더한 틀린 데이터 6종(없는 몬스터를 가리키는 가중치, 다섯 밖의 행동, 행동 칸 묶음 빠짐, weight 0, 몬스터 id 중복, 가중치 없는 웨이브)에서는 각각 exit 1이다 | 자동 테스트 | `python3 -B ./tools/check_data.py`, `python3 -B ./tools/check_data.py --self-test` |
| AC3 | 판정기가 대표 웨이브에서 통과하고 나머지를 콘텐츠 할 일로 보고한다: `--waves 1,3,6`은 exit 0, 전체는 exit 1이며 FAIL 행이 M2 웨이브 2·4·5·7·8·9·10과 M3 charge·armored뿐이다. M5·M7은 데이터만으로는 미측정이라고 출력한다 | 자동 테스트 | `python3 -B ./tools/wave_targets.py --waves 1,3,6`, `python3 -B ./tools/wave_targets.py`(FAIL 행 목록 대조) |
| AC4 | 판정기 자체 시험: 적 수 +6%, 사격형 웨이브 2 첫 등장, 기록의 생성 수 ≠ 데이터, 가만히 선 판이 웨이브 1에서 짐, 프레임 95백분위 20ms를 각각 FAIL로, 맞는 데이터·기록을 OK로 판정한다. `--require M5`는 M5 미측정이면 exit 1 | 자동 테스트 | `python3 -B ./tools/wave_targets.py --self-test` |
| AC5 | 접촉·중장갑: 반지름 + 5보다 멀면 속도대로 다가가고, 붙으면 공격 대기(접촉 1초, 중장갑 1.8초, 첫 대기 1초)마다 피해 × 웨이브 피해 배수를 준다. 쓰러진 대원은 표적이 아니다 | 자동 테스트 | `MonsterBehaviorTests.ContactApproachesThenHitsEveryInterval`, `MonsterBehaviorTests.ArmoredUsesLongerInterval`, `MonsterBehaviorTests.DeadMemberIsNotTargeted` |
| AC6 | 투사체: 사거리 × 0.92보다 멀면 다가가고 × 0.65보다 가까우면 물러나며, 사거리 안에서 2초마다 쏜다. 투사체는 선분 거리 15 안의 첫 대원 하나에게만 맞고 사라지며, 최대 이동 378을 넘으면 사라진다. 접촉 공격은 없다. 적 투사체는 살아 있는 몬스터 수(HUD 위협, `EnemyCount`)에 들지 않는다 | 자동 테스트 | `MonsterBehaviorTests.ProjectileKeepsDistanceBand`, `MonsterBehaviorTests.ProjectileFiresEveryInterval`, `MonsterBehaviorTests.EnemyShotHitsFirstMemberOnly`, `MonsterBehaviorTests.EnemyShotExpiresAtMaxTravel`, `MonsterBehaviorTests.EnemyShotsNotCountedAsThreat` |
| AC7 | 돌진: 280 안에서 대기가 끝나면 300 안의 대기 끝난 돌진형이 최대 16마리(자신 포함)까지 함께 0.8초 준비 → 0.7초 돌진 → 0.8초 회복을 거친다. 돌진 한 번에 같은 대원은 한 번만 맞고, 회복 뒤 2초 동안 다시 모이지 않는다. 붙어 있어도 접촉 공격을 하지 않는다 | 자동 테스트 | `MonsterBehaviorTests.ChargeRalliesUpToMax`, `MonsterBehaviorTests.ChargePhasesInOrder`, `MonsterBehaviorTests.DashHitsEachMemberOnce`, `MonsterBehaviorTests.ChargeCooldownAfterRecover` |
| AC8 | 자폭: 300 안에 들면 무리가 함께 1.4초 동안 속도 225로 오고, 65 안에서 1.2초 점화 뒤 90 안의 모든 산 대원에게 피해를 주고 사라지며 처치 수는 그대로다. 점화 중에 쓰러지면 터지지 않고 처치 수가 1 오른다. 자폭형은 6마리마다 새 방향에서 무리로 나온다 | 자동 테스트 | `MonsterBehaviorTests.BombSwarmsTogether`, `MonsterBehaviorTests.BombExplodesAfterFuse`, `MonsterBehaviorTests.BombKilledDuringFuseDoesNotExplode`, `MonsterSpawnTests.BombsSpawnInGroups` |
| AC9 | 정규 생성: 실제 데이터 10웨이브를 생성 부분만 돌리면 웨이브마다 생성 수 = `round(spawnCount × 배율)`, 마지막 정규 생성이 `duration − 4`초 + 한 걸음(1/30초) 안, 유형은 그 웨이브 가중치에 있는 것만, HP·피해 = 몬스터 값 × 웨이브 배수, 행동별 첫 등장 웨이브가 데이터의 가중치 > 0 첫 웨이브와 같다(판정기 데이터 판정과 같은 값). 배율 0이면 정규·버닝 모두 0마리 | 자동 테스트 | `MonsterSpawnTests.RealDataCountsTypesAndFirstWaves`, `MonsterSpawnTests.WaveScalesApplied`, `MonsterSpawnTests.ZeroScaleSpawnsNothing` |
| AC10 | 생성 자리·방향: 생성 거리가 16:9·21:9·4:3 보이는 영역 각각에서 대각선 절반 + 70이다. 원정대가 멈추면 방향이 고르게 퍼지고(8방위 칸마다 1만 번 중 12.5% ± 2%), 움직이면 앞 50%·왼쪽 15%·오른쪽 15%·뒤 20%(± 2%)다 | 자동 테스트 | `MonsterSpawnTests.SpawnDistanceByVisibleArea`, `MonsterSpawnTests.HeadingUniformWhenStill`, `MonsterSpawnTests.HeadingFrontBiasedWhenMoving` |
| AC11 | 버닝 추가 생성: 버닝 타임 창 동안에만, 정규 간격 절반마다 반대쪽 두 방향을 번갈아 slime·boar만 나오고 정규 생성 수에 들지 않는다. 버닝 없는 웨이브(1~5)에서는 0마리 | 자동 테스트 | `MonsterSpawnTests.BurningExtrasOnlyInWindow`, `MonsterSpawnTests.BurningExtrasAlternateSidesAndTypes` |
| AC12 | 같은 시드면 프레임을 1/60초 60번으로 주든 1초 1번으로 주든, 원정대가 움직이든 멈추든 웨이브별 생성 수와 유형 순서가 같다 | 자동 테스트 | `MonsterSpawnTests.SameSeedSameSpawnSequence` |
| AC13 | 재배치: Approach인 몬스터가 보이는 영역 + 300 밖, 원정대 뒤쪽(또는 정지)에 3초 넘게 있으면 보이는 영역 + 150 가장자리로 옮겨지고, 0.5초에 2마리를 넘지 않으며, Windup·Dash·Swarm·Fuse 중인 몬스터는 옮기지 않는다 | 자동 테스트 | `MonsterRecycleTests.FarBehindMovedAfterHold`, `MonsterRecycleTests.AtMostTwoPerInterval`, `MonsterRecycleTests.BusyPhasesNotMoved` |
| AC14 | 임시 적이 없어졌다: `placeholder_combat.json`과 `PlaceholderCombatData`에 `enemy`·`spawnCount`·`spawnMargin`·`burningIntervalScale`이 없고, 게임 코드가 그 이름을 읽지 않는다 | 자동 테스트 | `grep -rn "spawnCount\|spawnMargin\|burningIntervalScale" Assets/Game/02.Scripts Assets/Game/03.Resources/Resources/Data/Run`의 결과가 `WaveData`·`SpawnRulesData`·`spawn_rules.json` 밖에 없음, `python3 -B ./tools/check_data.py` |
| AC15 | 판정 경로 파일이 G1~G8을 번호·순서 그대로 가지고 형식 검사가 통과하며, F1 대비 바뀐 것은 E절 표(G5의 `do`·`expect`, G7의 `do`·`expect`·`timeout`) 추가뿐이다 | 자동 테스트 | `python3 -B ./tools/playable.py --check`, `git diff`로 `Tools/playable/route.json`을 F1 커밋과 비교 |
| AC16 | 판정 경로 G2까지 통과 | 플레이 | `python3 -B ./tools/playable.py --until G2` |
| AC17 | 흐름 경로가 통과한다(LOAD 승리·생성 수 = 데이터·버닝 프레임 기록, SHOW 웨이브 6에 5종과 적 투사체가 함께 있음, STAND 가만히 선 판 패배). 이어서 판정기가 기록 파일에서 "실행 = 데이터" 행을 모두 OK로, M5 행에 STAND 판의 진 웨이브 값을 출력한다(M5의 OK·FAIL은 콘텐츠 대기열이 맞춘다) | 플레이 | `python3 -B ./tools/playable.py --route tools/playable/f4-monsters-flows.json`, `python3 -B ./tools/wave_targets.py --metrics Logs/playable/latest/f4-metrics.json` 출력의 해당 행 |
| AC18 | 전투 화면에서 몬스터 5종이 색·크기로 서로 구별되고(중장갑이 가장 큼), 적 투사체가 보이며, 상단에 위협 수가 보인다 | 플레이 | SHOW 캡처(5종·투사체가 함께 있는 순간)를 열어 종류별로 확인하고 본 캡처 목록을 보고에 적음 |
| AC19 | `MonsterView`가 Windup 상태에서 돌진 방향 선을, Fuse 상태에서 폭발 반경 원을 켜고, 다른 상태로 바뀌면 끈다 | 자동 테스트 | `MonsterViewStateTests` |
| AC20 | 노치 기기에서 가장자리 UI가 가려지지 않고, 16:9·19.5:9·21:9·4:3 가로 화면에서 판과 UI가 잘리거나 겹치지 않는다(몬스터가 늘어난 전투 화면에서) | 플레이 | SHOW `capture: "all"` 4장을 열어 봄 + Device Simulator 노치 기기 1대(가로) 캡처 |

### 기존 테스트
레포에 지금 NUnit 테스트가 없다(F1 기획서 "기존 테스트", 2026-11-15). F1 테스트는 F1 구현이 만들고 있어 이 기획서를 쓸 때 열어 볼 수 없었다. 그래서 "그대로 통과"라고 쓰지 않고, F1 기획서 AC10·AC11·AC13이 적은 기대를 이 기획서의 새 흐름과 대조해 바뀌는 것을 아래에 올린다. 구현 단계는 시작할 때 실제 F1 테스트의 마지막 기대를 열어 같은 대조를 하고, 아래 목록 밖에서 기대가 바뀌는 테스트가 나오면 고치지 말고 멈춰 보고한다. 테스트를 만드는 준비 코드(임시 적 설정 → 몬스터·웨이브 데이터)만 바뀌고 기대가 같은 테스트는 준비 코드만 고친다.

- 대조함(기대 그대로): `RunServiceTests`(웨이브 시간표·동시 패배·즉시 패배·승리·버틴 웨이브·버닝 창·일시정지·배속 — F4는 C절 판 규칙을 바꾸지 않음), `PartyServiceTests`(편성·받는 피해 — F4는 F1 피해 적용을 부름), `PlaceholderCombatTests.SameSeedSameResultAcrossFrameSplits`(두 쪼개기의 결과가 같다는 기대 — F4 생성도 같은 시드면 같다, AC12).

기대 변경 허용(이 목록에 있는 것만 구현 단계가 기대값을 고칠 수 있다):

| 테스트 | 지금 기대(끝 상태·값) | 새 기대 | 이유(기획서 절) |
|---|---|---|---|
| `PlaceholderCombatTests`의 임시 적 수치·종류를 쓰는 기대(F1 AC13: 10초 처치 ≥ 1, 접촉 피해, 쓰러진 대원 제외) | 임시 적 한 종류(HP 20·피해 5·속도 63·반지름 18, 웨이브 배수 없음)로 잰 값 | 웨이브 1 데이터(slime, HP 20 × 0.7, 피해 5 × 1.0)로 잰 값. "처치 ≥ 1, 접촉한 적이 HP를 줄인다, 쓰러진 대원은 표적이 아니다"라는 문장은 그대로 | 적용안 A·G — 임시 적을 몬스터 데이터로 대체 |
| `PlaceholderCombatTests.BurningTimeSpawnsExtra` | 버닝 타임 동안 임시 적 한 종류가 추가로 나온다 | 추가로 나오는 적이 slime·boar 중 하나이고 정규 생성 수에 들지 않는다(추가로 나온다는 기대는 그대로) | 적용안 B — 버닝 유형 비율 |
| F1 테스트 중 `placeholder_combat.json`의 `enemy`·`spawnCount`·`spawnMargin`·`burningIntervalScale` 값을 읽는 기대(예: `DataLoadTests`) | 그 칸의 값(98, 70, 0.5, 임시 적 칸) | 그 칸이 없다. `spawnMargin`·`burningIntervalScale`은 `spawn_rules.json`에서 같은 값(70, 0.5), 적 수는 웨이브 파일에서 읽는다 | 적용안 G |
| F1 `check_data.py --self-test`의 placeholder 네 칸에 기대는 틀린 데이터(있다면) | 그 칸이 틀리면 exit 1 | 그 칸을 보지 않는다(같은 뜻의 검사는 F4 검사로 옮김) | 적용안 G |

## 열린 질문
없음. 작은 갈림길은 아래 가정으로 정했다(승인 때 함께 확인).

### 가정
| 번호 | 가정 | 틀리면 바뀌는 것 |
|---|---|---|
| MA1 | 웨이브 대표 데이터는 1·3·6이고, 나머지 7개는 앞 대표 웨이브 값을 복사한 임시값이다(판정기가 목표 밖으로 보고, 콘텐츠 대기열이 채움) | C절, AC3 |
| MA2 | 몬스터 5종 = 행동마다 하나, 예전 5종 값 그대로 | 데이터 스키마 몬스터 표 |
| MA3 | M2의 "생성 적 수"는 정규 생성만(버닝 추가 생성 제외, 예전 `Spawned`와 같음). 목표 = 98 → 407 직선 보간(웨이브마다 약 +34.3) ±5% | D절, G7 expect |
| MA4 | M3는 행동 기준(사격 = projectile, 돌진 = charge, 중장갑 = armored, 자폭 = bomb). 데이터 판정은 가중치 > 0인 첫 웨이브, 실행 판정은 실제로 처음 나온 웨이브 | D절, G7 expect |
| MA5 | M5 = 적 수 배율 1, 기록 중 봇 `Move`가 없던 진 판의 버틴 웨이브 + 1. G5 expect에 넣는다(게임 기획서 9절 "G5 결과의 버틴 웨이브") | D·E절 |
| MA6 | M7 = 배율 1 판의 마지막 웨이브 버닝 타임을 1배 시간으로 돌려 잰 에디터 프레임 시간 95백분위. 그래서 G7은 웨이브 10만 1배로 돌고 timeout이 늘어난다 | D·E절 |
| MA7 | 중장갑은 접촉과 같은 규칙에 수치만 다르다(예전과 같음, 피해 감소 없음) | A절 |
| MA8 | 돌진형은 접촉 공격을 하지 않는다(예전 코드에서 붙으면 늘 집결이 먼저였음) | A절, AC7 |
| MA9 | 자폭으로 사라진 몬스터는 처치 수에 들지 않는다(예전과 같음) | A절, AC8 |
| MA10 | 원정대 이동 방향은 원정대 중심의 걸음 사이 변화로 읽는다(F2 입력 모델에 기대지 않음) | B절 |
| MA11 | 생성 난수는 한 판 시드에서 만든 유형·자리 두 갈래라 움직임과 상관없이 같은 시드면 생성 순서가 같다 | B절, AC12 |
| MA12 | 적 수 배율 0이면 버닝 추가 생성도 없다(F1 "적이 없는 판"의 뜻) | B절, AC9 |
| MA13 | 몬스터끼리 밀어내기는 없다(예전 규칙, 자폭형 무리 분리만) | A절 |
| MA14 | 몬스터 id는 그림 이름(아트 기준서 K12)을 따른 slime·mushroom·boar·rock·puffer. 그림 키는 아트 갈래가 같은 파일에 더한다 | 데이터 스키마 |
| MA15 | 임시 상태 표시: 돌진 준비 = 돌진 방향 반투명 선, 자폭 점화 = 폭발 반경 반투명 원, 적 투사체 = 작은 원(#f2d3ff). F1 임시 스프라이트를 늘이거나 반투명으로 쓴다 | F절, AC19 |
| MA16 | 자폭 무리 방향 섞기 비율 `steerRate` 7(예전 코드의 상수)을 데이터 칸으로 둔다 | 데이터 스키마 |
