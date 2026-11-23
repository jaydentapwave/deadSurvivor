using NUnit.Framework;
using UnityEngine;
[Category("DQScreen")]
public class TiltedCameraRigTests
{
    [TestCase(false)] [TestCase(true)]
    public void C1C2FocusAndShakeOrderDoesNotChangeRestoredFocus(bool shakeFirst)
    {
        var go=new GameObject("camera rig",typeof(Camera));
        try {
            var rig=go.AddComponent<TiltedCameraRig>();rig.Initialize(30,20,.3f,60,.1f,1);
            var focus=new Vector3(1.013f,0,2.03f); var shake=new Vector2(.1f,.2f);
            if(shakeFirst) {rig.SetShakeOffset(shake);rig.SetFocus(focus);} else {rig.SetFocus(focus);rig.SetShakeOffset(shake);}
            Assert.Less(Error(go.transform,rig,focus),.0148f);
            rig.SetFocus(focus+Vector3.right); // C4 pause: a retained shake follows the new focus.
            Assert.Less(Error(go.transform,rig,focus+Vector3.right),.0148f);
            rig.SetShakeOffset(Vector2.zero); // C3 end.
            Assert.Less(Error(go.transform,rig,focus+Vector3.right),.0148f);
            go.transform.position+=Vector3.right; // C5 real transform failure must be observed.
            Assert.Greater(Error(go.transform,rig,focus+Vector3.right),.021f);
        } finally {Object.DestroyImmediate(go);}
    }
    private static float Error(Transform camera,TiltedCameraRig rig,Vector3 expected)
    {
        var diff=camera.position+camera.forward*rig.Distance-camera.right*rig.ShakeOffset.x-camera.up*rig.ShakeOffset.y-Vector3.up*TiltedCameraRig.FocusHeight-expected;
        return new Vector2(Vector3.Dot(diff,camera.right),Vector3.Dot(diff,camera.up)).magnitude;
    }
    [Test]
    public void TS3_TerrainFocusRemainsOnTheSnappedScreenPlane()
    {
        var go = new GameObject("Terrain camera", typeof(Camera), typeof(TiltedCameraRig));
        try
        {
            var rig = go.GetComponent<TiltedCameraRig>();
            rig.Initialize(30, 50, .1f, 100, .1f, 1);
            var grid = TerrainHeightMathTests.Plane(0, .2f);
            var foot = TerrainHeightMath.SnapOnTerrain(grid, 0, .02f, .5f, 48);
            rig.SetFocus(foot);
            var restored = go.transform.position + go.transform.forward * rig.Distance - Vector3.up * TiltedCameraRig.FocusHeight;
            Assert.AreEqual(.03094649f, restored.z, .000004f);
            Assert.Less(Vector3.Distance(restored, foot), .000005f);
            var unsnapped = IsoProjection.FloorToWorld(new Vector2(0, .02f), 1, grid);
            Assert.LessOrEqual(TiltedViewMath.WorldToScreenPlane(restored - unsnapped, .5f).magnitude, .0147f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
