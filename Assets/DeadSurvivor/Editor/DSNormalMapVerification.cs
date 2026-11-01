#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DeadSurvivor.Editor
{
    [Serializable] public class DSBodyObstacle {public Vector2[] points;}
    [Serializable] public class DSBodyFixtureSpec {public Vector2[] boundary;public DSBodyObstacle[] obstacles;public float[] radii;public double epsilon;}
    [Serializable] public class DSBodyBindingRaw {public Vector2[] boundary;public DSBodyObstacle[] obstacles;public float[] radii;public float centerRadius,worldScale,elevation;public bool fixture;public double epsilon;}
    [Serializable] public class DSNormalMapRequest:DSFullFrameRequest {public string fault;public DSBodyFixtureSpec fixture;public bool bindingChecks,useFixture;}
    [Serializable] public class DSNormalMapRaw
    {
        public string run,nonce,id,requestSha,scope="NORMAL_MAP_FRAME_OBSERVER_030",initializationBefore,initializationAfter,initializationEvent,error,recordingError,partialState,inputScopeEvidence;
        public int instanceId,startFrame,endFrame,deviceId;
        public bool complete,hasFailedFrame;
        public string initialQueueNonce;
        public int initialEventOffset,initialEventSequence;
        public long initialGameFrames,initialGameSimulations;
        public DSNormalMapFrame[] steps;
        public DSNormalMapFrame failedFrame;
        public DSMapRandomCall[] rng,lootRng;
        public DSBodyBindingRaw bodyBinding;public string bindingChecks;
    }
    public sealed class DSBodyVisualCleanup:MonoBehaviour
    {public readonly System.Collections.Generic.List<UnityEngine.Object> Items=new();void OnDestroy(){foreach(var item in Items)if(item)UnityEngine.Object.DestroyImmediate(item);}}
    public static class DSNormalMapVerification
    {
        static DSGame game;
        static DSNormalMapObserver binding;
        static Keyboard keyboard,previousKeyboard;
        static Action ownedKeyboardDriver;
        static string output;
        static DSNormalMapRaw raw;
        static string ReportPath(string path)
        {
            string prefix=Path.GetFullPath(Path.Combine(Application.dataPath,"../output/map-full-026/")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string full=Path.GetFullPath(path);
            if(!full.StartsWith(prefix,StringComparison.Ordinal))throw new Exception("INVALID_FRAME_PATH");
            string at=full;
            while(!string.IsNullOrEmpty(at))
            {
                if((File.Exists(at)||Directory.Exists(at)) && (File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)throw new Exception("FRAME_PATH_SYMLINK");
                at=Path.GetDirectoryName(at);
            }
            return full;
        }
        public static string Begin(string requestPath,string resultPath,string expectedSha)
        {
            if(binding!=null && !binding.Finished)throw new Exception("FRAME_CASE_ACTIVE");
            string text=File.ReadAllText(ReportPath(requestPath));
            if(DSMapBlockoutPreview.Hash(text)!=expectedSha)throw new Exception("FRAME_REQUEST_HASH");
            var spec=JsonUtility.FromJson<DSNormalMapRequest>(text);
            if(spec.count<1||spec.count>4||spec.formation<0||spec.formation>2||spec.seed<0||spec.lootSeed<0||spec.inputs==null||spec.inputs.Length<1||!(new[]{"FIXED_REPLAY","DEVICE_SMOKE"}).Contains(spec.mode)||!(new[]{"BATTLE","PREP","DEAD_PREP"}).Contains(spec.initialMode))throw new Exception("FRAME_REQUEST_INVALID");
            if(!string.IsNullOrEmpty(spec.fault)&&!(new[]{"EXTERNAL_QUEUE_DURING_COMMAND","UNOWNED_DOMAIN_AFTER_BIND","UNOWNED_SIMULATE_AFTER_BIND"}).Contains(spec.fault))throw new Exception("NORMAL_FAULT_UNSUPPORTED");
            output=ReportPath(resultPath);if(File.Exists(output)||File.Exists(output+".pending"))throw new Exception("FRAME_OUTPUT_EXISTS");
            var wrapper=DSMapMotionVerification.CreateExternalRoot(spec.run,spec.nonce,"Blockout018_"+spec.nonce);
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DSMapBlockoutVerification.PrefabPath),wrapper.transform);
            var preview=obj.GetComponent<DSMapBlockoutPreview>();
            var session=obj.AddComponent<DSMapSession>();session.Layout=preview.Layout;session.ExpectedDataSha=preview.ExpectedDataSha;session.ViewCamera=preview.ViewCamera;session.ViewCamera.tag="MainCamera";
            session.StaticGeometry=preview.StaticGeometry;session.RockHolder=preview.RockHolder;session.RockPlacementOffset=preview.RockPlacementOffset;session.StartCenter=spec.center;session.CombatEnabled=false;
            session.Visibility=DSMapPlayableVerification.AddVisibility(obj,preview);session.Visibility.Preview=null;
            UnityEngine.Object.DestroyImmediate(preview);
            var go=new GameObject("OwnedNormalGame030");go.transform.SetParent(obj.transform,false);game=go.AddComponent<DSGame>();game.MapSession=session;game.enabled=false;
            DSMapMotionVerification.GuardExternalRoot(spec.run,spec.nonce,wrapper,()=>{
                if(!game||game.MapSession!=session)throw new Exception("FRAME_GAME_OWNERSHIP_LOST");
            },()=>{
                if(game)game.enabled=false;
                if(ownedKeyboardDriver!=null)InputSystem.onBeforeUpdate-=ownedKeyboardDriver;ownedKeyboardDriver=null;
                DSOwnedInputScope.Restore(spec.run,spec.nonce);
                if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);
                if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();
                keyboard=previousKeyboard=null;
            });
            // Root is still inactive; Awake runs only after the complete binding exists.
            if(spec.useFixture)
            {
                if(spec.fixture==null||spec.fixture.boundary==null||spec.fixture.radii==null)throw new Exception("FIXTURE_REQUIRED");
                game.RequireMap=true;
                if(spec.fixture.obstacles==null)throw new Exception("FIXTURE_OBSTACLES_REQUIRED");
                if(spec.bindingChecks)
                {
                    bool rejected=false;try{session.PrepareOwnedFixture(new string('0',32),spec.nonce,game,spec.fixture.boundary,spec.fixture.obstacles.Select(o=>o.points).ToArray(),spec.fixture.radii,spec.fixture.epsilon,spec.center);}catch(InvalidOperationException e){rejected=e.Message=="MAP_FIXTURE_OWNERSHIP";}
                    if(!rejected)throw new Exception("FIXTURE_WRONG_OWNER_ACCEPTED");
                }
                session.PrepareOwnedFixture(spec.run,spec.nonce,game,spec.fixture.boundary,spec.fixture.obstacles.Select(o=>o.points).ToArray(),spec.fixture.radii,spec.fixture.epsilon,spec.center);

            }
            wrapper.SetActive(true);
            if(game.MapCommandError!="")throw new Exception("FRAME_BIND_FAILED:"+game.MapCommandError);
            game.VerificationRun=true;game.Config.speed=1;
            game.Rng=new DSMapTraceRandom(spec.seed);game.LootRng=new DSMapTraceRandom(spec.lootSeed);
            game.Selected=Enumerable.Range(0,spec.count).ToList();
            DSMapMotionVerification.RegisterOwnedUnitGame(spec.run,spec.nonce,game);
            raw=new DSNormalMapRaw{run=spec.run,nonce=spec.nonce,id=spec.id,requestSha=expectedSha,instanceId=game.GetInstanceID(),startFrame=Time.frameCount,initializationBefore=game.CaptureMapFrameState()};
            game.RunOwnedMapUnits(spec.run,spec.nonce,()=>{
            game.StartRun();
            if(game.State!="battle" || game.Team.Count!=spec.count)throw new Exception("FRAME_START_REJECTED");
            if(spec.formation!=0 || spec.initialMode!="BATTLE")
            {
                game.FinishWave(true);
                if(spec.initialMode=="DEAD_PREP")game.InitializeMapFixtureDamage(0);
                if(spec.formation!=0){game.SelectFormation(spec.formation);if(game.PendingFormation!=spec.formation)throw new Exception("FRAME_FORMATION_REJECTED");}
                if(spec.initialMode=="BATTLE")game.NextWave();
            }
            });
            if(spec.bindingChecks)raw.bindingChecks=DSMapBodyVerification.BoundChecks(game,spec.run,spec.nonce);
            if(spec.useFixture)
            {
                var visual=new GameObject("LogicalFixture032");visual.transform.SetParent(obj.transform,false);
                foreach(var renderer in session.StaticGeometry.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                foreach(var renderer in session.RockHolder.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                var cleanup=visual.AddComponent<DSBodyVisualCleanup>();var materials=cleanup.Items;
                void Mesh(Vector2[] points,bool ground)
                {
                    var mesh=DSBlockoutMesh.Prism(points,session.BoundWorldScale,ground?0:12,ground);materials.Add(mesh);
                    var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=ground?new Color(.2f,.28f,.23f):new Color(.48f,.53f,.50f);materials.Add(mat);
                    var shape=new GameObject(ground?"Ground":"LogicalWall");shape.transform.SetParent(visual.transform,false);shape.AddComponent<MeshFilter>().sharedMesh=mesh;shape.AddComponent<MeshRenderer>().sharedMaterial=mat;
                }
                Mesh(session.Geometry.Boundary,true);foreach(var polygon in session.Geometry.Obstacles)Mesh(polygon,false);
            }
            game.World.Render();game.UI.Tick();
            // Damage bursts are explicitly cleared in this combat-free observer fixture.
            raw.initializationEvent="OWNED_INITIALIZATION_START_FORMATION_DAMAGE_AND_CLEAR_EFFECTS";
            game.Effects.Clear();
            raw.initializationAfter=game.CaptureMapFrameState();
            raw.bodyBinding=new DSBodyBindingRaw{boundary=session.Geometry.Boundary,obstacles=session.Geometry.Obstacles.Select(p=>new DSBodyObstacle{points=p}).ToArray(),radii=session.BodyProfile.CopyRadii(spec.count),centerRadius=session.BodyProfile.CenterRadius,worldScale=session.BoundWorldScale,elevation=session.BoundElevation,fixture=session.IsFixture,epsilon=session.Geometry.Epsilon};
            raw.initialQueueNonce=game.MapCommandNonce;raw.initialEventOffset=game.MapCommandEvents.Length;raw.initialEventSequence=game.MapNextEventSequence;
            raw.initialGameFrames=game.NormalObservedGameFrames;raw.initialGameSimulations=game.NormalObservedGameSimulations;
            DSOwnedInputScope.Begin(spec.run,spec.nonce,()=>{if(ownedKeyboardDriver!=null)InputSystem.onBeforeUpdate-=ownedKeyboardDriver;ownedKeyboardDriver=null;if(binding!=null&&!binding.Finished)binding.Fail(game,"EXTERNAL_DEVICE_INPUT");if(game)game.enabled=false;});
            raw.inputScopeEvidence=DSOwnedInputScope.EvidencePath;
            previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>("Map030Keyboard");keyboard.MakeCurrent();DSOwnedInputScope.RegisterDevice(keyboard);raw.deviceId=keyboard.deviceId;
            if(spec.mode=="DEVICE_SMOKE")
            {
                ownedKeyboardDriver=()=>{if(keyboard!=null && keyboard.added && InputState.currentUpdateType==InputUpdateType.Dynamic)InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));};
                InputSystem.onBeforeUpdate+=ownedKeyboardDriver;
            }
            binding=new DSNormalMapObserver(spec.inputs,spec.mode=="DEVICE_SMOKE",raw.initializationAfter,
                ()=>game && game.GetInstanceID()==raw.instanceId && game.MapSession==session && !session.BoundCombatEnabled && session.Geometry!=null && keyboard!=null && keyboard.added && Keyboard.current==keyboard,
                Complete);
            if(spec.fault=="EXTERNAL_QUEUE_DURING_COMMAND")game.RunOwnedMapUnits(spec.run,spec.nonce,()=>game.SetOwnedMapCommandProbe(()=>game.EnqueueMapCommand("foreign-command",game.MapCommandNonce,"Resume",0,"TEST")));
            game.BindNormalMapObserver(spec.run,spec.nonce,binding);
            if(spec.fault=="UNOWNED_DOMAIN_AFTER_BIND")game.RequestPause(true);
            if(spec.fault=="UNOWNED_SIMULATE_AFTER_BIND")
            {try{game.Simulate(.05f);}catch(InvalidOperationException e){if(e.Message!="UNOWNED_MAP_SIMULATE")throw;}}
            if(binding.Finished)return "TERMINAL";
            if(DSOwnedInputScope.Error!=""){binding.Fail(game,"EXTERNAL_DEVICE_INPUT");return "CANCELLED";}game.enabled=true;
            return "STARTED";
        }
        static void Complete()
        {
            if(raw==null||binding==null)return;
            raw.endFrame=Time.frameCount;raw.error=binding.Error;raw.complete=binding.Error=="";
            raw.recordingError=binding.RecordingError;raw.partialState=binding.PartialState;raw.failedFrame=binding.FailedFrame;raw.hasFailedFrame=binding.FailedFrame!=null;
            raw.steps=binding.Frames.ToArray();raw.rng=((DSMapTraceRandom)game.Rng).Calls.ToArray();raw.lootRng=((DSMapTraceRandom)game.LootRng).Calls.ToArray();
            game.enabled=false;
            string temp=output+".pending";
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream)){writer.Write(JsonUtility.ToJson(raw));writer.Flush();stream.Flush(true);}
            File.Move(temp,output);
        }
        public static string TerminalEvidence(string run,string nonce)
        {
            if(raw==null||raw.run!=run||raw.nonce!=nonce||!game||game.enabled||binding==null||!binding.Finished||SessionState.GetString("DS.Map017.owner","")!=run+":"+nonce||game.transform.root.name!="Blockout018_"+nonce)throw new Exception("NORMAL_TERMINAL_OWNER");
            return JsonUtility.ToJson(new TerminalRecord{state=game.CaptureMapFrameState(),gameFrames=game.NormalObservedGameFrames,gameSimulations=game.NormalObservedGameSimulations,events=game.MapCommandEvents});
        }
        [Serializable] class TerminalRecord{public string state;public long gameFrames,gameSimulations;public DSMapCommandEvent[] events;}
        public static string Progress()=>JsonUtility.ToJson(new ProgressRecord{finished=binding?.Finished??false,error=binding?.Error??"NOT_BOUND",recordingError=binding?.RecordingError??"",frames=binding?.Frames.Count??0,gameFrames=game?game.NormalObservedGameFrames:0,gameSimulations=game?game.NormalObservedGameSimulations:0});
        [Serializable] class ProgressRecord{public bool finished;public string error,recordingError;public int frames;public long gameFrames,gameSimulations;}
    }
}
#endif
