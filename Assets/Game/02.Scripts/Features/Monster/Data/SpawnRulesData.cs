using System;
using System.Collections.Generic;
using UnityEngine;

// 생성·재배치 규칙(Data/Spawn/spawn_rules.json, 기획서 B절)
[Serializable]
public class SpawnRulesData
{
    [SerializeField] private float spawnMargin;
    [SerializeField] private float spawnEndGap;
    [SerializeField] private float initialAttackDelay;
    [SerializeField] private float burningIntervalScale;
    [SerializeField] private float burningSpread;
    [SerializeField] private TypeWeightData[] burningTypeWeights;
    [SerializeField] private SpawnHeadingData heading;
    [SerializeField] private SpawnRecycleData recycle;

    public float SpawnMargin => spawnMargin;
    public float SpawnEndGap => spawnEndGap;
    public float InitialAttackDelay => initialAttackDelay;
    public float BurningIntervalScale => burningIntervalScale;
    public float BurningSpread => burningSpread;
    public IReadOnlyList<TypeWeightData> BurningTypeWeights => burningTypeWeights;
    public SpawnHeadingData Heading => heading;
    public SpawnRecycleData Recycle => recycle;
}

// 원정대가 움직일 때 생성 방향 비율(합 1)과 흔들림(라디안)
[Serializable]
public class SpawnHeadingData
{
    [SerializeField] private float front;
    [SerializeField] private float left;
    [SerializeField] private float right;
    [SerializeField] private float back;
    [SerializeField] private float jitter;

    public float Front => front;
    public float Left => left;
    public float Right => right;
    public float Back => back;
    public float Jitter => jitter;
}

[Serializable]
public class SpawnRecycleData
{
    [SerializeField] private float outsideMargin;
    [SerializeField] private float holdSeconds;
    [SerializeField] private float placeMargin;
    [SerializeField] private int maxPerCheck;
    [SerializeField] private float checkInterval;
    [SerializeField] private float minAttackDelay;

    public float OutsideMargin => outsideMargin;
    public float HoldSeconds => holdSeconds;
    public float PlaceMargin => placeMargin;
    public int MaxPerCheck => maxPerCheck;
    public float CheckInterval => checkInterval;
    public float MinAttackDelay => minAttackDelay;
}
