using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

// 그림 키로 스프라이트 찾기(그림 넣기 기획서 C절). 아틀라스(Resources/Art/Characters)를 처음 쓸 때 한 번 읽고,
// GetSprite는 부를 때마다 복제를 만들므로 이름별로 캐시한다. 만든 복제는 scope가 끝날 때 지운다(PlaceholderSpriteFactory와 같음).
public class SpriteAtlasService : IDisposable
{
    // 캐릭터 그림 이름 = <key>_front·<key>_back·<key>_downed(쓰러짐), 몬스터 = <key>·<key>_telegraph(돌진 예고)·<key>_inflate(자폭 점화)
    public const string FrontSuffix = "_front";
    public const string BackSuffix = "_back";
    public const string DownedSuffix = "_downed";
    public const string TelegraphSuffix = "_telegraph";
    public const string InflateSuffix = "_inflate";
    private const string AtlasPath = "Art/Characters";

    private readonly ResourceService _resourceService;
    private readonly Dictionary<string, Sprite> _spriteDict = new();
    private SpriteAtlas _atlas;

    public SpriteAtlasService(ResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    // 없는 이름이면 오류 로그를 남기고 null(부르는 쪽이 F1 임시 도형으로 그린다)
    public Sprite Get(string spriteName)
    {
        if (_spriteDict.TryGetValue(spriteName, out var sprite))
        {
            return sprite;
        }

        if (_atlas == null)
        {
            _atlas = _resourceService.Load<SpriteAtlas>(AtlasPath);
        }

        sprite = _atlas.GetSprite(spriteName);
        if (sprite == null)
        {
            GameLogger.LogError($"[SpriteAtlasService] No sprite {spriteName}");
            return null;
        }

        _spriteDict.Add(spriteName, sprite);
        return sprite;
    }

    public void Dispose()
    {
        foreach (var sprite in _spriteDict.Values)
        {
            Object.Destroy(sprite);
        }

        _spriteDict.Clear();
    }
}
