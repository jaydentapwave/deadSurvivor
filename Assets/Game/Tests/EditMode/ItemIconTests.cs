using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

// T1·T3·T4 — 실제 32종의 그림과 데이터, 회전한 점유 칸과 그림 UV를 대조한다.
public class ItemIconTests
{
    private const string ArtFolder = "Assets/Game/03.Resources/Art/AIGenerated/Items";

    [Test]
    public void EveryItemHasUniqueAtlasIconWithMatchingCanvasAndOccupiedCells()
    {
        var itemList = new ItemRepository(new ResourceService()).ItemDataList.ToList();
        Assert.AreEqual(32, itemList.Count);
        Assert.AreEqual(itemList.Count, itemList.Select(item => item.Icon).Distinct().Count());
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(
            "Assets/Game/03.Resources/Resources/Art/Characters.spriteatlasv2");
        var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas));
        Assert.IsFalse(importer.packingSettings.enableRotation);
        Assert.IsFalse(importer.packingSettings.enableTightPacking);
        Assert.Contains(AssetDatabase.LoadAssetAtPath<Object>(ArtFolder), atlas.GetPackables());
        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        var service = new SpriteAtlasService(new ResourceService());
        var cellList = new List<Vector2Int>();
        foreach (var item in itemList)
        {
            Assert.IsNotEmpty(item.Icon, item.Id);
            var icon = service.Get(item.Icon);
            Assert.IsNotNull(icon, item.Id);
            Assert.AreSame(icon, service.Get(item.Icon), item.Id);
            var folder = $"{ArtFolder}/{item.Icon}";
            Assert.IsTrue(File.Exists($"{folder}/source.txt"), item.Id);
            var texture = new Texture2D(2, 2);
            Assert.IsTrue(texture.LoadImage(File.ReadAllBytes($"{folder}/{item.Icon}.png")), item.Id);
            BagRules.GetCells(item.Shape, 0, cellList);
            var width = cellList.Max(cell => cell.x) + 1;
            var height = cellList.Max(cell => cell.y) + 1;
            Assert.AreEqual(width * ItemIconUtils.CellPixels, texture.width, item.Id);
            Assert.AreEqual(height * ItemIconUtils.CellPixels, texture.height, item.Id);
            Assert.AreEqual(new Vector2(texture.width, texture.height), icon.rect.size, item.Id);
            var occupiedSet = new HashSet<Vector2Int>();
            var pixels = texture.GetPixels32();
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a > 0)
                    {
                        occupiedSet.Add(new Vector2Int(x / ItemIconUtils.CellPixels,
                            (texture.height - 1 - y) / ItemIconUtils.CellPixels));
                    }
                }
            }

            CollectionAssert.AreEquivalent(cellList, occupiedSet, item.Id);
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var size = ItemIconUtils.GetSize(icon, rotation);
                BagRules.GetCells(item.Shape, rotation, cellList);
                foreach (var cell in cellList)
                {
                    var uv = ItemIconUtils.GetSourceUv(ItemIconUtils.GetCellUv(cell, size).center, rotation);
                    var sourceCell = new Vector2Int(Mathf.FloorToInt(uv.x * width),
                        Mathf.FloorToInt((1f - uv.y) * height));
                    Assert.IsTrue(occupiedSet.Contains(sourceCell), $"{item.Id} rotation {rotation} cell {cell}");
                }
            }

            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(icon);
        }
    }

    [TestCase(0, 0.2f, 0.7f)]
    [TestCase(1, 0.3f, 0.2f)]
    [TestCase(2, 0.8f, 0.3f)]
    [TestCase(3, 0.7f, 0.8f)]
    [TestCase(4, 0.2f, 0.7f)]
    [TestCase(-1, 0.7f, 0.8f)]
    public void SourceUvMatchesClockwiseBagRotation(int rotation, float x, float y)
    {
        var uv = ItemIconUtils.GetSourceUv(new Vector2(0.2f, 0.7f), rotation);
        Assert.AreEqual(x, uv.x, 1e-6f);
        Assert.AreEqual(y, uv.y, 1e-6f);
    }

    [TestCase(100f, 100f, 0, 96f, 32f)]
    [TestCase(100f, 100f, 1, 32f, 96f)]
    [TestCase(10f, 100f, 0, 16f, 16f / 3f)]
    public void WholeIconMeshKeepsIntegralTexelsAspectAndIgnoresInput(float width, float height, int rotation,
        float expectedWidth, float expectedHeight)
    {
        // UI contract: 1080p canvas scale 3, floor to integer screen texels,
        // with a minimum of one screen pixel per source texel (design 4.2).
        var parent = new GameObject("TestIconParent", typeof(RectTransform), typeof(Canvas));
        parent.GetComponent<Canvas>().scaleFactor = 3;
        var view = ItemIconUtils.Create(parent.transform);
        view.rectTransform.anchorMax = Vector2.zero;
        view.rectTransform.sizeDelta = new Vector2(width, height);
        var texture = new Texture2D(48, 16);
        var icon = Sprite.Create(texture, new Rect(0f, 0f, 48f, 16f), Vector2.zero);
        view.UpdateUI(icon, rotation, new Rect(0f, 0f, 1f, 1f), Color.white);
        using (var vh = new VertexHelper())
        {
            Populate(view, vh);
            Assert.AreEqual(4, vh.currentVertCount);
            var a = new UIVertex();
            var b = new UIVertex();
            vh.PopulateUIVertex(ref a, 0);
            vh.PopulateUIVertex(ref b, 2);
            Assert.AreEqual(expectedWidth, b.position.x - a.position.x, 1e-4f);
            Assert.AreEqual(expectedHeight, b.position.y - a.position.y, 1e-4f);
            Assert.IsFalse(view.raycastTarget);
            Assert.AreSame(texture, view.mainTexture);
            view.UpdateUI(null, 0, new Rect(0f, 0f, 1f, 1f), Color.white);
            Populate(view, vh);
            Assert.AreEqual(0, vh.currentVertCount);
            Assert.IsFalse(view.enabled);
        }

        Object.DestroyImmediate(parent);
        Object.DestroyImmediate(icon);
        Object.DestroyImmediate(texture);
    }

    private static void Populate(UI_ItemIconView view, VertexHelper vh) =>
        typeof(UI_ItemIconView).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
            .Invoke(view, new object[] { vh });
}
