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

    private readonly Dictionary<Sprite, int> _visibleWidthDict = new();
    private Texture2D _readback;
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

    // UI는 회전·tight packing이 꺼진 Characters 아틀라스에 함께 들어간다.
    // border와 왼쪽 피벗은 코드에서만 정의하며 원본 .meta는 바꾸지 않는다.
    public Sprite GetUi(string key)
    {
        var cacheKey = "UI/" + key;
        if (_spriteDict.TryGetValue(cacheKey, out var sprite))
        {
            return sprite;
        }

        var source = Get(key);
        sprite = Sprite.Create(source.texture, source.textureRect, new Vector2(0f, 0.5f),
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect, UiArtLayout.Border(key));
        sprite.name = key;
        _spriteDict.Add(cacheKey, sprite);
        return sprite;
    }

    // 아틀라스는 읽기 불가 텍스처다. 잘린 영역만 최초 한 번 읽고 풀 재사용 시 폭을 재사용한다.
    public int GetVisibleWidth(Sprite sprite)
    {
        if (_visibleWidthDict.TryGetValue(sprite, out var width))
        {
            return width;
        }

        var rect = sprite.textureRect;
        var texture = sprite.texture;
        var target = RenderTexture.GetTemporary(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), 0,
            RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, target, new Vector2(rect.width / texture.width, rect.height / texture.height),
                new Vector2(rect.x / texture.width, rect.y / texture.height));
            RenderTexture.active = target;
            if (_readback == null)
            {
                _readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            }
            else
            {
                _readback.Reinitialize(target.width, target.height);
            }

            _readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            width = SpriteMotion.GetVisibleWidth(_readback.GetPixels32(), target.width);
            _visibleWidthDict.Add(sprite, width);
            return width;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
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
        _visibleWidthDict.Clear();
        Object.Destroy(_readback);
    }
}
