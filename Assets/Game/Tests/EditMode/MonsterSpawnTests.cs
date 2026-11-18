using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// F4 AC8(무리)·AC9~AC12 — 정규·버닝 생성, 방향, 생성 거리, 같은 시드
public class MonsterSpawnTests
{
    private const int DirectionSamples = 10000;
    private const float ShareTolerance = 0.02f;

    [TestCase(2400f, true)]
    [TestCase(0f, false)]
    public void B1B2SpawnClipsBoundaryFixtureButPreservesCentralDistance(float centerX, bool isClipped)
    {
        var context = new TestRunContext();
        var wave = context.BeginWaveOnly(1, 9);
        var size = PixelScaleMath.OrthographicSize(1080, 48, 2);
        var extent = TiltedViewMath.Footprint(size, 16f / 9f, .5f) / context.RunRules.UnitScale;
        context.MonsterService.SetViewHalfExtent(extent);
        context.PartyModel.SetCenter(new Vector2(centerX, 0f));
        typeof(MonsterSpawnService).GetField("_positionRandom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(context.MonsterSpawnService, new FixedPositionRandom());
        context.MonsterService.TickSpawn(context.RunRules.SimStep, context.RunRules.SimStep, false, wave.Duration * .5f);
        var position = context.MonsterModel.MonsterList[0].Position;
        var limit = context.RunRules.FieldCells / (2f * context.RunRules.UnitScale);
        Assert.LessOrEqual(Mathf.Abs(position.x) + Mathf.Abs(position.y), limit + .001f);
        Assert.AreEqual(isClipped ? limit : extent.magnitude + context.MonsterRepository.SpawnRules.SpawnMargin, position.x, .001f);
        Assert.AreEqual(0f, position.y, .001f);
    }

    private class FixedPositionRandom : System.Random
    {
        public override double NextDouble() => 0d;
    }

    [Test]
    public void RealDataCountsTypesAndFirstWaves()
    {
        var probe = new TestRunContext();
        var waveCount = probe.WaveRepository.WaveDataList.Count;
        var firstSpawnedWave = new Dictionary<MonsterBehavior, int>();
        var firstDataWave = new Dictionary<MonsterBehavior, int>();
        for (var wave = 1; wave <= waveCount; wave++)
        {
            var context = new TestRunContext();
            var waveData = context.BeginWaveOnly(wave, 11);
            var lastRegularSpawn = 0f;
            var lastCount = 0;
            context.SpawnWholeWave(waveData, elapsed =>
            {
                if (context.MonsterModel.SpawnedCount > lastCount)
                {
                    lastCount = context.MonsterModel.SpawnedCount;
                    lastRegularSpawn = elapsed;
                }
            });

            Assert.AreEqual(Mathf.RoundToInt(waveData.SpawnCount * 1f), context.MonsterModel.SpawnedCount, waveData.Id);
            Assert.LessOrEqual(lastRegularSpawn, waveData.Duration - 4f + context.RunRules.SimStep + 1e-3f, waveData.Id);

            var weightedSet = new HashSet<MonsterBehavior>();
            foreach (var entry in waveData.TypeWeights)
            {
                weightedSet.Add(context.MonsterRepository.Get(entry.Monster).Behavior);
            }

            var total = 0;
            foreach (MonsterBehavior behavior in Enum.GetValues(typeof(MonsterBehavior)))
            {
                var count = context.MonsterModel.SpawnCountOf(behavior);
                total += count;
                if (!weightedSet.Contains(behavior))
                {
                    Assert.AreEqual(0, count, $"{waveData.Id} {behavior}");
                }

                if (count > 0 && !firstSpawnedWave.ContainsKey(behavior))
                {
                    firstSpawnedWave[behavior] = wave;
                }

                if (weightedSet.Contains(behavior) && !firstDataWave.ContainsKey(behavior))
                {
                    firstDataWave[behavior] = wave;
                }
            }

            Assert.AreEqual(context.MonsterModel.SpawnedCount, total, waveData.Id);
        }

        CollectionAssert.AreEquivalent(firstDataWave, firstSpawnedWave);
    }

    [Test]
    public void WaveScalesApplied()
    {
        var context = new TestRunContext();
        var waveData = context.BeginWaveOnly(3, 5);
        context.SpawnWholeWave(waveData);

        Assert.Greater(context.MonsterModel.MonsterList.Count, 0);
        foreach (var monster in context.MonsterModel.MonsterList)
        {
            Assert.AreEqual(monster.Data.Hp * waveData.HpScale, monster.MaxHp, 1e-4f, monster.Data.Id);
            Assert.AreEqual(monster.Data.Damage * waveData.DamageScale, monster.Damage, 1e-4f, monster.Data.Id);
            Assert.AreEqual(context.MonsterRepository.SpawnRules.InitialAttackDelay, monster.AttackClock);
        }
    }

    [Test]
    public void ZeroScaleSpawnsNothing()
    {
        var context = new TestRunContext();
        context.SpawnWholeWave(context.BeginWaveOnly(6, 5, 0f));

        Assert.AreEqual(0, context.MonsterModel.MonsterList.Count);
        Assert.AreEqual(0, context.MonsterModel.BurningSpawnedCount);
    }

    [Test]
    public void SpawnDistanceByVisibleArea()
    {
        // 기준 높이 360px(PPU 48)에서 16:9·21:9·4:3
        foreach (var aspect in new[] { 16f / 9f, 21f / 9f, 4f / 3f })
        {
            var context = new TestRunContext();
            var halfHeight = 360f / Const.PixelsPerUnit / 2f;
            var extent = TiltedViewMath.Footprint(halfHeight, aspect, .5f) / context.RunRules.UnitScale;
            context.MonsterService.SetViewHalfExtent(extent);
            var waveData = context.BeginWaveOnly(1, 9);
            context.MonsterService.TickSpawn(context.RunRules.SimStep, context.RunRules.SimStep, false, waveData.Duration * 0.5f);

            Assert.AreEqual(1, context.MonsterModel.MonsterList.Count);
            var distance = Vector2.Distance(context.MonsterModel.MonsterList[0].Position, context.PartyModel.Center);
            Assert.AreEqual(extent.magnitude + 70f, distance, 1e-2f, $"aspect {aspect}");
        }
    }

    [Test]
    public void HeadingUniformWhenStill()
    {
        var context = new TestRunContext();
        context.BeginWaveOnly(1, 21);
        var bucketCounts = new int[8];
        for (var i = 0; i < DirectionSamples; i++)
        {
            var angle = Mathf.Repeat(context.MonsterSpawnService.PickHeadingAngle(), Mathf.PI * 2f);
            bucketCounts[Mathf.Min(7, Mathf.FloorToInt(angle / (Mathf.PI / 4f)))]++;
        }

        foreach (var count in bucketCounts)
        {
            Assert.AreEqual(0.125f, count / (float)DirectionSamples, ShareTolerance);
        }
    }

    [Test]
    public void HeadingFrontBiasedWhenMoving()
    {
        var context = new TestRunContext();
        context.BeginWaveOnly(1, 23);
        var heading = new Vector2(0.6f, 0.8f);
        context.MonsterModel.PartyHeading = heading;
        int front = 0, left = 0, right = 0, back = 0;
        for (var i = 0; i < DirectionSamples; i++)
        {
            var angle = context.MonsterSpawnService.PickHeadingAngle();
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var relative = Vector2.SignedAngle(heading, direction);
            if (Mathf.Abs(relative) < 45f)
            {
                front++;
            }
            else if (relative >= 45f && relative < 135f)
            {
                left++;
            }
            else if (relative <= -45f && relative > -135f)
            {
                right++;
            }
            else
            {
                back++;
            }
        }

        Assert.AreEqual(0.5f, front / (float)DirectionSamples, ShareTolerance);
        Assert.AreEqual(0.15f, left / (float)DirectionSamples, ShareTolerance);
        Assert.AreEqual(0.15f, right / (float)DirectionSamples, ShareTolerance);
        Assert.AreEqual(0.2f, back / (float)DirectionSamples, ShareTolerance);
    }

    [Test]
    public void BombsSpawnInGroups()
    {
        var context = new TestRunContext();
        var waveData = JsonUtility.FromJson<WaveData>(
            "{\"id\":\"bombs\",\"order\":1,\"duration\":30,\"hasBurningTime\":false,\"spawnCount\":12,\"hpScale\":1,\"damageScale\":1,"
            + "\"typeWeights\":[{\"monster\":\"puffer\",\"weight\":1}]}");
        context.PartyService.CreateParty();
        context.MonsterService.BeginRun(5);
        context.MonsterService.BeginWave(waveData, 1f);
        context.SpawnWholeWave(waveData);

        var spread = context.MonsterRepository.Get("puffer").Bomb.GroupSpread;
        var monsters = context.MonsterModel.MonsterList;
        Assert.AreEqual(12, monsters.Count);
        for (var group = 0; group < 2; group++)
        {
            var first = AngleOf(context, monsters[group * 6]);
            for (var i = 1; i < 6; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(first, AngleOf(context, monsters[group * 6 + i]))), spread * Mathf.Rad2Deg + 1e-3f,
                    $"group {group} member {i}");
            }
        }

        Assert.AreEqual(12, context.MonsterModel.BombGroupCount);
    }

    [Test]
    public void BurningExtrasOnlyInWindow()
    {
        for (var wave = 1; wave <= 5; wave++)
        {
            var still = new TestRunContext();
            still.SpawnWholeWave(still.BeginWaveOnly(wave, 3));
            Assert.AreEqual(0, still.MonsterModel.BurningSpawnedCount, $"wave {wave}");
        }

        var context = new TestRunContext();
        var waveData = context.BeginWaveOnly(6, 3);
        var burningStart = waveData.Duration * context.RunRules.BurningStartRatio;
        var lastBurning = 0;
        context.SpawnWholeWave(waveData, elapsed =>
        {
            if (context.MonsterModel.BurningSpawnedCount > lastBurning)
            {
                lastBurning = context.MonsterModel.BurningSpawnedCount;
                Assert.GreaterOrEqual(elapsed, burningStart - 1e-4f);
                // 창 끝을 넘는 걸음은 창 안에 들었어야 할 남은 몫을 낸다
                Assert.Less(elapsed, burningStart + context.RunRules.BurningSeconds + context.RunRules.SimStep + 1e-4f);
            }
        });

        var rules = context.MonsterRepository.SpawnRules;
        var interval = Mathf.Max(0.01f, Mathf.Max(0f, waveData.Duration - rules.SpawnEndGap) / (waveData.SpawnCount - 1) * rules.BurningIntervalScale);
        Assert.AreEqual(Mathf.CeilToInt(context.RunRules.BurningSeconds / interval), context.MonsterModel.BurningSpawnedCount);
        Assert.AreEqual(waveData.SpawnCount, context.MonsterModel.SpawnedCount);
    }

    [Test]
    public void BurningExtrasAlternateSidesAndTypes()
    {
        var context = new TestRunContext();
        var waveData = context.BeginWaveOnly(6, 3);
        var burningMonsterList = new List<Monster>();
        var lastBurning = 0;
        context.SpawnWholeWave(waveData, _ =>
        {
            // 한 걸음 안에서 정규 생성이 먼저, 버닝 추가가 뒤에 목록에 들어간다
            var added = context.MonsterModel.BurningSpawnedCount - lastBurning;
            var monsters = context.MonsterModel.MonsterList;
            for (var i = monsters.Count - added; i < monsters.Count; i++)
            {
                burningMonsterList.Add(monsters[i]);
            }

            lastBurning = context.MonsterModel.BurningSpawnedCount;
        });

        var halfSpread = context.MonsterRepository.SpawnRules.BurningSpread * 0.5f * Mathf.Rad2Deg + 1e-3f;
        var baseAngle = context.MonsterModel.BurningAngle * Mathf.Rad2Deg;
        var typeSet = new HashSet<string>();
        for (var i = 0; i < burningMonsterList.Count; i++)
        {
            var side = baseAngle + (i % 2) * 180f;
            Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(side, AngleOf(context, burningMonsterList[i]))), halfSpread, $"extra {i}");
            typeSet.Add(burningMonsterList[i].Data.Id);
        }

        Assert.Greater(burningMonsterList.Count, 0);
        CollectionAssert.AreEquivalent(new[] { "slime", "boar" }, typeSet);
    }

    [Test]
    public void SameSeedSameSpawnSequence()
    {
        // ① 실제 판(RunService.AdvanceFrame): 1/60초 60번과 1초 1번이 웨이브마다 같은 생성 셈. 버닝 웨이브(6)까지 간다
        var bySixtieth = new TestRunContext();
        var bySecond = new TestRunContext();
        bySixtieth.RunService.StartRun(7, 0.1f);
        bySecond.RunService.StartRun(7, 0.1f);
        var sixtiethCountList = new List<string>();
        var secondCountList = new List<string>();
        for (var second = 0; second < 300; second++)
        {
            for (var frame = 0; frame < 60; frame++)
            {
                bySixtieth.RunService.AdvanceFrame(1f / 60f);
            }

            bySecond.RunService.AdvanceFrame(1f);
            sixtiethCountList.Add(CountLine(bySixtieth));
            secondCountList.Add(CountLine(bySecond));
            AdvancePrep(bySixtieth);
            AdvancePrep(bySecond);
        }

        CollectionAssert.AreEqual(secondCountList, sixtiethCountList);
        Assert.GreaterOrEqual(bySecond.RunModel.Wave.CurrentValue, 6, "버닝 웨이브까지 가야 한다");

        // ② 생성만: 원정대가 움직이든 멈추든 웨이브별 생성 수와 유형 순서가 같다(자리 난수만 더 쓰인다)
        for (var wave = 1; wave <= bySecond.WaveRepository.WaveDataList.Count; wave++)
        {
            CollectionAssert.AreEqual(SpawnTypes(wave, Vector2.zero), SpawnTypes(wave, new Vector2(60f, 20f)), $"wave {wave}");
        }
    }

    private static string CountLine(TestRunContext context)
    {
        var model = context.MonsterModel;
        return $"{context.RunModel.Wave.CurrentValue}:{model.SpawnedCount}:{model.BurningSpawnedCount}:"
            + $"{model.SpawnCountOf(MonsterBehavior.Contact)}:{model.SpawnCountOf(MonsterBehavior.Projectile)}:"
            + $"{model.SpawnCountOf(MonsterBehavior.Charge)}:{model.SpawnCountOf(MonsterBehavior.Armored)}:{model.SpawnCountOf(MonsterBehavior.Bomb)}";
    }

    private static void AdvancePrep(TestRunContext context)
    {
        if (context.RunModel.Phase.CurrentValue == RunPhase.Prep)
        {
            context.RunService.NextWave();
        }
    }

    // 웨이브 하나를 생성만 돌린 유형 순서. velocity ≠ 0이면 걸음마다 원정대를 움직이고 이동 방향을 갱신한다
    private static List<string> SpawnTypes(int wave, Vector2 velocity)
    {
        var context = new TestRunContext();
        var waveData = context.BeginWaveOnly(wave, 13);
        var burningStart = waveData.Duration * context.RunRules.BurningStartRatio;
        var remaining = waveData.Duration;
        while (remaining > 0f)
        {
            var delta = Mathf.Min(context.RunRules.SimStep, remaining);
            remaining -= delta;
            var elapsed = waveData.Duration - remaining;
            context.PartyModel.SetCenter(context.PartyModel.Center + velocity * delta);
            context.MonsterRecycleService.UpdatePartyHeading();
            var isBurningTime = waveData.HasBurningTime && elapsed >= burningStart && elapsed < burningStart + context.RunRules.BurningSeconds;
            context.MonsterService.TickSpawn(delta, elapsed, isBurningTime, burningStart);
        }

        var typeList = new List<string>();
        foreach (var monster in context.MonsterModel.MonsterList)
        {
            typeList.Add(monster.Data.Id);
        }

        return typeList;
    }

    // 원정대 중심에서 본 몬스터 방향(도)
    private static float AngleOf(TestRunContext context, Monster monster)
    {
        var offset = monster.Position - context.PartyModel.Center;
        return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }
}
