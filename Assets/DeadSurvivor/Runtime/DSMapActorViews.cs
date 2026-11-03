using System;
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
            // 047 presentation state: attack/recoil/lunge timers, last cooldown seen, death pop
            public float recoil,lunge,lastCd=-1,attackAt=-9,dying=-1,height; public bool hasSlash,hasShoot,checkedParams; public object owner;
        }
        static readonly int RunHash=Animator.StringToHash("Run"),SlashHash=Animator.StringToHash("Slash"),ShootHash=Animator.StringToHash("Shoot");
        readonly List<View> dying=new();
        // 047: called when a monster view starts its death pop (feet, monster type, world height)
        public Action<Vector3,string,float> MonsterDied;
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
        public bool Monster(object owner,string type,float radius,Vector3 feet,bool flash)=>Monster(owner,type,radius,feet,flash,null,0,-1);
        // 047: phase/phaseTime/cd drive attack poses (windup crouch, dash stretch, fuse swell + blink, melee lunge)
        public bool Monster(object owner,string type,float radius,Vector3 feet,bool flash,string phase,float phaseTime,float cd)
        {
            var e=set.Monster(type);if(e==null)return false;
            float h=set.MonsterHeight*radius/Mathf.Max(1,set.MonsterReferenceRadius);
            bool blink=false;
            if(phase=="fuse"){float f=Mathf.Lerp(4,16,1-Mathf.Clamp01(phaseTime/1.2f));blink=Mathf.Sin(Time.unscaledTime*f*Mathf.PI*2)>0;}
            Place(owner,"M:"+type,e,feet,flash||blink,h,true);
            var v=live[owner];v.height=h;
            if(cd>=0){if(v.lastCd>=0&&cd>v.lastCd+.05f&&type!="ranged"&&phase!="dash")v.lunge=.18f;v.lastCd=cd;}
            Pose(v,phase,phaseTime);
            return true;
        }
        // 047: hero attack -> upper-body Slash/Shoot trigger (controller layer); face the target while standing
        public void HeroAttack(object owner,bool melee,Vector3 target)
        {
            if(!live.TryGetValue(owner,out var v)||!v.anim)return;
            if(!v.checkedParams){v.checkedParams=true;foreach(var prm in v.anim.parameters){if(prm.nameHash==SlashHash)v.hasSlash=true;if(prm.nameHash==ShootHash)v.hasShoot=true;}}
            if(Time.unscaledTime-v.attackAt<.28f)return;v.attackAt=Time.unscaledTime;
            if(melee&&v.hasSlash)v.anim.SetTrigger(SlashHash);else if(!melee&&v.hasShoot)v.anim.SetTrigger(ShootHash);
            var d=target-v.go.transform.position;d.y=0;
            if(!v.running&&d.sqrMagnitude>1e-4f)v.yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;
        }
        // 047: ranged monster fired
        public void MonsterShot(object owner){if(live.TryGetValue(owner,out var v))v.recoil=.16f;}
        void Pose(View v,string phase,float phaseTime)
        {
            float dt=Time.unscaledDeltaTime;var s=v.visual.localScale;var rot=v.visual.localRotation;var pos=v.visual.localPosition;
            if(phase=="windup"){float k=1-Mathf.Clamp01(phaseTime/.8f);s=new Vector3(1+.18f*k,1-.22f*k,1+.18f*k);pos+=new Vector3(Mathf.Sin(Time.unscaledTime*70)*.03f*k,0,0);}
            else if(phase=="dash"){s=new Vector3(.88f,.9f,1.28f);rot*=Quaternion.Euler(14,0,0);}
            else if(phase=="fuse"){float k=1-Mathf.Clamp01(phaseTime/1.2f);float sw=1+.28f*k;s=new Vector3(sw,sw*(1+.04f*Mathf.Sin(Time.unscaledTime*40)),sw);}
            if(v.recoil>0){float k=v.recoil/.16f;s=Vector3.Scale(s,new Vector3(1+.1f*k,1-.1f*k,1-.12f*k));rot*=Quaternion.Euler(-16*k,0,0);v.recoil-=dt;}
            if(v.lunge>0){float k=Mathf.Sin(Mathf.PI*(1-v.lunge/.18f));rot*=Quaternion.Euler(20*k,0,0);s=Vector3.Scale(s,new Vector3(1,1-.12f*k,1+.18f*k));v.lunge-=dt;}
            v.visual.localScale=s;v.visual.localRotation=rot;v.visual.localPosition=pos;
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
                var go=UnityEngine.Object.Instantiate(e.prefab,parent);go.name=key;
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
            v.dying=-1;v.recoil=v.lunge=0;v.lastCd=-1;v.attackAt=-9;
            v.go.SetActive(false);
            if(!free.TryGetValue(v.key,out var stack))free[v.key]=stack=new Stack<View>();
            stack.Push(v);
        }
        // Views not drawn this frame go back to the pool (dead enemies, dead heroes, previous run).
        public void End()
        {
            drop.Clear();
            foreach(var kv in live)if(!seen.Contains(kv.Key))drop.Add(kv.Key);
            foreach(var k in drop)
            {
                var v=live[k];live.Remove(k);
                // 047: a killed monster pops (flash, swell, squash to nothing) instead of vanishing
                if(k is Enemy en&&en.hp<=0&&v.key.StartsWith("M:")&&v.go.activeSelf){v.dying=0;dying.Add(v);MonsterDied?.Invoke(v.go.transform.position,v.key.Substring(2),v.height);}
                else Release(v);
            }
            for(int i=dying.Count-1;i>=0;i--)
            {
                var v=dying[i];v.dying+=Time.unscaledDeltaTime;float k=v.dying/.28f;
                if(k>=1){dying.RemoveAt(i);Release(v);continue;}
                bool white=k<.35f;if(set.HitMaterial&&white!=v.flashing){v.flashing=white;for(int j=0;j<v.renderers.Length;j++)v.renderers[j].sharedMaterials=white?v.hit[j]:v.normal[j];}
                float sw=k<.3f?1+.35f*(k/.3f):1.35f*(1-(k-.3f)/.7f);
                v.visual.localScale=new Vector3(sw*1.1f,Mathf.Max(.02f,sw*(k<.3f?1:.6f)),sw*1.1f);
            }
            RenderedFrame=Time.frameCount;
        }
        public void Clear()
        {
            foreach(var v in dying)Release(v);dying.Clear();
            foreach(var v in live.Values)Release(v);
            live.Clear();seen.Clear();
        }
    }
}
