// 아이템 칸의 가방 배치 정보(기획서 B2). 칸 = 대원 장착 목록의 순서 번호. 가방(F5)이 등록을 바꾼다
public interface IItemPlacementInfo
{
    float GetGradeMultiplier(int memberIndex, int slotIndex);

    bool IsAdjacent(int memberIndex, int slotA, int slotB);
}
