using System.Collections.Generic;
using UnityEngine;

// 살아 있는 적 목록. 쓰는 쪽: MonsterService(생성·이동·제거), BasicWeaponService(HP).
public class MonsterModel
{
    private readonly List<Monster> _monsterList = new();

    public IReadOnlyList<Monster> MonsterList => _monsterList;

    public void Add(Monster monster) => _monsterList.Add(monster);

    // 쓰러진 적을 빼고 뺀 수를 돌려준다
    public int RemoveDead() => _monsterList.RemoveAll(monster => !monster.IsAlive);

    public void Clear() => _monsterList.Clear();
}

public class Monster
{
    public int Id { get; }
    // 바닥 평면 좌표
    public Vector2 Position { get; set; }
    public float Hp { get; set; }
    public float AttackClock { get; set; }
    public bool IsAlive => Hp > 0f;

    public Monster(int id, Vector2 position, float hp)
    {
        Id = id;
        Position = position;
        Hp = hp;
    }
}
