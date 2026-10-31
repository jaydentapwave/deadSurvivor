using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class UnityAsset
{
    const string Project = "/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor";
    [Serializable] public class Request { public string source, destination, run_id; public int texture_size; }
    [Serializable] public class SavedScene { public string path; public bool isLoaded, isActive; }
    [Serializable] public class Saved { public SavedScene[] scenes; public string[] selection; public bool background; }

    static string[] StableSelection()
    {
        return Selection.objects.Select(obj=> {
            var id=GlobalObjectId.GetGlobalObjectIdSlow(obj);
            if(id.identifierType==0) throw new Exception("Unsupported transient selection; preserve user state");
            return id.ToString();
        }).ToArray();
    }

    static void Target()
    {
        if (Application.dataPath != Project + "/Assets") throw new Exception("Wrong Unity project");
    }

    public static object Snapshot()
    {
        Target();
        var tick = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand")).FirstOrDefault(t => t != null);
        if (tick == null) throw new Exception("Cannot read auto-tick state");
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        bool enabled = (bool)tick.GetField("s_Enabled", flags).GetValue(null);
        long interval = (long)tick.GetField("s_IntervalMs", flags).GetValue(null);
        var scenes = EditorSceneManager.GetSceneManagerSetup();
        bool dirty = false;
        for(int i=0;i<SceneManager.sceneCount;i++) dirty |= SceneManager.GetSceneAt(i).isDirty;
        // Shader compiler dirty flags do not represent editable serialized asset values.
        var dirtyAssets=Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(o=>!(o is Shader) && EditorUtility.IsPersistent(o) && EditorUtility.IsDirty(o))
            .Select(AssetDatabase.GetAssetPath).Where(path=>path.StartsWith("Assets/")).Distinct().OrderBy(path=>path).ToArray();
        return new { project=Project, pid=System.Diagnostics.Process.GetCurrentProcess().Id,
            playing=EditorApplication.isPlaying, dirty, dirtyAssets, scenes, selection=StableSelection(),
            background=Application.runInBackground, disabledReload=EditorSettings.enterPlayModeOptionsEnabled,
            tickEnabled=enabled, tickInterval=interval,
            persistedTickEnabled=SessionState.GetBool("Unity.Pipeline.AutoTick.Enabled", false),
            persistedTickInterval=SessionState.GetInt("Unity.Pipeline.AutoTick.IntervalMs", -1) };
    }

    public static object Ownership()
    {
        Target();
        return new {pid=System.Diagnostics.Process.GetCurrentProcess().Id,
            playing=EditorApplication.isPlaying,scene=SceneManager.GetActiveScene().path,
            owner=SessionState.GetString("DS.Asset012.Owner","")};
    }

    static Bounds BoundsOf(GameObject obj)
    {
        var rr=obj.GetComponentsInChildren<Renderer>(true);
        if(rr.Length==0) throw new Exception("No renderer");
        var b=rr[0].bounds; foreach(var r in rr) b.Encapsulate(r.bounds); return b;
    }

    static System.Collections.Generic.Dictionary<string,string> Hashes(string prefab)
    {
        var hashes=new System.Collections.Generic.Dictionary<string,string>();
        foreach(var dependency in AssetDatabase.GetDependencies(prefab,true).Where(s=>s.StartsWith("Assets/")))
        foreach(var name in new[]{dependency,dependency+".meta"}) {
            var full=Path.Combine(Project,name);
            using(var stream=File.OpenRead(full))
            using(var sha=System.Security.Cryptography.SHA256.Create())
                hashes[name]=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        return hashes;
    }

    public static object Measure(string prefab)
    {
        Target();
        var beforeHashes=Hashes(prefab);
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        if(!model) throw new Exception("Prefab missing");
        var preview=EditorSceneManager.NewPreviewScene();
        var root=(GameObject)PrefabUtility.InstantiatePrefab(model,preview);
        try {
            var meshes=root.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).ToArray();
            var renderers=root.GetComponentsInChildren<Renderer>(true);
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            var b=BoundsOf(root); var mat=materials.FirstOrDefault();
            var baseTex=mat ? mat.GetTexture("_BaseMap") : null;
            var normal=mat ? mat.GetTexture("_BumpMap") : null;
            var forbidden=root.GetComponentsInChildren<Component>(true).Where(c=>c==null || !(c is Transform || c is MeshFilter || c is MeshRenderer)).Select(c=>c ? c.GetType().FullName : "MissingScript").ToArray();
            var afterHashes=Hashes(prefab);
            if(beforeHashes.Count!=afterHashes.Count || beforeHashes.Any(kv=>!afterHashes.ContainsKey(kv.Key) || afterHashes[kv.Key]!=kv.Value)) throw new Exception("Asset changed during measurement");
            return new { triangles=meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3)),
                vertices=meshes.Sum(m=>m.vertexCount),meshes=meshes.Length,materials=materials.Length,
                size=new[]{b.size.x,b.size.y,b.size.z},center=new[]{b.center.x,b.center.y,b.center.z},bottom=b.min.y,
                textures=new{basecolor=baseTex ? new[]{baseTex.width,baseTex.height}:new[]{0,0},normal=normal ? new[]{normal.width,normal.height}:new[]{0,0}},
                shader=mat && mat.shader ? mat.shader.name : "",shader_supported=mat && mat.shader && mat.shader.isSupported,
                forbidden_components=forbidden,unity=Application.unityVersion,
                dependency_hashes=beforeHashes,prefab, guid=AssetDatabase.AssetPathToGUID(prefab)};
        } finally { UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview); }
    }

    public static object Build(string requestPath)
    {
        Target();
        if(EditorApplication.isPlaying) throw new Exception("Play active");
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene");
        var q=JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
        if(q.destination != "Assets/DeadSurvivor/Art/Tests/Asset012_"+q.run_id || q.run_id.Length!=32 || !q.run_id.All(Uri.IsHexDigit)) throw new Exception("Invalid owned destination");
        var directory=Path.Combine(Project,q.destination);
        if(Directory.Exists(directory) || File.Exists(directory+".meta")) throw new Exception("Candidate exists; refusing overwrite");
        Directory.CreateDirectory(directory);
        foreach(var name in new[]{"model.fbx","basecolor.png","normal.png"}) File.Copy(Path.Combine(q.source,name),Path.Combine(directory,name),false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var name in new[]{"basecolor","normal"}) {
            var ti=(TextureImporter)AssetImporter.GetAtPath(q.destination+"/"+name+".png");
            ti.textureType=name=="normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture=name=="basecolor"; ti.maxTextureSize=q.texture_size; ti.SaveAndReimport();
        }
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(q.destination+"/model.fbx");
        if(!model) throw new Exception("FBX not imported");
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(!shader || !shader.isSupported) throw new Exception("Required shader unavailable");
        var mat=new Material(shader){name="RockTransfer"};
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(q.destination+"/basecolor.png"));
        mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(q.destination+"/normal.png"));
        mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.7f);mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Metallic",0);
        AssetDatabase.CreateAsset(mat,q.destination+"/Rock.mat");
        var buildScene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("RockTransferTest");SceneManager.MoveGameObjectToScene(root,buildScene);
        try {
            var mesh=(GameObject)PrefabUtility.InstantiatePrefab(model,buildScene);mesh.transform.SetParent(root.transform,false);
            foreach(var r in mesh.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
            // No hidden scale repair here: the processed FBX must satisfy the contract itself.
            PrefabUtility.SaveAsPrefabAsset(root,q.destination+"/Rock.prefab");
        } finally { UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(buildScene); }
        AssetDatabase.SaveAssetIfDirty(mat);
        var measured=Measure(q.destination+"/Rock.prefab");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var loaded=AssetDatabase.LoadAssetAtPath<GameObject>(q.destination+"/Rock.prefab");
        PrefabUtility.InstantiatePrefab(loaded);
        var camera=new GameObject("Asset012Camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.orthographic=true;camera.orthographicSize=2.2f;camera.nearClipPlane=.1f;camera.farClipPlane=50;
        camera.transform.position=new Vector3(4,3.6f,-4);camera.transform.LookAt(new Vector3(0,.7f,0));
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.13f,.15f);
        var light=new GameObject("Asset012Light").AddComponent<Light>();light.type=LightType.Directional;
        light.intensity=2;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-25,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.58f,.62f);RenderSettings.skybox=null;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="PreviewGround";ground.transform.localScale=Vector3.one*.6f;
        var groundMaterial=new Material(shader);groundMaterial.SetColor("_BaseColor",new Color(.18f,.23f,.22f));
        AssetDatabase.CreateAsset(groundMaterial,q.destination+"/Ground.mat");ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
        EditorSceneManager.SaveScene(scene,q.destination+"/Preview.unity");AssetDatabase.SaveAssetIfDirty(groundMaterial);
        return measured;
    }

    public static object Restore(string snapshotPath, string ownedScene)
    {
        Target();
        if(EditorApplication.isPlaying) throw new Exception("Stop owned Play before restoring scenes");
        var snapshot=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(snapshotPath));
        var setups=snapshot["scenes"].Select(s=>new SceneSetup{path=(string)s["path"],isLoaded=(bool)s["isLoaded"],isActive=(bool)s["isActive"]}).ToArray();
        var active=SceneManager.GetActiveScene();
        if(active.path!=ownedScene && !setups.Any(s=>s.path==active.path)) throw new Exception("User scene change; refusing restore");
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("Dirty scene conflict; preserve user state");
        EditorSceneManager.RestoreSceneManagerSetup(setups);
        Selection.objects=snapshot["selection"].Select(token=> {
            GlobalObjectId id;
            if(!GlobalObjectId.TryParse((string)token,out id)) throw new Exception("Invalid saved selection");
            var obj=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            if(!obj) throw new Exception("Selected object missing after scene restore");
            return obj;
        }).ToArray();Application.runInBackground=(bool)snapshot["background"];
        return Snapshot();
    }
}
