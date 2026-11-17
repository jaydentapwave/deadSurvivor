using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class UiArtLayoutTests
{
    [TestCase(22)]
    [TestCase(24)]
    [TestCase(25)]
    [TestCase(26)]
    [TestCase(27)]
    [TestCase(48)]
    [TestCase(97)]
    [TestCase(150)]
    [TestCase(640)]
    public void PanelMeshHasCompleteStitchesAcrossTwelvePixelSeams(int size)
    {
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes("Assets/Game/03.Resources/Art/AIGenerated/UI/panel.png"));
        var sprite = Sprite.Create(texture, new Rect(0, 0, 48, 48), Vector2.one / 2, 48);
        var go = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.rectTransform.sizeDelta = new Vector2(size, size + 1);
        var surface = go.AddComponent<UI_ArtSurface>();
        surface.Initialize("panel");
        var vertices = new VertexHelper();
        try
        {
            surface.ModifyMesh(vertices);
            var stitchColor = texture.GetPixel(8, 2);
            // 실제 메시를 뒤에서부터 샘플링한다(위상 보정 메시가 9-slice 위를 덮는다).
            for (var axis = 0; axis < 2; axis++)
            {
                var length = axis == 0 ? size : size + 1;
                for (var side = 0; side < 2; side++)
                {
                    var rail = side == 0 ? 2 : (axis == 0 ? size + 1 : size) - 3;
                    var run = 0;
                    var count = 0;
                    for (var p = 8; p < length - 8; p++)
                    {
                        var point = axis == 0 ? new Vector2(p + 0.5f, rail + 0.5f) : new Vector2(rail + 0.5f, p + 0.5f);
                        point -= new Vector2(size, size + 1) / 2;
                        var isStitch = Sample(vertices, texture, point) == stitchColor;
                        if (isStitch)
                        {
                            run++;
                            count++;
                        }
                        else if (run > 0)
                        {
                            Assert.That(run, Is.EqualTo(2), $"{size} axis {axis} side {side} p {p}");
                            run = 0;
                        }
                    }

                    Assert.That(run == 0 || run == 2, Is.True, $"{size} axis {axis} side {side} last run {run}");
                    Assert.That(count, Is.EqualTo(2 * ((length - 14) / 4)));
                }
            }

            // 장식 8x8의 네 모서리는 저작본 픽셀과 같다.
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    foreach (var corner in new[] { Vector2Int.zero, new Vector2Int(size - 8, 0), new Vector2Int(0, size - 7), new Vector2Int(size - 8, size - 7) })
                    {
                        var point = new Vector2(corner.x + x + 0.5f, corner.y + y + 0.5f) - new Vector2(size, size + 1) / 2;
                        var sourceX = corner.x == 0 ? x : 40 + x;
                        var sourceY = corner.y == 0 ? y : 40 + y;
                        Assert.That(Sample(vertices, texture, point), Is.EqualTo(texture.GetPixel(sourceX, sourceY)));
                    }
                }
            }
        }
        finally
        {
            vertices.Dispose();
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [TestCase(12)]
    [TestCase(15)]
    [TestCase(16)]
    [TestCase(17)]
    [TestCase(40)]
    public void SmallPanelQuadsCoverRectWithoutInversionOrOverlap(int size)
    {
        var texture = new Texture2D(48, 48);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 48, 48), Vector2.one / 2, 48);
        var go = new GameObject("SmallPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        var surface = go.AddComponent<UI_ArtSurface>();
        surface.Initialize("panel");
        using (var vertices = new VertexHelper())
        {
            try
            {
                foreach (var dimensions in new[] { new Vector2(size, 40), new Vector2(40, size), new Vector2(size, size) })
                {
                    image.rectTransform.sizeDelta = dimensions;
                    surface.ModifyMesh(vertices);
                    var a = new UIVertex();
                    var c = new UIVertex();
                    var area = 0f;
                    // 처음 25조각은 5×5 바탕. 땀은 그 뒤에 덧그린다.
                    for (var i = 0; i < 100; i += 4)
                    {
                        vertices.PopulateUIVertex(ref a, i);
                        vertices.PopulateUIVertex(ref c, i + 2);
                        var width = c.position.x - a.position.x;
                        var height = c.position.y - a.position.y;
                        Assert.That(width, Is.GreaterThanOrEqualTo(0));
                        Assert.That(height, Is.GreaterThanOrEqualTo(0));
                        Assert.That(a.position.x, Is.InRange(-dimensions.x / 2, dimensions.x / 2));
                        Assert.That(c.position.x, Is.InRange(-dimensions.x / 2, dimensions.x / 2));
                        Assert.That(a.position.y, Is.InRange(-dimensions.y / 2, dimensions.y / 2));
                        Assert.That(c.position.y, Is.InRange(-dimensions.y / 2, dimensions.y / 2));
                        area += width * height;
                    }

                    Assert.That(area, Is.EqualTo(dimensions.x * dimensions.y).Within(0.001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }

    [TestCase(0, "0")]
    [TestCase(7, "7")]
    [TestCase(101, "101")]
    [TestCase(987654321, "987654321")]
    [TestCase(int.MaxValue, "2147483647")]
    public void DamageDigitsKeepZeroesAndOrder(int value, string expected)
    {
        Assert.That(UiArtLayout.Digits(value), Is.EqualTo(expected));
    }

    [TestCase("panel", 12, 12)]
    [TestCase("button", 4, 4)]
    [TestCase("bag_cell", 3, 3)]
    [TestCase("hp_bar_bg", 4, 2)]
    [TestCase("cooldown_bg", 2, 1)]
    [TestCase("hp_bar_fill", 0, 0)]
    public void BordersUseSourceContractWithoutImporterChanges(string key, int horizontal, int vertical)
    {
        Assert.That(UiArtLayout.Border(key), Is.EqualTo(new Vector4(horizontal, vertical, horizontal, vertical)));
    }

    [Test]
    public void WorldHpArtKeepsF6FillDimensionsAndAddsFixedFrameOutside()
    {
        var texture = new Texture2D(32, 6);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 6), new Vector2(0, 0.5f), 48, 0,
            SpriteMeshType.FullRect, new Vector4(4, 2, 4, 2));
        var back = new GameObject("Back").AddComponent<SpriteRenderer>();
        var fill = new GameObject("Fill").AddComponent<SpriteRenderer>();
        try
        {
            var size = new Vector2(14, 2) / Const.PixelsPerUnit;
            var offset = new Vector2(-7, 50) / Const.PixelsPerUnit;
            UiBarUtils.Initialize(back, fill, sprite, sprite, size, offset, Color.gray, Color.green);
            Assert.That(fill.size, Is.EqualTo(size));
            Assert.That((Vector2)fill.transform.localPosition, Is.EqualTo(offset));
            Assert.That(back.size.x * Const.PixelsPerUnit, Is.EqualTo(22).Within(0.001f));
            Assert.That(back.size.y * Const.PixelsPerUnit, Is.EqualTo(6).Within(0.001f));
            Assert.That(back.transform.localPosition.x * Const.PixelsPerUnit, Is.EqualTo(-11).Within(0.001f));
            Assert.That(fill.color, Is.EqualTo(Color.green));
        }
        finally
        {
            Object.DestroyImmediate(back.gameObject);
            Object.DestroyImmediate(fill.gameObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void DamageDigitRenderersReusePoolAndKeepF6HeightAndFade()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<DamageNumberView>(
            "Assets/Game/03.Resources/Resources/Prefab/DamageNumberView.prefab");
        var view = Object.Instantiate(prefab);
        try
        {
            var sprites = new Sprite[10];
            for (var i = 0; i < sprites.Length; i++)
            {
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Game/03.Resources/Art/AIGenerated/UI/digits/digit_{i}.png");
            }

            view.Initialize(101, Color.white, 8, sprites);
            var renderers = view.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(renderers.Length, Is.EqualTo(10));
            Assert.That(renderers[0].sprite, Is.SameAs(sprites[1]));
            Assert.That(renderers[1].sprite, Is.SameAs(sprites[0]));
            Assert.That(renderers[2].sprite, Is.SameAs(sprites[1]));
            Assert.That(renderers[3].enabled, Is.False);
            Assert.That(renderers[0].transform.localScale.y * 9, Is.EqualTo(8).Within(0.001f));
            view.UpdateView(Vector2.one, 0.25f);
            Assert.That(renderers[0].color.a, Is.EqualTo(0.25f));
            view.Initialize(int.MaxValue, Color.white, 8, sprites);
            Assert.That(view.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(10));
            Assert.That(renderers[9].enabled, Is.True);
            view.Initialize(0, Color.white, 8, sprites);
            Assert.That(renderers[0].sprite, Is.SameAs(sprites[0]));
            Assert.That(renderers[1].enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(view.gameObject);
        }
    }

    [Test]
    public void SceneArtMenuLeavesNoStaticPlaceholderImagesAndKeepsSafeAreaParents()
    {
        const string path = "Assets/Game/01.Scene/Main.unity";
        var previous = SceneManager.GetActiveScene();
        var main = SceneManager.GetSceneByPath(path);
        var wasLoaded = main.isLoaded;
        if (!wasLoaded)
        {
            main = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var type = Type.GetType("UiArtSceneUtils, Game.Editor", true);
            foreach (var root in main.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    var clone = Object.Instantiate(canvas.gameObject);
                    SceneManager.MoveGameObjectToScene(clone, preview);
                    type.GetMethod("ApplyToCanvas").Invoke(null, new object[] { clone.GetComponent<Canvas>() });
                    var safe = clone.transform.Find("SafeArea");
                    Assert.That(safe, Is.Not.Null);
                    foreach (var name in new[] { "LobbyView", "BattleHudView", "PrepView", "ResultView" })
                    {
                        Assert.That(safe.Find(name), Is.Not.Null, name);
                    }

                    foreach (var image in clone.GetComponentsInChildren<Image>(true))
                    {
                        // 이 두 종류만 기존 Presenter가 아틀라스 그림으로 바꾼다.
                        if (image.name == "Icon" || image.name == "Image" && image.GetComponentInParent<UI_CharacterCardView>() != null)
                        {
                            continue;
                        }

                        Assert.That(image.sprite, Is.Not.Null, image.name);
                        Assert.That(AssetDatabase.GetAssetPath(image.sprite), Does.StartWith("Assets/Game/03.Resources/Art/AIGenerated/UI/"));
                    }
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (!wasLoaded)
            {
                EditorSceneManager.CloseScene(main, true);
            }

            SceneManager.SetActiveScene(previous);
        }
    }

    private static Color Sample(VertexHelper vertices, Texture2D texture, Vector2 point)
    {
        var a = new UIVertex();
        var c = new UIVertex();
        for (var i = vertices.currentVertCount - 4; i >= 0; i -= 4)
        {
            vertices.PopulateUIVertex(ref a, i);
            vertices.PopulateUIVertex(ref c, i + 2);
            if (point.x < a.position.x || point.y < a.position.y || point.x >= c.position.x || point.y >= c.position.y)
            {
                continue;
            }

            var u = Mathf.Lerp(a.uv0.x, c.uv0.x, (point.x - a.position.x) / (c.position.x - a.position.x));
            var v = Mathf.Lerp(a.uv0.y, c.uv0.y, (point.y - a.position.y) / (c.position.y - a.position.y));
            return texture.GetPixel(Mathf.FloorToInt(u * texture.width), Mathf.FloorToInt(v * texture.height));
        }

        Assert.Fail($"No mesh at {point}");
        return default;
    }
}
