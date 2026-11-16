using System.Linq;
using NUnit.Framework;

// AC16 — 자동 장착 계획(기획서 E절)
public class AutoEquipPlannerTests
{
    private const int Bruno = 0;
    private const int Lena = 1;
    private const int Mira = 2;

    [Test]
    public void MergesStashSwordIntoBrunoSwordFirst()
    {
        var context = Setup();
        var stashSword = BagTestItems.AddToStash(context, "b_sword");
        var brunoSword = BagTestItems.BagItemAt(context, Bruno, 0).Uid;

        var stepList = CreatePlanner(context).Plan();

        Assert.AreEqual(BagPlanStepKind.Merge, stepList[0].Kind);
        Assert.AreEqual((stashSword, brunoSword), (stepList[0].Uid, stepList[0].TargetUid));
        Execute(context, stepList);
        Assert.AreEqual(1, context.BagModel.FindItem(brunoSword, out var owner).Grade);
        Assert.AreEqual(Bruno, owner);
    }

    [Test]
    public void DownMemberIsRevivedFirst()
    {
        var context = Setup();
        context.PartyModel.MemberList[Mira].Hp = 0f;

        CollectionAssert.AreEqual(new[] { Mira }, CreatePlanner(context).GetReviveMemberList());
    }

    [Test]
    public void DownMemberBagIsNotPlanned()
    {
        var context = Setup();
        BagTestItems.AddToStash(context, "b_whet");
        context.PartyModel.MemberList[Lena].Hp = 0f;

        var stepList = CreatePlanner(context).Plan();

        Assert.IsTrue(stepList.All(step => step.Kind != BagPlanStepKind.Drop || step.Member != Lena));
    }

    [Test]
    public void WhetstoneGoesToHighestScoreFirstPlace()
    {
        // 단궁 15.6/0.7 > 장검 17.6/1 > 번개 완드 17/1.4: 숫돌은 레나 단궁 옆, 회전 0, 가장 작은 y·x인 (3, 0)
        var context = Setup();
        var whetUid = BagTestItems.AddToStash(context, "b_whet");

        var stepList = CreatePlanner(context).Plan();

        Assert.AreEqual(1, stepList.Count);
        var step = stepList[0];
        Assert.AreEqual(BagPlanStepKind.Drop, step.Kind);
        Assert.AreEqual((whetUid, Lena, 0, 3, 0), (step.Uid, step.Member, step.Rotation, step.X, step.Y));
    }

    [Test]
    public void ItemThatDoesNotRaiseScoreStaysInStash()
    {
        // 화살통은 관통만 준다(원정대 점수에 없음)
        var context = Setup();
        BagTestItems.AddToStash(context, "b_quiver");

        var stepList = CreatePlanner(context).Plan();

        Assert.AreEqual(0, stepList.Count);
    }

    [Test]
    public void SameInputSamePlan()
    {
        var context = Setup();
        BagTestItems.AddToStash(context, "b_whet");
        BagTestItems.AddToStash(context, "b_shield");
        BagTestItems.AddToStash(context, "b_bow");
        var planner = CreatePlanner(context);

        var first = planner.Plan();
        var second = planner.Plan();

        CollectionAssert.AreEqual(first, second);
        Assert.AreEqual(3, context.BagModel.StashList.Count);
    }

    private static TestRunContext Setup()
    {
        var context = BagTestItems.CreateContext();
        BagTestItems.SetupBags(context, new[] { "b_sword" }, new[] { "b_bow" }, new[] { "b_wand" });
        return context;
    }

    private static AutoEquipPlanner CreatePlanner(TestRunContext context) => BagTestItems.CreatePlanner(context);

    private static void Execute(TestRunContext context, System.Collections.Generic.IEnumerable<BagPlanStep> stepList)
    {
        foreach (var step in stepList)
        {
            var isApplied = step.Kind == BagPlanStepKind.Merge
                ? context.BagService.Merge(step.Uid, step.TargetUid, out _)
                : context.BagService.Drop(step.Uid, step.Member, step.X, step.Y, step.Rotation, out _);
            Assert.IsTrue(isApplied, step.Kind.ToString());
        }
    }
}
