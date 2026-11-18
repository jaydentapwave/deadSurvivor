using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FieldViewBuildTests
{
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
            so.FindProperty("pondMesh").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/03.Resources/Meshes/HD2D/FieldPond.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            tilemap.SetTile(Vector3Int.zero,tile);
            field.BuildLandscape(JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json")));
            Assert.IsFalse(tileRenderer.enabled);
            Assert.AreSame(tile,tilemap.GetTile(Vector3Int.zero));
            Assert.AreEqual(1,field.PartCount("ground"));
            Assert.AreEqual(2,field.PartCount("bank"));
            foreach (Transform part in field.Landscape)
                if (part.name=="bank") Assert.AreSame(groundMaterial,part.GetComponent<Renderer>().sharedMaterials[0]);
            Assert.AreEqual(1,groundMaterial.GetFloat("_FlatDepth"),"Shared top retains D-2 depth");
            Assert.AreEqual(1,field.PartCount("pond"));
            Assert.AreEqual(1,field.PartCount("stream"));
            Assert.AreEqual(1,field.PartCount("waterfall"));
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
            so.FindProperty("pondMesh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/03.Resources/Meshes/HD2D/FieldPond.asset");
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
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
