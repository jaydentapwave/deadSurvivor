using System;
using R3;
using UnityEngine;
using VContainer.Unity;

// 원정대 피격 → 카메라 정수 픽셀 흔들림·진동(F6 기획서 D절). 피격 깃발을 프레임마다 한 번 읽고, 흔들림과 진동은 각자의 간격으로 따로 판정한다.
// 카메라 기준 위치는 다른 쪽(WorldPresenter)이 이번 프레임에 옮겼으면 그 위치, 아니면 지난 기준(ShakeMath.ResolveBase) — 오프셋이 쌓이지 않는다.
public class HitReactionPresenter : IInitializable, ILateTickable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly CombatFeedbackModel _combatFeedbackModel;
    private readonly ShakeRulesData _rules;
    private readonly HapticService _hapticService;
    private readonly CameraShakeView _cameraShakeView;
    private readonly CompositeDisposable _disposables = new();

    // 피드백 시계(전투 중·일시정지 아님일 때만 흐름)
    private float _clock;
    private float _lastShakeStart = float.NegativeInfinity;
    private float _shakeRemaining;
    private int _shakeStep;
    private Vector3 _basePosition;
    private Vector3 _lastAppliedPosition;
    // 마지막으로 얹은 오프셋. 일시정지(시계 0) 중에는 이것을 그대로 다시 얹는다
    private Vector2 _offset;

    // 남은 시간이 있거나 카메라에 아직 오프셋이 얹혀 있다(끝나는 프레임은 마지막 오프셋이 남고 다음 프레임에 0이 된다)
    public bool IsShaking => _shakeRemaining > 0f || _offset != Vector2.zero;

    public HitReactionPresenter(RunModel runModel, CombatFeedbackModel combatFeedbackModel,
        FeedbackRulesRepository feedbackRulesRepository, HapticService hapticService, CameraShakeView cameraShakeView)
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
        _basePosition = ShakeMath.ResolveBase(_cameraShakeView.Position, _lastAppliedPosition, _basePosition);
        if (deltaSeconds > 0f)
        {
            _offset = ShakeMath.GetOffset(_shakeRemaining, _rules.Seconds, _rules.AmplitudePx, _shakeStep);
            if (_shakeRemaining > 0f)
            {
                _shakeRemaining = Mathf.Max(0f, _shakeRemaining - deltaSeconds);
                _shakeStep++;
            }
        }

        _lastAppliedPosition = _basePosition + (Vector3)_offset;
        _cameraShakeView.SetPosition(_lastAppliedPosition);
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
            _cameraShakeView.SetPosition(_basePosition);
            _lastAppliedPosition = _basePosition;
        }

        _shakeRemaining = 0f;
        _offset = Vector2.zero;
    }
}
