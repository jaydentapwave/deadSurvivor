using NUnit.Framework;

// F6 데이터 — feedback_rules.json을 읽으면 스키마 출발값이 그대로다(기획서 데이터 스키마)
public class FeedbackDataLoadTests
{
    [Test]
    public void FeedbackRulesLoadWithSchemaStartValues()
    {
        var rules = new FeedbackRulesRepository(new ResourceService()).Rules;

        Assert.AreEqual(0.65f, rules.DamageNumber.Lifetime, 1e-5f);
        Assert.AreEqual(12f, rules.DamageNumber.RisePx, 1e-5f);
        Assert.AreEqual(0.6f, rules.DamageNumber.FadeStartRatio, 1e-5f);
        Assert.AreEqual(40, rules.DamageNumber.MaxActive);
        Assert.AreEqual("#ffffff", rules.DamageNumber.TextColor);
        Assert.AreEqual("#000000", rules.DamageNumber.OutlineColor);
        Assert.AreEqual(0.4f, rules.HpLoss.HoldSeconds, 1e-5f);
        Assert.AreEqual(0.35f, rules.HpLoss.ShrinkSeconds, 1e-5f);
        Assert.AreEqual(2, rules.Shake.AmplitudePx);
        Assert.AreEqual(0.15f, rules.Shake.Seconds, 1e-5f);
        Assert.AreEqual(0.25f, rules.Shake.MinInterval, 1e-5f);
        Assert.IsTrue(rules.Haptic.Enabled);
        Assert.AreEqual(0.3f, rules.Haptic.MinInterval, 1e-5f);
        Assert.AreEqual(40, rules.MonsterHp.MaxActive);
        Assert.AreEqual(16, rules.MonsterHp.WidthPx);
        Assert.AreEqual(2, rules.MonsterHp.HeightPx);
        CollectionAssert.AreEqual(new[] { "armored" }, rules.MonsterHp.AlwaysShowBehaviors);
    }
}
