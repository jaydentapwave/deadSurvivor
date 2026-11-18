using System;
using System.Collections.Generic;
using UnityEngine;

// 살아 있는 적·적 투사체와 이번 웨이브 셈, 생성·재배치 상태(기획서 데이터 스키마 "실행 중 상태", 저장 안 함).
// 1/30초마다 바뀌는 값이라 R3로 감싸지 않고 Presenter·진입점이 프레임마다 읽는다.
// 쓰는 쪽: MonsterService와 그 아래 생성·재배치·행동·투사체 서비스, CombatService(HP).
public class MonsterModel
{
    private static readonly int BehaviorCount = Enum.GetValues(typeof(MonsterBehavior)).Length;

    private readonly List<Monster> _monsterList = new();
    private readonly List<EnemyShot> _enemyShotList = new();
    private readonly int[] _behaviorSpawnCounts = new int[BehaviorCount];

    public IReadOnlyList<Monster> MonsterList => _monsterList;
    public IReadOnlyList<EnemyShot> EnemyShotList => _enemyShotList;
    // 카메라가 보는 바닥 영역의 반 크기(바닥 단위)
    public Vector2 ViewHalfExtent { get; set; }

    // 이번 웨이브 셈. Clear()는 지우지 않고 ResetWaveCounts()(다음 웨이브 시작)만 지운다
    public int SpawnedCount { get; set; }
    public int BurningSpawnedCount { get; set; }

    // 생성 상태
    public int SpawnTarget { get; set; }
    public float SpawnInterval { get; set; }
    public float SpawnClock { get; set; }
    public float BurningAngle { get; set; }
    public int BombGroupCount { get; set; }
    public float BombGroupAngle { get; set; }

    // 재배치 상태. PartyHeading = 지난 걸음 대비 원정대 중심 변화, 멈춰 있으면 0
    public float RecycleClock { get; set; }
    public Vector2 LastPartyCenter { get; set; }
    public Vector2 PartyHeading { get; set; }
    public bool IsPartyMoving => PartyHeading != Vector2.zero;

    public int SpawnCountOf(MonsterBehavior behavior) => _behaviorSpawnCounts[(int)behavior];

    public void AddSpawnCount(MonsterBehavior behavior) => _behaviorSpawnCounts[(int)behavior]++;

    public void ResetWaveCounts()
    {
        SpawnedCount = 0;
        BurningSpawnedCount = 0;
        Array.Clear(_behaviorSpawnCounts, 0, _behaviorSpawnCounts.Length);
    }

    public void Add(Monster monster) => _monsterList.Add(monster);

    public void AddEnemyShot(EnemyShot shot) => _enemyShotList.Add(shot);

    // 쓰러진 적을 빼고 뺀 수를 돌려준다
    public int RemoveDead() => _monsterList.RemoveAll(monster => !monster.IsAlive);

    public void RemoveDeadShots() => _enemyShotList.RemoveAll(shot => !shot.IsAlive);

    // 몬스터와 적 투사체를 비운다(웨이브 끝·판 끝·로비). 셈은 남긴다
    public void Clear()
    {
        _monsterList.Clear();
        _enemyShotList.Clear();
    }
}

public class Monster
{
    private readonly List<PartyMember> _dashHitMemberList = new();

    public int Id { get; }
    public MonsterData Data { get; }
    public MonsterBehavior Behavior => Data.Behavior;
    // 바닥 평면 좌표
    public Vector2 Position { get; set; }
    public float Hp { get; set; }
    public float MaxHp { get; }
    // 웨이브 피해 배수를 곱한 피해
    public float Damage { get; }
    public float AttackClock { get; set; }
    public MonsterPhase Phase { get; set; } = MonsterPhase.Approach;
    public float PhaseClock { get; set; }
    public Vector2 DashDirection { get; set; }
    public Vector2 FlockDirection { get; set; }
    // 재배치 조건(화면 밖·뒤쪽)을 이어서 만족한 시간
    public float OutsideSeconds { get; set; }
    public IReadOnlyList<PartyMember> DashHitMemberList => _dashHitMemberList;
    public bool IsAlive => Hp > 0f;

    public Monster(int id, MonsterData data, Vector2 position, float hp, float damage, float attackClock)
    {
        Id = id;
        Data = data;
        Position = position;
        Hp = hp;
        MaxHp = hp;
        Damage = damage;
        AttackClock = attackClock;
    }

    public void AddDashHit(PartyMember member) => _dashHitMemberList.Add(member);

    public void ClearDashHits() => _dashHitMemberList.Clear();
}

// 적 투사체(바닥 평면). 맞히거나 최대 이동에 닿으면 IsAlive = false, 걸음 끝에 목록에서 빠진다
public class EnemyShot
{
    public int Id { get; }
    public Vector2 Position { get; set; }
    public Vector2 Direction { get; }
    public float Speed { get; }
    public float Damage { get; }
    public float Travel { get; set; }
    public float MaxTravel { get; }
    public float HitRadius { get; }
    public string Color { get; }
    public string ShotSprite { get; }
    public bool IsAlive { get; set; } = true;

    public EnemyShot(int id, Vector2 position, Vector2 direction, float speed, float damage, float maxTravel, float hitRadius,
        string color, string shotSprite = "")
    {
        ShotSprite = shotSprite;
        Id = id;
        Position = position;
        Direction = direction;
        Speed = speed;
        Damage = damage;
        MaxTravel = maxTravel;
        HitRadius = hitRadius;
        Color = color;
    }
}
