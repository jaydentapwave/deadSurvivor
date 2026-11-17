using System;
using System.Collections.Generic;
using UnityEngine;

// T1: ItemData 계약을 유지하는 별도 표현 대응표. 아이템별 분기는 이 데이터에만 둔다.
[Serializable]
public class CombatEffectData
{
    [SerializeField] private CombatItemEffectData[] items;
    public IReadOnlyList<CombatItemEffectData> Items => items;

    public CombatItemEffectData Get(string itemId)
    {
        foreach (var item in items)
        {
            if (item.ItemId == itemId)
            {
                return item;
            }
        }

        throw new KeyNotFoundException($"[CombatEffectData] No effect for {itemId}");
    }
}

[Serializable]
public class CombatItemEffectData
{
    [SerializeField] private string itemId;
    [SerializeField] private string projectile;
    [SerializeField] private string burst;
    public string ItemId => itemId;
    public string Projectile => projectile;
    public string Burst => burst;
}
