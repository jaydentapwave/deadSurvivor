using System.Collections.Generic;
using UnityEngine;

// 날아가는 투사체 하나(기획서 B5 projectile·explosion). 바닥 평면 좌표
public class ProjectileState
{
    private readonly HashSet<int> _hitMonsterIdSet = new();

    public string ItemId { get; }
    public int Id { get; }
    public Vector2 Position { get; set; }
    public Vector2 Direction { get; }
    public float Speed { get; }
    public float Damage { get; }
    public float Range { get; }
    public float Traveled { get; set; }
    public int Pierce { get; }
    public bool IsExplosion { get; }
    public float ExplosionRadius { get; }
    public Vector2 TargetPosition { get; }
    public bool IsDone { get; set; }
    public ISet<int> HitMonsterIdSet => _hitMonsterIdSet;

    public ProjectileState(int id, Vector2 position, Vector2 direction, float speed, float damage, float range, int pierce,
        bool isExplosion, float explosionRadius, Vector2 targetPosition, string itemId = null)
    {
        ItemId = itemId;
        Id = id;
        Position = position;
        Direction = direction;
        Speed = speed;
        Damage = damage;
        Range = range;
        Pierce = pierce;
        IsExplosion = isExplosion;
        ExplosionRadius = explosionRadius;
        TargetPosition = targetPosition;
    }
}

// 범위 발동 고리 표시 하나(기획서 F절). 걸음 시간으로 줄어든다
public enum BurstEffectKind
{
    Melee,
    Heal,
    HealArea,
    Chain,
    Explosion,
    Pulse,
}

public class BurstState
{
    public string ItemId { get; }
    public BurstEffectKind Kind { get; }
    public Vector2 Direction { get; }
    public Vector2 EndPosition { get; }
    public int Id { get; }
    public Vector2 Position { get; }
    public float Radius { get; }
    public float Seconds { get; }
    public float RemainingSeconds { get; set; }

    public BurstState(int id, Vector2 position, float radius, float seconds, string itemId = null,
        BurstEffectKind kind = BurstEffectKind.Pulse, Vector2 direction = default, Vector2 endPosition = default)
    {
        Id = id;
        Position = position;
        ItemId = itemId;
        Kind = kind;
        Direction = direction;
        EndPosition = endPosition;
        Radius = radius;
        Seconds = seconds;
        RemainingSeconds = seconds;
    }
}
