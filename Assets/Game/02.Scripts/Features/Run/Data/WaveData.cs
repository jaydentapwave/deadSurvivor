using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaveData
{
    [SerializeField] private string id;
    [SerializeField] private int order;
    [SerializeField] private float duration;
    [SerializeField] private bool hasBurningTime;
    [SerializeField] private int spawnCount;
    [SerializeField] private float hpScale;
    [SerializeField] private float damageScale;
    [SerializeField] private TypeWeightData[] typeWeights;

    public string Id => id;
    public int Order => order;
    public float Duration => duration;
    public bool HasBurningTime => hasBurningTime;
    public int SpawnCount => spawnCount;
    public float HpScale => hpScale;
    public float DamageScale => damageScale;
    public IReadOnlyList<TypeWeightData> TypeWeights => typeWeights;
}
