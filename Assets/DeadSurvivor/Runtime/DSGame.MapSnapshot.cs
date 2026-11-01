#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
namespace DeadSurvivor
{
    [Serializable] public sealed class DSFullText {public bool isNull;public string value;public static DSFullText Copy(string value)=>new DSFullText{isNull=value==null,value=value??""};}
    [Serializable] public sealed class DSFullScalar {public string name,kind;public int integer;public long longInteger;public float real;public double doubleReal;public bool flag;public Vector2 vector;public DSFullText text;}
    [Serializable] public sealed class DSFullGear {public int id,grade,x,y,rotation;public DSFullText type;public float cd,lastPeriod,activationFlash;}
    [Serializable] public sealed class DSFullSurvivor {public int slot,index,shareProvider;public float hp,maxhp,shield,damageTaken,healingReceived,aliveTime,healClock,shieldClock,flash,shareClock,sharePeriod;public Vector2 pos,reserved;public float radius;public bool[] cells;public DSFullGear[] items;}
    [Serializable] public sealed class DSFullEnemy {public int id;public DSFullText type,phase;public Vector2 pos,charge,flock;public float hp,maxhp,damage,cd,flash,phaseTime,rushTime,offscreenTime;public int[] dashHitSlots;}
    [Serializable] public sealed class DSFullShot {public DSFullText type;public Vector2 pos,dir,target;public float speed,damage,range,travel,radius;public bool dead;public int ownerSlot,pierce;public int[] hits;}
    [Serializable] public sealed class DSFullEffect {public Vector2 pos;public float life,max,r;public DSFullText color,text;}
    [Serializable] public sealed class DSFullHistory {public int wave,kills,survivors,formation;public float time,realBattle,prepTime,pausedTime;public bool skipped,defeated;public DSFullText[] revived;}
    [Serializable] public sealed class DSFullWeight {public DSFullText id;public float weight;}
    [Serializable] public sealed class DSFullWave {public float duration,hp,damage;public int drops,count;public DSFullText[] types;}
    [Serializable] public sealed class DSFullConfig {public bool adaptiveDrops;public float duration,speed,countScale;public int drops;public DSFullWeight[] weights;public DSFullWave[] waves;}
    [Serializable] public sealed class DSFullRandom {public int seed,count;public string traceHash;public DSMapRandomCall[] calls;}
    [Serializable] public sealed class DSFullRoute {public int enemyId,index;public Vector2 target;public float retry;public bool hasPoints;public Vector2[] points;}
    [Serializable] public sealed class DSFullNode {public Vector2Int point;public bool safe;}
    [Serializable] public sealed class DSFullEdge {public Vector2Int a,b;public bool safe;}
    [Serializable] public sealed class DSFullNavigation {public float radius;public int lastVisited;public DSFullNode[] nodes;public DSFullEdge[] edges;}
    [Serializable] public sealed class DSFullQueued {public DSFullText id,nonce,kind,source;public int value,sequence,enqueueFrame;}
    [Serializable] public sealed class DSFullEvent {public DSFullText id,nonce,kind,source,result,before,after;public int value,sequence,enqueueFrame,consumeFrame;}
    [Serializable] public sealed class DSFullProtocol {public DSFullScalar[] scalars;public DSFullQueued[] pending;public DSFullText[] knownCommandIds;public DSFullEvent[] events;}
    [Serializable] public sealed class DSFullBinding {public string mapMode="MapRequired",layoutHash,sourceLayoutHash,sourceRole;public float imageScale,worldScale,bodyRadius,elevation;public double epsilon,coordinateLimit;public Vector2 startCenter;}
    [Serializable] public sealed class DSFullSnapshot
    {
        public int schema_version=1;
        public string document_role="FULL_MAP_SNAPSHOT_031",contentHash,geometryHash,comparisonPolicy="031/r001-game-protocol-render-separated";
        public DSFullBinding binding;
        public DSFullScalar[] game,renderMetadata;public int[] selected,liveEnemyIds;
        public DSFullSurvivor[] team;public DSFullGear[] stash;public DSFullEnemy[] enemyPool;public DSFullShot[] shots;public DSFullEffect[] effects;public DSFullHistory[] history;
        public DSFullConfig config;public DSFullRandom rng,lootRng;public DSFullRoute[] routes;public DSFullNavigation[] navigation;public DSFullProtocol protocol;
    }
    public sealed class DSFullSnapshotBudget
    {
        readonly Stopwatch clock=Stopwatch.StartNew();
        public const int MaximumBytes=32*1024*1024;
        int entries,textBytes,reservedBytes;
        public void Reserve(int bytes){Check();reservedBytes=checked(reservedBytes+bytes);if(reservedBytes>MaximumBytes)throw new InvalidOperationException("FULL_SNAPSHOT_RESERVED_OUTPUT_LIMIT");}
        public void AddText(string value,int maximum=4096){if(value==null)return;Count(value.Length,maximum,"text");textBytes=checked(textBytes+new UTF8Encoding(false,true).GetByteCount(value));if(textBytes>MaximumBytes)throw new InvalidOperationException("FULL_SNAPSHOT_TEXT_BYTES_LIMIT");}
        public void Check(){if(clock.Elapsed.TotalSeconds>2)throw new InvalidOperationException("FULL_SNAPSHOT_WALL_LIMIT");}
        public void Cache(int count){Check();entries=checked(entries+count);if(entries>100000)throw new InvalidOperationException("FULL_SNAPSHOT_CACHE_LIMIT");}
        public static void Count(int count,int limit,string name){if(count<0||count>limit)throw new InvalidOperationException("FULL_SNAPSHOT_LIMIT:"+name);}
        public static void Finite(float value){if(!DSMapGeometry.Finite(value))throw new InvalidOperationException("FULL_SNAPSHOT_NONFINITE");}
        public static void Finite(double value){if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidOperationException("FULL_SNAPSHOT_NONFINITE");}
        public static void Finite(Vector2 value){Finite(value.x);Finite(value.y);}
        public static void Text(string value){if(value==null)return;if(value.Length>4096)throw new InvalidOperationException("FULL_SNAPSHOT_TEXT_LIMIT");new UTF8Encoding(false,true).GetByteCount(value);}
    }
    sealed class DSFullSnapshotContext
    {
        public readonly int thread;
        public readonly string contentHash,sessionDataHash,layoutHash,bindingKey,geometryHash;
        public readonly DSMapSession session;public readonly DSMapGeometry geometry;
        public readonly DSUI ui;public readonly DSWorld world;
        public readonly PrototypeData data;public readonly Dictionary<string,ItemDef> items;public readonly Dictionary<string,MonsterDef> monsters;public readonly EquipmentRules equipment;public readonly TextAsset layout;public readonly DSBlockoutData sessionData;
        public readonly Dictionary<string,object> dataReferences,sessionReferences;
        public bool capturing;
        public DSFullSnapshotContext(DSGame game)
        {
            thread=Thread.CurrentThread.ManagedThreadId;session=game.MapSession;geometry=session.Geometry;ui=game.UI;world=game.World;
            data=game.Data;items=game.Items;monsters=game.Monsters;equipment=game.Equipment;layout=session.Layout;sessionData=session.Data;
            dataReferences=DSFullSnapshotContract.ContentReferences(data);sessionReferences=DSFullSnapshotContract.ContentReferences(sessionData);
            contentHash=DSFullSnapshotContract.ContentHash(game.Data);
            var original=JsonUtility.FromJson<PrototypeData>(Resources.Load<TextAsset>("DeadSurvivor/PrototypeData").text);
            if(contentHash!=DSFullSnapshotContract.ContentHash(original))throw new InvalidOperationException("FULL_SNAPSHOT_CONTENT_NOT_ORIGINAL");
            sessionDataHash=DSFullSnapshotContract.ContentHash(session.Data);layoutHash=DSMapBlockoutPreview.Hash(session.Layout.text);
            bindingKey=BindingKey(session);
            geometryHash=DSMapBlockoutPreview.Hash(DSFullSnapshotContract.ContentHash(geometry.Boundary)+DSFullSnapshotContract.ContentHash(geometry.Obstacles)+geometry.Epsilon.ToString("R",CultureInfo.InvariantCulture)+":"+geometry.CoordinateLimit.ToString("R",CultureInfo.InvariantCulture));
        }
        public static string BindingKey(DSMapSession value)=>string.Join("|",new[]{value.ImageScale.ToString("R",CultureInfo.InvariantCulture),value.Elevation.ToString("R",CultureInfo.InvariantCulture),value.StartCenter.x.ToString("R",CultureInfo.InvariantCulture),value.StartCenter.y.ToString("R",CultureInfo.InvariantCulture),value.ExpectedDataSha,value.CombatEnabled.ToString(),value.ViewCamera.GetInstanceID().ToString(CultureInfo.InvariantCulture),value.StaticGeometry.GetInstanceID().ToString(CultureInfo.InvariantCulture),value.RockHolder.GetInstanceID().ToString(CultureInfo.InvariantCulture),value.Visibility?value.Visibility.GetInstanceID().ToString(CultureInfo.InvariantCulture):"NULL",value.RockPlacementOffset.x.ToString("R",CultureInfo.InvariantCulture),value.RockPlacementOffset.y.ToString("R",CultureInfo.InvariantCulture),value.RockPlacementOffset.z.ToString("R",CultureInfo.InvariantCulture)});
    }
    public static class DSFullSnapshotContract
    {
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        // Exact name/type coverage: a new runtime field cannot silently disappear.
        public static void Cover(Type type,params (Type type,string names)[] groups)
        {
            var expected=new Dictionary<string,Type>();
            foreach(var group in groups)foreach(var name in group.names.Split(' ',StringSplitOptions.RemoveEmptyEntries))if(!expected.TryAdd(name,group.type))throw new InvalidOperationException("FULL_SNAPSHOT_DUPLICATE_CONTRACT");
            var fields=type.GetFields(Fields);
            if(fields.Length!=expected.Count||fields.Any(f=>!expected.TryGetValue(f.Name,out var t)||t!=f.FieldType))throw new InvalidOperationException("FULL_SNAPSHOT_FIELD_COVERAGE:"+type.Name);
        }
        static readonly HashSet<Type> ContentTypes=new(){typeof(PrototypeData),typeof(CharacterDef),typeof(ItemDef),typeof(MonsterDef),typeof(GradeDef),typeof(RunSettings),typeof(Weight),typeof(WaveDef),typeof(DSBlockoutData),typeof(DSBlockoutObject),typeof(DSBlockoutRoute)};
        public static Dictionary<string,object> ContentReferences(object root)
        {
            var result=new Dictionary<string,object>(StringComparer.Ordinal);int nodes=0;
            void Visit(object value,string path,int depth)
            {
                if(++nodes>100000||depth>32)throw new InvalidOperationException("FULL_SNAPSHOT_REFERENCE_GRAPH_LIMIT");
                if(value==null||value is string||value.GetType().IsValueType)return;
                if(!result.TryAdd(path,value))throw new InvalidOperationException("FULL_SNAPSHOT_REFERENCE_PATH");
                if(value is Array array){DSFullSnapshotBudget.Count(array.Length,16384,"reference-array");for(int i=0;i<array.Length;i++)Visit(array.GetValue(i),path+"["+i+"]",depth+1);}
                else if(ContentTypes.Contains(value.GetType()))foreach(var field in value.GetType().GetFields(Fields).OrderBy(f=>f.Name,StringComparer.Ordinal))Visit(field.GetValue(value),path+"."+field.Name,depth+1);
                else throw new InvalidOperationException("FULL_SNAPSHOT_REFERENCE_TYPE");
            }
            Visit(root,"root",0);return result;
        }
        public static bool SameReferences(Dictionary<string,object> frozen,object current)
        {
            int count=0;
            bool Visit(object value,string path,int depth)
            {
                if(depth>32)throw new InvalidOperationException("FULL_SNAPSHOT_REFERENCE_DEPTH");
                if(value==null||value is string||value.GetType().IsValueType)return true;
                if(++count>100000)throw new InvalidOperationException("FULL_SNAPSHOT_REFERENCE_GRAPH_LIMIT");
                if(!frozen.TryGetValue(path,out var expected)||!ReferenceEquals(expected,value))return false;
                if(value is Array array){for(int i=0;i<array.Length;i++)if(!Visit(array.GetValue(i),path+"["+i+"]",depth+1))return false;}
                else if(ContentTypes.Contains(value.GetType())){foreach(var field in value.GetType().GetFields(Fields).OrderBy(f=>f.Name,StringComparer.Ordinal))if(!Visit(field.GetValue(value),path+"."+field.Name,depth+1))return false;}
                else return false;
                return true;
            }
            return Visit(current,"root",0)&&count==frozen.Count;
        }
        public static string ContentHash(object value)
        {
            var buffer=new StringBuilder();int nodes=0;
            void Write(object item,int depth)
            {
                if(++nodes>100000||depth>32||buffer.Length>DSFullSnapshotBudget.MaximumBytes)throw new InvalidOperationException("FULL_SNAPSHOT_CONTENT_LIMIT");
                if(item==null){buffer.Append("N;");return;}
                var type=item.GetType();buffer.Append(type.FullName).Append(':');
                if(item is string text){DSFullSnapshotBudget.Text(text);buffer.Append(text.Length).Append(':').Append(text);}
                else if(item is float number){DSFullSnapshotBudget.Finite(number);buffer.Append(number.ToString("R",CultureInfo.InvariantCulture));}
                else if(item is double wide){DSFullSnapshotBudget.Finite(wide);buffer.Append(wide.ToString("R",CultureInfo.InvariantCulture));}
                else if(item is int integer)buffer.Append(integer.ToString(CultureInfo.InvariantCulture));
                else if(item is bool flag)buffer.Append(flag?'1':'0');
                else if(item is Vector2 point){Write(point.x,depth+1);Write(point.y,depth+1);}
                else if(item is Vector2Int grid){Write(grid.x,depth+1);Write(grid.y,depth+1);}
                else if(item is Array array){DSFullSnapshotBudget.Count(array.Length,16384,"content-array");buffer.Append(array.Length).Append('[');foreach(var child in array)Write(child,depth+1);buffer.Append(']');}
                else if(ContentTypes.Contains(type))
                {foreach(var field in type.GetFields(Fields).OrderBy(f=>f.Name,StringComparer.Ordinal)){buffer.Append(field.Name.Length).Append(':').Append(field.Name);Write(field.GetValue(item),depth+1);}}
                else throw new InvalidOperationException("FULL_SNAPSHOT_CONTENT_TYPE:"+type.Name);
                buffer.Append(';');
            }
            Write(value,0);return DSMapBlockoutPreview.Hash(buffer.ToString());
        }
        public static DSFullConfig Config(RunSettings value)
        {
            if(value==null||value.weights==null||value.waves==null)throw new InvalidOperationException("FULL_SNAPSHOT_CONFIG_NULL");
            DSFullSnapshotBudget.Count(value.weights.Length,256,"weights");DSFullSnapshotBudget.Count(value.waves.Length,128,"waves");
            int totalTypes=0;
            return new DSFullConfig{adaptiveDrops=value.adaptiveDrops,duration=value.duration,speed=value.speed,countScale=value.countScale,drops=value.drops,
                weights=value.weights.Select(w=>{if(w==null||w.GetType()!=typeof(Weight))throw new InvalidOperationException("FULL_SNAPSHOT_WEIGHT_TYPE");return new DSFullWeight{id=DSFullText.Copy(w.id),weight=w.weight};}).ToArray(),
                waves=value.waves.Select(w=>{if(w==null||w.GetType()!=typeof(WaveDef)||w.types==null)throw new InvalidOperationException("FULL_SNAPSHOT_WAVE_NULL_OR_TYPE");DSFullSnapshotBudget.Count(w.types.Length,8192,"wave-types");totalTypes=checked(totalTypes+w.types.Length);DSFullSnapshotBudget.Count(totalTypes,16384,"all-wave-types");return new DSFullWave{duration=w.duration,hp=w.hp,damage=w.damage,drops=w.drops,count=w.count,types=w.types.Select(DSFullText.Copy).ToArray()};}).ToArray()};
        }
        public static void ValidateValue(object value,DSFullSnapshotBudget budget,int depth=0)
        {
            budget.Check();if(value==null){budget.Reserve(4);return;}if(depth>32)throw new InvalidOperationException("FULL_SNAPSHOT_DEPTH_LIMIT");
            if(value is float f){DSFullSnapshotBudget.Finite(f);budget.Reserve(192);return;}if(value is double d){DSFullSnapshotBudget.Finite(d);budget.Reserve(1152);return;}
            if(value is Vector2 p){DSFullSnapshotBudget.Finite(p);budget.Reserve(416);return;}if(value is Vector2Int){budget.Reserve(80);return;}if(value is int||value is long){budget.Reserve(24);return;}if(value is bool){budget.Reserve(5);return;}
            if(value is string text){budget.AddText(text);budget.Reserve(checked(text.Length*6+2));return;}
            if(value is DSFullEvent commandEvent)
            {
                budget.Reserve(2+typeof(DSFullEvent).GetFields(BindingFlags.Public|BindingFlags.Instance).Sum(f=>f.Name.Length*6+4));
                foreach(var field in typeof(DSFullEvent).GetFields(BindingFlags.Public|BindingFlags.Instance))
                {
                    if(field.Name=="before"||field.Name=="after"){var textValue=(DSFullText)field.GetValue(commandEvent);if(textValue==null)throw new InvalidOperationException("FULL_SNAPSHOT_EVENT_TEXT_NULL");budget.AddText(textValue.value,1024*1024);budget.Reserve(checked(128+(textValue.value?.Length??0)*6+2));}
                    else ValidateValue(field.GetValue(commandEvent),budget,depth+1);
                }
                return;
            }
            if(value is Array rows){budget.Reserve(checked(rows.Length+2));foreach(var row in rows)ValidateValue(row,budget,depth+1);return;}
            var type=value.GetType();
            if(!type.Name.StartsWith("DSFull",StringComparison.Ordinal)&&type!=typeof(DSMapRandomCall)&&type!=typeof(DSMapQueuedCommand)&&type!=typeof(DSMapCommandEvent))throw new InvalidOperationException("FULL_SNAPSHOT_DTO_REFERENCE:"+type.Name);
            budget.Reserve(2+type.GetFields(BindingFlags.Public|BindingFlags.Instance).Sum(f=>f.Name.Length*6+4));
            foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))ValidateValue(field.GetValue(value),budget,depth+1);
        }
    }
    public sealed partial class DSMapNavigation
    {
        internal object[] FullSnapshotCacheIdentities()=>new object[]{nodes,edges};
        internal int FullSnapshotCacheCount=>checked(nodes.Count+edges.Count);
        internal DSFullNavigation CaptureFullNavigation(DSMapGeometry expected,DSFullSnapshotBudget budget)
        {
            DSFullSnapshotContract.Cover(typeof(DSMapNavigation),(typeof(DSMapGeometry),"map"),(typeof(float),"radius"),(typeof(Dictionary<Vector2Int,bool>),"nodes"),(typeof(Dictionary<(Vector2Int,Vector2Int),bool>),"edges"),(typeof(int),"<LastVisited>k__BackingField"));
            if(!ReferenceEquals(map,expected))throw new InvalidOperationException("FULL_SNAPSHOT_NAV_GEOMETRY_REFERENCE");
            budget.Check();
            return new DSFullNavigation{radius=radius,lastVisited=LastVisited,
                nodes=nodes.OrderBy(p=>p.Key.x).ThenBy(p=>p.Key.y).Select(p=>new DSFullNode{point=p.Key,safe=p.Value}).ToArray(),
                edges=edges.OrderBy(p=>p.Key.Item1.x).ThenBy(p=>p.Key.Item1.y).ThenBy(p=>p.Key.Item2.x).ThenBy(p=>p.Key.Item2.y).Select(p=>new DSFullEdge{a=p.Key.Item1,b=p.Key.Item2,safe=p.Value}).ToArray()};
        }
    }
    public partial class DSGame
    {
        DSFullSnapshotContext fullSnapshotContext;
        const string FullGameNames="Formation PendingFormation RealBattle RealPrep RealPaused WaveReal WavePaused LobbyTime ResultTime TestControlsUsed VerificationRun mapSearches mapSpawnSearches mapEnemyCursor <MapSpawnFailures>k__BackingField mapRequired State Paused Win Wave Kills Spawned WaveKills Elapsed TotalTime Center MoveInput uid enemyUid burningSpawned bomberSpawnCount spawnClock recycleCooldown burningAngle bomberSpawnAngle";
        const string FullRenderNames="ViewWidth ViewHeight InitialLoadTime initializationStarted <MapRejection>k__BackingField";
        const string FullProtocolNames="mapCommandPermit mapCommandConsuming mapQueueBootstrapUsed mapQueueNonce mapQueueError mapQueueSequence mapQueueConsumed <MapInputFrame>k__BackingField <MapInputDevice>k__BackingField <MapInputD>k__BackingField <MapInputObserved>k__BackingField mapUnitScope mapUnitRegisteredInstance mapUnitRegisteredOwner mapFramePermit fixtureFramePermit normalGameFrames normalGameSimulations";
        static void FullGameCoverage()
        {
            DSFullSnapshotContract.Cover(typeof(DSGame),
                (typeof(int),"Formation PendingFormation mapSearches mapSpawnSearches mapEnemyCursor <MapSpawnFailures>k__BackingField mapQueueSequence mapQueueConsumed <MapInputFrame>k__BackingField <MapInputDevice>k__BackingField mapUnitRegisteredInstance Wave Kills Spawned WaveKills uid enemyUid burningSpawned bomberSpawnCount"),
                (typeof(float),"RealBattle RealPrep RealPaused WaveReal WavePaused LobbyTime ResultTime Elapsed TotalTime ViewWidth ViewHeight spawnClock recycleCooldown burningAngle bomberSpawnAngle InitialLoadTime"),
                (typeof(double),"initializationStarted"),(typeof(long),"normalGameFrames normalGameSimulations"),
                (typeof(bool),"RequireMap TestControlsUsed VerificationRun mapRequired mapCommandPermit mapCommandConsuming mapQueueBootstrapUsed <MapInputD>k__BackingField mapUnitScope mapFramePermit fixtureFramePermit Paused Win"),
                (typeof(string),"mapQueueNonce mapQueueError mapUnitRegisteredOwner <MapRejection>k__BackingField State"),
                (typeof(Vector2),"<MapInputObserved>k__BackingField Center MoveInput"),
                (typeof(EquipmentRules),"Equipment"),(typeof(Dictionary<float,DSMapNavigation>),"mapNavigators"),(typeof(Dictionary<Enemy,EnemyRoute>),"mapRoutes"),
                (typeof(Queue<DSMapQueuedCommand>),"mapQueue"),(typeof(HashSet<string>),"mapQueueIds"),(typeof(List<DSMapCommandEvent>),"mapQueueEvents"),
                (typeof(GameObject),"mapUnitRegisteredRoot"),(typeof(Action),"mapCommandTestProbe"),(typeof(DSMapFrameBinding),"mapFrame"),(typeof(DSMovementFixture),"movementFixture"),(typeof(DSNormalMapObserver),"mapObserver"),
                (typeof(DSMapSession),"MapSession"),(typeof(PrototypeData),"Data"),(typeof(RunSettings),"Config"),(typeof(Dictionary<string,ItemDef>),"Items"),(typeof(Dictionary<string,MonsterDef>),"Monsters"),
                (typeof(List<int>),"Selected"),(typeof(List<Survivor>),"Team"),(typeof(List<Gear>),"Stash"),(typeof(List<Enemy>),"Enemies"),(typeof(List<Shot>),"Shots"),(typeof(List<Effect>),"Effects"),(typeof(List<WaveRecord>),"History"),
                (typeof(DSUI),"UI"),(typeof(DSWorld),"World"),(typeof(System.Random),"Rng LootRng"),(typeof(DSFullSnapshotContext),"fullSnapshotContext"));
            DSFullSnapshotContract.Cover(typeof(DSMapSession),(typeof(TextAsset),"Layout"),(typeof(string),"ExpectedDataSha"),(typeof(float),"ImageScale Elevation"),(typeof(Vector2),"StartCenter"),(typeof(Camera),"ViewCamera"),(typeof(Transform),"StaticGeometry RockHolder"),(typeof(Vector3),"RockPlacementOffset"),(typeof(DSMapVisibility),"Visibility"),(typeof(bool),"CombatEnabled"),(typeof(DSBlockoutData),"<Data>k__BackingField"),(typeof(DSMapGeometry),"<Geometry>k__BackingField"),(typeof(DSGame),"owner"),(typeof(DSMapBoundState),"bound"),(typeof(bool),"bindingAttempted"),(typeof(DSMapPendingFixture),"pendingFixture"));
            DSFullSnapshotContract.Cover(typeof(Gear),(typeof(int),"id grade x y rotation"),(typeof(string),"type"),(typeof(float),"cd lastPeriod activationFlash"));
            DSFullSnapshotContract.Cover(typeof(Survivor),(typeof(int),"index shareProvider"),(typeof(float),"hp maxhp shield damageTaken healingReceived aliveTime healClock shieldClock flash shareClock sharePeriod"),(typeof(Vector2),"pos"),(typeof(bool[]),"cells"),(typeof(List<Gear>),"items"));
            DSFullSnapshotContract.Cover(typeof(Enemy),(typeof(int),"id"),(typeof(string),"type phase"),(typeof(Vector2),"pos charge flock"),(typeof(float),"hp maxhp damage cd flash phaseTime rushTime offscreenTime"),(typeof(HashSet<Survivor>),"dashHits"));
            DSFullSnapshotContract.Cover(typeof(Shot),(typeof(string),"type"),(typeof(Vector2),"pos dir target"),(typeof(float),"speed damage range travel radius"),(typeof(bool),"dead"),(typeof(Survivor),"owner"),(typeof(int),"pierce"),(typeof(HashSet<int>),"hits"));
            DSFullSnapshotContract.Cover(typeof(Effect),(typeof(Vector2),"pos"),(typeof(float),"life max r"),(typeof(string),"color text"));
            DSFullSnapshotContract.Cover(typeof(DSMapQueuedCommand),(typeof(string),"id nonce kind source"),(typeof(int),"value sequence enqueueFrame"));
            DSFullSnapshotContract.Cover(typeof(DSMapCommandEvent),(typeof(string),"id nonce kind source result before after"),(typeof(int),"value sequence enqueueFrame consumeFrame"));
            DSFullSnapshotContract.Cover(typeof(EnemyRoute),(typeof(List<Vector2>),"points"),(typeof(int),"index"),(typeof(Vector2),"target"),(typeof(float),"retry"));
            DSFullSnapshotContract.Cover(typeof(RunSettings),(typeof(bool),"adaptiveDrops"),(typeof(float),"duration speed countScale"),(typeof(int),"drops"),(typeof(Weight[]),"weights"),(typeof(WaveDef[]),"waves"));
            DSFullSnapshotContract.Cover(typeof(Weight),(typeof(string),"id"),(typeof(float),"weight"));
            DSFullSnapshotContract.Cover(typeof(WaveDef),(typeof(float),"duration hp damage"),(typeof(int),"drops count"),(typeof(string[]),"types"));
            DSFullSnapshotContract.Cover(typeof(WaveRecord),(typeof(int),"wave kills survivors formation"),(typeof(float),"time realBattle prepTime pausedTime"),(typeof(bool),"skipped defeated"),(typeof(List<string>),"revived"));
        }
        DSFullScalar[] FullScalars(string names)
        {
            return names.Split(' ').OrderBy(x=>x,StringComparer.Ordinal).Select(name=>{
                var field=typeof(DSGame).GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);var value=field.GetValue(this);var row=new DSFullScalar{name=name,kind=field.FieldType.FullName,text=DSFullText.Copy(null)};
                if(value is int n)row.integer=n;else if(value is long l)row.longInteger=l;else if(value is float f)row.real=f;else if(value is double d)row.doubleReal=d;else if(value is bool flag)row.flag=flag;else if(value is Vector2 p)row.vector=p;else if(field.FieldType==typeof(string))row.text=DSFullText.Copy((string)value);else throw new InvalidOperationException("FULL_SNAPSHOT_SCALAR_TYPE");
                return row;
            }).ToArray();
        }

        void FullReferences(DSFullSnapshotBudget budget)
        {
            var context=fullSnapshotContext;
            if(!MapSession||!ReferenceEquals(Data,context.data)||!ReferenceEquals(Items,context.items)||!ReferenceEquals(Monsters,context.monsters)||!ReferenceEquals(Equipment,context.equipment)||!ReferenceEquals(MapSession.Layout,context.layout)||!ReferenceEquals(MapSession.Data,context.sessionData)||!DSFullSnapshotContract.SameReferences(context.dataReferences,Data)||!DSFullSnapshotContract.SameReferences(context.sessionReferences,MapSession.Data)||!ReferenceEquals(MapSession,context.session)||!ReferenceEquals(MapSession.Geometry,context.geometry)||!ReferenceEquals(UI,context.ui)||!ReferenceEquals(World,context.world)||!UI||!World||context.contentHash!=DSFullSnapshotContract.ContentHash(Data)||context.sessionDataHash!=DSFullSnapshotContract.ContentHash(MapSession.Data)||context.layoutHash!=DSMapBlockoutPreview.Hash(MapSession.Layout.text)||context.bindingKey!=DSFullSnapshotContext.BindingKey(MapSession))throw new InvalidOperationException("FULL_SNAPSHOT_REFERENCE_CHANGED");
            if(!ReferenceEquals(typeof(DSMapSession).GetField("owner",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(MapSession),this)||MapSession.Data.body_radius!=8)throw new InvalidOperationException("FULL_SNAPSHOT_BINDING_PROFILE");
            if(Items.Count!=Data.items.Length||Monsters.Count!=Data.monsters.Length||Data.items.Any(d=>!Items.TryGetValue(d.id,out var value)||!ReferenceEquals(value,d))||Data.monsters.Any(d=>!Monsters.TryGetValue(d.id,out var value)||!ReferenceEquals(value,d)))throw new InvalidOperationException("FULL_SNAPSHOT_DEFINITION_REFERENCE");
            DSFullSnapshotContract.Cover(typeof(EquipmentRules),(typeof(PrototypeData),"data"),(typeof(Dictionary<string,ItemDef>),"defs"));
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var equipmentData=typeof(EquipmentRules).GetField("data",flags).GetValue(Equipment);
            var equipmentDefs=(Dictionary<string,ItemDef>)typeof(EquipmentRules).GetField("defs",flags).GetValue(Equipment);
            if(!ReferenceEquals(equipmentData,Data)||equipmentDefs.Count!=Items.Count||Items.Any(d=>!equipmentDefs.TryGetValue(d.Key,out var value)||!ReferenceEquals(value,d.Value)))throw new InvalidOperationException("FULL_SNAPSHOT_EQUIPMENT_REFERENCE");
            budget.Check();
        }
        static T FullExact<T>(T value) where T:class
        {if(value==null||value.GetType()!=typeof(T))throw new InvalidOperationException("FULL_SNAPSHOT_SOURCE_TYPE:"+typeof(T).Name);return value;}
        public DSFullSnapshot CaptureFullMapSnapshot()
        {
            var context=fullSnapshotContext;
            if(GetType()!=typeof(DSGame))throw new InvalidOperationException("FULL_SNAPSHOT_GAME_SUBTYPE");
            if(context==null||Thread.CurrentThread.ManagedThreadId!=context.thread)throw new InvalidOperationException("FULL_SNAPSHOT_THREAD_OR_REGISTRATION");
            if(context.capturing)throw new InvalidOperationException("FULL_SNAPSHOT_REENTRANT");
            if(enabled||!mapRequired||!VerificationRun||!mapUnitScope||!mapCommandPermit||mapFrame!=null||movementFixture!=null||mapObserver!=null||!mapUnitRegisteredRoot||mapUnitRegisteredRoot!=transform.root.gameObject||mapUnitRegisteredInstance!=GetInstanceID()||UnityEditor.SessionState.GetString("DS.Map017.owner","")!=mapUnitRegisteredOwner||UnityEditor.SessionState.GetString("DS.Map017.phase","")!="PLAY"||UnityEditor.SessionState.GetInt("DS.Map017.pid",-1)!=System.Diagnostics.Process.GetCurrentProcess().Id)throw new InvalidOperationException("FULL_SNAPSHOT_OWNERSHIP");
            context.capturing=true;
            try
            {
                var budget=new DSFullSnapshotBudget();FullGameCoverage();
                if(MapSession.IsFixture||!MapSession.BodyProfile.IsDefault8)throw new InvalidOperationException("FULL_SNAPSHOT_UNSUPPORTED_PROFILE_031");
                FullReferences(budget);
                if(ReferenceEquals(Rng,LootRng))throw new InvalidOperationException("FULL_SNAPSHOT_RNG_ALIAS");
                DSFullSnapshotBudget.Count(Team.Count,4,"team");DSFullSnapshotBudget.Count(Selected.Count,4,"selected");DSFullSnapshotBudget.Count(mapNavigators.Count,256,"navigators");DSFullSnapshotBudget.Count(Stash.Count,256,"stash");DSFullSnapshotBudget.Count(Enemies.Count,4096,"enemies");DSFullSnapshotBudget.Count(mapRoutes.Count,4096,"routes");DSFullSnapshotBudget.Count(Shots.Count,8192,"shots");DSFullSnapshotBudget.Count(Effects.Count,8192,"effects");DSFullSnapshotBudget.Count(History.Count,128,"history");DSFullSnapshotBudget.Count(mapQueue.Count,32,"queue");DSFullSnapshotBudget.Count(mapQueueEvents.Count,128,"events");DSFullSnapshotBudget.Count(mapQueueIds.Count,256,"command-ids");
                var slots=new Dictionary<Survivor,int>(DSFullReferenceComparer<Survivor>.Instance);
                for(int i=0;i<Team.Count;i++)if(!slots.TryAdd(FullExact(Team[i]),i))throw new InvalidOperationException("FULL_SNAPSHOT_DUPLICATE_TEAM_REFERENCE");
                int Slot(Survivor survivor,bool nullable=false)
                {if(survivor==null&&nullable)return -1;if(survivor==null||!slots.TryGetValue(survivor,out var slot))throw new InvalidOperationException("FULL_SNAPSHOT_UNKNOWN_SURVIVOR_REFERENCE");return slot;}
                var gears=new HashSet<Gear>(DSFullReferenceComparer<Gear>.Instance);var gearIds=new HashSet<int>();int gearCount=0;
                DSFullGear GearCopy(Gear value)
                {
                    FullExact(value);if(!gears.Add(value)||!gearIds.Add(value.id))throw new InvalidOperationException("FULL_SNAPSHOT_GEAR_ALIAS_OR_ID");
                    DSFullSnapshotBudget.Count(++gearCount,256,"gear-total");
                    return new DSFullGear{id=value.id,grade=value.grade,x=value.x,y=value.y,rotation=value.rotation,type=DSFullText.Copy(value.type),cd=value.cd,lastPeriod=value.lastPeriod,activationFlash=value.activationFlash};
                }
                var pool=new List<Enemy>();var references=new HashSet<Enemy>(DSFullReferenceComparer<Enemy>.Instance);var enemyIds=new HashSet<int>();
                foreach(var enemy in Enemies){FullExact(enemy);if(!references.Add(enemy))throw new InvalidOperationException("FULL_SNAPSHOT_DUPLICATE_LIVE_ENEMY_REFERENCE");pool.Add(enemy);}
                foreach(var enemy in mapRoutes.Keys.OrderBy(e=>e.id))if(references.Add(FullExact(enemy)))pool.Add(enemy);
                DSFullSnapshotBudget.Count(pool.Count,4096,"live-and-ghost-enemies");
                foreach(var enemy in pool)if(!enemyIds.Add(enemy.id))throw new InvalidOperationException("FULL_SNAPSHOT_ENEMY_ID_COLLISION");
                int routePoints=0,shotHitCount=0;
                var shotReferences=new HashSet<Shot>(DSFullReferenceComparer<Shot>.Instance);var effectReferences=new HashSet<Effect>(DSFullReferenceComparer<Effect>.Instance);
                foreach(var shot in Shots){budget.Check();FullExact(shot);if(!shotReferences.Add(shot))throw new InvalidOperationException("FULL_SNAPSHOT_SHOT_ALIAS");if(shot.hits==null)throw new InvalidOperationException("FULL_SNAPSHOT_SHOT_SET_NULL");shotHitCount=checked(shotHitCount+shot.hits.Count);DSFullSnapshotBudget.Count(shotHitCount,65536,"all-historical-shot-hits");}
                foreach(var effect in Effects){budget.Check();FullExact(effect);if(!effectReferences.Add(effect))throw new InvalidOperationException("FULL_SNAPSHOT_EFFECT_ALIAS");}
                foreach(var route in mapRoutes.Values){budget.Check();if(route==null)throw new InvalidOperationException("FULL_SNAPSHOT_ROUTE_NULL");routePoints=checked(routePoints+(route.points?.Count??0));DSFullSnapshotBudget.Count(routePoints,65536,"route-points");}
                foreach(var navigator in mapNavigators.Values){budget.Check();if(navigator==null)throw new InvalidOperationException("FULL_SNAPSHOT_NAV_NULL");budget.Cache(navigator.FullSnapshotCacheCount);}

                // All mutable ownership edges are unique. Entity references carried
                // by owner/dash-hit/route-key are resolved explicitly, not traversed.
                var immutableReferences=new HashSet<object>(context.dataReferences.Values,DSFullReferenceComparer<object>.Instance);
                immutableReferences.UnionWith(context.sessionReferences.Values);
                var mutableReferences=new HashSet<object>(DSFullReferenceComparer<object>.Instance);
                void Mutable(object value,string label)
                {budget.Check();if(value!=null&&(!mutableReferences.Add(value)||immutableReferences.Contains(value)))throw new InvalidOperationException("FULL_SNAPSHOT_MUTABLE_ALIAS:"+label);}
                foreach(var value in DSFullSnapshotContract.ContentReferences(FullExact(Config)).Values)Mutable(value,"config");
                Mutable(Selected,"selected");Mutable(Team,"team");Mutable(Stash,"stash");Mutable(Enemies,"enemies");Mutable(Shots,"shots");Mutable(Effects,"effects");Mutable(History,"history");
                foreach(var survivor in Team){Mutable(survivor,"survivor");Mutable(survivor.cells,"cells");Mutable(survivor.items,"items");}
                foreach(var enemy in pool){Mutable(enemy,"enemy");Mutable(enemy.dashHits,"dash-hits");}
                foreach(var shot in Shots){Mutable(shot,"shot");Mutable(shot.hits,"shot-hits");}
                foreach(var effect in Effects)Mutable(effect,"effect");
                foreach(var history in History){FullExact(history);Mutable(history,"history-record");Mutable(history.revived,"revived");}
                Mutable(mapRoutes,"route-map");foreach(var route in mapRoutes.Values){Mutable(route,"route");Mutable(route.points,"route-points");}
                Mutable(mapNavigators,"navigators");foreach(var navigator in mapNavigators.Values){Mutable(navigator,"navigator");foreach(var cache in navigator.FullSnapshotCacheIdentities())Mutable(cache,"navigation-cache");}
                Mutable(mapQueue,"queue");Mutable(mapQueueIds,"command-ids");Mutable(mapQueueEvents,"command-events");foreach(var command in mapQueue)Mutable(command,"queued-command");foreach(var commandEvent in mapQueueEvents)Mutable(commandEvent,"command-event");
                var snapshot=new DSFullSnapshot{contentHash=context.contentHash,geometryHash=context.geometryHash,binding=new DSFullBinding{layoutHash=context.layoutHash,sourceLayoutHash=MapSession.Data.source_layout_sha256,sourceRole=MapSession.Data.document_role,imageScale=MapSession.ImageScale,worldScale=MapSession.Data.world_scale,bodyRadius=8,elevation=MapSession.Elevation,epsilon=context.geometry.Epsilon,coordinateLimit=context.geometry.CoordinateLimit,startCenter=MapSession.StartCenter},game=FullScalars(FullGameNames),renderMetadata=FullScalars(FullRenderNames),selected=Selected.ToArray(),liveEnemyIds=Enemies.Select(e=>e.id).ToArray(),
                    team=Team.Select((v,i)=>{
                        budget.Check();
                        if(v.cells==null||v.cells.Length!=36||v.items==null)throw new InvalidOperationException("FULL_SNAPSHOT_SURVIVOR_ARRAYS");DSFullSnapshotBudget.Count(v.items.Count,256,"survivor-gear");
                        return new DSFullSurvivor{slot=i,index=v.index,shareProvider=v.shareProvider,hp=v.hp,maxhp=v.maxhp,shield=v.shield,damageTaken=v.damageTaken,healingReceived=v.healingReceived,aliveTime=v.aliveTime,healClock=v.healClock,shieldClock=v.shieldClock,flash=v.flash,shareClock=v.shareClock,sharePeriod=v.sharePeriod,pos=v.pos,reserved=Center+Offset(i),radius=8,cells=(bool[])v.cells.Clone(),items=v.items.Select(GearCopy).ToArray()};}).ToArray(),stash=Stash.Select(GearCopy).ToArray(),
                    enemyPool=pool.Select(v=>{budget.Check();if(v.dashHits==null)throw new InvalidOperationException("FULL_SNAPSHOT_DASH_SET_NULL");DSFullSnapshotBudget.Count(v.dashHits.Count,4,"dash-hits");return new DSFullEnemy{id=v.id,type=DSFullText.Copy(v.type),phase=DSFullText.Copy(v.phase),pos=v.pos,charge=v.charge,flock=v.flock,hp=v.hp,maxhp=v.maxhp,damage=v.damage,cd=v.cd,flash=v.flash,phaseTime=v.phaseTime,rushTime=v.rushTime,offscreenTime=v.offscreenTime,dashHitSlots=v.dashHits.Select(v=>Slot(v)).OrderBy(v=>v).ToArray()};}).ToArray(),
                    shots=Shots.Select(v=>{budget.Check();FullExact(v);if(v.hits==null)throw new InvalidOperationException("FULL_SNAPSHOT_SHOT_SET_NULL");return new DSFullShot{type=DSFullText.Copy(v.type),pos=v.pos,dir=v.dir,target=v.target,speed=v.speed,damage=v.damage,range=v.range,travel=v.travel,dead=v.dead,ownerSlot=Slot(v.owner,true),pierce=v.pierce,radius=v.radius,hits=v.hits.OrderBy(v=>v).ToArray()};}).ToArray(),
                    effects=Effects.Select(v=>{budget.Check();FullExact(v);return new DSFullEffect{pos=v.pos,life=v.life,max=v.max,r=v.r,color=DSFullText.Copy(v.color),text=DSFullText.Copy(v.text)};}).ToArray(),
                    history=History.Select(v=>{budget.Check();FullExact(v);if(v.revived==null)throw new InvalidOperationException("FULL_SNAPSHOT_HISTORY_NULL");DSFullSnapshotBudget.Count(v.revived.Count,256,"revived");return new DSFullHistory{wave=v.wave,kills=v.kills,survivors=v.survivors,formation=v.formation,time=v.time,realBattle=v.realBattle,prepTime=v.prepTime,pausedTime=v.pausedTime,skipped=v.skipped,defeated=v.defeated,revived=v.revived.Select(DSFullText.Copy).ToArray()};}).ToArray(),
                    config=DSFullSnapshotContract.Config(FullExact(Config)),rng=FullRandom(Rng),lootRng=FullRandom(LootRng),
                    routes=mapRoutes.OrderBy(r=>r.Key.id).Select(p=>{var r=p.Value;if(r==null)throw new InvalidOperationException("FULL_SNAPSHOT_ROUTE_NULL");return new DSFullRoute{enemyId=p.Key.id,index=r.index,target=r.target,retry=r.retry,hasPoints=r.points!=null,points=r.points?.ToArray()??Array.Empty<Vector2>()};}).ToArray(),
                    navigation=mapNavigators.OrderBy(p=>p.Key).Select(p=>{var row=p.Value.CaptureFullNavigation(context.geometry,budget);if(row.radius!=p.Key)throw new InvalidOperationException("FULL_SNAPSHOT_NAV_RADIUS");return row;}).ToArray(),
                    protocol=new DSFullProtocol{scalars=FullScalars(FullProtocolNames),pending=mapQueue.Select(c=>{FullExact(c);return new DSFullQueued{id=DSFullText.Copy(c.id),nonce=DSFullText.Copy(c.nonce),kind=DSFullText.Copy(c.kind),source=DSFullText.Copy(c.source),value=c.value,sequence=c.sequence,enqueueFrame=c.enqueueFrame};}).ToArray(),knownCommandIds=mapQueueIds.OrderBy(x=>x,StringComparer.Ordinal).Select(DSFullText.Copy).ToArray(),
                        events=mapQueueEvents.Select(c=>{FullExact(c);return new DSFullEvent{id=DSFullText.Copy(c.id),nonce=DSFullText.Copy(c.nonce),kind=DSFullText.Copy(c.kind),source=DSFullText.Copy(c.source),result=DSFullText.Copy(c.result),before=DSFullText.Copy(c.before),after=DSFullText.Copy(c.after),value=c.value,sequence=c.sequence,enqueueFrame=c.enqueueFrame,consumeFrame=c.consumeFrame};}).ToArray()}};
                DSFullSnapshotContract.ValidateValue(snapshot,budget);FullReferences(budget);
                string json=JsonUtility.ToJson(snapshot);if(Encoding.UTF8.GetByteCount(json)>DSFullSnapshotBudget.MaximumBytes)throw new InvalidOperationException("FULL_SNAPSHOT_OUTPUT_LIMIT");budget.Check();
                return snapshot;
            }
            finally{context.capturing=false;}
        }
        static DSFullRandom FullRandom(System.Random value)
        {
            if(value is not DSMapTraceRandom trace)throw new InvalidOperationException("FULL_SNAPSHOT_UNREGISTERED_RNG");
            DSFullSnapshotBudget.Count(trace.Count,16384,"rng-trace");trace.ValidateTrace();
            return new DSFullRandom{seed=trace.Seed,count=trace.Count,traceHash=trace.TraceHash,calls=trace.Calls};
        }
    }
    sealed class DSFullReferenceComparer<T>:IEqualityComparer<T> where T:class
    {
        public static readonly DSFullReferenceComparer<T> Instance=new();
        public bool Equals(T a,T b)=>ReferenceEquals(a,b);
        public int GetHashCode(T value)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }

}
#endif
