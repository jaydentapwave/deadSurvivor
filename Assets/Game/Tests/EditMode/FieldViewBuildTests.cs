using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FieldViewBuildTests
{
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
            so.FindProperty("pondMesh").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/03.Resources/Meshes/HD2D/FieldPond.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            tilemap.SetTile(Vector3Int.zero,tile);
            field.BuildLandscape(JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json")));
            Assert.IsFalse(tileRenderer.enabled);
            Assert.AreSame(tile,tilemap.GetTile(Vector3Int.zero));
            Assert.AreEqual(1,field.PartCount("ground"));
            Assert.AreEqual(2,field.PartCount("bank"));
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
}
