using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace DeadSurvivor.Editor
{
    public static class DSMapPlayableVerification
    {
        public const string Folder="Assets/DeadSurvivor/Scenes/Map021";
        public const string Calibration=Folder+"/Map021_Calibration.unity";
        const string Source=DSMapBlockoutVerification.PrefabPath;
        public static string BuildCalibration()
        {
            var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
            if(File.Exists(Calibration))throw new Exception("DESTINATION_EXISTS");
            try
            {
                if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/DeadSurvivor/Scenes","Map021");
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
                PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var p=root.GetComponent<DSMapBlockoutPreview>();p.Overview=false;p.Elevation=55;p.ImageScale=4;
                AddVisibility(root,p);
                if(!EditorSceneManager.SaveScene(scene,Calibration))throw new Exception("SAVE_FAILED");
                return Calibration;
            }
            finally {Restore(saved);}
        }
        static void Restore(DSMapMotionVerification.SnapshotData s)
        {
            EditorSceneManager.RestoreSceneManagerSetup(s.scenes.Select(x=>new SceneSetup{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive}).ToArray());
            Selection.objects=s.selection.Select(x=>{if(!GlobalObjectId.TryParse(x,out var id))throw new Exception("INVALID_SELECTION");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);}).ToArray();
        }
        public static DSMapVisibility AddVisibility(GameObject root,DSMapBlockoutPreview p)
        {
            var v=root.AddComponent<DSMapVisibility>();v.ViewCamera=p.ViewCamera;v.Preview=p;
            v.Blockers=p.StaticGeometry.GetComponentsInChildren<Renderer>().Where(x=>x.name.StartsWith("OB-",StringComparison.Ordinal)).Concat(p.RockHolder.GetComponentsInChildren<Renderer>()).ToArray();
            if(v.Blockers.Length<64)throw new Exception("BLOCKER_SET_INCOMPLETE");return v;
        }
        public static string Calibrate(string run,string nonce,int scale,int angle)
        {
            var wrapper=DSMapMotionVerification.CreateExternalRoot(run,nonce,"Blockout018_"+nonce);
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source),wrapper.transform);obj.SetActive(false);
            var p=obj.GetComponent<DSMapBlockoutPreview>();p.ImageScale=scale;p.Elevation=angle;p.Overview=false;
            var data=JsonUtility.FromJson<DSBlockoutData>(p.Layout.text);
            p.InitialCenter=new Vector2((data.rock_center_px.x-data.origin_px.x)*scale,(data.objects[0].points_px.Min(x=>x.y)-data.origin_px.y)*scale-70);
            var visibility=AddVisibility(obj,p);wrapper.SetActive(true);obj.SetActive(true);p.Initialize();p.Freeze();p.enabled=false;visibility.Refresh();
            if(p.Error!="")throw new Exception(p.Error);
            DSMapMotionVerification.GuardExternalRoot(run,nonce,wrapper,()=>p.AssertOwnedTree(),()=>{});
            return InspectCalibration();
        }
        public const string PartyScene=Folder+"/Map022_PartyExperimental.unity";
        static DSGame game;
        static UnityEngine.InputSystem.Keyboard testKeyboard,priorKeyboard;
        static DSMapSession MakeGame(GameObject root,DSMapBlockoutPreview p,bool combat)
        {
            var session=root.AddComponent<DSMapSession>();session.Layout=p.Layout;session.ExpectedDataSha=p.ExpectedDataSha;
            session.ViewCamera=p.ViewCamera;session.ViewCamera.tag="MainCamera";session.StaticGeometry=p.StaticGeometry;session.RockHolder=p.RockHolder;session.RockPlacementOffset=p.RockPlacementOffset;
            session.Visibility=root.GetComponent<DSMapVisibility>()??AddVisibility(root,p);session.Visibility.Preview=null;session.CombatEnabled=combat;
            UnityEngine.Object.DestroyImmediate(p);
            var obj=new GameObject("ExperimentalGame");obj.transform.SetParent(root.transform,false);game=obj.AddComponent<DSGame>();game.MapSession=session;
            return session;
        }
        public static string BuildPartyScene()
        {
            var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
            if(File.Exists(PartyScene))throw new Exception("DESTINATION_EXISTS");
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
                PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                root.name="Map022_EXPERIMENTAL";MakeGame(root,root.GetComponent<DSMapBlockoutPreview>(),false);
                if(!EditorSceneManager.SaveScene(scene,PartyScene))throw new Exception("SAVE_FAILED");return PartyScene;
            }
            finally {Restore(saved);}
        }
        public static string BeginGame(string run,string nonce,int stage)
        {
            var wrapper=DSMapMotionVerification.CreateExternalRoot(run,nonce,"Blockout018_"+nonce);
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source),wrapper.transform);
            MakeGame(obj,obj.GetComponent<DSMapBlockoutPreview>(),stage==23);
            wrapper.SetActive(true);
            priorKeyboard=UnityEngine.InputSystem.Keyboard.current;
            testKeyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("MapPlayKeyboard");testKeyboard.MakeCurrent();
            DSMapMotionVerification.GuardExternalRoot(run,nonce,wrapper,()=>{if(!game || game.MapSession.transform.parent!=obj.transform && game.MapSession.gameObject!=obj)throw new Exception("GAME_BINDING_CHANGED");},()=>{
                if(testKeyboard!=null && testKeyboard.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(testKeyboard);
                if(priorKeyboard!=null && priorKeyboard.added)priorKeyboard.MakeCurrent();testKeyboard=null;priorKeyboard=null;
            });
            game.VerificationRun=true;game.Selected=Enumerable.Range(0,4).ToList();game.UI.Lobby();Click("스쿼드 출전 →");
            return GameEvidence();
        }
        static void Click(string label)
        {
            var b=game.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(x=>x.interactable && x.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t=>t.text==label));
            if(!b)throw new Exception("BUTTON_MISSING:"+label);b.onClick.Invoke();
        }
        public static string FreezeGame() {game.enabled=false;game.World.Render();return "FROZEN";}
        public static string GameEvidence()
        {
            game.World.Render();var m=game.MapSession;var cam=m.ViewCamera;
            return JsonUtility.ToJson(new GameRecord {frame=Time.frameCount,state=game.State,elapsed=game.Elapsed,wave=game.Wave,enemies=game.Enemies.Count,center=game.Center,party=game.Team.Select(c=>c.pos).ToArray(),
                camera=new DSBlockoutCameraRecord {elevation=m.Elevation,orthographicSize=cam.orthographicSize,width=cam.pixelWidth,height=cam.pixelHeight,position=cam.transform.position,rotation=cam.transform.eulerAngles,
                feet=game.Team.Select(c=>cam.WorldToScreenPoint(m.World(c.pos))).ToArray(),heads=game.Team.Select(c=>cam.WorldToScreenPoint(m.World(c.pos)+cam.transform.up*.72f)).ToArray()}});
        }
        [Serializable] public class GameRecord {public int frame,wave,enemies;public string state;public float elapsed;public Vector2 center;public Vector2[] party;public DSBlockoutCameraRecord camera;}
        sealed class ProbeRandom:System.Random
        {public int Calls;public ProbeRandom(int seed):base(seed){}public override double NextDouble(){Calls++;return base.NextDouble();}public override int Next(int max){Calls++;return base.Next(max);}public override int Next(int min,int max){Calls++;return base.Next(min,max);}}
        [Serializable] class StateRecord {public Survivor[] team;public Gear[] stash;public WaveRecord[] history;}
        static string StateFingerprint()
        {
            string timers=string.Join(";",game.Team.Select(c=>string.Join(",",c.healClock,c.shieldClock,c.flash,c.shareClock,c.sharePeriod,c.shareProvider,c.aliveTime,c.damageTaken,c.healingReceived)+":"+string.Join("/",c.items.Select(i=>string.Join(",",i.cd,i.lastPeriod,i.activationFlash)))))+"|"+string.Join(";",game.Stash.Select(i=>string.Join(",",i.cd,i.lastPeriod,i.activationFlash)));
            string publicState=string.Join(";",typeof(DSGame).GetFields().Where(f=>f.FieldType==typeof(int)||f.FieldType==typeof(float)||f.FieldType==typeof(bool)||f.FieldType==typeof(Vector2)).OrderBy(f=>f.Name).Select(f=>f.Name+":"+f.GetValue(game)));
            string privateState=string.Join(";",typeof(DSGame).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Where(f=>f.FieldType==typeof(int)||f.FieldType==typeof(float)).OrderBy(f=>f.Name).Select(f=>f.Name+":"+f.GetValue(game)));
            return string.Join("|",game.State,game.Wave,game.Formation,game.PendingFormation,game.Elapsed,game.TotalTime,game.Center,game.Kills,game.Spawned,
                JsonUtility.ToJson(new StateRecord{team=game.Team.ToArray(),stash=game.Stash.ToArray(),history=game.History.ToArray()}),string.Join(";",game.Team.Select(c=>c.pos.ToString("R"))),privateState,timers,publicState,((ProbeRandom)game.Rng).Calls,((ProbeRandom)game.LootRng).Calls);
        }
        public static string PartyTests()
        {
            game.enabled=false;game.Rng=new ProbeRandom(17);game.LootRng=new ProbeRandom(29);var checks=new System.Collections.Generic.List<string>();
            void Check(bool good,string name){if(!good)throw new Exception("PARTY_TEST:"+name);checks.Add(name);}
            var outer=new[]{new Vector2(-300,-300),new Vector2(300,-300),new Vector2(300,300),new Vector2(-300,300)};
            var open=new DSMapGeometry(outer,Array.Empty<Vector2[]>(),.001);
            for(int n=1;n<=4;n++)for(int f=0;f<3;f++)
            {
                Check(DSMapPartyRules.CanStart(open,Vector2.zero,n),"start-"+n+"-"+f);
                var team=Enumerable.Range(0,n).Select(i=>new Survivor{hp=i==0?0:100,pos=DSGame.FormationOffset(0,i,n)}).ToArray();
                Check(DSMapPartyRules.CanTransition(open,Vector2.zero,0,f,team,0),"formation-dead-revive-"+n+"-"+f);
            }
            var wall=new[]{new Vector2(36,-80),new Vector2(44,-80),new Vector2(44,80),new Vector2(36,80)};
            var blocked=new DSMapGeometry(outer,new[]{wall},.001);
            var bodies=Enumerable.Range(0,4).Select(i=>new Survivor{hp=100,pos=DSGame.FormationOffset(0,i,4)}).ToArray();
            Check(DSMapPartyRules.CanStart(blocked,Vector2.zero,4),"center-and-F0-safe");
            Check(!DSMapPartyRules.CanTransition(blocked,Vector2.zero,0,2,bodies),"reserved-crosses-wall");
            bodies[3].hp=0;Check(!DSMapPartyRules.CanTransition(blocked,Vector2.zero,0,2,bodies),"dead-reserved-crosses-wall");
            bodies[0].pos=new Vector2(72,0);Check(!DSMapPartyRules.CanTransition(blocked,Vector2.zero,0,0,bodies),"lagging-actual-crosses-wall");
            bodies[0].hp=0;bodies[0].pos=new Vector2(40,0);Check(DSMapPartyRules.CanTransition(blocked,Vector2.zero,0,0,bodies,0),"revive-from-reserved-not-corpse");
            game.Selected=Enumerable.Range(0,4).ToList();game.StartRun();
            for(int i=0;i<40;i++){game.MoveInput=Vector2.right;var before=game.Team.Select(c=>c.pos).ToArray();game.Simulate(.05f);Check(game.Team.Select((c,j)=>game.MapSession.Geometry.SafeSweep(before[j],c.pos,8)).All(x=>x),"actual-move-"+i);}
            game.MoveInput=Vector2.zero;game.StartRun();game.FinishWave(true);Check(game.State=="prep" && game.Stash.Count>0,"real-prep");
            game.Team[0].hp=0;game.UI.Prep();Click("부활하기");Check(game.Team[0].hp==game.Team[0].maxhp,"ui-revive");
            Click(DSGame.FormationNames[1]);Check(game.PendingFormation==1,"ui-formation");Click("다음 웨이브 →");Check(game.Wave==2 && game.State=="battle","ui-next-wave");
            game.FinishWave(true);
            var prepFingerprint=StateFingerprint();game.Simulate(.05f);Check(prepFingerprint==StateFingerprint(),"prep-timers-frozen");
            var center=game.Center;game.Formation=game.PendingFormation=0;
            Vector2? blockedCenter=null;
            foreach(var o in game.MapSession.Data.objects)
            {
                float minx=o.points_px.Min(p=>game.MapSession.Data.Logical(p,4).x),maxx=o.points_px.Max(p=>game.MapSession.Data.Logical(p,4).x);
                float miny=o.points_px.Min(p=>game.MapSession.Data.Logical(p,4).y),maxy=o.points_px.Max(p=>game.MapSession.Data.Logical(p,4).y);
                foreach(float x in new[]{minx-50,maxx+50})foreach(float y in new[]{miny,(miny+maxy)/2,maxy})
                {
                    var c=new Vector2(x,y);if(!DSMapPartyRules.CanStart(game.MapSession.Geometry,c,4))continue;
                    var probe=Enumerable.Range(0,4).Select(i=>new Survivor{hp=100,pos=c+DSGame.FormationOffset(0,i,4)}).ToArray();
                    if(!DSMapPartyRules.CanTransition(game.MapSession.Geometry,c,0,2,probe)){blockedCenter=c;break;}
                }
                if(blockedCenter.HasValue)break;
            }
            Check(blockedCenter.HasValue,"actual-map-center-safe-blocked-formation-found");game.Center=blockedCenter.Value;
            for(int i=0;i<4;i++){var c=game.Team[i];c.pos=game.Center+game.Offset(i);c.healClock=.7f;c.shieldClock=1.3f;c.shareClock=.9f;c.flash=.11f;foreach(var item in c.items){item.cd=.83f;item.lastPeriod=1.9f;item.activationFlash=.17f;}}

            var beforeState=StateFingerprint();game.SelectFormation(2);Check(beforeState==StateFingerprint(),"reject-formation-atomic");
            game.PendingFormation=2;beforeState=StateFingerprint();game.NextWave();Check(beforeState==StateFingerprint(),"reject-next-atomic");
            game.Team[0].hp=0;beforeState=StateFingerprint();game.Revive(0);Check(beforeState==StateFingerprint(),"reject-revive-atomic");
            var initial=game.MapSession.StartCenter;game.MapSession.StartCenter=game.MapSession.Data.Logical(game.MapSession.Data.rock_center_px,4);beforeState=StateFingerprint();game.StartRun();Check(beforeState==StateFingerprint(),"reject-start-atomic");game.MapSession.StartCenter=initial;
            game.Center=center;game.StartRun();game.World.Render();game.enabled=true;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(testKeyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.D));
            return JsonUtility.ToJson(new DSMapBlockoutVerification.UnitRecord {status="PASS",count=checks.Count,checks=checks.ToArray()});
        }
        public static string VisibilityTests(string run,string nonce)
        {
            var root=DSMapMotionVerification.CreateExternalRoot(run,nonce,"Blockout018_"+nonce);root.SetActive(true);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(root.transform,false);
            var renderer=cube.GetComponent<Renderer>();var original=new Material(Shader.Find("Universal Render Pipeline/Lit"));renderer.sharedMaterial=original;
            var cameraObject=new GameObject("TestCamera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);
            var cam=cameraObject.GetComponent<Camera>();cam.transform.position=new Vector3(0,0,-10);
            var v=root.AddComponent<DSMapVisibility>();v.ViewCamera=cam;v.Blockers=new[]{renderer};v.Focus=new[]{new Vector3(0,0,5)};
            v.Refresh();if(v.FadedCount!=1 || renderer.sharedMaterial==original || Mathf.Abs(renderer.sharedMaterial.color.a-.22f)>.001f)throw new Exception("FADE_TRIGGER");
            v.Focus=Array.Empty<Vector3>();v.Refresh();if(v.FadedCount!=0 || renderer.sharedMaterial!=original || original.GetFloat("_ZWrite")!=1)throw new Exception("FADE_CLEAR");
            v.Focus=new[]{new Vector3(0,0,5)};v.Refresh();UnityEngine.Object.DestroyImmediate(v);
            if(renderer.sharedMaterial!=original || original.color.a!=1)throw new Exception("FADE_DISPOSE");
            DSMapMotionVerification.GuardExternalRoot(run,nonce,root,()=>{},()=>UnityEngine.Object.DestroyImmediate(original));
            return "PASS_TRIGGER_CLEAR_DISPOSE";
        }
        public static string InspectCalibration()
        {
            var p=UnityEngine.Object.FindFirstObjectByType<DSMapBlockoutPreview>();
            var v=p.GetComponent<DSMapVisibility>();v.Refresh();
            return JsonUtility.ToJson(new CalibrationRecord {frame=Time.frameCount,scale=p.ImageScale,camera=p.CameraEvidence(),faded=v.FadedCount,blockers=v.Blockers.Length,rock=v.Blockers.Last().bounds.size});
        }
        [Serializable] public class CalibrationRecord {public int frame,faded,blockers;public float scale;public Vector3 rock;public DSBlockoutCameraRecord camera;}
    }
}
