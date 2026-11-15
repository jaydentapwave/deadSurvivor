using System;
using UnityEngine;

// 가방 규칙(기획서 데이터 스키마 Bag/bag_rules.json)
[Serializable]
public class BagRulesData
{
    [SerializeField] private int bagWidth;
    [SerializeField] private int bagHeight;
    [SerializeField] private ItemCell startCell;
    [SerializeField] private AutoEquipData autoEquip;

    public int BagWidth => bagWidth;
    public int BagHeight => bagHeight;
    // 한 판 시작 때 시작 아이템을 놓기 시작하는 칸(A6)
    public ItemCell StartCell => startCell;
    public AutoEquipData AutoEquip => autoEquip;
}

// 자동 장착 봇의 원정대 점수 가중치(기획서 E절)
[Serializable]
public class AutoEquipData
{
    [SerializeField] private float hpWeight;
    [SerializeField] private float shieldWeight;

    public float HpWeight => hpWeight;
    public float ShieldWeight => shieldWeight;
}
