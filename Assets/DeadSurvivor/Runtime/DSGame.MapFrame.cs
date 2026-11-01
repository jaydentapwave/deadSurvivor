using System;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
#endif
namespace DeadSurvivor
{
    public partial class DSGame
    {
        // UI and keyboard use the same domain entry point. Legacy behavior stays synchronous.
        public void RequestPause(bool pause)
        {
            if(RejectMovementFixtureCommand(pause?"Pause":"Resume"))return;
            if(State!="battle")return;
            Paused=pause;UI.ShowPause();
        }
#if UNITY_EDITOR
        DSMapFrameBinding mapFrame;
        bool mapFramePermit;
        public void BindMapFrame(DSMapFrameBinding binding)
        {
            if(enabled || mapObserver!=null || mapFrame!=null || movementFixture!=null || mapUnitScope || mapCommandPermit || !MapSession || binding==null)
                throw new InvalidOperationException("MAP_FRAME_BINDING_INVALID");
            mapFrame=binding;
        }
        internal void MapFrameInvoke(Action action)
        {
            if(mapFrame==null || mapFramePermit || mapCommandPermit)throw new InvalidOperationException("MAP_FRAME_TOKEN_INVALID");
            mapFramePermit=mapCommandPermit=true;
            try{action();}finally{mapFramePermit=mapCommandPermit=false;}
        }
        internal bool MapFrameReject(string command)
        {
            if(mapFrame==null || mapFramePermit)return false;
            mapFrame.Fail("UNOWNED_COMMAND:"+command);return true;
        }
        internal void MapFrameSimulation()
        {
            if(mapFrame==null)return;
            if(!mapFramePermit){mapFrame.Fail("UNOWNED_SIMULATE");throw new InvalidOperationException("UNOWNED_SIMULATE");}
            mapFrame.NoteSimulation();
        }
        public void InitializeMapFixtureDamage(int slot)
        {
            if(enabled || mapFrame!=null || !MapSession || slot<0 || slot>=Team.Count)throw new InvalidOperationException("INVALID_FIXTURE_DAMAGE");
            Damage(Team[slot],Team[slot].maxhp*1000);
        }
        internal void ClearMapRejection(){MapRejection="";}
        public string CaptureMapFrameState()
        {
            var fields=typeof(DSGame).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                .Where(f=>(f.FieldType==typeof(int)||f.FieldType==typeof(float)||f.FieldType==typeof(bool)||f.FieldType==typeof(string)||f.FieldType==typeof(Vector2)) && f.Name!="InitialLoadTime" && !f.Name.StartsWith("mapFrame",StringComparison.Ordinal) && !f.Name.StartsWith("mapQueue",StringComparison.Ordinal) && !f.Name.StartsWith("mapCommand",StringComparison.Ordinal) && !f.Name.StartsWith("mapUnit",StringComparison.Ordinal) && !f.Name.Contains("MapRejection") && !f.Name.Contains("MapInput"))
                .OrderBy(f=>f.Name).Select(f=>new DSMapScalar {name=f.Name,value=f.GetValue(this) is float n?n.ToString("R",CultureInfo.InvariantCulture):Convert.ToString(f.GetValue(this),CultureInfo.InvariantCulture)}).ToArray();
            return JsonUtility.ToJson(new DSMapFrameSnapshot {center=Center,actual=Team.Select(c=>c.pos).ToArray(),reserved=Enumerable.Range(0,Team.Count).Select(i=>Center+Offset(i)).ToArray(),alive=Team.Select(c=>c.hp>0).ToArray(),
                formation=Formation,pending=PendingFormation,state=State,paused=Paused,elapsed=Elapsed,total=TotalTime,wave=Wave,selected=Selected.ToArray(),config=Config,
                team=Team.ToArray(),stash=Stash.ToArray(),enemies=Enemies.ToArray(),shots=Shots.ToArray(),effects=Effects.ToArray(),history=History.ToArray(),scalars=fields,
                timers=Team.Select((c,i)=>new DSMapSurvivorTimers{slot=i,heal=c.healClock,shield=c.shieldClock,flash=c.flash,share=c.shareClock}).ToArray(),
                gearTimers=Team.SelectMany(c=>c.items).Concat(Stash).Select(g=>new DSMapGearTimers{id=g.id,cd=g.cd,period=g.lastPeriod,flash=g.activationFlash}).ToArray(),
                rngCalls=(Rng as DSMapTraceRandom)?.Count??-1,lootCalls=(LootRng as DSMapTraceRandom)?.Count??-1,
                rngHash=(Rng as DSMapTraceRandom)?.TraceHash??"UNTRACED",lootHash=(LootRng as DSMapTraceRandom)?.TraceHash??"UNTRACED"});
        }
#endif
    }
#if UNITY_EDITOR
    [Serializable] public class DSMapSurvivorTimers {public int slot;public float heal,shield,flash,share;}
    [Serializable] public class DSMapGearTimers {public int id;public float cd,period,flash;}
    [Serializable] public class DSMapScalar {public string name,value;}
    [Serializable] public class DSMapFrameSnapshot
    {
        public Vector2 center;public Vector2[] actual,reserved;public bool[] alive;
        public int formation,pending,wave,rngCalls,lootCalls;public string state,rngHash,lootHash;public bool paused;public float elapsed,total;
        public int[] selected;public RunSettings config;public Survivor[] team;public Gear[] stash;public Enemy[] enemies;public Shot[] shots;public Effect[] effects;public WaveRecord[] history;public DSMapScalar[] scalars;public DSMapSurvivorTimers[] timers;public DSMapGearTimers[] gearTimers;
    }
    [Serializable] public class DSMapRandomCall {public int sequence;public string method,args,response;}
    public sealed class DSMapTraceRandom:System.Random
    {
        readonly List<DSMapRandomCall> calls=new();
        readonly int seed;
        int drawDepth;
        const int TraceLimit=16384;
        public int Seed=>seed;
        public DSMapRandomCall[] Calls=>calls.Select(c=>new DSMapRandomCall{sequence=c.sequence,method=c.method,args=c.args,response=c.response}).ToArray();
        public DSMapTraceRandom(int initialSeed):base(initialSeed)
        {
            seed=initialSeed;
            string[] expected={"Double Sample()","Int32 Next()","Int32 Next(Int32, Int32)","Int32 Next(Int32)","Double NextDouble()","Void NextBytes(Byte[])","Void NextBytes(System.Span`1[System.Byte])"};
            var actual=typeof(System.Random).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(m=>m.IsVirtual).Select(m=>m.ToString()).OrderBy(x=>x).ToArray();
            if(!actual.SequenceEqual(expected.OrderBy(x=>x)))throw new InvalidOperationException("RNG_RUNTIME_API_CHANGED");
        }
        public int Count=>calls.Count;
        public string TraceHash=>DSMapBlockoutPreview.Hash(string.Join(";",calls.Select(c=>c.sequence+":"+c.method+":"+c.args+":"+c.response)));
        T Draw<T>(Func<T> action)
        {
            if(drawDepth!=0)throw new InvalidOperationException("RNG_REENTRANT_DRAW");
            if(calls.Count>=TraceLimit)throw new InvalidOperationException("RNG_TRACE_LIMIT");
            drawDepth++;
            try{return action();}finally{drawDepth--;}
        }
        protected override double Sample()
        {if(drawDepth!=1)throw new InvalidOperationException("RNG_UNTRACKED_SAMPLE");return base.Sample();}
        void Note(string method,string args,string response){calls.Add(new DSMapRandomCall{sequence=calls.Count,method=method,args=args,response=response});}
        public override int Next(){int n=Draw(()=>base.Next());Note("Next","",n.ToString(CultureInfo.InvariantCulture));return n;}
        public override int Next(int max){int n=Draw(()=>base.Next(max));Note("Next",max.ToString(CultureInfo.InvariantCulture),n.ToString(CultureInfo.InvariantCulture));return n;}
        public override int Next(int min,int max){int n=Draw(()=>base.Next(min,max));Note("Next",min.ToString(CultureInfo.InvariantCulture)+","+max.ToString(CultureInfo.InvariantCulture),n.ToString(CultureInfo.InvariantCulture));return n;}
        public override double NextDouble(){double n=Draw(()=>base.NextDouble());Note("NextDouble","",n.ToString("R",CultureInfo.InvariantCulture));return n;}
        public override void NextBytes(byte[] buffer)=>throw new InvalidOperationException("RNG_UNSUPPORTED_NEXT_BYTES");
        public override void NextBytes(Span<byte> buffer)=>throw new InvalidOperationException("RNG_UNSUPPORTED_NEXT_BYTES");
        public void ValidateTrace()
        {
            var replay=new System.Random(seed);
            foreach(var c in calls)
            {
                string expected;
                if(c.sequence<0||c.sequence>=calls.Count||!ReferenceEquals(c,calls[c.sequence]))throw new InvalidOperationException("RNG_TRACE_SEQUENCE");
                if(c.method=="NextDouble"&&c.args=="")expected=replay.NextDouble().ToString("R",CultureInfo.InvariantCulture);
                else if(c.method=="Next")
                {
                    var args=c.args==""?Array.Empty<int>():c.args.Split(',').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();
                    expected=(args.Length==0?replay.Next():args.Length==1?replay.Next(args[0]):args.Length==2?replay.Next(args[0],args[1]):throw new InvalidOperationException("RNG_TRACE_ARGS")).ToString(CultureInfo.InvariantCulture);
                }
                else throw new InvalidOperationException("RNG_TRACE_METHOD");
                if(expected!=c.response)throw new InvalidOperationException("RNG_TRACE_RESPONSE");
            }
        }
    }
    [Serializable] public class DSMapFrameCommand {public string id,kind;public int value;}
    [Serializable] public class DSMapFrameInput {public float dt;public Vector2 move;public DSMapFrameCommand[] commands;public bool continueOnRejection;}
    [Serializable] public class DSMapFrameCommandRecord {public string id,kind,before,after,result;public int sequence,enqueueFrame;}
    [Serializable] public class DSMapFrameObserved
    {
        public string run,nonce,caseId,before,after;public int step,frame,simulationCalls,combatCalls;
        public float actualDt,suppliedDt,requestedDt,appliedDt;public Vector2 input;
        public DSMapFrameCommandRecord[] commands;
    }
    public sealed class DSMapFrameBinding
    {
        readonly string run,nonce,caseId;
        readonly DSMapFrameInput[] inputs;
        readonly bool device;
        readonly int queueLimit;
        readonly Action completed;
        readonly HashSet<string> commandIds=new();
        readonly Func<bool> ownership;
        public readonly List<DSMapFrameObserved> Observed=new();
        public string Error {get;private set;}="";
        public string FailureExpected {get;private set;}="";
        public string FailureObserved {get;private set;}="";
        public bool Finished {get;private set;}
        public int SimulationCalls {get;private set;}
        public int CombatCalls {get;private set;}
        public void NoteCombatCall(){CombatCalls++;throw new InvalidOperationException("COMBAT_LEAK");}
        int frame=-1,consumeSequence;
        string previous;
        public DSMapFrameBinding(string runId,string nonceId,string id,DSMapFrameInput[] steps,bool useDevice,int limit,Func<bool> owner,Action done)
        {
            if(string.IsNullOrEmpty(runId)||string.IsNullOrEmpty(nonceId)||string.IsNullOrEmpty(id)||steps==null||steps.Length<1||steps.Length>10000||limit<1||owner==null||done==null)throw new ArgumentException("INVALID_FRAME_PLAN");
            run=runId;nonce=nonceId;caseId=id;device=useDevice;queueLimit=limit;ownership=owner;completed=done;
            // The producer cannot modify an already-bound input array by retaining a reference.
            inputs=steps.Select(s=>new DSMapFrameInput{dt=s.dt,move=s.move,continueOnRejection=s.continueOnRejection,commands=s.commands?.Select(c=>new DSMapFrameCommand{id=c.id,kind=c.kind,value=c.value}).ToArray()??Array.Empty<DSMapFrameCommand>()}).ToArray();
            var ids=new HashSet<string>();
            foreach(var s in inputs)
            {
                if(!DSMapGeometry.Finite(s.dt)||s.dt<0||s.dt>1||!DSMapGeometry.ValidPoint(s.move)||s.move.sqrMagnitude>1.000001f||s.commands.Length>limit)throw new ArgumentException("INVALID_FRAME_INPUT");
                foreach(var c in s.commands)if(string.IsNullOrEmpty(c.id)||!ids.Add(c.id)||!(new[]{"Pause","Resume","SelectFormation","CancelFormation","Revive","NextWave"}).Contains(c.kind))throw new ArgumentException("INVALID_FRAME_COMMAND");
            }
        }
        public void Fail(string reason){if(Error=="")Error=reason;Terminal();}
        void Terminal(){if(Finished)return;Finished=true;completed();}
        public void NoteSimulation(){if(++SimulationCalls!=1)throw new InvalidOperationException("UNEXPECTED_STEP");}
        public void AdvanceFrame(DSGame game)
        {
            if(Finished)return;
            try
            {
                if(!ownership())throw new InvalidOperationException("FRAME_OWNERSHIP_LOST");
                if(Time.frameCount<=frame)throw new InvalidOperationException("DUPLICATE_FRAME");
                frame=Time.frameCount;SimulationCalls=CombatCalls=0;
                string before=game.CaptureMapFrameState();
                if(previous!=null && before!=previous){FailureExpected=previous;FailureObserved=before;throw new InvalidOperationException("STATE_CONTAMINATION");}
                var input=inputs[Observed.Count];var commands=new List<DSMapFrameCommandRecord>();
                float applied=0,requested=0;
                game.MapFrameInvoke(()=>{
                    // This array is the frozen FIFO cut for this frame; no recursive consume.
                    foreach(var command in input.commands)
                    {
                        if(!commandIds.Add(command.id))throw new InvalidOperationException("DUPLICATE_COMMAND");
                        var record=new DSMapFrameCommandRecord{id=command.id,kind=command.kind,sequence=consumeSequence++,enqueueFrame=frame,before=game.CaptureMapFrameState()};
                        game.ClearMapRejection();
                        bool eligible=command.kind=="Pause"||command.kind=="Resume"?game.State=="battle":game.State=="prep";
                        if(command.kind=="SelectFormation")eligible &= command.value>=0 && command.value<=2;
                        if(command.kind=="Revive")eligible &= command.value>=0 && command.value<game.Team.Count && game.Team[command.value].hp<=0;
                        if(eligible)switch(command.kind)
                        {
                            case "Pause":game.RequestPause(true);break;
                            case "Resume":game.RequestPause(false);break;
                            case "SelectFormation":game.SelectFormation(command.value);break;
                            case "CancelFormation":game.SelectFormation(game.Formation);break;
                            case "Revive":game.Revive(command.value);break;
                            case "NextWave":game.NextWave();break;
                        }
                        record.after=game.CaptureMapFrameState();record.result=!eligible?"STATE_REJECTED":game.MapRejection!=""?game.MapRejection:"ACCEPTED";commands.Add(record);
                        if(record.result!="ACCEPTED" && !input.continueOnRejection)return;
                    }
                    if(game.State=="battle" && !game.Paused)
                    {
                        requested=Mathf.Min(device?Time.unscaledDeltaTime:input.dt,.05f)*game.Config.speed;
                        applied=Mathf.Min(requested,Mathf.Max(0,game.Duration-game.Elapsed));
                        game.MoveInput=device?game.ReadMovementInput():input.move;
                        game.Simulate(requested);
                    }
                });
                game.World.Render();game.UI.Tick();
                string after=game.CaptureMapFrameState();
                Observed.Add(new DSMapFrameObserved{run=run,nonce=nonce,caseId=caseId,step=Observed.Count,frame=frame,before=before,after=after,actualDt=Time.unscaledDeltaTime,suppliedDt=device?Time.unscaledDeltaTime:input.dt,requestedDt=requested,appliedDt=applied,simulationCalls=SimulationCalls,combatCalls=CombatCalls,input=game.MoveInput,commands=commands.ToArray()});
                previous=after;
                if(Observed.Count==inputs.Length)Terminal();
            }
            catch(Exception e){Fail(e.Message);}
        }
    }
#endif
}
