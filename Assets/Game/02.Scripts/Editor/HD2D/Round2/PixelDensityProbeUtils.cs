using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using Object = UnityEngine.Object;

public static class PixelDensityProbeUtils
{
    [Serializable]
    public sealed class Board
    {
        public string name;
        public Rect viewport;
        public float horizontalPixelsPerTexel;
        public float verticalPixelsPerTexel;
    }
    [Serializable]
    public sealed class Probe
    {
        public string plate;
        public string image;
        public string runId;
        public string sourceFingerprint;
        public PixelLookdevCaptureUtils.Settings settings;
        public Board[] boards;
    }
    [Serializable]
    public sealed class Probes
    {
        public Probe[] probes;
    }

    public static void Capture()
    {
        var collection = JsonUtility.FromJson<PixelLookdevCaptureUtils.Collection>(File.ReadAllText(PixelLookdevCaptureUtils.Output + "/plates2.json"));
        var records = new Probe[2];
        var names = new[] { "F2c", "F-int" };
        for (var i = 0; i < names.Length; i++)
        {
            PixelLookdevSceneUtils.Configure(names[i]);
            var camera = Camera.main;
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var enabled = renderers.Select(r => r.enabled).ToArray();
            var background = camera.backgroundColor;
            var root = new GameObject("Texel probe temporary geometry");
            var material = new Material(PixelLookdevAssets.Load<Material>("Surface_0.mat"));
            var sprite = Sprite.Create(PixelLookdevAssets.Load<Texture2D>("F2Density.png"),new Rect(0,0,48,48),Vector2.one * 0.5f,48);
            try
            {
                foreach (var renderer in renderers)
                {
                    renderer.enabled = false;
                }
                camera.backgroundColor = Color.black;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                PixelLookdevSceneUtils.Scope.SetScreenEffects(false);
                var standing = new GameObject("48 PPU sprite texels").AddComponent<SpriteRenderer>();
                standing.transform.SetParent(root.transform,false);
                standing.transform.position = new Vector3(-0.8f,0.55f,-0.4f);
                standing.transform.rotation = Quaternion.Euler(30,0,0);
                standing.sprite = sprite;
                standing.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ground.transform.SetParent(root.transform,false);
                ground.transform.position = new Vector3(0.8f,0.025f,-1f);
                ground.transform.rotation = Quaternion.Euler(90,0,0);
                ground.transform.localScale = new Vector3(1,2,1);
                material.SetFloat("_Diagnostic",2);
                ground.GetComponent<Renderer>().sharedMaterial = material;
                var path = PixelLookdevCaptureUtils.Output + "/density2-" + names[i] + ".png";
                LookdevCaptureUtils.Capture(path,1920,1080);
                records[i] = new Probe { plate = names[i], image = path, runId = collection.runId, sourceFingerprint = collection.sourceFingerprint, settings = PixelLookdevCaptureUtils.ReadSettings(), boards = new[] { Measure(camera,standing.transform,"sprite 48x48 texels"),Measure(camera,ground.transform,"floor 48x24 texels per unit") } };
            }
            finally
            {
                for (var r = 0; r < renderers.Length; r++)
                {
                    renderers[r].enabled = enabled[r];
                }
                camera.backgroundColor = background;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                PixelLookdevSceneUtils.Scope.SetScreenEffects(true);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(sprite);
            }
        }
        File.WriteAllText(PixelLookdevCaptureUtils.Output + "/measure2-texel-probes.json",JsonUtility.ToJson(new Probes { probes = records },true));
    }

    private static Board Measure(Camera camera, Transform transform, string name)
    {
        var corners = new[] { new Vector3(-0.5f,-0.5f,0),new Vector3(0.5f,-0.5f,0),new Vector3(-0.5f,0.5f,0),new Vector3(0.5f,0.5f,0) };
        var points = corners.Select(p => camera.WorldToViewportPoint(transform.TransformPoint(p))).ToArray();
        var width = (points.Max(p => p.x) - points.Min(p => p.x)) * 1920;
        var height = (points.Max(p => p.y) - points.Min(p => p.y)) * 1080;
        return new Board { name = name, viewport = Rect.MinMaxRect(points.Min(p => p.x),points.Min(p => p.y),points.Max(p => p.x),points.Max(p => p.y)), horizontalPixelsPerTexel = width / 48, verticalPixelsPerTexel = height / 48 };
    }
}
