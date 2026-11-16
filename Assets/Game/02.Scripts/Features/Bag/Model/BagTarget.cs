// 집은 아이템을 놓을 대상(기획서 C절 조작 흐름): 가방 칸, 보관함 아이템, 보관함 빈 곳
public enum BagTargetKind
{
    Cell,
    StashItem,
    StashEmpty,
}

public readonly struct BagTarget
{
    public BagTargetKind Kind { get; }
    // Cell일 때 대원 번호와 칸
    public int Member { get; }
    public int X { get; }
    public int Y { get; }
    // StashItem일 때 그 아이템
    public int Uid { get; }

    private BagTarget(BagTargetKind kind, int member, int x, int y, int uid)
    {
        Kind = kind;
        Member = member;
        X = x;
        Y = y;
        Uid = uid;
    }

    public static BagTarget Cell(int member, int x, int y) => new(BagTargetKind.Cell, member, x, y, -1);

    public static BagTarget StashItem(int uid) => new(BagTargetKind.StashItem, -1, 0, 0, uid);

    public static BagTarget StashEmpty() => new(BagTargetKind.StashEmpty, -1, 0, 0, -1);

    public bool IsSame(BagTarget other) =>
        Kind == other.Kind && Member == other.Member && X == other.X && Y == other.Y && Uid == other.Uid;
}
