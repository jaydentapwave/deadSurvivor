using UnityEngine;

// 접촉·중장갑(기획서 A절): 반지름 + contactGap보다 멀면 다가가고, 붙어 있고 대기가 끝났으면 피해, 대기 = contact.interval.
// 중장갑은 수치만 다르다(MA7).
public class ContactBehavior : IMonsterBehavior
{
    // 접촉 거리에 멈춘 적의 거리 비교 여유(바닥 단위). 실수 오차로 접촉 직전에 계속 멈추지 않게(F1 규칙)
    private const float ContactTolerance = 0.001f;

    private readonly PartyService _partyService;

    public ContactBehavior(PartyService partyService)
    {
        _partyService = partyService;
    }

    public bool CanHandle(MonsterBehavior behavior) => behavior == MonsterBehavior.Contact || behavior == MonsterBehavior.Armored;

    public void Tick(Monster monster, PartyMember target, float deltaSeconds)
    {
        var data = monster.Data;
        var contactDistance = data.Radius + data.ContactGap;
        var toTarget = target.Position - monster.Position;
        var distance = toTarget.magnitude;
        if (distance > contactDistance + ContactTolerance)
        {
            monster.Position += toTarget / distance * Mathf.Min(data.Speed * deltaSeconds, distance - contactDistance);
        }
        else if (monster.AttackClock <= 0f)
        {
            _partyService.ApplyDamage(target, monster.Damage);
            monster.AttackClock = data.Contact.Interval;
        }
    }
}
