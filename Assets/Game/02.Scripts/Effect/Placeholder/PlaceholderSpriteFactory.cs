using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// 임시 도형 스프라이트(원·마름모·상자)와 바닥 타일을 실행 중에 만든다(기획서 F절, 그림 파일 없음).
// Point 필터, PPU 48. 만든 텍스처·스프라이트·타일은 scope가 끝날 때 지운다.
public class PlaceholderSpriteFactory : IDisposable
{
    private readonly List<Object> _createdList = new();

    // 피벗 = 아래 가운데(발 가운데, 아트 기준서 3.1)
    public Sprite CreateCircle(int diameterPixels, Color color)
    {
        var radius = diameterPixels * 0.5f;
        return CreateSprite(diameterPixels, diameterPixels, color, new Vector2(0.5f, 0f), (x, y) =>
        {
            var dx = x + 0.5f - radius;
            var dy = y + 0.5f - radius;
            return dx * dx + dy * dy <= radius * radius;
        });
    }

    public Sprite CreateDiamond(int widthPixels, int heightPixels, Color color, Vector2 pivot)
    {
        var halfWidth = widthPixels * 0.5f;
        var halfHeight = heightPixels * 0.5f;
        return CreateSprite(widthPixels, heightPixels, color, pivot, (x, y) =>
            Mathf.Abs(x + 0.5f - halfWidth) / halfWidth + Mathf.Abs(y + 0.5f - halfHeight) / halfHeight <= 1f);
    }

    public Sprite CreateBox(int widthPixels, int heightPixels, Color color, Vector2 pivot)
    {
        return CreateSprite(widthPixels, heightPixels, color, pivot, (x, y) => true);
    }

    public Tile CreateTile(Sprite sprite)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        _createdList.Add(tile);
        return tile;
    }

    // 데이터의 #rrggbb 색. 읽지 못하면 오류를 남기고 자홍(눈에 띄게)
    public Color ParseColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var color))
        {
            return color;
        }

        GameLogger.LogError($"[PlaceholderSpriteFactory] Invalid color {hex}");
        return Color.magenta;
    }

    public void Dispose()
    {
        foreach (var created in _createdList)
        {
            Object.Destroy(created);
        }

        _createdList.Clear();
    }

    private Sprite CreateSprite(int width, int height, Color color, Vector2 pivot, Func<int, int, bool> isInside)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[width * height];
        Color32 fill = color;
        var clear = new Color32(0, 0, 0, 0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] = isInside(x, y) ? fill : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, Const.PixelsPerUnit, 0,
            SpriteMeshType.FullRect);
        _createdList.Add(texture);
        _createdList.Add(sprite);
        return sprite;
    }
}
