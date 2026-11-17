using NUnit.Framework;
using UnityEngine;
public class SpriteFacing8Tests
{
    [TestCase(0,"right")] [TestCase(-45,"down-right")] [TestCase(-90,"down")]
    [TestCase(45,"up-right")] [TestCase(90,"up")] [TestCase(180,"left")]
    [TestCase(-135,"down-left")] [TestCase(135,"up-left")]
    public void D1EightDirections(float degrees,string label)
    {
        var delta=new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad));
        Assert.AreEqual(label,SpriteFacing8.Next(SpriteFacing8.Front,delta,.001f,0).Label);
    }
    [Test] public void D2BoundaryHold()
    {
        var last=new SpriteFacing8(FacingDirection.Right,false);
        Vector2 At(float d) => new(Mathf.Cos(d*Mathf.Deg2Rad),Mathf.Sin(d*Mathf.Deg2Rad));
        Assert.AreEqual("right",SpriteFacing8.Next(last,At(-30),.001f,12).Label);
        Assert.AreEqual("down-right",SpriteFacing8.Next(last,At(-35),.001f,12).Label);
    }
    [Test] public void D3D4KeepLastFlip()
    {
        var last=new SpriteFacing8(FacingDirection.DownRight,true);
        Assert.AreEqual(last,SpriteFacing8.Next(last,new Vector2(.001f,.001f),.01f,12));
        var up=SpriteFacing8.Next(last,Vector2.up,.001f,0);
        Assert.AreEqual(FacingDirection.Up,up.Direction); Assert.IsTrue(up.IsFlipped);
    }
    [Test] public void D5NearestAvailableTieFacesFront()
    {
        var mask=(1<<(int)FacingDirection.DownRight)|(1<<(int)FacingDirection.UpRight);
        Assert.AreEqual(FacingDirection.DownRight,SpriteFacing8.ResolveAvailable(FacingDirection.Down,mask));
        Assert.AreEqual(FacingDirection.DownRight,SpriteFacing8.ResolveAvailable(FacingDirection.Right,mask));
        Assert.AreEqual(FacingDirection.UpRight,SpriteFacing8.ResolveAvailable(FacingDirection.Up,mask));
        foreach(FacingDirection direction in System.Enum.GetValues(typeof(FacingDirection)))
            Assert.AreEqual(FacingDirection.DownRight,SpriteFacing8.ResolveAvailable(direction,1<<(int)FacingDirection.DownRight));
    }
}
