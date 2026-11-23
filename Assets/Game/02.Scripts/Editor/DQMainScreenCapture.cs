using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Captures the actual Main camera; UI-inclusive captures remain in the unchanged playable runner.
public static class DQMainScreenCapture
{
    public const string Output = "output/hd2d-dq/apply/5/frames";
    public static void Capture(string label, int width, int height)
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MainSceneDQScreenMigration.ScenePath)
        {
            throw new InvalidOperationException("[DQMainScreenCapture] Requires Main Play.");
        }
        Directory.CreateDirectory(Output);
        var camera = Camera.main;
        var aspect = camera.aspect;
        var active = RenderTexture.active;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var path = Output + "/" + label + "-" + width + "x" + height;
        try
        {
            camera.aspect = (float)width / height;
            var actors = Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None)
                .Where(v => v.gameObject.activeInHierarchy).Select(v => Describe(v.BodyRenderer, camera, width, height)).ToArray();
            // Measurement anchors are declared before rendering, never hand-picked from the image.
            File.WriteAllText(path + "-before.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount,
                camera = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z }, pitch = camera.transform.eulerAngles.x, fov = camera.fieldOfView,
                orthographic = camera.orthographic, width, height, actors,
                near = camera.nearClipPlane, far = camera.farClipPlane,
                pixelRect = new[] { camera.pixelRect.x, camera.pixelRect.y, camera.pixelRect.width, camera.pixelRect.height },
                viewMatrix = MatrixValues(camera.worldToCameraMatrix), projectionMatrix = MatrixValues(camera.projectionMatrix),
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], pipeline = UniversalRenderPipeline.asset.name,
                renderScale = UniversalRenderPipeline.asset.renderScale
            }, new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore }));
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.aspect = aspect;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);
        }
    }

    private static float[] MatrixValues(Matrix4x4 matrix) => Enumerable.Range(0, 16).Select(i => matrix[i]).ToArray();

    private static object Describe(SpriteRenderer body, Camera camera, int width, int height)
    {
        var sprite = body.sprite;
        var name = sprite.name.Replace("(Clone)", "");
        var files = Directory.GetFiles("Assets/Game/03.Resources/Art", name + ".png", SearchOption.AllDirectories);
        if (files.Length != 1)
        {
            throw new InvalidOperationException("[DQMainScreenCapture] Expected unique source PNG for " + name);
        }
        var texture = new Texture2D(2, 2);
        try
        {
            texture.LoadImage(File.ReadAllBytes(files[0]));
            var min = new Vector2(texture.width, texture.height);
            var max = Vector2.zero;
            var pixels = texture.GetPixels32();
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a >= 128)
                    {
                        min = Vector2.Min(min, new Vector2(x, y));
                        max = Vector2.Max(max, new Vector2(x + 1, y + 1));
                    }
                }
            }
            var corners = new[] { min, max, new Vector2(min.x, max.y), new Vector2(max.x, min.y) }
                .Select(p => (p - sprite.pivot) / sprite.pixelsPerUnit)
                .Select(p => new Vector3(body.flipX ? -p.x : p.x, p.y, 0))
                .Select(p => camera.WorldToViewportPoint(body.transform.TransformPoint(p))).ToArray();
            return new { sprite = name, source = files[0], sourceHeight = max.y - min.y,
                screenHeight = (corners.Max(p => p.y) - corners.Min(p => p.y)) * height,
                texelPixels = (corners.Max(p => p.y) - corners.Min(p => p.y)) * height / (max.y - min.y),
                xMin = corners.Min(p => p.x) * width, xMax = corners.Max(p => p.x) * width,
                yMin = corners.Min(p => p.y) * height, yMax = corners.Max(p => p.y) * height };
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }
}
