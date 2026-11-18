using System.Collections.Generic;

// 살아 있는 적 번호 → 그림
public class MonsterViewRegistry
{
    private readonly Dictionary<int, MonsterView> _viewDict = new();

    public IEnumerable<int> MonsterIds => _viewDict.Keys;
    public IEnumerable<MonsterView> Views => _viewDict.Values;

    public void Register(int monsterId, MonsterView view) => _viewDict.Add(monsterId, view);

    public void Unregister(int monsterId) => _viewDict.Remove(monsterId);

    public bool TryGet(int monsterId, out MonsterView view) => _viewDict.TryGetValue(monsterId, out view);
}
