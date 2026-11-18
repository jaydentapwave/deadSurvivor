using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HD2DBackgroundRigTests
{
    [Test]
    public void ResizeAndDisableDestroyOwnedColorDepthAndCameraTarget()
    {
        var root = new GameObject("Background ownership test");
        var source = new RenderTexture(200, 200, 24);
        source.Create();
        var camera = root.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.targetTexture = source;
        var originalMask = camera.cullingMask;
        var rig = root.AddComponent<HD2DBackgroundRig>();
        try
        {
            // The component runs only in Play; invoke its real lifecycle in this EditMode resource test.
            if (rig.Color == null)
            {
                Invoke(rig, "OnEnable");
            }
            var oldColor = rig.Color.rt;
            var oldDepth = rig.Depth.rt;
            camera.targetTexture = null;
            source.Release();
            source.width = 240;
            source.Create();
            camera.targetTexture = source;
            Invoke(rig, "Sync");
            Assert.IsTrue(oldColor == null, "Resizing must destroy the previous owned color texture.");
            Assert.IsTrue(oldDepth == null, "Resizing must destroy the previous owned depth texture.");
            var color = rig.Color.rt;
            var depth = rig.Depth.rt;
            var target = (RenderTexture)typeof(HD2DBackgroundRig).GetField("_target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(rig);
            Invoke(rig, "OnDisable");
            Assert.IsTrue(color == null);
            Assert.IsTrue(depth == null);
            Assert.IsTrue(target == null);
            Assert.IsNull(rig.Color);
            Assert.IsNull(rig.Depth);
            Assert.AreEqual(originalMask, camera.cullingMask);
        }
        finally
        {
            Object.DestroyImmediate(root);
            source.Release();
            Object.DestroyImmediate(source);
        }
    }

    private static void Invoke(HD2DBackgroundRig rig, string method) =>
        typeof(HD2DBackgroundRig).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(rig, null);
}
