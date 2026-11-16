using System.Linq;
using NUnit.Framework;

// F4 데이터 스키마 — 실제 데이터 읽기, 행동마다 몬스터 하나, 웨이브 네 칸
public class MonsterDataTests
{
    [Test]
    public void FiveMonstersOnePerBehavior()
    {
        var context = new TestRunContext();
        var monsters = context.MonsterRepository.MonsterDataList;

        CollectionAssert.AreEquivalent(new[] { "slime", "mushroom", "boar", "rock", "puffer" }, monsters.Select(m => m.Id).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { MonsterBehavior.Contact, MonsterBehavior.Projectile, MonsterBehavior.Charge, MonsterBehavior.Armored, MonsterBehavior.Bomb },
            monsters.Select(m => m.Behavior).ToArray());
        Assert.AreEqual(1f, context.MonsterRepository.Get("slime").Contact.Interval);
        Assert.AreEqual(1.8f, context.MonsterRepository.Get("rock").Contact.Interval);
        Assert.AreEqual(252f, context.MonsterRepository.Get("mushroom").Projectile.Range);
        Assert.AreEqual("#f2d3ff", context.MonsterRepository.Get("mushroom").Projectile.ShotColor);
        Assert.AreEqual(15, context.MonsterRepository.Get("boar").Charge.RallyMax);
        Assert.AreEqual(6, context.MonsterRepository.Get("puffer").Bomb.GroupSize);
        Assert.AreEqual(7f, context.MonsterRepository.Get("puffer").Bomb.SteerRate);
        // 중장갑이 가장 크다(AC18 구별)
        Assert.AreEqual(monsters.Max(m => m.Radius), context.MonsterRepository.Get("rock").Radius);
    }

    [Test]
    public void WavesHaveSpawnFields()
    {
        var context = new TestRunContext();
        var waves = context.WaveRepository.WaveDataList;

        // 대표 웨이브 1·3·6(기획서 C절). 나머지 7개는 콘텐츠 대기열이 채운다(MA1, 5c33b44..0c5d324)
        CollectionAssert.AreEqual(new[] { 98, 167, 270 }, new[] { waves[0].SpawnCount, waves[2].SpawnCount, waves[5].SpawnCount });
        foreach (var wave in waves)
        {
            Assert.Greater(wave.HpScale, 0f, wave.Id);
            Assert.Greater(wave.DamageScale, 0f, wave.Id);
            Assert.GreaterOrEqual(wave.TypeWeights.Count, 1, wave.Id);
            foreach (var entry in wave.TypeWeights)
            {
                Assert.IsNotNull(context.MonsterRepository.Get(entry.Monster), $"{wave.Id} {entry.Monster}");
                Assert.Greater(entry.Weight, 0f, $"{wave.Id} {entry.Monster}");
            }
        }

        Assert.AreEqual(0.924f, waves[2].HpScale, 1e-6f);
        Assert.AreEqual(1.5f, waves[5].DamageScale, 1e-6f);
    }

    [Test]
    public void SpawnRulesLoad()
    {
        var rules = new TestRunContext().MonsterRepository.SpawnRules;

        Assert.AreEqual(4f, rules.SpawnEndGap);
        Assert.AreEqual(1f, rules.InitialAttackDelay);
        CollectionAssert.AreEqual(new[] { "slime", "boar" }, rules.BurningTypeWeights.Select(w => w.Monster).ToArray());
        Assert.AreEqual(1f, rules.Heading.Front + rules.Heading.Left + rules.Heading.Right + rules.Heading.Back, 1e-6f);
        Assert.AreEqual(2, rules.Recycle.MaxPerCheck);
    }
}
