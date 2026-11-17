using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kit3D
{
    // Animation probe and frames for `assets.py anim probe|frames` (rule .ai/rules/3d-animation.md 5.2 and 5.4).
    // Builds a temporary ADDITIVE scene far from the map and closes it without saving. The prefab asset is never written.
    // No project values here: the request gives the prefab, bone names (or none = Humanoid avatar), part names, character height
    // and the game camera.
    // probe : every clip sampled every `step` s (0..length inclusive): role bone positions and the lowest body point (renderers whose
    //         name does not start with a part name) plus the lowest point of each part, all in the prefab root space (m, front +Z).
    // frames: one clip played for `seconds` at `fps`, the character moving along `heading` at `speed`, skin update forced before
    //         every shot (a skinned mesh rendered right after sampling without it shows the bind pose while the bones move).
    public static class AnimProbe
    {
        [Serializable]
        public class Request
        {
            public string mode;
            public string prefab;
            public string out_path;
            public string[] clips = new string[0];
            public float step = 1f / 120f;
            public string[] roles = new string[0];
            public string[] bones = new string[0];
            public string[] parts = new string[0];
            public float char_height = 1f;
            public string clip;
            public string view = "game";
            public float speed;
            public float seconds = 2f;
            public float fps = 30f;
            public int width = 960;
            public int height = 540;
            public float heading = 90f;
            public string[] hide_parts = new string[0];
            public string prefix = "f";
            public float cam_pitch = 45f;
            public float cam_yaw = 180f;
            public float cam_fov = 25f;
            public float cam_distance = 7f;
            public float cam_target_height = 0.5f;
        }

        static readonly Vector3 Off = new Vector3(900f, 0f, 900f);
        static readonly Dictionary<string, HumanBodyBones> HumanRoles = new Dictionary<string, HumanBodyBones>
        {
            { "hips", HumanBodyBones.Hips }, { "head", HumanBodyBones.Head },
            { "foot_l", HumanBodyBones.LeftFoot }, { "foot_r", HumanBodyBones.RightFoot },
            { "hand_l", HumanBodyBones.LeftHand }, { "hand_r", HumanBodyBones.RightHand },
        };

        static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]";
        static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        public static string Run(string requestPath)
        {
            var q = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new Exception("PLAYING");
            }
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty)
            {
                throw new Exception("UNSAVED_SCENE:" + active.path);
            }
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            RenderTexture rt = null;
            string result;
            try
            {
                var root = new GameObject("AnimProbe_Temp").transform;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, temp);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(q.prefab);
                if (!src)
                {
                    throw new Exception("NO_PREFAB:" + q.prefab);
                }
                var inst = (GameObject)UnityEngine.Object.Instantiate(src, root);
                inst.transform.SetPositionAndRotation(Off, Quaternion.identity);
                var an = inst.GetComponentInChildren<Animator>();
                if (!an)
                {
                    throw new Exception("NO_ANIMATOR");
                }
                foreach (var s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    s.forceMatrixRecalculationPerRender = true;
                    s.updateWhenOffscreen = true;
                }
                AnimationMode.StartAnimationMode();
                if (q.mode == "probe")
                {
                    result = Probe(q, inst, an);
                }
                else if (q.mode == "frames")
                {
                    result = Frames(q, root, inst, an, out rt);
                }
                else
                {
                    throw new Exception("BAD_MODE:" + q.mode);
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }
                if (rt)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                EditorSceneManager.CloseScene(temp, true);
            }
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                throw new Exception("ACTIVE_SCENE_DIRTIED");
            }
            return result;
        }

        static AnimationClip ClipOf(Animator an, string name)
        {
            var all = an.runtimeAnimatorController ? an.runtimeAnimatorController.animationClips : new AnimationClip[0];
            var hit = all.Where(c => c.name == name || c.name.EndsWith("|" + name)).Distinct().ToArray();
            if (hit.Length != 1)
            {
                throw new Exception("CLIP_NOT_FOUND_OR_AMBIGUOUS:" + name + " have " + string.Join(",", all.Select(c => c.name).Distinct()));
            }
            return hit[0];
        }

        static Dictionary<string, Transform> Roles(Request q, GameObject inst, Animator an)
        {
            var map = new Dictionary<string, Transform>();
            if (q.roles.Length != q.bones.Length)
            {
                throw new Exception("ROLES_BONES_LENGTH");
            }
            if (q.roles.Length > 0)
            {
                var all = inst.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < q.roles.Length; i++)
                {
                    var hit = all.Where(t => t.name == q.bones[i]).ToArray();
                    if (hit.Length != 1)
                    {
                        throw new Exception("BONE_NOT_FOUND_OR_AMBIGUOUS:" + q.roles[i] + "=" + q.bones[i] + " (" + hit.Length + ")");
                    }
                    map[q.roles[i]] = hit[0];
                }
                return map;
            }
            if (!an.isHuman)
            {
                throw new Exception("NO_BONE_MAP (Generic rig: give catalog anim.bones {role: bone name})");
            }
            foreach (var kv in HumanRoles)
            {
                var t = an.GetBoneTransform(kv.Value);
                if (t)
                {
                    map[kv.Key] = t;
                }
            }
            return map;
        }

        static string PartOf(Renderer r, string[] parts)
        {
            foreach (var p in parts)
            {
                if (r.gameObject.name == p || r.gameObject.name.StartsWith(p + "_"))
                {
                    return p;
                }
            }
            return null;
        }

        // lowest y (prefab root space) of one renderer's current vertices. A skinned mesh is baked; whether the baked vertices carry
        // the transform scale differs between Unity versions, so both readings are compared with the renderer's world bounds once.
        static float Lowest(Renderer r, Transform space, Mesh bake, ref int bakeMode)
        {
            Vector3[] vs;
            var tr = r.transform;
            if (r is SkinnedMeshRenderer sk)
            {
                sk.BakeMesh(bake, true);
                vs = bake.vertices;
                if (bakeMode == 0)
                {
                    // Decide by height, not by the lowest point: feet at y 0 read 0 in both spaces, so the lowest point cannot tell them apart (B66).
                    float aLow = float.MaxValue, aHigh = float.MinValue, bLow = float.MaxValue, bHigh = float.MinValue;
                    foreach (var v in vs)
                    {
                        var ay = (tr.position + tr.rotation * v).y;
                        var by = tr.localToWorldMatrix.MultiplyPoint3x4(v).y;
                        aLow = Mathf.Min(aLow, ay);
                        aHigh = Mathf.Max(aHigh, ay);
                        bLow = Mathf.Min(bLow, by);
                        bHigh = Mathf.Max(bHigh, by);
                    }
                    var want = sk.bounds.size.y;
                    bakeMode = Mathf.Abs(aHigh - aLow - want) <= Mathf.Abs(bHigh - bLow - want) ? 1 : 2;
                }
            }
            else
            {
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh)
                {
                    return float.MaxValue;
                }
                vs = mf.sharedMesh.vertices;
            }
            var lo = float.MaxValue;
            var skinned = r is SkinnedMeshRenderer;
            foreach (var v in vs)
            {
                var w = skinned && bakeMode == 1 ? tr.position + tr.rotation * v : tr.localToWorldMatrix.MultiplyPoint3x4(v);
                lo = Mathf.Min(lo, space.InverseTransformPoint(w).y);
            }
            return lo;
        }

        static void Sample(Animator an, AnimationClip clip, float t)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(an.gameObject, clip, t);
            AnimationMode.EndSampling();
        }

        static string Probe(Request q, GameObject inst, Animator an)
        {
            var roles = Roles(q, inst, an);
            var rs = inst.GetComponentsInChildren<Renderer>(true);
            var bake = new Mesh();
            var bakeMode = 0;
            var sb = new StringBuilder();
            var model = an.transform;
            sb.Append("{\"schema\":\"3dkit-anim-probe/1\",\"prefab\":" + Q(q.prefab) + ",\"char_height\":" + F(q.char_height));
            sb.Append(",\"humanoid\":" + (an.isHuman ? "true" : "false") + ",\"model_scale\":" + V(model.lossyScale));
            sb.Append(",\"roles\":{" + string.Join(",", roles.Select(kv => Q(kv.Key) + ":" + Q(kv.Value.name))) + "}");
            sb.Append(",\"renderers\":{" + string.Join(",", rs.Select(r => Q(r.gameObject.name) + ":" + Q(PartOf(r, q.parts) ?? "body"))) + "}");
            sb.Append(",\"clips\":[");
            var summary = new StringBuilder();
            for (var c = 0; c < q.clips.Length; c++)
            {
                var clip = ClipOf(an, q.clips[c]);
                var n = Mathf.Max(1, Mathf.RoundToInt(clip.length / Mathf.Max(q.step, 1e-4f)));
                sb.Append((c > 0 ? "," : "") + "{\"name\":" + Q(q.clips[c]) + ",\"length\":" + F(clip.length) + ",\"fps\":" + F(clip.frameRate));
                sb.Append(",\"loop\":" + (clip.isLooping ? "true" : "false") + ",\"frames\":[");
                float lowAll = float.MaxValue;
                for (var i = 0; i <= n; i++)
                {
                    var t = clip.length * i / n;
                    Sample(an, clip, t);
                    var body = float.MaxValue;
                    var parts = new Dictionary<string, float>();
                    foreach (var r in rs)
                    {
                        var y = Lowest(r, inst.transform, bake, ref bakeMode);
                        var p = PartOf(r, q.parts);
                        if (p == null)
                        {
                            body = Mathf.Min(body, y);
                        }
                        else
                        {
                            parts[p] = parts.TryGetValue(p, out var old) ? Mathf.Min(old, y) : y;
                        }
                    }
                    lowAll = Mathf.Min(lowAll, body);
                    sb.Append((i > 0 ? "," : "") + "{\"t\":" + F(t) + ",\"b\":{");
                    sb.Append(string.Join(",", roles.Select(kv => Q(kv.Key) + ":" + V(inst.transform.InverseTransformPoint(kv.Value.position)))));
                    sb.Append("},\"low\":" + F(body) + ",\"parts\":{" + string.Join(",", parts.Select(kv => Q(kv.Key) + ":" + F(kv.Value))) + "}}");
                }
                sb.Append("]}");
                summary.AppendLine("clip " + q.clips[c] + " len=" + F(clip.length) + " fps=" + F(clip.frameRate) + " loop=" + clip.isLooping + " samples=" + (n + 1) + " lowest body=" + F(lowAll));
            }
            sb.Append("],\"bake_mode\":" + bakeMode + "}");
            UnityEngine.Object.DestroyImmediate(bake);
            File.WriteAllText(q.out_path, sb.ToString());
            return "humanoid=" + an.isHuman + " model scale=" + F(model.lossyScale.x) + " roles=" + string.Join(",", roles.Keys) + " bake_mode=" + bakeMode + "\n" + summary;
        }

        static void Ground(Transform root, float half)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var ma = new Material(lit) { color = new Color(.55f, .75f, .35f) };
            var mb = new Material(lit) { color = new Color(.48f, .68f, .3f) };
            foreach (var m in new[] { ma, mb })
            {
                if (m.HasProperty("_Smoothness"))
                {
                    m.SetFloat("_Smoothness", 0f);
                }
            }
            var n = Mathf.CeilToInt(half);
            for (var x = -n; x < n; x++)
            {
                for (var z = -n; z < n; z++)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    g.transform.SetParent(root);
                    g.transform.position = Off + new Vector3(x + .5f, 0f, z + .5f);
                    g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    g.GetComponent<Renderer>().sharedMaterial = ((x + z) & 1) == 0 ? ma : mb;
                    UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
                }
            }
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static string Frames(Request q, Transform root, GameObject inst, Animator an, out RenderTexture rt)
        {
            var clip = ClipOf(an, q.clip);
            var count = Mathf.Max(1, Mathf.RoundToInt(q.seconds * q.fps));
            var travel = q.speed * q.seconds;
            Ground(root, Mathf.Max(6f, travel / 2f + 4f));
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (q.hide_parts.Any(p => r.gameObject.name == p || r.gameObject.name.StartsWith(p + "_")))
                {
                    r.enabled = false;
                }
            }
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(root);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.5f, .77f, .91f);
            cam.farClipPlane = 300f;
            rt = new RenderTexture(q.width, q.height, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = (float)q.width / q.height;
            var dir = Quaternion.Euler(0f, q.heading, 0f) * Vector3.forward;
            inst.transform.rotation = Quaternion.Euler(0f, q.heading, 0f);
            var start = Off - dir * travel / 2f;
            var view = q.view;
            var fixedCam = view.EndsWith("_fixed");
            var baseView = fixedCam ? view.Substring(0, view.Length - 6) : view;
            var h = q.char_height;
            var right = Quaternion.Euler(0f, q.heading, 0f) * Vector3.right;
            Directory.CreateDirectory(q.out_path);
            for (var i = 0; i < count; i++)
            {
                var t = i / q.fps;
                Sample(an, clip, clip.isLooping ? t % clip.length : Mathf.Min(t, clip.length));
                var pos = start + dir * (q.speed * t);
                inst.transform.position = pos;
                var focus = fixedCam ? Off : pos;
                if (baseView == "game")
                {
                    cam.fieldOfView = q.cam_fov;
                    var r = Quaternion.Euler(q.cam_pitch, q.cam_yaw, 0f);
                    cam.transform.SetPositionAndRotation(focus + Vector3.up * q.cam_target_height - r * Vector3.forward * q.cam_distance, r);
                }
                else
                {
                    Vector3 side;
                    float dist, camY, lookY;
                    if (baseView == "side_r") { side = right; dist = h * 4.3f; camY = h * 1.2f; lookY = h * 0.45f; }
                    else if (baseView == "side_l") { side = -right; dist = h * 4.3f; camY = h * 1.2f; lookY = h * 0.45f; }
                    else if (baseView == "front") { side = dir; dist = h * 4.3f; camY = h * 1.2f; lookY = h * 0.45f; }
                    else if (baseView == "feet") { side = right; dist = h * 1.2f; camY = h * 0.12f; lookY = h * 0.1f; }
                    else { throw new Exception("BAD_VIEW:" + view + " (game, game_fixed, side_r, side_l, front, feet, +_fixed)"); }
                    cam.fieldOfView = q.cam_fov;
                    var eye = focus + side * dist + Vector3.up * camY;
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus + Vector3.up * lookY - eye));
                }
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(q.out_path, q.prefix + "_" + i.ToString("000") + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            return "frames=" + count + " clip=" + q.clip + " len=" + F(clip.length) + " loop=" + clip.isLooping + " view=" + view + " speed=" + F(q.speed) + " heading=" + F(q.heading) + " out=" + q.out_path;
        }
    }
}
