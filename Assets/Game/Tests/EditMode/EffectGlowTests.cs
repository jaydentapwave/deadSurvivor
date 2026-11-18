using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EffectGlowTests
{
    [Test]
    public void OnlyTwoWhitePaletteColorsEmitAboveBody()
    {
        var colors = new[] { "ffffff", "fff9ed", "ffe08a", "f8d818", "f8c838", "f8b828", "f88808", "d81808", "77211d", "a89898", "786868", "a8c828", "7fa040", "7fd0c8", "b07fd0", "7a4a9e", "e3d7c8", "34140e", "c44334", "e4b67e", "774c3b" };
        var texture = new Texture2D(colors.Length, 1, TextureFormat.RGBA32, false);
        var pixels = new Color[colors.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            ColorUtility.TryParseHtmlString("#" + colors[i], out pixels[i]);
        }
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, colors.Length, 1), Vector2.one * .5f, 1);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/HD2D_EffectGlow_Fire.mat");
        ShaderUtil.CompilePass(material, 0, true);
        Assert.IsTrue(ShaderUtil.IsPassCompiled(material, 0));
        var probeMaterial = new Material(material);
        probeMaterial.mainTexture = texture;
        var mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(-1, -1), new Vector3(-1, 1), new Vector3(1, 1), new Vector3(1, -1) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        var root = new GameObject("EffectGlowShaderTest");
        var cameraObject = new GameObject("EffectGlowCamera");
        var target = new RenderTexture(colors.Length * 4, 4, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
        var previous = RenderTexture.active;
        try
        {
            root.layer = 31;
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            var camera = cameraObject.AddComponent<Camera>();
            var extra = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            extra.renderPostProcessing = false;
            extra.volumeLayerMask = 0;
            extra.SetRenderer(0);
            camera.orthographic = true;
            camera.orthographicSize = .5f;
            camera.aspect = colors.Length;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            camera.targetTexture = target;
            var commands = new UnityEngine.Rendering.CommandBuffer();
            try
            {
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.black);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                var properties = new MaterialPropertyBlock();
                properties.SetVector("unity_SpriteProps", Vector4.one);
                properties.SetColor("unity_SpriteColor", Color.white);
                commands.DrawMesh(mesh, Matrix4x4.identity, probeMaterial, 0, 0, properties);
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally
            {
                commands.Release();
            }
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply();
            System.IO.File.WriteAllText("output/hd2d-fx/verification/F1-gpu-pixels.txt", string.Join("\n", System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, colors.Length), i => colors[i] + "=" + readback.GetPixel(i * 4 + 2, 2).ToString("F4"))));
            for (var i = 0; i < colors.Length; i++)
            {
                var value = readback.GetPixel(i * 4 + 2, 2).maxColorComponent;
                var source = QualitySettings.activeColorSpace == ColorSpace.Linear ? pixels[i].linear.maxColorComponent : pixels[i].maxColorComponent;
                Assert.AreEqual(source * (i < 2 ? 2.4f : 1f), value, .035f, colors[i]);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(probeMaterial);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(readback);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
