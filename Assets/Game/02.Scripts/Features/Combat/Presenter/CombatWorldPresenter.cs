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
    private readonly ViewRulesData _viewRules;
    private readonly CombatEffectData _effectData;
    private readonly EffectSpriteCache _effectSpriteCache;
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
    private readonly Dictionary<string, List<int>> _frameSeenDict = new();
    private readonly Dictionary<string, long> _frameObservedIdDict = new();
    public string GetFrameSequence(string key) => _frameSeenDict.TryGetValue(key, out var frames) ? string.Join(",", frames) : "";

    public CombatWorldPresenter(CombatModel combatModel, CombatFeedbackModel combatFeedbackModel, RunModel runModel,
        RunRulesData runRules, CombatEffectData effectData, SpriteAtlasService spriteAtlasService,
        ProjectileViewFactory projectileViewFactory, ProjectileViewRegistry projectileViewRegistry, ViewRulesData viewRules, EffectSpriteCache effectSpriteCache)
    {
        _combatModel = combatModel;
        _combatFeedbackModel = combatFeedbackModel;
        _runModel = runModel;
        _runRules = runRules;
        _viewRules = viewRules;
        _effectData = effectData;
        _effectSpriteCache = effectSpriteCache;
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

            DrawFrame((long)hit.Id << 32, "hit_star", HitSeconds - hit.Remaining,
                World(hit.Position) + TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees));
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
            var spriteName = _effectData.Get(projectile.ItemId).Projectile;
            var clip = _effectData.GetClip(spriteName);
            var facing = EffectFrameMath.DirectionSprite(Mathf.Atan2(Screen(projectile.Direction).y, Screen(projectile.Direction).x) * Mathf.Rad2Deg);
            var index = EffectFrameMath.FrameAt(projectile.Traveled / projectile.Speed, clip.Frames, clip.Fps, clip.Loop);
            var sprite = _spriteAtlasService.Get(clip.FrameKey(index, EffectFrameMath.DirectionName(facing.index)));
            if (!_projectileViewRegistry.TryGetProjectile(projectile.Id, out var view))
            {
                view = _projectileViewFactory.CreateProjectile(projectile.Id, sprite);
                MarkShown(spriteName);
            }

            view.SetFrame(sprite, spriteName, index + 1, clip.MaterialIndex == 1);
            view.UpdateView(World(projectile.Position), Screen(projectile.Direction));
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
        var clip = _effectData.GetClip(spriteName);
        var sprite = _spriteAtlasService.Get(clip.FrameKey(0));
        var position = World(burst.Position);
        var key = (long)burst.Id << 32;
        if (burst.Kind == BurstEffectKind.Chain)
        {
            var from = position + TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees);
            var to = World(burst.EndPosition) + TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees);
            var delta = to - from;
            var dx = Mathf.RoundToInt(delta.x * 48);
            var dy = Mathf.RoundToInt(delta.z * _viewRules.SinTilt * 48);
            if (dx == 0 && dy == 0)
            {
                return;
            }
            var chainElapsed = burst.Seconds - burst.RemainingSeconds;
            var frame = EffectFrameMath.FrameAt(chainElapsed, clip.Frames, clip.Fps, false);
            DrawFrame(key, spriteName, chainElapsed, from, false, false,
                _effectSpriteCache.Chain(dx, dy, (uint)burst.Id, frame));

            return;
        }

        var isFlipped = false;
        switch (burst.Kind)
        {
            case BurstEffectKind.Melee:
                position += _viewRulesOffset(Screen(burst.Direction).normalized * (sprite.rect.width / Const.PixelsPerUnit * .5f));
                position += TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees);
                // 원본 베기 날이 왼쪽이다. 목표가 오른쪽이면 반전한다.
                isFlipped = burst.Direction.x > 0f;
                break;
            case BurstEffectKind.Heal:
                position += TiltedViewMath.UprightOffset(HealHeight, _viewRules.TiltDegrees);
                break;
            case BurstEffectKind.HealArea:
                break;
            case BurstEffectKind.Explosion:
                position += TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees);

                break;
            case BurstEffectKind.Pulse:
                break;
        }

        var elapsed = burst.Seconds - burst.RemainingSeconds;
        if (burst.Kind == BurstEffectKind.HealArea || burst.Kind == BurstEffectKind.Pulse)
        {
            var frame = EffectFrameMath.FrameAt(elapsed, clip.Frames, clip.Fps, false);
            var ring = _effectSpriteCache.Ring(burst.Kind == BurstEffectKind.HealArea ? "heal" : "pulse", burst.Radius * _runRules.UnitScale, frame);
            DrawFrame(key, spriteName, elapsed, position, false, true, ring);
        }
        else
        {
            DrawFrame(key, spriteName, elapsed, position, isFlipped);
            if (burst.Kind == BurstEffectKind.Explosion)
            {
                var groundClip = _effectData.GetClip("ground_boom");
                var frame = EffectFrameMath.FrameAt(elapsed, groundClip.Frames, groundClip.Fps, false);
                DrawFrame(key + 1, "ground_boom", elapsed, World(burst.Position), false, true,
                    _effectSpriteCache.Ring("boom", burst.Radius * _runRules.UnitScale, frame));
            }
        }
    }

    private void DrawFrame(long id, string key, float elapsed, Vector3 position, bool isFlipped = false, bool isGround = false, Sprite rasterSprite = null)
    {
        var clip = _effectData.GetClip(key);
        var index = EffectFrameMath.FrameAt(elapsed, clip.Frames, clip.Fps, clip.Loop);
        var sprite = rasterSprite == null ? _spriteAtlasService.Get(clip.FrameKey(index)) : rasterSprite;
        _aliveBurstIdSet.Add(id);
        if (!_projectileViewRegistry.TryGetBurst(id, out var view))
        {
            view = _projectileViewFactory.CreateBurst(id, sprite);
            MarkShown(key);
        }
        view.UpdateView(position, sprite, key, index + 1, clip.MaterialIndex, isFlipped, isGround, _viewRules.SinTilt);
        if (!_frameSeenDict.TryGetValue(key, out var frames))
        {
            frames = new List<int>();
            _frameSeenDict.Add(key, frames);
            _frameObservedIdDict.Add(key, id);
        }
        if (_frameObservedIdDict[key] == id && (frames.Count == 0 || frames[frames.Count - 1] != index + 1))
        {
            frames.Add(index + 1);
        }
    }

    private Vector3 World(Vector2 floor) => IsoProjection.FloorToWorldSnapped(floor, _runRules.UnitScale, _viewRules.SinTilt);
    private Vector2 Screen(Vector2 floor) => TiltedViewMath.FloorToScreenPlane(floor * _runRules.UnitScale, _viewRules.SinTilt);
    private Vector3 _viewRulesOffset(Vector2 screen) => TiltedViewMath.UprightRotation(_viewRules.TiltDegrees) * (Vector3)screen;

    private void MarkShown(string spriteName) => _shownCountDict[spriteName] = GetShownCount(spriteName) + 1;

    private class HitEffect
    {
        public int Id;
        public Vector2 Position;
        public int Frame;
        public float Remaining = HitSeconds;
    }
}
