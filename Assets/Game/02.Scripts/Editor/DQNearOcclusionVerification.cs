using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Play-only verification fixture: the production registry, presenter and faded material
// handle a real near-crossing prop. Never saves a scene, prefab, or material.
public static class DQNearOcclusionVerification
{
    public const string Output = "output/hd2d-dq/apply/5/r3-registered-near.json";

    public static void Run()
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
                DQMainScreenCapture.Capture("r3-registered-near-" + label, 1920, 1080);
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
                File.WriteAllText(Output, JsonConvert.SerializeObject(new { result = passed ? "PASS" : "FAIL",
                    scope = "registered FieldView prop -> production FieldOcclusionPresenter -> actual faded material and GPU capture",
                    originalCount, finalCount = field.Occluders.Count, rows }));
            }
            catch (Exception error)
            {
                Cleanup();
                File.WriteAllText(Output, JsonConvert.SerializeObject(new { result = "FAIL", error = error.ToString(), rows }));
            }
        };
        Time.timeScale = 0;
        EditorApplication.update += callback;
    }
}
