// F3 배치 정보 자리를 실제 가방으로 채운다(기획서 A3). 칸 번호 = 그 대원 가방 목록의 순서 번호(= 장착 목록 순서, A2)
public class BagPlacementInfo : IItemPlacementInfo
{
    private readonly BagModel _bagModel;
    private readonly GradeRepository _gradeRepository;
    private readonly ItemRepository _itemRepository;

    public BagPlacementInfo(BagModel bagModel, GradeRepository gradeRepository, ItemRepository itemRepository)
    {
        _bagModel = bagModel;
        _gradeRepository = gradeRepository;
        _itemRepository = itemRepository;
    }

    public float GetGradeMultiplier(int memberIndex, int slotIndex)
    {
        if (!HasSlot(memberIndex, slotIndex))
        {
            return 1f;
        }

        return _gradeRepository.Get(_bagModel.GetBag(memberIndex)[slotIndex].Grade).Mult;
    }

    public bool IsAdjacent(int memberIndex, int slotA, int slotB)
    {
        if (!HasSlot(memberIndex, slotA) || !HasSlot(memberIndex, slotB))
        {
            return false;
        }

        var bag = _bagModel.GetBag(memberIndex);
        return BagRules.IsAdjacent(bag[slotA], bag[slotB], _itemRepository);
    }

    // F3 계산은 장착 목록과 같은 수의 칸만 묻는다. 밖이면 가방과 장착 목록이 어긋난 것
    private bool HasSlot(int memberIndex, int slotIndex)
    {
        if (memberIndex < 0 || memberIndex >= _bagModel.MemberCount || slotIndex < 0
            || slotIndex >= _bagModel.GetBag(memberIndex).Count)
        {
            GameLogger.LogError($"[BagPlacementInfo] No bag slot {slotIndex} for member {memberIndex}");
            return false;
        }

        return true;
    }
}
