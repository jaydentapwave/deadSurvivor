using System;
using UnityEngine;

// 임시 전투 값(기획서 E절). F3·F4가 칸을 지우고 마지막에 파일을 지운다(F3이 weapon을 지웠다).
[Serializable]
public class PlaceholderCombatData
{
    [SerializeField] private PlaceholderEnemyData enemy;
    [SerializeField] private int spawnCount;
    [SerializeField] private float spawnMargin;
    [SerializeField] private float burningIntervalScale;

    public PlaceholderEnemyData Enemy => enemy;
    public int SpawnCount => spawnCount;
    public float SpawnMargin => spawnMargin;
    public float BurningIntervalScale => burningIntervalScale;
}

[Serializable]
public class PlaceholderEnemyData
{
    [SerializeField] private float hp;
    [SerializeField] private float damage;
    [SerializeField] private float speed;
    [SerializeField] private float radius;
    [SerializeField] private float attackInterval;
    [SerializeField] private float contactGap;
    [SerializeField] private string color;

    public float Hp => hp;
    public float Damage => damage;
    public float Speed => speed;
    public float Radius => radius;
    public float AttackInterval => attackInterval;
    public float ContactGap => contactGap;
    public string Color => color;
}
