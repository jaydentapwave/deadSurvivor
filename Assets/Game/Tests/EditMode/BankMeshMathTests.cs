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
    public void ActualFUnionKeepsDeepJoinCellsAcrossSeeds()
    {
        var rects = new[] { new Rect(-6.75f,8.25f,5.625f,1), new Rect(-5.5f,6,2,2.5f) };
        foreach (var seed in new[] { 181108, 1, 7, 98765 })
        {
            var cells = BankMeshMath.Cells(rects,.125f,seed);
            Assert.IsTrue(cells.Contains(new Vector2Int(-42,66)), "review B3 left join");
            Assert.IsTrue(cells.Contains(new Vector2Int(-31,66)), "review B3 right join");
            bool Original(int x,int y) => rects.Any(r=>x>=Mathf.RoundToInt(r.xMin/.125f) && x<Mathf.RoundToInt(r.xMax/.125f) && y>=Mathf.RoundToInt(r.yMin/.125f) && y<Mathf.RoundToInt(r.yMax/.125f));
            for (var y=48;y<74;y++)
            for (var x=-54;x<-9;x++)
            {
                var deep=true;
                for (var dy=-2;dy<=2;dy++)
                for (var dx=-2;dx<=2;dx++) deep &= Original(x+dx,y+dy);
                if (deep) Assert.IsTrue(cells.Contains(new Vector2Int(x,y)), $"deep union {x},{y}, seed{seed}");
            }
            Assert.IsTrue(cells.All(c=>Original(c.x,c.y)), "Only remove; do not expand the union");
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
