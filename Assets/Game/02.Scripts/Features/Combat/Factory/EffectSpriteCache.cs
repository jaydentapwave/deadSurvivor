using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// 크기별 래스터 그림의 생성·수명 책임. 같은 키는 같은 Sprite를 재사용한다.
public class EffectSpriteCache : IDisposable
{
    private readonly Dictionary<(string, int, double, int, int), Sprite> _spriteDict = new();
    private readonly List<Object> _createdList = new();

    public Sprite Ring(string kind, double radiusMeters, int frame)
    {
        // 픽셀 외접 반지름이 같아도 가장자리 픽셀은 달라질 수 있으므로 원래 반지름도 키에 남긴다.
        var key = (kind, (int)Math.Ceiling(radiusMeters * 48), radiusMeters, 0, frame);
        if (!_spriteDict.TryGetValue(key, out var sprite))
        {
            sprite = Create(GroundMarkRaster.Ring(kind, radiusMeters, frame), $"ground_{kind}_{frame + 1}");
            _spriteDict.Add(key, sprite);
        }
        return sprite;
    }

    public Sprite Dash(double lengthMeters, float floorAngle, int frame)
    {
        var direction = ((Mathf.RoundToInt(floorAngle / 11.25f) % 32) + 32) % 32;
        var key = ("dash", (int)Math.Ceiling(lengthMeters * 48), lengthMeters, direction, frame);
        if (!_spriteDict.TryGetValue(key, out var sprite))
        {
            sprite = Create(GroundMarkRaster.DashPath(lengthMeters, direction * 11.25, frame), $"dash_line_{frame + 1}");
            _spriteDict.Add(key, sprite);
        }
        return sprite;
    }

    private Sprite Create(EffectRaster image, string name)
    {
        var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };
        texture.SetPixels32(image.Pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, image.Width, image.Height), image.Pivot, 48, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        _createdList.Add(sprite);
        _createdList.Add(texture);
        return sprite;
    }

    public void Dispose()
    {
        foreach (var created in _createdList)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(created);
            }
            else
            {
                Object.DestroyImmediate(created);
            }
        }
        _spriteDict.Clear();
        _createdList.Clear();
    }
}
