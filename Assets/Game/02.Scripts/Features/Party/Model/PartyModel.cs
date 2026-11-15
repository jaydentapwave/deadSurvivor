using System.Collections.Generic;
using UnityEngine;

// 원정대 상태. 1/30초마다 바뀌는 실체 값이라 R3로 감싸지 않고 Presenter가 프레임마다 읽는다.
// 쓰는 쪽: PartyService(만들기·피해·회복), CombatService(최대 HP·유효 방어·보호막, 판·웨이브 시작과 부활의 HP).
public class PartyModel
{
    private readonly List<PartyMember> _memberList = new();

    public IReadOnlyList<PartyMember> MemberList => _memberList;
    // 원정대 중심(바닥 평면). F1은 원점 고정, 이동은 F2
    public Vector2 Center { get; private set; }

    public void SetCenter(Vector2 center) => Center = center;

    public void Add(PartyMember member) => _memberList.Add(member);

    public void Clear() => _memberList.Clear();
}

public class PartyMember
{
    private readonly List<string> _equippedItemIdList = new();

    public string CharacterId { get; }
    public float MaxHp { get; set; }
    public float Hp { get; set; }
    // 유효 방어(기획서 B3)
    public float Defense { get; set; }
    public float Shield { get; set; }
    // 바닥 평면 좌표
    public Vector2 Position { get; set; }
    public IReadOnlyList<string> EquippedItemIdList => _equippedItemIdList;
    public bool IsAlive => Hp > 0f;

    public PartyMember(string characterId, float maxHp, float defense, Vector2 position)
    {
        CharacterId = characterId;
        MaxHp = maxHp;
        Hp = maxHp;
        Defense = defense;
        Position = position;
    }

    public void Equip(string itemId) => _equippedItemIdList.Add(itemId);
}
