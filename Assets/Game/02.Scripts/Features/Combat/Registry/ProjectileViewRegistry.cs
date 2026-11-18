using System.Collections.Generic;

// 살아 있는 투사체·고리 번호 → 그림
public class ProjectileViewRegistry
{
    private readonly Dictionary<int, ProjectileView> _projectileViewDict = new();
    private readonly Dictionary<long, BurstView> _burstViewDict = new();

    public IEnumerable<int> ProjectileIds => _projectileViewDict.Keys;
    public IEnumerable<long> BurstIds => _burstViewDict.Keys;

    public void RegisterProjectile(int id, ProjectileView view) => _projectileViewDict.Add(id, view);

    public void UnregisterProjectile(int id) => _projectileViewDict.Remove(id);

    public bool TryGetProjectile(int id, out ProjectileView view) => _projectileViewDict.TryGetValue(id, out view);

    public void RegisterBurst(long id, BurstView view) => _burstViewDict.Add(id, view);

    public void UnregisterBurst(long id) => _burstViewDict.Remove(id);

    public bool TryGetBurst(long id, out BurstView view) => _burstViewDict.TryGetValue(id, out view);
}
