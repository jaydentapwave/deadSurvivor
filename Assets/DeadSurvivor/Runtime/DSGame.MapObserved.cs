#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        DSNormalMapObserver mapObserver;
        long normalGameFrames,normalGameSimulations;
        public long NormalObservedGameFrames=>normalGameFrames;
        public long NormalObservedGameSimulations=>normalGameSimulations;
        bool NormalObserverRecordingTime=>mapObserver!=null&&mapObserver.InFrame;
        public void BindNormalMapObserver(string run,string nonce,DSNormalMapObserver observer)
        {
            if(observer==null||mapObserver!=null||mapFrame!=null||movementFixture!=null)throw new InvalidOperationException("NORMAL_OBSERVER_BINDING");
            OwnedUnitPermission(run,nonce);
            mapObserver=observer;
        }
    }
    [Serializable] public class DSNormalMapFrame
    {
        public int step,frame,simulationCalls,eventOffset;
        public long gameFrame,gameSimulation;
        public float actualDt,suppliedDt,requestedDt;
        public Vector2 input;
        public string before,afterTime,beforeSimulation,afterSimulation,after;
        public DSMapCommandEvent[] commands;
    }
    // Observes the production queue path. It owns only a frozen input tape.
    public sealed class DSNormalMapObserver
    {
        readonly DSMapFrameInput[] tape;
        readonly Func<bool> owner;
        readonly Action complete;
        readonly bool device;
        readonly string initial;
        string previous;
        int frame=-1,eventOffset;
        long firstSimulation;
        bool submitting;
        HashSet<string> expectedIds=new();
        DSNormalMapFrame current;
        public readonly List<DSNormalMapFrame> Frames=new();
        public string Error {get;private set;}="";
        public string RecordingError {get;private set;}="";
        public string PartialState {get;private set;}="";
        public bool Finished {get;private set;}
        public bool InFrame=>current!=null&&!Finished;
        public bool ContinueOnRejection=>tape[Frames.Count].continueOnRejection;
        public bool Device=>device;
        public DSNormalMapFrame FailedFrame=>current;
        public DSNormalMapObserver(DSMapFrameInput[] inputs,bool useDevice,string frozenInitial,Func<bool> ownership,Action done)
        {
            if(inputs==null||inputs.Length<1||inputs.Length>10000||ownership==null||done==null||string.IsNullOrEmpty(frozenInitial))throw new ArgumentException("NORMAL_OBSERVER_PLAN");
            tape=inputs.Select(s=>s==null?throw new ArgumentException("NORMAL_OBSERVER_STEP"):new DSMapFrameInput{dt=s.dt,move=s.move,continueOnRejection=s.continueOnRejection,commands=s.commands?.Select(c=>c==null?throw new ArgumentException("NORMAL_OBSERVER_COMMAND"):new DSMapFrameCommand{id=c.id,kind=c.kind,value=c.value}).ToArray()??Array.Empty<DSMapFrameCommand>()}).ToArray();
            var ids=new HashSet<string>();
            foreach(var s in tape)
            {
                if(!DSMapGeometry.Finite(s.dt)||s.dt<0||s.dt>1||!DSMapGeometry.ValidPoint(s.move)||s.move.sqrMagnitude>1.000001f||s.commands.Length>32)throw new ArgumentException("NORMAL_OBSERVER_INPUT");
                foreach(var c in s.commands)if(string.IsNullOrEmpty(c.id)||c.id.Length>128||!ids.Add(c.id)||!(new[]{"Pause","Resume","SelectFormation","CancelFormation","Revive","NextWave"}).Contains(c.kind))throw new ArgumentException("NORMAL_OBSERVER_COMMAND");
            }
            device=useDevice;initial=frozenInitial;owner=ownership;complete=done;
        }
        static string State(DSGame g)
        {
            if(g.Enemies.Count!=0||g.Shots.Count!=0||g.Effects.Count!=0)throw new InvalidOperationException("NORMAL_SNAPSHOT_UNSUPPORTED_COMBAT_OBJECTS");
            return g.CaptureMapFrameState();
        }
        public void Begin(DSGame g,long gameFrame,long gameSimulation)
        {
            if(Finished||current!=null||!owner()||Time.frameCount<=frame)throw new InvalidOperationException("NORMAL_OBSERVER_OWNERSHIP_OR_FRAME");
            string before=State(g);
            if(before!=(previous??initial))throw new InvalidOperationException("NORMAL_STATE_CONTAMINATION");
            frame=Time.frameCount;eventOffset=g.MapCommandEvents.Length;firstSimulation=gameSimulation;
            current=new DSNormalMapFrame{step=Frames.Count,frame=frame,gameFrame=gameFrame,eventOffset=eventOffset,actualDt=Time.unscaledDeltaTime,suppliedDt=device?Time.unscaledDeltaTime:tape[Frames.Count].dt,before=before};
            expectedIds=new HashSet<string>(tape[Frames.Count].commands.Select(c=>c.id));
            submitting=true;
            try
            {
                foreach(var c in tape[Frames.Count].commands)
                    if(!g.EnqueueMapCommand(c.id,g.MapCommandNonce,c.kind,c.value,"TEST"))throw new InvalidOperationException("NORMAL_DRIVER_ENQUEUE_FAILED");
            }
            finally{submitting=false;}
        }
        public bool AllowsSubmission(string id,string source)=>InFrame&&submitting&&source=="TEST"&&expectedIds.Remove(id);
        public void AfterTime(DSGame g){current.afterTime=State(g);}
        public Vector2 Movement(Vector2 native)=>device?native:tape[Frames.Count].move;
        public float Delta=>device?Time.unscaledDeltaTime:tape[Frames.Count].dt;
        public void BeforeSimulation(DSGame g,float requested,Vector2 input)
        {current.beforeSimulation=State(g);current.requestedDt=requested;current.input=input;}
        public void AfterSimulation(DSGame g){current.afterSimulation=State(g);}
        public void End(DSGame g,long gameSimulation)
        {
            if(!InFrame||!owner()||expectedIds.Count!=0)throw new InvalidOperationException("NORMAL_OBSERVER_END");
            current.commands=g.MapCommandEvents.Skip(eventOffset).ToArray();current.gameSimulation=gameSimulation;current.simulationCalls=checked((int)(gameSimulation-firstSimulation));
            if(current.simulationCalls>1)throw new InvalidOperationException("NORMAL_SIMULATION_MULTIPLE");
            current.after=State(g);previous=current.after;Frames.Add(current);current=null;
            if(Frames.Count==tape.Length)Terminal();
        }
        public void Fail(DSGame g,string error)
        {
            if(Error=="")Error=error;
            try{PartialState=g.CaptureMapFrameState();}catch(Exception e){PartialState="CAPTURE_FAILED:"+e.Message;}
            if(current!=null)
            {
                current.after=PartialState;current.commands=g.MapCommandEvents.Skip(eventOffset).ToArray();
                current.gameSimulation=g.NormalObservedGameSimulations;current.simulationCalls=checked((int)(current.gameSimulation-firstSimulation));
            }
            Terminal();
        }
        void Terminal()
        {
            if(Finished)return;Finished=true;
            try{complete();}catch(Exception e){RecordingError=e.ToString();}
        }
    }
}
#endif
