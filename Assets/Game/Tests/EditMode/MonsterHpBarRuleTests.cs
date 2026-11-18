using System.Collections.Generic;
using NUnit.Framework;

// F6 AC10 — 몬스터 HP 막대 규칙(기획서 E절): 행동 5종 × 공격 중·아님, 머무름, 상한 순서. 값은 실제 몬스터·피드백 데이터
public class MonsterHpBarRuleTests
{
    [Test]
    public void ContactAndArmoredAttackOnlyWhenTouching()
    {
        var monsters = new MonsterRepository(new ResourceService());
        foreach (var id in new[] { "slime", "rock" })
        {
            var data = monsters.Get(id);
            var contact = data.Radius + data.ContactGap;
            Assert.IsTrue(MonsterHpBarRule.IsAttacking(data.Behavior, MonsterPhase.Approach, contact, contact, 0f), id);
            // ContactBehavior와 같은 여유 0.001 안이면 붙은 것
            Assert.IsTrue(MonsterHpBarRule.IsAttacking(data.Behavior, MonsterPhase.Approach, contact + 0.0005f, contact, 0f), id);
            Assert.IsFalse(MonsterHpBarRule.IsAttacking(data.Behavior, MonsterPhase.Approach, contact + 5f, contact, 0f), id);
        }
    }

    [Test]
    public void ProjectileAttacksInsideRangeOnly()
    {
        var data = new MonsterRepository(new ResourceService()).Get("mushroom");
        var range = data.Projectile.Range;
        Assert.AreEqual(MonsterBehavior.Projectile, data.Behavior);
        Assert.IsTrue(MonsterHpBarRule.IsAttacking(data.Behavior, MonsterPhase.Approach, range - 1f, 0f, range));
        Assert.IsFalse(MonsterHpBarRule.IsAttacking(data.Behavior, MonsterPhase.Approach, range, 0f, range));
    }

    [Test]
    public void ChargeAndBombAttackOnlyInTheirPhases()
    {
        foreach (MonsterPhase phase in System.Enum.GetValues(typeof(MonsterPhase)))
        {
            var isCharging = phase == MonsterPhase.Windup || phase == MonsterPhase.Dash;
            Assert.AreEqual(isCharging, MonsterHpBarRule.IsAttacking(MonsterBehavior.Charge, phase, 1f, 100f, 0f), $"charge {phase}");
            Assert.AreEqual(phase == MonsterPhase.Fuse, MonsterHpBarRule.IsAttacking(MonsterBehavior.Bomb, phase, 1f, 100f, 0f), $"bomb {phase}");
        }
    }

    [Test]
    public void StrongShowsAlwaysAndAttackLingers()
    {
        var rules = new FeedbackRulesRepository(new ResourceService()).Rules.MonsterHp;
        CollectionAssert.AreEqual(new[] { "armored" }, rules.AlwaysShowBehaviors);

        Assert.IsTrue(MonsterHpBarRule.IsCandidate(true, false, 100f, float.NegativeInfinity, rules.LingerSeconds));
        Assert.IsTrue(MonsterHpBarRule.IsCandidate(false, true, 0f, 0f, rules.LingerSeconds));
        Assert.IsTrue(MonsterHpBarRule.IsCandidate(false, false, rules.LingerSeconds - 0.01f, 0f, rules.LingerSeconds));
        Assert.IsFalse(MonsterHpBarRule.IsCandidate(false, false, rules.LingerSeconds + 0.01f, 0f, rules.LingerSeconds));
        // 멀리 있는 다친 슬라임: 강하지 않고 공격한 적 없음
        Assert.IsFalse(MonsterHpBarRule.IsCandidate(false, false, 5f, float.NegativeInfinity, rules.LingerSeconds));
    }

    [Test]
    public void CapKeepsStrongFirstThenNearest()
    {
        var candidateList = new List<MonsterHpBarCandidate>
        {
            new(1, false, 10f), new(2, true, 300f), new(3, false, 5f), new(4, false, 50f), new(5, true, 100f),
        };
        var shownIdSet = new HashSet<int>();

        MonsterHpBarRule.SelectShown(candidateList, 3, shownIdSet);

        CollectionAssert.AreEquivalent(new[] { 2, 5, 3 }, shownIdSet);

        MonsterHpBarRule.SelectShown(candidateList, 10, shownIdSet);
        Assert.AreEqual(5, shownIdSet.Count);
    }
}
