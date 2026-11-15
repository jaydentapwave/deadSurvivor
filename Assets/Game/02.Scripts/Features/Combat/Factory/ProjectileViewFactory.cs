using UnityEngine;

// 투사체·고리 그림을 풀에서 꺼내 그림을 넣고 등록부에 올린다. 돌려줄 때는 등록부에서 빼고 풀로.
public class ProjectileViewFactory
{
    private const string ProjectilePoolKey = "ProjectileView";
    private const string BurstPoolKey = "BurstView";
    private const string ProjectilePrefabPath = "Prefab/ProjectileView";
    private const string BurstPrefabPath = "Prefab/BurstView";

    private readonly PoolManager _poolManager;
    private readonly ResourceService _resourceService;
    private readonly ProjectileViewRegistry _projectileViewRegistry;
    private readonly Transform _parent;

    private ProjectileView _projectilePrefab;
    private BurstView _burstPrefab;

    // parent: 장면의 CombatEffects 오브젝트
    public ProjectileViewFactory(PoolManager poolManager, ResourceService resourceService,
        ProjectileViewRegistry projectileViewRegistry, Transform parent)
    {
        _poolManager = poolManager;
        _resourceService = resourceService;
        _projectileViewRegistry = projectileViewRegistry;
        _parent = parent;
    }

    public ProjectileView CreateProjectile(int id, Sprite sprite)
    {
        if (_projectilePrefab == null)
        {
            _projectilePrefab = _resourceService.Load<ProjectileView>(ProjectilePrefabPath);
        }

        var view = _poolManager.Get(ProjectilePoolKey, _projectilePrefab, _parent);
        view.Initialize(sprite);
        _projectileViewRegistry.RegisterProjectile(id, view);
        return view;
    }

    public BurstView CreateBurst(int id, Sprite sprite)
    {
        if (_burstPrefab == null)
        {
            _burstPrefab = _resourceService.Load<BurstView>(BurstPrefabPath);
        }

        var view = _poolManager.Get(BurstPoolKey, _burstPrefab, _parent);
        view.Initialize(sprite);
        _projectileViewRegistry.RegisterBurst(id, view);
        return view;
    }

    public void ReleaseProjectile(int id)
    {
        if (!_projectileViewRegistry.TryGetProjectile(id, out var view))
        {
            GameLogger.LogWarning($"[ProjectileViewFactory] No projectile view {id}");
            return;
        }

        _projectileViewRegistry.UnregisterProjectile(id);
        _poolManager.Release(ProjectilePoolKey, view);
    }

    public void ReleaseBurst(int id)
    {
        if (!_projectileViewRegistry.TryGetBurst(id, out var view))
        {
            GameLogger.LogWarning($"[ProjectileViewFactory] No burst view {id}");
            return;
        }

        _projectileViewRegistry.UnregisterBurst(id);
        _poolManager.Release(BurstPoolKey, view);
    }
}
