using NUnit.Framework;
using UnityEngine;

public class FieldLayoutMathTests
{
    [TestCase(20.75f,11.25f,true)]
    [TestCase(21f,11.25f,false)]
    [TestCase(10f,22f,true)]
    [TestCase(0f,24f,false)]
    public void VisibleBoundary(float x, float y, bool expected) => Assert.AreEqual(expected, FieldLayoutMath.IsVisible(new Vector2(x,y)));

    [Test]
    public void PushUsesOutwardXAndPreservesDepth()
    {
        var path = new[] { Vector2.zero, new Vector2(1,1) };
        var right = FieldLayoutMath.PushOutside(new Vector2(.2f,0),path,3);
        var left = FieldLayoutMath.PushOutside(new Vector2(-.2f,0),path,3);
        Assert.GreaterOrEqual(FieldLayoutMath.PathDistance(right,path),3);
        Assert.Less(left.x,0);
        Assert.AreEqual(0,right.y);
        Assert.AreEqual(0,left.y);
    }

    [Test]
    public void DensityChecksWindowsAwayFromOriginAndInclusiveEdges()
    {
        Assert.AreEqual(3, FieldLayoutMath.MaxWindowCount(new[] { new Vector2(30,20), new Vector2(40,30), new Vector2(50,42.5f), new Vector2(-30,-20) }));
        Assert.AreEqual(0,FieldLayoutMath.MaxWindowCount(new Vector2[0]));
    }
}
