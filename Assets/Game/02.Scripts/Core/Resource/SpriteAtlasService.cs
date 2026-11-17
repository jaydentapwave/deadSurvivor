using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

// 그림 키로 스프라이트 찾기(그림 넣기 기획서 C절). 아틀라스(Resources/Art/Characters, 바닥은 Resources/Art/Ground — 아트 기준서 3.6)를 처음 쓸 때 한 번 읽고,
// GetSprite는 부를 때마다 복제를 만들므로 이름별로 캐시한다. 만든 복제는 scope가 끝날 때 지운다(PlaceholderSpriteFactory와 같음).
public class SpriteAtlasService : IDisposable
{
    // 캐릭터 그림 이름 = <key>_front·<key>_back·<key>_downed(쓰러짐), 몬스터 = <key>·<key>_telegraph(돌진 예고)·<key>_inflate(자폭 점화)
    public const string FrontSuffix = "_front";
    public const string BackSuffix = "_back";
    public const string DownedSuffix = "_downed";
    public const string TelegraphSuffix = "_telegraph";
    public const string InflateSuffix = "_inflate";
    public const string CharactersAtlasPath = "Art/Characters";
    public const string GroundAtlasPath = "Art/Ground";

    private readonly ResourceService _resourceService;
    private readonly Dictionary<string, Sprite> _spriteDict = new();
    private readonly Dictionary<string, SpriteAtlas> _atlasDict = new();

    public SpriteAtlasService(ResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    // 없는 이름이면 오류 로그를 남기고 null(부르는 쪽이 F1 임시 도형으로 그린다)
    public Sprite Get(string spriteName, string atlasPath = CharactersAtlasPath)
    {
        if (_spriteDict.TryGetValue(spriteName, out var sprite))
        {
            return sprite;
        }

        if (!_atlasDict.TryGetValue(atlasPath, out var atlas))
        {
            atlas = _resourceService.Load<SpriteAtlas>(atlasPath);
            _atlasDict.Add(atlasPath, atlas);
        }

        sprite = atlas.GetSprite(spriteName);
        if (sprite == null)
        {
            GameLogger.LogError($"[SpriteAtlasService] No sprite {spriteName}");
            return null;
        }

        _spriteDict.Add(spriteName, sprite);
        return sprite;
    }

    public Sprite[] GetMemberFrames(string key, string clip, int frameCount)
    {
        var frames = new Sprite[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            frames[i] = Get($"{key}_{clip}_{i:00}");
        }

        return frames;
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
