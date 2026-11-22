using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DQBackgroundTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void TerrainSetupKeepsTransparentQueueAndRestoresOriginalMaterialState(bool initialKeyword, bool shouldFail)
    {
        var root = new GameObject("DQ terrain material lifecycle");
        var field = root.AddComponent<FieldView>();
        var tiles = new GameObject("tiles", typeof(Tilemap), typeof(TilemapRenderer));
        tiles.transform.SetParent(root.transform);
        var surface = new Material(Shader.Find("DeadSurvivor/DQ/Main surface"));
        var water = new Material(Shader.Find("DeadSurvivor/DQ/Main water"));
        surface.renderQueue = -1;
        water.renderQueue = 3000;
        if (initialKeyword)
        {
            surface.EnableKeyword("_TERRAIN_HEIGHT");
            water.EnableKeyword("_TERRAIN_HEIGHT");
        }
        try
        {
            Set(field, "tilemap", tiles.GetComponent<Tilemap>());
            foreach (var name in new[] { "groundMaterial", "bankMaterial", "cardMaterial", "footShadowMaterial", "woodMaterial",
                "bridgeMaterial", "tentMaterial", "ditherSpriteMaterial", "ditherWoodMaterial", "ditherTentMaterial", "flagMaterial", "ditherFlagMaterial" })
            {
                Set(field, name, surface);
            }
            Set(field, "waterMaterial", water);
            var grid = TerrainHeightGrid.FromMillimetres(1, -1, -1, 3, 3, new[] { 100, 110, 120, 110, 120, 130, 120, 130, 140 });
            var layout = new FieldLayoutData { groundHalfDiagonal = 1, banks = shouldFail ? null : Array.Empty<FieldBankLayout>(), water = Array.Empty<FieldWaterLayout>() };
            if (shouldFail)
            {
                Assert.Throws<NullReferenceException>(() => field.BuildLandscape(layout, grid));
                Assert.That(surface.IsKeywordEnabled("_TERRAIN_HEIGHT"), Is.EqualTo(initialKeyword));
                Assert.That(surface.rawRenderQueue, Is.EqualTo(-1));
                Assert.That(field.Landscape, Is.Null);
                Assert.That(root.GetComponentsInChildren<MeshFilter>(), Is.Empty);
            }
            else
            {
                field.BuildLandscape(layout, grid);
                Assert.That(surface.IsKeywordEnabled("_TERRAIN_HEIGHT"), Is.True);
                Assert.That(water.IsKeywordEnabled("_TERRAIN_HEIGHT"), Is.True);
                Assert.That(surface.renderQueue, Is.EqualTo(1990));
            }
            Assert.That(water.renderQueue, Is.EqualTo(3000));
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(surface.IsKeywordEnabled("_TERRAIN_HEIGHT"), Is.EqualTo(initialKeyword));
            Assert.That(water.IsKeywordEnabled("_TERRAIN_HEIGHT"), Is.EqualTo(initialKeyword));
            Assert.That(surface.rawRenderQueue, Is.EqualTo(-1));
            Assert.That(water.rawRenderQueue, Is.EqualTo(3000));
        }
        finally
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            UnityEngine.Object.DestroyImmediate(surface);
            UnityEngine.Object.DestroyImmediate(water);
        }
    }

    [Test]
    public void RoundedDecorativeBankMeetsGroundWithoutAVerticalLip()
    {
        var rectangles = new[] {new Rect(-2,-2,4,4)};
        var mesh = BankMeshMath.Build(rectangles,.25f,.4f,8);
        try
        {
            BankMeshMath.RoundProfile(mesh,null,rectangles,.25f,8);
            var vertices=mesh.vertices;
            Assert.That(Array.Exists(vertices,v=>v.y>.3f),Is.True,"Retain the raised bank interior");
            // Every original side quad must collapse at its upper/lower endpoints.
            var side=mesh.GetTriangles(1);
            foreach(var index in side) Assert.That(vertices[index].y,Is.EqualTo(0).Within(.0001f),"No exposed vertical wall");
        }
        finally {UnityEngine.Object.DestroyImmediate(mesh);}
    }

    private static void Set(FieldView field, string name, object value)
    {
        typeof(FieldView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(field, value);
    }
}
