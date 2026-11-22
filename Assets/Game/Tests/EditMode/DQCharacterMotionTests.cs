using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class DQCharacterMotionTests
{
    private static DQClipData Clip(bool loop = false, int eventFrame = 1) => new()
    {
        id = "attack_right", frameKeys = new[] { "a", "b", "c" },
        durationsMs = new[] { 100, 200, 300 }, loop = loop, eventFrame = eventFrame
    };

    [TestCase(-1f, 0)]
    [TestCase(0f, 0)]
    [TestCase(.099f, 0)]
    [TestCase(.0999995f, 0)]
    [TestCase(.1000005f, 1)]
    [TestCase(.2999995f, 1)]
    [TestCase(.3000005f, 2)]
    [TestCase(.100f, 1)]
    [TestCase(.299f, 1)]
    [TestCase(.300f, 2)]
    [TestCase(60f, 2)]
    public void UnequalDurationsHaveExactBoundaries(float seconds, int expected)
    {
        Assert.AreEqual(expected, DQClipMath.FrameAt(Clip(), seconds));
    }

    [TestCase(.6f, 0)]
    [TestCase(6.1f, 1)]
    public void LoopWrapsAcrossManyCycles(float seconds, int expected)
    {
        Assert.AreEqual(expected, DQClipMath.FrameAt(Clip(true), seconds));
    }

    [TestCase(-2)]
    [TestCase(-1)]
    [TestCase(3)]
    public void InvalidAttackEventIsRejected(int eventFrame)
    {
        Assert.Throws<InvalidOperationException>(() => Clip(false, eventFrame).Validate());
    }

    [Test]
    public void EmptyAndMismatchedAndZeroDurationsAreRejected()
    {
        var clip = Clip();
        clip.durationsMs = new[] { 100 };
        Assert.Throws<InvalidOperationException>(clip.Validate);
        clip = Clip();
        clip.durationsMs[1] = 0;
        Assert.Throws<InvalidOperationException>(clip.Validate);
        clip = Clip();
        clip.frameKeys = Array.Empty<string>();
        Assert.Throws<InvalidOperationException>(clip.Validate);
    }

    [TestCase(0)]
    [TestCase(2)]
    public void FirstAndLastHitFramesAndZeroPreparationHaveNoDivisionByZero(int hit)
    {
        var clip = Clip(false, hit);
        clip.Validate();
        Assert.AreEqual(hit, DQClipMath.AttackFrame(clip, 0, 0, true));
        Assert.AreEqual(hit, DQClipMath.AttackFrame(clip, .2f, .2f, true));
        Assert.That(DQClipMath.AttackFrame(clip, .1f, .2f, false), Is.InRange(0, hit));
    }

    [Test]
    public void HitBatchSurvivesRestartAndDoesNotConsumeNewerRecords()
    {
        var hits = new DQHitBuffer();
        hits.Add(new DQHitRecord(1, 1, new SpriteFacing(FacingDirection.Up, false)));
        var applied = hits.Latest;
        hits.Add(new DQHitRecord(2, 2, SpriteFacing.Front));
        hits.Acknowledge(applied.AttackId);
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual(2, hits.Latest.AttackId);
        Assert.AreEqual(FacingDirection.DownRight, hits.Latest.Facing.Direction);
        hits.Acknowledge(2);
        hits.Acknowledge(2);
        Assert.AreEqual(0, hits.Count);
        Assert.AreEqual(2, hits.Rendered);
    }

    [Test]
    public void MultipleStepsCoalesceOnlyAfterAcknowledgementAndResetClears()
    {
        var hits = new DQHitBuffer();
        for (var i = 1; i <= 4; i++)
        {
            hits.Add(new DQHitRecord(i, i, SpriteFacing.Front));
        }
        Assert.AreEqual(4, hits.Count);
        Assert.AreEqual(0, hits.Rendered);
        hits.Acknowledge(hits.Latest.AttackId);
        Assert.AreEqual(1, hits.Rendered);
        Assert.AreEqual(3, hits.Coalesced);
        hits.Add(new DQHitRecord(5, 5, SpriteFacing.Front));
        hits.Clear("test-reset");
        Assert.AreEqual(0, hits.Count);
        Assert.AreEqual(1, hits.Suppressed);
    }

    [Test]
    public void EveryAttackHasItsRenderedBatchOrSuppressionCause()
    {
        var hits = new DQHitBuffer();
        var audit = new System.Collections.Generic.List<DQHitDisposition>();
        hits.Recorded += audit.Add;
        hits.Add(new DQHitRecord(10, 1, new SpriteFacing(FacingDirection.Right, false)));
        hits.Add(new DQHitRecord(11, 2, new SpriteFacing(FacingDirection.Up, true)));
        hits.Acknowledge(11);
        hits.Add(new DQHitRecord(12, 3, SpriteFacing.Front));
        Assert.Throws<ArgumentException>(() => hits.Clear(null));
        Assert.AreEqual(1, hits.Count, "invalid cause must not discard the pending id");
        hits.Clear("suppressed-by-death");
        hits.Add(new DQHitRecord(13, 4, SpriteFacing.Front));
        hits.Clear("dispose");
        Assert.AreEqual(new long[] { 10, 11, 12, 13 }, audit.Select(a => a.Hit.AttackId));
        Assert.AreEqual(new[] { "coalesced", "rendered", "suppressed-by-death", "dispose" }, audit.Select(a => a.Reason));
        Assert.AreEqual(new long[] { 11, 11, 0, 0 }, audit.Select(a => a.AppliedAttackId));
        Assert.AreEqual(new[] { 1, 1, 0, 0 }, audit.Select(a => a.Batch));
        Assert.AreEqual(FacingDirection.Up, audit[1].Hit.Facing.Direction);
        Assert.IsTrue(audit[1].Hit.Facing.IsFlipped);
        Assert.AreEqual(2, audit[1].Hit.Step);
    }

    [Test]
    public void RealCombatLargeStepPreservesHitBeforeSameStepNewAttack()
    {
        var context = new TestRunContext();
        context.SetupParty(new[] { "sword" });
        var events = new EventBus();
        var hits = new DQHitBuffer();
        using var subscription = events.Subscribe<GameEvent.MemberAttackHitEvent>((in GameEvent.MemberAttackHitEvent e) => hits.Add(e.Hit));
        var combat = new CombatService(context.CombatModel, context.PartyModel, context.PartyService,
            context.MonsterModel, context.MonsterService, context.ItemRepository, context.CharacterRepository,
            context.EquipmentStatService, context.CombatRulesRepository, context.ViewRules, events);
        var member = context.PartyModel.MemberList[0];
        var monster = new Monster(900, context.MonsterRepository.Get("slime"), member.Position + Vector2.right * 10, 10000, 1, 100);
        context.MonsterModel.Add(monster);
        combat.Step(.033f);
        var motion = context.CombatModel.MemberStateList[0].AttackMotion;
        var id = motion.AttackId;
        var facing = motion.Facing;
        combat.Step(2f);
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual(id, hits.Latest.AttackId);
        Assert.AreEqual(facing.Label, hits.Latest.Facing.Label);
        Assert.Greater(motion.AttackId, id, "same step starts a new attack");
        Assert.Less(monster.Hp, 10000, "real damage was applied");
        Assert.AreEqual(1, hits.Received);
    }

    [Test]
    public void CatalogHasOnlyAdoptedDirectionsAndVariableTimes()
    {
        var catalog = new ResourceService().LoadJson<DQClipCatalog>("Data/DQ/dq_character_clips");
        Assert.AreEqual(9, catalog.actors.Length);
        foreach (var actor in catalog.actors)
        {
            Assert.AreEqual(5, actor.staticKeys.Length);
            Assert.AreEqual(5, actor.directionSources.Length);
            foreach (var clip in actor.clips)
            {
                clip.Validate();
                if (actor.hero && clip.id.StartsWith("walk_"))
                {
                    Assert.AreEqual(24, clip.frameKeys.Length);
                }
                if (actor.hero && clip.id.StartsWith("idle_"))
                {
                    Assert.AreEqual(12, clip.frameKeys.Length);
                }
                if (actor.hero && clip.id.StartsWith("attack_"))
                {
                    Assert.AreEqual(7, clip.frameKeys.Length);
                }
            }
        }
        var lena = catalog.actors.Single(a => a.actorId == "lena");
        Assert.AreEqual(lena.clips.Single(c => c.id == "attack_right").frameKeys,
            lena.clips.Single(c => c.id == "attack_upright").frameKeys);
        Assert.AreEqual(lena.clips.Single(c => c.id == "attack_down").frameKeys,
            lena.clips.Single(c => c.id == "attack_front").frameKeys);
        Assert.That(catalog.actors.Single(a => a.actorId == "boar").clips.All(c => c.id != "fall_front"));
    }
}
