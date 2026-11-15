using NUnit.Framework;
using UnityEngine;

// AC8·AC9 — 발동 방식과 쿨타임(기획서 B5). F1 기본 무기 시험(쓰러진 대원은 발동 0, 가장 가까운 적)도 여기로 옮겼다
public class ItemActivationTests
{
    private const float Hp = 1000f;
    // 시험 공격 아이템 피해 10 × 브루노 공격 배수 0.8
    private const float Hit = 8f;

    [Test]
    public void MeleeHitsOnlyWithinHalfAngle50AndRange()
    {
        var context = Create("t_melee_test");
        var origin = context.PartyModel.MemberList[0].Position;
        var target = context.AddMonster(1, origin + new Vector2(50f, 0f), Hp);
        var inCone = context.AddMonster(2, origin + Polar(45f, 100f), Hp);
        var outCone = context.AddMonster(3, origin + Polar(60f, 100f), Hp);
        var outRange = context.AddMonster(4, origin + new Vector2(140f, 0f), Hp);

        context.CombatService.Step(context.RunRules.SimStep);

        Assert.AreEqual(Hp - Hit, target.Hp, 1e-3f);
        Assert.AreEqual(Hp - Hit, inCone.Hp, 1e-3f);
        Assert.AreEqual(Hp, outCone.Hp);
        Assert.AreEqual(Hp, outRange.Hp);
    }

    [Test]
    public void SweepHitsWithinHalfAngle90()
    {
        var context = Create("t_sweep_test");
        var origin = context.PartyModel.MemberList[0].Position;
        context.AddMonster(1, origin + new Vector2(50f, 0f), Hp);
        var inCone = context.AddMonster(2, origin + Polar(80f, 100f), Hp);
        var outCone = context.AddMonster(3, origin + Polar(100f, 100f), Hp);

        context.CombatService.Step(context.RunRules.SimStep);

        Assert.AreEqual(Hp - Hit, inCone.Hp, 1e-3f);
        Assert.AreEqual(Hp, outCone.Hp);
    }

    [Test]
    public void ProjectilePierce0HitsOneAndPierce1HitsTwoAndRangeEnds()
    {
        foreach (var pierce in new[] { 0, 1 })
        {
            var items = pierce == 0 ? new[] { "t_shot_test" } : new[] { "t_shot_test", "t_pierce" };
            var context = Create(new FakePlacementInfo { IsAllAdjacent = true }, items);
            var origin = context.PartyModel.MemberList[0].Position;
            var first = context.AddMonster(1, origin + new Vector2(50f, 0f), Hp);
            var second = context.AddMonster(2, origin + new Vector2(80f, 0f), Hp);
            var beyond = context.AddMonster(3, origin + new Vector2(200f, 0f), Hp);
            // 관통 화살촉 기본 효과 공격 +0.05
            var hit = pierce == 0 ? Hit : Hit * 1.05f;

            // 사거리 126(3칸)을 0.67초면 다 난다. 간격 5초라 다음 발사는 없다
            context.StepCombat(20, context.RunRules.SimStep);

            Assert.AreEqual(Hp - hit, first.Hp, 1e-3f, $"pierce {pierce}");
            Assert.AreEqual(pierce == 0 ? Hp : Hp - hit, second.Hp, 1e-3f, $"pierce {pierce}");
            Assert.AreEqual(Hp, beyond.Hp, $"pierce {pierce}");
            Assert.AreEqual(0, context.CombatModel.ProjectileList.Count, $"pierce {pierce}");
            Assert.AreEqual(1, context.CombatModel.GetFireCount("t_shot_test"));
        }
    }

    [Test]
    public void ExtraShot1FiresTwoAtPlusMinus006Rad()
    {
        var context = Create(new FakePlacementInfo { IsAllAdjacent = true }, "t_shot_test", "t_split");
        var origin = context.PartyModel.MemberList[0].Position;
        context.AddMonster(1, origin + new Vector2(0f, 100f), Hp);

        context.CombatService.Step(context.RunRules.SimStep);

        var projectiles = context.CombatModel.ProjectileList;
        Assert.AreEqual(2, projectiles.Count);
        Assert.AreEqual(-0.06f * Mathf.Rad2Deg, Vector2.SignedAngle(Vector2.up, projectiles[0].Direction), 1e-3f);
        Assert.AreEqual(0.06f * Mathf.Rad2Deg, Vector2.SignedAngle(Vector2.up, projectiles[1].Direction), 1e-3f);
    }

    [Test]
    public void ExplosionHitsAllWithinRadius75Or105()
    {
        foreach (var hasSpellbook in new[] { false, true })
        {
            var items = hasSpellbook ? new[] { "t_blast_test", "t_spellbook" } : new[] { "t_blast_test" };
            var context = Create(new FakePlacementInfo { IsAllAdjacent = true }, items);
            var origin = context.PartyModel.MemberList[0].Position;
            var target = context.AddMonster(1, origin + new Vector2(150f, 0f), Hp);
            var near = context.AddMonster(2, origin + new Vector2(150f, 60f), Hp);
            var middle = context.AddMonster(3, origin + new Vector2(150f, 90f), Hp);
            var far = context.AddMonster(4, origin + new Vector2(150f, 120f), Hp);

            context.StepCombat(30, context.RunRules.SimStep);

            var label = hasSpellbook ? "radius 105" : "radius 75";
            // 마법서 기본 효과 공격 +0.06
            var hit = hasSpellbook ? Hit * 1.06f : Hit;
            Assert.AreEqual(Hp - hit, target.Hp, 1e-3f, label);
            Assert.AreEqual(Hp - hit, near.Hp, 1e-3f, label);
            Assert.AreEqual(hasSpellbook ? Hp - hit : Hp, middle.Hp, 1e-3f, label);
            Assert.AreEqual(Hp, far.Hp, label);
            Assert.AreEqual(0, context.CombatModel.ProjectileList.Count, label);
        }
    }

    [Test]
    public void ChainHits3Or5AndStopsBeyondHopRange()
    {
        foreach (var hasHat in new[] { false, true })
        {
            var items = hasHat ? new[] { "t_chain_test", "t_hat" } : new[] { "t_chain_test" };
            var context = Create(new FakePlacementInfo { IsAllAdjacent = true }, items);
            var origin = context.PartyModel.MemberList[0].Position;
            var monsters = new Monster[6];
            for (var i = 0; i < monsters.Length; i++)
            {
                monsters[i] = context.AddMonster(i + 1, origin + new Vector2(100f * (i + 1), 0f), Hp);
            }

            context.CombatService.Step(context.RunRules.SimStep);

            var expectedHits = hasHat ? 5 : 3;
            // 마법사 모자 기본 효과 공격 +0.05
            var hit = hasHat ? Hit * 1.05f : Hit;
            for (var i = 0; i < monsters.Length; i++)
            {
                Assert.AreEqual(i < expectedHits ? Hp - hit : Hp, monsters[i].Hp, 1e-3f, $"hat {hasHat} monster {i}");
            }
        }

        var gapped = Create("t_chain_test");
        var gappedOrigin = gapped.PartyModel.MemberList[0].Position;
        var a = gapped.AddMonster(1, gappedOrigin + new Vector2(100f, 0f), Hp);
        var b = gapped.AddMonster(2, gappedOrigin + new Vector2(200f, 0f), Hp);
        var c = gapped.AddMonster(3, gappedOrigin + new Vector2(360f, 0f), Hp);
        gapped.CombatService.Step(gapped.RunRules.SimStep);
        Assert.AreEqual(Hp - Hit, a.Hp, 1e-3f);
        Assert.AreEqual(Hp - Hit, b.Hp, 1e-3f);
        Assert.AreEqual(Hp, c.Hp, "160 > 150 hop");

        var pair = Create("t_chain_test");
        var pairOrigin = pair.PartyModel.MemberList[0].Position;
        var p1 = pair.AddMonster(1, pairOrigin + new Vector2(100f, 0f), Hp);
        var p2 = pair.AddMonster(2, pairOrigin + new Vector2(150f, 0f), Hp);
        pair.CombatService.Step(pair.RunRules.SimStep);
        Assert.AreEqual(Hp - Hit, p1.Hp, 1e-3f, "no double hit");
        Assert.AreEqual(Hp - Hit, p2.Hp, 1e-3f, "no double hit");
    }

    [Test]
    public void SingleHealPicksLowestRatioAndAreaHealsAllHurtInRange()
    {
        var single = Create(new FakePlacementInfo(), new[] { "t_heal_test" }, new string[0], new string[0], new string[0]);
        var members = single.PartyModel.MemberList;
        // 레나 88/110 = 0.8, 미라 70/140 = 볼트 60/120 = 0.5
        members[1].Hp = 88f;
        members[2].Hp = 70f;
        members[3].Hp = 60f;
        var before = new[] { members[0].Hp, members[1].Hp, members[2].Hp, members[3].Hp };

        single.CombatService.Step(single.RunRules.SimStep);

        Assert.AreEqual(before[0], members[0].Hp, "full member not chosen");
        Assert.AreEqual(before[1], members[1].Hp);
        Assert.AreEqual(before[2] + 10f, members[2].Hp, 1e-3f, "lowest ratio, first in order");
        Assert.AreEqual(before[3], members[3].Hp);

        var area = Create(new FakePlacementInfo(), new[] { "t_heal_area_test" }, new string[0], new string[0], new string[0]);
        var areaMembers = area.PartyModel.MemberList;
        areaMembers[1].Hp = 50f;
        areaMembers[2].Hp = 50f;
        areaMembers[3].Hp = 50f;
        areaMembers[3].Position += new Vector2(1000f, 0f);

        area.CombatService.Step(area.RunRules.SimStep);

        Assert.AreEqual(areaMembers[0].MaxHp, areaMembers[0].Hp);
        Assert.AreEqual(60f, areaMembers[1].Hp, 1e-3f);
        Assert.AreEqual(60f, areaMembers[2].Hp, 1e-3f);
        Assert.AreEqual(50f, areaMembers[3].Hp, "out of range");
    }

    [Test]
    public void TwoItemsKeepOwnCooldowns()
    {
        var context = Create("t_cd_1", "t_cd_075");
        context.AddMonster(1, context.PartyModel.MemberList[0].Position + new Vector2(30f, 0f), 1e9f);

        context.StepCombat(48, 0.125f);

        Assert.AreEqual(6, context.CombatModel.GetFireCount("t_cd_1"));
        Assert.AreEqual(8, context.CombatModel.GetFireCount("t_cd_075"));
    }

    [Test]
    public void NoTargetKeepsCooldownReadyAndFiresOnFirstStepWithTarget()
    {
        var context = Create("t_melee_test");
        var slot = context.CombatModel.MemberStateList[0].SlotList[0];

        context.StepCombat(10, context.RunRules.SimStep);
        Assert.AreEqual(0, context.CombatModel.GetFireCount("t_melee_test"));
        Assert.LessOrEqual(slot.RemainingCooldown, 0f);

        context.AddMonster(1, context.PartyModel.MemberList[0].Position + new Vector2(30f, 0f), Hp);
        context.CombatService.Step(context.RunRules.SimStep);
        Assert.AreEqual(1, context.CombatModel.GetFireCount("t_melee_test"));
    }

    [Test]
    public void SameDistancePicksFirstSpawned()
    {
        var context = Create("t_shot_test");
        var origin = context.PartyModel.MemberList[0].Position;
        var first = context.AddMonster(1, origin + new Vector2(0f, 60f), Hp);
        var second = context.AddMonster(2, origin + new Vector2(0f, -60f), Hp);

        context.StepCombat(10, context.RunRules.SimStep);

        Assert.AreEqual(Hp - Hit, first.Hp, 1e-3f);
        Assert.AreEqual(Hp, second.Hp);
    }

    [Test]
    public void DeadMemberDoesNotFire()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        context.CombatService.ResetRun();
        context.CombatService.PrepareWave();
        var members = context.PartyModel.MemberList;
        var bruno = members[0];
        bruno.Hp = 0f;
        for (var i = 1; i < members.Count; i++)
        {
            members[i].Position += new Vector2(1000f, 1000f);
        }

        var monster = context.AddMonster(1, bruno.Position + new Vector2(-30f, -30f), context.CombatData.Enemy.Hp);
        context.CombatService.Step(context.RunRules.SimStep);

        Assert.AreEqual(0, context.CombatModel.GetFireCount("sword"));
        Assert.AreEqual(context.CombatData.Enemy.Hp, monster.Hp);
    }

    [Test]
    public void StartItemsKillWithinFifteenSeconds()
    {
        // F1 "10초에 처치 1 이상"을 시작 아이템 발동으로 옮김. 적이 생성 거리(약 960)에서 단궁 사거리 336까지 걷는 데
        // 약 10초가 걸리고(속도 63), F1 기본 무기와 달리 사거리 336은 단궁 하나뿐이며 투사체가 날아가므로 15초로 본다
        var context = new TestRunContext();
        context.RunService.StartRun(1, context.RunRules.DefaultEnemyCountScale);

        context.StepSeconds(15f);

        Assert.GreaterOrEqual(context.RunModel.Kills.CurrentValue, 1);
        Assert.Greater(context.CombatModel.GetFireCount("sword") + context.CombatModel.GetFireCount("shortbow")
            + context.CombatModel.GetFireCount("firestaff"), 0);
    }

    private static TestRunContext Create(params string[] itemIds) => Create(new FakePlacementInfo(), itemIds);

    private static TestRunContext Create(FakePlacementInfo placement, params string[] itemIds)
    {
        return Create(placement, new[] { itemIds });
    }

    private static TestRunContext Create(FakePlacementInfo placement, params string[][] itemIdsPerMember)
    {
        var context = new TestRunContext(CombatTestItems.CreateRepository(
            CombatTestItems.Action("t_melee_test", "melee", 10f, 1f, 3f),
            CombatTestItems.Action("t_sweep_test", "sweep", 10f, 1f, 3f),
            CombatTestItems.Action("t_shot_test", "projectile", 10f, 5f, 3f),
            CombatTestItems.Action("t_blast_test", "explosion", 10f, 5f, 7f),
            CombatTestItems.Action("t_chain_test", "chain", 10f, 5f, 7f),
            CombatTestItems.Action("t_heal_test", "heal", 10f, 5f, 6f),
            CombatTestItems.Action("t_heal_area_test", "healArea", 10f, 5f, 6f),
            CombatTestItems.Action("t_cd_1", "melee", 1f, 1f, 3f),
            CombatTestItems.Action("t_cd_075", "melee", 1f, 0.75f, 3f)), placement);
        context.SetupParty(itemIdsPerMember);
        return context;
    }

    private static Vector2 Polar(float degrees, float distance)
    {
        var radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance;
    }
}
