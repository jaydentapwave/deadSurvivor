using UnityEngine;

// 원정대 편성(S8)과 피해. 대원 자리·원정대 중심은 PartyMoveService가 정한다(F2).
public class PartyService
{
    private readonly PartyModel _partyModel;
    private readonly CharacterRepository _characterRepository;

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

    public PartyService(PartyModel partyModel, CharacterRepository characterRepository)
    {
        _partyModel = partyModel;
        _characterRepository = characterRepository;
    }

    // 데이터 order 순서로 만들고 HP를 최대로, 장착 목록에 시작 아이템 하나. 좌표는 PartyMoveService.ResetForRun이 정한다
    public void CreateParty()
    {
        _partyModel.Clear();
        var characterDataList = _characterRepository.CharacterDataList;
        for (var i = 0; i < characterDataList.Count; i++)
        {
            var data = characterDataList[i];
            var member = new PartyMember(data.Id, data.Hp, Vector2.zero);
            member.Equip(data.StartItem);
            _partyModel.Add(member);
        }
    }

    public void ClearParty() => _partyModel.Clear();

    // 받는 피해 = 피해 × (1 − 방어)
    public void ApplyDamage(PartyMember member, float damage)
    {
        var def = _characterRepository.Get(member.CharacterId).Def;
        member.Hp = Mathf.Max(0f, member.Hp - damage * (1f - def));
    }

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
