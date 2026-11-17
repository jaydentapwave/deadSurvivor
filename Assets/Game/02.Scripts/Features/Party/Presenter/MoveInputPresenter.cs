using System;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

// 이동 입력(V2, F2 기획서 D절). 전투 중 매 프레임 조이스틱(눌려 있으면)이나 WASD를 읽어 지난 값과 다를 때만 이동 명령을 보낸다
// (사람이 만지지 않으면 판정 봇의 명령을 덮지 않는다). 전투가 아니게 되면 조이스틱을 숨기고 0을 보낸다.
public class MoveInputPresenter : IInitializable, ITickable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly PartyMoveService _partyMoveService;
    private readonly MoveRulesData _moveRules;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly UI_JoystickView _joystickView;
    private readonly CompositeDisposable _disposables = new();

    private bool _hasSentInput;
    private Vector2 _sentInput;

    public MoveInputPresenter(RunModel runModel, PartyMoveService partyMoveService, MoveRulesData moveRules,
        SpriteAtlasService spriteAtlasService, UI_JoystickView joystickView)
    {
        _runModel = runModel;
        _partyMoveService = partyMoveService;
        _moveRules = moveRules;
        _spriteAtlasService = spriteAtlasService;
        _joystickView = joystickView;
    }

    public void Initialize()
    {
        _joystickView.Initialize(_spriteAtlasService.Get("joystick_base"), _spriteAtlasService.Get("joystick_knob"));
        _runModel.Phase.Subscribe(OnPhaseChanged).AddTo(_disposables);
    }

    public void Tick()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        var isJoystickPressed = _joystickView.IsPressed;
        var input = isJoystickPressed
            ? MoveInputMath.JoystickInput(_joystickView.PressPosition, _joystickView.CurrentPosition,
                _moveRules.JoystickRadius * _joystickView.CanvasScale)
            : ReadKeyboardInput();
        if (!_hasSentInput || input != _sentInput)
        {
            SendInput(input);
        }

        _joystickView.UpdateUI(isJoystickPressed, _joystickView.PressPosition, input);
    }

    public void Dispose() => _disposables.Dispose();

    // 전투가 시작되면(웨이브마다) 지난 값을 지워 눌린 키가 다시 들어가게 하고, 전투가 아니면 조이스틱을 숨기고 0을 보낸다
    private void OnPhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.Battle)
        {
            _hasSentInput = false;
            return;
        }

        // 판정 봇 등 다른 쪽이 넣은 명령이 남아 있을 수 있어 지난 값과 상관없이 0을 보낸다
        _joystickView.UpdateUI(false, Vector2.zero, Vector2.zero);
        SendInput(Vector2.zero);
    }

    private void SendInput(Vector2 input)
    {
        _partyMoveService.SetMoveInput(input);
        _sentInput = input;
        _hasSentInput = true;
    }

    // 키보드가 없는 기기(모바일)면 0
    private static Vector2 ReadKeyboardInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        return MoveInputMath.KeyboardInput(keyboard.wKey.isPressed, keyboard.aKey.isPressed, keyboard.sKey.isPressed,
            keyboard.dKey.isPressed);
    }
}
