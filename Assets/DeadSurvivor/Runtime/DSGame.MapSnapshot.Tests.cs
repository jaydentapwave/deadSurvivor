#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
namespace DeadSurvivor
{
    [Serializable] public sealed class DSFullSnapshotUnitRaw
    {
        public string role="SOURCE_DTO_UNIT_DIAGNOSTIC_031",initial,baseline,afterSourceMutation,frozenBefore,frozenAfter;
        public string[] checkedFields,checks;
        public string initialSourceBefore,initialSourceAfter,baselineSourceBefore,baselineSourceAfter;
        public bool sourceReferencesUnchanged;
    }
    // Independent logical source witness. It never calls the DTO collector.
    // Observer context and CLR collection projection/monitor caches are excluded;
    // logical entries, versions, object identity and Random private state are kept.
    sealed class DSFullSourceWitness
    {
        readonly Dictionary<string,object> references=new(StringComparer.Ordinal);
        public string Hash;
        public bool Same(DSFullSourceWitness other)=>Hash==other.Hash&&references.Count==other.references.Count&&references.All(p=>other.references.TryGetValue(p.Key,out var value)&&ReferenceEquals(p.Value,value));
        public static DSFullSourceWitness Capture(DSGame game)
        {
            var result=new DSFullSourceWitness();var seen=new HashSet<object>(DSFullReferenceComparer<object>.Instance);var text=new System.Text.StringBuilder();int nodes=0;
            void Atom(string value){text.Append(value.Length).Append(':').Append(value);}
            void Version(object value)
            {
                for(var type=value.GetType();type!=null&&type!=typeof(object);type=type.BaseType)
                    foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(f=>f.Name.IndexOf("version",StringComparison.OrdinalIgnoreCase)>=0))
                        if(field.FieldType==typeof(int)||field.FieldType==typeof(long)||field.FieldType==typeof(uint)){Atom(field.Name);Atom(Convert.ToString(field.GetValue(value),CultureInfo.InvariantCulture));}
            }
            void Fields(object value,Type type,string path,int depth)
            {
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).OrderBy(f=>f.Name,StringComparer.Ordinal))
                {if(type==typeof(DSGame)&&field.Name=="fullSnapshotContext")continue;Atom(type.FullName+"."+field.Name);Walk(field.GetValue(value),path+"."+type.FullName+"."+field.Name,depth+1);}
            }
            void Walk(object value,string path,int depth)
            {
                if(++nodes>200000||depth>64||text.Length>16*1024*1024)throw new InvalidOperationException("SNAPSHOT_SOURCE_WITNESS_LIMIT");
                if(value==null){Atom("NULL");return;}
                var type=value.GetType();Atom(type.FullName);
                if(value is string s){Atom(s);return;}if(value is float f){Atom(BitConverter.SingleToInt32Bits(f).ToString("X8",CultureInfo.InvariantCulture));return;}if(value is double d){Atom(BitConverter.DoubleToInt64Bits(d).ToString("X16",CultureInfo.InvariantCulture));return;}
                if(type.IsPrimitive||type.IsEnum){Atom(Convert.ToString(value,CultureInfo.InvariantCulture));return;}
                if(!type.IsValueType){result.references.Add(path,value);if(!seen.Add(value)){Atom("ALREADY_SEEN");return;}}
                if(value is DSMapSession){Fields(value,typeof(DSMapSession),path,depth);return;}
                if(value is UnityEngine.Object obj){Atom(obj.GetInstanceID().ToString(CultureInfo.InvariantCulture));return;}
                if(value is System.Collections.IDictionary dictionary)
                {Atom(dictionary.Count.ToString(CultureInfo.InvariantCulture));Version(value);int i=0;foreach(System.Collections.DictionaryEntry entry in dictionary){Walk(entry.Key,path+"["+i+"].key",depth+1);Walk(entry.Value,path+"["+i+"].value",depth+1);i++;}return;}
                if(value is System.Collections.IEnumerable sequence)
                {Version(value);int i=0;foreach(var item in sequence){Walk(item,path+"["+i+"]",depth+1);i++;}Atom(i.ToString(CultureInfo.InvariantCulture));return;}
                for(var current=type;current!=null&&current!=typeof(object);current=current.BaseType)Fields(value,current,path,depth);
            }
            result.references.Add("game",game);Fields(game,typeof(DSGame),"game",0);result.Hash=DSMapBlockoutPreview.Hash(text.ToString());return result;
        }
    }
    public sealed partial class DSMapNavigation
    {
        internal void PrepareSnapshotUnitCache()
        {nodes[new Vector2Int(3,4)]=true;nodes[new Vector2Int(-2,1)]=false;edges[(new Vector2Int(-2,1),new Vector2Int(3,4))]=true;LastVisited=7;}
        internal void MutateSnapshotUnitCache(){nodes[new Vector2Int(3,4)]=false;LastVisited=9;}
    }
    public partial class DSGame
    {
        sealed class SnapshotUnitDerivedGear:Gear {public int extra=123;}
        public DSFullSnapshotUnitRaw RunFullSnapshotOwnedUnitChecks()
        {
            // The collector verifies disabled, registered ownership before any fixture mutation.
            if(fullSnapshotContext==null||System.Threading.Thread.CurrentThread.ManagedThreadId!=fullSnapshotContext.thread||enabled||!mapUnitScope||!mapCommandPermit)throw new InvalidOperationException("SNAPSHOT_UNIT_OWNERSHIP");
            var initialSourceBefore=DSFullSourceWitness.Capture(this);var initial=CaptureFullMapSnapshot();var initialSourceAfter=DSFullSourceWitness.Capture(this);
            var checks=new List<string>();var fields=new List<string>();
            void Check(bool value,string id){if(!value)throw new InvalidOperationException("SNAPSHOT_UNIT:"+id);checks.Add(id);}
            void SameFields(object source,object dto,string prefix,params string[] special)
            {
                foreach(var field in source.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    if(special.Contains(field.Name))continue;
                    var destination=dto.GetType().GetField(field.Name,BindingFlags.Public|BindingFlags.Instance);
                    if(destination==null)throw new InvalidOperationException("SNAPSHOT_UNIT_FIELD_MISSING:"+prefix+"."+field.Name);
                    object expected=field.GetValue(source),actual=destination.GetValue(dto);bool equal;
                    if(field.FieldType==typeof(string)){var text=(DSFullText)actual;equal=text!=null&&text.isNull==(expected==null)&&text.value==((string)expected??"");}
                    else if(expected is bool[] cells)equal=actual is bool[] copy&&!ReferenceEquals(cells,copy)&&cells.SequenceEqual(copy);
                    else if(field.FieldType==typeof(int)||field.FieldType==typeof(float)||field.FieldType==typeof(bool)||field.FieldType==typeof(Vector2))equal=Equals(expected,actual);
                    else throw new InvalidOperationException("SNAPSHOT_UNIT_UNHANDLED_FIELD:"+prefix+"."+field.Name);
                    Check(equal,"field:"+prefix+"."+field.Name);fields.Add(prefix+"."+field.Name);
                }
            }
            void SameScalars(DSFullScalar[] rows,string prefix)
            {
                foreach(var row in rows)
                {
                    var field=typeof(DSGame).GetField(row.name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);var source=field.GetValue(this);object actual;
                    if(field.FieldType==typeof(int))actual=row.integer;else if(field.FieldType==typeof(long))actual=row.longInteger;else if(field.FieldType==typeof(float))actual=row.real;else if(field.FieldType==typeof(double))actual=row.doubleReal;else if(field.FieldType==typeof(bool))actual=row.flag;else if(field.FieldType==typeof(Vector2))actual=row.vector;
                    else if(field.FieldType==typeof(string)){Check(row.text.isNull==(source==null)&&row.text.value==((string)source??""),"field:"+prefix+"."+row.name);fields.Add(prefix+"."+row.name);continue;}
                    else throw new InvalidOperationException("SNAPSHOT_UNIT_SCALAR_TYPE");
                    Check(row.kind==field.FieldType.FullName&&Equals(source,actual),"field:"+prefix+"."+row.name);fields.Add(prefix+"."+row.name);
                }
            }
            Check(initialSourceBefore.Same(initialSourceAfter),"initial-capture-source-unchanged");
            Check(Team.Count==2&&Team.All(s=>s.items.Count>0),"fixture-two-survivors-with-gear");
            uid=30000;enemyUid=100000;burningSpawned=3;bomberSpawnCount=4;spawnClock=.45f;recycleCooldown=.65f;burningAngle=.85f;bomberSpawnAngle=1.05f;
            RealBattle=12.5f;RealPrep=2.25f;RealPaused=.75f;WaveReal=3.75f;WavePaused=.125f;LobbyTime=5.25f;ResultTime=1.125f;Elapsed=7.5f;TotalTime=10.25f;Kills=3;Spawned=4;WaveKills=2;TestControlsUsed=true;MoveInput=new Vector2(.6f,-.8f);ViewWidth=1000;ViewHeight=600;mapSearches=7;mapSpawnSearches=9;mapEnemyCursor=11;MapSpawnFailures=17;
            Team[0].pos=new Vector2(1.25f,-2.5f);Team[0].healClock=1.1f;Team[0].shieldClock=2.2f;Team[0].flash=.3f;Team[0].shareClock=4.4f;Team[0].shareProvider=1;Team[0].sharePeriod=5.5f;
            var gear=Team[0].items[0];gear.cd=1.25f;gear.lastPeriod=3.75f;gear.activationFlash=.15f;
            Stash.Add(new Gear{id=20001,grade=2,x=2,y=3,rotation=1,type=gear.type,cd=.6f,lastPeriod=2.5f,activationFlash=.4f});
            var enemy=new Enemy{id=10001,type="melee",phase="rush",pos=new Vector2(11.25f,-13.5f),charge=new Vector2(.5f,-.75f),flock=new Vector2(2.5f,3.25f),hp=12.5f,maxhp=20.25f,damage=3.5f,cd=.7f,flash=.8f,phaseTime=.9f,rushTime=1.1f,offscreenTime=1.2f};enemy.dashHits.Add(Team[1]);enemy.dashHits.Add(Team[0]);Enemies.Add(enemy);
            var secondEnemy=new Enemy{id=10000,type="melee",pos=new Vector2(-9,7),hp=9,maxhp=11,damage=2};Enemies.Add(secondEnemy);
            var ghost=new Enemy{id=10002,type="charger",phase="charge",pos=new Vector2(-21,22),hp=0,maxhp=17,damage=4};
            mapRoutes[ghost]=new EnemyRoute{points=new List<Vector2>{new Vector2(2,3),new Vector2(5,7)},index=1,target=new Vector2(11,13),retry=2.75f};
            var shot=new Shot{type="arrow",pos=new Vector2(1,3),dir=new Vector2(.6f,.8f),target=new Vector2(7,9),speed=123.5f,damage=12.25f,range=150.5f,travel=27.75f,dead=true,owner=Team[1],pierce=2,radius=3.25f};shot.hits.Add(90000);shot.hits.Add(enemy.id);Shots.Add(shot);
            Shots.Add(new Shot{type="enemy",owner=null,pos=new Vector2(-1,-3),speed=17,radius=2});
            Effects.Add(new Effect{pos=new Vector2(4.25f,8.5f),life=.3f,max=.9f,r=17.5f,color=null,text=""});Effects.Add(new Effect{pos=new Vector2(-3,-5),life=.2f,max=.4f,r=2,color="",text=null});
            History.Add(new WaveRecord{wave=1,kills=5,survivors=2,formation=0,time=7.75f,realBattle=1.25f,prepTime=2.5f,pausedTime=.75f,skipped=true,defeated=false,revived=new List<string>{"alpha","beta"}});
            var nav=Navigator(10);nav.PrepareSnapshotUnitCache();
            Check(EnqueueMapCommand("snapshot-pending",MapCommandNonce,"Pause",0,"TEST"),"fixture-pending-command");
            Check(!EnqueueMapCommand("snapshot-invalid","wrong","Resume",42,"TEST"),"fixture-invalid-event");
            var baselineSourceBefore=DSFullSourceWitness.Capture(this);var baseline=CaptureFullMapSnapshot();var baselineSourceAfter=DSFullSourceWitness.Capture(this);
            Check(baselineSourceBefore.Same(baselineSourceAfter),"baseline-capture-source-unchanged");
            string text=JsonUtility.ToJson(baseline);
            Check(text==JsonUtility.ToJson(CaptureFullMapSnapshot()),"capture-is-pure-and-repeatable");
            SameScalars(baseline.game,"Game");SameScalars(baseline.renderMetadata,"Render");SameScalars(baseline.protocol.scalars,"Protocol");
            for(int i=0;i<Team.Count;i++)
            {
                SameFields(Team[i],baseline.team[i],"Survivor"+i,"items");
                Check(baseline.team[i].slot==i&&baseline.team[i].reserved==Center+Offset(i),"survivor-slot-reference:"+i);
                for(int j=0;j<Team[i].items.Count;j++)SameFields(Team[i].items[j],baseline.team[i].items[j],"Gear"+i+"-"+j);
            }
            for(int i=0;i<Stash.Count;i++)SameFields(Stash[i],baseline.stash[i],"Stash"+i);
            SameFields(enemy,baseline.enemyPool.Single(e=>e.id==enemy.id),"Enemy","dashHits");SameFields(secondEnemy,baseline.enemyPool.Single(e=>e.id==secondEnemy.id),"SecondEnemy","dashHits");SameFields(ghost,baseline.enemyPool.Single(e=>e.id==ghost.id),"GhostEnemy","dashHits");
            Check(baseline.liveEnemyIds.SequenceEqual(new[]{enemy.id,secondEnemy.id})&&baseline.enemyPool.Length==3,"live-and-ghost-membership");
            Check(baseline.enemyPool.Single(e=>e.id==enemy.id).dashHitSlots.SequenceEqual(new[]{0,1}),"dash-hit-slot-reference");
            for(int i=0;i<Shots.Count;i++)
            {SameFields(Shots[i],baseline.shots[i],"Shot"+i,"owner","hits");Check(baseline.shots[i].ownerSlot==(Shots[i].owner==null?-1:Team.IndexOf(Shots[i].owner)),"shot-owner-reference:"+i);Check(baseline.shots[i].hits.SequenceEqual(Shots[i].hits.OrderBy(x=>x)),"historical-shot-hit-set:"+i);}
            for(int i=0;i<Effects.Count;i++)SameFields(Effects[i],baseline.effects[i],"Effect"+i);
            SameFields(History[0],baseline.history[0],"History","revived");Check(History[0].revived.SequenceEqual(baseline.history[0].revived.Select(t=>t.value)),"history-strings-copy");
            SameFields(Config,baseline.config,"Config","weights","waves");
            for(int i=0;i<Config.weights.Length;i++)SameFields(Config.weights[i],baseline.config.weights[i],"Weight"+i);
            for(int i=0;i<Config.waves.Length;i++){SameFields(Config.waves[i],baseline.config.waves[i],"Wave"+i,"types");Check(Config.waves[i].types.SequenceEqual(baseline.config.waves[i].types.Select(t=>t.value)),"wave-types-copy:"+i);}
            SameFields(mapRoutes[ghost],baseline.routes.Single(),"Route","points");
            Check(baseline.routes.Length==1&&baseline.routes[0].enemyId==ghost.id&&baseline.routes[0].points.SequenceEqual(mapRoutes[ghost].points)&&!ReferenceEquals(baseline.routes[0].points,mapRoutes[ghost].points),"ghost-route-copy");
            Check(baseline.navigation.Length==1&&baseline.navigation[0].nodes.Length==2&&baseline.navigation[0].edges.Length==1&&baseline.navigation[0].lastVisited==7,"navigation-cache-copy");
            SameFields(mapQueue.Peek(),baseline.protocol.pending.Single(),"PendingCommand");SameFields(mapQueueEvents.Single(),baseline.protocol.events.Single(),"CommandEvent");
            Check(baseline.protocol.knownCommandIds.Select(t=>t.value).SequenceEqual(new[]{"snapshot-pending"}),"queue-known-id-copy");
            Check(baseline.rng.seed==((DSMapTraceRandom)Rng).Seed&&baseline.rng.count==((DSMapTraceRandom)Rng).Count&&baseline.rng.traceHash==((DSMapTraceRandom)Rng).TraceHash,"rng-prefix-identity");
            // Deliberately mutate every mutable collection family after capture.
            enemy.pos+=new Vector2(3,4);gear.cd+=5;Stash[0].x+=1;Team[0].cells[0]=!Team[0].cells[0];Config.weights[0].weight+=2;Config.waves[0].types[0]="changed";History[0].revived.Add("changed");mapRoutes[ghost].points.Add(new Vector2(17,19));nav.MutateSnapshotUnitCache();Rng.Next();shot.hits.Add(99999);enemy.dashHits.Clear();Effects[0].text="changed";mapQueue.Peek().value+=1;mapQueueEvents[0].result="changed";mapQueueIds.Add("snapshot-more-id");
            string frozenAfter=JsonUtility.ToJson(baseline);Check(frozenAfter==text,"all-nested-values-detached-from-source");
            var changed=CaptureFullMapSnapshot();Check(JsonUtility.ToJson(changed)!=text,"new-capture-observes-source-change");
            void Reject(string id,Action mutate,Action restore,string expected)
            {
                string before=JsonUtility.ToJson(CaptureFullMapSnapshot());
                try
                {
                    mutate();bool caught=false;
                    try{CaptureFullMapSnapshot();}catch(InvalidOperationException e){caught=e.Message.Contains(expected);}
                    Check(caught,"reject:"+id);
                }
                finally{restore();}
                Check(before==JsonUtility.ToJson(CaptureFullMapSnapshot()),"restore:"+id);
            }
            Reject("gear-alias",()=>Stash.Add(gear),()=>Stash.RemoveAt(Stash.Count-1),"GEAR_ALIAS_OR_ID");
            var duplicate=new Gear{id=gear.id,type=gear.type};Reject("gear-id",()=>Stash.Add(duplicate),()=>Stash.RemoveAt(Stash.Count-1),"GEAR_ALIAS_OR_ID");
            Reject("team-alias",()=>Team.Add(Team[0]),()=>Team.RemoveAt(Team.Count-1),"DUPLICATE_TEAM_REFERENCE");
            Reject("live-enemy-alias",()=>Enemies.Add(enemy),()=>Enemies.RemoveAt(Enemies.Count-1),"DUPLICATE_LIVE_ENEMY_REFERENCE");
            var collision=new Enemy{id=enemy.id,type="melee"};Reject("enemy-id",()=>Enemies.Add(collision),()=>Enemies.Remove(collision),"ENEMY_ID_COLLISION");
            var foreign=new Survivor();Reject("unknown-owner",()=>shot.owner=foreign,()=>shot.owner=Team[1],"UNKNOWN_SURVIVOR_REFERENCE");
            Reject("unknown-dash-hit",()=>enemy.dashHits.Add(foreign),()=>enemy.dashHits.Remove(foreign),"UNKNOWN_SURVIVOR_REFERENCE");
            var originalLootRng=LootRng;Reject("rng-alias",()=>LootRng=Rng,()=>LootRng=originalLootRng,"RNG_ALIAS");
            var originalRng=Rng;Reject("untraced-rng",()=>Rng=new System.Random(11),()=>Rng=originalRng,"UNREGISTERED_RNG");
            float originalSpeed=Data.settings.speed;Reject("content-mutation",()=>Data.settings.speed=originalSpeed+1,()=>Data.settings.speed=originalSpeed,"REFERENCE_CHANGED");
            float originalLife=Effects[0].life;Reject("nonfinite",()=>Effects[0].life=float.NaN,()=>Effects[0].life=originalLife,"NONFINITE");
            var derived=new SnapshotUnitDerivedGear{id=777,type=gear.type};Reject("derived-gear",()=>Stash.Add(derived),()=>Stash.RemoveAt(Stash.Count-1),"SOURCE_TYPE:Gear");
            Reject("reentrant",()=>fullSnapshotContext.capturing=true,()=>fullSnapshotContext.capturing=false,"REENTRANT");
            bool threadRejected=Task.Run(()=>{try{CaptureFullMapSnapshot();return false;}catch(InvalidOperationException e){return e.Message=="FULL_SNAPSHOT_THREAD_OR_REGISTRATION";}}).GetAwaiter().GetResult();Check(threadRejected,"wrong-thread-rejected-before-unity-access");
            int effectCount=Effects.Count;Reject("effect-limit",()=>{for(int i=Effects.Count;i<8193;i++)Effects.Add(new Effect());},()=>Effects.RemoveRange(effectCount,Effects.Count-effectCount),"LIMIT:effects");
            int selectedCount=Selected.Count;Reject("selected-limit",()=>{while(Selected.Count<5)Selected.Add(0);},()=>Selected.RemoveRange(selectedCount,Selected.Count-selectedCount),"LIMIT:selected");
            var originalEffects=Effects.ToArray();string escapedText=new string('\0',1024);
            Reject("escaped-output-limit",()=>{Effects.Clear();for(int i=0;i<8192;i++)Effects.Add(new Effect{text=escapedText});},()=>{Effects.Clear();Effects.AddRange(originalEffects);},"RESERVED_OUTPUT_LIMIT");
            var oldHits0=Shots[0].hits;var oldHits1=Shots[1].hits;Reject("aggregate-shot-hits",()=>{Shots[0].hits=new HashSet<int>(Enumerable.Range(0,32769));Shots[1].hits=new HashSet<int>(Enumerable.Range(0,32769));},()=>{Shots[0].hits=oldHits0;Shots[1].hits=oldHits1;},"LIMIT:all-historical-shot-hits");
            Reject("shot-alias",()=>Shots.Add(shot),()=>Shots.RemoveAt(Shots.Count-1),"SHOT_ALIAS");
            var effectAlias=Effects[0];Reject("effect-alias",()=>Effects.Add(effectAlias),()=>Effects.RemoveAt(Effects.Count-1),"EFFECT_ALIAS");
            var oldData=Data;var oldItems=Items;var oldMonsters=Monsters;var oldEquipment=Equipment;
            Reject("identical-content-replacement",()=>{Data=JsonUtility.FromJson<PrototypeData>(JsonUtility.ToJson(Data));Items=Data.items.ToDictionary(d=>d.id);Monsters=Data.monsters.ToDictionary(d=>d.id);Equipment=new EquipmentRules(Data);},()=>{Data=oldData;Items=oldItems;Monsters=oldMonsters;Equipment=oldEquipment;},"REFERENCE_CHANGED");
            Reject("identical-items-dictionary",()=>Items=new Dictionary<string,ItemDef>(Items),()=>Items=oldItems,"REFERENCE_CHANGED");
            Reject("identical-equipment",()=>Equipment=new EquipmentRules(Data),()=>Equipment=oldEquipment,"REFERENCE_CHANGED");
            var oldSettings=Data.settings;Reject("identical-nested-settings",()=>Data.settings=JsonUtility.FromJson<RunSettings>(JsonUtility.ToJson(Data.settings)),()=>Data.settings=oldSettings,"REFERENCE_CHANGED");
            var oldArray=Data.items;Reject("identical-nested-array",()=>Data.items=(ItemDef[])Data.items.Clone(),()=>Data.items=oldArray,"REFERENCE_CHANGED");
            var oldLayout=MapSession.Layout;TextAsset layoutCopy=null;
            Reject("identical-layout-object",()=>{layoutCopy=new TextAsset(oldLayout.text);MapSession.Layout=layoutCopy;},()=>{MapSession.Layout=oldLayout;if(layoutCopy)UnityEngine.Object.DestroyImmediate(layoutCopy);},"REFERENCE_CHANGED");
            var savedRng=Rng;var savedLootRng=LootRng;
            try{Rng=new DSMapTraceRandom(71);LootRng=new DSMapTraceRandom(71);var pair=CaptureFullMapSnapshot();Check(pair.rng.seed==pair.lootRng.seed&&pair.rng.count==0&&pair.lootRng.count==0,"independent-identical-seed-rng-allowed");}finally{Rng=savedRng;LootRng=savedLootRng;}
            var originalShotHits=Shots[1].hits;Reject("shot-hit-set-alias",()=>Shots[1].hits=Shots[0].hits,()=>Shots[1].hits=originalShotHits,"MUTABLE_ALIAS:shot-hits");
            var originalCells=Team[1].cells;Reject("survivor-cells-alias",()=>Team[1].cells=Team[0].cells,()=>Team[1].cells=originalCells,"MUTABLE_ALIAS:cells");
            var originalTypes=Config.waves[1].types;Reject("config-array-alias",()=>Config.waves[1].types=Config.waves[0].types,()=>Config.waves[1].types=originalTypes,"MUTABLE_ALIAS:config");
            var sharedHistory=History[0];Reject("history-alias",()=>History.Add(sharedHistory),()=>History.RemoveAt(History.Count-1),"MUTABLE_ALIAS:history-record");
            var random=new DSMapTraceRandom(-17);var golden=new System.Random(-17);
            Check(random.Next()==golden.Next()&&random.Next(17)==golden.Next(17)&&random.Next(-3,7)==golden.Next(-3,7)&&random.NextDouble()==golden.NextDouble(),"rng-supported-api-golden");random.ValidateTrace();string trace=random.TraceHash;
            var exported=random.Calls;exported[0].response="mutated";random.ValidateTrace();Check(random.TraceHash==trace,"rng-export-does-not-alias");
            bool byteRejected=false,spanRejected=false;try{random.NextBytes(new byte[4]);}catch(InvalidOperationException){byteRejected=true;}try{random.NextBytes(new Span<byte>(new byte[4]));}catch(InvalidOperationException){spanRejected=true;}Check(byteRejected&&spanRejected&&random.TraceHash==trace,"rng-unsupported-api-does-not-advance");
            Check(random.Next()==golden.Next(),"rng-rejected-api-preserves-internal-state");
            return new DSFullSnapshotUnitRaw{initialSourceBefore=initialSourceBefore.Hash,initialSourceAfter=initialSourceAfter.Hash,baselineSourceBefore=baselineSourceBefore.Hash,baselineSourceAfter=baselineSourceAfter.Hash,sourceReferencesUnchanged=initialSourceBefore.Same(initialSourceAfter)&&baselineSourceBefore.Same(baselineSourceAfter),initial=JsonUtility.ToJson(initial),baseline=text,afterSourceMutation=JsonUtility.ToJson(changed),frozenBefore=text,frozenAfter=frozenAfter,checks=checks.ToArray(),checkedFields=fields.ToArray()};
        }
    }
}
#endif
