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
            Assert.Greater(field.Landscape.GetComponentsInChildren<MeshRenderer>().Length, 600);
            Assert.IsEmpty(field.Landscape.GetComponentsInChildren<UprightFootShadow>());
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
