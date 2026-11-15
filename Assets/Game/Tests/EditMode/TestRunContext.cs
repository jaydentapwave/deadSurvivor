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
    public readonly RunModel RunModel = new();
    public readonly PartyModel PartyModel = new();
    public readonly MonsterModel MonsterModel = new();
    public readonly PartyService PartyService;
    public readonly CombatRulesRepository CombatRulesRepository;
    public readonly CombatModel CombatModel = new();
    public readonly EquipmentStatService EquipmentStatService;
    public readonly CombatService CombatService;
    public readonly ReviveService ReviveService;
    public readonly MonsterService MonsterService;
    public readonly GradeRepository GradeRepository;
    public readonly BagRulesRepository BagRulesRepository;
    public readonly LootRulesRepository LootRulesRepository;
    public readonly BagModel BagModel;
    public readonly BagService BagService;
    public readonly LootService LootService;
    public readonly RunService RunService;

    // 기준 화면 640×360px(PPU 48)이 보는 바닥 영역의 반 크기(바닥 단위)
    public Vector2 ReferenceViewHalfExtent => IsoProjection.WorldToFloor(
        new Vector2(640f / Const.PixelsPerUnit / 2f, 360f / Const.PixelsPerUnit / 2f), RunRules.UnitScale);

    public CombatRulesData CombatRules => CombatRulesRepository.Rules;

    // itemRepository·placementInfo: 시험용 아이템 목록·가짜 배치 정보(없으면 실제 데이터 파일·가방 없음).
    // bagModel: 배치 정보 시험이 BagPlacementInfo와 같은 가방 모델을 쓸 때(없으면 새로)
    public TestRunContext(ItemRepository itemRepository = null, IItemPlacementInfo placementInfo = null, BagModel bagModel = null)
    {
        RunRules = ResourceService.LoadJson<RunRulesData>("Data/Run/run_rules");
        CombatData = ResourceService.LoadJson<PlaceholderCombatData>("Data/Run/placeholder_combat");
        CharacterRepository = new CharacterRepository(ResourceService);
        ItemRepository = itemRepository ?? new ItemRepository(ResourceService);
        CombatRulesRepository = new CombatRulesRepository(ResourceService);
        WaveRepository = new WaveRepository(ResourceService);
        PartyService = new PartyService(PartyModel, CharacterRepository, CombatData);
        MonsterService = new MonsterService(MonsterModel, PartyModel, PartyService, CombatData, RunRules);
        EquipmentStatService = new EquipmentStatService(ItemRepository, CombatRulesRepository,
            placementInfo ?? new NoBagPlacementInfo());
        CombatService = new CombatService(CombatModel, PartyModel, PartyService, MonsterModel, MonsterService, ItemRepository,
            CharacterRepository, EquipmentStatService, CombatRulesRepository);
        ReviveService = new ReviveService(RunModel, PartyModel, CombatService);
        GradeRepository = new GradeRepository(ResourceService);
        BagRulesRepository = new BagRulesRepository(ResourceService);
        LootRulesRepository = new LootRulesRepository(ResourceService);
        BagModel = bagModel ?? new BagModel();
        BagService = new BagService(BagModel, RunModel, PartyModel, PartyService, ItemRepository, GradeRepository,
            BagRulesRepository);
        LootService = new LootService(BagModel, BagService, PartyModel, ItemRepository, LootRulesRepository);
        RunService = new RunService(RunModel, WaveRepository, RunRules, PartyService, CombatService, MonsterService, BagService,
            LootService);
        MonsterService.SetViewHalfExtent(ReferenceViewHalfExtent);
    }

    // 데이터 순서의 캐릭터로 대원을 만들어(F1 자리) 각자 itemIds를 장착하고 전투 판·웨이브를 연다
    public void SetupParty(params string[][] itemIdsPerMember)
    {
        PartyModel.Clear();
        for (var i = 0; i < itemIdsPerMember.Length; i++)
        {
            var data = CharacterRepository.CharacterDataList[i];
            var member = new PartyMember(data.Id, data.Hp, data.Def, CombatData.MemberSpots[i]);
            foreach (var itemId in itemIdsPerMember[i])
            {
                member.Equip(itemId);
            }

            PartyModel.Add(member);
        }

        CombatService.ResetRun();
        CombatService.PrepareWave();
    }

    public Monster AddMonster(int id, Vector2 position, float hp = 1000000f)
    {
        var monster = new Monster(id, position, hp);
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
        var contact = CombatData.Enemy.Radius + CombatData.Enemy.ContactGap;
        MonsterModel.Add(new Monster(9999, survivor.Position + Vector2.left * contact, CombatData.Enemy.Hp));
        return survivor;
    }
}
