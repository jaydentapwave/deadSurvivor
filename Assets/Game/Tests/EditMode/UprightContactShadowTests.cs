using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class UprightContactShadowTests
{
    [Test]
    public void ActualOpaqueBottomTexelsStayOnSupportAcrossPoseFlipAndMovement()
    {
        var root = new GameObject("Contact fixture");
        var sun = new GameObject("Contact sun").AddComponent<Light>();
        var oldSun = RenderSettings.sun;
        sun.transform.rotation = Quaternion.Euler(32f, 135f, 0f);
        RenderSettings.sun = sun;
        var body = new GameObject("Body").AddComponent<SpriteRenderer>();
        body.transform.SetParent(root.transform, false);
        body.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var colors = new Color32[64];
        colors[1 * 8 + 2] = colors[1 * 8 + 5] = new Color32(255, 255, 255, 255);
        colors[4 * 8 + 3] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(colors);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, 0f), 48f);
        var secondTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var secondColors = new Color32[64];
        secondColors[2 * 8 + 1] = secondColors[2 * 8 + 6] = new Color32(255, 255, 255, 255);
        secondTexture.SetPixels32(secondColors);
        secondTexture.Apply();
        var secondSprite = Sprite.Create(secondTexture, new Rect(0, 0, 8, 8), new Vector2(.5f, 0f), 48f);
        body.sprite = sprite;
        var shadow = root.AddComponent<UprightContactShadow>();
        Mesh mesh = null;
        try
        {
            root.transform.position = new Vector3(1.25f, 0f, 2.5f);
            shadow.Initialize(body, UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/03.Resources/Materials/HD2D/HD2D_ContactShadow.mat"));
            mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
            foreach (var isFlipped in new[] { false, true })
            {
                body.sprite = isFlipped ? secondSprite : sprite;
                body.flipX = isFlipped;
                body.transform.localPosition = new Vector3(0f, 1f / 48f, 0f);
                typeof(UprightContactShadow).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shadow, null);
                var vertices = mesh.vertices;
                var uvs = mesh.uv;
                var contactIndices = new System.Collections.Generic.List<int>();
                for (var i = 0; i < uvs.Length; i++)
                {
                    if (uvs[i].x < 0f)
                    {
                        contactIndices.Add(i);
                    }
                    Assert.AreEqual(.002f, shadow.GetComponentInChildren<MeshFilter>().transform.TransformPoint(vertices[i]).y, 1e-5f);
                }
                Assert.AreEqual(8, contactIndices.Count, "Only the two opaque pixels in the lowest nonempty row bridge to the support.");
                var along = new Vector3(sun.transform.forward.x, 0f, sun.transform.forward.z).normalized;
                for (var foot = 0; foot < 2; foot++)
                {
                    var i = contactIndices[foot * 4];
                    var midpoint = shadow.GetComponentInChildren<MeshFilter>().transform.TransformPoint((vertices[i] + vertices[i + 1]) * .5f) + along * .04f;
                    var pixelX = isFlipped ? (foot == 0 ? 1.5f : 6.5f) : (foot == 0 ? 2.5f : 5.5f);
                    var expected = body.transform.TransformPoint(new Vector3((pixelX - 4f) / 48f * (isFlipped ? -1f : 1f), (isFlipped ? 2.5f : 1.5f) / 48f, 0f));
                    expected.y = .002f;
                    Assert.That(Vector3.Distance(expected, midpoint), Is.LessThan(1e-5f));
                }
                root.transform.position += new Vector3(1f / 48f, 0f, 1f / 24f);
            }
            shadow.Initialize(body, shadow.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            Assert.AreSame(mesh, shadow.GetComponentInChildren<MeshFilter>().sharedMesh);
            typeof(UprightContactShadow).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shadow, null);
            Assert.IsTrue(mesh == null);
            Object.DestroyImmediate(root);
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
            RenderSettings.sun = oldSun;
            Object.DestroyImmediate(sun.gameObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(secondSprite);
            Object.DestroyImmediate(secondTexture);
        }
    }
}
