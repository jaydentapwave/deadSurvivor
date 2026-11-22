using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Captures the actual Main camera; UI-inclusive captures remain in the unchanged playable runner.
public static class DQBackgroundVerification
{
    public static string Output = "output/hd2d-dq/apply/6/frames";
    public static void Capture(string label, int width, int height)
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MainSceneDQScreenMigration.ScenePath)
        {
            throw new InvalidOperationException("[DQBackgroundVerification] Requires Main Play.");
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
            var container = Object.FindAnyObjectByType<GameSceneLifetimeScope>().Container;
            var unitScale = ((RunRulesData)container.Resolve(typeof(RunRulesData))).UnitScale;
            var terrain = (TerrainHeightGrid)container.Resolve(typeof(TerrainHeightGrid));
            var partyFloor = new Vector2(PlayableProbe.PartyX * unitScale, PlayableProbe.PartyY * unitScale);
            var grassCenter = partyFloor + new Vector2(3, -1);
            var layout = (FieldLayoutData)container.Resolve(typeof(FieldLayoutData));
            var isGrassSampleClear = false;
            // Reject paths, water and placed objects from data BEFORE seeing any pixels.
            for (var x = 2; x <= 12 && !isGrassSampleClear; x++)
            {
                for (var z = -9; z <= 4 && !isGrassSampleClear; z++)
                {
                    var candidate = partyFloor + new Vector2(x, z);
                    var roi = new Rect(candidate.x - 1, candidate.y - 1.5f, 2, 3);
                    if (IsGrassRectangleClear(layout, roi) && IsScreenSampleClear(camera, terrain, roi))
                    {
                        grassCenter = candidate;
                        isGrassSampleClear = true;
                    }
                }
            }
            var grassCorners = new[] { new Vector2(-1, -1.5f), new Vector2(1, -1.5f), new Vector2(-1, 1.5f), new Vector2(1, 1.5f) }
                .Select(p => p + grassCenter).Select(p => camera.WorldToViewportPoint(new Vector3(p.x, TerrainHeightMath.Sample(terrain, p.x, p.y), p.y))).ToArray();
            var grassRegion = new[] { grassCorners.Min(p => p.x) * width, (1 - grassCorners.Max(p => p.y)) * height,
                grassCorners.Max(p => p.x) * width, (1 - grassCorners.Min(p => p.y)) * height };
            // Measurement anchors are declared before rendering, never hand-picked from the image.
            File.WriteAllText(path + "-before.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount,
                camera = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z }, pitch = camera.transform.eulerAngles.x, fov = camera.fieldOfView,
                orthographic = camera.orthographic, width, height, actors, grassRegion, isGrassSampleClear, grassCenter = new[] { grassCenter.x, grassCenter.y },
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

    private static bool IsFootprintRoot(Transform t) => t.name.StartsWith("3D:") || t.name.StartsWith("Far forest") || t.name == "bridge" || t.name == "tent" || t.name == "fence" || t.name == "sign" || t.name == "flag";

    private static bool IsScreenSampleClear(Camera camera, TerrainHeightGrid terrain, Rect roi)
    {
        var corners = new[] {new Vector2(roi.xMin,roi.yMin),new Vector2(roi.xMin,roi.yMax),new Vector2(roi.xMax,roi.yMax),new Vector2(roi.xMax,roi.yMin)};
        var screen = corners.Select(p=>camera.WorldToViewportPoint(new Vector3(p.x,TerrainHeightMath.Sample(terrain,p.x,p.y),p.y))).ToArray();
        var sample = Rect.MinMaxRect(screen.Min(p=>p.x),screen.Min(p=>p.y),screen.Max(p=>p.x),screen.Max(p=>p.y));
        if(sample.xMin<0||sample.xMax>1||sample.yMin<0||sample.yMax>1) return false;
        foreach(var renderer in Object.FindObjectsByType<Renderer>())
        {
            if(!renderer.enabled)continue;
            if(renderer.name.StartsWith("3D plant batch "))
            {
                if(BatchOverlaps(renderer,sample,camera))return false;
                continue;
            }
            var significant = renderer is SpriteRenderer || renderer.GetComponentsInParent<Transform>().Any(t=>IsFootprintRoot(t));
            if(!significant)continue;
            var bounds=renderer.bounds;var points=new List<Vector3>();
            for(var x=0;x<2;x++)for(var y=0;y<2;y++)for(var z=0;z<2;z++)points.Add(camera.WorldToViewportPoint(new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,z==0?bounds.min.z:bounds.max.z)));
            if(points.All(p=>p.z<0))continue;
            if(Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y)).Overlaps(sample))return false;
        }
        return true;
    }

    private static bool BatchOverlaps(Renderer renderer, Rect rectangle, Camera camera)
    {
        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
        var points=mesh.vertices.Select(p=>renderer.transform.TransformPoint(p)).Select(p=>camera!=null?camera.WorldToViewportPoint(p):new Vector3(p.x,p.z,1)).ToArray();
        var triangles=mesh.triangles;
        for(var i=0;i<triangles.Length;i+=3)
        {
            var a=points[triangles[i]];var b=points[triangles[i+1]];var c=points[triangles[i+2]];
            if(a.z<0&&b.z<0&&c.z<0)continue;
            if(Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x),Mathf.Min(a.y,b.y,c.y),Mathf.Max(a.x,b.x,c.x),Mathf.Max(a.y,b.y,c.y)).Overlaps(rectangle))return true;
        }
        return false;
    }

    public static bool IsGrassRectangleClear(FieldLayoutData layout, Rect roi)
    {
        var corners = new[] { new Vector2(roi.xMin, roi.yMin), new Vector2(roi.xMin, roi.yMax), new Vector2(roi.xMax, roi.yMax), new Vector2(roi.xMax, roi.yMin) };
        float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = b - a; var t = d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0;
            return Vector2.Distance(p, a + t * d);
        }
        bool SegmentHits(Vector2 a, Vector2 b, float radius)
        {
            if (roi.Contains(a) || roi.Contains(b)) return true;
            var delta = b - a;
            var enter = 0f; var exit = 1f;
            bool Clip(float origin, float direction, float min, float max)
            {
                if (Mathf.Abs(direction) < 1e-7f) return origin >= min && origin <= max;
                var t0 = (min - origin) / direction; var t1 = (max - origin) / direction;
                enter = Mathf.Max(enter, Mathf.Min(t0, t1)); exit = Mathf.Min(exit, Mathf.Max(t0, t1)); return enter <= exit;
            }
            if (Clip(a.x, delta.x, roi.xMin, roi.xMax) && Clip(a.y, delta.y, roi.yMin, roi.yMax)) return true;
            var closestA = new Vector2(Mathf.Clamp(a.x, roi.xMin, roi.xMax), Mathf.Clamp(a.y, roi.yMin, roi.yMax));
            var closestB = new Vector2(Mathf.Clamp(b.x, roi.xMin, roi.xMax), Mathf.Clamp(b.y, roi.yMin, roi.yMax));
            return Vector2.Distance(a, closestA) <= radius || Vector2.Distance(b, closestB) <= radius || corners.Any(p => SegmentDistance(p, a, b) <= radius);
        }
        bool PathHits(Vector2[] points, float radius, bool closed = false)
        {
            if (points == null || points.Length == 0) return false;
            if (points.Length == 1) return SegmentHits(points[0], points[0], radius);
            for (var i = 1; i < points.Length; i++) if (SegmentHits(points[i - 1], points[i], radius)) return true;
            if (closed && SegmentHits(points[points.Length - 1], points[0], radius)) return true;
            if (!closed) return false;
            // A rectangle wholly inside a pond has no crossing edges: point-in-polygon.
            foreach (var point in corners)
            {
                var inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    var a = points[i]; var b = points[j];
                    if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }
        var pathRadius = layout.path.halfWidth + Mathf.Abs(layout.path.widthVariation) + Mathf.Abs(layout.path.edgeVariation) + .25f;
        if (PathHits(layout.path.points, pathRadius) || PathHits(layout.path.branch, pathRadius)) return false;
        if (layout.water.Any(w => PathHits(w.points, w.name == "pond" ? .25f : w.width * .5f + .25f, w.name == "pond"))) return false;
        bool PlacementHits(FieldLayoutPlacement p)
        {
            // Small flower/grass cards do not have a 3x5m model footprint.
            var radius = p.kind == "prop" ? 3f : p.sprite.StartsWith("tree_") ? 1.5f : p.sprite.StartsWith("bush_") ? 1f : p.sprite.Contains("boulder") ? .8f : .25f;
            return new Rect(p.x-radius,p.y-radius,radius*2,radius*2).Overlaps(roi);
        }
        if(layout.placements.Any(PlacementHits))return false;
        // Include the actual model footprints, rather than relying only on placement centers.
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(!renderer.enabled)continue;
            if(renderer.name.StartsWith("3D plant batch "))
            {
                if(BatchOverlaps(renderer,roi,null))return false;
                continue;
            }
            if (!renderer.GetComponentsInParent<Transform>().Any(t => IsFootprintRoot(t))) continue;
            var bounds = renderer.bounds;
            if (new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z).Overlaps(roi)) return false;
        }
        return true;
    }

    public static void CaptureOverview()
    {
        var camera = Camera.main;
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        var fov = camera.fieldOfView;
        try
        {
            camera.transform.SetPositionAndRotation(new Vector3(0, 75, 0), Quaternion.Euler(90, 0, 0));
            camera.fieldOfView = 66;
            Capture("overview", 1920, 1920);
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov;
        }
    }

    public static void CaptureAspectSet(string label)
    {
        Capture(label, 1920, 1080);
        Capture(label, 2340, 1080);
        Capture(label, 2048, 1536);
    }

    public static void CaptureControls()
    {
        var all = Object.FindObjectsByType<Renderer>();
        var enabled = all.Select(r => r.enabled).ToArray();
        var materials = all.Select(r => r.sharedMaterials).ToArray();
        var shadows = all.Select(r => r.shadowCastingMode).ToArray();
        var blocks = all.Select(r=>{var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b;}).ToArray();
        var camera = Camera.main;
        var background = camera.backgroundColor;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        var post = cameraData.renderPostProcessing;
        var features = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MainSceneDQScreenMigration.RendererPath).rendererFeatures;
        var featureStates = features.Select(f => f.isActive).ToArray();
        var maskCopies = new List<Material>();
        try
        {
            foreach (var renderer in all)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            Capture("start-no-shadows", 1920, 1080);
            camera.backgroundColor = Color.black;
            cameraData.renderPostProcessing = false;
            foreach (var feature in features)
            {
                feature.SetActive(false);
            }
            for (var i = 0; i < all.Length; i++)
            {
                var renderer = all[i];
                var isForest = renderer.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("3D:tree") || t.name.StartsWith("3D:bush") || t.name.StartsWith("Far forest") || t.name.StartsWith("tree_v") || t.name.StartsWith("bush_v"));
                renderer.enabled = enabled[i];
                var isGround = renderer.name == "ground" || renderer.name == "bank" || renderer.name == "DQ render-only outer floor" || renderer.name == "North spawn mask hills";
                // All opaque objects retain depth writes; hidden trees cannot enter the mask.
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    var copy=new Material(original);maskCopies.Add(copy);
                    if(copy.shader.name=="DeadSurvivor/DQ/Main surface")
                    {
                        copy.SetFloat("_Diagnostic",isForest?2:isGround?3:4);
                    }
                    else if(copy.shader.name=="DeadSurvivor/DQ/Main water")copy.SetFloat("_DiagnosticMask",1);
                    else if(copy.HasProperty("_BaseColor"))
                    {
                        copy.SetColor("_BaseColor",Color.black);
                        if(copy.HasProperty("_EmissionColor"))copy.SetColor("_EmissionColor",Color.black);
                    }
                    else return original; // e.g. multiplicative foot shadows: retain native depth/blend.
                    return copy;
                }).ToArray();
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);block.SetFloat("_Appearance",1);renderer.SetPropertyBlock(block);
            }
            Capture("start-forest-mask", 1920, 1080);
        }
        finally
        {
            for (var i = 0; i < all.Length; i++)
            {
                all[i].enabled = enabled[i];
                all[i].sharedMaterials = materials[i];
                all[i].shadowCastingMode = shadows[i];
                all[i].SetPropertyBlock(blocks[i]);
            }
            camera.backgroundColor = background;
            cameraData.renderPostProcessing = post;
            for (var i = 0; i < features.Count; i++)
            {
                features[i].SetActive(featureStates[i]);
            }
            foreach(var copy in maskCopies)Object.DestroyImmediate(copy);
        }
    }

    // Same frozen Main frame, native renderer/depth; no gameplay or persistent asset mutation.
    public static void CaptureLightingPlates(bool includeMonsters = false)
    {
        if (!Application.isPlaying || Time.timeScale != 0)
        {
            throw new InvalidOperationException("Lighting plates require frozen Main Play.");
        }
        var camera = Camera.main;
        var data = camera.GetUniversalAdditionalCameraData();
        var features = data.scriptableRenderer is UniversalRenderer
            ? AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MainSceneDQScreenMigration.RendererPath).rendererFeatures : throw new InvalidOperationException("Expected DQ renderer.");
        var featureStates = features.Select(f => f.isActive).ToArray();
        var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var states = all.Select(r => r.enabled).ToArray();
        var materials = all.Select(r => r.sharedMaterials).ToArray();
        var background = camera.backgroundColor;
        var post = data.renderPostProcessing;
        var copies = new List<Material>();
        var bodies = Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None).Where(v => v.gameObject.activeInHierarchy).Select(v => v.BodyRenderer).ToArray();
        if (includeMonsters)
        {
            bodies = bodies.Concat(Object.FindObjectsByType<MonsterView>(FindObjectsSortMode.None)
                .Where(v => v.gameObject.activeInHierarchy && v.name.StartsWith("DQ light fixture:", StringComparison.Ordinal))
                .Select(v => (SpriteRenderer)new SerializedObject(v).FindProperty("bodyRenderer").objectReferenceValue)).ToArray();
        }
        try
        {
            Capture("light-main", 1920, 1080);
            var blur = features.OfType<FullScreenPassRendererFeature>().Single();
            blur.SetActive(false);
            Capture("light-no-blur", 1920, 1080);
            blur.SetActive(featureStates[features.IndexOf(blur)]);
            foreach (var renderer in bodies)
            {
                renderer.enabled = false;
            }
            Capture("light-actor-free", 1920, 1080);
            for (var i = 0; i < all.Length; i++)
            {
                all[i].enabled = states[i];
            }
            // Preserve actor depth/pose but isolate each member from party overlap for body metrics.
            for (var member = 0; member < bodies.Length; member++)
            {
                for (var other = 0; other < bodies.Length; other++)
                {
                    bodies[other].enabled = other == member;
                }
                Capture("isolated-member-" + member + "-lit", 1920, 1080);
            }
            camera.backgroundColor = Color.black;
            data.renderPostProcessing = false;
            foreach (var feature in features)
            {
                feature.SetActive(false);
            }
            for (var i = 0; i < all.Length; i++)
            {
                var renderer = all[i];
                if (renderer.GetComponentInParent<ParticleSystem>() != null || renderer.name == "DQ soft foot shadow")
                {
                    renderer.enabled = false;
                }
                renderer.sharedMaterials = materials[i].Select(original =>
                {
                    var copy = new Material(original);
                    copies.Add(copy);
                    if (copy.HasProperty("_Diagnostic"))
                    {
                        copy.SetFloat("_Diagnostic", 4);
                    }
                    if (copy.HasProperty("_DiagnosticMask"))
                    {
                        copy.SetFloat("_DiagnosticMask", 1);
                    }
                    if (copy.HasProperty("_BaseColor"))
                    {
                        copy.SetColor("_BaseColor", Color.black);
                    }
                    return copy;
                }).ToArray();
            }
            var white = new Material(Shader.Find("HD2D/HD2D_SpriteFlash"));
            copies.Add(white);
            for (var i = 0; i < bodies.Length; i++)
            {
                for (var other = 0; other < bodies.Length; other++)
                {
                    bodies[other].enabled = other == i;
                }
                var previous = bodies[i].sharedMaterial;
                bodies[i].sharedMaterial = white;
                Capture("member-" + i + "-mask", 1920, 1080);
                bodies[i].sharedMaterial = previous;
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Output), "lighting-plate-state.json"), JsonConvert.SerializeObject(new
            {
                utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount,
                members = bodies.Select((b, i) => new { index = i, sprite = b.sprite.name, rect = b.sprite.textureRect.ToString(), uv = DQSpriteLighting.InnerRect(b.sprite).ToString(), material = materials[Array.IndexOf(all,b)][0].name }).ToArray(),
                depthRequired = data.requiresDepthTexture,
                features = features.Select(f => new { name = f.name, type = f.GetType().Name, active = featureStates[features.IndexOf(f)] }).ToArray()
            }, Formatting.Indented));
        }
        finally
        {
            for (var i = 0; i < all.Length; i++)
            {
                all[i].enabled = states[i];
                all[i].sharedMaterials = materials[i];
            }
            for (var i = 0; i < features.Count; i++)
            {
                features[i].SetActive(featureStates[i]);
            }
            camera.backgroundColor = background;
            data.renderPostProcessing = post;
            foreach (var copy in copies)
            {
                Object.DestroyImmediate(copy);
            }
        }
    }

    public static void RunTour(int firstStage = 0)
    {
        if (!Application.isPlaying || PlayableProbe.Screen != "Battle")
        {
            throw new InvalidOperationException("Requires battle for normal-input tour.");
        }
        var scope = Object.FindAnyObjectByType<GameSceneLifetimeScope>().Container;
        var scale = ((RunRulesData)scope.Resolve(typeof(RunRulesData))).UnitScale;
        var points = new[] { new Vector2(-1, -3), new Vector2(6, 2), new Vector2(-5, 4), new Vector2(-4, -4), new Vector2(0, 100) };
        var names = new[] { "path", "water", "bridge", "camp", "edge" };
        var rows = new System.Collections.Generic.List<object>();
        var stage = firstStage;
        var frame = -1;
        var motion = 0;
        var deadline = EditorApplication.timeSinceStartup + 110;
        var originalBackground = Application.runInBackground;
        Application.runInBackground = true;
        EditorApplication.CallbackFunction callback = null;
        callback = () =>
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (Application.isPlaying && PlayableProbe.Screen == "Prep")
                {
                    PlayableProbe.PressNextWave();
                }
                if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline || PlayableProbe.Screen != "Battle")
                {
                    throw new InvalidOperationException("Normal-input tour interrupted or timed out: " + PlayableProbe.Screen);
                }
                if (frame == Time.frameCount)
                {
                    return;
                }
                frame = Time.frameCount;
                var position = new Vector2(PlayableProbe.PartyX, PlayableProbe.PartyY) * scale;
                var target = PartyMoveMath.ClampToDiamond(points[stage], PlayableProbe.PartyCenterLimitWorld);
                var delta = target - position;
                if (delta.magnitude < (stage == points.Length - 1 ? .002f : .12f))
                {
                    PlayableProbe.SetMove(0, 0);
                    CaptureAspectSet(names[stage]);
                    rows.Add(new { name = names[stage], frame, position = new[] { position.x, position.y }, target = new[] { target.x, target.y }, distance = delta.magnitude });
                    stage++;
                    if (stage == points.Length)
                    {
                        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Output), "tour.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), success = true, rows }, Newtonsoft.Json.Formatting.Indented));
                        EditorApplication.update -= callback;
                        Application.runInBackground = originalBackground;
                        return;
                    }
                }
                else
                {
                    var camera = Camera.main;
                    var here = camera.WorldToViewportPoint(new Vector3(position.x, PlayableProbe.PartyGroundHeight, position.y));
                    var next = camera.WorldToViewportPoint(new Vector3(position.x + delta.x, PlayableProbe.PartyGroundHeight, position.y + delta.y));
                    var input = new Vector2(next.x - here.x, next.y - here.y).normalized;
                    PlayableProbe.SetMove(input.x, input.y);
                    if (motion < 8 && stage == 0)
                    {
                        Capture("motion-" + motion, 1920, 1080);
                        motion++;
                    }
                }
            }
            catch (Exception exception)
            {
                EditorApplication.update -= callback;
                if (Application.isPlaying)
                {
                    PlayableProbe.SetMove(0, 0);
                }
                Application.runInBackground = originalBackground;
                File.WriteAllText("output/hd2d-dq/apply/6/tour-error.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), stage, frame, error = exception.ToString(), rows }, Newtonsoft.Json.Formatting.Indented));
            }
        };
        EditorApplication.update += callback;
    }

    private static float[] MatrixValues(Matrix4x4 matrix) => Enumerable.Range(0, 16).Select(i => matrix[i]).ToArray();

    private static object Describe(SpriteRenderer body, Camera camera, int width, int height)
    {
        var sprite = body.sprite;
        var name = sprite.name.Replace("(Clone)", "");
        var files = Directory.GetFiles("Assets/Game/03.Resources/Art", name + ".png", SearchOption.AllDirectories);
        if (files.Length != 1 && !name.StartsWith("dq_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("[DQBackgroundVerification] Expected source PNG or declared DQ atlas for " + name);
        }
        var texture = new Texture2D(2, 2);
        try
        {
            if (files.Length == 1)
            {
                texture.LoadImage(File.ReadAllBytes(files[0]));
            }
            else
            {
                var rect = sprite.textureRect;
                var target = RenderTexture.GetTemporary((int)rect.width, (int)rect.height, 0);
                var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(sprite.texture, target, rect.size / new Vector2(sprite.texture.width, sprite.texture.height),
                        rect.position / new Vector2(sprite.texture.width, sprite.texture.height));
                    RenderTexture.active = target;
                    texture.Reinitialize(target.width, target.height);
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    texture.Apply();
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                }
            }
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
                .Select(p => (p + sprite.textureRectOffset - sprite.pivot) / sprite.pixelsPerUnit)
                .Select(p => new Vector3(body.flipX ? -p.x : p.x, p.y, 0))
                .Select(p => camera.WorldToViewportPoint(body.transform.TransformPoint(p))).ToArray();
            return new { sprite = name, source = files.Length == 1 ? files[0] : "DQ atlas:" + sprite.texture.name, sourceHeight = max.y - min.y,
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
    public static void RunNear()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Requires Main Play.");
        var container = Object.FindFirstObjectByType<GameSceneLifetimeScope>().Container;
        var world = (WorldPresenter)container.Resolve(typeof(WorldPresenter));
        var presenter = (FieldOcclusionPresenter)container.Resolve(typeof(FieldOcclusionPresenter));
        var members = (IReadOnlyList<MemberView>)container.Resolve(typeof(IReadOnlyList<MemberView>));
        var originalVisibility = members.Select(member => member.gameObject.activeSelf).ToArray();
        var originalTimeScale = Time.timeScale;
        var field = Object.FindFirstObjectByType<FieldView>();
        var camera = Camera.main;
        var material = (Material)new SerializedObject(field).FindProperty("woodMaterial").objectReferenceValue;
        var normalId = material.GetInstanceID();
        var originalCount = field.Occluders.Count;
        // Only cleanup uses reflection: production layouts have no dynamic-unregister API.
        var registry = (List<FieldOccluder>)typeof(FieldView).GetField("_occluderList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(field);
        var root = new GameObject("Registered near-plane verification prop") { hideFlags = HideFlags.DontSave };
        var mesh = new Mesh { name = "Near-crossing verification panel", hideFlags = HideFlags.DontSave };
        mesh.vertices = new[] { new Vector3(-.025f, -.04f, -.08f), new Vector3(.06f, -.04f, .20f),
            new Vector3(.06f, .04f, .20f), new Vector3(-.025f, .04f, -.08f) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.colors = Enumerable.Repeat(Color.white, 4).ToArray();
        mesh.normals = Enumerable.Repeat(Vector3.back, 4).ToArray();
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        root.layer = HD2DBackgroundRig.BackgroundLayer;
        var origin = camera.transform.position + camera.transform.forward * .16f;
        root.transform.SetPositionAndRotation(origin, camera.transform.rotation);
        field.RegisterOccluder(root.transform, new Renderer[] { renderer });
        var prop = field.Occluders.Last();
        var rows = new List<object>();
        var passed = true;
        var frame = -1;
        var stage = 0;
        var deadline = EditorApplication.timeSinceStartup + 40;
        var cleaned = false;
        EditorApplication.CallbackFunction callback = null;

        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            EditorApplication.update -= callback;
            if (renderer != null) prop.SetFaded(false);
            registry.Remove(prop);
            for (var i = 0; i < members.Count; i++)
                if (members[i] != null) members[i].gameObject.SetActive(originalVisibility[i]);
            Time.timeScale = originalTimeScale;
            if (root != null) Object.DestroyImmediate(root);
            if (mesh != null) Object.DestroyImmediate(mesh);
        }

        callback = () =>
        {
            try
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Verification interrupted or timed out.");
                if (Time.frameCount == frame) return;
                frame = Time.frameCount;
                var covered = stage == 1 || stage == 2 || stage == 4 || stage == 6;
                var offscreen = stage == 3;
                var returned = stage == 7;
                foreach (var member in members) member.gameObject.SetActive(covered || offscreen || returned);
                if (covered || offscreen || returned) world.LateTick();
                root.transform.position = offscreen ? origin + camera.transform.right : origin;
                if (returned) { prop.SetFaded(false); registry.Remove(prop); }
                presenter.LateTick();
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var count = block.GetInt("_OcclusionRectCount");
                var depths = mesh.vertices.Select(v => camera.WorldToViewportPoint(root.transform.TransformPoint(v)).z).ToArray();
                var crossesNear = depths.Min() < camera.nearClipPlane && depths.Max() > camera.nearClipPlane;
                var registered = field.Occluders.Contains(prop);
                var restored = renderer.sharedMaterial.GetInstanceID() == normalId && !prop.IsFaded && count == 0;
                var stateOk = covered ? registered && crossesNear && prop.IsFaded && count > 0 && !restored
                    : restored && (returned ? !registered && field.Occluders.Count == originalCount : registered);
                passed &= stateOk;
                var label = new[] { "opaque-no-actors", "window", "window-next-frame", "offscreen-restored",
                    "window-again", "actors-disabled-restored", "window-before-return", "unregistered-restored" }[stage];
                Capture("r3-registered-near-" + label, 1920, 1080);
                var rect = OcclusionMath.ProjectBounds(camera, renderer.bounds);
                rows.Add(new { label, frame, time = Time.time, registered, crossesNear, depths,
                    near = camera.nearClipPlane, prop.IsFaded, count, normalId,
                    activeMaterialId = renderer.sharedMaterial.GetInstanceID(), activeMaterial = renderer.sharedMaterial.name,
                    shader = renderer.sharedMaterial.shader.name, restored, stateOk,
                    registryCount = field.Occluders.Count, presenter.FadedCount,
                    projectedBounds = new[] { rect.xMin, rect.yMin, rect.xMax, rect.yMax },
                    windows = count > 0 ? block.GetVectorArray("_OcclusionRects").Take(count)
                        .Select(v => new[] { v.x, v.y, v.z, v.w }).ToArray() : Array.Empty<float[]>() });
                stage++;
                if (stage < 8) return;
                Cleanup();
                File.WriteAllText("output/hd2d-dq/apply/6/registered-near.json", JsonConvert.SerializeObject(new { result = passed ? "PASS" : "FAIL",
                    scope = "registered FieldView prop -> production FieldOcclusionPresenter -> actual faded material and GPU capture",
                    originalCount, finalCount = field.Occluders.Count, rows }));
            }
            catch (Exception error)
            {
                Cleanup();
                File.WriteAllText("output/hd2d-dq/apply/6/registered-near.json", JsonConvert.SerializeObject(new { result = "FAIL", error = error.ToString(), rows }));
            }
        };
        Time.timeScale = 0;
        EditorApplication.update += callback;
    }
}
