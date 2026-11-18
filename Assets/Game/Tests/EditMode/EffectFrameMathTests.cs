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
}
