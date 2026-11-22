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
    public static string Output = "output/hd2d-dq/apply/7/recheck1";
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

    // Isolate one production receiver, comparing its actual clipped output to the
    // same frame without it. Diagnostic 4 supplies pre-clip depth/support per pixel.
    public static object ReceiverTransition(string label, DQBlobShadow target)
    {
        Directory.CreateDirectory(Output);
        var camera = Camera.main;
        var data = camera.GetUniversalAdditionalCameraData();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MainSceneDQScreenMigration.RendererPath);
        var featureStates = renderer.rendererFeatures.Select(f => f.isActive).ToArray();
        var post = data.renderPostProcessing;
        var blobs = Object.FindObjectsByType<DQBlobShadow>(FindObjectsSortMode.None).Where(b => b.OwnedMesh != null).ToArray();
        var enabled = blobs.Select(b => b.Renderer.enabled).ToArray();
        var material = target.SharedMaterial;
        var probe = new Material(material);
        const int width = 960;
        const int height = 540;
        try
        {
            data.renderPostProcessing = false;
            foreach (var feature in renderer.rendererFeatures)
            {
                feature.SetActive(false);
            }
            foreach (var blob in blobs)
            {
                blob.Renderer.enabled = false;
            }
            var absent = Render(camera, width, height);
            target.Renderer.enabled = true;
            var present = Render(camera, width, height);
            probe.SetFloat("_Diagnostic", 4);
            probe.SetFloat("_SrcBlend", 1);
            probe.SetFloat("_DstBlend", 0);
            target.Renderer.sharedMaterial = probe;
            var diagnostic = Render(camera, width, height);
            var candidates = 0;
            var received = 0;
            var wrongSupport = 0;
            var wrongDepth = 0;
            var maxError = 0f;
            var rejectedSupport = 0;
            for (var i = 0; i < diagnostic.Length; i++)
            {
                var value = diagnostic[i];
                if (value.b < 31.9f || value.b > 33.1f || value.g < 16)
                {
                    continue;
                }
                candidates++;
                if (value.b >= 32.5f)
                {
                    rejectedSupport++;
                }
                var delta = present[i] - absent[i];
                if (Mathf.Max(Mathf.Abs(delta.r), Mathf.Abs(delta.g), Mathf.Abs(delta.b)) <= .00001f)
                {
                    continue;
                }
                received++;
                maxError = Mathf.Max(maxError, value.r);
                if (value.b >= 32.5f)
                {
                    wrongSupport++;
                }
                if (value.r > .04f)
                {
                    wrongDepth++;
                }
            }
            var result = new
            {
                utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount, label,
                position = new[] { target.transform.position.x, target.transform.position.y, target.transform.position.z },
                candidates, received, wrongSupport, wrongDepth, maxReceivedWorldError = maxError,
                rejectedSupportCandidates = rejectedSupport, toleranceMetres = .04f,
                result = received == 0 ? "NOT_RUN" : wrongSupport == 0 && wrongDepth == 0 ? "PASS" : "FAIL",
                mesh = target.OwnedMesh.GetInstanceID(), material = material.GetInstanceID(), texture = material.mainTexture.GetInstanceID(),
                method = "Actual production material output minus same-frame disabled receiver; only changed diagnostic-tagged pixels count as received. No CPU-only prediction of clipping.",
                renderSize = new[] { width, height }, diagnosticDepthWidths = diagnostic.Where(c => c.b > 31.9f && c.b < 33.1f && c.g > 16).Select(c => c.g).Distinct().ToArray()
            };
            File.WriteAllText(Path.Combine(Output, label + ".json"), JsonConvert.SerializeObject(result, Formatting.Indented));
            return result;
        }
        finally
        {
            target.Renderer.sharedMaterial = material;
            Object.DestroyImmediate(probe);
            for (var i = 0; i < blobs.Length; i++)
            {
                if (blobs[i] != null)
                {
                    blobs[i].Renderer.enabled = enabled[i];
                }
            }
            for (var i = 0; i < featureStates.Length; i++)
            {
                renderer.rendererFeatures[i].SetActive(featureStates[i]);
            }
            data.renderPostProcessing = post;
        }
    }

    public static void StartTransitionMatrix()
    {
        if (!Application.isPlaying || PlayableProbe.Screen != "Battle" || Time.timeScale != 0)
        {
            throw new InvalidOperationException("Transition matrix requires frozen Main Battle.");
        }
        var scope = Object.FindAnyObjectByType<GameSceneLifetimeScope>().Container;
        var terrain = (TerrainHeightGrid)scope.Resolve(typeof(TerrainHeightGrid));
        var factory = (MonsterViewFactory)scope.Resolve(typeof(MonsterViewFactory));
        var field = (FieldView)scope.Resolve(typeof(FieldView));
        var member = Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None).Single(v => v.gameObject.activeInHierarchy && v.DQActorId == "bruno");
        var camera = Camera.main;
        var cameraPosition = camera.transform.position;
        var memberPosition = member.transform.position;
        var offset = cameraPosition - memberPosition;
        var originalOutput = Output;
        var originalCaptureOutput = DQBackgroundVerification.Output;
        var runInBackground = Application.runInBackground;
        var actorRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.GetComponentInParent<MemberView>() != null || r.GetComponentInParent<MonsterView>() != null).ToArray();
        var enabled = actorRenderers.Select(r => r.enabled).ToArray();
        var scenery = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.GetComponentInParent<MemberView>() == null && r.GetComponentInParent<MonsterView>() == null
                && r.name != "ground" && r.name != "bank" && r.sharedMaterials.All(m => m != null && m.shader.name != "DeadSurvivor/DQ/Main water")).ToArray();
        var species = new[] { "bruno", "slime", "mushroom", "rock", "boar", "puffer" };
        var regions = new[] { "flat", "slope", "water", "bank" };
        var from = new[] { new Vector2(0, 0), new Vector2(11, -8), new Vector2(7.5f, 5), new Vector2(7.3f, 8) };
        var to = new[] { new Vector2(1, 0), new Vector2(13, -8), new Vector2(8.5f, 5), new Vector2(8.3f, 8) };
        var rows = new List<object>();
        var actor = 0;
        var region = 0;
        var step = 0;
        var lastFrame = -1;
        MonsterView monster = null;
        var monsterId = -9500;
        var deadline = EditorApplication.timeSinceStartup + 300;
        Output = "output/hd2d-dq/apply/7/recheck2/final";
        var output = Output;
        DQBackgroundVerification.Output = output + "/frames";
        Directory.CreateDirectory(output);
        Application.runInBackground = true;
        EditorApplication.CallbackFunction callback = null;
        void Finish(string error)
        {
            EditorApplication.update -= callback;
            if (monster != null)
            {
                factory.ReleaseDetached(monster);
            }
            member.transform.position = memberPosition;
            camera.transform.position = cameraPosition;
            foreach (var renderer in scenery)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            for (var i = 0; i < actorRenderers.Length; i++)
            {
                if (actorRenderers[i] != null)
                {
                    actorRenderers[i].enabled = enabled[i];
                }
            }
            Application.runInBackground = runInBackground;
            Output = originalOutput;
            DQBackgroundVerification.Output = originalCaptureOutput;
            File.WriteAllText(output + "/matrix.json", JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow.ToString("O"), completed = error == null && rows.Count == 120,
                error, expectedRows = 120, rows,
                note = "Real Main surfaces and production factory, frozen simulation; five distinct engine frames per species/region. Region-to-region repositioning is fixture setup, not gameplay navigation. Root uses production height grid (no invented bank walking). Inspect coverage before accepting any region."
            }, Formatting.Indented));
        }
        callback = () =>
        {
            try
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline)
                {
                    throw new InvalidOperationException("Transition matrix interrupted or exceeded five minutes.");
                }
                EditorApplication.QueuePlayerLoopUpdate();
                if (Time.frameCount == lastFrame)
                {
                    return;
                }
                lastFrame = Time.frameCount;
                foreach (var renderer in actorRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                }
                if (actor > 0 && monster == null)
                {
                    var data = JsonUtility.FromJson<MonsterData>(Resources.Load<TextAsset>("Data/Monster/" + species[actor]).text);
                    data.OnAfterDeserialize();
                    monster = factory.Create(new Monster(monsterId, data, Vector2.zero, data.Hp, data.Damage, 0), null);
                    factory.Detach(monsterId);
                    monster.name = "DQ transition fixture: " + species[actor];
                }
                var root = actor == 0 ? member.transform : monster.transform;
                var body = actor == 0 ? member.BodyRenderer : monster.BodyRenderer;
                var point = Vector2.Lerp(from[region], to[region], step / 4f);
                var position = new Vector3(point.x, TerrainHeightMath.Sample(terrain, point.x, point.y), point.y);
                root.position = position;
                body.enabled = true;
                camera.transform.position = position + offset;
                var frame = new PerspectiveFrame(camera.transform.position, camera.transform.rotation, camera.fieldOfView,
                    camera.aspect, camera.pixelHeight, camera.nearClipPlane, camera.farClipPlane);
                if (actor == 0)
                {
                    member.ApplyProjection(frame, terrain);
                    member.SetGround(terrain, TerrainHeightMath.Normal(terrain, point.x, point.y), field.TerrainShadowMaterial);
                }
                else
                {
                    monster.ApplyProjection(frame, terrain);
                    monster.SetTerrain(terrain, TerrainHeightMath.Normal(terrain, point.x, point.y), field.TerrainShadowMaterial);
                }
                var label = species[actor] + "-" + regions[region] + "-" + step;
                foreach (var renderer in scenery)
                {
                    renderer.enabled = false;
                }
                var blob = root.GetComponent<DQBlobShadow>();
                var receiver = ReceiverTransition(label, blob);
                var normal = TerrainHeightMath.Normal(terrain, point.x, point.y);
                rows.Add(new { species = species[actor], region = regions[region], step, frame = Time.frameCount,
                    position = new[] { position.x, position.y, position.z }, normal = new[] { normal.x, normal.y, normal.z }, receiver });
                DQBackgroundVerification.Capture(label, 1920, 1080);
                step++;
                if (step == 5)
                {
                    step = 0;
                    region++;
                }
                if (region == 4)
                {
                    if (monster != null)
                    {
                        factory.ReleaseDetached(monster);
                        monster = null;
                    }
                    region = 0;
                    actor++;
                }
                if (actor == species.Length)
                {
                    Finish(null);
                }
            }
            catch (Exception exception)
            {
                Finish(exception.ToString());
            }
        };
        EditorApplication.update += callback;
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
