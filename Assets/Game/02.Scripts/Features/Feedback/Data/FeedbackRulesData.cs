using System;
using UnityEngine;

// 전투 피드백 규칙(F6 기획서 데이터 스키마 Feedback/feedback_rules.json). 세기·시간·상한은 모두 이 값
[Serializable]
public class FeedbackRulesData
{
    [SerializeField] private DamageNumberRulesData damageNumber;
    [SerializeField] private HpLossRulesData hpLoss;
    [SerializeField] private ShakeRulesData shake;
    [SerializeField] private HapticRulesData haptic;
    [SerializeField] private MonsterHpRulesData monsterHp;

    public DamageNumberRulesData DamageNumber => damageNumber;
    public HpLossRulesData HpLoss => hpLoss;
    public ShakeRulesData Shake => shake;
    public HapticRulesData Haptic => haptic;
    public MonsterHpRulesData MonsterHp => monsterHp;
}

[Serializable]
public class DamageNumberRulesData
{
    [SerializeField] private float lifetime;
    [SerializeField] private float risePx;
    [SerializeField] private float fadeStartRatio;
    [SerializeField] private float offsetPx;
    [SerializeField] private float fontSizePx;
    [SerializeField] private int maxActive;
    [SerializeField] private string textColor;
    [SerializeField] private string outlineColor;

    public float Lifetime => lifetime;
    public float RisePx => risePx;
    public float FadeStartRatio => fadeStartRatio;
    public float OffsetPx => offsetPx;
    public float FontSizePx => fontSizePx;
    public int MaxActive => maxActive;
    public string TextColor => textColor;
    public string OutlineColor => outlineColor;
}

[Serializable]
public class HpLossRulesData
{
    [SerializeField] private float holdSeconds;
    [SerializeField] private float shrinkSeconds;
    [SerializeField] private string color;

    public float HoldSeconds => holdSeconds;
    public float ShrinkSeconds => shrinkSeconds;
    public string Color => color;
}

[Serializable]
public class ShakeRulesData
{
    [SerializeField] private int amplitudePx;
    [SerializeField] private float seconds;
    [SerializeField] private float minInterval;

    public int AmplitudePx => amplitudePx;
    public float Seconds => seconds;
    public float MinInterval => minInterval;
}

[Serializable]
public class HapticRulesData
{
    [SerializeField] private bool enabled;
    [SerializeField] private float minInterval;

    public bool Enabled => enabled;
    public float MinInterval => minInterval;
}

[Serializable]
public class MonsterHpRulesData
{
    [SerializeField] private string[] alwaysShowBehaviors;
    [SerializeField] private float lingerSeconds;
    [SerializeField] private int maxActive;
    [SerializeField] private int widthPx;
    [SerializeField] private int heightPx;
    [SerializeField] private float offsetPx;
    [SerializeField] private string fillColor;
    [SerializeField] private string backColor;

    // monster 파일의 behavior 글자(contact·projectile·charge·armored·bomb)
    public string[] AlwaysShowBehaviors => alwaysShowBehaviors;
    public float LingerSeconds => lingerSeconds;
    public int MaxActive => maxActive;
    public int WidthPx => widthPx;
    public int HeightPx => heightPx;
    public float OffsetPx => offsetPx;
    public string FillColor => fillColor;
    public string BackColor => backColor;
}
