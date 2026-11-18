using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class FieldModelImportTests
{
    private const string Path = "Assets/Game/03.Resources/Models/HD2D/Scenery/FieldModelCatalog.asset";

    [Test]
    public void ImportRepeatsWithoutChangingGuidsOrSourceNormalsAndWiresAllSixteenPrefabs()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<FieldModelCatalog>(Path);
        Assert.AreEqual(16, catalog.Entries.Length);
        var paths = AssetDatabase.GetDependencies(Path, true);
        var guids = paths.ToDictionary(p => p, AssetDatabase.AssetPathToGUID);
        Type.GetType("HD2DSceneryMigration, Game.Editor").GetMethod("Import").Invoke(null, null);
        foreach (var p in paths)
        {
            Assert.AreEqual(guids[p], AssetDatabase.AssetPathToGUID(p), p);
        }
        foreach (var entry in catalog.Entries)
        {
            Assert.Greater(entry.Bounds.size.y, .1f, entry.Id);
            Assert.That(entry.Bounds.min.y, Is.EqualTo(0).Within(.003), entry.Id);
            Assert.AreEqual(5, entry.Material.GetFloat("_Category"));
            Assert.AreSame(entry.Material.GetTexture("_MainTex"), entry.FadedMaterial.GetTexture("_MainTex"));
            Assert.Greater(entry.FadedMaterial.GetFloat("_Dither"), 0);
            var renderer = entry.Prefab.GetComponentInChildren<MeshRenderer>();
            Assert.IsTrue(entry.Material == renderer.sharedMaterial, entry.Id + " must reference the same Unity material asset");
            Assert.AreEqual(HD2DBackgroundRig.BackgroundLayer, renderer.gameObject.layer);
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Game/03.Resources/Models/HD2D/Scenery/" + entry.Id + ".fbx");
            Assert.AreEqual(ModelImporterNormals.Import, importer.importNormals);
            var texture = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(entry.Material.GetTexture("_MainTex")));
            Assert.AreEqual(FilterMode.Point, texture.filterMode);
            Assert.IsFalse(texture.mipmapEnabled);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, texture.textureCompression);
            Assert.IsEmpty(entry.Prefab.GetComponentsInChildren<Collider>());
        }
    }
}
