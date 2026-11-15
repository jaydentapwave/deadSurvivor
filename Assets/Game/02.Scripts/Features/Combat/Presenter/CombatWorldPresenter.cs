using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

// 투사체·범위 발동 고리 그림(기획서 F절): 모델의 번호마다 그림 하나, 새 번호는 풀에서 꺼내고 사라진 번호는 풀로 돌린다.
// 전투 걸음 뒤에 그리도록 LateTick(F1 WorldPresenter와 같다)
public class CombatWorldPresenter : IInitializable, ILateTickable
{
    private static readonly Vector2 CenterPivot = new(0.5f, 0.5f);
    // 투사체 원 지름(px)
    private const int ProjectilePixels = 5;
    // 고리 그림을 만드는 기준 반경(바닥 단위). 실제 반경 ÷ 이 값만큼 늘린다
    private const float BurstBaseRadius = 100f;
    private static readonly Color ProjectileColor = new(1f, 0.85f, 0.45f, 1f);
    private static readonly Color BurstColor = new(1f, 0.9f, 0.6f, 1f);
    private const float BurstMaxAlpha = 0.45f;

    private readonly CombatModel _combatModel;
    private readonly RunRulesData _runRules;
    private readonly PlaceholderSpriteFactory _placeholderSpriteFactory;
    private readonly ProjectileViewFactory _projectileViewFactory;
    private readonly ProjectileViewRegistry _projectileViewRegistry;

    private readonly HashSet<int> _aliveIdSet = new();
    private readonly List<int> _releaseIdList = new();
    private Sprite _projectileSprite;
    private Sprite _burstSprite;

    public CombatWorldPresenter(CombatModel combatModel, RunRulesData runRules, PlaceholderSpriteFactory placeholderSpriteFactory,
        ProjectileViewFactory projectileViewFactory, ProjectileViewRegistry projectileViewRegistry)
    {
        _combatModel = combatModel;
        _runRules = runRules;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _projectileViewFactory = projectileViewFactory;
        _projectileViewRegistry = projectileViewRegistry;
    }

    public void Initialize()
    {
        _projectileSprite = _placeholderSpriteFactory.CreateCircle(ProjectilePixels, ProjectileColor);
        // 바닥 위 원의 아이소 모양(가로 2 : 세로 1)을 마름모로 근사
        var burstWorld = IsoProjection.FloorToWorld(new Vector2(BurstBaseRadius, BurstBaseRadius), _runRules.UnitScale);
        var widthPixels = Mathf.Max(1, Mathf.RoundToInt(burstWorld.x * 2f * Const.PixelsPerUnit));
        var heightPixels = Mathf.Max(1, Mathf.RoundToInt(burstWorld.y * 2f * Const.PixelsPerUnit));
        _burstSprite = _placeholderSpriteFactory.CreateDiamond(widthPixels, heightPixels, BurstColor, CenterPivot);
    }

    public void LateTick()
    {
        SyncProjectileViews();
        SyncBurstViews();
    }

    private void SyncProjectileViews()
    {
        _aliveIdSet.Clear();
        foreach (var projectile in _combatModel.ProjectileList)
        {
            _aliveIdSet.Add(projectile.Id);
            if (!_projectileViewRegistry.TryGetProjectile(projectile.Id, out var view))
            {
                view = _projectileViewFactory.CreateProjectile(projectile.Id, _projectileSprite);
            }

            view.UpdateView(IsoProjection.FloorToWorld(projectile.Position, _runRules.UnitScale));
        }

        _releaseIdList.Clear();
        foreach (var id in _projectileViewRegistry.ProjectileIds)
        {
            if (!_aliveIdSet.Contains(id))
            {
                _releaseIdList.Add(id);
            }
        }

        foreach (var id in _releaseIdList)
        {
            _projectileViewFactory.ReleaseProjectile(id);
        }
    }

    private void SyncBurstViews()
    {
        _aliveIdSet.Clear();
        foreach (var burst in _combatModel.BurstList)
        {
            _aliveIdSet.Add(burst.Id);
            if (!_projectileViewRegistry.TryGetBurst(burst.Id, out var view))
            {
                view = _projectileViewFactory.CreateBurst(burst.Id, _burstSprite);
            }

            view.UpdateView(IsoProjection.FloorToWorld(burst.Position, _runRules.UnitScale), burst.Radius / BurstBaseRadius,
                BurstMaxAlpha * Mathf.Clamp01(burst.RemainingSeconds / burst.Seconds));
        }

        _releaseIdList.Clear();
        foreach (var id in _projectileViewRegistry.BurstIds)
        {
            if (!_aliveIdSet.Contains(id))
            {
                _releaseIdList.Add(id);
            }
        }

        foreach (var id in _releaseIdList)
        {
            _projectileViewFactory.ReleaseBurst(id);
        }
    }
}
