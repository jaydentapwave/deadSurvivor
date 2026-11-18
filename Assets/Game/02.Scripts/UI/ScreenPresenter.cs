using System;
using R3;
using VContainer.Unity;

// RunModel.Phase(= 지금 화면)로 화면 판넬 네 개를 켜고 끈다(기획서 B절)
public class ScreenPresenter : IInitializable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly UI_LobbyView _lobbyView;
    private readonly UI_BattleHudView _battleHudView;
    private readonly UI_PrepView _prepView;
    private readonly UI_ResultView _resultView;
    private readonly CompositeDisposable _disposables = new();

    public ScreenPresenter(RunModel runModel, UI_LobbyView lobbyView, UI_BattleHudView battleHudView, UI_PrepView prepView,
        UI_ResultView resultView)
    {
        _runModel = runModel;
        _lobbyView = lobbyView;
        _battleHudView = battleHudView;
        _prepView = prepView;
        _resultView = resultView;
    }

    public void Initialize()
    {
        _runModel.Phase.Subscribe(OnPhaseChanged).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    private void OnPhaseChanged(RunPhase phase)
    {
        _lobbyView.gameObject.SetActive(phase == RunPhase.Lobby);
        _battleHudView.gameObject.SetActive(phase == RunPhase.Battle);
        _prepView.gameObject.SetActive(phase == RunPhase.Prep);
        _resultView.gameObject.SetActive(phase == RunPhase.Result);
    }
}
