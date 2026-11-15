using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// 전투 시험용 아이템(데이터 파일에 기대지 않는다). 실제 읽기 경로(JsonUtility + OnAfterDeserialize)로 만든다
internal static class CombatTestItems
{
    public static ItemData Action(string id, string mode, float power, float interval, float range)
    {
        return Create(id, mode, power, interval, range, "", 0f, "", 0f, "", false);
    }

    public static ItemData Passive(string id, string baseEffect, float baseValue, string bonusEffect = "", float bonusValue = 0f,
        string bonusTarget = "", bool bonusNeedsAdjacent = false, float effectInterval = 0f, float effectRadius = 0f)
    {
        return Create(id, "passive", 0f, 0f, 0f, baseEffect, baseValue, bonusEffect, bonusValue, bonusTarget, bonusNeedsAdjacent,
            effectInterval, effectRadius);
    }

    // 기획서 C절 표의 시험 아이템 묶음(값은 표 그대로)
    public static List<ItemData> CreateCatalog()
    {
        return new List<ItemData>
        {
            Action("t_sword", "melee", 22f, 1f, 3f),
            Action("t_axe", "sweep", 35f, 1.8f, 3.5f),
            Action("t_bow", "projectile", 13f, 0.7f, 8f),
            Action("t_staff", "explosion", 24f, 1.7f, 7f),
            Action("t_wand", "chain", 17f, 1.4f, 7f),
            Action("t_relic", "heal", 12f, 2f, 6f),
            Action("t_censer", "healArea", 6f, 2.5f, 4f),
            Passive("t_ring", "haste", 0.04f, "haste", 0.1f, "attack", true),
            Passive("t_whetstone", "attack", 0.06f, "attack", 0.14f, "attack", true),
            Passive("t_pierce", "attack", 0.05f, "pierce", 1f, "projectile", true),
            Passive("t_split", "attack", 0.05f, "extraShots", 1f, "projectile", true),
            Passive("t_hat", "attack", 0.05f, "chain", 2f, "chain", true),
            Passive("t_spellbook", "attack", 0.06f, "radius", 0.4f, "explosion", true),
            Passive("t_chainmail", "hp", 45f, "hp", 30f),
            Passive("t_helmet", "def", 0.03f, "startShield", 15f),
            Passive("t_banner", "def", 0.04f, "shield", 20f),
            Passive("t_scripture", "heal", 4.8f, "share", 1f),
            Passive("t_amulet", "heal", 1f, "share", 1f),
            Passive("t_orb", "attack", 0.08f, "pulse", 10f, "", false, 3f, 100f),
        };
    }

    // 시험 아이템 + 실제 데이터 아이템(시작 아이템을 쓰는 한 판 시험용)
    public static ItemRepository CreateRepository(params ItemData[] extraItems)
    {
        var realItems = new ItemRepository(new ResourceService()).ItemDataList;
        return new ItemRepository(realItems.Concat(CreateCatalog()).Concat(extraItems).ToList());
    }

    private static ItemData Create(string id, string mode, float power, float interval, float range, string baseEffect,
        float baseValue, string bonusEffect, float bonusValue, string bonusTarget, bool bonusNeedsAdjacent,
        float effectInterval = 0f, float effectRadius = 0f)
    {
        var json = "{" + $"\"id\":\"{id}\",\"name\":\"{id}\",\"mode\":\"{mode}\",\"shape\":[{{\"x\":0,\"y\":0}}],"
            + $"\"power\":{F(power)},\"interval\":{F(interval)},\"range\":{F(range)},"
            + $"\"baseEffect\":\"{baseEffect}\",\"baseValue\":{F(baseValue)},"
            + $"\"bonusEffect\":\"{bonusEffect}\",\"bonusValue\":{F(bonusValue)},\"bonusTarget\":\"{bonusTarget}\","
            + $"\"bonusNeedsAdjacent\":{(bonusNeedsAdjacent ? "true" : "false")},"
            + $"\"effectInterval\":{F(effectInterval)},\"effectRadius\":{F(effectRadius)},\"icon\":\"\"" + "}";
        return JsonUtility.FromJson<ItemData>(json);
    }

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
}

// 시험용 배치 정보: 칸별 등급 배수와 "모든 칸이 붙어 있음" 여부를 시험이 정한다
internal class FakePlacementInfo : IItemPlacementInfo
{
    private readonly Dictionary<(int, int), float> _gradeDict = new();

    public bool IsAllAdjacent { get; set; }

    public void SetGrade(int memberIndex, int slotIndex, float multiplier) => _gradeDict[(memberIndex, slotIndex)] = multiplier;

    public float GetGradeMultiplier(int memberIndex, int slotIndex) =>
        _gradeDict.TryGetValue((memberIndex, slotIndex), out var multiplier) ? multiplier : 1f;

    public bool IsAdjacent(int memberIndex, int slotA, int slotB) => IsAllAdjacent;
}
