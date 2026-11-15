using System.Linq;
using NUnit.Framework;

// AC3 — 실제 데이터 파일(아이템 대표 8종, 캐릭터 sprite 칸·F1 값, 전투 규칙)
public class CombatDataLoadTests
{
    [Test]
    public void ItemsMatchPlanTable()
    {
        var repository = new ItemRepository(new ResourceService());
        var expected = new (string id, string name, ItemMode mode, int cells, float power, float interval, float range)[]
        {
            ("sword", "장검", ItemMode.Melee, 3, 22f, 1f, 3f),
            ("axe", "전투 도끼", ItemMode.Sweep, 4, 35f, 1.8f, 3.5f),
            ("shortbow", "단궁", ItemMode.Projectile, 3, 13f, 0.7f, 8f),
            ("firestaff", "화염 지팡이", ItemMode.Explosion, 4, 24f, 1.7f, 7f),
            ("wand", "번개 완드", ItemMode.Chain, 3, 17f, 1.4f, 7f),
            ("relic", "치유 성물", ItemMode.Heal, 4, 12f, 2f, 6f),
            ("censer", "성스러운 향로", ItemMode.HealArea, 4, 6f, 2.5f, 4f),
            ("ironshield", "철제 방패", ItemMode.Passive, 4, 0f, 0f, 0f),
        };

        Assert.AreEqual(expected.Length, repository.ItemDataList.Count());
        foreach (var row in expected)
        {
            var item = repository.Get(row.id);
            Assert.AreEqual(row.name, item.Name, row.id);
            Assert.AreEqual(row.mode, item.Mode, row.id);
            Assert.AreEqual(row.cells, item.Shape.Count, row.id);
            Assert.AreEqual(row.power, item.Power, 1e-6f, row.id);
            Assert.AreEqual(row.interval, item.Interval, 1e-6f, row.id);
            Assert.AreEqual(row.range, item.Range, 1e-6f, row.id);
            Assert.AreEqual("", item.Icon, row.id);
        }

        var shield = repository.Get("ironshield");
        Assert.AreEqual(ItemEffectKind.Def, shield.BaseEffect);
        Assert.AreEqual(0.08f, shield.BaseValue, 1e-6f);
        Assert.AreEqual(ItemEffectKind.Def, shield.BonusEffect);
        Assert.AreEqual(0.08f, shield.BonusValue, 1e-6f);
        Assert.AreEqual(ItemBonusTarget.Any, shield.BonusTarget);
        Assert.IsFalse(shield.BonusNeedsAdjacent);
        var axeCells = repository.Get("axe").Shape.Select(c => (c.X, c.Y)).ToArray();
        CollectionAssert.AreEqual(new[] { (0, 0), (0, 1), (0, 2), (1, 2) }, axeCells);

        var modes = repository.ItemDataList.Select(item => item.Mode).Distinct().Count();
        Assert.AreEqual(8, modes, "all 8 modes");
    }

    [Test]
    public void CharactersKeepF1ValuesAndHaveSpriteField()
    {
        var repository = new CharacterRepository(new ResourceService());
        var expected = new (string id, string name, string role, int hp, float atk, float def, string startItem)[]
        {
            ("bruno", "브루노", "전사", 400, 0.8f, 0.15f, "sword"),
            ("lena", "레나", "궁수", 110, 1.2f, 0f, "shortbow"),
            ("mira", "미라", "마법사", 140, 1f, 0.05f, "firestaff"),
            ("bolt", "볼트", "사제", 120, 1.1f, 0.05f, "relic"),
        };

        Assert.AreEqual(expected.Length, repository.CharacterDataList.Count);
        foreach (var row in expected)
        {
            var character = repository.Get(row.id);
            Assert.AreEqual(row.name, character.Name);
            Assert.AreEqual(row.role, character.Role);
            Assert.AreEqual(row.hp, character.Hp);
            Assert.AreEqual(row.atk, character.Atk, 1e-6f);
            Assert.AreEqual(row.def, character.Def, 1e-6f);
            Assert.AreEqual(row.startItem, character.StartItem);
            Assert.AreEqual("", character.Sprite, row.id);
        }
    }

    [Test]
    public void CombatRulesMatchSchemaTable()
    {
        var rules = new CombatRulesRepository(new ResourceService()).Rules;

        Assert.AreEqual(42f, rules.RangeUnit);
        Assert.AreEqual(0.8f, rules.DefCap, 1e-6f);
        Assert.AreEqual(1f, rules.HasteCap);
        Assert.AreEqual(3f, rules.HealPeriod);
        Assert.AreEqual(8f, rules.ShieldPeriod);
        Assert.AreEqual(50f, rules.MeleeHalfAngle);
        Assert.AreEqual(90f, rules.SweepHalfAngle);
        Assert.AreEqual(440f, rules.ProjectileSpeed);
        Assert.AreEqual(0.12f, rules.ProjectileSpread, 1e-6f);
        Assert.AreEqual(75f, rules.ExplosionRadius);
        Assert.AreEqual(5f, rules.ExplosionTriggerGap);
        Assert.AreEqual(3, rules.ChainBaseHits);
        Assert.AreEqual(150f, rules.ChainHopRange);
        Assert.AreEqual(0.18f, rules.FiredSeconds, 1e-6f);
    }
}
