// 가방이 없을 때의 배치 정보: 등급 배수 1, 인접 없음(기획서 B2). F5가 가방 구현으로 바꾼다
public class NoBagPlacementInfo : IItemPlacementInfo
{
    public float GetGradeMultiplier(int memberIndex, int slotIndex) => 1f;

    public bool IsAdjacent(int memberIndex, int slotA, int slotB) => false;
}
