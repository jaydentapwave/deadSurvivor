using System.Collections.Generic;
using UnityEngine;

// 원정대 상태. 1/30초마다 바뀌는 실체 값이라 R3로 감싸지 않고 Presenter가 프레임마다 읽는다.
// 쓰는 쪽: PartyService(만들기·피해), BasicWeaponService(무기 쿨 시계), PartyMoveService(중심·대원 좌표, F2).
public class PartyModel
{
    private readonly List<PartyMember> _memberList = new();

    public IReadOnlyList<PartyMember> MemberList => _memberList;
    // 원정대 중심(바닥 평면). 쓰는 쪽은 PartyMoveService뿐이다(F2)
    public Vector2 Center { get; private set; }

    public void SetCenter(Vector2 center) => Center = center;

    public void Add(PartyMember member) => _memberList.Add(member);

    public void Clear() => _memberList.Clear();
}

public class PartyMember
{
    private readonly List<string> _equippedItemIdList = new();

    public string CharacterId { get; }
    public float MaxHp { get; }
    public float Hp { get; set; }
    // 바닥 평면 좌표
    public Vector2 Position { get; set; }
    public float WeaponClock { get; set; }
    public IReadOnlyList<string> EquippedItemIdList => _equippedItemIdList;
    public bool IsAlive => Hp > 0f;

    public PartyMember(string characterId, float maxHp, Vector2 position)
    {
        CharacterId = characterId;
        MaxHp = maxHp;
        Hp = maxHp;
        Position = position;
    }

    public void Equip(string itemId) => _equippedItemIdList.Add(itemId);
}
