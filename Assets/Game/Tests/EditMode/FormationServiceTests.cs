using NUnit.Framework;

// F2 AC19 — 진형 고르기와 적용
public class FormationServiceTests
{
    [Test]
    public void RunStartsWithOrderOneFormation()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);

        var first = context.FormationRepository.FormationDataList[0];
        Assert.AreEqual(1, first.Order);
        Assert.AreEqual("balanced", first.Id);
        Assert.AreEqual(first.Id, context.FormationModel.CurrentId.CurrentValue);
        Assert.AreEqual(first.Id, context.FormationModel.PendingId.CurrentValue);
    }

    [Test]
    public void SelectOutsidePrepIsIgnored()
    {
        var context = new TestRunContext();
        context.FormationService.SelectPending("wedge");
        context.RunService.StartRun(1, 0f);
        Assert.AreEqual(RunPhase.Battle, context.RunModel.Phase.CurrentValue);

        context.FormationService.SelectPending("wedge");

        Assert.AreEqual("balanced", context.FormationModel.PendingId.CurrentValue);
        Assert.AreEqual("balanced", context.FormationModel.CurrentId.CurrentValue);
    }

    [Test]
    public void UnknownIdIsIgnored()
    {
        var context = StartAndReachPrep();

        context.FormationService.SelectPending("no_such_formation");

        Assert.AreEqual("balanced", context.FormationModel.PendingId.CurrentValue);
    }

    // 정비에서는 대기 진형만 바뀌고, 다음 웨이브 시작에 지금 진형이 바뀌며, 다시 고르지 않으면 유지된다
    [Test]
    public void PendingAppliesAtNextWaveAndPersists()
    {
        var context = StartAndReachPrep();

        context.FormationService.SelectPending("wedge");
        Assert.AreEqual("wedge", context.FormationModel.PendingId.CurrentValue);
        Assert.AreEqual("balanced", context.FormationModel.CurrentId.CurrentValue);

        context.RunService.NextWave();
        Assert.AreEqual("wedge", context.FormationModel.CurrentId.CurrentValue);
        context.FormationRepository.TryGet("wedge", out var wedge);
        for (var i = 0; i < wedge.Slots.Count; i++)
        {
            Assert.AreEqual(wedge.Slots[i], context.FormationService.GetSlot(i), $"slot {i}");
        }

        context.StepUntilBattleEnds();
        Assert.AreEqual(RunPhase.Prep, context.RunModel.Phase.CurrentValue);
        context.RunService.NextWave();
        Assert.AreEqual("wedge", context.FormationModel.CurrentId.CurrentValue);
    }

    [Test]
    public void NewRunStartsWithOrderOneFormation()
    {
        var context = StartAndReachPrep();
        context.FormationService.SelectPending("line");
        context.RunService.NextWave();
        context.PlaceKillingContact(1f);
        context.RunService.Step(context.RunRules.SimStep);
        Assert.AreEqual(RunPhase.Result, context.RunModel.Phase.CurrentValue);
        context.RunService.ReturnToLobby();

        context.RunService.StartRun(2, 0f);

        Assert.AreEqual("balanced", context.FormationModel.CurrentId.CurrentValue);
        Assert.AreEqual("balanced", context.FormationModel.PendingId.CurrentValue);
    }

    private static TestRunContext StartAndReachPrep()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        context.StepUntilBattleEnds();
        Assert.AreEqual(RunPhase.Prep, context.RunModel.Phase.CurrentValue);
        return context;
    }
}
