using System.Linq;
using NUnit.Framework;

// 데이터 파일이 게임 형식으로 읽히는지(기획서 데이터 스키마)
public class DataLoadTests
{
    [Test]
    public void CharactersLoadInOrderWithStartItems()
    {
        var context = new TestRunContext();
        var characters = context.CharacterRepository.CharacterDataList;

        CollectionAssert.AreEqual(new[] { "bruno", "lena", "mira", "bolt" }, characters.Select(c => c.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "브루노", "레나", "미라", "볼트" }, characters.Select(c => c.Name).ToArray());
        CollectionAssert.AreEqual(new[] { 400, 110, 140, 120 }, characters.Select(c => c.Hp).ToArray());
        foreach (var character in characters)
        {
            Assert.IsNotNull(context.ItemRepository.Get(character.StartItem), character.Id);
        }

        Assert.AreEqual("화염 지팡이", context.ItemRepository.Get("firestaff").Name);
    }

    [Test]
    public void WavesLoadInOrder()
    {
        var context = new TestRunContext();
        var waves = context.WaveRepository.WaveDataList;

        CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToArray(), waves.Select(w => w.Order).ToArray());
        Assert.AreEqual(435f, waves.Sum(w => w.Duration));
        CollectionAssert.AreEqual(new[] { false, false, false, false, false, true, true, true, true, true },
            waves.Select(w => w.HasBurningTime).ToArray());
    }

    [Test]
    public void RunRulesAndPlaceholderCombatLoad()
    {
        var context = new TestRunContext();

        Assert.AreEqual(1f / 30f, context.RunRules.SimStep, 1e-6f);
        CollectionAssert.AreEqual(new[] { 1f, 2f }, context.RunRules.SpeedOptions.ToArray());
        Assert.AreEqual(64, context.RunRules.FieldCells);
        Assert.AreEqual(4, context.CombatData.MemberSpots.Count);
        Assert.AreEqual(336f, context.CombatData.Weapon.Range);
        // F4: 임시 적 칸은 지워졌다. 같은 값이 생성 규칙(70, 0.5)과 웨이브 파일(98)에 있다
        Assert.AreEqual(70f, context.MonsterRepository.SpawnRules.SpawnMargin);
        Assert.AreEqual(0.5f, context.MonsterRepository.SpawnRules.BurningIntervalScale);
        Assert.AreEqual(98, context.WaveRepository.WaveDataList[0].SpawnCount);
    }
}
