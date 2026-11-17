using System;
using R3;
using UnityEngine;
using VContainer.Unity;

// 원정대 피격 → 카메라 정수 픽셀 흔들림·진동(F6 기획서 D절). 피격 깃발을 프레임마다 한 번 읽고, 흔들림과 진동은 각자의 간격으로 따로 판정한다.
// Rig가 초점과 흔들림을 따로 받아 순서에 관계없이 위치를 정한다.
public class HitReactionPresenter : IInitializable, ILateTickable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly CombatFeedbackModel _combatFeedbackModel;
    private readonly ShakeRulesData _rules;
    private readonly HapticService _hapticService;
    private readonly TiltedCameraRig _cameraShakeView;
    private readonly CompositeDisposable _disposables = new();

    // 피드백 시계(전투 중·일시정지 아님일 때만 흐름)
    private float _clock;
    private float _lastShakeStart = float.NegativeInfinity;
    private float _shakeRemaining;
    private int _shakeStep;
    // 마지막으로 얹은 오프셋. 일시정지(시계 0) 중에는 이것을 그대로 다시 얹는다
    private Vector2 _offset;

    // 남은 시간이 있거나 카메라에 아직 오프셋이 얹혀 있다(시간이 다 하는 프레임에 오프셋은 바로 0이 된다)
    public bool IsShaking => _shakeRemaining > 0f || _offset != Vector2.zero;

    public HitReactionPresenter(RunModel runModel, CombatFeedbackModel combatFeedbackModel,
        FeedbackRulesRepository feedbackRulesRepository, HapticService hapticService, TiltedCameraRig cameraShakeView)
    {
        _runModel = runModel;
        _combatFeedbackModel = combatFeedbackModel;
        _rules = feedbackRulesRepository.Rules.Shake;
        _hapticService = hapticService;
        _cameraShakeView = cameraShakeView;
    }

    public void Initialize()
    {
        _runModel.Phase.Where(phase => phase != RunPhase.Battle).Subscribe(_ => OnBattleLeft()).AddTo(_disposables);
        _runModel.RunCount.Skip(1).Subscribe(_ => OnRunStarted()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    public void LateTick()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        var deltaSeconds = _runModel.IsPaused.CurrentValue ? 0f : Time.deltaTime;
        _clock += deltaSeconds;
        if (_combatFeedbackModel.ConsumeHitReaction())
        {
            if (ShakeMath.CanStart(_clock, _lastShakeStart, _rules.MinInterval))
            {
                _lastShakeStart = _clock;
                _shakeRemaining = _rules.Seconds;
                _shakeStep = 0;
                _combatFeedbackModel.AddShake();
            }

            _hapticService.TryPlay(_clock);
        }

        ApplyCamera(deltaSeconds);
    }

    // 흔들리는 중이면 기준 + 오프셋, 끝났으면 기준 그대로(오프셋 0). 시계가 멈춘 프레임은 지난 오프셋을 그대로 둔다
    private void ApplyCamera(float deltaSeconds)
    {
        if (deltaSeconds > 0f)
        {
            // 이 프레임에 남은 시간이 다 하면 오프셋 0(seconds 뒤 0, 끝난 흔들림의 오프셋이 일시정지 동안 남지 않게)
            var isEnding = _shakeRemaining - deltaSeconds <= 0f;
            _offset = isEnding ? Vector2.zero : ShakeMath.GetOffset(_shakeRemaining, _rules.Seconds, _rules.AmplitudePx, _shakeStep);
            if (_shakeRemaining > 0f)
            {
                _shakeRemaining = Mathf.Max(0f, _shakeRemaining - deltaSeconds);
                _shakeStep++;
            }
        }

        _cameraShakeView.SetShakeOffset(_offset);
    }

    private void OnBattleLeft()
    {
        StopShake();
        _combatFeedbackModel.ConsumeHitReaction();
    }

    private void OnRunStarted()
    {
        StopShake();
        _combatFeedbackModel.ConsumeHitReaction();
        _combatFeedbackModel.ResetShakeCount();
        _hapticService.ResetRun();
        _lastShakeStart = float.NegativeInfinity;
    }

    // 오프셋이 얹혀 있으면 카메라를 기준 위치로 되돌린다
    private void StopShake()
    {
        if (IsShaking)
        {
            _cameraShakeView.SetShakeOffset(Vector2.zero);
        }

        _shakeRemaining = 0f;
        _offset = Vector2.zero;
    }
}
