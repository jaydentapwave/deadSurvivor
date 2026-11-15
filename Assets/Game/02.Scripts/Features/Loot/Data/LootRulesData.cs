using System;
using UnityEngine;

// 전리품 규칙(기획서 데이터 스키마 Loot/loot_rules.json, B2·B3)
[Serializable]
public class LootRulesData
{
    [SerializeField] private int minCount;
    [SerializeField] private int maxCount;
    [SerializeField] private int earlyWaveMax;
    [SerializeField] private float earlyBonusFactor;
    [SerializeField] private int mergeWaveMin;
    [SerializeField] private float mergeFactor;
    [SerializeField] private int lootSeedOffset;

    public int MinCount => minCount;
    public int MaxCount => maxCount;
    // 끝난 웨이브가 이 값 이하면 초반 배율
    public int EarlyWaveMax => earlyWaveMax;
    public float EarlyBonusFactor => earlyBonusFactor;
    // 끝난 웨이브가 이 값 이상이면 합성 배율
    public int MergeWaveMin => mergeWaveMin;
    public float MergeFactor => mergeFactor;
    // 전리품 난수 시드 = 이번 판 시드 + 이 값
    public int LootSeedOffset => lootSeedOffset;
}
