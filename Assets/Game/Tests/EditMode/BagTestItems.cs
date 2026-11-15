using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

// 가방 시험용 아이템(칸 모양이 있는 것)과 한 판 준비. 실제 읽기 경로(JsonUtility + OnAfterDeserialize)로 만든다
internal static class BagTestItems
{
    public static readonly (int x, int y)[] Row3 = { (0, 0), (1, 0), (2, 0) };
    public static readonly (int x, int y)[] One = { (0, 0) };
    public static readonly (int x, int y)[] Square = { (0, 0), (1, 0), (0, 1), (1, 1) };

    public static List<ItemData> CreateCatalog()
    {
        return new List<ItemData>
        {
            Create("b_bow", "단궁", "projectile", Row3, 13f, 0.7f, 8f),
            Create("b_sword", "장검", "melee", Row3, 22f, 1f, 3f),
            Create("b_wand", "번개 완드", "chain", Row3, 17f, 1.4f, 7f),
            Create("b_whet", "숫돌", "passive", One, baseEffect: "attack", baseValue: 0.06f, bonusEffect: "attack",
                bonusValue: 0.14f, bonusTarget: "attack", bonusNeedsAdjacent: true),
            Create("b_quiver", "화살통", "passive", One, bonusEffect: "pierce", bonusValue: 1f, bonusTarget: "projectile",
                bonusNeedsAdjacent: true),
            Create("b_ring", "가속 반지", "passive", One, baseEffect: "haste", baseValue: 0.04f, bonusEffect: "haste",
                bonusValue: 0.1f, bonusTarget: "attack", bonusNeedsAdjacent: true),
            Create("b_shield", "철제 방패", "passive", Square, baseEffect: "def", baseValue: 0.08f, bonusEffect: "def",
                bonusValue: 0.08f),
            Create("b_zero", "안 나옴", "passive", One, baseEffect: "hp", baseValue: 10f, lootWeight: 0f),
        };
    }

    // 실제 데이터 아이템(시작 아이템) + 가방 시험 아이템
    public static ItemRepository CreateRepository()
    {
        var realItems = new ItemRepository(new ResourceService()).ItemDataList;
        return new ItemRepository(realItems.Concat(CreateCatalog()).ToList());
    }

    // 배치 정보가 실제 가방(BagPlacementInfo)인 한 판 묶음
    public static TestRunContext CreateContext(ItemRepository itemRepository = null)
    {
        var repository = itemRepository ?? CreateRepository();
        var bagModel = new BagModel();
        var placementInfo = new BagPlacementInfo(bagModel, new GradeRepository(new ResourceService()), repository);
        return new TestRunContext(repository, placementInfo, bagModel);
    }

    // 데이터 순서 캐릭터로 대원을 만들어 각자 itemIds를 장착하고, 가방을 그 목록으로 만든 뒤(A6) 전투 칸을 열고 정비 화면으로 둔다
    public static void SetupBags(TestRunContext context, params string[][] itemIdsPerMember)
    {
        context.PartyModel.Clear();
        for (var i = 0; i < itemIdsPerMember.Length; i++)
        {
            var data = context.CharacterRepository.CharacterDataList[i];
            var member = new PartyMember(data.Id, data.Hp, data.Def, context.CombatData.MemberSpots[i]);
            foreach (var itemId in itemIdsPerMember[i])
            {
                member.Equip(itemId);
            }

            context.PartyModel.Add(member);
        }

        context.BagService.ResetRun();
        context.CombatService.ResetRun();
        context.RunModel.SetPhase(RunPhase.Prep);
    }

    // 보관함 끝에 넣고 그 Uid
    public static int AddToStash(TestRunContext context, string itemId)
    {
        context.BagService.AddLoot(new[] { itemId });
        return context.BagModel.StashList[context.BagModel.StashList.Count - 1].Uid;
    }

    public static BagPreviewService CreatePreview(TestRunContext context) =>
        new(context.BagModel, context.BagService, context.PartyModel, context.ItemRepository, context.CharacterRepository,
            context.CombatRulesRepository, context.GradeRepository, context.BagRulesRepository);

    public static AutoEquipPlanner CreatePlanner(TestRunContext context) =>
        new(context.BagModel, context.BagService, CreatePreview(context), context.PartyModel, context.GradeRepository,
            context.BagRulesRepository, context.CombatRulesRepository);

    public static BagItem BagItemAt(TestRunContext context, int member, int index) => context.BagModel.GetBag(member)[index];

    public static ItemData Create(string id, string name, string mode, (int x, int y)[] shape, float power = 0f,
        float interval = 0f, float range = 0f, string baseEffect = "", float baseValue = 0f, string bonusEffect = "",
        float bonusValue = 0f, string bonusTarget = "", bool bonusNeedsAdjacent = false, float lootWeight = 1f)
    {
        var shapeJson = new StringBuilder();
        foreach (var (x, y) in shape)
        {
            shapeJson.Append(shapeJson.Length > 0 ? "," : "").Append($"{{\"x\":{x},\"y\":{y}}}");
        }

        var json = "{" + $"\"id\":\"{id}\",\"name\":\"{name}\",\"mode\":\"{mode}\",\"shape\":[{shapeJson}],"
            + $"\"power\":{F(power)},\"interval\":{F(interval)},\"range\":{F(range)},"
            + $"\"baseEffect\":\"{baseEffect}\",\"baseValue\":{F(baseValue)},"
            + $"\"bonusEffect\":\"{bonusEffect}\",\"bonusValue\":{F(bonusValue)},\"bonusTarget\":\"{bonusTarget}\","
            + $"\"bonusNeedsAdjacent\":{(bonusNeedsAdjacent ? "true" : "false")},"
            + $"\"effectInterval\":0,\"effectRadius\":0,\"icon\":\"\",\"lootWeight\":{F(lootWeight)}" + "}";
        return JsonUtility.FromJson<ItemData>(json);
    }

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
}
