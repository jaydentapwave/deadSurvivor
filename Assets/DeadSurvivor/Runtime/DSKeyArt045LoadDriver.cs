#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace DeadSurvivor
{
    // 045 verification only (editor builds). Attached by the Play probe and removed afterwards.
    // Runs after DSGame.Update (order 1000) so each tick sees a finished Simulate + World.Render.
    // Per completed simulation: order check -> compare views -> heal -> (load mode) inject at most one Spawn(extra:true)
    // when this simulation used no path search. HealOnly mode never injects (natural waves with protected heroes).
    [DefaultExecutionOrder(1000)]
    public sealed class DSKeyArt045LoadDriver : MonoBehaviour
    {
        public DSGame Game;
        public int Target=300;
        public bool HealOnly;
        public int InjectPerSimulation=10;   // load generation rate; slow (mobile-pipeline) editor frames need more per simulation
        public float HoldSeconds=20, ReachLimitSeconds=35;
        static readonly FieldInfo Searches=typeof(DSGame).GetField("mapSpawnSearches",BindingFlags.Instance|BindingFlags.NonPublic);
        long lastSimulation=-1;
        float started=-1, reachedAt=-1, nextSample;
        public int Ticks, Injected, InjectFailures, SkippedBudget, Mismatches, OrderViolations, MaxAlive, MinAliveWhileHolding=int.MaxValue;
        // natural spawn failures per simulation: no path search happened at all (every candidate rejected by boundary/obstacles)
        // vs a search happened (path failure or the 1-search-per-simulation budget). Injection failures are excluded.
        public int GeoFailures, SearchFailures;int lastFailures=-1;
        public bool Reached, Held, HeroDied, Ended, SearchFieldMissing;
        public string EndReason="";
        public float HoldEnd=-1;
        // still enemies (>=10 s, moved < 0.5, farther than 60 from every hero), classified by cause
        // holdRanged: ranged inside its real stop band 163.8..231.84 with line of sight (DSGame.Combat.cs:283-286);
        // phase: fuse/windup; waitingRetry: route search failed/pending and its retry time not reached;
        // pendingSearch: no route points and retry due (waiting for the per-frame search budget);
        // onRoute/noRoute: still although following a route / with no route entry = unexplained (judged).
        public int RawStillMax, HoldRangedMax, PhaseMax, NoPathMax, WaitingPathMax, PendingSearchMax, UnexplainedMax;
        public float RawStillMaxRatio, NoPathMaxRatio, WaitingPathMaxRatio, UnexplainedMaxRatio;
        static readonly FieldInfo Routes=typeof(DSGame).GetField("mapRoutes",BindingFlags.Instance|BindingFlags.NonPublic);
        public readonly List<string> UnexplainedSamples=new();
        readonly Dictionary<Enemy,(Vector2 anchor,float since)> still=new();
        readonly Dictionary<Enemy,(bool path,float at)> pathCache=new();
        readonly Dictionary<float,DSMapNavigation> navs=new();
        public readonly List<string> Order=new(), Samples=new(), NoPathSamples=new();
        public readonly List<float> HoldFrameMs=new();
        public void Begin(DSGame game,bool protectHeroes=true)
        {
            Game=game;started=Time.unscaledTime;nextSample=0;
            SearchFieldMissing=Searches==null;
            if(protectHeroes)foreach(var c in game.Team)if(c.hp>0){c.maxhp=1e9f;c.hp=1e9f;}
            lastSimulation=game.NormalObservedGameSimulations;lastFailures=game.MapSpawnFailures;
        }
        void Update()
        {
            if(Game==null||Ended)return;
            long sim=Game.NormalObservedGameSimulations;
            int rendered=Game.World.MapActorRenderedFrame;bool hasViews=Game.World.MapActorViewCount>=0;
            if(Order.Count<10)Order.Add($"frame={Time.frameCount} sim={sim} rendered={rendered}");
            if(sim==lastSimulation)return;            // no completed simulation this frame (paused, result, ...)
            lastSimulation=sim;Ticks++;
            int aliveHeroes=0;foreach(var c in Game.Team)if(c.hp>0)aliveHeroes++;
            if(aliveHeroes<Game.Team.Count){HeroDied=true;Ended=true;EndReason="HERO_DIED";return;}
            if(Game.State!="battle"){Ended=true;EndReason="STATE_"+Game.State;return;}
            int alive=Game.Enemies.Count;
            if(alive>MaxAlive)MaxAlive=alive;
            int df=Game.MapSpawnFailures-lastFailures;
            if(df>0&&Searches!=null){if((int)Searches.GetValue(Game)==0)GeoFailures+=df;else SearchFailures+=df;}
            // order: this frame's DSGame.Update must already have rendered (views path only)
            if(hasViews&&rendered!=Time.frameCount)OrderViolations++;
            else if(hasViews&&Game.World.MapActorViewCount!=alive+aliveHeroes)Mismatches++;
            float t=Time.unscaledTime-started;
            if(t>=nextSample){nextSample=t+.5f;Classify(t,alive);}
            if(!HealOnly)
            {
                if(!Reached && alive>=Target){Reached=true;reachedAt=t;}
                if(Reached && !Held)
                {
                    HoldFrameMs.Add(Time.unscaledDeltaTime*1000);
                    if(alive<MinAliveWhileHolding)MinAliveWhileHolding=alive;
                    if(alive<Target){Reached=false;MinAliveWhileHolding=int.MaxValue;HoldFrameMs.Clear();}
                    else if(t-reachedAt>=HoldSeconds){Held=true;HoldEnd=t;Ended=true;EndReason="HELD";}
                }
                if(!Reached && t>ReachLimitSeconds){Ended=true;EndReason="REACH_LIMIT";return;}
            }
            foreach(var c in Game.Team)c.hp=c.maxhp;   // heal
            lastFailures=Game.MapSpawnFailures;
            int ceiling=Target+50;   // top up above the judged target so a few kills do not reset the 20 s hold
            if(HealOnly||alive>=ceiling)return;
            if(Searches==null)return;
            // Load generation only: up to InjectPerSimulation injected spawns per simulation, each with the per-simulation search budget cleared
            // (test-only memory write; the game's own spawn rules and code are unchanged). Counted apart from natural failures.
            for(int k=0;k<InjectPerSimulation&&Game.Enemies.Count<ceiling;k++)
            {
                if((int)Searches.GetValue(Game)>=1){SkippedBudget++;Searches.SetValue(Game,0);}
                int before=Game.MapSpawnFailures;
                Game.Spawn(extra:true);
                if(Game.MapSpawnFailures>before)InjectFailures++;else Injected++;
            }
            lastFailures=Game.MapSpawnFailures;
        }
        void Classify(float t,int alive)
        {
            var gone=new List<Enemy>();foreach(var k in still.Keys)if(!Game.Enemies.Contains(k))gone.Add(k);foreach(var k in gone){still.Remove(k);pathCache.Remove(k);}
            int raw=0,ranged=0,phase=0,noPath=0,waiting=0,pending=0,unexplained=0;var map=Game.MapSession?Game.MapSession.Geometry:null;
            var routes=Routes?.GetValue(Game) as System.Collections.IDictionary;
            foreach(var e in Game.Enemies)
            {
                if(!still.TryGetValue(e,out var s)||(e.pos-s.anchor).magnitude>=.5f){still[e]=(e.pos,t);continue;}
                Survivor hero=null;float near=float.MaxValue;foreach(var c in Game.Team)if(c.hp>0){float d=(c.pos-e.pos).magnitude;if(d<near){near=d;hero=c;}}
                if(t-s.since<10||near<=60||hero==null)continue;
                raw++;
                if(e.type=="ranged"&&near>=252*.65f&&near<=252*.92f&&(map==null||map.SafeSweep(e.pos,hero.pos,.01f))){ranged++;continue;}
                if(e.phase=="fuse"||e.phase=="windup"){phase++;continue;}
                if(map==null){unexplained++;continue;}
                if(!pathCache.TryGetValue(e,out var pc)||t-pc.at>5)
                {
                    float r=Game.Monsters[e.type].radius;if(!navs.TryGetValue(r,out var nav))navs[r]=nav=new DSMapNavigation(map,r);
                    pc=(nav.TryPath(e.pos,hero.pos,out _,16384),t);pathCache[e]=pc;
                }
                if(!pc.path){noPath++;if(NoPathSamples.Count<20)NoPathSamples.Add($"{t:F1}s {e.type} pos=({e.pos.x:F0},{e.pos.y:F0}) hero=({hero.pos.x:F0},{hero.pos.y:F0})");continue;}
                string state="noRoute";
                if(routes!=null&&routes.Contains(e))
                {
                    var rt=routes[e];var tp=rt.GetType();
                    var points=tp.GetField("points").GetValue(rt);float retry=(float)tp.GetField("retry").GetValue(rt);
                    state=points!=null?"onRoute":Game.TotalTime<retry?"waitingRetry":"pendingSearch";
                }
                if(state=="waitingRetry")waiting++;
                else if(state=="pendingSearch")pending++;
                else{unexplained++;if(UnexplainedSamples.Count<20)UnexplainedSamples.Add($"{t:F1}s {e.type} {state} pos=({e.pos.x:F0},{e.pos.y:F0}) hero={near:F0}");}
            }
            if(raw>RawStillMax)RawStillMax=raw;if(ranged>HoldRangedMax)HoldRangedMax=ranged;if(phase>PhaseMax)PhaseMax=phase;
            if(noPath>NoPathMax)NoPathMax=noPath;if(waiting>WaitingPathMax)WaitingPathMax=waiting;if(pending>PendingSearchMax)PendingSearchMax=pending;if(unexplained>UnexplainedMax)UnexplainedMax=unexplained;
            bool judge=HealOnly||Reached;
            if(judge&&alive>0)
            {
                RawStillMaxRatio=Mathf.Max(RawStillMaxRatio,(float)raw/alive);
                NoPathMaxRatio=Mathf.Max(NoPathMaxRatio,(float)noPath/alive);
                WaitingPathMaxRatio=Mathf.Max(WaitingPathMaxRatio,(float)(waiting+pending)/alive);
                UnexplainedMaxRatio=Mathf.Max(UnexplainedMaxRatio,(float)unexplained/alive);
            }
            Samples.Add($"{t:F1}s alive={alive} views={Game.World.MapActorViewCount} injected={Injected} fail={InjectFailures} still={raw} ranged={ranged} phase={phase} noPath={noPath} waitRetry={waiting} pending={pending} unexplained={unexplained} dt={Time.unscaledDeltaTime*1000:F1}ms");
        }
    }
}
#endif
