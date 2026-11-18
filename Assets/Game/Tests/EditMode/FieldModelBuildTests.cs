using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class FieldModelBuildTests
{
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
