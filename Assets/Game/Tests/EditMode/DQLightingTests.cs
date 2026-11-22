using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DQLightingTests
{
    [Test]
    public void BlobTriangleInteriorsFollowValleyCrease()
    {
        var root=new GameObject("crease");var body=new GameObject("body").AddComponent<SpriteRenderer>();body.transform.SetParent(root.transform,false);
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/DQ_BlobShadow.mat");
        var grid=TerrainHeightGrid.FromMillimetres(1,-1,-1,3,3,new[]{200,0,200,200,0,200,200,0,200});
        var blob=root.AddComponent<DQBlobShadow>();
        try {
            foreach(var x in new[]{-.23f,0,.17f}) {
                root.transform.position=new Vector3(x,TerrainHeightMath.Sample(grid,x,0),0);blob.Initialize(body,material,.9f);blob.SetGround(grid,material);
                var mesh=blob.OwnedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;var transform=blob.GetComponentInChildren<MeshFilter>().transform;
                for(var i=0;i<indices.Length;i+=3) for(var a=1;a<8;a++) for(var b=1;b<8-a;b++) {
                    var u=a/8f;var v=b/8f;var p=transform.TransformPoint(vertices[indices[i]]*u+vertices[indices[i+1]]*v+vertices[indices[i+2]]*(1-u-v));
                    Assert.AreEqual(TerrainHeightMath.Sample(grid,p.x,p.z)+.002f,p.y,1e-5,"triangle interior bridges terrain crease");
                }
            }
        } finally {Object.DestroyImmediate(root);}
    }

    [Test]
    public void MainGlobalsRestoreAtCameraEndBeforeOtherCamera()
    {
        var root=new GameObject("camera",typeof(Camera));var other=new GameObject("other",typeof(Camera));
        var beforeFocus=Shader.GetGlobalVector("_DQLightFocus");var beforeAir=Shader.GetGlobalVector("_DQAirColor");
        var focus=new Vector4(2,3,4,5);var air=new Vector4(.1f,.2f,.3f,1);var context=default(ScriptableRenderContext);
        var component=root.AddComponent<DQSceneLighting>();
        void Invoke(string method,Camera camera)=>typeof(DQSceneLighting).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(component,new object[]{context,camera});
        try {Shader.SetGlobalVector("_DQLightFocus",focus);Shader.SetGlobalVector("_DQAirColor",air);
            Invoke("OnCameraRendering",root.GetComponent<Camera>());Assert.AreNotEqual(focus,Shader.GetGlobalVector("_DQLightFocus"));
            Invoke("OnCameraRendered",root.GetComponent<Camera>());Invoke("OnCameraRendering",other.GetComponent<Camera>());
            Assert.AreEqual(focus,Shader.GetGlobalVector("_DQLightFocus"));Assert.AreEqual(air,Shader.GetGlobalVector("_DQAirColor"));
        } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(other);Shader.SetGlobalVector("_DQLightFocus",beforeFocus);Shader.SetGlobalVector("_DQAirColor",beforeAir);}
    }

    [Test]
    public void AtlasRectTracksFrameTextureAndPreservesAppearance()
    {
        var root = new GameObject("rect fixture");
        var renderer = root.AddComponent<SpriteRenderer>();
        var texture = new Texture2D(32, 16);
        var a = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.zero, 48);
        var b = Sprite.Create(texture, new Rect(16, 0, 8, 8), Vector2.zero, 48);
        MaterialPropertyBlock properties = null;
        var initial = new MaterialPropertyBlock();
        initial.SetFloat("_Appearance", .37f);
        renderer.SetPropertyBlock(initial);
        try
        {
            foreach (var sprite in new[] { a, b, a })
            {
                foreach (var flip in new[] { false, true })
                {
                    renderer.flipX = flip;
                    DQSpriteLighting.Apply(renderer, sprite, ref properties);
                    renderer.GetPropertyBlock(initial);
                    Assert.AreEqual(.37f, initial.GetFloat("_Appearance"));
                    var uv = initial.GetVector("_DQSpriteUVRect");
                    Assert.AreEqual((sprite.textureRect.xMin + .5f) / 32, uv.x, 1e-6f);
                    Assert.AreEqual((sprite.textureRect.xMax - .5f) / 32, uv.z, 1e-6f);
                    Assert.AreEqual(.5f / 16, uv.y, 1e-6f);
                    Assert.AreSame(sprite, renderer.sprite);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void BlobReusesMeshAndSharedResourcesAcrossSlopePoolAndDestruction()
    {
        var root = new GameObject("blob fixture");
        var body = new GameObject("body").AddComponent<SpriteRenderer>();
        body.transform.SetParent(root.transform, false);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/DQ_BlobShadow.mat");
        Assert.NotNull(material);
        var blob = root.AddComponent<DQBlobShadow>();
        var grid = TerrainHeightGrid.FromMillimetres(1, -1, -1, 3, 3, new[] { 0, 100, 200, 0, 100, 200, 0, 100, 200 });
        blob.Initialize(body, material, .8f);
        var mesh = blob.OwnedMesh;
        try
        {
            foreach (var position in new[] { Vector3.zero, new Vector3(.2f, .12f, .1f), new Vector3(-.2f, .08f, 0) })
            {
                root.transform.position = position;
                blob.SetGround(grid, material);
                Assert.AreSame(material, blob.SharedMaterial);
                Assert.AreSame(mesh, blob.OwnedMesh);
                var filter = root.GetComponentInChildren<MeshFilter>();
                foreach (var vertex in mesh.vertices)
                {
                    var world = filter.transform.TransformPoint(vertex);
                    var support = position.y - TerrainHeightMath.Sample(grid, position.x, position.z);
                    Assert.AreEqual(TerrainHeightMath.Sample(grid, world.x, world.z) + support + .002f, world.y, 1e-5);
                }
            }
            root.SetActive(false);
            Assert.IsFalse(root.GetComponentInChildren<MeshRenderer>(true).enabled);
            root.SetActive(true);
            blob.Initialize(body, material, .9f);
            blob.SetGround(grid, material);
            Assert.AreSame(mesh, blob.OwnedMesh);
            Assert.IsTrue(root.GetComponentInChildren<MeshRenderer>().enabled);
            Assert.AreEqual(.9f, mesh.bounds.size.x, 1e-5);
            body.enabled = false;
            typeof(DQBlobShadow).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(blob, null);
            Assert.IsFalse(root.GetComponentInChildren<MeshRenderer>().enabled);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        Assert.IsTrue(mesh == null);
        Assert.IsTrue(material != null, "Actor cannot destroy the persistent shared resource.");
    }

    [Test]
    public void FieldSuppliesSamePersistentMaterialOnFlatAndRaisedTerrain()
    {
        var root = new GameObject("field fixture");
        var field = root.AddComponent<FieldView>();
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/DQ_BlobShadow.mat");
        typeof(FieldView).GetField("dqBlobShadowMaterial", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(field, material);
        try
        {
            foreach (var terrain in new[] { TerrainHeightGrid.Flat, TerrainHeightGrid.FromMillimetres(1, 0, 0, 2, 2, new[] { 0, 100, 100, 200 }) })
            {
                typeof(FieldView).GetField("_terrain", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(field, terrain);
                Assert.AreSame(material, field.TerrainShadowMaterial);
                Assert.IsNull(typeof(FieldView).GetField("_terrainShadowMaterial", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(field));
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        Assert.IsTrue(material != null);
    }

    [Test]
    public void RendererRequiresDepthAndDrawsIndicatorsAfterFocus()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Game/03.Resources/Settings/DQ_MainRenderer.asset");
        var focus = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().Single();
        var indicators = renderer.rendererFeatures.OfType<RenderObjects>().Single();
        Assert.AreEqual(ScriptableRenderPassInput.Depth, focus.requirements);
        Assert.IsTrue(focus.fetchColorBuffer);
        Assert.Less(renderer.rendererFeatures.IndexOf(focus), renderer.rendererFeatures.IndexOf(indicators));
        Assert.AreEqual(600, (int)focus.injectionPoint);
        Assert.AreEqual(600, (int)indicators.settings.Event);
        Assert.AreEqual(0, new SerializedObject(renderer).FindProperty("m_CopyDepthMode").enumValueIndex);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Game/03.Resources/Settings/DQ_LightVolume.asset");
        Assert.IsTrue(profile.TryGet<Vignette>(out var vignette));
        Assert.IsTrue(vignette.active, "Zero must override the project default volume, not be skipped.");
        Assert.IsTrue(vignette.intensity.overrideState);
        Assert.IsFalse(vignette.IsActive());
        Assert.AreEqual(0, vignette.intensity.value);
    }

    [Test]
    public void HeroLightProfilesUsePersistentMaterialAssignmentsAndRejectMissingArt()
    {
        var profiles = AssetDatabase.LoadAssetAtPath<DQCharacterLightProfiles>("Assets/Game/03.Resources/Materials/HD2D/DQ_CharacterLightProfiles.asset");
        Assert.NotNull(profiles);
        CollectionAssert.AreEquivalent(new[] { "bruno", "lena", "mira", "bolt" }, profiles.Entries.Select(e => e.ActorId));
        foreach (var entry in profiles.Entries)
        {
            Assert.AreSame(entry.Material, profiles.Find(entry.ActorId));
            Assert.IsTrue(AssetDatabase.Contains(entry.Material));
            Assert.AreEqual(1, entry.Material.GetFloat("_DQLighting"));
            Assert.Greater(entry.Material.GetFloat("_DQExposure"), 0);
            Assert.AreEqual(0, entry.Material.GetFloat("_Emission"));
        }
        Assert.Throws<System.ArgumentException>(() => profiles.Find("missing"));
    }

    [Test]
    public void SavedMainPersistsAllFourHeroProfileReferences()
    {
        var path = "Assets/Game/03.Resources/Materials/HD2D/DQ_CharacterLightProfiles.asset";
        var guid = AssetDatabase.AssetPathToGUID(path);
        Assert.IsNotEmpty(guid);
        var scene = System.IO.File.ReadAllText("Assets/Game/01.Scene/Main.unity");
        var links = System.Text.RegularExpressions.Regex.Matches(scene, @"dqLightProfiles: \{fileID: 11400000, guid: " + guid + @", type: 2\}");
        Assert.AreEqual(4, links.Count, "A created asset and unchanged file hashes cannot prove the saved scene is wired.");
    }

    [Test]
    public void AlphaAssetHasContinuousCoverageAndBilinearImport()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Game/03.Resources/Art/HD2D/DQAppliedCharacters/FootSoft.png");
        Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
        Assert.IsFalse(importer.sRGBTexture);
        var texture = new Texture2D(2, 2);
        try
        {
            texture.LoadImage(System.IO.File.ReadAllBytes("Assets/Game/03.Resources/Art/HD2D/DQAppliedCharacters/FootSoft.png"));
            var coverage = texture.GetPixels32().Select(c => c.a).Distinct().ToArray();
            Assert.Greater(coverage.Length, 100);
            Assert.Contains((byte)0, coverage);
            Assert.Contains((byte)255, coverage);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }
}
