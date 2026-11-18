using System.Collections.Generic;

// 날아가는 적 투사체 번호 → 그림
public class EnemyShotViewRegistry
{
    private readonly Dictionary<int, EnemyShotView> _viewDict = new();

    public IEnumerable<int> ShotIds => _viewDict.Keys;

    public void Register(int shotId, EnemyShotView view) => _viewDict.Add(shotId, view);

    public void Unregister(int shotId) => _viewDict.Remove(shotId);

    public bool TryGet(int shotId, out EnemyShotView view) => _viewDict.TryGetValue(shotId, out view);
}
