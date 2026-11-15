using System;
using R3;

// 한 판의 진행 상태. 쓰는 쪽은 RunService뿐이다.
public class RunModel : IDisposable
{
    private readonly ReactiveProperty<RunPhase> _phase = new(RunPhase.Lobby);
    private readonly ReactiveProperty<int> _wave = new(0);
    private readonly ReactiveProperty<int> _completedWave = new(0);
    private readonly ReactiveProperty<float> _waveSeconds = new(0f);
    private readonly ReactiveProperty<float> _remainingSeconds = new(0f);
    private readonly ReactiveProperty<bool> _isBurningTime = new(false);
    private readonly ReactiveProperty<int> _kills = new(0);
    private readonly ReactiveProperty<float> _totalBattleSeconds = new(0f);
    private readonly ReactiveProperty<RunOutcome> _outcome = new(RunOutcome.None);
    private readonly ReactiveProperty<int> _wavesSurvived = new(0);
    private readonly ReactiveProperty<bool> _isPaused = new(false);
    private readonly ReactiveProperty<int> _speedIndex = new(0);
    private readonly ReactiveProperty<int> _runCount = new(0);

    public ReadOnlyReactiveProperty<RunPhase> Phase => _phase;
    public ReadOnlyReactiveProperty<int> Wave => _wave;
    public ReadOnlyReactiveProperty<int> CompletedWave => _completedWave;
    public ReadOnlyReactiveProperty<float> WaveSeconds => _waveSeconds;
    public ReadOnlyReactiveProperty<float> RemainingSeconds => _remainingSeconds;
    public ReadOnlyReactiveProperty<bool> IsBurningTime => _isBurningTime;
    public ReadOnlyReactiveProperty<int> Kills => _kills;
    public ReadOnlyReactiveProperty<float> TotalBattleSeconds => _totalBattleSeconds;
    public ReadOnlyReactiveProperty<RunOutcome> Outcome => _outcome;
    public ReadOnlyReactiveProperty<int> WavesSurvived => _wavesSurvived;
    public ReadOnlyReactiveProperty<bool> IsPaused => _isPaused;
    public ReadOnlyReactiveProperty<int> SpeedIndex => _speedIndex;
    public ReadOnlyReactiveProperty<int> RunCount => _runCount;

    public float ElapsedSeconds => _waveSeconds.Value - _remainingSeconds.Value;

    public void SetPhase(RunPhase phase) => _phase.Value = phase;

    public void SetWave(int wave) => _wave.Value = wave;

    public void SetCompletedWave(int wave) => _completedWave.Value = wave;

    public void SetWaveSeconds(float seconds) => _waveSeconds.Value = seconds;

    public void SetRemainingSeconds(float seconds) => _remainingSeconds.Value = seconds;

    public void SetBurningTime(bool isBurningTime) => _isBurningTime.Value = isBurningTime;

    public void SetKills(int kills) => _kills.Value = kills;

    public void SetTotalBattleSeconds(float seconds) => _totalBattleSeconds.Value = seconds;

    public void SetOutcome(RunOutcome outcome) => _outcome.Value = outcome;

    public void SetWavesSurvived(int waves) => _wavesSurvived.Value = waves;

    public void SetPaused(bool isPaused) => _isPaused.Value = isPaused;

    public void SetSpeedIndex(int index) => _speedIndex.Value = index;

    public void SetRunCount(int count) => _runCount.Value = count;

    public void Dispose()
    {
        _phase.Dispose();
        _wave.Dispose();
        _completedWave.Dispose();
        _waveSeconds.Dispose();
        _remainingSeconds.Dispose();
        _isBurningTime.Dispose();
        _kills.Dispose();
        _totalBattleSeconds.Dispose();
        _outcome.Dispose();
        _wavesSurvived.Dispose();
        _isPaused.Dispose();
        _speedIndex.Dispose();
        _runCount.Dispose();
    }
}
