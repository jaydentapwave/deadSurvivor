using System.Collections.Generic;

// HP 막대가 보이는 몬스터 번호 → 막대 그림(MonsterViewRegistry와 같은 모양)
public class MonsterHpBarViewRegistry
{
    private readonly Dictionary<int, MonsterHpBarView> _viewDict = new();

    public IEnumerable<int> MonsterIds => _viewDict.Keys;
    public int Count => _viewDict.Count;

    public void Register(int monsterId, MonsterHpBarView view) => _viewDict.Add(monsterId, view);

    public void Unregister(int monsterId) => _viewDict.Remove(monsterId);

    public bool TryGet(int monsterId, out MonsterHpBarView view) => _viewDict.TryGetValue(monsterId, out view);

    public bool Contains(int monsterId) => _viewDict.ContainsKey(monsterId);
}
