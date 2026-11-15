using System;
using R3;
using VContainer.Unity;

// 결과 화면(V4): 결과로 바뀌면 승패·버틴 웨이브·처치·전투 시간을 채우고, 로비로 버튼으로 로비에 돌아간다
public class ResultPresenter : IInitializable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly RunService _runService;
    private readonly WaveRepository _waveRepository;
    private readonly UI_ResultView _resultView;
    private readonly CompositeDisposable _disposables = new();

    public ResultPresenter(RunModel runModel, RunService runService, WaveRepository waveRepository, UI_ResultView resultView)
    {
        _runModel = runModel;
        _runService = runService;
        _waveRepository = waveRepository;
        _resultView = resultView;
    }

    public void Initialize()
    {
        _runModel.Phase.Where(phase => phase == RunPhase.Result).Subscribe(_ => OnResultEntered()).AddTo(_disposables);
        _resultView.ToLobbyButton.OnClickAsObservable().Subscribe(_ => OnToLobbyClicked()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    public void OnToLobbyClicked() => _runService.ReturnToLobby();

    private void OnResultEntered()
    {
        _resultView.UpdateUI(_runModel.Outcome.CurrentValue, _runModel.WavesSurvived.CurrentValue,
            _waveRepository.WaveDataList.Count, _runModel.Kills.CurrentValue, _runModel.TotalBattleSeconds.CurrentValue);
    }
}
