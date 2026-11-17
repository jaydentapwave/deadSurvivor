using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 화면 고치기 T6 — 같은 (크기, 색, 피벗) 상자는 한 스프라이트(한 텍스처)를 같이 써서 2D 렌더러가 묶어 그린다
public class PlaceholderSpriteFactoryTests
{
    private static readonly Vector2 BarPivot = new(0f, 0.5f);

    private readonly HashSet<Sprite> _spriteSet = new();

    // Dispose의 Object.Destroy는 에디터 시험에서 쓸 수 없어 시험이 직접 지운다(스프라이트와 텍스처 둘 다)
    [TearDown]
    public void DestroyCreated()
    {
        foreach (var sprite in _spriteSet)
        {
            Object.DestroyImmediate(sprite.texture);
            Object.DestroyImmediate(sprite);
        }

        _spriteSet.Clear();
    }

    [Test]
    public void SameBoxArgumentsShareOneSprite()
    {
        var factory = new PlaceholderSpriteFactory();

        var first = Track(factory.CreateBox(14, 2, Color.white, BarPivot));
        var second = Track(factory.CreateBox(14, 2, Color.white, BarPivot));

        Assert.AreSame(first, second);
        Assert.AreSame(first.texture, second.texture);
    }

    [Test]
    public void DifferentBoxArgumentsMakeDifferentSprites()
    {
        var factory = new PlaceholderSpriteFactory();
        var bar = Track(factory.CreateBox(14, 2, Color.white, BarPivot));

        var otherSize = Track(factory.CreateBox(16, 2, Color.white, BarPivot));
        var otherColor = Track(factory.CreateBox(14, 2, Color.red, BarPivot));
        var otherPivot = Track(factory.CreateBox(14, 2, Color.white, new Vector2(0.5f, 0f)));

        Assert.AreNotSame(bar, otherSize);
        Assert.AreNotSame(bar, otherColor);
        Assert.AreNotSame(bar, otherPivot);
        Assert.AreEqual(new Rect(0f, 0f, 16f, 2f), otherSize.rect);
        Assert.AreEqual(Color.red, otherColor.texture.GetPixel(0, 0));
        Assert.AreEqual(new Vector2(7f, 0f), otherPivot.pivot);
    }

    private Sprite Track(Sprite sprite)
    {
        _spriteSet.Add(sprite);
        return sprite;
    }
}
