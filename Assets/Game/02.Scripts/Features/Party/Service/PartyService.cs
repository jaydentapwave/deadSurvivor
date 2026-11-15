using UnityEngine;

// 원정대 편성(S8)과 피해. 자리는 원정대 중심 둘레 고정 4자리(임시 규칙, 기획서 E절 — F2가 진형으로 바꾼다).
public class PartyService
{
    private readonly PartyModel _partyModel;
    private readonly CharacterRepository _characterRepository;
    private readonly PlaceholderCombatData _combatData;

    public int AliveCount
    {
        get
        {
            var count = 0;
            foreach (var member in _partyModel.MemberList)
            {
                if (member.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public PartyService(PartyModel partyModel, CharacterRepository characterRepository, PlaceholderCombatData combatData)
    {
        _partyModel = partyModel;
        _characterRepository = characterRepository;
        _combatData = combatData;
    }

    // 데이터 order 순서로 만들고 HP를 최대로, 장착 목록에 시작 아이템 하나
    public void CreateParty()
    {
        _partyModel.Clear();
        _partyModel.SetCenter(Vector2.zero);
        var characterDataList = _characterRepository.CharacterDataList;
        var spots = _combatData.MemberSpots;
        if (spots.Count < characterDataList.Count)
        {
            GameLogger.LogError($"[PartyService] memberSpots {spots.Count} < characters {characterDataList.Count}");
            return;
        }

        for (var i = 0; i < characterDataList.Count; i++)
        {
            var data = characterDataList[i];
            var member = new PartyMember(data.Id, data.Hp, data.Def, _partyModel.Center + spots[i]);
            member.Equip(data.StartItem);
            _partyModel.Add(member);
        }
    }

    public void ClearParty() => _partyModel.Clear();

    // 받는 피해 = 피해 × (1 − 유효 방어), 보호막이 먼저 흡수(기획서 B4)
    public void ApplyDamage(PartyMember member, float damage)
    {
        var received = damage * (1f - member.Defense);
        var absorbed = Mathf.Min(member.Shield, received);
        member.Shield -= absorbed;
        member.Hp = Mathf.Max(0f, member.Hp - (received - absorbed));
    }

    // 쓰러지지 않은 대원만 최대 HP까지(기획서 B4)
    public void Heal(PartyMember member, float amount)
    {
        if (!member.IsAlive)
        {
            return;
        }

        member.Hp = Mathf.Min(member.MaxHp, member.Hp + amount);
    }

    // 쓰러진 대원은 정비에서 장비를 바꿀 수 없다(기획서 E절). 장착 목록을 바꾸는 쪽(F5)이 먼저 부른다
    public bool CanChangeEquipment(int memberIndex) => _partyModel.MemberList[memberIndex].IsAlive;

    // 산 대원 중 가장 가까운 대원. 없으면 null
    public PartyMember FindNearestAlive(Vector2 position)
    {
        PartyMember nearest = null;
        var nearestSqr = float.MaxValue;
        foreach (var member in _partyModel.MemberList)
        {
            if (!member.IsAlive)
            {
                continue;
            }

            var sqr = (member.Position - position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = member;
            }
        }

        return nearest;
    }
}
