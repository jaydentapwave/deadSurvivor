using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Render evidence only. All temporary state/resources are restored in finally.
public static class DQLightingVerification
{
    public const string Output = "output/hd2d-dq/apply/7/recheck1";
    private static Color[] Render(Camera camera, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var image = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
        var active = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            return image.GetPixels();
        }
        finally { RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
    }

    public static void Receiver(string label)
    {
        Directory.CreateDirectory(Output);
        var camera = Camera.main;
        var data = camera.GetComponent<UniversalAdditionalCameraData>();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MainSceneDQScreenMigration.RendererPath);
        var features = renderer.rendererFeatures.Select(f => f.isActive).ToArray();
        var post = data.renderPostProcessing;
        var blobs = Object.FindObjectsByType<DQBlobShadow>().Where(b => b.OwnedMesh != null).ToArray();
        var materials = blobs.Select(b => b.SharedMaterial).ToArray();
        var probes = new List<Material>();
        try
        {
            data.renderPostProcessing = false;
            foreach (var feature in renderer.rendererFeatures) feature.SetActive(false);
            for (var i = 0; i < blobs.Length; i++)
            {
                var probe = new Material(materials[i]); probes.Add(probe);
                probe.SetFloat("_Diagnostic", 4); probe.SetFloat("_SrcBlend", 1); probe.SetFloat("_DstBlend", 0);
                blobs[i].Renderer.sharedMaterial = probe;
            }
            var pixels = Render(camera, 1920, 1080);
            var sampled = pixels.Where(c => c.b > 31.9f && c.b < 33.1f && c.g > 16).ToArray();
            var matching = sampled.Where(c => c.b < 32.5f).ToArray();
            var valid = matching.Where(c => c.r < .04f).ToArray();
            var depth = Shader.GetGlobalTexture("_CameraDepthTexture");
            File.WriteAllText(Output + "/receiver-" + label + ".json", JsonConvert.SerializeObject(new {
                utc = DateTime.UtcNow.ToString("O"), camera = camera.name, perspective = !camera.orthographic,
                sampled = sampled.Length, supportMatching = matching.Length, acceptedWithinUnchangedTolerance = valid.Length,
                rejectedDifferentSupport = sampled.Length - matching.Length,
                rejectedDistance = matching.Length - valid.Length,
                maxAcceptedWorldError = valid.Length > 0 ? valid.Max(c => c.r) : -1,
                medianAcceptedWorldError = valid.Length > 0 ? valid.Select(c => c.r).OrderBy(v => v).ElementAt(valid.Length / 2) : -1,
                depthCopy = renderer.copyDepthMode.ToString(),
                actualFragmentDepthWidths = sampled.Select(c => c.g).Distinct().ToArray(),
                postRequestGlobalDepthTexture = depth != null ? new[]{depth.width,depth.height} : null,
                note = "Probe executed on actual blob draw queue2990, before water3000. Error uses same ComputeWorldSpacePosition and actual pre-transparent depth; blue32+supportMismatch marks probe fragments; green records actual input depth width during the blob fragment pass. Rejections may include foreground occlusion; they are not all valid shadow loss.",
                resources = blobs.Select((b,i) => new { actor=b.name, mesh=b.OwnedMesh.GetInstanceID(), material=materials[i].GetInstanceID(), texture=materials[i].mainTexture.GetInstanceID(), width=b.Width }).ToArray()
            }, Formatting.Indented));
        }
        finally
        {
            for (var i=0;i<blobs.Length;i++) if(blobs[i]!=null) blobs[i].Renderer.sharedMaterial=materials[i];
            foreach(var probe in probes) Object.DestroyImmediate(probe);
            for(var i=0;i<features.Length;i++) renderer.rendererFeatures[i].SetActive(features[i]);
            data.renderPostProcessing=post;
        }
    }

    public static void BloomMaterials()
    {
        Directory.CreateDirectory(Output + "/bloom");
        var camera = Camera.main; var data=camera.GetComponent<UniversalAdditionalCameraData>();
        var renderers=Object.FindObjectsByType<Renderer>().Where(r=>r.gameObject.activeInHierarchy).ToArray();
        var enabled=renderers.Select(r=>r.enabled).ToArray();
        var materials=renderers.Where((r,i)=>enabled[i]).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
        var post=data.renderPostProcessing;var bg=camera.backgroundColor;var flags=camera.clearFlags;
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MainSceneDQScreenMigration.RendererPath);
        var features=renderer.rendererFeatures.Select(f=>f.isActive).ToArray();var rows=new List<object>();
        try
        {
            data.renderPostProcessing=false;camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;
            foreach(var f in renderer.rendererFeatures)f.SetActive(false);
            foreach(var material in materials)
            {
                for(var i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i]&&renderers[i].sharedMaterials.Contains(material);
                var pixels=Render(camera,512,288); var max=pixels.Max(c=>Mathf.Max(c.r,c.g,c.b));
                var threshold=VolumeManager.instance.stack.GetComponent<Bloom>().threshold.value;
                var linearThreshold=Mathf.GammaToLinearSpace(threshold);
                var count=pixels.Count(c=>Mathf.Max(c.r,c.g,c.b)>linearThreshold);
                var image=new Texture2D(512,288,TextureFormat.RGB24,false,true);
                try { image.SetPixels(pixels.Select(c=>Mathf.Max(c.r,c.g,c.b)>linearThreshold?Color.white:Color.black).ToArray());image.Apply();File.WriteAllBytes(Output+"/bloom/"+material.GetInstanceID()+".png",image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
                rows.Add(new {material=material.name,path=AssetDatabase.GetAssetPath(material),shader=material.shader.name,maxLinearRGB=max,bloomThresholdGamma=threshold,bloomThresholdLinear=linearThreshold,aboveThresholdPixels=count,mask=material.GetInstanceID()+".png"});
            }
            File.WriteAllText(Output+"/bloom/materials.json",JsonConvert.SerializeObject(new{utc=DateTime.UtcNow.ToString("O"),method="Isolated actual active shared material, Main URP camera ARGBFloat linear render, post/blur disabled, unchanged sun/ambient; threshold uses URP GammaToLinearSpace. No source material replacement.",rows},Formatting.Indented));
        }
        finally
        {
            for(var i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=enabled[i];
            for(var i=0;i<features.Length;i++)renderer.rendererFeatures[i].SetActive(features[i]);
            data.renderPostProcessing=post;camera.backgroundColor=bg;camera.clearFlags=flags;
        }
    }
}
