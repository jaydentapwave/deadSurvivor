using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class DQLookdevCapture
{
    [Serializable] private sealed class Actor
    {
        public string name;
        public float[] rectangle;
        public float[] foot;
        public float[] world;
        public float depth;
    }
    [Serializable] private sealed class Plate
    {
        public string id;
        public float angle, fov, distance;
        public float[] position;
        public List<Actor> actors;
    }
    [Serializable] private sealed class Settings
    {
        public string utc;
        public string source = "URP SingleCameraRequest; full-resolution native render including camera-only optical focus and volume bloom";
        public float terrainTexelsPerMeter = 128;
        public string textureFilter = "Trilinear; mipmaps";
        public float farBlurRadius = 5.55f, nearBlurRadius = 1.35f, farBlurStart = .80f;
        public float bloomThreshold = 1.4f, bloomIntensity = .1f;
        public bool vignette = false;
        public List<Plate> plates = new List<Plate>();
    }

    public static void Capture(string path, int width, int height)
    {
        var camera = Camera.main;
        var previousAspect = camera.aspect;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            camera.aspect = (float)width / height;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.aspect = previousAspect; RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
        }
    }

    private static Actor Describe(SpriteRenderer sr)
    {
        var camera = Camera.main;
        var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sr.sprite)));
        var pixels = texture.GetPixels32(); var minX = texture.width; var minY = texture.height; var maxX = 0; var maxY = 0;
        for (var y = 0; y < texture.height; y++)
        {
            for (var x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a >= 128)
                {
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x + 1); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y + 1);
                }
            }
        }
        Object.DestroyImmediate(texture);
        var pivot = sr.sprite.pivot; var ppu = sr.sprite.pixelsPerUnit;
        var corners = new[] { new Vector3(minX - pivot.x, minY - pivot.y, 0), new Vector3(maxX - pivot.x, minY - pivot.y, 0), new Vector3(minX - pivot.x, maxY - pivot.y, 0), new Vector3(maxX - pivot.x, maxY - pivot.y, 0) };
        var points = corners.Select(v => camera.WorldToViewportPoint(sr.transform.TransformPoint(v / ppu))).ToArray();
        var foot = camera.WorldToViewportPoint(sr.transform.position);
        var position = sr.transform.position;
        return new Actor { name = sr.name, rectangle = new[] { points.Min(p => p.x) * 1920, (1 - points.Max(p => p.y)) * 1080, points.Max(p => p.x) * 1920, (1 - points.Min(p => p.y)) * 1080 }, foot = new[] { foot.x * 1920, (1 - foot.y) * 1080 }, world = new[] { position.x, position.y, position.z }, depth = foot.z };
    }

    public static void All(string folder = "")
    {
        if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DQLookdevBuilder.Scene)
        {
            throw new InvalidOperationException("DQ capture requires the DQ scene in Edit mode.");
        }
        var output = DQLookdevBuilder.Output + folder;
        Directory.CreateDirectory(output);
        var settings = new Settings { utc = DateTime.UtcNow.ToString("O") };
        foreach (var angle in new[] { 18f, 28f })
        {
            var id = angle == 18 ? "A" : "B";
            DQLookdevBuilder.Configure(angle);
            var camera = Camera.main;
            var actorList = GameObject.Find("Actors").GetComponentsInChildren<SpriteRenderer>().Where(sr => sr.name != "Blob shadow").ToArray();
            settings.plates.Add(new Plate { id = id, angle = angle, fov = camera.fieldOfView, distance = Vector3.Distance(camera.transform.position, new Vector3(0, .95f, 0)), position = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z }, actors = actorList.Select(Describe).ToList() });
            Capture(output + "/" + id + "-main-1920x1080.png", 1920, 1080);
            var shadeList = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Sun projected canopy shade").ToArray();
            var shadeStates = shadeList.Select(r => r.enabled).ToArray();
            try
            {
                foreach (var shade in shadeList)
                {
                    shade.enabled = false;
                }
                Capture(output + "/" + id + "-no-canopy-shade-control.png", 1920, 1080);
            }
            finally
            {
                for (var j = 0; j < shadeList.Length; j++)
                {
                    shadeList[j].enabled = shadeStates[j];
                }
            }
            Diagnostics(id, output);
            if (id == "B")
            {
                Capture(output + "/B-2340x1080.png", 2340, 1080);
                Capture(output + "/B-2048x1536.png", 2048, 1536);
            }
        }
        File.WriteAllText(output + "/settings.json", JsonUtility.ToJson(settings, true));
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), DQLookdevBuilder.Scene);
    }
    private static void Diagnostics(string id, string output)
    {
        var camera = Camera.main;
        var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var states = all.Select(r => r.enabled).ToArray();
        var originals = all.Select(r => r.sharedMaterials).ToArray();
        var data = camera.GetUniversalAdditionalCameraData();
        var isPost = data.renderPostProcessing;
        var background = camera.backgroundColor;
        var scope = camera.GetComponent<DeadSurvivor.DQLookdev.DQCameraScope>();
        var features = scope.RendererData.rendererFeatures;
        var mask = new Material(Shader.Find("DeadSurvivor/DQ/Sprite")); mask.SetFloat("_Mask", 1);
        var solid = new Material(AssetDatabase.LoadAssetAtPath<Material>(DQLookdevBuilder.Art + "/Leaves.mat")); solid.SetFloat("_Mask", 1);
        var actorList = GameObject.Find("Actors").GetComponentsInChildren<SpriteRenderer>().Where(sr => sr.name != "Blob shadow").ToArray();
        var markerList = new List<GameObject>();
        try
        {
            // The primary screenshots retain all post processing. These labelled masks do not.
            data.renderPostProcessing = false; camera.backgroundColor = Color.black;
            foreach (var feature in features)
            {
                feature.SetActive(false);
            }
            foreach (var r in all)
            {
                r.enabled = actorList.Contains(r); r.sharedMaterial = mask;
            }
            Capture(output + "/" + id + "-actors-mask.png", 1920, 1080);
            foreach (var r in all)
            {
                r.enabled = r.name == "Leaves"; r.sharedMaterial = solid;
            }
            Capture(output + "/" + id + "-forest-mask.png", 1920, 1080);
            foreach (var r in all)
            {
                r.enabled = false;
            }
            // Equal 1m camera-facing markers, measured at 10%, 50%, 90% of image height.
            var markerDataList = new List<Actor>();
            foreach (var imageY in new[] { .1f, .5f, .9f })
            {
                var ray = camera.ViewportPointToRay(new Vector3(.5f, 1 - imageY, 0));
                var floor = new Plane(Vector3.up, Vector3.zero);
                floor.Raycast(ray, out var enter);
                var position = ray.GetPoint(enter);
                var marker = new GameObject("marker_" + imageY, typeof(SpriteRenderer));
                markerList.Add(marker); marker.transform.position = position;
                marker.transform.rotation = camera.transform.rotation;
                var sr = marker.GetComponent<SpriteRenderer>();
                sr.sprite = actorList[0].sprite; sr.sharedMaterial = mask;
                var described = Describe(sr); markerDataList.Add(described);
            }
            Capture(output + "/" + id + "-perspective-markers.png", 1920, 1080);
            File.WriteAllText(output + "/" + id + "-markers.json", JsonUtility.ToJson(new MarkerCollection { markers = markerDataList }, true));
        }
        finally
        {
            foreach (var marker in markerList)
            {
                Object.DestroyImmediate(marker);
            }
            for (var j = 0; j < all.Length; j++)
            {
                all[j].enabled = states[j]; all[j].sharedMaterials = originals[j];
            }
            foreach (var feature in features)
            {
                feature.SetActive(true);
            }
            data.renderPostProcessing = isPost; camera.backgroundColor = background;
            Object.DestroyImmediate(mask); Object.DestroyImmediate(solid);
        }
    }
    [Serializable] private sealed class MarkerCollection { public List<Actor> markers; }

}
