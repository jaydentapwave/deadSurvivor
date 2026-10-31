using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DeadSurvivor.Editor
{
    [InitializeOnLoad]
    public static class DSMapMotionVerification
    {
        const string Key="DS.Map017.";
        const string Project="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor";
        const string Reports="/Users/hyeongwoo/Documents/ChatGPT/데드 서바이버/output/map-i2-017/";
        static DSMovementFixture fixture;
        static GameObject[] roots;
        static Keyboard keyboard,previousKeyboard;
        static Action externalGuard,externalCleanup;
        [Serializable] public class SavedScene { public string path;public bool isLoaded,isActive; }
        [Serializable] public class SnapshotData { public SavedScene[] scenes;public string[] selection;public bool background; }
        [Serializable] public class CaseRequest { public string run_id,nonce;public DSMotionFixtureSpec spec; }
        static DSMapMotionVerification() { EditorApplication.playModeStateChanged+=Transition; }
        static void Transition(PlayModeStateChange change)
        {
            if(SessionState.GetString(Key+"owner","")=="") return;
            var phase=SessionState.GetString(Key+"phase","");
            if(change==PlayModeStateChange.ExitingEditMode && phase!="ENTERING" ||
                change==PlayModeStateChange.EnteredPlayMode && phase!="ENTERING" ||
                change==PlayModeStateChange.ExitingPlayMode && phase!="EXITING" ||
                change==PlayModeStateChange.EnteredEditMode && phase!="EXITING")
                SessionState.SetString(Key+"phase","CONFLICT");
            else if(change==PlayModeStateChange.EnteredPlayMode)
            {
                if(!MarkerValid()) {SessionState.SetString(Key+"phase","CONFLICT");return;}
                Application.runInBackground=true;
                SessionState.SetString(Key+"play_settings",SceneSettings());
                SessionState.SetString(Key+"phase","PLAY");
            }
            else if(change==PlayModeStateChange.EnteredEditMode) SessionState.SetString(Key+"phase","STOPPED");
        }
        static void Target() {if(Application.dataPath!=Project+"/Assets") throw new Exception("WRONG_PROJECT");}
        static string MarkerName => "DSMapMotionOwner_"+SessionState.GetString(Key+"owner","").Replace(':','_');
        static GameObject[] CaseRoots() => SceneManager.GetActiveScene().GetRootGameObjects().Where(o=>o.name!=MarkerName).ToArray();
        static string SceneSettings() => EditorJsonUtility.ToJson(Unsupported.GetRenderSettings());
        static bool MarkerValid()
        {
            if(SceneManager.sceneCount!=1 || SceneManager.GetActiveScene().path!="") return false;
            var markers=SceneManager.GetActiveScene().GetRootGameObjects().Where(o=>o.name==MarkerName).ToArray();
            if(markers.Length!=1) return false;
            var m=markers[0];
            return m.GetComponents<Component>().Length==1 && m.transform.childCount==0 && m.transform.position==Vector3.zero &&
                m.transform.rotation==Quaternion.identity && m.transform.localScale==Vector3.one && m.activeSelf;
        }
        static void MarkerOnly() {if(!MarkerValid() || CaseRoots().Length!=0) throw new Exception("UNOWNED_SCENE_CHANGE");}
        static void Owner(string run,string nonce)
        {
            Target();
            if(SessionState.GetString(Key+"owner","")!=run+":"+nonce ||
                SessionState.GetInt(Key+"pid",-1)!=System.Diagnostics.Process.GetCurrentProcess().Id ||
                SessionState.GetString(Key+"phase","")=="CONFLICT" || !MarkerValid())
                throw new Exception("OWNERSHIP_CONFLICT");
            string phase=SessionState.GetString(Key+"phase","");
            if(phase=="PLAY" && SceneSettings()!=SessionState.GetString(Key+"play_settings","") ||
                (phase=="PREPARED" || phase=="STOPPED") && SceneSettings()!=SessionState.GetString(Key+"edit_settings",""))
                throw new Exception("SCENE_SETTINGS_CONFLICT");
        }
        static void CleanScenes()
        {
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("UNSAVED_SCENE");
        }
        public static object Snapshot()
        {
            Target();CleanScenes();
            if(EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorSettings.enterPlayModeOptionsEnabled)
                throw new Exception("PLAY_OR_NONSTANDARD_RELOAD");
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>string.IsNullOrEmpty(s.path))) throw new Exception("UNSAVED_SCENE_PATH");
            return new SnapshotData {scenes=scenes.Select(s=>new SavedScene {path=s.path,isLoaded=s.isLoaded,isActive=s.isActive}).ToArray(),background=Application.runInBackground,selection=Selection.objects.Select(o=>{
                var id=GlobalObjectId.GetGlobalObjectIdSlow(o);if(id.identifierType==0) throw new Exception("TRANSIENT_SELECTION");return id.ToString();}).ToArray()};
        }
        public static string Prepare(string run,string nonce)
        {
            Snapshot();
            if(run.Length!=32 || nonce.Length!=32 || !run.All(Uri.IsHexDigit) || !nonce.All(Uri.IsHexDigit) || SessionState.GetString(Key+"owner","")!="")
                throw new Exception("EXISTING_OWNER_OR_INVALID_ID");
            SessionState.SetString(Key+"owner",run+":"+nonce);
            SessionState.SetInt(Key+"pid",System.Diagnostics.Process.GetCurrentProcess().Id);
            SessionState.SetString(Key+"phase","PREPARED");
            SessionState.SetBool(Key+"background",Application.runInBackground);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            scene.name="DSMapMotion_"+nonce;
            new GameObject(MarkerName);
            SessionState.SetString(Key+"edit_settings",SceneSettings());
            Selection.objects=Array.Empty<UnityEngine.Object>();
            return "PREPARED";
        }
        public static string StartOwned(string run,string nonce)
        {
            Owner(run,nonce);MarkerOnly();
            if(SessionState.GetString(Key+"phase","")!="PREPARED" || EditorApplication.isPlaying) throw new Exception("INVALID_PHASE");
            SessionState.SetString(Key+"phase","ENTERING");EditorApplication.isPlaying=true;return "ENTERING";
        }
        public static string StopOwned(string run,string nonce)
        {
            Owner(run,nonce);
            if(SessionState.GetString(Key+"phase","")!="PLAY" || !EditorApplication.isPlaying) throw new Exception("INVALID_PHASE");
            ReleaseCase();
            Application.runInBackground=SessionState.GetBool(Key+"background",false);
            SessionState.SetString(Key+"phase","EXITING");EditorApplication.isPlaying=false;return "EXITING";
        }
        public static string Restore(string run,string nonce,string snapshotPath)
        {
            Owner(run,nonce);MarkerOnly();
            string phase=SessionState.GetString(Key+"phase","");
            if(EditorApplication.isPlaying || phase!="STOPPED" && phase!="PREPARED") throw new Exception("INVALID_RESTORE_PHASE");
            var saved=JsonUtility.FromJson<SnapshotData>(File.ReadAllText(ReportPath(snapshotPath)));
            if(Application.runInBackground!=saved.background) throw new Exception("BACKGROUND_OWNERSHIP_CONFLICT");
            var setup=saved.scenes.Select(s=>new SceneSetup {path=s.path,isLoaded=s.isLoaded,isActive=s.isActive}).ToArray();
            if(setup.Any(s=>string.IsNullOrEmpty(s.path)) || setup.Count(s=>s.isActive)!=1 || !setup.Any(s=>s.isLoaded)) throw new Exception("INVALID_SAVED_SCENES");
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            var selection=saved.selection.Select(s=>{
                if(!GlobalObjectId.TryParse(s,out var id)) throw new Exception("INVALID_SELECTION");
                var obj=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);if(obj==null) throw new Exception("MISSING_SELECTION");return obj;}).ToArray();
            Selection.objects=selection;Application.runInBackground=saved.background;
            SessionState.EraseString(Key+"owner");SessionState.EraseString(Key+"phase");SessionState.EraseInt(Key+"pid");
            SessionState.EraseString(Key+"edit_settings");SessionState.EraseString(Key+"play_settings");
            SessionState.EraseBool(Key+"background");
            return "RESTORED";
        }
        public static object Ownership() => new {owner=SessionState.GetString(Key+"owner",""),phase=SessionState.GetString(Key+"phase",""),
            pid=System.Diagnostics.Process.GetCurrentProcess().Id,playing=EditorApplication.isPlaying,scene=SceneManager.GetActiveScene().name,
            marker=MarkerValid()?MarkerName:"",
            devices=InputSystem.devices.Count(d=>d.name.StartsWith("Map017Keyboard"))};
        static string ReportPath(string path)
        {
            string full=Path.GetFullPath(path);
            if(!full.StartsWith(Reports,StringComparison.Ordinal) && !full.StartsWith("/Users/hyeongwoo/Documents/ChatGPT/데드 서바이버/output/map-blockout-018/",StringComparison.Ordinal)) throw new Exception("INVALID_REPORT_PATH");
            return full;
        }
        static void ReleaseKeyboard()
        {
            if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard=null;
            if(previousKeyboard!=null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            previousKeyboard=null;
        }
        static void ReleaseCase()
        {
            externalGuard?.Invoke();
            externalCleanup?.Invoke();externalCleanup=null;externalGuard=null;
            fixture?.Dispose();fixture=null;ReleaseKeyboard();
            if(roots!=null)
            {
                var current=CaseRoots();
                if(current.Any(o=>!roots.Contains(o))) throw new Exception("UNOWNED_SCENE_OBJECT");
                foreach(var o in roots) if(o!=null) UnityEngine.Object.DestroyImmediate(o);
                roots=null;
            }
        }
        public static GameObject CreateExternalRoot(string run,string nonce,string name)
        {
            Owner(run,nonce);
            if(!EditorApplication.isPlaying || SessionState.GetString(Key+"phase","")!="PLAY" || fixture!=null && !fixture.Terminal)
                throw new Exception("NOT_OWNED_IDLE_PLAY");
            if(name!="Blockout018_"+nonce)throw new Exception("INVALID_EXTERNAL_ROOT_NAME");
            ReleaseCase();MarkerOnly();
            var root=new GameObject(name);root.SetActive(false);roots=new[]{root};return root;
        }
        public static void GuardExternalRoot(string run,string nonce,GameObject root,Action guard,Action cleanup)
        {
            Owner(run,nonce);
            if(roots==null || roots.Length!=1 || roots[0]!=root || guard==null || cleanup==null)throw new Exception("INVALID_EXTERNAL_ROOT_GUARD");
            externalGuard=guard;externalCleanup=cleanup;
        }
        public static string BeginCase(string requestPath,string output)
        {
            var request=JsonUtility.FromJson<CaseRequest>(File.ReadAllText(ReportPath(requestPath)));
            Owner(request.run_id,request.nonce);
            if(!EditorApplication.isPlaying || SessionState.GetString(Key+"phase","")!="PLAY") throw new Exception("NOT_OWNED_PLAY");
            if(fixture!=null && !fixture.Terminal) throw new Exception("CASE_STILL_ACTIVE");
            output=ReportPath(output);if(File.Exists(output)) throw new Exception("OUTPUT_EXISTS");
            ReleaseCase();
            MarkerOnly();
            DSGame g=null;
            try
            {
                var cameraObject=new GameObject("Map017Camera",typeof(Camera));cameraObject.tag="MainCamera";
                var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
                var obj=new GameObject("Map017Game");obj.SetActive(false);g=obj.AddComponent<DSGame>();g.enabled=false;obj.SetActive(true);
                var s=request.spec;
                g.VerificationRun=true;g.Rng=new System.Random(17);g.LootRng=new System.Random(29);
                g.Selected=Enumerable.Range(0,s.count).ToList();g.Config.speed=s.speed;g.StartRun();
                g.Formation=g.PendingFormation=s.formation;g.Center=s.center;g.Elapsed=s.elapsed;
                if(s.actual.Length==0) s.actual=Enumerable.Range(0,s.count).Select(i=>g.Center+g.Offset(i)).ToArray();
                for(int i=0;i<g.Team.Count;i++) {g.Team[i].pos=s.actual[i];if(!s.alive[i])g.Team[i].hp=0;}
                // UI remains visible, but it cannot submit a gameplay state command during this probe.
                foreach(var ray in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsSortMode.None)) ray.enabled=false;
                if(s.mode=="DEVICE_SMOKE")
                {
                    previousKeyboard=Keyboard.current;
                    keyboard=InputSystem.AddDevice<Keyboard>("Map017Keyboard_"+request.nonce);keyboard.MakeCurrent();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D));
                }
                fixture=new DSMovementFixture(g,s,request.run_id,request.nonce,keyboard,done=>{
                    g.enabled=false;ReleaseKeyboard();
                    done.Result.request_sha256=FileHash(requestPath);
                    string tmp=output+".pending";File.WriteAllText(tmp,JsonUtility.ToJson(done.Result,true));File.Move(tmp,output);
                });
                g.BindMovementFixture(fixture);
                DrawGeometry(s);
                roots=CaseRoots();
                g.enabled=true;
                return "RUNNING";
            }
            catch(Exception e)
            {
                if(g!=null)g.enabled=false;ReleaseKeyboard();
                roots=CaseRoots();
                fixture?.Fail(e.Message);
                var result=new DSMotionFixtureResult {run_id=request.run_id,nonce=request.nonce,case_id=request.spec.id,spec=request.spec,
                    instance_id=g==null?0:g.GetInstanceID(),error=e.Message,request_sha256=FileHash(requestPath)};
                if(!File.Exists(output)) File.WriteAllText(output,JsonUtility.ToJson(result,true));
                return "REJECTED:"+e.Message;
            }
        }

        static string FileHash(string path)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }

        static void DrawGeometry(DSMotionFixtureSpec spec)
        {
            var root=new GameObject("Map017Geometry").transform;
            var square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);
            void Draw(Vector2[] points,Color color)
            {
                for(int i=0;i<points.Length;i++)
                {
                    var a=points[i];var b=points[(i+1)%points.Length];
                    var p=new Vector3(a.x/42,-a.y/42,0);var q=new Vector3(b.x/42,-b.y/42,0);
                    var go=new GameObject("Fixture edge",typeof(SpriteRenderer));go.transform.SetParent(root);
                    go.transform.position=(p+q)*.5f;go.transform.localScale=new Vector3(Vector3.Distance(p,q),.08f,1);
                    go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(q.y-p.y,q.x-p.x)*Mathf.Rad2Deg);
                    var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=square;renderer.color=color;renderer.sortingOrder=1;
                }
            }
            Draw(spec.boundary,new Color(.55f,.72f,.4f));
            foreach(var solid in spec.obstacles) Draw(solid.points,new Color(.85f,.6f,.35f));
        }

        static Vector2[] Box(float x0,float y0,float x1,float y1) => new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1)};
        public static object MarkerGuardTests(string run,string nonce)
        {
            Owner(run,nonce);MarkerOnly();
            var m=SceneManager.GetActiveScene().GetRootGameObjects().Single();
            int checks=0;
            void Rejected() {bool rejected=false;try{Owner(run,nonce);}catch(Exception){rejected=true;}if(!rejected)throw new Exception("MARKER_GUARD_FAILED");checks++;}
            var name=m.name;
            m.name="Wrong marker";try{Rejected();}finally{m.name=name;}
            var duplicate=new GameObject(name);try{Rejected();}finally{UnityEngine.Object.DestroyImmediate(duplicate);}
            var child=new GameObject("Unowned child");child.transform.SetParent(m.transform);try{Rejected();}finally{UnityEngine.Object.DestroyImmediate(child);}
            var original=RenderSettings.fogColor;RenderSettings.fogColor=Color.magenta;try{Rejected();}finally{RenderSettings.fogColor=original;}
            Owner(run,nonce);MarkerOnly();return new {status="PASS",checks};
        }
        [Serializable] public class FormationRow { public int count,formation; public Vector2[] offsets; }
        [Serializable] public class FormationRows { public FormationRow[] entries; }
        public static string FormationCatalog() => JsonUtility.ToJson(new FormationRows {entries=Enumerable.Range(1,4).SelectMany(n=>Enumerable.Range(0,3).Select(f=>
            new FormationRow {count=n,formation=f,offsets=Enumerable.Range(0,n).Select(i=>DSGame.FormationOffset(f,i,n)).ToArray()})).ToArray()});
        public static object UnitTests()
        {
            var checks=new List<string>();
            void Check(bool ok,string name) {if(!ok)throw new Exception("FAILED:"+name);checks.Add(name);}
            void Throws(Action action,string name) {try{action();}catch(ArgumentException){checks.Add(name);return;}throw new Exception("FAILED:"+name);}
            var saved=new SnapshotData {scenes=new[]{new SavedScene {path="Assets/DeadSurvivor/Scenes/DeadSurvivor.unity",isLoaded=true,isActive=true}},selection=Array.Empty<string>(),background=false};
            var roundtrip=JsonUtility.FromJson<SnapshotData>(JsonUtility.ToJson(saved));
            Check(roundtrip.scenes[0].path==saved.scenes[0].path && roundtrip.scenes[0].isLoaded && roundtrip.scenes[0].isActive,"scene-snapshot-json-roundtrip");
            var box=Box(-400,-400,400,400);var empty=Array.Empty<Vector2[]>();
            DSMapGeometry Map(Vector2[] outer=null,Vector2[][] obs=null) => new DSMapGeometry(outer??box,obs??empty,.001);
            var map=Map();
            for(int n=1;n<=4;n++) for(int f=0;f<3;f++)
            {
                var slots=Enumerable.Range(0,n).Select(i=>DSGame.FormationOffset(f,i,n)).ToArray();
                var alive=Enumerable.Repeat(true,n).ToArray();var radii=Enumerable.Repeat(8f,n).ToArray();
                var proposal=DSMapMotion.Propose(map,Vector2.zero,slots,slots,alive,radii,8,Vector2.right,.05f);
                Check(proposal.Alpha==1 && proposal.Center==new Vector2(7,0),"open:"+n+":"+f);
                for(int i=0;i<n;i++) Check(Vector2.Distance(proposal.Actual[i],slots[i]+new Vector2(4.2f,0))<.00001f,"follow:"+n+":"+f+":"+i);
            }
            var wedge=Enumerable.Range(0,4).Select(i=>DSGame.FormationOffset(1,i,4)).ToArray();
            var actual=(Vector2[])wedge.Clone();actual[3]=new Vector2(-48,48);
            var top=Box(-200,-200,200,60);var obstacle=Box(-31,39.5f,-29,40.5f);
            var all=Enumerable.Repeat(true,4).ToArray();var rr=Enumerable.Repeat(8f,4).ToArray();
            var p=DSMapMotion.Propose(Map(top,new[]{obstacle}),Vector2.zero,actual,wedge,all,rr,8,Vector2.up,.05f);
            Check(p.Alpha==146/256f && !p.Held,"nonmonotonic-max146");
            p=DSMapMotion.Propose(Map(top,new[]{obstacle}),Vector2.zero,actual,wedge,all,rr,8,Vector2.zero,.05f);
            Check(p.Held && p.Center==Vector2.zero && p.Actual.SequenceEqual(actual),"all257-unsafe-freeze");
            actual=(Vector2[])wedge.Clone();actual[3]=new Vector2(0,44);
            p=DSMapMotion.Propose(Map(Box(-200,-200,200,56.01f)),Vector2.zero,actual,wedge,all,rr,8,Vector2.up,.05f);
            Check(p.Alpha==0 && !p.Held && Math.Abs(p.Actual[3].y-46.4)<.00001,"last-candidate-zero-still-follows");
            all[3]=false;actual[3]=new Vector2(-500,-500);
            p=DSMapMotion.Propose(Map(top),Vector2.zero,actual,wedge,all,rr,8,Vector2.up,.05f);
            Check(p.Alpha==146/256f && p.Actual[3]==actual[3],"dead-reservation-no-corpse-sweep");
            p=DSMapMotion.Propose(Map(obs:new[]{Box(0,-100,.01f,100)}),new Vector2(-12,0),new[]{new Vector2(-12,0)},new[]{Vector2.zero},new[]{true},new[]{8f},8,Vector2.right,.2f);
            Check(p.Alpha==36/256f && p.Center==new Vector2(-8.0625f,0),"thin-wall-exact36");
            p=DSMapMotion.Propose(Map(Box(-1024,-100,1024,100)),new Vector2(1010,0),new[]{new Vector2(1010,0)},new[]{Vector2.zero},new[]{true},new[]{8f},8,Vector2.right,.2f);
            Check(p.Alpha==54/256f && p.Center==new Vector2(1015.90625f,0),"candidate-outside-range-still-searches");
            var u=new[]{new Vector2(-50,-50),new Vector2(50,-50),new Vector2(50,50),new Vector2(20,50),new Vector2(20,-20),new Vector2(-20,-20),new Vector2(-20,50),new Vector2(-50,50)};
            Check(Map(obs:new[]{u}).SafeSweep(Vector2.zero,new Vector2(0,7),8),"concave-notch-not-hull");
            Check(!Map(u).SafeSweep(new Vector2(-35,30),new Vector2(35,30),8),"concave-boundary-whole-segment");
            var margin=new DSMapGeometry(Box(-20,-20,20,20),empty,.125);
            Check(!margin.SafeSweep(new Vector2(11.75f,0),new Vector2(11.75f,0),8),"exact2epsilon-is-unsafe");
            Check(margin.SafeSweep(new Vector2(11.749f,0),new Vector2(11.749f,0),8),"beyond2epsilon-is-safe");
            var raw=Box(-100,-100,100,100);var immutable=Map(raw);raw[0]=new Vector2(999,999);var copy=immutable.Boundary;copy[1]=new Vector2(999,999);
            Check(immutable.SafeSweep(Vector2.zero,Vector2.right,8),"owned-input-and-output-copy");
            Throws(()=>Map(new[]{Vector2.zero,Vector2.one,new Vector2(0,1),Vector2.right}),"self-intersect-rejected");
            Throws(()=>Map(new[]{Vector2.zero,Vector2.right,Vector2.right}),"duplicate-rejected");
            Throws(()=>Map(new[]{Vector2.zero,Vector2.right,2*Vector2.right}),"collinear-rejected");
            Throws(()=>map.SafeSweep(new Vector2(float.NaN,0),Vector2.zero,8),"nan-rejected");
            Throws(()=>map.SafeSweep(Vector2.zero,Vector2.zero,0),"zero-radius-rejected");
            bool invalid=false;try{DSMapMotion.Propose(Map(obs:new[]{Box(-10,-10,10,10)}),Vector2.zero,new[]{Vector2.zero},new[]{Vector2.zero},new[]{true},new[]{8f},8,Vector2.right,.05f);}catch(InvalidOperationException e){invalid=e.Message=="INVALID_INITIAL_OCCUPANCY";}
            Check(invalid,"invalid-initial-not-free-ground");
            invalid=false;try{DSMapMotion.Propose(Map(Box(-200,-200,200,40)),Vector2.zero,new[]{wedge[0],wedge[1],wedge[2],new Vector2(0,30)},wedge,new[]{true,true,true,false},rr,8,Vector2.zero,0);}catch(InvalidOperationException e){invalid=e.Message=="INVALID_INITIAL_OCCUPANCY";}
            Check(invalid,"invalid-dead-reservation-only");
            invalid=false;try{DSMapMotion.Propose(Map(obs:new[]{Box(90,-10,110,10)}),Vector2.zero,new[]{new Vector2(100,0)},new[]{Vector2.zero},new[]{true},new[]{8f},8,Vector2.zero,0);}catch(InvalidOperationException e){invalid=e.Message=="INVALID_INITIAL_OCCUPANCY";}
            Check(invalid,"invalid-live-actual-only");
            var middle=Map(obs:new[]{Box(-1,-1,1,1)});
            Check(!middle.SafeSweep(new Vector2(0,-20),new Vector2(0,22),8) && middle.SafeSweep(new Vector2(-24,-20),new Vector2(-24,22),8) && middle.SafeSweep(new Vector2(24,-20),new Vector2(24,22),8),"center-only-crossing-witness");
            return new {status="PASS",count=checks.Count,checks};
        }
    }
}
