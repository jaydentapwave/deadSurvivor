using System.Collections.Generic;

// 살아 있는 투사체·고리 번호 → 그림
public class ProjectileViewRegistry
{
    private readonly Dictionary<int, ProjectileView> _projectileViewDict = new();
    private readonly Dictionary<int, BurstView> _burstViewDict = new();

    public IEnumerable<int> ProjectileIds => _projectileViewDict.Keys;
    public IEnumerable<int> BurstIds => _burstViewDict.Keys;

    public void RegisterProjectile(int id, ProjectileView view) => _projectileViewDict.Add(id, view);

    public void UnregisterProjectile(int id) => _projectileViewDict.Remove(id);

    public bool TryGetProjectile(int id, out ProjectileView view) => _projectileViewDict.TryGetValue(id, out view);

    public void RegisterBurst(int id, BurstView view) => _burstViewDict.Add(id, view);

    public void UnregisterBurst(int id) => _burstViewDict.Remove(id);

    public bool TryGetBurst(int id, out BurstView view) => _burstViewDict.TryGetValue(id, out view);
}
