using System.Collections.Generic;
using UnityEngine;

// 가방 규칙(기획서 A4, 예전 EquipmentRules.Shape·Fits·Adjacent·MergeAllowed). 상태 없는 순수 함수.
// 좌표: x = 가방 오른쪽, y = 가방 아래(화면 위 줄이 y 0)
public static class BagRules
{
    // 칸 모양을 회전 수만큼 (x, y) → (−y, x)로 돌린 뒤 가장 작은 x·y를 0으로 당긴다. result를 비우고 채운다
    public static void GetCells(IReadOnlyList<ItemCell> shape, int rotation, List<Vector2Int> result)
    {
        result.Clear();
        foreach (var cell in shape)
        {
            result.Add(new Vector2Int(cell.X, cell.Y));
        }

        var turns = ((rotation % 4) + 4) % 4;
        for (var turn = 0; turn < turns; turn++)
        {
            for (var i = 0; i < result.Count; i++)
            {
                result[i] = new Vector2Int(-result[i].y, result[i].x);
            }
        }

        if (result.Count == 0)
        {
            return;
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        foreach (var cell in result)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }

        for (var i = 0; i < result.Count; i++)
        {
            result[i] = new Vector2Int(result[i].x - minX, result[i].y - minY);
        }
    }

    // 돌린 모양을 왼쪽 위 (x, y)에 놓았을 때의 가방 칸들
    public static void GetPlacedCells(IReadOnlyList<ItemCell> shape, int rotation, int x, int y, List<Vector2Int> result)
    {
        GetCells(shape, rotation, result);
        for (var i = 0; i < result.Count; i++)
        {
            result[i] += new Vector2Int(x, y);
        }
    }

    // 그 아이템이 지금 차지한 가방 칸들
    public static void GetItemCells(BagItem item, ItemRepository itemRepository, List<Vector2Int> result) =>
        GetPlacedCells(itemRepository.Get(item.ItemId).Shape, item.Rotation, item.X, item.Y, result);

    // 들어감: 모든 칸이 가방 안이고, 그 가방의 다른 아이템(같은 Uid는 뺀다) 칸과 겹치지 않는다
    public static bool Fits(IReadOnlyList<BagItem> bag, int uid, IReadOnlyList<ItemCell> shape, int rotation, int x, int y,
        ItemRepository itemRepository, int width, int height, out BagFailReason reason)
    {
        var cellList = new List<Vector2Int>();
        GetPlacedCells(shape, rotation, x, y, cellList);
        foreach (var cell in cellList)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
            {
                reason = BagFailReason.OutOfBag;
                return false;
            }
        }

        var otherCellList = new List<Vector2Int>();
        foreach (var other in bag)
        {
            if (other.Uid == uid)
            {
                continue;
            }

            GetItemCells(other, itemRepository, otherCellList);
            foreach (var cell in cellList)
            {
                if (otherCellList.Contains(cell))
                {
                    reason = BagFailReason.Overlap;
                    return false;
                }
            }
        }

        reason = BagFailReason.None;
        return true;
    }

    // 그 칸을 덮는 아이템(excludeUid는 뺀다). 없으면 null
    public static BagItem FindItemAt(IReadOnlyList<BagItem> bag, int x, int y, int excludeUid, ItemRepository itemRepository)
    {
        var cellList = new List<Vector2Int>();
        var target = new Vector2Int(x, y);
        foreach (var item in bag)
        {
            if (item.Uid == excludeUid)
            {
                continue;
            }

            GetItemCells(item, itemRepository, cellList);
            if (cellList.Contains(target))
            {
                return item;
            }
        }

        return null;
    }

    // 인접: 서로 다른 두 아이템의 칸 중 |Δx| + |Δy| = 1인 쌍이 있다
    public static bool IsAdjacent(BagItem a, BagItem b, ItemRepository itemRepository)
    {
        if (a.Uid == b.Uid)
        {
            return false;
        }

        var cellListA = new List<Vector2Int>();
        var cellListB = new List<Vector2Int>();
        GetItemCells(a, itemRepository, cellListA);
        GetItemCells(b, itemRepository, cellListB);
        foreach (var cellA in cellListA)
        {
            foreach (var cellB in cellListB)
            {
                if (Mathf.Abs(cellA.x - cellB.x) + Mathf.Abs(cellA.y - cellB.y) == 1)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 합성 가능: 서로 다른 Uid, 같은 아이템 id, 같은 등급, 등급 < 마지막 등급 번호
    public static bool CanMerge(BagItem source, BagItem target, int lastGrade) =>
        source.Uid != target.Uid && source.ItemId == target.ItemId && source.Grade == target.Grade && target.Grade < lastGrade;
}
