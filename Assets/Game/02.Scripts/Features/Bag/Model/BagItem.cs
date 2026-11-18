// 가방·보관함의 아이템 하나(기획서 A1). 칸 X·Y(왼쪽 위)와 회전은 가방에 있을 때만 뜻이 있다. 값을 바꾸는 쪽은 BagService뿐이다
public class BagItem
{
    // 이번 판 번호(1부터)
    public int Uid { get; }
    public string ItemId { get; }
    // 등급 번호(Grade/grades.json 배열 순서)
    public int Grade { get; set; }
    // 90° 회전 수 0~3
    public int Rotation { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public BagItem(int uid, string itemId, int grade, int rotation, int x, int y)
    {
        Uid = uid;
        ItemId = itemId;
        Grade = grade;
        Rotation = rotation;
        X = x;
        Y = y;
    }

    public BagItem Clone() => new(Uid, ItemId, Grade, Rotation, X, Y);
}
