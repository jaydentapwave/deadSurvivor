using NUnit.Framework;

public class EffectFrameMathTests
{
    [TestCase(-1f, 0)]
    [TestCase(0f, 0)]
    [TestCase(.049f, 0)]
    [TestCase(.05f, 1)]
    [TestCase(.1f, 2)]
    [TestCase(.15f, 3)]
    [TestCase(.2f, 4)]
    [TestCase(.25f, 4)]
    [TestCase(3f, 4)]
    public void SingleShotClampsAtLastFrame(float elapsed, int frame)
    {
        Assert.AreEqual(frame, EffectFrameMath.FrameAt(elapsed, 5, 20, false));
    }

    [TestCase(0f, 0)]
    [TestCase(.25f, 0)]
    [TestCase(.34f, 1)]
    public void LoopWrapsAtDuration(float elapsed, int frame)
    {
        Assert.AreEqual(frame, EffectFrameMath.FrameAt(elapsed, 3, 12, true));
    }
    [Test]
    public void LargeElapsedDoesNotOverflowTheFrameIndex()
    {
        Assert.AreEqual(4, EffectFrameMath.FrameAt(float.MaxValue, 5, 20, false));
        Assert.That(EffectFrameMath.FrameAt(float.MaxValue, 3, 12, true), Is.InRange(0, 2));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => EffectFrameMath.FrameAt(1, 3, float.NaN, true));
    }

    [TestCase(0, 0, false, false)]
    [TestCase(45, 1, false, false)]
    [TestCase(90, 2, false, false)]
    [TestCase(135, 1, true, false)]
    [TestCase(180, 0, true, false)]
    [TestCase(-135, 1, true, true)]
    [TestCase(-90, 2, false, true)]
    [TestCase(-45, 1, false, true)]
    public void DirectionTableUsesBakedArtAndIndependentExpectedFlips(float angle, int index, bool x, bool y)
    {
        var actual = EffectFrameMath.DirectionSprite(angle);
        Assert.AreEqual(index, actual.index);
        Assert.AreEqual(x, actual.flipX);
        Assert.AreEqual(y, actual.flipY);
    }

}
