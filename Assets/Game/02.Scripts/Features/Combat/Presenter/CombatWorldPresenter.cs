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
        ProjectileViewFactory projectileViewFactory, ProjectileViewRegistry projectileViewRegistry, ViewRulesData viewRules)
    {
        _combatModel = combatModel;
        _combatFeedbackModel = combatFeedbackModel;
        _runModel = runModel;
        _runRules = runRules;
        _viewRules = viewRules;
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
            if (!_projectileViewRegistry.TryGetProjectile(projectile.Id, out var view))
            {
                var spriteName = _effectData.Get(projectile.ItemId).Projectile;
                view = _projectileViewFactory.CreateProjectile(projectile.Id, _spriteAtlasService.Get(spriteName));
                MarkShown(spriteName);
            }

            view.UpdateView(World(projectile.Position) + TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees), Screen(projectile.Direction));
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
        var alpha = Mathf.Clamp01(burst.RemainingSeconds / burst.Seconds);
        var position = World(burst.Position);
        var key = (long)burst.Id << 32;
        if (burst.Kind == BurstEffectKind.Chain)
        {
            var from = position + TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees);
            var to = World(burst.EndPosition) + TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees);
            var screen = Screen(burst.EndPosition - burst.Position);
            var width = sprite.rect.width / Const.PixelsPerUnit;
            var count = CombatEffectMotion.GetSegmentCount(screen.magnitude, width);
            var angle = Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg;
            for (var i = 0; i < count; i++)
            {
                CombatEffectMotion.GetSegment(Vector2.zero, screen, width, i, out var center, out var scaleX);
                var ratio = screen.sqrMagnitude > 0 ? Vector2.Dot(center, screen) / screen.sqrMagnitude : .5f;
                Draw(key + i, spriteName, Vector3.Lerp(from, to, ratio), new Vector2(scaleX, 1f), alpha, angle, snapToPixels: false);
            }

            return;
        }

        var scale = Vector2.one;
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
                scale *= burst.Radius * _runRules.UnitScale * 2f / (sprite.rect.width / Const.PixelsPerUnit);
                break;
            case BurstEffectKind.Explosion:
                position += TiltedViewMath.UprightOffset(BodyHeight, _viewRules.TiltDegrees);

                break;
            case BurstEffectKind.Pulse:
                scale *= burst.Radius * _runRules.UnitScale * 2f / (sprite.rect.width / Const.PixelsPerUnit);
                break;
        }

        DrawFrame(key, spriteName, burst.Seconds - burst.RemainingSeconds, position, isFlipped,
            burst.Kind == BurstEffectKind.HealArea || burst.Kind == BurstEffectKind.Pulse);
    }

    private void DrawFrame(long id, string key, float elapsed, Vector3 position, bool isFlipped = false, bool isGround = false)
    {
        var clip = _effectData.GetClip(key);
        var index = EffectFrameMath.FrameAt(elapsed, clip.Frames, clip.Fps, clip.Loop);
        var sprite = _spriteAtlasService.Get(clip.FrameKey(index));
        _aliveBurstIdSet.Add(id);
        if (!_projectileViewRegistry.TryGetBurst(id, out var view))
        {
            view = _projectileViewFactory.CreateBurst(id, sprite);
            MarkShown(key);
        }
        view.UpdateFrame(position, sprite, key, index + 1, clip.MaterialIndex, isFlipped, isGround, _viewRules.SinTilt);
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

    private void Draw(long id, string spriteName, Vector3 position, Vector2 scale, float alpha,
        float angle = 0f, bool isFlipped = false, bool snapToPixels = true, bool isGround = false)
    {
        _aliveBurstIdSet.Add(id);
        if (!_projectileViewRegistry.TryGetBurst(id, out var view))
        {
            view = _projectileViewFactory.CreateBurst(id, _spriteAtlasService.Get(_effectData.GetClip(spriteName).FrameKey(0)));
            MarkShown(spriteName);
        }

        view.UpdateView(position, scale, alpha, angle, isFlipped, snapToPixels, isGround);
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
