using NUnit.Framework;
public class PixelScaleMathTests
{
    [TestCase(720,2)] [TestCase(1080,2)] [TestCase(1170,2)] [TestCase(1284,3)]
    [TestCase(1440,3)] [TestCase(1536,3)] [TestCase(2048,4)] [TestCase(200,1)]
    public void S1S2IntegerScale(int height,int expected)
    {
        var scale=PixelScaleMath.Scale(height,.1f,48,1);
        Assert.AreEqual(expected,scale);
        Assert.AreEqual(height/(96f*expected),PixelScaleMath.OrthographicSize(height,48,scale),1e-6);
    }
}
