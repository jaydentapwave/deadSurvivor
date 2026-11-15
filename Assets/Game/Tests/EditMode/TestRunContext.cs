using UnityEngine;

// 시험용 한 판 객체 묶음. 실제 데이터 파일(Resources)을 읽어 GameSceneLifetimeScope와 같은 의존으로 만든다.
internal class TestRunContext
{
    public readonly ResourceService ResourceService = new();
    public readonly RunRulesData RunRules;
    public readonly PlaceholderCombatData CombatData;
    public readonly CharacterRepository CharacterRepository;
    public readonly ItemRepository ItemRepository;
    public readonly WaveRepository WaveRepository;
    public readonly MonsterRepository MonsterRepository;
    public readonly RunModel RunModel = new();
    public readonly PartyModel PartyModel = new();
    public readonly MonsterModel MonsterModel = new();
    public readonly PartyService PartyService;
    public readonly BasicWeaponService BasicWeaponService;
    public readonly MonsterSpawnService MonsterSpawnService;
    public readonly MonsterRecycleService MonsterRecycleService;
    public readonly MonsterBehaviorService MonsterBehaviorService;
    public readonly EnemyShotService EnemyShotService;
    public readonly MonsterService MonsterService;
    public readonly RunService RunService;

    // 기준 화면 640×360px(PPU 48)이 보는 바닥 영역의 반 크기(바닥 단위)
    public Vector2 ReferenceViewHalfExtent => IsoProjection.WorldToFloor(
        new Vector2(640f / Const.PixelsPerUnit / 2f, 360f / Const.PixelsPerUnit / 2f), RunRules.UnitScale);

    public TestRunContext()
    {
        RunRules = ResourceService.LoadJson<RunRulesData>("Data/Run/run_rules");
        CombatData = ResourceService.LoadJson<PlaceholderCombatData>("Data/Run/placeholder_combat");
        CharacterRepository = new CharacterRepository(ResourceService);
        ItemRepository = new ItemRepository(ResourceService);
        WaveRepository = new WaveRepository(ResourceService);
        MonsterRepository = new MonsterRepository(ResourceService);
        PartyService = new PartyService(PartyModel, CharacterRepository, CombatData);
        BasicWeaponService = new BasicWeaponService(PartyModel, MonsterModel, CharacterRepository, CombatData);
        MonsterSpawnService = new MonsterSpawnService(MonsterModel, PartyModel, MonsterRepository, RunRules);
        MonsterRecycleService = new MonsterRecycleService(MonsterModel, PartyModel, MonsterRepository, MonsterSpawnService);
        MonsterBehaviorService = new MonsterBehaviorService(MonsterModel, PartyService, new IMonsterBehavior[]
        {
            new ContactBehavior(PartyService),
            new ProjectileBehavior(MonsterModel),
            new ChargeBehavior(MonsterModel, PartyModel, PartyService),
            new BombBehavior(MonsterModel, PartyModel, PartyService),
        });
        EnemyShotService = new EnemyShotService(MonsterModel, PartyModel, PartyService);
        MonsterService = new MonsterService(MonsterModel, MonsterSpawnService, MonsterRecycleService, MonsterBehaviorService,
            EnemyShotService);
        RunService = new RunService(RunModel, WaveRepository, RunRules, PartyService, BasicWeaponService, MonsterService);
        MonsterService.SetViewHalfExtent(ReferenceViewHalfExtent);
    }

    // 전투가 끝날 때까지(정비·결과) 걸음을 돌리고 돈 걸음 수를 돌려준다
    public int StepUntilBattleEnds(int maxSteps = 100000)
    {
        var steps = 0;
        while (RunModel.Phase.CurrentValue == RunPhase.Battle && steps < maxSteps)
        {
            RunService.Step(RunRules.SimStep);
            steps++;
        }

        return steps;
    }

    public void StepSeconds(float seconds)
    {
        var steps = Mathf.RoundToInt(seconds / RunRules.SimStep);
        for (var i = 0; i < steps && RunModel.Phase.CurrentValue == RunPhase.Battle; i++)
        {
            RunService.Step(RunRules.SimStep);
        }
    }

    // 대원 하나만 HP를 hp로 남기고 나머지를 쓰러뜨린 뒤, 그 대원 접촉 거리에 공격 준비된 적 하나를 둔다
    public PartyMember PlaceKillingContact(float hp)
    {
        var members = PartyModel.MemberList;
        for (var i = 1; i < members.Count; i++)
        {
            members[i].Hp = 0f;
        }

        var survivor = members[0];
        survivor.Hp = hp;
        var slime = MonsterRepository.Get("slime");
        MonsterModel.Add(CreateMonster(9999, "slime", survivor.Position + Vector2.left * (slime.Radius + slime.ContactGap)));
        return survivor;
    }

    // 손으로 놓는 몬스터: 그 종류의 데이터 HP·피해 그대로(웨이브 배수 없음), 공격 대기 0(F1 임시 적과 같은 준비)
    public Monster CreateMonster(int id, string monsterId, Vector2 position)
    {
        var data = MonsterRepository.Get(monsterId);
        return new Monster(id, data, position, data.Hp, data.Damage, 0f);
    }

    // 웨이브 하나를 시작 상태로(원정대 만들기 + 생성 BeginRun·BeginWave). 무기·이동 없이 생성·행동만 볼 때
    public WaveData BeginWaveOnly(int wave, int seed, float enemyCountScale = 1f)
    {
        PartyService.CreateParty();
        var waveData = WaveRepository.WaveDataList[wave - 1];
        MonsterService.BeginRun(seed);
        MonsterService.BeginWave(waveData, enemyCountScale);
        return waveData;
    }

    // BeginWaveOnly 뒤 웨이브 끝까지 생성만 돌린다(RunService.Step ②·③과 같은 시간 계산). onStep은 걸음마다(경과 시간)
    public void SpawnWholeWave(WaveData waveData, System.Action<float> onStep = null)
    {
        var burningStart = waveData.Duration * RunRules.BurningStartRatio;
        var remaining = waveData.Duration;
        while (remaining > 0f)
        {
            var delta = Mathf.Min(RunRules.SimStep, remaining);
            remaining -= delta;
            var elapsed = waveData.Duration - remaining;
            var isBurningTime = waveData.HasBurningTime && elapsed >= burningStart && elapsed < burningStart + RunRules.BurningSeconds;
            MonsterService.TickSpawn(delta, elapsed, isBurningTime, burningStart);
            onStep?.Invoke(elapsed);
        }
    }
}
