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
        foreach (var clip in clips ?? Array.Empty<CombatEffectClipData>())
        {
            if (clip.Key == key)
            {
                return clip;
            }
        }
        return CombatEffectClipData.SingleSprite(key);
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
    [SerializeField] private int frames = 1;
    [SerializeField] private float fps = 1f;
    [SerializeField] private bool loop;
    [SerializeField] private bool directional;
    [SerializeField] private int materialIndex;
    public string Key => key;
    public string Sprite => sprite;
    public int Frames => frames;
    public float Fps => fps;
    public bool Loop => loop;
    public bool Directional => directional;
    public int MaterialIndex => materialIndex;
    public bool IsSingleSprite => frames == 1 && !directional;
    public string FrameKey(int index, string direction = "right") =>
        IsSingleSprite ? sprite : directional ? $"{sprite}_{direction}_{index + 1}" : $"{sprite}_{index + 1}";

    // clips가 없는 옛 데이터는 전달된 그림 키 한 장을 그대로 표시한다.
    public static CombatEffectClipData SingleSprite(string spriteKey) => new()
    {
        key = spriteKey,
        sprite = spriteKey
    };
}
