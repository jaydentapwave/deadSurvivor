using NUnit.Framework;
using UnityEngine;

public class OcclusionMathTests
{
    [TestCase(2, 3, true)]
    [TestCase(3, 2, false)]
    [TestCase(2, 2, false)]
    [TestCase(-2, 3, false)]
    public void RequiresForegroundAndPositiveDepth(float propDepth, float memberDepth, bool expected) =>
        Assert.AreEqual(expected, OcclusionMath.Covers(new Rect(0, 0, 2, 2), propDepth, new Rect(1, 1, 2, 2), memberDepth));

    [Test]
    public void TouchingEdgesAndSeparatedBoxesDoNotOcclude()
    {
        Assert.IsFalse(OcclusionMath.Covers(new Rect(0,0,1,1),1,new Rect(1,0,1,1),2));
        Assert.IsFalse(OcclusionMath.Covers(new Rect(0,0,1,1),1,new Rect(2,2,1,1),2));
    }

    [Test]
    public void ProjectsAllCornersOfTiltedBounds()
    {
        var go = new GameObject("Projection camera");
        try
        {
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.aspect = 1;
            camera.transform.SetPositionAndRotation(new Vector3(0, 5, -10), Quaternion.Euler(30,0,0));
            var bounds = new Bounds(Vector3.zero, new Vector3(2,4,2));
            var rect = OcclusionMath.ProjectBounds(camera,bounds);
            Assert.AreEqual(.2f,rect.width,.0001f);
            Assert.AreEqual((4 * Mathf.Cos(Mathf.PI/6) + 2 * .5f)/10,rect.height,.0001f);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
