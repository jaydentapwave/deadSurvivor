using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// F4 AC13 — 멀어진 적 재배치
public class MonsterRecycleTests
{
    [Test]
    public void FarBehindMovedAfterHold()
    {
        var context = new TestRunContext();
        context.BeginWaveOnly(1, 3);
        var extent = context.MonsterModel.ViewHalfExtent;
        var recycle = context.MonsterRepository.SpawnRules.Recycle;
        // 원정대는 +x로 움직인다. behind는 뒤(−x) 멀리, ahead는 앞(+x) 멀리
        var behind = context.CreateMonster(1, "slime", new Vector2(-(extent.x + recycle.OutsideMargin + 100f), 0f));
        var ahead = context.CreateMonster(2, "slime", new Vector2(extent.x + recycle.OutsideMargin + 100f, 0f));
        context.MonsterModel.Add(behind);
        context.MonsterModel.Add(ahead);
        var behindStart = behind.Position;
        var aheadStart = ahead.Position;
        var dt = context.RunRules.SimStep;

        var movedAt = -1f;
        for (var i = 1; i <= Mathf.RoundToInt(4f / dt); i++)
        {
            context.PartyModel.SetCenter(context.PartyModel.Center + Vector2.right * 0.001f);
            context.MonsterRecycleService.UpdatePartyHeading();
            context.MonsterRecycleService.Tick(dt);
            if (movedAt < 0f && behind.Position != behindStart)
            {
                movedAt = i * dt;
            }
        }

        Assert.AreEqual(recycle.HoldSeconds, movedAt, dt + 1e-3f);
        Assert.AreEqual(aheadStart, ahead.Position);
        var offset = behind.Position - context.PartyModel.Center;
        var isOnEdge = Mathf.Abs(Mathf.Abs(offset.x) - (extent.x + recycle.PlaceMargin)) < 0.05f
            || Mathf.Abs(Mathf.Abs(offset.y) - (extent.y + recycle.PlaceMargin)) < 0.05f;
        Assert.IsTrue(isOnEdge, $"offset {offset}");
        Assert.LessOrEqual(Mathf.Abs(offset.x), extent.x + recycle.PlaceMargin + 0.05f);
        Assert.LessOrEqual(Mathf.Abs(offset.y), extent.y + recycle.PlaceMargin + 0.05f);
        Assert.GreaterOrEqual(behind.AttackClock, recycle.MinAttackDelay - dt - 1e-3f);
    }

    [Test]
    public void AtMostTwoPerInterval()
    {
        var context = new TestRunContext();
        context.BeginWaveOnly(1, 3);
        var extent = context.MonsterModel.ViewHalfExtent;
        var recycle = context.MonsterRepository.SpawnRules.Recycle;
        var startList = new List<Vector2>();
        for (var i = 0; i < 5; i++)
        {
            var monster = context.CreateMonster(i + 1, "slime", new Vector2(0f, extent.y + recycle.OutsideMargin + 100f + i));
            context.MonsterModel.Add(monster);
            startList.Add(monster.Position);
        }

        var dt = context.RunRules.SimStep;
        var movedCountList = new List<int>();
        for (var i = 1; i <= Mathf.RoundToInt(4.2f / dt); i++)
        {
            context.MonsterRecycleService.UpdatePartyHeading();
            context.MonsterRecycleService.Tick(dt);
            var moved = 0;
            for (var m = 0; m < 5; m++)
            {
                if (context.MonsterModel.MonsterList[m].Position != startList[m])
                {
                    moved++;
                }
            }

            movedCountList.Add(moved);
        }

        // 걸음마다 옮긴 수의 증가가 2 이하이고, 옮긴 걸음 사이는 checkInterval 이상
        var lastMoveStep = -1000;
        for (var i = 1; i < movedCountList.Count; i++)
        {
            var increase = movedCountList[i] - movedCountList[i - 1];
            Assert.LessOrEqual(increase, recycle.MaxPerCheck);
            if (increase > 0)
            {
                Assert.GreaterOrEqual((i - lastMoveStep) * dt, recycle.CheckInterval - 1e-3f);
                lastMoveStep = i;
            }
        }

        Assert.AreEqual(5, movedCountList[movedCountList.Count - 1]);
        Assert.AreEqual(2, movedCountList[Mathf.RoundToInt(3.1f / dt)]);
    }

    [Test]
    public void BusyPhasesNotMoved()
    {
        var context = new TestRunContext();
        context.BeginWaveOnly(1, 3);
        var extent = context.MonsterModel.ViewHalfExtent;
        var recycle = context.MonsterRepository.SpawnRules.Recycle;
        var phases = new[] { MonsterPhase.Windup, MonsterPhase.Dash, MonsterPhase.Swarm, MonsterPhase.Fuse };
        for (var i = 0; i < phases.Length; i++)
        {
            var monster = context.CreateMonster(i + 1, "boar", new Vector2(extent.x + recycle.OutsideMargin + 100f, i * 10f));
            monster.Phase = phases[i];
            context.MonsterModel.Add(monster);
        }

        for (var i = 0; i < Mathf.RoundToInt(5f / context.RunRules.SimStep); i++)
        {
            context.MonsterRecycleService.UpdatePartyHeading();
            context.MonsterRecycleService.Tick(context.RunRules.SimStep);
        }

        for (var i = 0; i < phases.Length; i++)
        {
            Assert.AreEqual(new Vector2(extent.x + recycle.OutsideMargin + 100f, i * 10f), context.MonsterModel.MonsterList[i].Position,
                phases[i].ToString());
        }
    }
}
