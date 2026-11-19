using NUnit.Framework;
using UnityEngine;

public class GroundDecalMathTests
{
    [Test]
    public void TC1TC3_DecalPreservesXZAndUVAndFollowsTerrain()
    {
        foreach (var grid in new[] { TerrainHeightGrid.Flat, TerrainHeightMathTests.Plane(.2f, 0) })
        {
            var mesh = GroundDecalMath.Build(grid, new Vector3(2, 0, 3), new Vector2(2, 1), 30, .25f, .001f);
            try
            {
                var vertices = mesh.vertices;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var p = vertices[i];
                    Assert.AreEqual(TerrainHeightMath.Sample(grid, p.x, p.z) + .001f, p.y, 1e-6);
                    var expected = new Vector3(2, 0, 3) + Quaternion.Euler(0, 30, 0) * new Vector3((mesh.uv[i].x - .5f) * 2, 0, mesh.uv[i].y - .5f);
                    Assert.AreEqual(expected.x, p.x, 1e-6);
                    Assert.AreEqual(expected.z, p.z, 1e-6);
                }
                Assert.AreEqual(Vector2.zero, mesh.uv[0]);
                Assert.AreEqual(Vector2.one, mesh.uv[mesh.uv.Length - 1]);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
    [Test]
    public void TC2TC4_RidgeProjectionAndShadowFragmentsMatchTheContract()
    {
        var grid = TerrainHeightGrid.FromMillimetres(1, -1, -1, 3, 3, new[] { 40, 40, 40, 0, 0, 0, 40, 40, 40 });
        var point = new Vector3(0, -.0182f, .48f);
        var gap = TerrainHeightMath.RayGroundDepthGap(grid, point, Mathf.PI / 6);
        Assert.AreEqual(.06995348f, gap, .000001f);
        Assert.IsFalse(.49f <= .4f || gap <= .04f, "old depth cutoff rejects this fragment");
        Assert.IsTrue(TerrainHeightMath.ShadowFragmentSurvives(1, .49f, 0, 0, false));
        Assert.AreEqual(.07182389f, TerrainHeightMath.RayGroundDepthGap(grid, point - Vector3.up * .001f, Mathf.PI / 6), .000001f);
        var random = new System.Random(8);
        for (var i = 0; i < 500; i++)
        {
            var angle = (float)random.NextDouble() * Mathf.PI * 2;
            var radius = Mathf.Sqrt((float)random.NextDouble()) * .5f;
            var z = -.01f + Mathf.Sin(angle) * radius;
            var p = new Vector3(Mathf.Cos(angle) * radius, .0004f - .04f * (z + .01f) + .001f, z);
            Assert.IsTrue(TerrainHeightMath.ShadowFragmentSurvives(1, radius, 0, 0, false));
            Assert.LessOrEqual(TerrainHeightMath.RayGroundDepthGap(grid, p, Mathf.PI / 6), .1225f);
        }
        var shader = System.IO.File.ReadAllText("Assets/Game/03.Resources/Shaders/HD2D/HD2D_GroundShadow.shader");
        StringAssert.DoesNotContain("SampleSceneDepth", shader);
        StringAssert.DoesNotContain("_CameraDepthTexture", shader);
        StringAssert.Contains("ZTest Always", shader);
        var mesh = GroundDecalMath.Build(grid, new Vector3(0, 0, -.01f), Vector2.one, 0);
        try
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            for (var i = 0; i < t.Length; i += 3)
            {
                var p = (v[t[i]] + v[t[i+1]] + v[t[i+2]]) / 3;
                Assert.LessOrEqual(Mathf.Abs(p.y - .002f - TerrainHeightMath.Sample(grid, p.x, p.z)), .01f);
            }
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void TC5_GroundEffectPairsPreserveOriginalShadeAndAlphaClip()
    {
        foreach (var key in new[] { "Boom", "Heal", "Pulse", "Warn" })
        {
            var path = "Assets/Game/03.Resources/Materials/HD2D/HD2D_EffectGround_" + key;
            var original = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
            var pair = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path + "_Terrain.mat");
            Assert.AreEqual(original.shader, pair.shader);
            Assert.AreEqual(original.GetFloat("_CoreIntensity"), pair.GetFloat("_CoreIntensity"));
            Assert.AreEqual(original.GetFloat("_BodyIntensity"), pair.GetFloat("_BodyIntensity"));
            Assert.AreEqual(original.GetFloat("_ZWrite"), pair.GetFloat("_ZWrite"));
            Assert.AreEqual(8, pair.GetFloat("_ZTest"));
            Assert.AreEqual(1996, pair.renderQueue);
            foreach (var color in new[] { Color.white, new Color(.97f,.96f,.9f,1), new Color(.4f,.9f,.5f,1), new Color(.95f,.95f,.95f,1) })
            {
                foreach (var alpha in new[] { .49f, .5f, .51f })
                {
                    var input = color.linear;
                    input.a = alpha;
                    var a = EffectGlowMath.Shade(input, original.GetFloat("_CoreIntensity"), original.GetFloat("_BodyIntensity"), out var shownA);
                    var b = EffectGlowMath.Shade(input, pair.GetFloat("_CoreIntensity"), pair.GetFloat("_BodyIntensity"), out var shownB);
                    Assert.AreEqual(alpha >= .5f, shownA);
                    Assert.AreEqual(shownA, shownB);
                    Assert.AreEqual(a, b);
                }
            }
        }
        var white = EffectGlowMath.Shade(Color.white, 2.2f, 1, out _);
        Assert.AreEqual(2.2f, white.r, 1e-6);
        var body = EffectGlowMath.Shade(new Color(.4f,.9f,.5f,1).linear, 2.2f, 1, out _);
        Assert.AreEqual(new Color(.4f,.9f,.5f,1).linear.g, body.g, 1e-6);
    }
}
