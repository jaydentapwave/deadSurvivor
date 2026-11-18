using System.Collections.Generic;
using UnityEngine;

// 능력치 계산(기획서 B3·B4, 예전 EquipmentRules.Stats·BonusApplies·WeaponPower). 상태를 바꾸지 않는 계산만 한다.
public class EquipmentStatService
{
    private readonly ItemRepository _itemRepository;
    private readonly CombatRulesData _rules;
    private readonly IItemPlacementInfo _placementInfo;

    public EquipmentStatService(ItemRepository itemRepository, CombatRulesRepository combatRulesRepository,
        IItemPlacementInfo placementInfo)
    {
        _itemRepository = itemRepository;
        _rules = combatRulesRepository.Rules;
        _placementInfo = placementInfo;
    }

    public float GetGradeMultiplier(int memberIndex, int slotIndex) => _placementInfo.GetGradeMultiplier(memberIndex, slotIndex);

    // actionSlot −1 = 대원 전체 계산(조건 있는 추가 효과는 넣지 않는다)
    public CombatStats Calculate(int memberIndex, CharacterData character, IReadOnlyList<string> equippedItemIdList, int actionSlot)
    {
        var stats = new CombatStats();
        for (var slot = 0; slot < equippedItemIdList.Count; slot++)
        {
            var item = _itemRepository.Get(equippedItemIdList[slot]);
            var multiplier = _placementInfo.GetGradeMultiplier(memberIndex, slot);
            Apply(ref stats, item.BaseEffect, item.BaseValue * multiplier);
            if (IsBonusApplied(memberIndex, equippedItemIdList, slot, actionSlot))
            {
                Apply(ref stats, item.BonusEffect, GetBonusValue(item, multiplier));
            }
        }

        stats.Defense = Mathf.Min(_rules.DefCap, character.Def + stats.Defense);
        stats.Haste = Mathf.Min(_rules.HasteCap, stats.Haste);
        stats.MaxHp += character.Hp;
        stats.Shield = Mathf.Max(stats.Shield, stats.StartShield);
        return stats;
    }

    // 추가 효과가 이 계산에 적용되는가. 조건(대상·인접)이 없으면 늘, 있으면 그 발동 아이템(자기 자신 아님)에만
    public bool IsBonusApplied(int memberIndex, IReadOnlyList<string> equippedItemIdList, int sourceSlot, int actionSlot)
    {
        var source = _itemRepository.Get(equippedItemIdList[sourceSlot]);
        if (source.BonusEffect == ItemEffectKind.None)
        {
            return false;
        }

        if (!HasBonusCondition(source))
        {
            return true;
        }

        if (actionSlot < 0 || actionSlot == sourceSlot)
        {
            return false;
        }

        var action = _itemRepository.Get(equippedItemIdList[actionSlot]);
        return IsTargetMatched(source.BonusTarget, action.Mode)
            && (!source.BonusNeedsAdjacent || _placementInfo.IsAdjacent(memberIndex, sourceSlot, actionSlot));
    }

    // 지속 효과(보호막 채움·파동·나눔)로 쓸 추가 효과가 켜져 있는가: 조건이 없거나, 그 대원의 발동 칸 하나에라도 적용된다
    public bool IsBonusActive(int memberIndex, IReadOnlyList<string> equippedItemIdList, int sourceSlot)
    {
        var source = _itemRepository.Get(equippedItemIdList[sourceSlot]);
        if (source.BonusEffect == ItemEffectKind.None)
        {
            return false;
        }

        if (!HasBonusCondition(source))
        {
            return true;
        }

        for (var slot = 0; slot < equippedItemIdList.Count; slot++)
        {
            if (_itemRepository.Get(equippedItemIdList[slot]).Mode != ItemMode.Passive
                && IsBonusApplied(memberIndex, equippedItemIdList, sourceSlot, slot))
            {
                return true;
            }
        }

        return false;
    }

    // 추가 효과 값: pierce·extraShots·chain은 등급 배수를 곱하지 않는다
    public float GetBonusValue(ItemData item, float gradeMultiplier)
    {
        var isCount = item.BonusEffect == ItemEffectKind.Pierce || item.BonusEffect == ItemEffectKind.ExtraShots
            || item.BonusEffect == ItemEffectKind.Chain;
        return isCount ? item.BonusValue : item.BonusValue * gradeMultiplier;
    }

    // 발동 피해(공격 방식) 또는 회복량(회복 방식)
    public float GetPower(ItemData item, float gradeMultiplier, CharacterData character, CombatStats actionStats)
    {
        var value = item.Power * gradeMultiplier;
        return item.IsHealMode ? value : value * character.Atk * (1f + actionStats.Attack);
    }

    public float GetPeriod(ItemData item, CombatStats actionStats) => item.Interval / (1f + actionStats.Haste);

    public float GetRange(ItemData item, CombatStats actionStats) => item.Range * _rules.RangeUnit * (1f + actionStats.Range);

    private static bool HasBonusCondition(ItemData item) => item.BonusTarget != ItemBonusTarget.Any || item.BonusNeedsAdjacent;

    private static bool IsTargetMatched(ItemBonusTarget target, ItemMode mode)
    {
        switch (target)
        {
            case ItemBonusTarget.Any:
                return true;
            case ItemBonusTarget.Attack:
                return mode != ItemMode.Heal && mode != ItemMode.HealArea && mode != ItemMode.Passive;
            case ItemBonusTarget.Melee:
                return mode == ItemMode.Melee;
            case ItemBonusTarget.Sweep:
                return mode == ItemMode.Sweep;
            case ItemBonusTarget.Projectile:
                return mode == ItemMode.Projectile;
            case ItemBonusTarget.Explosion:
                return mode == ItemMode.Explosion;
            case ItemBonusTarget.Chain:
                return mode == ItemMode.Chain;
            case ItemBonusTarget.Heal:
                return mode == ItemMode.Heal;
            case ItemBonusTarget.HealArea:
                return mode == ItemMode.HealArea;
            default:
                GameLogger.LogError($"[EquipmentStatService] Unknown bonus target {target}");
                return false;
        }
    }

    private static void Apply(ref CombatStats stats, ItemEffectKind kind, float value)
    {
        switch (kind)
        {
            case ItemEffectKind.None:
                break;
            case ItemEffectKind.Attack:
                stats.Attack += value;
                break;
            case ItemEffectKind.Def:
                stats.Defense += value;
                break;
            case ItemEffectKind.Hp:
                stats.MaxHp += value;
                break;
            case ItemEffectKind.Heal:
                stats.Heal += value;
                break;
            case ItemEffectKind.Haste:
                stats.Haste += value;
                break;
            case ItemEffectKind.Range:
                stats.Range += value;
                break;
            case ItemEffectKind.Radius:
                stats.Radius += value;
                break;
            case ItemEffectKind.Shield:
                stats.Shield = Mathf.Max(stats.Shield, value);
                break;
            case ItemEffectKind.StartShield:
                stats.StartShield = Mathf.Max(stats.StartShield, value);
                break;
            case ItemEffectKind.Pierce:
                stats.Pierce = Mathf.Max(stats.Pierce, Mathf.RoundToInt(value));
                break;
            case ItemEffectKind.ExtraShots:
                stats.ExtraShots = Mathf.Max(stats.ExtraShots, Mathf.RoundToInt(value));
                break;
            case ItemEffectKind.Chain:
                stats.Chain = Mathf.Max(stats.Chain, Mathf.RoundToInt(value));
                break;
            case ItemEffectKind.Pulse:
            case ItemEffectKind.Share:
                // 동작 효과는 능력치가 아니다(CombatService가 IsBonusActive로 본다)
                break;
            default:
                GameLogger.LogError($"[EquipmentStatService] Unknown effect {kind}");
                break;
        }
    }
}
