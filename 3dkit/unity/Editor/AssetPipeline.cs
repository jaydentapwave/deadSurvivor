using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kit3D
{
    // 3dkit Unity side of the asset pipeline. 3dkit/tools/assets.py writes <work_root>/<Entry>-request.json and calls
    //   unity command script eval -- 'return Kit3D.AssetPipeline.Run("<Entry>", "<request path>");'
    // Every project value (paths, shader, texture slots, import settings, prefab options) comes from the request; game
    // types are reached only through IKit3DProjectHooks. Ported from Deadsurvivor Tools/Art/AssetBuild.cs (050).
    //   Move    - one-time AssetDatabase moves (GUIDs kept)
    //   Build   - per item: texture import per map, material, model import (+ Mesh LOD), prefab (root at the feet ->
    //             Visual -> model normalised to height 1, front turned to +Z); shared base prefab default material;
    //             project register hook. Never opens or edits a scene.
    //   Check   - measures items and the build scenes' dependencies (assets.py judges) + project hook measurements
    //   Capture - game-angle and front line-ups in a temporary additive scene (closed without saving)
    public static class AssetPipeline
    {
        public static string Run(string entry, string requestPath)
        {
            var req = JObject.Parse(File.ReadAllText(requestPath));
            Guard(req);
            object result;
            switch (entry)
            {
                case "Move": result = Move(req); break;
                case "Build": result = Build(req); break;
                case "Check": result = Check(req); break;
                case "Capture": result = Capture(req); break;
                default: throw new Exception("UNKNOWN_ENTRY:" + entry);
            }
            var resPath = Path.Combine((string)req["out_dir"], entry + "-result.json");
            File.WriteAllText(resPath, JsonConvert.SerializeObject(result, Formatting.Indented));
            return entry + " OK " + resPath;
        }

        static void Guard(JObject req)
        {
            string want = Path.GetFullPath(((string)req["project_root"]).TrimEnd('/') + "/Assets");
            if (Path.GetFullPath(Application.dataPath) != want || EditorApplication.isPlaying) throw new Exception("WRONG_TARGET_OR_PLAYING");
        }

        static T E<T>(JToken v) => (T)Enum.Parse(typeof(T), (string)v);
        static string Str(JToken v) => v == null || v.Type == JTokenType.Null ? null : (string)v;

        static IKit3DProjectHooks Hooks(JObject req, bool required)
        {
            if (req["hooks"] == null || req["hooks"].Type == JTokenType.Null)
            {
                if (required) throw new Exception("NO_HOOK_DATA");
                return null;
            }
            var types = TypeCache.GetTypesDerivedFrom<IKit3DProjectHooks>()
                .Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null).ToList();
            if (types.Count == 0) throw new Exception("NO_PROJECT_HOOKS");
            if (types.Count > 1) throw new Exception("MANY_PROJECT_HOOKS:" + string.Join(",", types.Select(t => t.FullName)));
            return (IKit3DProjectHooks)Activator.CreateInstance(types[0]);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------- Move ----------------
        static object Move(JObject req)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++) { var s = EditorSceneManager.GetSceneAt(i); if (s.isDirty) throw new Exception("UNSAVED_SCENE:" + s.path); }
            var rows = new List<object>();
            foreach (var m in req["moves"])
            {
                string from = (string)m[0], to = (string)m[1];
                string gFrom = AssetDatabase.AssetPathToGUID(from), gTo = AssetDatabase.AssetPathToGUID(to);
                if (string.IsNullOrEmpty(gFrom) && !string.IsNullOrEmpty(gTo)) { rows.Add(new { from, to, state = "ALREADY_MOVED", guid = gTo }); continue; }
                if (string.IsNullOrEmpty(gFrom)) throw new Exception("MISSING:" + from);
                if (!string.IsNullOrEmpty(gTo)) throw new Exception("TARGET_EXISTS:" + to);
                EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
                var err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err)) throw new Exception("MOVE_FAILED:" + from + ":" + err);
                if (AssetDatabase.AssetPathToGUID(to) != gFrom) throw new Exception("GUID_CHANGED:" + to);
                rows.Add(new { from, to, state = "MOVED", guid = gFrom });
            }
            AssetDatabase.SaveAssets();
            return new { moves = rows };
        }

        // ---------------- Build ----------------
        static object Build(JObject req)
        {
            var m = (JObject)req["material"];
            var shader = Shader.Find((string)m["shader"]) ?? throw new Exception("NO_SHADER:" + m["shader"]);
            var hooks = Hooks(req, false);
            hooks?.Prepare(req);                         // validate before any write
            var rows = new List<object>();
            var mats = new Dictionary<string, Material>();
            var prefabs = new Dictionary<string, GameObject>();
            foreach (JObject it in req["items"])
            {
                string id = (string)it["id"], folder = (string)it["folder"];
                EnsureFolder(folder);
                var tex = new Dictionary<string, Texture2D>();
                foreach (var kv in (JObject)it["textures"])
                {
                    var t = (JObject)kv.Value;
                    tex[kv.Key] = ImportTexture((string)t["src"], (string)t["dst"], (int)t["size"], (JObject)t["settings"]);
                }
                var mat = MakeMaterial((string)it["material"], shader, tex, m, (JObject)it["preset"]);
                mats[id] = mat;
                GameObject prefab = null;
                if (Str(it["model_src"]) != null)
                {
                    var fbx = ImportModel((string)it["model_src"], (string)it["model_dst"], (JObject)req["model_import"], (bool)it["mesh_lod"]);
                    prefab = MakePrefab(fbx, (string)it["prefab"], mat, (float)it["yaw"], (string)it["shadow"], (JObject)req["prefab"]);
                    prefabs[id] = prefab;
                }
                rows.Add(new { id, textures = tex.ToDictionary(k => k.Key, k => AssetDatabase.GetAssetPath(k.Value)), material = AssetDatabase.GetAssetPath(mat),
                               prefab = prefab ? AssetDatabase.GetAssetPath(prefab) : null });
            }
            foreach (var mt in mats.Values) AssetDatabase.SaveAssetIfDirty(mt);
            object register = hooks?.Register(req, mats, prefabs);
            // a shared base prefab (variants share its model) gets its own renderer material from one variant, so it never
            // points at a trial folder
            var bases = new List<object>();
            foreach (JObject b in req["base_defaults"])
            {
                string hp = (string)b["prefab"];
                var def = mats[(string)b["item"]];
                var root = PrefabUtility.LoadPrefabContents(hp);
                try
                {
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = Enumerable.Repeat(def, r.sharedMaterials.Length).ToArray();
                    PrefabUtility.SaveAsPrefabAsset(root, hp);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                bases.Add(new { prefab = hp, material = AssetDatabase.GetAssetPath(def) });
            }
            return new { items = rows, baseDefaults = bases, register };
        }

        static Texture2D ImportTexture(string src, string dst, int size, JObject s)
        {
            if (!File.Exists(src)) throw new Exception("MISSING_TEXTURE:" + src);
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            ti.textureType = E<TextureImporterType>(s["type"]);
            ti.sRGBTexture = (bool)s["srgb"];
            ti.alphaSource = E<TextureImporterAlphaSource>(s["alpha"]);
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = (bool)s["mip"];
            ti.filterMode = E<FilterMode>(s["filter"]);
            ti.wrapMode = E<TextureWrapMode>(s["wrap"]);
            ti.npotScale = E<TextureImporterNPOTScale>(s["npot"]);
            ti.maxTextureSize = size;
            ti.textureCompression = E<TextureImporterCompression>(s["compression"]);
            ti.isReadable = (bool)s["readable"];
            foreach (var kv in (JObject)s["platforms"])
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = kv.Key, overridden = true, maxTextureSize = size, format = E<TextureImporterFormat>(kv.Value),
                    textureCompression = ti.textureCompression
                });
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst) ?? throw new Exception("TEXTURE_NOT_IMPORTED:" + dst);
        }

        static Material MakeMaterial(string path, Shader shader, Dictionary<string, Texture2D> tex, JObject m, JObject preset)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            foreach (var kv in (JObject)m["slots"])
            {
                if (!tex.ContainsKey(kv.Key)) continue;
                if (!mat.HasProperty((string)kv.Value)) throw new Exception("NO_PROPERTY:" + kv.Value);
                mat.SetTexture((string)kv.Value, tex[kv.Key]);
            }
            foreach (var kv in (JObject)m["colors"])
            {
                var c = (JArray)kv.Value;
                mat.SetColor(kv.Key, new Color((float)c[0], (float)c[1], (float)c[2], (float)c[3]));
            }
            mat.enableInstancing = (bool)m["instancing"];
            foreach (var kv in preset)
            {
                if (!mat.HasProperty(kv.Key)) throw new Exception("NO_PROPERTY:" + kv.Key);
                mat.SetFloat(kv.Key, (float)kv.Value);
            }
            foreach (var kv in (JObject)m["keyword_toggles"])
            {
                if (mat.HasProperty(kv.Key) && mat.GetFloat(kv.Key) > .5f) mat.EnableKeyword((string)kv.Value); else mat.DisableKeyword((string)kv.Value);
            }
            foreach (var kv in (JObject)m["map_keywords"])
            {
                if (tex.ContainsKey(kv.Key)) mat.EnableKeyword((string)kv.Value); else mat.DisableKeyword((string)kv.Value);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject ImportModel(string src, string dst, JObject s, bool meshLod)
        {
            if (!File.Exists(src)) throw new Exception("MISSING_MODEL:" + src);
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            var mi = (ModelImporter)AssetImporter.GetAtPath(dst);
            mi.materialImportMode = E<ModelImporterMaterialImportMode>(s["materials"]);
            if (!(bool)s["animation"]) { mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None; }
            mi.isReadable = (bool)s["readable"];
            mi.importBlendShapes = (bool)s["blend_shapes"];
            mi.importCameras = (bool)s["cameras"];
            mi.importLights = (bool)s["lights"];
            mi.importVisibility = (bool)s["visibility"];
            if (Str(s["normals"]) != null) mi.importNormals = E<ModelImporterNormals>(s["normals"]);
            if (meshLod)
            {
                // Unity 6000.2+ Mesh LOD; reflection so the package still compiles where the property is missing
                var p = typeof(ModelImporter).GetProperty("generateMeshLods") ?? throw new Exception("MESH_LOD_UNSUPPORTED");
                p.SetValue(mi, true);
            }
            mi.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(dst) ?? throw new Exception("MODEL_NOT_IMPORTED:" + dst);
        }

        // root (feet, identity) -> "Visual" -> model: turned by yaw so the front is +Z, normalised to height 1, feet at 0,
        // centred on XZ
        static GameObject MakePrefab(GameObject fbx, string path, Material mat, float yaw, string shadow, JObject p)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(visual.transform, false);
                model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                var anim = model.GetComponent<Animator>();
                if (anim) UnityEngine.Object.DestroyImmediate(anim);
                var rs = model.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) throw new Exception("NO_RENDERER:" + path);
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float s = 1f / Mathf.Max(.001f, b.size.y);
                model.transform.localScale = Vector3.one * s;
                model.transform.localPosition = new Vector3(-b.center.x * s, -b.min.y * s, -b.center.z * s);
                foreach (var r in rs)
                {
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = shadow == "real" ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.receiveShadows = (bool)p["receive_shadows"];
                    r.lightProbeUsage = E<LightProbeUsage>(p["light_probes"]);
                    r.reflectionProbeUsage = E<ReflectionProbeUsage>(p["reflection_probes"]);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ---------------- Check (measure only; assets.py judges) ----------------
        static object Check(JObject req)
        {
            var props = req["material_props"].Select(x => (string)x).ToArray();
            var slots = req["slots"].Select(x => (string)x).ToArray();
            var items = new List<object>();
            foreach (JObject it in req["items"])
            {
                bool modelChild = (bool)it["model_child"];
                var row = new Dictionary<string, object> { ["id"] = (string)it["id"], ["model_child"] = modelChild };
                string prefab = Str(it["prefab"]), material = Str(it["material"]);
                row["folder_exists"] = AssetDatabase.IsValidFolder((string)it["folder"]);
                if (prefab != null) row["prefab"] = MeasurePrefab(prefab, modelChild);
                if (material != null)
                {
                    var mt = AssetDatabase.LoadAssetAtPath<Material>(material);
                    row["material"] = mt ? (object)new
                    {
                        shader = mt.shader.name,
                        textures = slots.Where(mt.HasProperty).ToDictionary(s => s, s => mt.GetTexture(s) ? AssetDatabase.GetAssetPath(mt.GetTexture(s)) : null),
                        props = props.ToDictionary(p => p, p => mt.HasProperty(p) ? (float?)mt.GetFloat(p) : null)
                    } : null;
                }
                row["textures"] = ((JObject)it["textures"]).Properties().ToDictionary(p => p.Name, p => MeasureTexture((string)p.Value));
                items.Add(row);
            }
            string content = (string)req["content_root"] + "/", art = (string)req["art_root"] + "/";
            var scenes = new List<object>();
            foreach (var sc in req["scenes"])
            {
                string path = (string)sc;
                var deps = AssetDatabase.GetDependencies(path, true).Where(d => d.StartsWith("Assets/") && !d.EndsWith(".cs")).ToArray();
                var matShaders = deps.Where(d => d.EndsWith(".mat") && d.StartsWith(content))
                    .Select(d => { var mt = AssetDatabase.LoadAssetAtPath<Material>(d); return new { path = d, shader = mt ? mt.shader.name : null }; }).ToArray();
                var tex = deps.Where(d => d.StartsWith(art) && AssetDatabase.GetMainAssetTypeAtPath(d) == typeof(Texture2D))
                    .Select(d => new { path = d, t = MeasureTexture(d) }).ToArray();
                scenes.Add(new { scene = path, deps, materials = matShaders, textures = tex });
            }
            var problems = new List<string>();
            object project = Hooks(req, false)?.Measure(req, problems);
            return new { items, scenes, project, project_problems = problems };
        }

        static object MeasurePrefab(string path, bool modelChild)
        {
            if (!File.Exists(path)) return new { missing = true };
            if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                // a model a scene builder places itself (materials come from the builder): triangles only
                var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
                return new { tris = meshes.Sum(x => x.triangles.Length / 3), bones = 0, renderers = meshes.Length, materials = 0, shaders = new string[0], shadows = new string[0],
                             height = -1f, feet = -1f, centre = -1f, visual = (string)null, yaw = float.NaN, materialPaths = new string[0] };
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int tris = 0, bones = 0;
                var shaders = new HashSet<string>(); var mats = new HashSet<Material>(); var shadows = new HashSet<string>();
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
                foreach (var sm in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { if (sm.sharedMesh) tris += sm.sharedMesh.triangles.Length / 3; bones = Math.Max(bones, sm.bones.Length); }
                var rs = root.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rs) { foreach (var mt in r.sharedMaterials) if (mt) { mats.Add(mt); shaders.Add(mt.shader.name); } shadows.Add(r.shadowCastingMode.ToString()); }
                float height = -1, feet = -1, centre = -1;
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (rs.All(r => r is SkinnedMeshRenderer)) b = ((SkinnedMeshRenderer)rs[0]).bounds;
                    height = b.size.y; feet = b.min.y; centre = new Vector2(b.center.x, b.center.z).magnitude;
                }
                float yaw = float.NaN;
                var visual = root.transform.childCount > 0 ? root.transform.GetChild(0) : null;
                if (modelChild && visual && visual.childCount > 0) yaw = visual.GetChild(0).localEulerAngles.y;
                return new { tris, bones, renderers = rs.Length, materials = mats.Count, shaders = shaders.ToArray(), shadows = shadows.ToArray(), height, feet, centre,
                             visual = visual ? visual.name : null, yaw, materialPaths = mats.Select(x => AssetDatabase.GetAssetPath(x)).ToArray() };
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static object MeasureTexture(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!ti || !t) return new { missing = true };
            var a = ti.GetPlatformTextureSettings("Android"); var i = ti.GetPlatformTextureSettings("iPhone");
            return new { width = t.width, height = t.height, max = ti.maxTextureSize, mip = ti.mipmapEnabled, alpha = ti.alphaSource.ToString(), filter = ti.filterMode.ToString(),
                         type = ti.textureType.ToString(), srgb = ti.sRGBTexture,
                         android = a.overridden ? a.format + "@" + a.maxTextureSize : "default@" + ti.maxTextureSize,
                         ios = i.overridden ? i.format + "@" + i.maxTextureSize : "default@" + ti.maxTextureSize };
        }

        // ---------------- Capture ----------------
        // Line-ups in a temporary additive scene far from the map (lit by the open scene's sun), closed without saving.
        static object Capture(JObject req)
        {
            var items = Hooks(req, true).Lineup(req).ToList();
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty) throw new Exception("UNSAVED_SCENE:" + active.path);
            var bg = (JArray)req["background"];
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var shots = new List<string>();
            try
            {
                var go = new GameObject("Kit3DLineup_Temp");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, temp);
                float x = 0;
                foreach (var e in items)
                {
                    var inst = (GameObject)UnityEngine.Object.Instantiate(e.prefab, go.transform);
                    inst.transform.position = new Vector3(500 + x, 0, 500);
                    inst.transform.localScale = Vector3.one * e.scale;
                    inst.transform.GetChild(0).localRotation = Quaternion.Euler(0, e.visualYaw, 0);
                    if (e.material) foreach (var rr in inst.GetComponentsInChildren<Renderer>()) rr.sharedMaterials = Enumerable.Repeat(e.material, rr.sharedMaterials.Length).ToArray();
                    x += 1.5f;
                }
                var camGo = new GameObject("LineupCam");
                camGo.transform.SetParent(go.transform);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color((float)bg[0], (float)bg[1], (float)bg[2]);
                var c = new Vector3(500 + (x - 1.5f) / 2, .6f, 500);
                foreach (var (name, rot, size) in new[] { ("lineup_game", Quaternion.Euler(55, 0, 0), 2.6f), ("lineup_front", Quaternion.Euler(8, 180, 0), 2.6f) })
                {
                    cam.orthographicSize = size;
                    cam.transform.SetPositionAndRotation(c - (rot * Vector3.forward) * 30, rot);
                    var rt = new RenderTexture(2400, 700, 24) { antiAliasing = 4 };
                    cam.targetTexture = rt; cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(2400, 700, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 2400, 700), 0, 0); tex.Apply();
                    RenderTexture.active = prev; cam.targetTexture = null;
                    var file = Path.Combine((string)req["out_dir"], $"{(string)req["tag"]}-{name}.png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                    shots.Add(file);
                }
            }
            finally { EditorSceneManager.CloseScene(temp, true); }
            if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("ACTIVE_SCENE_DIRTIED");
            return new { shots };
        }
    }
}
