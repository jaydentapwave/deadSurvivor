using System;
using System.Collections.Generic;
using R3;

// 가방·보관함의 원본(기획서 A1). 쓰기 메서드는 BagService만 부른다. 바뀔 때마다 BagService가 Version을 1 올린다.
// 미리보기·자동 장착 계획은 CreateCopy로 만든 복사본에 같은 규칙을 적용해 본다
public class BagModel : IDisposable
{
    private readonly List<List<BagItem>> _bagList = new();
    private readonly List<BagItem> _stashList = new();
    private readonly List<int> _lastGrantedUidList = new();
    private readonly ReactiveProperty<int> _version = new(0);

    public ReadOnlyReactiveProperty<int> Version => _version;
    public int MemberCount => _bagList.Count;
    // 보관함 목록(앞이 먼저 들어온 것). 칸 X·Y는 쓰지 않는다
    public IReadOnlyList<BagItem> StashList => _stashList;
    // 마지막 전리품 지급의 Uid들("새" 표시)
    public IReadOnlyList<int> LastGrantedUidList => _lastGrantedUidList;
    public int NextUid { get; private set; } = 1;

    // 대원 가방 목록. 순서 번호 = 장착 목록 순서 = F3 아이템 칸 번호(A2)
    public IReadOnlyList<BagItem> GetBag(int member) => _bagList[member];

    // 그 Uid의 아이템. member = 가방 주인, 보관함이면 −1. 없으면 null
    public BagItem FindItem(int uid, out int member)
    {
        for (var i = 0; i < _bagList.Count; i++)
        {
            foreach (var item in _bagList[i])
            {
                if (item.Uid == uid)
                {
                    member = i;
                    return item;
                }
            }
        }

        member = -1;
        foreach (var item in _stashList)
        {
            if (item.Uid == uid)
            {
                return item;
            }
        }

        return null;
    }

    // 대원 수만큼 빈 가방, 빈 보관함, Uid 1부터
    public void Reset(int memberCount)
    {
        _bagList.Clear();
        for (var i = 0; i < memberCount; i++)
        {
            AddBag();
        }

        _stashList.Clear();
        _lastGrantedUidList.Clear();
        NextUid = 1;
    }

    public void AddBag() => _bagList.Add(new List<BagItem>());

    public int TakeUid() => NextUid++;

    public void AddToBag(int member, BagItem item) => _bagList[member].Add(item);

    public void AddToStash(BagItem item) => _stashList.Add(item);

    // 가방이나 보관함에서 뺀다
    public void RemoveItem(BagItem item)
    {
        foreach (var bag in _bagList)
        {
            if (bag.Remove(item))
            {
                return;
            }
        }

        _stashList.Remove(item);
    }

    public void SetLastGranted(IEnumerable<int> uids)
    {
        _lastGrantedUidList.Clear();
        _lastGrantedUidList.AddRange(uids);
    }

    public void IncrementVersion() => _version.Value++;

    // 아이템까지 깊은 복사(Version은 0부터). 쓴 쪽이 Dispose한다
    public BagModel CreateCopy()
    {
        var copy = new BagModel();
        foreach (var bag in _bagList)
        {
            var bagCopy = new List<BagItem>(bag.Count);
            foreach (var item in bag)
            {
                bagCopy.Add(item.Clone());
            }

            copy._bagList.Add(bagCopy);
        }

        foreach (var item in _stashList)
        {
            copy._stashList.Add(item.Clone());
        }

        copy._lastGrantedUidList.AddRange(_lastGrantedUidList);
        copy.NextUid = NextUid;
        return copy;
    }

    public void Dispose() => _version.Dispose();
}
