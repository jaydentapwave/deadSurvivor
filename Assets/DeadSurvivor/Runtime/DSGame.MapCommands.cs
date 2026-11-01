using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace DeadSurvivor
{
    [Serializable] public class DSMapQueuedCommand
    {public string id,nonce,kind,source;public int value,sequence,enqueueFrame;}
    [Serializable] public class DSMapCommandEvent
    {public string id,nonce,kind,source,result,before,after;public int value,enqueueFrame,consumeFrame,sequence;}
    [Serializable] class DSMapCommandState
    {public string state;public bool paused;public int wave,formation,pending;public float elapsed,total;public Vector2 center;public int[] selected;public Survivor[] team;public Gear[] stash;public WaveRecord[] history;}
    public partial class DSGame
    {
        const int MapQueueLimit=32,MapEventLimit=4096;
        readonly Queue<DSMapQueuedCommand> mapQueue=new();
        readonly HashSet<string> mapQueueIds=new();
        readonly List<DSMapCommandEvent> mapQueueEvents=new();
        bool mapRequired,mapCommandPermit,mapCommandConsuming,mapQueueBootstrapUsed;
        string mapQueueNonce="",mapQueueError="";
        int mapQueueSequence,mapQueueConsumed;
        public int MapInputFrame {get;private set;}
        public int MapInputDevice {get;private set;}
        public bool MapInputD {get;private set;}
        public Vector2 MapInputObserved {get;private set;}
        public string MapCommandNonce=>mapQueueNonce;
        public string MapCommandError=>mapQueueError;
        public DSMapCommandEvent[] MapCommandEvents=>mapQueueEvents.ToArray();
        public int MapNextEventSequence=>mapQueueConsumed;
        void InitializeMapCommands(){mapRequired=true;RotateMapNonce("INITIALIZED");}
        string CommandState()
        {
#if UNITY_EDITOR
            return CaptureMapFrameState();
#else
            return JsonUtility.ToJson(new DSMapCommandState{state=State,paused=Paused,wave=Wave,formation=Formation,pending=PendingFormation,elapsed=Elapsed,total=TotalTime,center=Center,selected=Selected.ToArray(),team=Team.ToArray(),stash=Stash.ToArray(),history=History.ToArray()});
#endif
        }
        void RequireMapEventCapacity(int count)
        {
            if(count<0||mapQueueEvents.Count+count>MapEventLimit){mapQueueError="MAP_EVENT_LIMIT";throw new InvalidOperationException("MAP_EVENT_LIMIT");}
        }
        void CommandEvent(DSMapQueuedCommand c,string result,string before,string after)
        {
            if(mapQueueEvents.Count>=MapEventLimit){mapQueueError="MAP_EVENT_LIMIT";throw new InvalidOperationException("MAP_EVENT_LIMIT");}
            mapQueueEvents.Add(new DSMapCommandEvent{id=c.id,nonce=c.nonce,kind=c.kind,source=c.source,value=c.value,enqueueFrame=c.enqueueFrame,consumeFrame=Time.frameCount,sequence=mapQueueConsumed++,result=result,before=before,after=after});
        }
        void RotateMapNonce(string reason)
        {
            while(mapQueue.Count>0){var c=mapQueue.Dequeue();CommandEvent(c,"CANCELLED:"+reason,"","");}
            mapQueueIds.Clear();mapQueueNonce=Guid.NewGuid().ToString("N");
        }
        public bool SubmitMapCommand(string kind,int value=0,string source="UI")
        {
            if(!mapRequired && !MapSession){InvokeMapCommand(kind,value);return true;}
            return EnqueueMapCommand(Guid.NewGuid().ToString("N"),mapQueueNonce,kind,value,source);
        }
        public bool EnqueueMapCommand(string id,string nonce,string kind,int value,string source)
        {
#if UNITY_EDITOR
            if(mapFrame!=null){mapFrame.Fail("EXTERNAL_QUEUED_COMMAND:"+kind);return false;}
            if(mapObserver!=null&&!mapObserver.AllowsSubmission(id,source))
            {
                // The active frame catch seals failure only after command exception
                // events have been recorded. Disabling alone cannot abort Update.
                if(!mapObserver.InFrame){mapObserver.Fail(this,"EXTERNAL_QUEUED_COMMAND:"+kind);enabled=false;}
                throw new InvalidOperationException("EXTERNAL_QUEUED_COMMAND:"+kind);
            }
#endif
            if(!mapRequired || !MapSession || mapQueueError!="")return false;
            RequireMapEventCapacity(1);
            var c=new DSMapQueuedCommand{id=id,nonce=nonce,kind=kind,value=value,source=source,sequence=mapQueueSequence++,enqueueFrame=Time.frameCount};
            if(string.IsNullOrEmpty(id)||id.Length>128||nonce!=mapQueueNonce||source!="UI"&&source!="KEYBOARD"&&source!="DEVICE_SMOKE"&&source!="TEST")
            {CommandEvent(c,"INVALID_ENVELOPE","","");return false;}
            if(!mapQueueIds.Add(id)){CommandEvent(c,"DUPLICATE_ID","","");return false;}
            if(mapQueue.Count>=MapQueueLimit){CommandEvent(c,"QUEUE_OVERFLOW","","");return false;}
            mapQueue.Enqueue(c);return true;
        }
        string ValidateMapCommand(DSMapQueuedCommand c)
        {
            if(!MapSession||MapSession.Geometry==null)return "MAP_BINDING_MISSING";
            switch(c.kind)
            {
                case "ToggleSelection":return State!="lobby"?"STATE_REJECTED":c.value<0||c.value>=Data.characters.Length?"PAYLOAD_REJECTED":!Selected.Contains(c.value)&&Selected.Count>=4?"SELECTION_LIMIT":"";
                case "StartRun":return State!="lobby"?"STATE_REJECTED":Selected.Count<1||Selected.Count>4||Selected.Distinct().Count()!=Selected.Count||Selected.Any(i=>i<0||i>=Data.characters.Length)?"SELECTION_REJECTED":"";
                case "SelectFormation":return State!="prep"?"STATE_REJECTED":c.value<0||c.value>2?"PAYLOAD_REJECTED":"";
                case "CancelFormation":return State!="prep"?"STATE_REJECTED":"";
                case "Revive":return State!="prep"?"STATE_REJECTED":c.value<0||c.value>=Team.Count?"PAYLOAD_REJECTED":Team[c.value].hp>0?"NOT_DEAD":"";
                case "NextWave":return State!="prep"?"STATE_REJECTED":Team.Count<1||Wave<1||Wave>10?"WAVE_REJECTED":"";
                case "Pause":case "Resume":return State!="battle"?"STATE_REJECTED":"";
                case "DebugFinish":return State!="battle"||Paused?"STATE_REJECTED":"";
                case "ReturnLobby":return State=="lobby"?"STATE_REJECTED":"";
                default:return "UNKNOWN_COMMAND";
            }
        }
        void InvokeMapCommand(string kind,int value)
        {
            switch(kind)
            {
                case "ToggleSelection":ToggleSelection(value);break;
                case "StartRun":StartRun();break;
                case "SelectFormation":SelectFormation(value);break;
                case "CancelFormation":SelectFormation(Formation);break;
                case "Revive":Revive(value);break;
                case "NextWave":NextWave();break;
                case "Pause":RequestPause(true);break;
                case "Resume":RequestPause(false);break;
                case "DebugFinish":FinishWave(true);break;
                case "ReturnLobby":ReturnLobby();break;
                default:throw new ArgumentException("UNKNOWN_COMMAND");
            }
        }
        bool ConsumeMapCommands()
        {
            if(mapQueueError!="")throw new InvalidOperationException("MAP_SESSION_TERMINAL:"+mapQueueError);
            if(mapCommandConsuming||mapCommandPermit)throw new InvalidOperationException("REENTRANT_MAP_CONSUME");
            RequireMapEventCapacity(mapQueue.Count==0?0:mapQueue.Count+MapQueueLimit+1);
            bool skipSimulation=false;int cut=mapQueue.Count;
            mapCommandConsuming=true;mapCommandPermit=true;
            try
            {
                for(int i=0;i<cut && mapQueue.Count>0;i++)
                {
                    var c=mapQueue.Dequeue();string before="";
                    try
                    {
                        before=CommandState();
                        if(c.nonce!=mapQueueNonce){CommandEvent(c,"CANCELLED:NONCE_CHANGED",before,before);continue;}
                        string reason=ValidateMapCommand(c);
                        if(reason!="")
                        {
                            CommandEvent(c,reason,before,before);
#if UNITY_EDITOR
                            if(mapObserver!=null&&!mapObserver.ContinueOnRejection)
                            {for(int j=i+1;j<cut&&mapQueue.Count>0;j++)CommandEvent(mapQueue.Dequeue(),"CANCELLED:REJECTION",before,before);skipSimulation=true;break;}
#endif
                            continue;
                        }
                        MapRejection="";InvokeMapCommand(c.kind,c.value);
#if UNITY_EDITOR
                        mapCommandTestProbe?.Invoke();
#endif
                        string result=MapRejection!=""?MapRejection:"ACCEPTED";
                        CommandEvent(c,result,before,CommandState());
#if UNITY_EDITOR
                        if(result!="ACCEPTED"&&mapObserver!=null&&!mapObserver.ContinueOnRejection)
                        {string rejectedState=CommandState();for(int j=i+1;j<cut&&mapQueue.Count>0;j++)CommandEvent(mapQueue.Dequeue(),"CANCELLED:REJECTION",rejectedState,rejectedState);skipSimulation=true;break;}
#endif
                        if(result=="ACCEPTED" && (c.kind=="StartRun"||c.kind=="ReturnLobby"))
                        {skipSimulation=true;RotateMapNonce(c.kind);break;}
                    }
                    catch(Exception e)
                    {
                        string partial;
                        try{partial=CommandState();}catch(Exception capture){partial="CAPTURE_FAILED:"+capture.Message;}
                        CommandEvent(c,"EXCEPTION:"+e.Message,before,partial);
                        mapQueueError=e.Message;throw;
                    }
                }
            }
            catch(Exception e){mapQueueError=e.Message;throw;}
            finally{mapCommandPermit=false;mapCommandConsuming=false;}
            return skipSimulation;
        }
        void AdvanceNormalMapFrame()
        {
            try
            {
                if(!MapSession||MapSession.Geometry==null)throw new InvalidOperationException("MAP_BINDING_LOST");
                if(mapQueueError!="")throw new InvalidOperationException("MAP_SESSION_TERMINAL:"+mapQueueError);
#if UNITY_EDITOR
                normalGameFrames++;
                mapObserver?.Begin(this,normalGameFrames,normalGameSimulations);
#endif
                RecordTime(Time.unscaledDeltaTime);
#if UNITY_EDITOR
                mapObserver?.AfterTime(this);
#endif
                var k=UnityEngine.InputSystem.Keyboard.current;
                if(k!=null)
                {
                    if(k.spaceKey.wasPressedThisFrame||k.escapeKey.wasPressedThisFrame)SubmitMapCommand(Paused?"Resume":"Pause",0,"KEYBOARD");
                    if(k.digit1Key.wasPressedThisFrame)SubmitMapCommand("DebugFinish",0,"KEYBOARD");
                    if(k.rKey.wasPressedThisFrame)UI.RotateHeld();
                }
                bool skip=ConsumeMapCommands();
                if(!skip && State=="battle"&&!Paused)
                {
                    MapInputFrame=Time.frameCount;MapInputDevice=UnityEngine.InputSystem.Keyboard.current?.deviceId??0;MapInputD=UnityEngine.InputSystem.Keyboard.current?.dKey.isPressed??false;
                    MoveInput=ReadMovementInput();float suppliedDt=Time.unscaledDeltaTime;
#if UNITY_EDITOR
                    if(mapObserver!=null){MoveInput=mapObserver.Movement(MoveInput);suppliedDt=mapObserver.Delta;}
#endif
                    MoveInput=Vector2.ClampMagnitude(MoveInput,1);MapInputObserved=MoveInput;mapCommandPermit=true;
                    float requestedDt=Mathf.Min(suppliedDt,.05f)*Config.speed;
                    try
                    {
#if UNITY_EDITOR
                        mapObserver?.BeforeSimulation(this,requestedDt,MoveInput);
#endif
                        Simulate(requestedDt);
#if UNITY_EDITOR
                        mapObserver?.AfterSimulation(this);
#endif
                    }
                    finally{mapCommandPermit=false;}
                }
                World.Render();UI.Tick();
#if UNITY_EDITOR
                mapObserver?.End(this,normalGameSimulations);
                if(mapObserver!=null&&mapObserver.Finished)enabled=false;
#endif
            }
            catch(Exception e)
            {
                mapQueueError=e.Message;enabled=false;
#if UNITY_EDITOR
                mapObserver?.Fail(this,e.ToString());
#endif
                Debug.LogError("Map command session stopped: "+e.Message);
            }
        }
        bool RejectUnownedMapCommand(string command)
        {
            if(!mapRequired||mapCommandPermit)return false;
#if UNITY_EDITOR
            if(mapObserver!=null)
            {
                if(mapObserver.InFrame)throw new InvalidOperationException("UNOWNED_DOMAIN_CALL:"+command);
                mapObserver.Fail(this,"UNOWNED_DOMAIN_CALL:"+command);enabled=false;
            }
#endif
            CommandEvent(new DSMapQueuedCommand{id="direct-"+mapQueueSequence++,nonce=mapQueueNonce,kind=command,source="DIRECT",enqueueFrame=Time.frameCount},"UNOWNED_DOMAIN_CALL","","");return true;
        }
        void RequireMapSimulationPermit()
        {
            if(mapRequired&&!mapCommandPermit)
            {
#if UNITY_EDITOR
                if(mapObserver!=null){mapObserver.Fail(this,"UNOWNED_MAP_SIMULATE");enabled=false;}
#endif
                throw new InvalidOperationException("UNOWNED_MAP_SIMULATE");
            }
        }
#if UNITY_EDITOR
        bool mapUnitScope;
        GameObject mapUnitRegisteredRoot;
        int mapUnitRegisteredInstance;
        string mapUnitRegisteredOwner="";
        Action mapCommandTestProbe;
        public void RegisterOwnedMapUnits(string run,string nonce,GameObject root)
        {
            if(mapUnitRegisteredRoot || !root || root!=transform.root.gameObject || root.name!="Blockout018_"+nonce || root.GetComponentsInChildren<DSGame>(true).Length!=1 || enabled || !VerificationRun || !mapRequired || SessionState.GetString("DS.Map017.owner","")!=run+":"+nonce)throw new InvalidOperationException("INVALID_UNIT_REGISTRATION");
            var snapshotContext=new DSFullSnapshotContext(this);
            mapUnitRegisteredRoot=root;mapUnitRegisteredInstance=GetInstanceID();mapUnitRegisteredOwner=run+":"+nonce;
            fullSnapshotContext=snapshotContext;
        }
        public void SetOwnedMapCommandProbe(Action probe)
        {if(!mapUnitScope||!mapCommandPermit)throw new InvalidOperationException("NOT_MAP_UNIT_SCOPE");mapCommandTestProbe=probe;}
        public bool OwnedMapUnitActive=>mapUnitScope;
        public void DrainOwnedUnitQueue()
        {
            if(!mapUnitScope||!mapCommandPermit||mapCommandConsuming)throw new InvalidOperationException("NOT_MAP_UNIT_SCOPE");
            mapCommandPermit=false;
            try{ConsumeMapCommands();}finally{mapCommandPermit=true;}
        }
        void OwnedUnitPermission(string run,string nonce)
        {
            if(enabled||!VerificationRun||!mapRequired||mapObserver!=null||mapFrame!=null||mapUnitScope||mapCommandPermit||mapCommandConsuming||SessionState.GetString("DS.Map017.owner","")!=run+":"+nonce||SessionState.GetString("DS.Map017.phase","")!="PLAY"||SessionState.GetInt("DS.Map017.pid",-1)!=System.Diagnostics.Process.GetCurrentProcess().Id||transform.root.name!="Blockout018_"+nonce)
                throw new InvalidOperationException("INVALID_MAP_UNIT_SCOPE");
            if(!mapUnitRegisteredRoot || mapUnitRegisteredRoot!=transform.root.gameObject || mapUnitRegisteredInstance!=GetInstanceID() || mapUnitRegisteredOwner!=run+":"+nonce || mapUnitRegisteredRoot.GetComponentsInChildren<DSGame>(true).Length!=1 || gameObject.scene.GetRootGameObjects().Count(r=>r.name=="Blockout018_"+nonce)!=1)throw new InvalidOperationException("UNIT_IDENTITY_CONFLICT");
        }
        public void RunOwnedMapUnits(string run,string nonce,Action action)
        {
            OwnedUnitPermission(run,nonce);if(action==null)throw new ArgumentNullException(nameof(action));
            mapCommandPermit=mapUnitScope=true;
            try{action();}finally{mapCommandPermit=mapUnitScope=false;}
        }
        public bool ConsumeOwnedMapQueueTest(string run,string nonce)
        {
            OwnedUnitPermission(run,nonce);return ConsumeMapCommands();
        }
        public void BootstrapOwnedMapQueue(string run,string nonce)
        {
            OwnedUnitPermission(run,nonce);if(mapQueueBootstrapUsed)throw new InvalidOperationException("DUPLICATE_MAP_BOOTSTRAP");
            mapQueueBootstrapUsed=true;ConsumeMapCommands();
        }
#endif
    }
}
