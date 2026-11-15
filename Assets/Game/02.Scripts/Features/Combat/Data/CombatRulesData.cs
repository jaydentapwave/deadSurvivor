using System;
using UnityEngine;

// 전투 규칙 값(기획서 데이터 스키마 Combat/combat_rules.json). 예전 판 상수를 데이터로 뺐다
[Serializable]
public class CombatRulesData
{
    [SerializeField] private float rangeUnit;
    [SerializeField] private float defCap;
    [SerializeField] private float hasteCap;
    [SerializeField] private float healPeriod;
    [SerializeField] private float shieldPeriod;
    [SerializeField] private float meleeHalfAngle;
    [SerializeField] private float sweepHalfAngle;
    [SerializeField] private float projectileSpeed;
    [SerializeField] private float projectileSpread;
    [SerializeField] private float explosionRadius;
    [SerializeField] private float explosionTriggerGap;
    [SerializeField] private int chainBaseHits;
    [SerializeField] private float chainHopRange;
    [SerializeField] private float firedSeconds;

    // 아이템 사거리 1칸 = 바닥 단위
    public float RangeUnit => rangeUnit;
    public float DefCap => defCap;
    public float HasteCap => hasteCap;
    // 자기 회복·나눔 기본 주기(초, 가속으로 나눔)
    public float HealPeriod => healPeriod;
    public float ShieldPeriod => shieldPeriod;
    // 반각(도)
    public float MeleeHalfAngle => meleeHalfAngle;
    public float SweepHalfAngle => sweepHalfAngle;
    public float ProjectileSpeed => projectileSpeed;
    // 갈래 사이 각(라디안)
    public float ProjectileSpread => projectileSpread;
    public float ExplosionRadius => explosionRadius;
    public float ExplosionTriggerGap => explosionTriggerGap;
    public int ChainBaseHits => chainBaseHits;
    public float ChainHopRange => chainHopRange;
    // 하단 칸 "발동!" 표시 시간
    public float FiredSeconds => firedSeconds;
}
