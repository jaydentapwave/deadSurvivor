using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// 크기별 래스터 그림의 생성·수명 책임. 같은 키는 같은 Sprite를 재사용한다.
public class EffectSpriteCache : IDisposable
{
    private readonly Dictionary<(string, int, double, int, int), Sprite> _spriteDict = new();
    private readonly Dictionary<(int, int, uint, int), Sprite> _chainDict = new();
    private readonly List<Object> _createdList = new();
    private readonly Dictionary<(int, double, double, float, int), Sprite> _dqDashDict = new();
    private DQTelegraphTiles _dqTelegraph;
    private bool _dqTelegraphLoaded;

    public bool HasDQTelegraph
    {
        get
        {
            if (!_dqTelegraphLoaded)
            {
                _dqTelegraph = Resources.Load<DQTelegraphTiles>(DQTelegraphTiles.ResourcePath);
                _dqTelegraphLoaded = true;
            }
            return _dqTelegraph != null;
        }
    }

    public Sprite DQDash(double lengthMetres, double radiusMetres, float floorAngle, int stage, int ownerId = 0)
    {
        if (!HasDQTelegraph)
        {
            return Dash(lengthMetres, floorAngle, Mathf.Min(stage, 2));
        }
        var angle = Mathf.Repeat(floorAngle, 360);
        var key = (ownerId, lengthMetres, radiusMetres, angle, stage);
        if (!_dqDashDict.TryGetValue(key, out var sprite))
        {
            sprite = Create(_dqTelegraph.Raster(lengthMetres, radiusMetres, angle, stage), $"dq_dash_line_{stage + 1}");
            _dqDashDict.Add(key, sprite);
        }
        return sprite;
    }

    public void ReleaseDQDash(int ownerId)
    {
        if (_dqDashDict.Count == 0)
        {
            return;
        }
        var keys = new List<(int, double, double, float, int)>();
        foreach (var pair in _dqDashDict)
        {
            if (pair.Key.Item1 == ownerId)
            {
                keys.Add(pair.Key);
            }
        }
        foreach (var key in keys)
        {
            var sprite = _dqDashDict[key];
            var texture = sprite.texture;
            _dqDashDict.Remove(key);
            _createdList.Remove(sprite);
            _createdList.Remove(texture);
            DestroyOwned(sprite);
            DestroyOwned(texture);
        }
    }

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

    public Sprite Chain(int dx, int dy, uint seed, int frame)
    {
        var key = (dx, dy, seed, frame);
        if (!_chainDict.TryGetValue(key, out var sprite))
        {
            sprite = Create(ChainBoltRaster.Draw(dx, dy, seed, frame), $"chain_bolt_{frame + 1}");
            _chainDict.Add(key, sprite);
        }
        return sprite;
    }

    // 발동 id의 모든 프레임은 뷰를 풀로 돌려준 뒤 해제한다. 다음 발동까지 보관하지 않는다.
    public void ReleaseChain(uint seed)
    {
        var keys = new List<(int, int, uint, int)>();
        foreach (var pair in _chainDict)
        {
            if (pair.Key.Item3 == seed)
            {
                keys.Add(pair.Key);
            }
        }
        foreach (var key in keys)
        {
            var sprite = _chainDict[key];
            var texture = sprite.texture;
            _chainDict.Remove(key);
            _createdList.Remove(sprite);
            _createdList.Remove(texture);
            DestroyOwned(sprite);
            DestroyOwned(texture);
        }
    }

    private static void DestroyOwned(Object created)
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
            DestroyOwned(created);
        }
        _spriteDict.Clear();
        _chainDict.Clear();
        _dqDashDict.Clear();
        _createdList.Clear();
    }
}
