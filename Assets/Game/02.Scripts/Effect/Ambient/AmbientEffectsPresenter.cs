using System;
using UnityEngine;
using VContainer.Unity;

// View-only battle ambience. ParticleSystem owns emission/cap; PoolManager owns the instance.
public sealed class AmbientEffectsPresenter : IStartable, ITickable, IDisposable
{
    private const string DustKey = "HD2DAmbientDust";
    private readonly PoolManager _poolManager;
    private readonly ResourceService _resourceService;
    private readonly FieldView _field;
    private readonly PartyModel _party;
    private readonly RunModel _run;
    private readonly RunRulesData _rules;
    private readonly TerrainHeightGrid _terrain;
    private ParticleSystem _dust;
    private double _clock;
    private bool _wasBattle;
    private float _oldFrame;
    public int DustCount => _dust.particleCount;
    public int DustLimit => _dust.main.maxParticles;
    public float DustRate => _dust.emission.rateOverTime.constant;
    public int MotionFrame => AmbientMotionMath.FrameAt(_clock);

    public AmbientEffectsPresenter(PoolManager poolManager, ResourceService resourceService,
        FieldView field, PartyModel party, RunModel run, RunRulesData rules, TerrainHeightGrid terrain)
    {
        _poolManager = poolManager;
        _resourceService = resourceService;
        _field = field;
        _party = party;
        _run = run;
        _rules = rules;
        _terrain = terrain;
    }

    public void Start()
    {
        _oldFrame = Shader.GetGlobalFloat("_HD2DAmbientFrame");
        var prefab = _resourceService.Load<ParticleSystem>("Prefab/HD2DAmbientDust");
        _dust = _poolManager.Get(DustKey, prefab, _field.transform);
        _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Shader.SetGlobalFloat("_HD2DAmbientFrame", 0);
    }

    public void Tick()
    {
        var isBattle = _run.Phase.CurrentValue == RunPhase.Battle;
        if (!isBattle)
        {
            if (_wasBattle)
            {
                _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _clock = 0;
                Shader.SetGlobalFloat("_HD2DAmbientFrame", 0);
            }
            _wasBattle = false;
            return;
        }
        var floor = _party.Center * _rules.UnitScale;
        _dust.transform.position = new Vector3(floor.x, TerrainHeightMath.Sample(_terrain, floor.x, floor.y) + .7f, floor.y);
        if (!_wasBattle)
        {
            _dust.Play();
            _wasBattle = true;
        }
        _clock += Time.deltaTime;
        Shader.SetGlobalFloat("_HD2DAmbientFrame", MotionFrame);
    }

    public void Dispose()
    {
        Shader.SetGlobalFloat("_HD2DAmbientFrame", _oldFrame);
        // The scene may have destroyed its runtime-spawned particle root first.
        if (_dust != null)
        {
            _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _poolManager.Release(DustKey, _dust);
        }
    }
}
