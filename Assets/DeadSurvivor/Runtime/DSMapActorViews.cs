using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace DeadSurvivor
{
    // 045: one 3D view per live simulation object, keyed by object reference (never by id or team slot:
    // ids restart and slots change between runs). Reads simulation state only. All motion goes to the
    // prefab's first child ("Visual"); the model under it carries the height normalisation. No root motion.
    public sealed class DSMapActorViews
    {
        sealed class RefEq : IEqualityComparer<object>
        {
            public new bool Equals(object a,object b)=>ReferenceEquals(a,b);
            public int GetHashCode(object o)=>RuntimeHelpers.GetHashCode(o);
        }
        sealed class View
        {
            public string key; public GameObject go; public Transform visual; public Renderer[] renderers;
            public Material[][] normal,hit; public Animator anim;
            public Vector3 last; public bool hasLast,flashing,running; public float yaw,phase;
        }
        static readonly int RunHash=Animator.StringToHash("Run");
        readonly DSMapActorSet set; readonly Transform parent;
        readonly Dictionary<object,View> live=new(new RefEq());
        readonly Dictionary<string,Stack<View>> free=new();
        readonly HashSet<object> seen=new(new RefEq());
        readonly List<object> drop=new();
        public int LiveCount=>live.Count;
        public string KeyOf(object owner)=>live.TryGetValue(owner,out var v)?v.key:null;
        public float YawOf(object owner)=>live.TryGetValue(owner,out var v)?v.yaw:float.NaN;
        public bool FlashingOf(object owner)=>live.TryGetValue(owner,out var v)&&v.flashing;
        public int RenderedFrame {get;private set;}=-1;
        public DSMapActorSet Set=>set;
        public DSMapActorViews(DSMapActorSet set,Transform parent){this.set=set;this.parent=parent;}
        public void Begin()=>seen.Clear();
        public bool Hero(object owner,string id,Vector3 feet,bool flash)
        {
            var e=set.Hero(id);if(e==null)return false;
            Place(owner,"H:"+id,e,feet,flash,set.HeroHeight,false);return true;
        }
        public bool Monster(object owner,string type,float radius,Vector3 feet,bool flash)
        {
            var e=set.Monster(type);if(e==null)return false;
            Place(owner,"M:"+type,e,feet,flash,set.MonsterHeight*radius/Mathf.Max(1,set.MonsterReferenceRadius),true);return true;
        }
        void Place(object owner,string key,DSMapActorSet.Entry e,Vector3 feet,bool flash,float height,bool monster)
        {
            seen.Add(owner);
            if(live.TryGetValue(owner,out var v)&&v.key!=key){Release(v);live.Remove(owner);v=null;}
            if(v==null){v=Rent(owner,key,e);live[owner]=v;}
            float dt=Time.unscaledDeltaTime;
            v.go.transform.SetPositionAndRotation(feet,Quaternion.identity);
            v.go.transform.localScale=Vector3.one*height;
            Vector3 delta=v.hasLast?feet-v.last:Vector3.zero;delta.y=0;
            if(delta.sqrMagnitude>36f){delta=Vector3.zero;v.yaw=180;}   // recycle/teleport (> 6 world units): reset motion history and facing
            float speed=dt>0?delta.magnitude/dt:0;
            if(delta.sqrMagnitude>1e-8f)v.yaw=Mathf.MoveTowardsAngle(v.yaw,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,set.TurnDegreesPerSecond*Mathf.Max(dt,1/60f));
            v.last=feet;v.hasLast=true;
            v.visual.localRotation=Quaternion.Euler(0,v.yaw+e.yawOffset,0);
            if(monster)
            {
                float b=Mathf.Abs(Mathf.Sin(Time.unscaledTime*set.BobRate+v.phase));
                v.visual.localPosition=Vector3.up*(b*set.BobHeight/Mathf.Max(.01f,height));
                float s=.06f*(1-b);v.visual.localScale=new Vector3(1+s,1-s,1+s);
            }
            bool run=speed>set.RunSpeedThreshold;
            if(v.anim&&run!=v.running){v.running=run;v.anim.SetBool(RunHash,run);}
            if(flash!=v.flashing&&set.HitMaterial)
            {
                v.flashing=flash;
                for(int i=0;i<v.renderers.Length;i++)v.renderers[i].sharedMaterials=flash?v.hit[i]:v.normal[i];
            }
        }
        View Rent(object owner,string key,DSMapActorSet.Entry e)
        {
            View v=null;
            if(free.TryGetValue(key,out var stack)&&stack.Count>0)v=stack.Pop();
            if(v==null)
            {
                var go=Object.Instantiate(e.prefab,parent);go.name=key;
                var rs=go.GetComponentsInChildren<Renderer>(true);
                v=new View{key=key,go=go,visual=go.transform.childCount>0?go.transform.GetChild(0):go.transform,renderers=rs,anim=go.GetComponentInChildren<Animator>(true)};
                v.normal=new Material[rs.Length][];v.hit=new Material[rs.Length][];
                for(int i=0;i<rs.Length;i++)
                {
                    var mats=rs[i].sharedMaterials;
                    if(e.material)for(int j=0;j<mats.Length;j++)mats[j]=e.material;
                    v.normal[i]=mats;v.hit[i]=new Material[mats.Length];
                    for(int j=0;j<mats.Length;j++)v.hit[i][j]=set.HitMaterial?set.HitMaterial:mats[j];
                }
                if(v.anim)v.anim.applyRootMotion=false;
            }
            v.go.SetActive(true);
            v.hasLast=false;v.yaw=180;v.flashing=false;v.running=false;
            v.phase=(RuntimeHelpers.GetHashCode(owner)&1023)*.00614f;   // visual-only phase; no game or Unity RNG
            v.visual.localPosition=Vector3.zero;v.visual.localScale=Vector3.one;
            for(int i=0;i<v.renderers.Length;i++)v.renderers[i].sharedMaterials=v.normal[i];
            if(v.anim){v.anim.Rebind();v.anim.SetBool(RunHash,false);}
            return v;
        }
        void Release(View v)
        {
            v.go.SetActive(false);
            if(!free.TryGetValue(v.key,out var stack))free[v.key]=stack=new Stack<View>();
            stack.Push(v);
        }
        // Views not drawn this frame go back to the pool (dead enemies, dead heroes, previous run).
        public void End()
        {
            drop.Clear();
            foreach(var kv in live)if(!seen.Contains(kv.Key))drop.Add(kv.Key);
            foreach(var k in drop){Release(live[k]);live.Remove(k);}
            RenderedFrame=Time.frameCount;
        }
        public void Clear()
        {
            foreach(var v in live.Values)Release(v);
            live.Clear();seen.Clear();
        }
    }
}
