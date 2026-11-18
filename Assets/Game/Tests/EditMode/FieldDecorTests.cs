using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

// 장식 T1~T5. 에디터 없는 갈래에서 작성만 하며, 합친 뒤 루트 에디터에서 실행한다(D29·D30).
public class FieldDecorTests
{
    private const string DecorFolder = "Assets/Game/03.Resources/Art/AIGenerated/Decor";

    [Test]
    public void GroundAtlasIncludesEveryDecorSpriteAtOriginalScale()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>("Assets/Game/03.Resources/Resources/Art/Ground.spriteatlasv2");
        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        foreach (var path in Directory.GetFiles(DecorFolder, "*.png"))
        {
            var sprite = atlas.GetSprite(Path.GetFileNameWithoutExtension(path));
            Assert.IsNotNull(sprite, path);
            try
            {
                Assert.AreEqual(Const.PixelsPerUnit, sprite.pixelsPerUnit);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
            }
        }
    }





    [TestCase(false, false, 0)]
    [TestCase(false, true, 0)]
    [TestCase(true, false, -90)]
    [TestCase(true, true, -90)]
    public void DecorUsesFootSortingSharedMaterialAndNoCollision(bool isGround, bool isFlipped, int order)
    {
        var gridObject = CreateField(out var field);
        try
        {
            var placement = JsonUtility.FromJson<FieldDecorPlacement>("{\"sprite\":\"fence_01\",\"x\":17,\"y\":14," +
                "\"pivotOffsetXPixels\":8,\"isGround\":" + isGround.ToString().ToLowerInvariant() +
                ",\"isFlipped\":" + isFlipped.ToString().ToLowerInvariant() + "}");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{DecorFolder}/fence_01.png");
            var renderer = field.AddDecor(sprite, placement);
            var cell = IsoProjection.IsoCellToFloor(placement.Cell);
            var foot = new Vector3(cell.x, .001f, cell.y);
            Assert.AreEqual(order, renderer.sortingOrder);
            Assert.AreEqual(0, renderer.sortingLayerID);
            Assert.AreEqual(SpriteSortPoint.Pivot, renderer.spriteSortPoint);
            Assert.AreSame(field.Tilemap.GetComponent<TilemapRenderer>().sharedMaterial, renderer.sharedMaterial);
            Assert.AreEqual(Vector3.one, renderer.transform.localScale);
            Assert.AreEqual(foot.y, renderer.transform.position.y);
            Assert.That(renderer.transform.position.x - foot.x,
                Is.EqualTo((isFlipped ? -8f : 8f) / Const.PixelsPerUnit).Within(0.00001f));
            Assert.AreEqual(isFlipped, renderer.flipX);
            Assert.IsEmpty(renderer.GetComponentsInChildren<Collider2D>());
            Assert.IsEmpty(renderer.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(renderer.GetComponentsInChildren<Rigidbody2D>());
        }
        finally
        {
            Object.DestroyImmediate(gridObject);
        }
    }



    private static GameObject CreateField(out FieldView field)
    {
        var root = new GameObject("DecorTestGrid");
        var grid = root.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = new Vector3(1f, 0.5f, 1f);
        var child = new GameObject("Field");
        child.transform.SetParent(root.transform, false);
        var tilemap = child.AddComponent<Tilemap>();
        tilemap.tileAnchor = Vector3.zero;
        child.AddComponent<TilemapRenderer>();
        field = child.AddComponent<FieldView>();
        var serialized = new SerializedObject(field);
        serialized.FindProperty("tilemap").objectReferenceValue = tilemap;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }
}
