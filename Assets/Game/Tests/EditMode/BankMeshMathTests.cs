using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class BankMeshMathTests
{
    [Test]
    public void BankIsDeterministicHasOnlyExposedWallsAndFlatTop()
    {
        var rects = new[] { new Rect(-2,-1,4,2) };
        var a = BankMeshMath.Build(rects,.125f,.36f,181108);
        var b = BankMeshMath.Build(rects,.125f,.36f,181108);
        try
        {
            CollectionAssert.AreEqual(a.vertices,b.vertices);
            CollectionAssert.AreEqual(a.triangles,b.triangles);
            Assert.AreEqual(2,a.subMeshCount);
            Assert.IsTrue(a.GetTriangles(0).All(i => Mathf.Approximately(a.vertices[i].y,.36f)));
            Assert.Greater(a.GetTriangles(1).Length,0);
            Assert.That(a.bounds.size.y,Is.EqualTo(.36f).Within(.00001f));
            Assert.Greater(BankMeshMath.Cells(rects,.125f,181108).Count,300);
        }
        finally
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }

    [Test]
    public void DifferentSeedChangesOuterWalkNotHeight()
    {
        var rects = new[] { new Rect(0,0,4,2) };
        Assert.IsFalse(BankMeshMath.Cells(rects,.125f,1).SetEquals(BankMeshMath.Cells(rects,.125f,2)));
        var mesh = RibbonMeshMath.Build(new[] { Vector2.zero,Vector2.up },1.3f,.36f,-.03f);
        try
        {
            Assert.That(mesh.bounds.size.x,Is.EqualTo(1.3f).Within(.0001));
            Assert.That(mesh.bounds.min.y,Is.EqualTo(-.03f).Within(.0001));
            Assert.That(mesh.bounds.max.y,Is.EqualTo(.36f).Within(.0001));
            Assert.IsTrue(mesh.normals.All(n=>n.y>0));
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }
}
