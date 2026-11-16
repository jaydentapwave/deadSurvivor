using System;
using UnityEngine;

// 몬스터 한 종류(Data/Monster/<id>.json, 기획서 데이터 스키마). 행동에 맞는 칸 묶음만 읽는다.
[Serializable]
public class MonsterData : ISerializationCallbackReceiver
{
    [SerializeField] private string id;
    [SerializeField] private string behavior;
    [SerializeField] private float hp;
    [SerializeField] private float damage;
    [SerializeField] private float speed;
    [SerializeField] private float radius;
    [SerializeField] private float contactGap;
    [SerializeField] private string color;
    [SerializeField] private MonsterContactData contact;
    [SerializeField] private MonsterProjectileData projectile;
    [SerializeField] private MonsterChargeData charge;
    [SerializeField] private MonsterBombData bomb;

    private MonsterBehavior _behavior;

    public string Id => id;
    public MonsterBehavior Behavior => _behavior;
    public float Hp => hp;
    public float Damage => damage;
    public float Speed => speed;
    public float Radius => radius;
    public float ContactGap => contactGap;
    public string Color => color;
    public MonsterContactData Contact => contact;
    public MonsterProjectileData Projectile => projectile;
    public MonsterChargeData Charge => charge;
    public MonsterBombData Bomb => bomb;

    public void OnBeforeSerialize()
    {
    }

    // behavior 글자를 열거형으로 한 번 푼다. 다섯 밖이면 오류(데이터 검사 check_data.py가 먼저 막는다)
    public void OnAfterDeserialize()
    {
        if (!Enum.TryParse(behavior, true, out _behavior))
        {
            GameLogger.LogError($"[MonsterData] {id}: unknown behavior {behavior}");
        }
    }
}

[Serializable]
public class MonsterContactData
{
    [SerializeField] private float interval;

    public float Interval => interval;
}

[Serializable]
public class MonsterProjectileData
{
    [SerializeField] private float range;
    [SerializeField] private float approachRatio;
    [SerializeField] private float retreatRatio;
    [SerializeField] private float interval;
    [SerializeField] private float shotSpeed;
    [SerializeField] private float shotTravelScale;
    [SerializeField] private float shotHitRadius;
    [SerializeField] private string shotColor;

    public float Range => range;
    public float ApproachRatio => approachRatio;
    public float RetreatRatio => retreatRatio;
    public float Interval => interval;
    public float ShotSpeed => shotSpeed;
    public float ShotTravelScale => shotTravelScale;
    public float ShotHitRadius => shotHitRadius;
    public string ShotColor => shotColor;
}

[Serializable]
public class MonsterChargeData
{
    [SerializeField] private float triggerRange;
    [SerializeField] private float rallyRadius;
    [SerializeField] private int rallyMax;
    [SerializeField] private float windupSeconds;
    [SerializeField] private float dashSeconds;
    [SerializeField] private float dashSpeed;
    [SerializeField] private float dashHitRadius;
    [SerializeField] private float recoverSeconds;
    [SerializeField] private float cooldown;

    public float TriggerRange => triggerRange;
    public float RallyRadius => rallyRadius;
    // 자신 말고 함께 모이는 최대 수
    public int RallyMax => rallyMax;
    public float WindupSeconds => windupSeconds;
    public float DashSeconds => dashSeconds;
    public float DashSpeed => dashSpeed;
    public float DashHitRadius => dashHitRadius;
    public float RecoverSeconds => recoverSeconds;
    public float Cooldown => cooldown;
}

[Serializable]
public class MonsterBombData
{
    [SerializeField] private float fuseDistance;
    [SerializeField] private float fuseSeconds;
    [SerializeField] private float blastRadius;
    [SerializeField] private float swarmTriggerRange;
    [SerializeField] private float swarmSeconds;
    [SerializeField] private float swarmSpeed;
    [SerializeField] private float flockRadius;
    [SerializeField] private float separationRadius;
    [SerializeField] private float cohesion;
    [SerializeField] private float alignment;
    [SerializeField] private float separation;
    [SerializeField] private float steerRate;
    [SerializeField] private int groupSize;
    [SerializeField] private float groupSpread;

    public float FuseDistance => fuseDistance;
    public float FuseSeconds => fuseSeconds;
    public float BlastRadius => blastRadius;
    public float SwarmTriggerRange => swarmTriggerRange;
    public float SwarmSeconds => swarmSeconds;
    public float SwarmSpeed => swarmSpeed;
    public float FlockRadius => flockRadius;
    public float SeparationRadius => separationRadius;
    public float Cohesion => cohesion;
    public float Alignment => alignment;
    public float Separation => separation;
    public float SteerRate => steerRate;
    public int GroupSize => groupSize;
    public float GroupSpread => groupSpread;
}
