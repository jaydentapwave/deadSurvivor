using NUnit.Framework;
using UnityEngine;

public class MemberAttackMotionTests
{
    [TestCase("bruno", false, 0.2f)]
    [TestCase("bruno", true, 0.3f)]
    [TestCase("lena", false, 0.3f)]
    [TestCase("lena", true, 0.3f)]
    [TestCase("mira", false, 0.3f)]
    [TestCase("mira", true, 0.3f)]
    [TestCase("bolt", false, 0.2f)]
    [TestCase("bolt", true, 0.2f)]
    public void HitBeginsAtSourceFrameExactlyOnce(string id, bool isBack, float hitSeconds)
    {
        var data = new CharacterRepository(new ResourceService()).Get(id).Motion;
        var motion = new MemberAttackMotion();
        motion.Start(data, new SpriteFacing(isBack ? FacingDirection.UpRight : FacingDirection.DownRight, true), 5f);
        Assert.AreEqual(hitSeconds, motion.HitSeconds, 1e-5f);
        Assert.IsFalse(motion.Advance(hitSeconds - 0.001f));
        Assert.IsTrue(motion.Advance(0.001f));
        Assert.AreEqual(isBack ? data.HitBack : data.HitFront, motion.GetFrame(data.AttackFrames));
        Assert.IsFalse(motion.Advance(0.01f));
        Assert.IsTrue(motion.IsPlaying);
        motion.Advance(1f);
        Assert.IsFalse(motion.IsPlaying);
    }

    // D7: enumerate both previous source directions and every specified target angle.
    [Test]
    public void D7PreviousBackOnlyChangesTimingInTheApprovedHorizontalSectors()
    {
        var data = new CharacterRepository(new ResourceService()).Get("bruno").Motion;
        foreach (var wasBack in new[] { false, true })
        {
            foreach (var angle in new[] { 0f, 10f, 18f, 30f, -10f, -30f, 90f, 170f, 180f })
            {
                var radians = angle * Mathf.Deg2Rad;
                var screen = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                var oldBack = Mathf.Abs(screen.y) >= .25f * Mathf.Abs(screen.x) ? screen.y > 0f : wasBack;
                var last = new SpriteFacing(wasBack ? FacingDirection.UpRight : FacingDirection.DownRight, false);
                var facing = SpriteFacing.Next(last, screen, .001f, 0f);
                var newBack = facing.Direction == FacingDirection.UpRight || facing.Direction == FacingDirection.Up;
                var oldSeconds = (oldBack ? data.HitBack : data.HitFront) / (float)data.Fps;
                var newSeconds = data.HitSeconds(facing.Direction);
                var horizontal = Mathf.Abs(Mathf.DeltaAngle(0f, angle)) < 14f || Mathf.Abs(Mathf.DeltaAngle(180f, angle)) < 14f;
                var positiveNearHorizontal = angle == 18f;
                Assert.AreEqual((wasBack && horizontal) || positiveNearHorizontal, oldBack != newBack, $"{wasBack}/{angle}");
                Assert.AreEqual(oldBack != newBack ? .1f : 0f, Mathf.Abs(oldSeconds - newSeconds), 1e-5f);
            }
        }
    }

    [Test]
    public void ShortCooldownAcceleratesWholeClipAndStopCancelsHit()
    {
        var data = new CharacterRepository(new ResourceService()).Get("bruno").Motion;
        var motion = new MemberAttackMotion();
        motion.Start(data, SpriteFacing.Front, 0.15f);
        Assert.AreEqual(0.15f, motion.Duration, 1e-5f);
        Assert.AreEqual(0.05f, motion.HitSeconds, 1e-5f);
        Assert.IsTrue(motion.Advance(0.05f));
        motion.Stop();
        Assert.IsFalse(motion.IsPlaying);
        Assert.IsFalse(motion.Advance(1f));
        motion.Start(data, SpriteFacing.Front, 1f);
        Assert.IsFalse(motion.HasHit);
        Assert.AreEqual(0, motion.GetFrame(data.AttackFrames));
    }

    [TestCase("sword", false)]
    [TestCase("shortbow", true)]
    public void WeaponWaitsForHitFrame(string itemId, bool isProjectile)
    {
        var context = new TestRunContext();
        context.SetupParty(new[] { itemId });
        var member = context.PartyModel.MemberList[0];
        var target = context.AddMonster(1, member.Position + Vector2.right * 50f, 1000f);
        context.CombatService.Step(0f);
        var motion = context.CombatModel.MemberStateList[0].AttackMotion;
        Assert.AreEqual(1000f, target.Hp);
        Assert.AreEqual(0, context.CombatModel.ProjectileList.Count);
        context.CombatService.Step(motion.HitSeconds - 0.001f);
        Assert.AreEqual(1000f, target.Hp);
        Assert.AreEqual(0, context.CombatModel.ProjectileList.Count);
        context.CombatService.Step(0.001f);
        Assert.AreEqual(isProjectile ? 1 : 0, context.CombatModel.ProjectileList.Count);
        Assert.AreEqual(!isProjectile, target.Hp < 1000f);
        Assert.AreEqual(0, context.CombatModel.MemberStateList[0].PendingActionList.Count);
    }

    [Test]
    public void DownedBeforeHitCancelsDamageAndReviveStartsFresh()
    {
        var context = new TestRunContext();
        context.SetupParty(new[] { "sword" });
        var member = context.PartyModel.MemberList[0];
        var target = context.AddMonster(1, member.Position + Vector2.right * 30f, 1000f);
        context.CombatService.Step(0f);
        member.Hp = 0f;
        context.CombatService.Step(1f);
        var state = context.CombatModel.MemberStateList[0];
        Assert.IsFalse(state.AttackMotion.IsPlaying);
        Assert.AreEqual(0, state.PendingActionList.Count);
        Assert.AreEqual(1000f, target.Hp);
        context.CombatService.ReviveMember(0);
        context.CombatService.Step(0f);
        Assert.IsTrue(state.AttackMotion.IsPreparing);
        Assert.AreEqual(0f, state.AttackMotion.Elapsed);
        Assert.AreEqual(1000f, target.Hp);
    }

    [Test]
    public void SimultaneousWeaponsShareHitAndKeepOwnCooldowns()
    {
        var context = new TestRunContext();
        context.SetupParty(new[] { "sword", "shortbow" });
        var member = context.PartyModel.MemberList[0];
        context.AddMonster(1, member.Position + Vector2.right * 30f, 1000f);
        context.CombatService.Step(0f);
        var state = context.CombatModel.MemberStateList[0];
        Assert.AreEqual(2, state.PendingActionList.Count);
        foreach (var slot in state.SlotList)
        {
            Assert.AreEqual(slot.LastPeriod, slot.RemainingCooldown);
            Assert.AreEqual(1, context.CombatModel.GetFireCount(slot.Item.Id));
        }

        context.CombatService.Step(state.AttackMotion.HitSeconds);
        Assert.AreEqual(0, state.PendingActionList.Count);
        context.CombatService.ClearProjectiles();
        Assert.IsFalse(state.AttackMotion.IsPlaying);
    }
    [Test]
    public void LaterWeaponJoinsPreparationButRestartsRecovery()
    {
        var context = new TestRunContext();
        context.SetupParty(new[] { "sword", "shortbow" });
        var member = context.PartyModel.MemberList[0];
        context.AddMonster(1, member.Position + Vector2.right * 30f, 1000f);
        var state = context.CombatModel.MemberStateList[0];
        state.SlotList[1].RemainingCooldown = 0.1f;
        context.CombatService.Step(0f);
        Assert.AreEqual(1, state.PendingActionList.Count);
        context.CombatService.Step(0.1f);
        Assert.AreEqual(2, state.PendingActionList.Count);
        Assert.AreEqual(0.1f, state.AttackMotion.Elapsed, 1e-5f);
        context.CombatService.Step(0.1f);
        Assert.IsTrue(state.AttackMotion.HasHit);
        Assert.AreEqual(0, state.PendingActionList.Count);
        state.SlotList[1].RemainingCooldown = 0f;
        context.CombatService.Step(0f);
        Assert.IsTrue(state.AttackMotion.IsPreparing);
        Assert.AreEqual(0f, state.AttackMotion.Elapsed);
        Assert.AreEqual(1, state.PendingActionList.Count);
        context.CombatService.PrepareWave();
        Assert.IsFalse(state.AttackMotion.IsPlaying);
        Assert.AreEqual(0, state.PendingActionList.Count);
    }

}
