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
    [Serializable] public class DSFullFrameRequest
    {
        public string run,nonce,id,mode,initialMode;
        public int count,formation,seed,lootSeed;
        public Vector2 center;
        public DSMapFrameInput[] inputs;
    }
    [Serializable] public class DSFullFrameRaw
    {
        public string run,nonce,id,requestSha,scope="OWNED_FRAME_ADAPTER_ONLY",initializationBefore,initializationAfter,error,failureExpected,failureObserved,inputScopeEvidence;
        public int instanceId,startFrame,endFrame,deviceId;
        public bool complete;
        public DSMapFrameObserved[] steps;
        public DSMapRandomCall[] rng,lootRng;
    }
    [Serializable] public class DSTopologyObject {public string id;public Vector2[] points;}
    [Serializable] public class DSTopologyRuntimeProfile
    {
        public string scope="TOPOLOGY_PRODUCTION_PROFILE_027",sourceDataHash,sourceLayoutHash,catalog,engineVersion;
        public double epsilon,radius,coordinateLimit;public float scale,worldScale;
        public Vector2[] boundary;public DSTopologyObject[] logicalObjects;
    }
    public static class DSMapFullVerification
    {
        static DSGame game;
        static DSMapFrameBinding binding;
        static Keyboard keyboard,previousKeyboard;
        static Action ownedKeyboardDriver;
        static string output;
        static DSFullFrameRaw raw;
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
            var spec=JsonUtility.FromJson<DSFullFrameRequest>(text);
            if(spec.count<1||spec.count>4||spec.formation<0||spec.formation>2||spec.seed<0||spec.lootSeed<0||spec.inputs==null||spec.inputs.Length<1||!(new[]{"FIXED_REPLAY","DEVICE_SMOKE"}).Contains(spec.mode)||!(new[]{"BATTLE","PREP","DEAD_PREP"}).Contains(spec.initialMode))throw new Exception("FRAME_REQUEST_INVALID");
            output=ReportPath(resultPath);if(File.Exists(output)||File.Exists(output+".pending"))throw new Exception("FRAME_OUTPUT_EXISTS");
            var wrapper=DSMapMotionVerification.CreateExternalRoot(spec.run,spec.nonce,"Blockout018_"+spec.nonce);
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DSMapBlockoutVerification.PrefabPath),wrapper.transform);
            var preview=obj.GetComponent<DSMapBlockoutPreview>();
            var session=obj.AddComponent<DSMapSession>();session.Layout=preview.Layout;session.ExpectedDataSha=preview.ExpectedDataSha;session.ViewCamera=preview.ViewCamera;session.ViewCamera.tag="MainCamera";
            session.StaticGeometry=preview.StaticGeometry;session.RockHolder=preview.RockHolder;session.RockPlacementOffset=preview.RockPlacementOffset;session.StartCenter=spec.center;session.CombatEnabled=false;
            session.Visibility=DSMapPlayableVerification.AddVisibility(obj,preview);session.Visibility.Preview=null;
            UnityEngine.Object.DestroyImmediate(preview);
            var go=new GameObject("OwnedFrameGame026");go.transform.SetParent(obj.transform,false);game=go.AddComponent<DSGame>();game.MapSession=session;game.enabled=false;
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
            // Root is still inactive; Awake runs only after the complete serialized binding exists.
            wrapper.SetActive(true);
            game.VerificationRun=true;game.Config.speed=1;
            game.Rng=new DSMapTraceRandom(spec.seed);game.LootRng=new DSMapTraceRandom(spec.lootSeed);
            game.Selected=Enumerable.Range(0,spec.count).ToList();
            DSMapMotionVerification.RegisterOwnedUnitGame(spec.run,spec.nonce,game);
            raw=new DSFullFrameRaw{run=spec.run,nonce=spec.nonce,id=spec.id,requestSha=expectedSha,instanceId=game.GetInstanceID(),startFrame=Time.frameCount,initializationBefore=game.CaptureMapFrameState()};
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
            game.World.Render();game.UI.Tick();
            raw.initializationAfter=game.CaptureMapFrameState();
            DSOwnedInputScope.Begin(spec.run,spec.nonce,()=>{if(ownedKeyboardDriver!=null)InputSystem.onBeforeUpdate-=ownedKeyboardDriver;ownedKeyboardDriver=null;if(binding!=null&&!binding.Finished)binding.Fail("EXTERNAL_DEVICE_INPUT");if(game)game.enabled=false;});
            raw.inputScopeEvidence=DSOwnedInputScope.EvidencePath;
            previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>("Map026Keyboard");keyboard.MakeCurrent();DSOwnedInputScope.RegisterDevice(keyboard);raw.deviceId=keyboard.deviceId;
            if(spec.mode=="DEVICE_SMOKE")
            {
                ownedKeyboardDriver=()=>{if(keyboard!=null && keyboard.added && InputState.currentUpdateType==InputUpdateType.Dynamic)InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));};
                InputSystem.onBeforeUpdate+=ownedKeyboardDriver;
            }
            binding=new DSMapFrameBinding(spec.run,spec.nonce,spec.id,spec.inputs,spec.mode=="DEVICE_SMOKE",32,
                ()=>game && game.GetInstanceID()==raw.instanceId && game.MapSession==session && session.Geometry!=null && keyboard!=null && keyboard.added && Keyboard.current==keyboard,
                Complete);
            game.BindMapFrame(binding);if(DSOwnedInputScope.Error!=""){binding.Fail("EXTERNAL_DEVICE_INPUT");return "CANCELLED";}game.enabled=true;
            return "STARTED";
        }
        static void Complete()
        {
            if(raw==null||binding==null)return;
            raw.endFrame=Time.frameCount;raw.error=binding.Error;raw.complete=binding.Error=="";
            raw.failureExpected=binding.FailureExpected;raw.failureObserved=binding.FailureObserved;
            raw.steps=binding.Observed.ToArray();raw.rng=((DSMapTraceRandom)game.Rng).Calls.ToArray();raw.lootRng=((DSMapTraceRandom)game.LootRng).Calls.ToArray();
            game.enabled=false;
            string temp=output+".pending";
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream)){writer.Write(JsonUtility.ToJson(raw));writer.Flush();stream.Flush(true);}
            File.Move(temp,output);
        }
        public static string QueueTests(string run,string nonce)
        {
            var g=UnityEngine.Object.FindFirstObjectByType<DSGame>();
            if(!g || g.transform.root.name!="Blockout018_"+nonce)throw new Exception("QUEUE_TEST_OWNER");
            g.enabled=false;
            var checks=new System.Collections.Generic.List<string>();
            void Check(bool ok,string id){if(!ok)throw new Exception("QUEUE_TEST:"+id);checks.Add(id);}
            bool Pump()=>g.ConsumeOwnedMapQueueTest(run,nonce);
            string State()=>g.CaptureMapFrameState();
            string before=State();
            g.SubmitMapCommand("Pause");g.SubmitMapCommand("Resume");Check(State()==before,"submit-does-not-mutate-domain");
            Check(!Pump() && !g.Paused,"fifo-pause-resume");
            var events=g.MapCommandEvents;Check(events[events.Length-2].kind=="Pause" && events.Last().kind=="Resume" && events.Last().result=="ACCEPTED","ordered-events");
            before=State();g.SubmitMapCommand("NextWave");Pump();Check(State()==before && g.MapCommandEvents.Last().result=="STATE_REJECTED","battle-next-reject-atomic");
            var token=g.MapCommandNonce;Check(g.EnqueueMapCommand("same",token,"Pause",0,"TEST"),"first-id");Check(!g.EnqueueMapCommand("same",token,"Pause",0,"TEST"),"duplicate-id");Pump();Check(g.Paused,"queued-pause");
            g.SubmitMapCommand("Resume");Pump();before=State();Check(!g.EnqueueMapCommand("stale",new string('0',32),"Pause",0,"TEST") && State()==before,"stale-nonce-no-mutation");
            for(int i=0;i<32;i++)Check(g.EnqueueMapCommand("overflow-"+i,g.MapCommandNonce,i%2==0?"Pause":"Resume",0,"TEST"),"fill-"+i);
            Check(!g.EnqueueMapCommand("overflow-32",g.MapCommandNonce,"Pause",0,"TEST"),"queue-overflow");Pump();Check(!g.Paused,"overflow-keeps-existing-order");
            before=State();g.StartRun();Check(State()==before && g.MapCommandEvents.Last().result=="UNOWNED_DOMAIN_CALL","direct-domain-rejected");
            bool rejected=false;try{g.Simulate(.05f);}catch(InvalidOperationException e){rejected=e.Message=="UNOWNED_MAP_SIMULATE";}Check(rejected && State()==before,"direct-simulation-rejected");
            g.SubmitMapCommand("ReturnLobby");g.SubmitMapCommand("NextWave");Check(Pump() && g.State=="lobby","return-lobby-cut");Check(g.MapCommandEvents.Last().result=="CANCELLED:ReturnLobby","cancel-old-run-tail");
            string old=g.MapCommandNonce;g.SubmitMapCommand("StartRun");g.SubmitMapCommand("StartRun");Check(Pump() && g.State=="battle" && g.Wave==1 && g.MapCommandNonce!=old,"single-start-nonce-rotation");Check(g.MapCommandEvents.Last().result=="CANCELLED:StartRun","cancel-second-start");
            Check(!g.EnqueueMapCommand("old",old,"Pause",0,"TEST"),"old-nonce-after-start");
            bool scopeFailure=false;try{g.RunOwnedMapUnits(run,nonce,()=>{throw new Exception("EXPECTED_UNIT_FAILURE");});}catch(Exception e){scopeFailure=e.Message=="EXPECTED_UNIT_FAILURE";}
            g.SubmitMapCommand("Pause");Pump();Check(scopeFailure && g.Paused,"unit-token-finally-released");g.SubmitMapCommand("Resume");Pump();
            bool duplicateBootstrap=false;try{g.BootstrapOwnedMapQueue(run,nonce);}catch(InvalidOperationException e){duplicateBootstrap=e.Message=="DUPLICATE_MAP_BOOTSTRAP";}Check(duplicateBootstrap,"bootstrap-once");
            var session=g.MapSession;g.MapSession=null;Check(!g.SubmitMapCommand("Pause"),"binding-loss-no-submit-fallback");g.MapSession=session;
            var duplicate=new GameObject("Blockout018_"+nonce);
            try{bool denied=false;try{g.RunOwnedMapUnits(run,nonce,()=>{});}catch(InvalidOperationException e){denied=e.Message=="UNIT_IDENTITY_CONFLICT";}Check(denied,"duplicate-root-unit-scope-rejected");}finally{UnityEngine.Object.DestroyImmediate(duplicate);}
            var second=new GameObject("SecondInactiveGame");second.SetActive(false);second.transform.SetParent(g.transform.root,false);second.AddComponent<DSGame>();
            try{bool denied=false;try{g.RunOwnedMapUnits(run,nonce,()=>{});}catch(InvalidOperationException e){denied=e.Message=="UNIT_IDENTITY_CONFLICT";}Check(denied,"second-game-unit-scope-rejected");}finally{UnityEngine.Object.DestroyImmediate(second);}
            g.enabled=true;
            return JsonUtility.ToJson(new DSMapBlockoutVerification.UnitRecord{status="PASS",count=checks.Count,checks=checks.ToArray()});
        }
        public static string QueueFailureTests(string run,string nonce,bool exhaustion)
        {
            var g=UnityEngine.Object.FindFirstObjectByType<DSGame>();if(!g)throw new Exception("QUEUE_GAME_MISSING");g.enabled=false;
            string before=g.CaptureMapFrameState();bool detected=false;
            if(exhaustion)
            {
                // Each invalid envelope is an explicit unit-test event; no domain mutation.
                for(int i=g.MapCommandEvents.Length;i<4090;i++)g.EnqueueMapCommand("fill-"+i,"wrong","Pause",0,"TEST");
                g.SubmitMapCommand("Pause");
                try{g.ConsumeOwnedMapQueueTest(run,nonce);}catch(InvalidOperationException e){detected=e.Message=="MAP_EVENT_LIMIT";}
                if(!detected||g.CaptureMapFrameState()!=before||g.MapCommandError!="MAP_EVENT_LIMIT")throw new Exception("QUEUE_EVENT_CAPACITY_MUTATED");
            }
            else
            {
                g.RunOwnedMapUnits(run,nonce,()=>g.SetOwnedMapCommandProbe(()=>{throw new Exception("EXPECTED_COMMAND_FAILURE");}));
                g.SubmitMapCommand("Pause");g.SubmitMapCommand("Resume");
                try{g.ConsumeOwnedMapQueueTest(run,nonce);}catch(Exception e){detected=e.Message=="EXPECTED_COMMAND_FAILURE";}
                var last=g.MapCommandEvents.Last();
                if(!detected||!g.Paused||last.result!="EXCEPTION:EXPECTED_COMMAND_FAILURE"||last.before!=before||last.after!=g.CaptureMapFrameState())throw new Exception("QUEUE_EXCEPTION_EVIDENCE_MISSING");
            }
            bool terminal=false;try{g.ConsumeOwnedMapQueueTest(run,nonce);}catch(InvalidOperationException e){terminal=e.Message.StartsWith("MAP_SESSION_TERMINAL:",StringComparison.Ordinal);}
            if(!terminal)throw new Exception("QUEUE_TERMINAL_CONSUMED");
            return exhaustion?"PASS_CAPACITY_BEFORE_MUTATION_AND_TERMINAL":"PASS_PARTIAL_EXCEPTION_EVIDENCE_AND_TERMINAL";
        }
        public static string FreezeTopologyProfile(string destination)
        {
            destination=ReportPath(destination);if(File.Exists(destination))throw new Exception("PROFILE_EXISTS");
            var text=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json");
            var data=JsonUtility.FromJson<DSBlockoutData>(text.text);var geometry=data.Geometry(4);
            var profile=new DSTopologyRuntimeProfile{sourceDataHash=DSMapBlockoutPreview.Hash(text.text),sourceLayoutHash=data.source_layout_sha256,catalog=DSMapMotionVerification.FormationCatalog(),engineVersion=Application.unityVersion,
                epsilon=geometry.Epsilon,radius=data.body_radius,coordinateLimit=geometry.CoordinateLimit,scale=4,worldScale=data.world_scale,boundary=geometry.Boundary,
                logicalObjects=data.objects.Select(o=>new DSTopologyObject{id=o.id,points=o.points_px.Select(p=>data.Logical(p,4)).ToArray()}).ToArray()};
            File.WriteAllText(destination,JsonUtility.ToJson(profile,true));return destination;
        }
        public static string Evidence()
        {
            if(!game)throw new Exception("FRAME_GAME_MISSING");game.World.Render();return game.CaptureMapFrameState();
        }
    }
}
#endif
