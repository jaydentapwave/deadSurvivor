using UnityEngine;

// 임시 기본 무기(기획서 E절). F3이 장착 아이템의 발동 방식·쿨타임으로 바꾼다.
// 살아 있는 대원마다 interval마다 사거리 안 가장 가까운 산 적에게 즉시 damage × atk.
public class BasicWeaponService
{
    private readonly PartyModel _partyModel;
    private readonly MonsterModel _monsterModel;
    private readonly CharacterRepository _characterRepository;
    private readonly PlaceholderCombatData _combatData;

    public BasicWeaponService(PartyModel partyModel, MonsterModel monsterModel, CharacterRepository characterRepository,
        PlaceholderCombatData combatData)
    {
        _partyModel = partyModel;
        _monsterModel = monsterModel;
        _characterRepository = characterRepository;
        _combatData = combatData;
    }

    public void ResetClocks()
    {
        foreach (var member in _partyModel.MemberList)
        {
            member.WeaponClock = 0f;
        }
    }

    public void Tick(float deltaSeconds)
    {
        var weapon = _combatData.Weapon;
        foreach (var member in _partyModel.MemberList)
        {
            if (!member.IsAlive)
            {
                continue;
            }

            // 쿨이 남았을 때만 줄인다. 이번 걸음에 0을 넘어선 몫은 다음 쿨에서 빼서 간격을 정확히 지킨다
            if (member.WeaponClock > 0f)
            {
                member.WeaponClock -= deltaSeconds;
                if (member.WeaponClock > 0f)
                {
                    continue;
                }
            }

            var target = FindNearestAliveInRange(member.Position, weapon.Range);
            if (target == null)
            {
                // 표적이 없으면 준비된 채로 기다린다(넘어선 몫을 버려 표적이 생겼을 때 간격이 줄지 않게)
                member.WeaponClock = 0f;
                continue;
            }

            target.Hp -= weapon.Damage * _characterRepository.Get(member.CharacterId).Atk;
            member.WeaponClock += weapon.Interval;
        }
    }

    private Monster FindNearestAliveInRange(Vector2 position, float range)
    {
        Monster nearest = null;
        var nearestSqr = range * range;
        foreach (var monster in _monsterModel.MonsterList)
        {
            if (!monster.IsAlive)
            {
                continue;
            }

            var sqr = (monster.Position - position).sqrMagnitude;
            if (sqr <= nearestSqr)
            {
                nearestSqr = sqr;
                nearest = monster;
            }
        }

        return nearest;
    }
}
