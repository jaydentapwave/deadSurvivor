using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kit3D
{
    // 3dkit Unity side of the asset pipeline. .ai/3d-art/tools/assets.py writes <work_root>/<Entry>-request.json and calls
    //   unity command script eval -- 'return Kit3D.AssetPipeline.Run("<Entry>", "<request path>");'
    // Every project value (paths, shader, texture slots, import settings, prefab options) comes from the request; game
    // types are reached only through IKit3DProjectHooks. Ported from an earlier project (Tools/Art/AssetBuild.cs, 050).
    //   Move    - one-time AssetDatabase moves (GUIDs kept)
    //   Build   - per item: texture import per map, material, model import (+ Mesh LOD), prefab (root at the feet ->
    //             Visual -> model normalised to its target height (default 1; terrain modules keep their real size), front turned to +Z); shared base prefab default material;
    //             project register hook. Never opens or edits a scene.
    //             An item with an "animation" block (catalog 'anim') imports its clips (Generic, plain names, loop flags) and keeps
    //             the model's Animator with a generated AnimatorController (states per clip, default state, optional Speed
    //             parameter idle <-> walk). Without it the import is unchanged (animation off, Animator removed).
    //             "part_materials" (catalog 'parts'): renderers named <label> / <label>_<k> get that part's own material.
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
                object animInfo = null;
                if (Str(it["model_src"]) != null)
                {
                    var anim = it["animation"] as JObject;
                    var fbx = ImportModel((string)it["model_src"], (string)it["model_dst"], (JObject)req["model_import"], (bool)it["mesh_lod"], anim);
                    AnimatorController ctrl = null;
                    if (anim != null) (ctrl, animInfo) = MakeController((string)it["model_dst"], anim);
                    var parts = new Dictionary<string, Material>();
                    if (it["part_materials"] is JObject pm)
                        foreach (var kv in pm)
                            parts[kv.Key] = AssetDatabase.LoadAssetAtPath<Material>((string)kv.Value) ?? throw new Exception("MISSING_PART_MATERIAL:" + kv.Key + ":" + kv.Value + " (build the part asset first)");
                    prefab = MakePrefab(fbx, (string)it["prefab"], mat, (float)it["yaw"], (string)it["shadow"], (JObject)req["prefab"],
                        it["target_height"] != null && it["target_height"].Type != JTokenType.Null ? (float)it["target_height"] : 1f, Str(it["anchor"]) == "top", ctrl, parts);
                    prefabs[id] = prefab;
                }
                rows.Add(new { id, textures = tex.ToDictionary(k => k.Key, k => AssetDatabase.GetAssetPath(k.Value)), material = AssetDatabase.GetAssetPath(mat),
                               prefab = prefab ? AssetDatabase.GetAssetPath(prefab) : null, animation = animInfo });
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
            // URP Lit's material validation copies _BaseMap into the legacy _MainTex on first inspect/refresh, so a kit material
            // showed as modified in git afterwards. Write the same texture/scale/offset here so the saved file is already stable.
            if (mat.HasProperty("_BaseMap") && mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", mat.GetTexture("_BaseMap"));
                mat.SetTextureScale("_MainTex", mat.GetTextureScale("_BaseMap"));
                mat.SetTextureOffset("_MainTex", mat.GetTextureOffset("_BaseMap"));
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
            // scalars that only make sense with a map (e.g. smoothness: _Smoothness = 1 so the map's A channel is the value;
            // URP Lit multiplies it). Applied after the preset, which holds the no-map single value.
            if (m["map_floats"] is JObject mapFloats)
            {
                foreach (var kv in mapFloats)
                {
                    if (!tex.ContainsKey(kv.Key)) continue;
                    foreach (var f in (JObject)kv.Value)
                    {
                        if (!mat.HasProperty(f.Key)) throw new Exception("NO_PROPERTY:" + f.Key);
                        mat.SetFloat(f.Key, (float)f.Value);
                    }
                }
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject ImportModel(string src, string dst, JObject s, bool meshLod, JObject anim)
        {
            if (!File.Exists(src)) throw new Exception("MISSING_MODEL:" + src);
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            var mi = (ModelImporter)AssetImporter.GetAtPath(dst);
            mi.materialImportMode = E<ModelImporterMaterialImportMode>(s["materials"]);
            if (anim != null) ImportClips(mi, dst, anim);
            else if (!(bool)s["animation"]) { mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None; }
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

        // the FBX clip names carry Blender's 'Armature|' prefixes (one more per re-save): a clip is found by its last '|' part
        static string PlainClipName(string n) => n.Substring(n.LastIndexOf('|') + 1);

        // Generic rig (the generated skeleton as it is, avatar from this model), one clip per wanted plain name, loop flags
        static void ImportClips(ModelImporter mi, string dst, JObject anim)
        {
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.clipAnimations = new ModelImporterClipAnimation[0];
            mi.SaveAndReimport();                         // defaultClipAnimations is read from an import with animation on
            var defs = mi.defaultClipAnimations;
            var loop = new HashSet<string>(anim["loop"].Select(x => (string)x));
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var want in anim["clips"].Select(x => (string)x))
            {
                var found = defs.Where(d => PlainClipName(d.takeName) == want || PlainClipName(d.name) == want).ToArray();
                if (found.Length != 1) throw new Exception("CLIP_NOT_FOUND_OR_AMBIGUOUS:" + want + " in " + dst + " (takes: " + string.Join(", ", defs.Select(d => d.takeName)) + ")");
                var c = found[0];
                c.name = want;
                c.loopTime = loop.Contains(want);
                c.loopPose = false;
                clips.Add(c);
            }
            mi.clipAnimations = clips.ToArray();
        }

        // <model folder>/<name>.controller: one state per clip (motion = the imported clip), default state, and with idle + walk a
        // float parameter (Speed) idle -> walk above the threshold, walk -> idle below it. Rebuilt in place so its GUID stays.
        static (AnimatorController, object) MakeController(string modelPath, JObject anim)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            string path = (string)anim["controller"];
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(0);
            if (ctrl.layers.Length == 0) ctrl.AddLayer("Base Layer");
            while (ctrl.layers.Length > 1) ctrl.RemoveLayer(1);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states.ToArray()) sm.RemoveState(cs.state);
            foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
            var states = new Dictionary<string, AnimatorState>();
            foreach (var name in anim["clips"].Select(x => (string)x))
            {
                if (!clips.TryGetValue(name, out var clip)) throw new Exception("CLIP_NOT_IMPORTED:" + name + " in " + modelPath);
                var st = sm.AddState(name);
                st.motion = clip;
                st.writeDefaultValues = true;
                states[name] = st;
            }
            sm.defaultState = states[(string)anim["default"]];
            string param = Str(anim["parameter"]);
            if (param != null)
            {
                float th = (float)anim["threshold"];
                ctrl.AddParameter(param, AnimatorControllerParameterType.Float);
                foreach (var (from, to, mode) in new[] { ("idle", "walk", AnimatorConditionMode.Greater), ("walk", "idle", AnimatorConditionMode.Less) })
                {
                    var tr = states[from].AddTransition(states[to]);
                    tr.hasExitTime = false; tr.hasFixedDuration = true; tr.duration = .1f;
                    tr.AddCondition(mode, th, param);
                }
            }
            if (anim["overlay"] is JObject overlay) AddOverlayLayer(ctrl, sm, states, modelPath, path, overlay);
            if (anim["overlays"] is JArray overlayList) foreach (var item in overlayList.OfType<JObject>()) AddOverlayLayer(ctrl, sm, states, modelPath, path, item);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssetIfDirty(ctrl);
            var info = new
            {
                controller = path, layer = ctrl.layers[0].name, default_state = sm.defaultState.name, parameter = param,
                threshold = param != null ? (float?)(float)anim["threshold"] : null,
                clips = states.Values.Select(st => { var c = (AnimationClip)st.motion; return new { state = st.name, clip = c.name, length = c.length, frameRate = c.frameRate, loop = c.isLooping }; }).ToArray()
            };
            return (ctrl, info);
        }

        // One more layer with an avatar mask on one bone subtree: empty default state, trigger from any state -> the clip -> back to empty.
        // The clip's state moves out of the base layer (nothing there plays it). The mask sits next to the controller, GUID kept.
        static void AddOverlayLayer(AnimatorController ctrl, AnimatorStateMachine baseMachine, Dictionary<string, AnimatorState> states, string modelPath, string controllerPath, JObject overlay)
        {
            string clipName = (string)overlay["clip"], trigger = (string)overlay["trigger"], maskRoot = (string)overlay["mask_root"];
            var motion = states[clipName].motion;
            baseMachine.RemoveState(states[clipName]);
            states.Remove(clipName);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var root = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == maskRoot);
            if (root == null) throw new Exception("OVERLAY_MASK_ROOT_NOT_FOUND:" + maskRoot + " in " + modelPath);
            string maskPath = Path.ChangeExtension(controllerPath, null) + "_" + clipName + ".mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, maskPath); }
            var all = model.GetComponentsInChildren<Transform>(true).Where(t => t != model.transform).ToArray();
            mask.transformCount = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(all[i], model.transform));
                mask.SetTransformActive(i, all[i] == root || all[i].IsChildOf(root));
            }
            EditorUtility.SetDirty(mask);
            ctrl.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            ctrl.AddLayer("Overlay");
            var layers = ctrl.layers;
            var added = layers.Length - 1;
            layers[added].avatarMask = mask;
            layers[added].defaultWeight = 1f;
            layers[added].blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = layers;
            var sm = ctrl.layers[added].stateMachine;
            var empty = sm.AddState("Empty");
            empty.writeDefaultValues = true;
            sm.defaultState = empty;
            var st = sm.AddState(clipName);
            st.motion = motion;
            st.writeDefaultValues = true;
            var enter = sm.AddAnyStateTransition(st);
            enter.hasExitTime = false; enter.hasFixedDuration = true; enter.duration = 0f; enter.canTransitionToSelf = true;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            var leave = st.AddTransition(empty);
            leave.hasExitTime = true; leave.exitTime = 1f; leave.hasFixedDuration = true; leave.duration = .05f;
        }

        // root (feet, identity) -> "Visual" -> model: turned by yaw so the front is +Z, scaled so its height is targetHeight
        // (default 1; a catalog 'height' or a terrain module's fit height + bury), centred on XZ, feet at 0 - or, for a
        // terrain module (anchorTop), the top face at 0 so the buried part stays below the cell
        // An animated item keeps the model's Animator (on the model object, so the clips' bone paths bind) with the controller,
        // no root motion; a static item loses it as before. Part renderers (name = label or label_k) get the part material.
        static GameObject MakePrefab(GameObject fbx, string path, Material mat, float yaw, string shadow, JObject p, float targetHeight, bool anchorTop,
            AnimatorController ctrl, Dictionary<string, Material> parts)
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
                if (ctrl)
                {
                    if (!anim) anim = model.AddComponent<Animator>();
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    if (!anim.avatar) anim.avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(fbx)).OfType<Avatar>().FirstOrDefault();
                }
                else if (anim) UnityEngine.Object.DestroyImmediate(anim);
                var rs = model.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) throw new Exception("NO_RENDERER:" + path);
                // held items (attach "Prop" meshes) hang below the feet in the bind pose (arms down): size and ground the
                // character from the body (+ hat, which counts in the hero height, art standard K1), not from the prop (S7 r5: 5.9 cm)
                var b = MeshBounds(SizingRenderers(rs));
                float s = targetHeight / Mathf.Max(.001f, b.size.y);
                model.transform.localScale = Vector3.one * s;
                model.transform.localPosition = new Vector3(-b.center.x * s, -(anchorTop ? b.max.y : b.min.y) * s, -b.center.z * s);
                var used = new HashSet<string>();
                foreach (var r in rs)
                {
                    string n = r.gameObject.name;
                    string label = parts.Keys.FirstOrDefault(k => n == k || n.StartsWith(k + "_"));
                    if (label != null) used.Add(label);
                    var m = label != null ? parts[label] : mat;
                    r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = shadow == "real" ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.receiveShadows = (bool)p["receive_shadows"];
                    r.lightProbeUsage = E<LightProbeUsage>(p["light_probes"]);
                    r.reflectionProbeUsage = E<ReflectionProbeUsage>(p["reflection_probes"]);
                }
                var unused = parts.Keys.Where(k => !used.Contains(k)).ToArray();
                if (unused.Length > 0) throw new Exception("PART_RENDERER_NOT_FOUND:" + string.Join(",", unused) + " in " + path + " (re-run assets.py attach: it names the part meshes)");
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

        // World bounds of the renderers' meshes. A SkinnedMeshRenderer's bounds are the importer's animation-wide box (larger than
        // the mesh: rigged models came out ~5% small with floating feet), so a skinned mesh uses its bind-pose vertices (mesh space
        // = the renderer's transform space). MeshRenderer bounds are unchanged.
        // renderers that define a prefab's size: everything except held items (attach names them "Prop", "Prop_1", ...)
        static Renderer[] SizingRenderers(Renderer[] rs)
        {
            var body = rs.Where(r => !(r.gameObject.name == "Prop" || r.gameObject.name.StartsWith("Prop_"))).ToArray();
            return body.Length > 0 ? body : rs;
        }

        static Bounds MeshBounds(Renderer[] rs)
        {
            Bounds? total = null;
            foreach (var r in rs)
            {
                var b = r.bounds;
                if (r is SkinnedMeshRenderer sm && sm.sharedMesh && sm.sharedMesh.vertexCount > 0)
                {
                    var m = sm.transform.localToWorldMatrix;
                    var vs = sm.sharedMesh.vertices;
                    b = new Bounds(m.MultiplyPoint3x4(vs[0]), Vector3.zero);
                    foreach (var v in vs) b.Encapsulate(m.MultiplyPoint3x4(v));
                }
                if (total.HasValue) { var t = total.Value; t.Encapsulate(b); total = t; } else total = b;
            }
            return total.Value;
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
                    var b = MeshBounds(SizingRenderers(rs));
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
