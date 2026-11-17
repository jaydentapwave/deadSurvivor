using UnityEngine;

// 시험용 한 판 객체 묶음. 실제 데이터 파일(Resources)을 읽어 GameSceneLifetimeScope와 같은 의존으로 만든다.
internal class TestRunContext
{
    // F1 임시 4자리(F2가 placeholder_combat에서 지운 칸). SetupParty로 만드는 F3 시험의 대원 배치를 그대로 둔다
    private static readonly Vector2[] F1MemberSpots =
    {
        new(-24f, -24f), new(24f, -24f), new(-24f, 24f), new(24f, 24f),
    };

    public readonly ResourceService ResourceService = new();
    public readonly RunRulesData RunRules;
    public readonly ViewRulesData ViewRules;
    public readonly PlaceholderCombatData CombatData;
    public readonly CharacterRepository CharacterRepository;
    public readonly ItemRepository ItemRepository;
    public readonly WaveRepository WaveRepository;
    public readonly FormationRepository FormationRepository;
    public readonly MoveRulesData MoveRules;
    public readonly MonsterRepository MonsterRepository;
    public readonly RunModel RunModel = new();
    public readonly PartyModel PartyModel = new();
    public readonly MonsterModel MonsterModel = new();
    public readonly FormationModel FormationModel = new();
    public readonly PartyMoveModel PartyMoveModel = new();
    public readonly CombatFeedbackModel CombatFeedbackModel = new();
    public readonly PartyService PartyService;
    public readonly CombatRulesRepository CombatRulesRepository;
    public readonly CombatModel CombatModel = new();
    public readonly EquipmentStatService EquipmentStatService;
    public readonly CombatService CombatService;
    public readonly ReviveService ReviveService;
    public readonly MonsterSpawnService MonsterSpawnService;
    public readonly MonsterRecycleService MonsterRecycleService;
    public readonly MonsterBehaviorService MonsterBehaviorService;
    public readonly EnemyShotService EnemyShotService;
    public readonly MonsterService MonsterService;
    public readonly FormationService FormationService;
    public readonly PartyMoveService PartyMoveService;
    public readonly GradeRepository GradeRepository;
    public readonly BagRulesRepository BagRulesRepository;
    public readonly LootRulesRepository LootRulesRepository;
    public readonly BagModel BagModel;
    public readonly BagService BagService;
    public readonly LootService LootService;
    public readonly RunService RunService;

    // 기준 화면 640×360px(PPU 48)이 보는 바닥 영역의 반 크기(바닥 단위)
    public Vector2 ReferenceViewHalfExtent => TiltedViewMath.Footprint(360f / Const.PixelsPerUnit / 2f, 16f/9f, .5f) / RunRules.UnitScale;

    public CombatRulesData CombatRules => CombatRulesRepository.Rules;

    // itemRepository·placementInfo: 시험용 아이템 목록·가짜 배치 정보(없으면 실제 데이터 파일·가방 없음).
    // bagModel: 배치 정보 시험이 BagPlacementInfo와 같은 가방 모델을 쓸 때(없으면 새로)
    public TestRunContext(ItemRepository itemRepository = null, IItemPlacementInfo placementInfo = null, BagModel bagModel = null)
    {
        ViewRules = ResourceService.LoadJson<ViewRulesData>("Data/View/view_rules");
        RunRules = ResourceService.LoadJson<RunRulesData>("Data/Run/run_rules");
        CombatData = ResourceService.LoadJson<PlaceholderCombatData>("Data/Run/placeholder_combat");
        CharacterRepository = new CharacterRepository(ResourceService);
        ItemRepository = itemRepository ?? new ItemRepository(ResourceService);
        CombatRulesRepository = new CombatRulesRepository(ResourceService);
        WaveRepository = new WaveRepository(ResourceService);
        FormationRepository = new FormationRepository(ResourceService);
        MoveRules = ResourceService.LoadJson<MoveRulesData>("Data/Party/move_rules");
        PartyService = new PartyService(PartyModel, CharacterRepository, CombatFeedbackModel);
        MonsterRepository = new MonsterRepository(ResourceService);
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
            EnemyShotService, CombatFeedbackModel);
        EquipmentStatService = new EquipmentStatService(ItemRepository, CombatRulesRepository,
            placementInfo ?? new NoBagPlacementInfo());
        CombatService = new CombatService(CombatModel, PartyModel, PartyService, MonsterModel, MonsterService, ItemRepository,
            CharacterRepository, EquipmentStatService, CombatRulesRepository, ViewRules);
        ReviveService = new ReviveService(RunModel, PartyModel, CombatService);
        FormationService = new FormationService(FormationModel, FormationRepository, RunModel);
        PartyMoveService = new PartyMoveService(PartyModel, PartyMoveModel, FormationService, MoveRules, RunRules, ViewRules);
        GradeRepository = new GradeRepository(ResourceService);
        BagRulesRepository = new BagRulesRepository(ResourceService);
        LootRulesRepository = new LootRulesRepository(ResourceService);
        BagModel = bagModel ?? new BagModel();
        BagService = new BagService(BagModel, RunModel, PartyModel, PartyService, ItemRepository, GradeRepository,
            BagRulesRepository);
        LootService = new LootService(BagModel, BagService, PartyModel, ItemRepository, LootRulesRepository);
        RunService = new RunService(RunModel, WaveRepository, RunRules, PartyService, CombatService, MonsterService,
            FormationService, PartyMoveService, BagService, LootService);
        MonsterService.SetViewHalfExtent(ReferenceViewHalfExtent);
        PartyMoveService.SetViewHalfExtent(ReferenceViewHalfExtent);
    }

    // 데이터 순서의 캐릭터로 대원을 만들어(F1 자리) 각자 itemIds를 장착하고 전투 판·웨이브를 연다
    public void SetupParty(params string[][] itemIdsPerMember)
    {
        PartyModel.Clear();
        for (var i = 0; i < itemIdsPerMember.Length; i++)
        {
            var data = CharacterRepository.CharacterDataList[i];
            var member = new PartyMember(data.Id, data.Hp, data.Def, F1MemberSpots[i]);
            foreach (var itemId in itemIdsPerMember[i])
            {
                member.Equip(itemId);
            }

            PartyModel.Add(member);
        }

        CombatService.ResetRun();
        CombatService.PrepareWave();
    }

    // F3 전투 시험용 표적: slime 데이터(반지름 18 = F1 임시 적)에 HP만 hp로
    public Monster AddMonster(int id, Vector2 position, float hp = 1000000f)
    {
        var slime = MonsterRepository.Get("slime");
        var monster = new Monster(id, slime, position, hp, slime.Damage, 0f);
        MonsterModel.Add(monster);
        return monster;
    }

    public void StepCombat(int steps, float stepSeconds)
    {
        for (var i = 0; i < steps; i++)
        {
            CombatService.Step(stepSeconds);
        }
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
