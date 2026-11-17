using NUnit.Framework;
using UnityEngine;

// 그림 넣기 AC7 — 대기·피격·이동 움직임(기획서 E절, 아트 기준서 3.10). 모두 정수 픽셀
public class SpriteMotionTests
{
    private const float IdlePeriod = 0.8f;
    private const float MovePeriod = 0.4f;
    private const float FlashSeconds = 0.1f;
    private const float Frame = 1f / 60f;

    [TestCase("Characters/bruno/front/bruno_front")]
    [TestCase("Characters/lena/front/lena_front")]
    [TestCase("Characters/mira/front/mira_front")]
    [TestCase("Characters/bolt/front/bolt_front")]
    [TestCase("Monsters/slime/slime")]
    [TestCase("Monsters/mushroom/mushroom")]
    [TestCase("Monsters/boar/boar")]
    [TestCase("Monsters/rock/rock")]
    [TestCase("Monsters/puffer/puffer")]
    public void ShadowMatchesOpaqueBodyWidth(string path)
    {
        var texture = new Texture2D(2, 2);
        try
        {
            texture.LoadImage(System.IO.File.ReadAllBytes($"Assets/Game/03.Resources/Art/AIGenerated/{path}.png"));
            var width = SpriteMotion.GetVisibleWidth(texture.GetPixels32(), texture.width);
            Assert.That(width, Is.GreaterThan(0).And.LessThan(texture.width));
            var scale = SpriteMotion.GetShadowScale(width, 24);
            Assert.That(scale, Is.GreaterThanOrEqualTo(1));
            Assert.That(24f * scale / width, Is.InRange(0.65f, 1.4f));
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [TestCase(35, 1)]
    [TestCase(36, 1)] // 1.5배는 작은 쪽
    [TestCase(37, 2)]
    [TestCase(47, 2)]
    [TestCase(52, 2)]
    [TestCase(60, 2)] // 2.5배도 작은 쪽
    [TestCase(61, 3)]
    public void ShadowScaleChoosesSmallerAtExactHalf(int bodyWidth, int expected)
    {
        Assert.AreEqual(expected, SpriteMotion.GetShadowScale(bodyWidth, 24));
    }

    [Test]
    public void AllFourMembersUseSameShadowScale()
    {
        var scales = new System.Collections.Generic.List<int>();
        foreach (var character in new CharacterRepository(new ResourceService()).CharacterDataList)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(
                    $"Assets/Game/03.Resources/Art/AIGenerated/Characters/{character.Sprite}/front/{character.Sprite}_front.png"));
                var width = SpriteMotion.GetVisibleWidth(texture.GetPixels32(), texture.width);
                scales.Add(SpriteMotion.GetShadowScale(width, 24));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, scales);
    }

    [Test]
    public void OpaqueWidthIgnoresTransparentMarginsAndIncludesBothEdges()
    {
        var pixels = new Color32[24];
        pixels[9] = new Color32(0, 0, 0, 1);
        pixels[20] = new Color32(0, 0, 0, 255);
        Assert.AreEqual(4, SpriteMotion.GetVisibleWidth(pixels, 8));
        Assert.AreEqual(0, SpriteMotion.GetVisibleWidth(new Color32[8], 8));
        Assert.AreEqual(1, SpriteMotion.GetShadowScale(12, 24));
        Assert.AreEqual(1, SpriteMotion.GetShadowScale(24, 24));
        Assert.AreEqual(2, SpriteMotion.GetShadowScale(48, 24));
    }

    [TestCase(0f, 0)]
    [TestCase(0.39f, 0)]
    [TestCase(0.4f, 1)]
    [TestCase(0.79f, 1)]
    [TestCase(0.8f, 0)]
    [TestCase(1.25f, 1)]
    public void IdleIsZeroThenOnePixelUp(float time, int expected)
    {
        Assert.AreEqual(expected, SpriteMotion.GetIdleOffsetY(time, IdlePeriod, 0f));
    }

    [Test]
    public void IdleStartOffsetShiftsCycle()
    {
        // 대원 2(시작 0.4초 어긋남)는 0초에 이미 뒤 프레임
        Assert.AreEqual(1, SpriteMotion.GetIdleOffsetY(0f, IdlePeriod, 2 * 0.2f));
        Assert.AreEqual(0, SpriteMotion.GetIdleOffsetY(0.4f, IdlePeriod, 2 * 0.2f));
    }

    [TestCase(0f, 0, 0)]
    [TestCase(0.19f, 0, 0)]
    [TestCase(0.2f, 1, -1)]
    [TestCase(0.39f, 1, -1)]
    [TestCase(0.4f, 0, 0)]
    public void MoveHopUpSquashDown(float time, int hop, int squash)
    {
        Assert.AreEqual(hop, SpriteMotion.GetMoveOffsetY(MonsterMoveStyle.Hop, time, MovePeriod, 0f));
        Assert.AreEqual(squash, SpriteMotion.GetMoveOffsetY(MonsterMoveStyle.Squash, time, MovePeriod, 0f));
    }

    [Test]
    public void HitPushesOnePixelAwayFromFacing()
    {
        Assert.AreEqual(-1, SpriteMotion.GetHitOffsetX(true, false));
        Assert.AreEqual(1, SpriteMotion.GetHitOffsetX(true, true));
        Assert.AreEqual(0, SpriteMotion.GetHitOffsetX(false, false));
        Assert.AreEqual(0, SpriteMotion.GetHitOffsetX(false, true));
    }

    [Test]
    public void FlashLastsPointOneSecondsThenOff()
    {
        var flash = new HitFlash();
        flash.Start(FlashSeconds);
        var frames = 0;
        var isEnded = false;
        while (!isEnded)
        {
            Assert.IsTrue(flash.IsOn, $"frame {frames}");
            isEnded = flash.Advance(Frame);
            frames++;
        }

        Assert.IsFalse(flash.IsOn);
        Assert.AreEqual(6, frames);
        Assert.That(flash.Elapsed, Is.InRange(FlashSeconds, FlashSeconds + Frame));
        Assert.AreEqual(0, SpriteMotion.GetHitOffsetX(flash.IsOn, false));
    }

    [Test]
    public void HitDuringFlashRestartsFullDuration()
    {
        var flash = new HitFlash();
        flash.Start(FlashSeconds);
        flash.Advance(0.08f);
        flash.Start(FlashSeconds);

        Assert.AreEqual(FlashSeconds, flash.Remaining, 1e-6f);
        Assert.AreEqual(0f, flash.Elapsed);
        Assert.IsFalse(flash.Advance(0.08f));
        Assert.IsTrue(flash.IsOn);
        Assert.IsTrue(flash.Advance(0.03f));
    }

    [Test]
    public void ClockStopsWhenNotRunning()
    {
        var clock = SpriteMotion.Advance(1f, Frame, true);
        Assert.AreEqual(1f + Frame, clock, 1e-6f);
        Assert.AreEqual(clock, SpriteMotion.Advance(clock, 0.5f, false));

        // 멈춘 시계에서는 대기 프레임도 그대로
        Assert.AreEqual(SpriteMotion.GetIdleOffsetY(clock, IdlePeriod, 0f),
            SpriteMotion.GetIdleOffsetY(SpriteMotion.Advance(clock, 0.5f, false), IdlePeriod, 0f));
    }

    [Test]
    public void OffsetsAreWholePixels()
    {
        var units = SpriteMotion.ToUnits(new Vector2Int(-1, 1));

        Assert.AreEqual(-1f, units.x * Const.PixelsPerUnit, 1e-5f);
        Assert.AreEqual(1f, units.y * Const.PixelsPerUnit, 1e-5f);
        for (var t = 0f; t < 2f; t += 0.013f)
        {
            Assert.That(SpriteMotion.GetIdleOffsetY(t, IdlePeriod, 0f), Is.EqualTo(0).Or.EqualTo(1));
            Assert.That(SpriteMotion.GetMoveOffsetY(MonsterMoveStyle.Squash, t, MovePeriod, 0.37f), Is.EqualTo(0).Or.EqualTo(-1));
        }
    }

    // 쓰러짐·예고 T2·T3 — 예고 중 0.1초 주기로 0 / +1px, 예고가 아니면 0
    [TestCase(0f, 0)]
    [TestCase(0.04f, 0)]
    [TestCase(0.06f, 1)]
    [TestCase(0.12f, 0)]
    [TestCase(0.17f, 1)]
    public void ShakeAlternatesOnlyWhileWarning(float time, int expected)
    {
        Assert.AreEqual(expected, SpriteMotion.GetShakeOffsetX(true, time, 0.1f));
        Assert.AreEqual(0, SpriteMotion.GetShakeOffsetX(false, time, 0.1f));
    }
}
