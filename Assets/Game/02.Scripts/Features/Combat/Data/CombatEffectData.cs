using System;
using System.Collections.Generic;
using UnityEngine;

// T1: ItemData 계약을 유지하는 별도 표현 대응표. 아이템별 분기는 이 데이터에만 둔다.
[Serializable]
public class CombatEffectData
{
    [SerializeField] private CombatItemEffectData[] items;
    [SerializeField] private CombatEffectClipData[] clips;
    public IReadOnlyList<CombatItemEffectData> Items => items;
    public IReadOnlyList<CombatEffectClipData> Clips => clips;

    public CombatEffectClipData GetClip(string key)
    {
        foreach (var clip in clips)
        {
            if (clip.Key == key)
            {
                return clip;
            }
        }
        throw new KeyNotFoundException($"[CombatEffectData] No clip for {key}");
    }

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

[Serializable]
public class CombatEffectClipData
{
    [SerializeField] private string key;
    [SerializeField] private string sprite;
    [SerializeField] private int frames;
    [SerializeField] private float fps;
    [SerializeField] private bool loop;
    [SerializeField] private bool directional;
    public string Key => key;
    public string Sprite => sprite;
    public int Frames => frames;
    public float Fps => fps;
    public bool Loop => loop;
    public bool Directional => directional;
    public string FrameKey(int index, string direction = "right") =>
        directional ? $"{sprite}_{direction}_{index + 1}" : $"{sprite}_{index + 1}";
}
