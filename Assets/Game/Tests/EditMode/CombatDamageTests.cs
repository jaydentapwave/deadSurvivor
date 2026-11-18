using NUnit.Framework;

// AC11 — 받는 피해와 회복(기획서 B4)
public class CombatDamageTests
{
    [Test]
    public void DefenseReducesDamage()
    {
        var (context, member) = CreateMember(0.15f, 100f);

        context.PartyService.ApplyDamage(member, 10f);

        Assert.AreEqual(91.5f, member.Hp, 1e-4f);
    }

    [Test]
    public void ShieldAbsorbsFirst()
    {
        var (context, member) = CreateMember(0.15f, 100f);
        member.Shield = 5f;

        context.PartyService.ApplyDamage(member, 10f);

        Assert.AreEqual(0f, member.Shield, 1e-4f);
        Assert.AreEqual(96.5f, member.Hp, 1e-4f);
    }

    [Test]
    public void LethalDamageDownsMember()
    {
        var (context, member) = CreateMember(0.15f, 3f);

        context.PartyService.ApplyDamage(member, 10f);

        Assert.AreEqual(0f, member.Hp);
        Assert.IsFalse(member.IsAlive);
    }

    [Test]
    public void HealDoesNotExceedMaxHpAndSkipsDownedMember()
    {
        var (context, member) = CreateMember(0f, 95f);

        context.PartyService.Heal(member, 10f);
        Assert.AreEqual(member.MaxHp, member.Hp);

        member.Hp = 0f;
        context.PartyService.Heal(member, 10f);
        Assert.AreEqual(0f, member.Hp);
    }

    private static (TestRunContext, PartyMember) CreateMember(float defense, float hp)
    {
        var context = new TestRunContext();
        var member = new PartyMember("bruno", 100f, defense, UnityEngine.Vector2.zero) { Hp = hp };
        context.PartyModel.Add(member);
        return (context, member);
    }
}
