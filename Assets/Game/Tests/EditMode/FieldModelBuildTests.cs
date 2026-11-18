using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class FieldModelBuildTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void AuthoredStoneCurvesAndWallTransformsSurvivePlacementReordering(bool reverse)
    {
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"));
        var placements = layout.placements.Where(p => p.group.StartsWith("wall_")).ToArray();
        var baseline = WallSnapshot(layout, placements);
        var reordered = (FieldLayoutPlacement[])placements.Clone();
        if (reverse)
        {
            System.Array.Reverse(reordered);
        }
        else
        {
            (reordered[0], reordered[1]) = (reordered[1], reordered[0]);
        }
        CollectionAssert.AreEqual(baseline, WallSnapshot(layout, reordered),
            "The same placements must produce the same position, yaw, scale and shared prefab material");
    }

    private static string[] WallSnapshot(FieldLayoutData layout, FieldLayoutPlacement[] placements)
    {
        EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var field = Object.FindFirstObjectByType<FieldView>();
        field.Initialize(30);
        field.BuildLandscape(layout);
        try
        {
            foreach (var p in placements)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Decor/" + p.sprite + ".png");
                field.AddCard(sprite, p, Vector3.down);
            }
            // Only these placements take part in this isolated scenery build; source JSON is untouched.
            field.FinishScenery(new FieldLayoutData { seed = layout.seed, placements = placements });
            var roots = field.Landscape.Cast<Transform>().Where(t => t.name.StartsWith("3D:wall_")).ToArray();
            var stones = roots.Where(t => t.name.StartsWith("3D:wall_edge_")).ToArray();
            Assert.AreEqual(19, stones.Length, "All authored curve stones must remain individual stones");
            Assert.AreEqual(7, stones.Count(t => t.name.StartsWith("3D:wall_edge_3:")));
            Assert.IsTrue(stones.Any(t => Vector3.Distance(t.localPosition, new Vector3(1.741f, 0, -15.710f)) < .00001f),
                "The original middle of wall_edge_3 must not collapse onto an endpoint chord");
            foreach (var p in placements.Where(p => p.group.StartsWith("wall_edge_")))
            {
                var atSource = stones.Where(t => t.name.StartsWith("3D:" + p.group + ":")
                    && Vector3.Distance(t.localPosition, new Vector3(p.x, p.height, p.y)) < .00001f).ToArray();
                Assert.AreEqual(1, atSource.Length, p.group);
                Assert.That(atSource[0].GetComponentInChildren<MeshRenderer>().sharedMaterial.name, Does.StartWith("stone_v"));
            }
            return roots.Select(t => t.name + "|" + t.localPosition.ToString("F6") + "|" + t.localRotation.ToString("F6")
                    + "|" + t.localScale.ToString("F6") + "|" + t.GetComponentInChildren<MeshRenderer>().sharedMaterial.name)
                .OrderBy(s => s, System.StringComparer.Ordinal).ToArray();
        }
        finally
        {
            Object.DestroyImmediate(field.Landscape.gameObject);
            EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        }
    }

    [Test]
    public void ModelsReplaceEveryCardWithoutFootShadowsAndMatchBushScreenFootprint()
    {
        EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var field = Object.FindFirstObjectByType<FieldView>();
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"));
        Assert.IsTrue(field.UsesModelScenery);
        field.Initialize(30);
        field.BuildLandscape(layout);
        try
        {
            foreach (var p in layout.placements.Where(p => p.kind == "card"))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Decor/" + p.sprite + ".png");
                var legacy = field.AddCard(sprite, p, Vector3.down);
                Assert.IsFalse(legacy.enabled, p.sprite);
                Assert.IsNull(legacy.GetComponent<UprightFootShadow>());
            }
            field.FinishScenery(layout);
            field.BuildProps(layout,
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Decor/flag_01.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Common/shadow.png"));
            Assert.IsFalse(field.Landscape.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Flag cloth"));
            Assert.AreEqual(3, field.Landscape.GetComponentsInChildren<MeshRenderer>().Count(r => r.name == "Flag cloth"));
            Assert.IsFalse(field.Landscape.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled));
            Assert.IsTrue(field.Landscape.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Wood")
                .All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On));
            Assert.Greater(field.Landscape.GetComponentsInChildren<MeshRenderer>(true).Length, 600);
            Assert.IsEmpty(field.Landscape.GetComponentsInChildren<UprightFootShadow>());
            var plants = field.Landscape.Cast<Transform>().Where(t => t.name.StartsWith("3D:flowers:")).ToArray();
            var batches = field.Landscape.Cast<Transform>().Where(t => t.name.StartsWith("3D plant batch ")).ToArray();
            Assert.IsTrue(plants.All(t => !t.gameObject.activeSelf));
            Assert.Greater(batches.Length, 0);
            Assert.Less(batches.Length, plants.Length);
            var plantTriangles = plants.SelectMany(t => t.GetComponentsInChildren<MeshFilter>(true)).Sum(f => f.sharedMesh.triangles.Length);
            Assert.AreEqual(plantTriangles, batches.Sum(t => t.GetComponent<MeshFilter>().sharedMesh.triangles.Length));
            Assert.IsTrue(batches.All(t => t.GetComponent<MeshRenderer>().shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));

            var bush = layout.placements.First(p => p.sprite.StartsWith("bush_"));
            var root = field.Landscape.Cast<Transform>().First(t => t.name.StartsWith("3D:bush:") && Vector3.Distance(t.localPosition, new Vector3(bush.x, bush.height, bush.y)) < .001f);
            var points = root.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices.Select(f.transform.TransformPoint)).ToArray();
            var size = FieldModelMath.ProjectedSize(points, 0, 30);
            var source = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Decor/" + bush.sprite + ".png");
            Assert.That(size.x, Is.EqualTo(source.bounds.size.x).Within(.001));
            Assert.That(size.y, Is.EqualTo(source.bounds.size.y).Within(.001));
        }
        finally
        {
            Object.DestroyImmediate(field.Landscape.gameObject);
            EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        }
    }
}
