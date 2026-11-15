using System.Collections.Generic;
using NUnit.Framework;
using R3;

// AC9·AC10·AC11 — 한 판 규칙(기획서 C절)
public class RunServiceTests
{
    private static readonly float[] ExpectedWaveSeconds = { 30, 30, 35, 40, 40, 45, 45, 50, 55, 65 };

    [Test]
    public void WaveScheduleAndWin()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);

        var waveCount = context.WaveRepository.WaveDataList.Count;
        Assert.AreEqual(ExpectedWaveSeconds.Length, waveCount);
        for (var wave = 1; wave <= waveCount; wave++)
        {
            Assert.AreEqual(RunPhase.Battle, context.RunModel.Phase.CurrentValue);
            Assert.AreEqual(wave, context.RunModel.Wave.CurrentValue);
            Assert.AreEqual(ExpectedWaveSeconds[wave - 1], context.RunModel.WaveSeconds.CurrentValue);
            var steps = context.StepUntilBattleEnds();
            // 1/30초 걸음으로 웨이브 시간만큼 돈다(마지막 걸음은 남은 시간으로 잘림)
            Assert.AreEqual(ExpectedWaveSeconds[wave - 1] * 30f, steps, 1f);
            Assert.AreEqual(0f, context.RunModel.RemainingSeconds.CurrentValue);
            if (wave < waveCount)
            {
                Assert.AreEqual(RunPhase.Prep, context.RunModel.Phase.CurrentValue);
                Assert.AreEqual(wave, context.RunModel.CompletedWave.CurrentValue);
                context.RunService.NextWave();
            }
        }

        Assert.AreEqual(RunPhase.Result, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(RunOutcome.Win, context.RunModel.Outcome.CurrentValue);
        Assert.AreEqual(waveCount, context.RunModel.WavesSurvived.CurrentValue);
        Assert.AreEqual(435f, context.RunModel.TotalBattleSeconds.CurrentValue, 0.01f);
        Assert.AreEqual(4, context.PartyService.AliveCount);
    }

    [Test]
    public void TimeEndAndWipeInSameStepIsLose()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        while (context.RunModel.RemainingSeconds.CurrentValue > context.RunRules.SimStep)
        {
            context.RunService.Step(context.RunRules.SimStep);
        }

        context.PlaceKillingContact(1f);
        context.RunService.Step(context.RunRules.SimStep);

        Assert.AreEqual(0f, context.RunModel.RemainingSeconds.CurrentValue);
        Assert.AreEqual(0, context.PartyService.AliveCount);
        Assert.AreEqual(RunPhase.Result, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(RunOutcome.Lose, context.RunModel.Outcome.CurrentValue);
        Assert.AreEqual(0, context.RunModel.WavesSurvived.CurrentValue);
    }

    [Test]
    public void WipeMidWaveIsImmediateLose()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        context.StepSeconds(5f);

        context.PlaceKillingContact(1f);
        context.RunService.Step(context.RunRules.SimStep);

        Assert.Greater(context.RunModel.RemainingSeconds.CurrentValue, 20f);
        Assert.AreEqual(RunPhase.Result, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(RunOutcome.Lose, context.RunModel.Outcome.CurrentValue);
    }

    [Test]
    public void WavesSurvivedOnLoseIsLostWaveMinusOne()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        for (var wave = 1; wave < 3; wave++)
        {
            context.StepUntilBattleEnds();
            context.RunService.NextWave();
        }

        Assert.AreEqual(3, context.RunModel.Wave.CurrentValue);
        context.StepSeconds(3f);
        context.PlaceKillingContact(1f);
        context.RunService.Step(context.RunRules.SimStep);

        Assert.AreEqual(RunOutcome.Lose, context.RunModel.Outcome.CurrentValue);
        Assert.AreEqual(2, context.RunModel.WavesSurvived.CurrentValue);
    }

    [Test]
    public void BurningTimeWindow()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        var waveCount = context.WaveRepository.WaveDataList.Count;
        for (var wave = 1; wave <= waveCount; wave++)
        {
            var waveSeconds = context.RunModel.WaveSeconds.CurrentValue;
            var start = waveSeconds * 0.5f;
            var burningSteps = new List<float>();
            while (context.RunModel.Phase.CurrentValue == RunPhase.Battle)
            {
                context.RunService.Step(context.RunRules.SimStep);
                if (context.RunModel.IsBurningTime.CurrentValue)
                {
                    burningSteps.Add(context.RunModel.ElapsedSeconds);
                }
            }

            if (wave < 6)
            {
                Assert.IsEmpty(burningSteps, $"wave {wave}");
            }
            else
            {
                Assert.IsNotEmpty(burningSteps, $"wave {wave}");
                Assert.GreaterOrEqual(burningSteps[0], start, $"wave {wave} start");
                Assert.AreEqual(start, burningSteps[0], 0.04f, $"wave {wave} start");
                Assert.Less(burningSteps[burningSteps.Count - 1], start + 10f, $"wave {wave} end");
                Assert.AreEqual(start + 10f, burningSteps[burningSteps.Count - 1], 0.04f, $"wave {wave} end");
            }

            if (wave < waveCount)
            {
                context.RunService.NextWave();
            }
        }
    }

    [Test]
    public void PauseStopsTime()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        context.RunService.AdvanceFrame(1f);
        var before = context.RunModel.RemainingSeconds.CurrentValue;

        context.RunService.TogglePause();
        for (var i = 0; i < 120; i++)
        {
            context.RunService.AdvanceFrame(1f / 60f);
        }

        Assert.IsTrue(context.RunModel.IsPaused.CurrentValue);
        Assert.AreEqual(before, context.RunModel.RemainingSeconds.CurrentValue);

        context.RunService.TogglePause();
        context.RunService.AdvanceFrame(1f);
        Assert.AreEqual(before - 1f, context.RunModel.RemainingSeconds.CurrentValue, 0.001f);
    }

    // 알고리즘 검증 지적: 144fps(1/144초 × 144)와 1초 1번이 같은 걸음 수
    [TestCase(144)]
    [TestCase(60)]
    [TestCase(30)]
    [TestCase(3)]
    public void FrameRateDoesNotChangeStepCount(int framesPerSecond)
    {
        var split = new TestRunContext();
        var whole = new TestRunContext();
        split.RunService.StartRun(1, 0f);
        whole.RunService.StartRun(1, 0f);
        for (var i = 0; i < framesPerSecond; i++)
        {
            split.RunService.AdvanceFrame(1f / framesPerSecond);
        }

        whole.RunService.AdvanceFrame(1f);
        Assert.AreEqual(whole.RunModel.RemainingSeconds.CurrentValue, split.RunModel.RemainingSeconds.CurrentValue);
        Assert.AreEqual(29f, whole.RunModel.RemainingSeconds.CurrentValue, 0.001f);
    }

    // 설계 검토 지적: 65초 웨이브 안에서 1초 × 64와 1/60초 × 3840이 같은 걸음 수(1920)
    [Test]
    public void LongWaveSameStepCountForDifferentFrameSplits()
    {
        var bySecond = new TestRunContext();
        var bySixtieth = new TestRunContext();
        foreach (var context in new[] { bySecond, bySixtieth })
        {
            context.RunService.StartRun(1, 0f);
            while (context.RunModel.Wave.CurrentValue < context.WaveRepository.WaveDataList.Count)
            {
                context.StepUntilBattleEnds();
                context.RunService.NextWave();
            }
        }

        Assert.AreEqual(65f, bySecond.RunModel.WaveSeconds.CurrentValue);
        for (var second = 0; second < 64; second++)
        {
            bySecond.RunService.AdvanceFrame(1f);
            for (var frame = 0; frame < 60; frame++)
            {
                bySixtieth.RunService.AdvanceFrame(1f / 60f);
            }
        }

        Assert.AreEqual(bySecond.RunModel.RemainingSeconds.CurrentValue, bySixtieth.RunModel.RemainingSeconds.CurrentValue);
        Assert.AreEqual(1f, bySecond.RunModel.RemainingSeconds.CurrentValue, 0.01f);
    }

    [Test]
    public void SpeedDoubles()
    {
        var normal = new TestRunContext();
        var fast = new TestRunContext();
        normal.RunService.StartRun(1, 0f);
        fast.RunService.StartRun(1, 0f);
        fast.RunService.ToggleSpeed();

        for (var i = 0; i < 60; i++)
        {
            normal.RunService.AdvanceFrame(1f / 60f);
            fast.RunService.AdvanceFrame(1f / 60f);
        }

        var normalSpent = normal.RunModel.WaveSeconds.CurrentValue - normal.RunModel.RemainingSeconds.CurrentValue;
        var fastSpent = fast.RunModel.WaveSeconds.CurrentValue - fast.RunModel.RemainingSeconds.CurrentValue;
        Assert.AreEqual(1, fast.RunModel.SpeedIndex.CurrentValue);
        Assert.AreEqual(1f, normalSpent, 0.001f);
        Assert.AreEqual(2f, fastSpent, 0.001f);
    }

    // 설계 검토 M2: Phase를 구독한 쪽이 바뀌는 순간 결과·완료 웨이브 값이 이미 최종값이다
    [Test]
    public void PhaseSubscribersSeeFinalValues()
    {
        var context = new TestRunContext();
        var seenCompletedWave = -1;
        var seenOutcome = RunOutcome.None;
        var seenWavesSurvived = -1;
        var seenTotalSeconds = -1f;
        using var subscription = context.RunModel.Phase.Subscribe(phase =>
        {
            if (phase == RunPhase.Prep)
            {
                seenCompletedWave = context.RunModel.CompletedWave.CurrentValue;
            }
            else if (phase == RunPhase.Result)
            {
                seenOutcome = context.RunModel.Outcome.CurrentValue;
                seenWavesSurvived = context.RunModel.WavesSurvived.CurrentValue;
                seenTotalSeconds = context.RunModel.TotalBattleSeconds.CurrentValue;
            }
        });

        context.RunService.StartRun(1, 0f);
        context.StepUntilBattleEnds();
        Assert.AreEqual(1, seenCompletedWave);

        context.RunService.NextWave();
        context.StepSeconds(2f);
        context.PlaceKillingContact(1f);
        context.RunService.Step(context.RunRules.SimStep);

        Assert.AreEqual(RunOutcome.Lose, seenOutcome);
        Assert.AreEqual(1, seenWavesSurvived);
        Assert.AreEqual(context.RunModel.TotalBattleSeconds.CurrentValue, seenTotalSeconds);
    }

    // 설계 검토 M4: 한 프레임에 웨이브 끝을 넘는 시간이 와도 정비 중에 걸음이 돌지 않고 다음 웨이브로 넘어가지 않는다
    [Test]
    public void LeftoverFrameTimeIsDroppedAtWaveEnd()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);

        // 30초 웨이브(실수 오차로 900~901걸음)를 넘어 수십 걸음 몫이 남는 프레임. 남은 몫을 버리지 않으면 아래 0.01초 프레임에서 걸음이 돈다
        context.RunService.AdvanceFrame(30.5f);
        Assert.AreEqual(RunPhase.Prep, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(0f, context.RunModel.RemainingSeconds.CurrentValue);

        context.RunService.AdvanceFrame(5f);
        Assert.AreEqual(RunPhase.Prep, context.RunModel.Phase.CurrentValue);

        context.RunService.NextWave();
        Assert.AreEqual(context.RunModel.WaveSeconds.CurrentValue, context.RunModel.RemainingSeconds.CurrentValue);
        context.RunService.AdvanceFrame(0.01f);
        Assert.AreEqual(context.RunModel.WaveSeconds.CurrentValue, context.RunModel.RemainingSeconds.CurrentValue);
    }

    // 설계 검토 M4: 여러 웨이브에 걸쳐서도 프레임 나누기와 상관없이 같은 결과(시간 배율 20의 큰 프레임 포함)
    [Test]
    public void SameResultAcrossWavesForDifferentFrameSplits()
    {
        var smallFrames = new TestRunContext();
        var largeFrames = new TestRunContext();
        smallFrames.RunService.StartRun(11, 1f);
        largeFrames.RunService.StartRun(11, 1f);

        RunWaves(smallFrames, 1f / 60f, 3);
        // 시간 배율 20, 프레임 1/60초 → 한 프레임 1/3초
        RunWaves(largeFrames, 20f / 60f, 3);

        Assert.AreEqual(largeFrames.RunModel.Phase.CurrentValue, smallFrames.RunModel.Phase.CurrentValue);
        Assert.AreEqual(largeFrames.RunModel.Wave.CurrentValue, smallFrames.RunModel.Wave.CurrentValue);
        Assert.AreEqual(largeFrames.RunModel.Kills.CurrentValue, smallFrames.RunModel.Kills.CurrentValue);
        Assert.Greater(smallFrames.RunModel.Kills.CurrentValue, 0);
        for (var i = 0; i < smallFrames.PartyModel.MemberList.Count; i++)
        {
            Assert.AreEqual(largeFrames.PartyModel.MemberList[i].Hp, smallFrames.PartyModel.MemberList[i].Hp, $"member {i}");
        }
    }

    // 정비가 나오면 바로 다음 웨이브를 누르며 waves개 웨이브를 끝낼 때까지 같은 크기의 프레임을 넣는다
    private static void RunWaves(TestRunContext context, float frameSeconds, int waves)
    {
        var guard = 0;
        while (context.RunModel.CompletedWave.CurrentValue < waves && context.RunModel.Phase.CurrentValue != RunPhase.Result
               && guard++ < 1000000)
        {
            if (context.RunModel.Phase.CurrentValue == RunPhase.Prep)
            {
                context.RunService.NextWave();
            }

            context.RunService.AdvanceFrame(frameSeconds);
        }
    }

    [Test]
    public void ReturnToLobbyThenNewRunStartsFresh()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 1f);
        context.StepSeconds(15f);
        context.PlaceKillingContact(1f);
        context.RunService.Step(context.RunRules.SimStep);
        Assert.AreEqual(RunPhase.Result, context.RunModel.Phase.CurrentValue);

        context.RunService.ReturnToLobby();
        Assert.AreEqual(RunPhase.Lobby, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(0, context.MonsterModel.MonsterList.Count);
        Assert.AreEqual(0, context.PartyModel.MemberList.Count);

        context.RunService.StartRun(2, 1f);
        Assert.AreEqual(RunPhase.Battle, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(1, context.RunModel.Wave.CurrentValue);
        Assert.AreEqual(0, context.RunModel.Kills.CurrentValue);
        Assert.AreEqual(4, context.PartyService.AliveCount);
        Assert.AreEqual(2, context.RunModel.RunCount.CurrentValue);
    }
}
