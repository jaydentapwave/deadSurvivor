using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

// T1~T3: 기존 풀·등록부로 투사체와 발동 그림을 동기화. 피격 별은 피해 기록 소비 순서와 독립적이다.
public class CombatWorldPresenter : IInitializable, ILateTickable, IDisposable
{
    private const float HitSeconds = 0.15f;
    private const float BodyHeight = 24f / Const.PixelsPerUnit;
    private const float HealHeight = 56f / Const.PixelsPerUnit;
    private readonly CombatModel _combatModel;
    private readonly CombatFeedbackModel _combatFeedbackModel;
    private readonly RunModel _runModel;
    private readonly RunRulesData _runRules;
    private readonly CombatEffectData _effectData;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly ProjectileViewFactory _projectileViewFactory;
    private readonly ProjectileViewRegistry _projectileViewRegistry;
    private readonly HashSet<int> _aliveProjectileIdSet = new();
    private readonly HashSet<long> _aliveBurstIdSet = new();
    private readonly List<int> _releaseProjectileIdList = new();
    private readonly List<long> _releaseBurstIdList = new();
    private readonly List<HitEffect> _hitList = new();
    private readonly Dictionary<string, int> _shownCountDict = new();
    private int _nextHitId;

    public CombatWorldPresenter(CombatModel combatModel, CombatFeedbackModel combatFeedbackModel, RunModel runModel,
        RunRulesData runRules, CombatEffectData effectData, SpriteAtlasService spriteAtlasService,
        ProjectileViewFactory projectileViewFactory, ProjectileViewRegistry projectileViewRegistry)
    {
        _combatModel = combatModel;
        _combatFeedbackModel = combatFeedbackModel;
        _runModel = runModel;
        _runRules = runRules;
        _effectData = effectData;
        _spriteAtlasService = spriteAtlasService;
        _projectileViewFactory = projectileViewFactory;
        _projectileViewRegistry = projectileViewRegistry;
    }

    public void Initialize() => _combatFeedbackModel.MonsterHit += OnMonsterHit;

    public void Dispose() => _combatFeedbackModel.MonsterHit -= OnMonsterHit;

    private void OnMonsterHit(MonsterHitRecord hit)
    {
        _hitList.Add(new HitEffect { Id = --_nextHitId, Position = hit.Position, Frame = Time.frameCount });
    }

    public int GetShownCount(string spriteName) => _shownCountDict.TryGetValue(spriteName, out var count) ? count : 0;

    public void LateTick()
    {
        SyncProjectileViews();
        _aliveBurstIdSet.Clear();
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            _hitList.Clear();
        }

        foreach (var burst in _combatModel.BurstList)
        {
            DrawBurst(burst);
        }

        var delta = _runModel.IsPaused.CurrentValue ? 0f : Time.deltaTime;
        for (var i = _hitList.Count - 1; i >= 0; i--)
        {
            var hit = _hitList[i];
            if (hit.Frame != Time.frameCount)
            {
                hit.Remaining -= delta;
            }

            if (hit.Remaining <= 0f)
            {
                _hitList.RemoveAt(i);
                continue;
            }

            Draw((long)hit.Id << 32, "hit_star", World(hit.Position) + Vector2.up * BodyHeight,
                Vector2.one, Mathf.Clamp01(hit.Remaining / HitSeconds));
        }

        _releaseBurstIdList.Clear();
        foreach (var id in _projectileViewRegistry.BurstIds)
        {
            if (!_aliveBurstIdSet.Contains(id))
            {
                _releaseBurstIdList.Add(id);
            }
        }

        foreach (var id in _releaseBurstIdList)
        {
            _projectileViewFactory.ReleaseBurst(id);
        }
    }

    private void SyncProjectileViews()
    {
        _aliveProjectileIdSet.Clear();
        foreach (var projectile in _combatModel.ProjectileList)
        {
            _aliveProjectileIdSet.Add(projectile.Id);
            if (!_projectileViewRegistry.TryGetProjectile(projectile.Id, out var view))
            {
                var spriteName = _effectData.Get(projectile.ItemId).Projectile;
                view = _projectileViewFactory.CreateProjectile(projectile.Id, _spriteAtlasService.Get(spriteName));
                MarkShown(spriteName);
            }

            view.UpdateView(World(projectile.Position) + Vector2.up * BodyHeight, World(projectile.Direction));
        }

        _releaseProjectileIdList.Clear();
        foreach (var id in _projectileViewRegistry.ProjectileIds)
        {
            if (!_aliveProjectileIdSet.Contains(id))
            {
                _releaseProjectileIdList.Add(id);
            }
        }

        foreach (var id in _releaseProjectileIdList)
        {
            _projectileViewFactory.ReleaseProjectile(id);
        }
    }

    private void DrawBurst(BurstState burst)
    {
        var spriteName = _effectData.Get(burst.ItemId).Burst;
        var sprite = _spriteAtlasService.Get(spriteName);
        var alpha = Mathf.Clamp01(burst.RemainingSeconds / burst.Seconds);
        var position = World(burst.Position);
        var key = (long)burst.Id << 32;
        if (burst.Kind == BurstEffectKind.Chain)
        {
            var from = CombatEffectMotion.Snap(position + Vector2.up * BodyHeight);
            var to = CombatEffectMotion.Snap(World(burst.EndPosition) + Vector2.up * BodyHeight);
            var width = sprite.rect.width / Const.PixelsPerUnit;
            var count = CombatEffectMotion.GetSegmentCount(Vector2.Distance(from, to), width);
            var angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            for (var i = 0; i < count; i++)
            {
                CombatEffectMotion.GetSegment(from, to, width, i, out var center, out var scaleX);
                Draw(key + i, spriteName, center, new Vector2(scaleX, 1f), alpha, angle, snapToPixels: false);
            }

            return;
        }

        var scale = Vector2.one;
        var isFlipped = false;
        switch (burst.Kind)
        {
            case BurstEffectKind.Melee:
                position += World(burst.Direction).normalized * (sprite.rect.width / Const.PixelsPerUnit * 0.5f);
                position += Vector2.up * BodyHeight;
                // 원본 베기 날이 왼쪽이다. 목표가 오른쪽이면 반전한다.
                isFlipped = burst.Direction.x > 0f;
                break;
            case BurstEffectKind.Heal:
                position += Vector2.up * (HealHeight + (1f - alpha) * 4f / Const.PixelsPerUnit);
                break;
            case BurstEffectKind.HealArea:
                scale *= burst.Radius * _runRules.UnitScale * 2f / (sprite.rect.width / Const.PixelsPerUnit);
                break;
            case BurstEffectKind.Explosion:
                position += Vector2.up * BodyHeight;
                scale *= 1f + (1f - alpha) * 0.2f;
                break;
            case BurstEffectKind.Pulse:
                scale *= burst.Radius * _runRules.UnitScale * 2f / (sprite.rect.width / Const.PixelsPerUnit);
                break;
        }

        Draw(key, spriteName, position, scale, alpha, isFlipped: isFlipped);
    }

    private void Draw(long id, string spriteName, Vector2 position, Vector2 scale, float alpha,
        float angle = 0f, bool isFlipped = false, bool snapToPixels = true)
    {
        _aliveBurstIdSet.Add(id);
        if (!_projectileViewRegistry.TryGetBurst(id, out var view))
        {
            view = _projectileViewFactory.CreateBurst(id, _spriteAtlasService.Get(spriteName));
            MarkShown(spriteName);
        }

        view.UpdateView(position, scale, alpha, angle, isFlipped, snapToPixels);
    }

    private Vector2 World(Vector2 floor) => IsoProjection.FloorToWorld(floor, _runRules.UnitScale);

    private void MarkShown(string spriteName) => _shownCountDict[spriteName] = GetShownCount(spriteName) + 1;

    private class HitEffect
    {
        public int Id;
        public Vector2 Position;
        public int Frame;
        public float Remaining = HitSeconds;
    }
}
