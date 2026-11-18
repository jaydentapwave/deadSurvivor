using NUnit.Framework;
using UnityEngine;
public class CardSnapMathTests
{
    [TestCase(37.5f,0f,30f)]
    [TestCase(26f,0f,30f)]
    [TestCase(21.5f,0f,45f)]
    public void NativeCornerLandsOnBothScreenAxes(float pivotX,float pivotY,float tilt)
    {
        var pivot=new Vector2(pivotX,pivotY);
        var p=CardSnapMath.Snap(new Vector3(2.137f,.361f,-3.142f),pivot,48,tilt);
        var rotation=Quaternion.Euler(tilt,0,0);
        var corner=p+rotation*new Vector3(-pivot.x/48,-pivot.y/48,0);
        Assert.That(corner.x*48,Is.EqualTo(Mathf.Round(corner.x*48)).Within(.0001));
        var y=Vector3.Dot(corner,rotation*Vector3.up)*48;
        Assert.That(y,Is.EqualTo(Mathf.Round(y)).Within(.0001));
        Assert.AreEqual(.361f,p.y);
        Assert.Less(Vector3.Distance(p,CardSnapMath.Snap(p,pivot,48,tilt)),.00001f);
    }
}
