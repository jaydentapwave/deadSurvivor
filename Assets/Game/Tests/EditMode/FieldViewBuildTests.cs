using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FieldViewBuildTests
{
    [Test]
    public void PcgPropsKeepTheirBakedPositionRotationScaleAndNoCollision()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var field = Object.FindFirstObjectByType<FieldView>();
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"));
        field.Initialize(30);
        try
        {
            field.BuildLandscape(layout);
            field.BuildProps(layout,
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Decor/flag_01.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Common/shadow.png"));
            foreach (var kind in new[] { "bridge", "tent", "fence", "sign", "flag" })
            {
                var placements = layout.placements.Where(p => p.prop == kind).ToArray();
                var roots = field.Landscape.Cast<Transform>().Where(t => t.name == kind).ToArray();
                Assert.AreEqual(placements.Length, roots.Length, kind);
                for (var i = 0; i < roots.Length; i++)
                {
                    Assert.That(Vector3.Distance(new Vector3(placements[i].x, placements[i].height, placements[i].y), roots[i].localPosition), Is.LessThan(.0001f));
                    Assert.That(Quaternion.Angle(Quaternion.Euler(0, placements[i].yaw, 0), roots[i].localRotation), Is.LessThan(.01f));
                    Assert.AreEqual(Vector3.one * placements[i].scale, roots[i].localScale);
                }
            }
            Assert.IsEmpty(field.Landscape.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(field.Landscape.GetComponentsInChildren<Collider2D>());
        }
        finally
        {
            Object.DestroyImmediate(field.Landscape.gameObject);
            EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        }
    }

    [Test]
    public void ZoneMaskUsesAllChannelsWithoutFilteringOrColorConversion()
    {
        const string path = "Assets/Game/03.Resources/Textures/HD2D/FieldZoneMask.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.IsFalse(importer.sRGBTexture);
        Assert.IsFalse(importer.mipmapEnabled);
        Assert.IsFalse(importer.alphaIsTransparency);
        Assert.AreEqual(FilterMode.Point, importer.filterMode);
        Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
        Assert.AreEqual(4096, importer.maxTextureSize);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/HD2D_Field_Ground.mat");
        Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(path), material.GetTexture("_ZoneMask"));
    }

    [Test]
    public void LandscapeKeepsTileDataAndBuildsOwnedPartsWithoutCollision()
    {
        var root = new GameObject("FieldBuildTest");
        root.AddComponent<Grid>();
        var child = new GameObject("Tiles");
        child.transform.SetParent(root.transform,false);
        var tilemap = child.AddComponent<Tilemap>();
        var tileRenderer = child.AddComponent<TilemapRenderer>();
        var field = child.AddComponent<FieldView>();
        var tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            var so = new SerializedObject(field);
            so.FindProperty("tilemap").objectReferenceValue=tilemap;
            var groundMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/HD2D_Field_Ground.mat");
            so.FindProperty("groundMaterial").objectReferenceValue=groundMaterial;
            so.ApplyModifiedPropertiesWithoutUndo();
            tilemap.SetTile(Vector3Int.zero,tile);
            field.BuildLandscape(JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json")));
            Assert.IsFalse(tileRenderer.enabled);
            Assert.AreSame(tile,tilemap.GetTile(Vector3Int.zero));
            Assert.AreEqual(1,field.PartCount("ground"));
            Assert.AreEqual(1,field.PartCount("bank"));
            foreach (Transform part in field.Landscape)
                if (part.name=="bank") Assert.AreSame(groundMaterial,part.GetComponent<Renderer>().sharedMaterials[0]);
            Assert.AreEqual(1,groundMaterial.GetFloat("_FlatDepth"),"Shared top retains D-2 depth");
            Assert.AreEqual(1,field.PartCount("pond"));
            Assert.AreEqual(1,field.PartCount("stream"));
            Assert.AreEqual(1,field.PartCount("waterfall"));
            Assert.AreEqual(1,field.PartCount("stream-upper"));
            var pond = field.Landscape.Find("pond").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(4, pond.vertexCount, "Organic water is revealed through the mask, independent of the legacy pond asset");
            Assert.That(pond.bounds.center.y, Is.EqualTo(-.015f).Within(.00001f));
            var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"));
            Assert.AreEqual(BankMeshMath.Cells(layout.banks[0].runs).Count * 6, field.Landscape.Find("bank").GetComponent<MeshFilter>().sharedMesh.GetTriangles(0).Length);
            Assert.AreEqual(Quaternion.identity,field.Landscape.rotation);
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(root.GetComponentsInChildren<Collider2D>());
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(tile);
        }
    }
    [Test]
    public void CardsStayUprightAtNativeScaleAndUseSharedMaterial()
    {
        var root = new GameObject("Card field test");
        var field = root.AddComponent<FieldView>();
        var child = new GameObject("Tiles");
        child.transform.SetParent(root.transform, false);
        var tiles = child.AddComponent<Tilemap>();
        child.AddComponent<TilemapRenderer>();
        try
        {
            var so = new SerializedObject(field);
            so.FindProperty("tilemap").objectReferenceValue = tiles;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/HD2D_SpriteLit.mat");
            so.FindProperty("cardMaterial").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
            field.Initialize(30);
            field.BuildLandscape(JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json")));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Decor/grass_tuft_01.png");
            var card = field.AddCard(sprite, new FieldLayoutPlacement { x = 5, y = -4, group = "flowers", sprite = "grass_tuft_01" }, Vector3.down);
            Assert.That(card.transform.eulerAngles.x, Is.EqualTo(30).Within(.0001));
            Assert.AreEqual(Vector3.one, card.transform.localScale);
            Assert.AreSame(material, card.sharedMaterial);
            Assert.AreEqual(48, card.sprite.pixelsPerUnit);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, card.shadowCastingMode);
            Assert.IsEmpty(card.GetComponentsInChildren<Collider>());
            var bushSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Decor/bush_02.png");
            var bush = field.AddCard(bushSprite, new FieldLayoutPlacement { x = 4, y = 2, group = "bush", sprite = "bush_02" }, Vector3.down);
            Assert.IsTrue(field.Occluders.Any(prop => prop.Anchor == bush.transform), "Bushes must participate in actor occlusion fading");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
    [Test]
    public void DT8_LoadingBoundaryLogsAndRethrowsInvalidHeight()
    {
        var method = typeof(GameSceneLifetimeScope).GetMethod("CreateTerrainGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("\\[GameSceneLifetimeScope\\] Invalid field_height"));
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, new object[] { new FieldHeightData { cell = 1, width = 2, depth = 2, heightsMm = new int[3] } }));
        Assert.IsInstanceOf<System.ArgumentException>(error.InnerException);
    }
}
