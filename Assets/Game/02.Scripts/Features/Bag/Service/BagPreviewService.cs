using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 수치 변화 미리보기(기획서 C절): 지금 가방·보관함을 복사해 같은 BagService 규칙으로 조작을 적용해 보고,
// 대원마다 F3 EquipmentStatService로 이전·이후를 계산해 바뀐 항목만 줄로 낸다. 원본은 바꾸지 않는다
public class BagPreviewService
{
    private const float ChangeEpsilon = 1e-4f;

    private readonly BagModel _bagModel;
    private readonly BagService _bagService;
    private readonly PartyModel _partyModel;
    private readonly ItemRepository _itemRepository;
    private readonly CharacterRepository _characterRepository;
    private readonly CombatRulesRepository _combatRulesRepository;
    private readonly GradeRepository _gradeRepository;
    private readonly BagRulesData _bagRules;

    public BagPreviewService(BagModel bagModel, BagService bagService, PartyModel partyModel, ItemRepository itemRepository,
        CharacterRepository characterRepository, CombatRulesRepository combatRulesRepository, GradeRepository gradeRepository,
        BagRulesRepository bagRulesRepository)
    {
        _bagModel = bagModel;
        _bagService = bagService;
        _partyModel = partyModel;
        _itemRepository = itemRepository;
        _characterRepository = characterRepository;
        _combatRulesRepository = combatRulesRepository;
        _gradeRepository = gradeRepository;
        _bagRules = bagRulesRepository.Rules;
    }

    public BagPreviewResult Preview(int uid, int rotation, BagTarget target)
    {
        var result = new BagPreviewResult { Member = -1 };
        if (target.Kind == BagTargetKind.Cell)
        {
            result.Member = target.Member;
            CollectTargetCells(uid, rotation, target, result.CellList);
        }

        var before = CalculateSheet(_bagModel);
        using var copy = _bagModel.CreateCopy();
        result.IsSuccess = _bagService.TryApply(copy, uid, rotation, target, out var reason);
        result.Reason = reason;
        if (result.IsSuccess)
        {
            AddChangeLines(before, CalculateSheet(copy), result.LineList);
        }

        return result;
    }

    // 대원마다 전체 능력치와 발동 아이템(가방 목록 순)의 계산값. 계산은 그 모델의 배치 정보로 만든 F3 계산 그대로
    public List<MemberStatSheet> CalculateSheet(BagModel model)
    {
        var placementInfo = new BagPlacementInfo(model, _gradeRepository, _itemRepository);
        var statService = new EquipmentStatService(_itemRepository, _combatRulesRepository, placementInfo);
        var sheetList = new List<MemberStatSheet>();
        var memberList = _partyModel.MemberList;
        for (var member = 0; member < memberList.Count && member < model.MemberCount; member++)
        {
            var character = _characterRepository.Get(memberList[member].CharacterId);
            var bag = model.GetBag(member);
            var equippedIdList = new List<string>(bag.Count);
            foreach (var bagItem in bag)
            {
                equippedIdList.Add(bagItem.ItemId);
            }

            var sheet = new MemberStatSheet { Stats = statService.Calculate(member, character, equippedIdList, -1) };
            for (var slot = 0; slot < bag.Count; slot++)
            {
                var item = _itemRepository.Get(bag[slot].ItemId);
                if (item.Mode == ItemMode.Passive)
                {
                    continue;
                }

                var actionStats = statService.Calculate(member, character, equippedIdList, slot);
                var multiplier = placementInfo.GetGradeMultiplier(member, slot);
                sheet.ActionList.Add(new ActionStat
                {
                    Uid = bag[slot].Uid,
                    Item = item,
                    Power = statService.GetPower(item, multiplier, character, actionStats),
                    Period = statService.GetPeriod(item, actionStats),
                    Range = statService.GetRange(item, actionStats),
                    Pierce = actionStats.Pierce,
                    ExtraShots = actionStats.ExtraShots,
                    Chain = actionStats.Chain,
                });
            }

            sheetList.Add(sheet);
        }

        return sheetList;
    }

    // 놓기면 놓일 칸(가방 안만), 그 칸 아이템과 합성이면 그 아이템 칸
    private void CollectTargetCells(int uid, int rotation, BagTarget target, List<Vector2Int> cellList)
    {
        var item = _bagModel.FindItem(uid, out _);
        if (item == null || target.Member < 0 || target.Member >= _bagModel.MemberCount)
        {
            return;
        }

        var bag = _bagModel.GetBag(target.Member);
        var cover = BagRules.FindItemAt(bag, target.X, target.Y, uid, _itemRepository);
        if (cover != null && BagRules.CanMerge(item, cover, _gradeRepository.LastGrade))
        {
            BagRules.GetItemCells(cover, _itemRepository, cellList);
            return;
        }

        BagRules.GetPlacedCells(_itemRepository.Get(item.ItemId).Shape, rotation, target.X, target.Y, cellList);
        cellList.RemoveAll(cell => cell.x < 0 || cell.y < 0 || cell.x >= _bagRules.BagWidth || cell.y >= _bagRules.BagHeight);
    }

    private void AddChangeLines(List<MemberStatSheet> beforeList, List<MemberStatSheet> afterList, List<string> lineList)
    {
        for (var member = 0; member < beforeList.Count && member < afterList.Count; member++)
        {
            var name = _characterRepository.Get(_partyModel.MemberList[member].CharacterId).Name;
            var before = beforeList[member];
            var after = afterList[member];
            AddLine(lineList, $"{name} 최대 HP", before.Stats.MaxHp, after.Stats.MaxHp, true, false);
            AddLine(lineList, $"{name} 방어", before.Stats.Defense, after.Stats.Defense, true, true);
            AddLine(lineList, $"{name} 공격 증가", before.Stats.Attack, after.Stats.Attack, true, true);
            AddLine(lineList, $"{name} 자기 회복", before.Stats.Heal, after.Stats.Heal, true, false);
            AddLine(lineList, $"{name} 보호막", before.Stats.Shield, after.Stats.Shield, true, false);
            foreach (var action in after.ActionList)
            {
                var label = $"{name} {action.Item.Name}";
                if (!TryFindAction(before, action.Uid, out var old))
                {
                    lineList.Add($"[개선] {label} 발동 추가");
                    continue;
                }

                var powerName = action.Item.IsHealMode ? "회복량" : "피해";
                AddLine(lineList, $"{label} {powerName}", old.Power, action.Power, true, false);
                AddLine(lineList, $"{label} 주기", old.Period, action.Period, false, false);
                AddLine(lineList, $"{label} 사거리", old.Range, action.Range, true, false);
                AddLine(lineList, $"{label} 관통", old.Pierce, action.Pierce, true, false);
                AddLine(lineList, $"{label} 갈래", old.ExtraShots, action.ExtraShots, true, false);
                AddLine(lineList, $"{label} 추가 연쇄", old.Chain, action.Chain, true, false);
            }

            foreach (var action in before.ActionList)
            {
                if (!TryFindAction(after, action.Uid, out _))
                {
                    lineList.Add($"[감소] {name} {action.Item.Name} 발동 빠짐");
                }
            }
        }
    }

    private static bool TryFindAction(MemberStatSheet sheet, int uid, out ActionStat action)
    {
        foreach (var candidate in sheet.ActionList)
        {
            if (candidate.Uid == uid)
            {
                action = candidate;
                return true;
            }
        }

        action = default;
        return false;
    }

    // isHigherBetter: 커지면 개선(주기만 거짓). isPercent: 0.06 → 6%
    private static void AddLine(List<string> lineList, string label, float before, float after, bool isHigherBetter,
        bool isPercent)
    {
        if (Mathf.Abs(after - before) <= ChangeEpsilon)
        {
            return;
        }

        var isImproved = isHigherBetter ? after > before : after < before;
        var tag = isImproved ? "[개선]" : "[감소]";
        lineList.Add($"{tag} {label} {Format(before, isPercent)} → {Format(after, isPercent)}");
    }

    private static string Format(float value, bool isPercent) =>
        isPercent
            ? (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : value.ToString("0.##", CultureInfo.InvariantCulture);
}

// 미리보기 결과. Member·CellList = 칠할 가방 칸(놓기·가방 위 합성일 때만), LineList = 바뀐 항목 줄(성공일 때만)
public class BagPreviewResult
{
    public bool IsSuccess { get; set; }
    public BagFailReason Reason { get; set; }
    public int Member { get; set; }
    public List<Vector2Int> CellList { get; } = new();
    public List<string> LineList { get; } = new();
}

// 대원 하나의 계산값(미리보기 비교·자동 장착 점수)
public class MemberStatSheet
{
    public CombatStats Stats { get; set; }
    public List<ActionStat> ActionList { get; } = new();
}

// 발동 아이템 하나의 계산값
public struct ActionStat
{
    public int Uid;
    public ItemData Item;
    // 피해(공격 방식) 또는 회복량(회복 방식)
    public float Power;
    public float Period;
    public float Range;
    public int Pierce;
    public int ExtraShots;
    public int Chain;
}
