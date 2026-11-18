using NUnit.Framework;

// F6 AC8 — HP 감소 구간 빨간 강조(기획서 C절). 값은 실제 feedback_rules.json
public class HpLossTrailTests
{
    private const float Frame = 1f / 60f;

    private static HpLossRulesData Rules => new FeedbackRulesRepository(new ResourceService()).Rules.HpLoss;

    [Test]
    public void LossStaysForHoldThenShrinksToZeroWithinShrinkSeconds()
    {
        var rules = Rules;
        var trail = new HpLossTrail();
        trail.Reset(1f);

        trail.Hit(1f, 0.7f, rules.HoldSeconds);
        trail.Tick(0f, 0.7f, rules.ShrinkSeconds);
        Assert.AreEqual(1f, trail.LossFrom, 1e-5f);
        Assert.AreEqual(0.3f, trail.Length, 1e-5f);

        var holdElapsed = 0f;
        while (holdElapsed + Frame < rules.HoldSeconds)
        {
            trail.Tick(Frame, 0.7f, rules.ShrinkSeconds);
            holdElapsed += Frame;
        }

        Assert.AreEqual(0.3f, trail.Length, 1e-5f);
        var shrinkElapsed = 0f;
        while (trail.Length > 0f && shrinkElapsed < rules.HoldSeconds + rules.ShrinkSeconds + 1f)
        {
            trail.Tick(Frame, 0.7f, rules.ShrinkSeconds);
            shrinkElapsed += Frame;
        }

        Assert.AreEqual(0f, trail.Length);
        Assert.LessOrEqual(holdElapsed + shrinkElapsed, rules.HoldSeconds + rules.ShrinkSeconds + Frame * 1.5f);
    }

    [Test]
    public void SecondHitDuringHoldKeepsHigherStartAndRestartsHold()
    {
        var rules = Rules;
        var trail = new HpLossTrail();
        trail.Reset(1f);
        trail.Hit(1f, 0.7f, rules.HoldSeconds);
        trail.Tick(rules.HoldSeconds * 0.5f, 0.7f, rules.ShrinkSeconds);

        trail.Hit(0.7f, 0.5f, rules.HoldSeconds);
        trail.Tick(0f, 0.5f, rules.ShrinkSeconds);

        Assert.AreEqual(1f, trail.LossFrom, 1e-5f);
        Assert.AreEqual(0.5f, trail.Length, 1e-5f);
        Assert.AreEqual(rules.HoldSeconds, trail.HoldRemaining, 1e-5f);
        trail.Tick(rules.HoldSeconds - 0.01f, 0.5f, rules.ShrinkSeconds);
        Assert.AreEqual(0.5f, trail.Length, 1e-5f);
    }

    [Test]
    public void HealOrReviveEndsLossAtOnce()
    {
        var rules = Rules;
        var trail = new HpLossTrail();
        trail.Reset(1f);
        trail.Hit(1f, 0.6f, rules.HoldSeconds);
        trail.Tick(Frame, 0.6f, rules.ShrinkSeconds);

        trail.Tick(Frame, 0.65f, rules.ShrinkSeconds);

        Assert.AreEqual(0f, trail.Length);

        trail.Hit(0.65f, 0f, rules.HoldSeconds);
        trail.Tick(Frame, 0f, rules.ShrinkSeconds);
        Assert.AreEqual(0.65f, trail.Length, 1e-5f);
        trail.Tick(Frame, 1f, rules.ShrinkSeconds);
        Assert.AreEqual(0f, trail.Length);
    }

    [Test]
    public void ShieldAbsorbingWholeHitMakesNoLoss()
    {
        var rules = Rules;
        var trail = new HpLossTrail();
        trail.Reset(0.8f);

        trail.Hit(0.8f, 0.8f, rules.HoldSeconds);
        trail.Tick(Frame, 0.8f, rules.ShrinkSeconds);

        Assert.AreEqual(0f, trail.Length);
        Assert.AreEqual(0f, trail.HoldRemaining);
    }
}
