using System;
using UnityEngine;

// 한 판 규칙(S4, 기획서 B·C절). RunModel을 쓰는 유일한 곳.
public class RunService
{
    // 걸음 단위로 모은 시간이 1에 이만큼 모자라도 한 걸음으로 친다(float 프레임 시간을 나눠 더한 오차, 0.0001걸음 ≈ 3.3µs)
    private const double StepTolerance = 1e-4;

    private readonly RunModel _runModel;
    private readonly WaveRepository _waveRepository;
    private readonly RunRulesData _runRules;
    private readonly PartyService _partyService;
    private readonly BasicWeaponService _basicWeaponService;
    private readonly MonsterService _monsterService;

    // 프레임 시간 × 배속을 걸음 단위(double)로 모은다. 프레임마다 반올림하면 같은 시간을 다르게 나눴을 때 걸음 수가 갈린다(AC11)
    private double _accumulatedSteps;
    private float _finishedWaveSeconds;
    private float _enemyCountScale;

    private WaveData CurrentWave => _waveRepository.WaveDataList[_runModel.Wave.CurrentValue - 1];

    public RunService(RunModel runModel, WaveRepository waveRepository, RunRulesData runRules, PartyService partyService,
        BasicWeaponService basicWeaponService, MonsterService monsterService)
    {
        _runModel = runModel;
        _waveRepository = waveRepository;
        _runRules = runRules;
        _partyService = partyService;
        _basicWeaponService = basicWeaponService;
        _monsterService = monsterService;
    }

    // seed 0 = 시작 시각에서 뽑는다(A11)
    public void StartRun(int seed, float enemyCountScale)
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Lobby)
        {
            GameLogger.LogWarning($"[RunService] StartRun in {_runModel.Phase.CurrentValue}");
            return;
        }

        var runSeed = seed != 0 ? seed : Environment.TickCount;
        _enemyCountScale = enemyCountScale;
        _finishedWaveSeconds = 0f;
        _partyService.CreateParty();
        _monsterService.BeginRun(runSeed);
        _runModel.SetRunCount(_runModel.RunCount.CurrentValue + 1);
        _runModel.SetKills(0);
        _runModel.SetTotalBattleSeconds(0f);
        _runModel.SetOutcome(RunOutcome.None);
        _runModel.SetWavesSurvived(0);
        _runModel.SetCompletedWave(0);
        _runModel.SetPaused(false);
        _runModel.SetSpeedIndex(0);
        GameLogger.Log($"[RunService] Run {_runModel.RunCount.CurrentValue} seed {runSeed} enemyCountScale {enemyCountScale}");
        BeginWave(1);
    }

    // 프레임 시간 × 배속을 모아 simStep씩 Step. 일시정지 중이거나 전투가 아니면 아무것도 하지 않는다
    public void AdvanceFrame(float frameSeconds)
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle || _runModel.IsPaused.CurrentValue)
        {
            return;
        }

        var speed = _runRules.SpeedOptions[_runModel.SpeedIndex.CurrentValue];
        _accumulatedSteps += (double)frameSeconds * speed / _runRules.SimStep;
        while (_accumulatedSteps >= 1d - StepTolerance)
        {
            _accumulatedSteps -= 1d;
            Step(_runRules.SimStep);
            if (_runModel.Phase.CurrentValue != RunPhase.Battle)
            {
                // 웨이브·판이 끝나면 남은 시간을 버린다(정비 중에 걸음이 돌거나 다음 웨이브로 넘어가지 않게)
                _accumulatedSteps = 0d;
                return;
            }
        }
    }

    // 한 걸음(기획서 C절 ①~⑦)
    public void Step(float stepSeconds)
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        // ① 걸음 시간을 남은 웨이브 시간으로 자름
        var remaining = _runModel.RemainingSeconds.CurrentValue;
        var deltaSeconds = Mathf.Min(stepSeconds, remaining);

        // ② 경과·전투 시간
        remaining -= deltaSeconds;
        _runModel.SetRemainingSeconds(remaining);
        var waveSeconds = _runModel.WaveSeconds.CurrentValue;
        var elapsed = waveSeconds - remaining;
        _runModel.SetTotalBattleSeconds(_finishedWaveSeconds + elapsed);
        var burningStart = waveSeconds * _runRules.BurningStartRatio;
        var isBurningTime = CurrentWave.HasBurningTime && elapsed >= burningStart
            && elapsed < burningStart + _runRules.BurningSeconds;
        _runModel.SetBurningTime(isBurningTime);

        // ③ 적 생성(버닝 타임 포함)
        _monsterService.TickSpawn(deltaSeconds, elapsed, isBurningTime, burningStart);

        // ④ 기본 무기
        _basicWeaponService.Tick(deltaSeconds);
        _runModel.SetKills(_runModel.Kills.CurrentValue + _monsterService.RemoveDead());

        // ⑤ 적 이동·접촉 공격
        _monsterService.TickMove(deltaSeconds);

        // ⑥ 생존 0이면 즉시 패배(시간 끝과 겹쳐도 패배가 먼저)
        if (_partyService.AliveCount == 0)
        {
            EndRun(RunOutcome.Lose);
            return;
        }

        // ⑦ 남은 시간 0이면 웨이브 끝
        if (remaining <= 0f)
        {
            FinishWave();
        }
    }

    public void NextWave()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Prep)
        {
            GameLogger.LogWarning($"[RunService] NextWave in {_runModel.Phase.CurrentValue}");
            return;
        }

        BeginWave(_runModel.Wave.CurrentValue + 1);
    }

    // 적·한 판 상태를 비우고 로비로
    public void ReturnToLobby()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Result)
        {
            GameLogger.LogWarning($"[RunService] ReturnToLobby in {_runModel.Phase.CurrentValue}");
            return;
        }

        _accumulatedSteps = 0d;
        _monsterService.ClearMonsters();
        _partyService.ClearParty();
        _runModel.SetWave(0);
        _runModel.SetCompletedWave(0);
        _runModel.SetWaveSeconds(0f);
        _runModel.SetRemainingSeconds(0f);
        _runModel.SetBurningTime(false);
        _runModel.SetKills(0);
        _runModel.SetTotalBattleSeconds(0f);
        _runModel.SetOutcome(RunOutcome.None);
        _runModel.SetWavesSurvived(0);
        _runModel.SetPaused(false);
        _runModel.SetSpeedIndex(0);
        _runModel.SetPhase(RunPhase.Lobby);
    }

    public void TogglePause()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        _runModel.SetPaused(!_runModel.IsPaused.CurrentValue);
    }

    public void ToggleSpeed()
    {
        var next = (_runModel.SpeedIndex.CurrentValue + 1) % _runRules.SpeedOptions.Count;
        _runModel.SetSpeedIndex(next);
    }

    private void BeginWave(int wave)
    {
        var waveCount = _waveRepository.WaveDataList.Count;
        if (wave < 1 || wave > waveCount)
        {
            GameLogger.LogError($"[RunService] Wave {wave} out of 1..{waveCount}");
            return;
        }

        var waveData = _waveRepository.WaveDataList[wave - 1];
        _accumulatedSteps = 0d;
        _runModel.SetWave(wave);
        _runModel.SetWaveSeconds(waveData.Duration);
        _runModel.SetRemainingSeconds(waveData.Duration);
        _runModel.SetBurningTime(false);
        _basicWeaponService.ResetClocks();
        _monsterService.BeginWave(waveData.Duration, _enemyCountScale);
        _runModel.SetPhase(RunPhase.Battle);
    }

    // 시간이 끝났고 생존 ≥ 1: 마지막 웨이브면 승리, 아니면 정비
    private void FinishWave()
    {
        _finishedWaveSeconds += _runModel.WaveSeconds.CurrentValue;
        var wave = _runModel.Wave.CurrentValue;
        if (wave >= _waveRepository.WaveDataList.Count)
        {
            EndRun(RunOutcome.Win);
            return;
        }

        _monsterService.ClearMonsters();
        _runModel.SetBurningTime(false);
        _runModel.SetCompletedWave(wave);
        _runModel.SetPhase(RunPhase.Prep);
    }

    // 버틴 웨이브: 승리는 마지막 웨이브 번호, 패배는 진 웨이브 − 1
    private void EndRun(RunOutcome outcome)
    {
        var wave = _runModel.Wave.CurrentValue;
        _monsterService.ClearMonsters();
        _runModel.SetBurningTime(false);
        _runModel.SetWavesSurvived(outcome == RunOutcome.Win ? wave : wave - 1);
        _runModel.SetOutcome(outcome);
        _runModel.SetPhase(RunPhase.Result);
    }
}
