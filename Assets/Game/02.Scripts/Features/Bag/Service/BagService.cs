using System.Collections.Generic;
using System.Linq;

// 가방 조작(기획서 A5·A6)과 장착 목록 맞추기(A2). BagModel을 쓰는 유일한 곳.
// Try*는 주어진 모델(원본 또는 미리보기·계획용 복사본)에 규칙만 적용한다: 모든 확인을 먼저 하고 성공일 때만 쓴다.
// 실제 조작(Drop·Merge·Unequip·Apply)은 정비 화면 확인 → 원본에 Try* → 장착 목록 다시 쓰기 → Version +1
public class BagService
{
    private readonly BagModel _bagModel;
    private readonly RunModel _runModel;
    private readonly PartyModel _partyModel;
    private readonly PartyService _partyService;
    private readonly ItemRepository _itemRepository;
    private readonly GradeRepository _gradeRepository;
    private readonly BagRulesData _rules;

    public BagService(BagModel bagModel, RunModel runModel, PartyModel partyModel, PartyService partyService,
        ItemRepository itemRepository, GradeRepository gradeRepository, BagRulesRepository bagRulesRepository)
    {
        _bagModel = bagModel;
        _runModel = runModel;
        _partyModel = partyModel;
        _partyService = partyService;
        _itemRepository = itemRepository;
        _gradeRepository = gradeRepository;
        _rules = bagRulesRepository.Rules;
    }

    // 한 판 시작(A6): 보관함 비움, Uid 1부터, 대원마다 장착 목록 아이템을 시작 칸부터 (y, x) 순 첫 들어가는 칸에 회전 0·일반 등급으로.
    // 장착 목록은 쓰지 않는다(F1이 넣은 그대로)
    public void ResetRun()
    {
        var memberList = _partyModel.MemberList;
        _bagModel.Reset(memberList.Count);
        for (var member = 0; member < memberList.Count; member++)
        {
            foreach (var itemId in memberList[member].EquippedItemIdList)
            {
                PlaceStartItem(member, itemId);
            }
        }

        _bagModel.IncrementVersion();
    }

    // 로비로 갈 때: 가방·보관함을 비운다
    public void Clear()
    {
        _bagModel.Reset(0);
        _bagModel.IncrementVersion();
    }

    // 전리품(B3): 일반 등급·회전 0으로 보관함 끝에. 마지막 지급 목록 = 이번 것
    public void AddLoot(IReadOnlyList<string> itemIdList)
    {
        var uidList = new List<int>(itemIdList.Count);
        foreach (var itemId in itemIdList)
        {
            var item = new BagItem(_bagModel.TakeUid(), itemId, 0, 0, 0, 0);
            _bagModel.AddToStash(item);
            uidList.Add(item.Uid);
        }

        _bagModel.SetLastGranted(uidList);
        _bagModel.IncrementVersion();
    }

    public bool Drop(int uid, int member, int x, int y, int rotation, out BagFailReason reason)
    {
        if (!IsPrep(out reason))
        {
            return false;
        }

        return Commit(TryDrop(_bagModel, uid, member, x, y, rotation, out reason));
    }

    public bool Merge(int sourceUid, int targetUid, out BagFailReason reason)
    {
        if (!IsPrep(out reason))
        {
            return false;
        }

        return Commit(TryMerge(_bagModel, sourceUid, targetUid, out reason));
    }

    public bool Unequip(int uid, out BagFailReason reason)
    {
        if (!IsPrep(out reason))
        {
            return false;
        }

        return Commit(TryUnequip(_bagModel, uid, out reason));
    }

    // 집은 아이템을 대상에 적용(기획서 C절 "적용")
    public bool Apply(int uid, int rotation, BagTarget target, out BagFailReason reason)
    {
        if (!IsPrep(out reason))
        {
            return false;
        }

        return Commit(TryApply(_bagModel, uid, rotation, target, out reason));
    }

    // 놓기(A5): (x, y)를 덮는 그 가방의 다른 아이템과 합성할 수 있으면 합성, 아니면 들어갈 때만 원래 자리에서 빼 그 가방 끝에 붙인다
    public bool TryDrop(BagModel model, int uid, int member, int x, int y, int rotation, out BagFailReason reason)
    {
        var item = model.FindItem(uid, out var owner);
        if (item == null)
        {
            return NotFound(uid, out reason);
        }

        if (member < 0 || member >= model.MemberCount)
        {
            GameLogger.LogError($"[BagService] Member {member} out of 0..{model.MemberCount - 1}");
            reason = BagFailReason.NotFound;
            return false;
        }

        var bag = model.GetBag(member);
        var cover = BagRules.FindItemAt(bag, x, y, uid, _itemRepository);
        if (cover != null && BagRules.CanMerge(item, cover, _gradeRepository.LastGrade))
        {
            return TryMerge(model, uid, cover.Uid, out reason);
        }

        if (!CanChange(owner) || !CanChange(member))
        {
            reason = BagFailReason.Locked;
            return false;
        }

        var shape = _itemRepository.Get(item.ItemId).Shape;
        if (!BagRules.Fits(bag, uid, shape, rotation, x, y, _itemRepository, _rules.BagWidth, _rules.BagHeight, out reason))
        {
            return false;
        }

        model.RemoveItem(item);
        item.Rotation = ((rotation % 4) + 4) % 4;
        item.X = x;
        item.Y = y;
        model.AddToBag(member, item);
        return true;
    }

    // 합성(A5): 끌고 온 쪽을 원래 자리에서 빼고 대상 등급 +1. 대상은 제자리·같은 순서
    public bool TryMerge(BagModel model, int sourceUid, int targetUid, out BagFailReason reason)
    {
        var source = model.FindItem(sourceUid, out var sourceOwner);
        if (source == null)
        {
            return NotFound(sourceUid, out reason);
        }

        var target = model.FindItem(targetUid, out var targetOwner);
        if (target == null)
        {
            return NotFound(targetUid, out reason);
        }

        if (!CanChange(sourceOwner) || !CanChange(targetOwner))
        {
            reason = BagFailReason.Locked;
            return false;
        }

        if (!BagRules.CanMerge(source, target, _gradeRepository.LastGrade))
        {
            reason = BagFailReason.CannotMerge;
            return false;
        }

        model.RemoveItem(source);
        target.Grade++;
        reason = BagFailReason.None;
        return true;
    }

    // 해제(A5): 가방의 아이템을 보관함 끝으로(회전 값은 그대로). 보관함 아이템이면 거짓
    public bool TryUnequip(BagModel model, int uid, out BagFailReason reason)
    {
        var item = model.FindItem(uid, out var owner);
        if (item == null)
        {
            return NotFound(uid, out reason);
        }

        if (owner < 0)
        {
            reason = BagFailReason.InStash;
            return false;
        }

        if (!CanChange(owner))
        {
            reason = BagFailReason.Locked;
            return false;
        }

        model.RemoveItem(item);
        model.AddToStash(item);
        reason = BagFailReason.None;
        return true;
    }

    // 적용(C절): 가방 칸 → 놓기. 보관함 아이템이고 합성 가능 → 합성. 보관함 빈 곳이거나 합성 안 되는 보관함 아이템 → 해제(집은 것이 보관함이면 실패)
    public bool TryApply(BagModel model, int uid, int rotation, BagTarget target, out BagFailReason reason)
    {
        if (target.Kind == BagTargetKind.Cell)
        {
            return TryDrop(model, uid, target.Member, target.X, target.Y, rotation, out reason);
        }

        if (target.Kind == BagTargetKind.StashItem)
        {
            var source = model.FindItem(uid, out _);
            var stashItem = model.FindItem(target.Uid, out _);
            if (source != null && stashItem != null && BagRules.CanMerge(source, stashItem, _gradeRepository.LastGrade))
            {
                return TryMerge(model, uid, target.Uid, out reason);
            }

            var isUnequipped = TryUnequip(model, uid, out reason);
            if (!isUnequipped && reason == BagFailReason.InStash)
            {
                // 보관함끼리 합성이 안 되는 곳에 놓음
                reason = BagFailReason.CannotMerge;
            }

            return isUnequipped;
        }

        return TryUnequip(model, uid, out reason);
    }

    // 빈 칸 놓기가 되는가(합성으로 가는 곳은 아니다). 진입점·자동 장착 계획이 쓴다
    public bool CanPlace(BagModel model, int uid, int member, int x, int y, int rotation)
    {
        var item = model.FindItem(uid, out var owner);
        if (item == null || !CanChange(owner) || !CanChange(member))
        {
            return false;
        }

        var bag = model.GetBag(member);
        var cover = BagRules.FindItemAt(bag, x, y, uid, _itemRepository);
        if (cover != null && BagRules.CanMerge(item, cover, _gradeRepository.LastGrade))
        {
            return false;
        }

        var shape = _itemRepository.Get(item.ItemId).Shape;
        return BagRules.Fits(bag, uid, shape, rotation, x, y, _itemRepository, _rules.BagWidth, _rules.BagHeight, out _);
    }

    // 가방 아이템이면 그 주인이 장비를 바꿀 수 있는가(F3 E절). 보관함(−1)은 늘 참
    private bool CanChange(int member) => member < 0 || _partyService.CanChangeEquipment(member);

    private bool IsPrep(out BagFailReason reason)
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Prep)
        {
            GameLogger.LogWarning($"[BagService] Bag change in {_runModel.Phase.CurrentValue}");
            reason = BagFailReason.NotPrep;
            return false;
        }

        reason = BagFailReason.None;
        return true;
    }

    private static bool NotFound(int uid, out BagFailReason reason)
    {
        GameLogger.LogError($"[BagService] No bag item {uid}");
        reason = BagFailReason.NotFound;
        return false;
    }

    // 성공이면 모든 대원 장착 목록 = 가방 목록 id 순서(A2), Version +1
    private bool Commit(bool isApplied)
    {
        if (!isApplied)
        {
            return false;
        }

        var memberList = _partyModel.MemberList;
        for (var member = 0; member < memberList.Count && member < _bagModel.MemberCount; member++)
        {
            memberList[member].SetEquippedItemIdList(_bagModel.GetBag(member).Select(item => item.ItemId));
        }

        _bagModel.IncrementVersion();
        return true;
    }

    private void PlaceStartItem(int member, string itemId)
    {
        var shape = _itemRepository.Get(itemId).Shape;
        var bag = _bagModel.GetBag(member);
        var start = _rules.StartCell;
        for (var y = start.Y; y < _rules.BagHeight; y++)
        {
            for (var x = y == start.Y ? start.X : 0; x < _rules.BagWidth; x++)
            {
                if (BagRules.Fits(bag, -1, shape, 0, x, y, _itemRepository, _rules.BagWidth, _rules.BagHeight, out _))
                {
                    _bagModel.AddToBag(member, new BagItem(_bagModel.TakeUid(), itemId, 0, 0, x, y));
                    return;
                }
            }
        }

        GameLogger.LogError($"[BagService] Start item {itemId} of member {member} does not fit from start cell");
    }
}
