using System;
using System.Collections.Generic;
using UnityEngine;

// 아이템 한 종(기획서 C절·데이터 스키마). 문자열 칸은 읽을 때 enum으로 바꿔 둔다(모르는 값은 에러 로그 — 데이터 검사가 먼저 막는다)
[Serializable]
public class ItemData : ISerializationCallbackReceiver
{
    [SerializeField] private string id;
    [SerializeField] private string name;
    [SerializeField] private string mode;
    [SerializeField] private ItemCell[] shape;
    [SerializeField] private float power;
    [SerializeField] private float interval;
    [SerializeField] private float range;
    [SerializeField] private string baseEffect;
    [SerializeField] private float baseValue;
    [SerializeField] private string bonusEffect;
    [SerializeField] private float bonusValue;
    [SerializeField] private string bonusTarget;
    [SerializeField] private bool bonusNeedsAdjacent;
    [SerializeField] private float effectInterval;
    [SerializeField] private float effectRadius;
    [SerializeField] private string icon;
    // 전리품 가중치(F5 B3). 칸이 없는 파일은 1, 0이면 전리품에 안 나온다
    [SerializeField] private float lootWeight = 1f;

    private ItemMode _mode;
    private ItemEffectKind _baseEffect;
    private ItemEffectKind _bonusEffect;
    private ItemBonusTarget _bonusTarget;

    public string Id => id;
    public string Name => name;
    public ItemMode Mode => _mode;
    public IReadOnlyList<ItemCell> Shape => shape;
    public float Power => power;
    public float Interval => interval;
    public float Range => range;
    public ItemEffectKind BaseEffect => _baseEffect;
    public float BaseValue => baseValue;
    public ItemEffectKind BonusEffect => _bonusEffect;
    public float BonusValue => bonusValue;
    public ItemBonusTarget BonusTarget => _bonusTarget;
    public bool BonusNeedsAdjacent => bonusNeedsAdjacent;
    public float EffectInterval => effectInterval;
    public float EffectRadius => effectRadius;
    public string Icon => icon;
    public float LootWeight => lootWeight;
    public bool IsHealMode => _mode == ItemMode.Heal || _mode == ItemMode.HealArea;

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        _mode = Parse<ItemMode>(mode, nameof(mode));
        _baseEffect = string.IsNullOrEmpty(baseEffect) ? ItemEffectKind.None : Parse<ItemEffectKind>(baseEffect, nameof(baseEffect));
        _bonusEffect = string.IsNullOrEmpty(bonusEffect) ? ItemEffectKind.None : Parse<ItemEffectKind>(bonusEffect, nameof(bonusEffect));
        _bonusTarget = string.IsNullOrEmpty(bonusTarget) ? ItemBonusTarget.Any : Parse<ItemBonusTarget>(bonusTarget, nameof(bonusTarget));
    }

    private T Parse<T>(string value, string field) where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(typeof(T), parsed) && !int.TryParse(value, out _))
        {
            return parsed;
        }

        GameLogger.LogError($"[ItemData] {id}: unknown {field} '{value}'");
        return default;
    }
}

// 가방 칸 모양의 한 칸(좌표 뜻은 F5가 정한다)
[Serializable]
public struct ItemCell
{
    [SerializeField] private int x;
    [SerializeField] private int y;

    public int X => x;
    public int Y => y;
}
