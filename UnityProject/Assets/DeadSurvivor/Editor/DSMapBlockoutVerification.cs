using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DeadSurvivor.Editor
{
    public static class DSMapBlockoutVerification
    {
        public const string Folder="Assets/DeadSurvivor/Scenes/Map018_r001";
        public const string ScenePath=Folder+"/MapBlockout018_r001.unity";
        public const string PrefabPath=Folder+"/MapBlockout018_r001.prefab";
        const string Project="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor";
        const string Reports="/Users/hyeongwoo/Documents/ChatGPT/데드 서바이버/output/map-blockout-018/";
        const string RockPath="Assets/DeadSurvivor/Art/Tests/Asset012_69f5e6272f3f4368aa0c9babb64262e8/Rock.prefab";
        static DSMapBlockoutPreview current;
        static Keyboard keyboard,previous;
        [Serializable] public class BuildRequest { public string data_file,data_sha256,output_report,run_id;public HashRow[] expected_rock_dependencies;public int fail_after_assets; }
        [Serializable] public class OwnedFiles {public HashRow[] files;}
        [Serializable] public class HashRow { public string path,sha256; }
        [Serializable] public class RockRecord
        {public Vector3 center,bounds_size;public float bottom,projection_radius,display_scale,inradius_world;public Vector2 center_px;public HashRow[] dependencies;}
        [Serializable] public class BuildRecord
        {public string status="PASS",scene,prefab,data_sha256;public int objects;public RockRecord rock;public HashRow[] generated;}
        [Serializable] public class CaseRequest
        {public string run_id,nonce,case_id,output;public float scale;public Vector2 center,input;public int steps,reverse_at=-1;public bool device;}
        [Serializable] public class CaseResult
        {public string scope="BLOCKOUT_PREVIEW_ONLY",run_id,nonce,case_id,request_sha256,error,data_sha256;public float scale;public int instance_id;public List<DSBlockoutFrame> frames;}
        [Serializable] public class MeshRecord {public string id;public Vector3[] vertices,world_vertices;public int[] triangles;public Vector3 local_scale;}
        [Serializable] public class AuditRecord
        {public string scope="BLOCKOUT_PREVIEW_ONLY",data_sha256;public float scale,k;public MeshRecord[] meshes;public Vector3 rock_center,rock_size;public float rock_bottom;public Vector3[] rock_corners;public DSBlockoutCameraRecord camera;}
        [Serializable] public class UnitRecord {public string status="PASS";public int count;public string[] checks;}
        static string Allowed(string path)
        {path=Path.GetFullPath(path);if(!path.StartsWith(Reports,StringComparison.Ordinal))throw new Exception("INVALID_REPORT_PATH");return path;}
        static string FileHash(string path)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }
        static void Target() {if(Application.dataPath!=Project+"/Assets")throw new Exception("WRONG_PROJECT");}
        static HashRow[] Dependencies() => AssetDatabase.GetDependencies(RockPath,true).Where(s=>s.StartsWith("Assets/",StringComparison.Ordinal))
            .SelectMany(s=>new[]{s,s+".meta"}).OrderBy(s=>s,StringComparer.Ordinal).Select(s=>new HashRow {path=s,sha256=FileHash(Path.Combine(Project,s))}).ToArray();
        static bool Same(HashRow[] a,HashRow[] b) => a.Length==b.Length && a.Zip(b,(x,y)=>x.path==y.path && x.sha256==y.sha256).All(x=>x);
        static Vector3[] MeshBoundCorners(GameObject root)
        {
            var corners=new List<Vector3>();
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!mf.sharedMesh)throw new Exception("MISSING_ROCK_MESH");
                var b=mf.sharedMesh.bounds;
                for(int i=0;i<8;i++)corners.Add(mf.transform.TransformPoint(new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z)));
            }
            if(corners.Count==0)throw new Exception("NO_ROCK_MESH");return corners.ToArray();
        }
        static Bounds Bounds(Vector3[] p)
        {var b=new Bounds(p[0],Vector3.zero);foreach(var q in p)b.Encapsulate(q);return b;}
        static double PointDistance(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;double t=Math.Max(0,Math.Min(1,Vector2.Dot(p-a,d)/d.sqrMagnitude));return Vector2.Distance(p,a+d*(float)t);}
        static Material Material(string name,Color color,bool twoSided=false)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new Exception("URP_LIT_MISSING");
            var m=new Material(shader){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);if(twoSided)m.SetFloat("_Cull",0);return m;
        }
        static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        static void RestoreSnapshot(DSMapMotionVerification.SnapshotData saved)
        {
            EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(s=>new SceneSetup {path=s.path,isLoaded=s.isLoaded,isActive=s.isActive}).ToArray());
            Selection.objects=saved.selection.Select(s=>{if(!GlobalObjectId.TryParse(s,out var id))throw new Exception("INVALID_SELECTION");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);}).ToArray();
        }
        public static string Build(string requestPath)
        {
            Target();var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
            var request=JsonUtility.FromJson<BuildRequest>(File.ReadAllText(Allowed(requestPath)));
            if(request.run_id.Length!=32 || !request.run_id.All(Uri.IsHexDigit) || FileHash(Allowed(request.data_file))!=request.data_sha256)
                throw new Exception("BUILD_INPUT_MISMATCH");
            if(AssetDatabase.IsValidFolder(Folder) || Directory.Exists(Path.Combine(Project,Folder)))throw new Exception("PREVIEW_DESTINATION_EXISTS");
            var data=JsonUtility.FromJson<DSBlockoutData>(File.ReadAllText(request.data_file));data.Validate();
            var logical=data.objects.ToDictionary(o=>o.id,o=>o.points_px.Select(p=>data.Logical(p,4)).ToArray());
            data.Geometry(4);
            var meshes=new Dictionary<string,Mesh>();
            var dependencies=Dependencies();GameObject root=null;Scene created=default;
            if(request.expected_rock_dependencies==null || !Same(request.expected_rock_dependencies,dependencies))throw new Exception("ROCK_FROZEN_INPUT_MISMATCH");
            var ownedFiles=new Dictionary<string,string>();string journal=Path.Combine(Path.GetDirectoryName(requestPath),"created-files.json");
            void Confirm(params string[] declared)
            {
                foreach(var rel in declared.SelectMany(p=>new[]{p,p+".meta"}).Where(p=>File.Exists(Path.Combine(Project,p))))
                {
                    string hash=FileHash(Path.Combine(Project,rel));
                    if(ownedFiles.ContainsKey(rel) && ownedFiles[rel]!=hash)throw new Exception("CREATED_FILE_CHANGED");
                    ownedFiles[rel]=hash;
                }
                var json=JsonUtility.ToJson(new OwnedFiles {files=ownedFiles.OrderBy(p=>p.Key).Select(p=>new HashRow {path=p.Key,sha256=p.Value}).ToArray()},true);
                string temp=journal+".pending";File.WriteAllText(temp,json);if(File.Exists(journal))File.Replace(temp,journal,null);else File.Move(temp,journal);
            }
            var pendingNative=new List<UnityEngine.Object>();
            try
            {
                void Stage(string name,Mesh mesh){meshes.Add(name,mesh);pendingNative.Add(mesh);}
                foreach(var o in data.objects)Stage(o.id,DSBlockoutMesh.Prism(logical[o.id],data.world_scale,o.height_logical));
                Stage("Ground",DSBlockoutMesh.Prism(data.boundary_px.Select(p=>data.Logical(p,4)).ToArray(),data.world_scale,0,true));
                Stage("PERIM-01",DSBlockoutMesh.Perimeter(data.boundary_px.Select(p=>data.Logical(p,4)).ToArray(),data.world_scale,32));
                AssetDatabase.CreateFolder("Assets/DeadSurvivor/Scenes","Map018_r001");AssetDatabase.CreateFolder(Folder,"Meshes");
                Confirm(Folder,Folder+"/Meshes");
                File.Copy(request.data_file,Path.Combine(Project,Folder,"Layout.json"));AssetDatabase.ImportAsset(Folder+"/Layout.json",ImportAssetOptions.ForceSynchronousImport);
                Confirm(Folder+"/Layout.json");
                int createdAssets=0;
                foreach(var pair in meshes){pair.Value.name=pair.Key;string asset=Folder+"/Meshes/"+pair.Key+".asset";AssetDatabase.CreateAsset(pair.Value,asset);pendingNative.Remove(pair.Value);Confirm(asset);
                    if(request.fail_after_assets>0 && ++createdAssets==request.fail_after_assets)throw new Exception("INJECTED_BUILD_FAILURE");}
                var ground=Material("Ground",new Color(.27f,.35f,.22f));var solid=Material("Candidate Stone",new Color(.55f,.59f,.55f));var rim=Material("Candidate Boundary",new Color(.38f,.45f,.39f),true);
                pendingNative.AddRange(new[]{ground,solid,rim});
                foreach(var mat in new[]{ground,solid,rim}){string asset=Folder+"/"+mat.name+".mat";AssetDatabase.CreateAsset(mat,asset);pendingNative.Remove(mat);Confirm(asset);}
                created=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                root=new GameObject("MapBlockout018_r001");
                var preview=root.AddComponent<DSMapBlockoutPreview>();preview.Layout=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/Layout.json");preview.ExpectedDataSha=request.data_sha256;
                var geometry=new GameObject("CandidateGeometry").transform;geometry.SetParent(root.transform,false);preview.StaticGeometry=geometry;
                MeshObject("Ground",geometry,meshes["Ground"],ground);
                MeshObject("PERIM-01",geometry,meshes["PERIM-01"],rim);
                foreach(var o in data.objects)MeshObject(o.id,geometry,meshes[o.id],solid);
                var rockHolder=new GameObject("ExistingTripoRock_OB-001").transform;rockHolder.SetParent(root.transform,false);
                var rock=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RockPath),rockHolder);
                if(rock.GetComponentsInChildren<Component>(true).Any(c=>!(c is Transform || c is MeshFilter || c is MeshRenderer))) throw new Exception("FORBIDDEN_ROCK_COMPONENT");
                var b=Bounds(MeshBoundCorners(rock));var poly=logical[data.rock_object_id];var center=data.Logical(data.rock_center_px,4);
                float inradius=(float)Enumerable.Range(0,poly.Length).Min(i=>PointDistance(center,poly[i],poly[(i+1)%poly.Length]))*data.world_scale;
                if(!new DSMapGeometry(poly,Array.Empty<Vector2[]>(),.001,16384).SafeSweep(center,center,.01))throw new Exception("ROCK_CENTER_OUTSIDE_C");
                float r=Mathf.Sqrt(b.extents.x*b.extents.x+b.extents.z*b.extents.z);
                if(r<=0 || !DSMapGeometry.Finite(r))throw new Exception("INVALID_ROCK_BOUNDS");
                float scale=inradius*.9f/r;rockHolder.localScale=Vector3.one*scale;
                var offset=-new Vector3(b.center.x,b.min.y,b.center.z)*scale;
                rockHolder.localPosition=DSBlockoutMesh.World(center,data.world_scale)+offset;
                preview.RockHolder=rockHolder;preview.RockPlacementOffset=offset;
                var cb=new GameObject("PreviewCamera",typeof(Camera));cb.transform.SetParent(root.transform,false);cb.tag="MainCamera";
                var camera=cb.GetComponent<Camera>();camera.orthographic=true;camera.backgroundColor=new Color(.07f,.10f,.10f);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.nearClipPlane=.1f;camera.farClipPlane=500;camera.transform.position=new Vector3(0,150,0);camera.transform.rotation=Quaternion.Euler(90,0,0);camera.orthographicSize=45;
                preview.ViewCamera=camera;
                var lightObject=new GameObject("PreviewSun",typeof(Light));lightObject.transform.SetParent(root.transform,false);lightObject.transform.rotation=Quaternion.Euler(55,-30,0);
                var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.shadows=LightShadows.Soft;
                if(!Same(dependencies,Dependencies()))throw new Exception("ROCK_DEPENDENCY_CHANGED");
                var measured=Bounds(MeshBoundCorners(rock));
                var record=new BuildRecord {scene=ScenePath,prefab=PrefabPath,data_sha256=request.data_sha256,objects=data.objects.Length,
                    rock=new RockRecord {center=measured.center,bounds_size=measured.size,bottom=measured.min.y,projection_radius=r*scale,display_scale=scale,inradius_world=inradius,center_px=data.rock_center_px,dependencies=dependencies}};
                PrefabUtility.SaveAsPrefabAssetAndConnect(root,PrefabPath,InteractionMode.AutomatedAction);
                Confirm(PrefabPath);
                if(!EditorSceneManager.SaveScene(created,ScenePath))throw new Exception("SCENE_SAVE_FAILED");
                Confirm(ScenePath);
                record.generated=Directory.GetFiles(Path.Combine(Project,Folder),"*",SearchOption.AllDirectories).Concat(new[]{Path.Combine(Project,Folder+".meta")}).OrderBy(s=>s,StringComparer.Ordinal).Select(s=>new HashRow {path=s.Substring(Project.Length+1).Replace('\\','/'),sha256=FileHash(s)}).ToArray();
                string json=JsonUtility.ToJson(record,true);File.WriteAllText(Allowed(request.output_report),json);return json;
            }
            finally
            {
                foreach(var obj in pendingNative)if(obj)UnityEngine.Object.DestroyImmediate(obj);
                if(created.IsValid() && SceneManager.GetActiveScene()==created)
                {
                    if(SceneManager.sceneCount!=1 || created.GetRootGameObjects().Length!=1 || created.GetRootGameObjects()[0]!=root)
                        throw new Exception("BUILD_SCENE_OWNERSHIP_CONFLICT");
                    RestoreSnapshot(saved);
                }
            }
        }
        static void ReleaseKeyboard()
        {
            if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;
            if(previous!=null && previous.added)previous.MakeCurrent();previous=null;
        }
        public static string RecoverCreatedFolder(string requestPath)
        {
            Target();DSMapMotionVerification.Snapshot();
            var request=JsonUtility.FromJson<OwnedFiles>(File.ReadAllText(Allowed(requestPath)));
            if(!Directory.Exists(Path.Combine(Project,Folder)))throw new Exception("NO_OWNED_BUILD_FOLDER");
            if((File.GetAttributes(Path.Combine(Project,Folder))&FileAttributes.ReparsePoint)!=0 || Directory.GetDirectories(Path.Combine(Project,Folder),"*",SearchOption.AllDirectories).Any(p=>(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0))throw new Exception("BUILD_RECOVERY_SYMLINK");
            var paths=Directory.GetFiles(Path.Combine(Project,Folder),"*",SearchOption.AllDirectories).Concat(new[]{Path.Combine(Project,Folder+".meta")})
                .Select(p=>p.Substring(Project.Length+1).Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            var declared=request.files.OrderBy(p=>p.path,StringComparer.Ordinal).ToArray();
            if(!paths.SequenceEqual(declared.Select(p=>p.path)) || declared.Any(p=>FileHash(Path.Combine(Project,p.path))!=p.sha256 || (File.GetAttributes(Path.Combine(Project,p.path))&FileAttributes.ReparsePoint)!=0))
                throw new Exception("BUILD_RECOVERY_FILE_CONFLICT");
            if(Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Any(o=>EditorUtility.IsPersistent(o) && AssetDatabase.GetAssetPath(o).StartsWith(Folder+"/",StringComparison.Ordinal) && EditorUtility.IsDirty(o)))
                throw new Exception("BUILD_RECOVERY_DIRTY_ASSET");
            if(!AssetDatabase.DeleteAsset(Folder))throw new Exception("BUILD_RECOVERY_DELETE_FAILED");
            return "REMOVED_OWN_CONFIRMED_FOLDER";
        }
        static void AssertWrapper(GameObject wrapper,GameObject instance)
        {
            if(wrapper.GetComponents<Component>().Length!=1 || wrapper.transform.childCount!=1 || wrapper.transform.GetChild(0)!=instance.transform ||
                wrapper.transform.position!=Vector3.zero || wrapper.transform.rotation!=Quaternion.identity || wrapper.transform.localScale!=Vector3.one)
                throw new Exception("PREVIEW_WRAPPER_CONFLICT");
        }
        public static string BeginCase(string requestPath)
        {
            var request=JsonUtility.FromJson<CaseRequest>(File.ReadAllText(Allowed(requestPath)));
            if(!new[]{4f,6f,8f}.Contains(request.scale) || request.steps<1 || request.steps>1000)throw new Exception("INVALID_PREVIEW_CASE");
            string output=Allowed(request.output);if(File.Exists(output))throw new Exception("OUTPUT_EXISTS");
            var wrapper=DSMapMotionVerification.CreateExternalRoot(request.run_id,request.nonce,"Blockout018_"+request.nonce);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),wrapper.transform);
            current=instance.GetComponent<DSMapBlockoutPreview>();var preview=current;
            DSMapMotionVerification.GuardExternalRoot(request.run_id,request.nonce,wrapper,()=>{
                AssertWrapper(wrapper,instance);
                preview.AssertOwnedTree();
            },()=>{preview.CancelRecording();ReleaseKeyboard();});
            preview.ImageScale=request.scale;preview.InitialCenter=request.center;preview.Overview=false;preview.Elevation=55;
            preview.Record=true;preview.FixedInput=!request.device;preview.RecordSteps=request.steps;preview.TestInput=request.input;preview.ReverseAt=request.reverse_at;
            if(request.device)
            {
                previous=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>("Map017Keyboard_Blockout018_"+request.nonce);keyboard.MakeCurrent();preview.ExpectedDevice=keyboard.deviceId;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D));
            }
            preview.Completed+=()=>{
                ReleaseKeyboard();
                var result=new CaseResult {run_id=request.run_id,nonce=request.nonce,case_id=request.case_id,request_sha256=FileHash(requestPath),
                    error=preview.Error,data_sha256=preview.ExpectedDataSha,scale=preview.ImageScale,instance_id=preview.GetInstanceID(),frames=preview.Frames};
                if(!File.Exists(output)){var tmp=output+".pending";File.WriteAllText(tmp,JsonUtility.ToJson(result,true));File.Move(tmp,output);}
            };
            preview.Initialize();wrapper.SetActive(true);return preview.Error==""?"RUNNING":"REJECTED:"+preview.Error;
        }
        public static string Audit()
        {
            if(!current || !current.Ready || current.Error!="")throw new Exception("NO_READY_PREVIEW");current.AssertOwnedTree();
            var meshes=current.StaticGeometry.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("OB-")).Select(m=>new MeshRecord {
                id=m.name,vertices=m.sharedMesh.vertices,world_vertices=m.sharedMesh.vertices.Select(p=>m.transform.TransformPoint(p)).ToArray(),triangles=m.sharedMesh.triangles,local_scale=current.StaticGeometry.localScale}).ToArray();
            var corners=MeshBoundCorners(current.RockHolder.gameObject);var b=Bounds(corners);
            return JsonUtility.ToJson(new AuditRecord {data_sha256=current.ExpectedDataSha,scale=current.ImageScale,k=current.Data.world_scale,meshes=meshes,
                rock_center=b.center,rock_size=b.size,rock_bottom=b.min.y,rock_corners=corners,camera=current.CameraEvidence()},true);
        }
        public static string CameraView(float elevation,bool overview,bool focusRock)
        {
            if(!current || !current.Ready || current.Error!="")throw new Exception("NO_READY_PREVIEW");
            current.AssertOwnedTree();current.Elevation=elevation;current.Overview=overview;current.FocusRock=focusRock;current.FrameCamera();
            return JsonUtility.ToJson(current.CameraEvidence(),true);
        }
        [MenuItem("Dead Survivor/Map Blockout 018/Open Preview Scene")]
        public static void OpenPreview()
        {DSMapMotionVerification.Snapshot();EditorSceneManager.OpenScene(ScenePath);}
        public static string UnitTests()
        {
            var checks=new List<string>();void Check(bool value,string name){if(!value)throw new Exception("FAILED:"+name);checks.Add(name);}
            var wrapper=new GameObject("Unit wrapper");var inner=new GameObject("Unit preview");inner.transform.SetParent(wrapper.transform,false);
            try {
                AssertWrapper(wrapper,inner);Check(true,"owned-wrapper");
                var sibling=new GameObject("Unowned sibling");sibling.transform.SetParent(wrapper.transform,false);
                bool rejected=false;try{AssertWrapper(wrapper,inner);}catch(Exception){rejected=true;}Check(rejected,"unowned-wrapper-sibling-rejected");UnityEngine.Object.DestroyImmediate(sibling);
                var component=wrapper.AddComponent<BoxCollider>();rejected=false;try{AssertWrapper(wrapper,inner);}catch(Exception){rejected=true;}Check(rejected,"unowned-wrapper-component-rejected");UnityEngine.Object.DestroyImmediate(component);
            }finally{UnityEngine.Object.DestroyImmediate(wrapper);}
            var l=new[]{Vector2.zero,new Vector2(3,0),new Vector2(3,1),new Vector2(1,1),new Vector2(1,3),new Vector2(0,3)};
            foreach(var p in new[]{l,l.Reverse().ToArray(),l.Skip(2).Concat(l.Take(2)).ToArray()})
            {
                var mesh=DSBlockoutMesh.Prism(p,1,2);try {
                    Check(mesh.triangles.Length/3==20,"L-prism20");
                    double volume=0,topArea=0;var v=mesh.vertices;var t=mesh.triangles;
                    for(int i=0;i<t.Length;i+=3){var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];volume+=Vector3.Dot(a,Vector3.Cross(b,c))/6.0;
                        if(a.y==2&&b.y==2&&c.y==2){var cross=Vector3.Cross(b-a,c-a);Check(cross.y>0,"top-up");topArea+=cross.y/2;}}
                    Check(Math.Abs(topArea-5)<.00001 && Math.Abs(volume-10)<.00001,"L-area5-volume10");
                }finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            foreach(float s in new[]{4f,6f,8f})
            {
                Check(DSBlockoutMesh.World(new Vector2(s,0),.02f).x==s*.02f,"right-one-pixel:"+s);
                Check(DSBlockoutMesh.World(new Vector2(0,s),.02f).z==-s*.02f,"down-one-pixel:"+s);
            }
            var outer=new[]{new Vector2(-6000,-6000),new Vector2(6000,-6000),new Vector2(6000,6000),new Vector2(-6000,6000)};
            var map=new DSMapGeometry(outer,Array.Empty<Vector2[]>(),.001,16384);
            var proposal=DSMapMotion.Propose(map,new Vector2(5040,4240),new[]{new Vector2(5040,4240)},new[]{Vector2.zero},new[]{true},new[]{8f},8,Vector2.right,.05f);
            Check(proposal.Center==new Vector2(5047,4240),"extended-map-motion");
            Check(!DSMapGeometry.ValidPoint(new Vector2(5040,4240)) && map.ValidCoordinate(new Vector2(16384,0)) && !map.ValidCoordinate(new Vector2(16384.1f,0)),"default-and-instance-limit-isolation");
            foreach(double n in new[]{0d,-1d,double.NaN,double.PositiveInfinity,16385d})
            {bool rejected=false;try{new DSMapGeometry(outer,Array.Empty<Vector2[]>(),.001,n);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid-limit:"+n);}
            bool old=false;try{new DSMapGeometry(outer,Array.Empty<Vector2[]>(),.001);}catch(ArgumentException){old=true;}Check(old,"default1024-retained");
            return JsonUtility.ToJson(new UnitRecord {count=checks.Count,checks=checks.ToArray()},true);
        }
    }
}
