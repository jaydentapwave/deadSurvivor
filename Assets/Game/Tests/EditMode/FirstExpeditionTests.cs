using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class FirstExpeditionTests
{
    [Test]
    public void ApproachAndCombatRecruitTwoMembersWithoutReplacingExistingBags()
    {
        var game = new TestRunContext();
        game.RunService.StartRun(73, 1f, true);
        CollectionAssert.AreEqual(new[] { "bruno", "bolt" }, game.PartyModel.MemberList.Select(member => member.CharacterId));

        game.StepSeconds(2f);
        Assert.That(game.FirstExpedition.HasEncounter, Is.True);
        Assert.That(game.RunModel.RemainingSeconds.CurrentValue, Is.EqualTo(game.RunModel.WaveSeconds.CurrentValue));
        Assert.That(game.RunModel.Kills.CurrentValue, Is.Zero);
        ClearEncounterByPlaying(game);
        Assert.That(game.RunModel.Kills.CurrentValue, Is.EqualTo(1));

        game.StepUntilBattleEnds(1000);
        Assert.That(game.RunModel.Phase.CurrentValue, Is.EqualTo(RunPhase.Prep));
        var stone = game.BagModel.StashList.First(item => item.ItemId == "whetstone");
        Assert.That(game.BagService.Drop(stone.Uid, 0, 0, 1, 0, out _), Is.True);

        game.RunService.NextWave();
        Assert.That(game.FirstExpedition.WaitingCharacterId, Is.EqualTo("lena"));
        ClearEncounterByPlaying(game);
        Assert.That(game.PartyModel.MemberList[2].CharacterId, Is.EqualTo("lena"));
        Assert.That(game.BagModel.GetBag(2).Single().ItemId, Is.EqualTo("shortbow"));
        Assert.That(game.CombatModel.MemberStateList[2].SlotList.Single().Item.Id, Is.EqualTo("shortbow"));

        game.StepUntilBattleEnds(1000);
        Assert.That(game.RunModel.Phase.CurrentValue, Is.EqualTo(RunPhase.Prep));
        var secondStone = game.BagModel.StashList.First(item => item.ItemId == "whetstone");
        Assert.That(game.BagService.Merge(secondStone.Uid, stone.Uid, out _), Is.True);
        Assert.That(stone.Grade, Is.EqualTo(1));

        game.RunService.NextWave();
        Assert.That(game.FirstExpedition.WaitingCharacterId, Is.EqualTo("mira"));
        var killsBeforeRescue = game.RunModel.Kills.CurrentValue;
        game.StepSeconds(2f);
        Assert.That(game.MonsterService.AliveCount, Is.EqualTo(3));
        Assert.That(game.RunModel.Kills.CurrentValue, Is.EqualTo(killsBeforeRescue));
        ClearEncounterByPlaying(game);
        CollectionAssert.AreEqual(new[] { "bruno", "bolt", "lena", "mira" }, game.PartyModel.MemberList.Select(member => member.CharacterId));
        Assert.That(game.BagModel.GetBag(3).Single().ItemId, Is.EqualTo("firestaff"));
        Assert.That(game.CombatModel.MemberStateList[3].SlotList.Single().Item.Id, Is.EqualTo("firestaff"));
        Assert.That(game.BagModel.GetBag(0).Single(item => item.Uid == stone.Uid).Grade, Is.EqualTo(1));
    }

    [Test]
    public void RevivingTheOnlyAttackerLetsThePartyLeaveCamp()
    {
        var game = new TestRunContext();
        game.RunService.StartRun(73, 0f, true);
        ClearEncounterByPlaying(game);
        game.StepUntilBattleEnds(1000);
        game.PartyService.ApplyDamage(game.PartyModel.MemberList[0], 10000f);

        Assert.That(game.RunService.CanStartNextWave, Is.False);
        game.RunService.NextWave();
        Assert.That(game.RunModel.Phase.CurrentValue, Is.EqualTo(RunPhase.Prep));

        Assert.That(game.ReviveService.Revive(0), Is.True);
        game.RunService.NextWave();
        Assert.That(game.RunModel.Phase.CurrentValue, Is.EqualTo(RunPhase.Battle));
        Assert.That(game.FirstExpedition.WaitingCharacterId, Is.EqualTo("lena"));
    }

    private static void ClearEncounterByPlaying(TestRunContext game)
    {
        for (var i = 0; i < 360 && game.FirstExpedition.HasEncounter; i++)
        {
            var toEncounter = game.FirstExpedition.EncounterPosition - game.PartyModel.Center;
            var screenDirection = new Vector2(toEncounter.x, toEncounter.y * game.ViewRules.SinTilt);
            game.PartyMoveService.SetMoveInput(toEncounter.magnitude > 55f ? screenDirection.normalized : Vector2.zero);
            game.RunService.Step(game.RunRules.SimStep);
        }

        game.PartyMoveService.SetMoveInput(Vector2.zero);
        Assert.That(game.FirstExpedition.HasEncounter, Is.False);
        Assert.That(game.RunModel.Phase.CurrentValue, Is.EqualTo(RunPhase.Battle));
    }
}
