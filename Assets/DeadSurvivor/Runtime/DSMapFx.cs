using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace DeadSurvivor
{
    // 047: 3D combat effects for map scenes (presentation only; reads simulation state, never writes it).
    // Lives next to DSMapSession/DSMapPresentation; absent component = previous behaviour (2D discs, text only).
    // Materials are serialized (scene-referenced) so builds keep their shaders.
    [DisallowMultipleComponent]
    public sealed class DSMapFx : MonoBehaviour
    {
        public Material Solid;      // URP Unlit, opaque
        public Material Fade;       // URP Unlit, transparent (alpha from _BaseColor)
        public Material Particles;  // URP Particles/Unlit, transparent
        [Tooltip("World height of projectiles above the ground.")] public float ShotHeight=.38f;

        sealed class RefEq : IEqualityComparer<object>
        {
            public new bool Equals(object a,object b)=>ReferenceEquals(a,b);
            public int GetHashCode(object o)=>RuntimeHelpers.GetHashCode(o);
        }
        sealed class Pooled { public GameObject go; public MeshRenderer r; public MeshFilter f; public TrailRenderer trail; public LineRenderer line; public string kind; }
        sealed class Timed { public Pooled p; public float age,life; public Vector3 pos; public float size; public Color color; public string kind; public Quaternion rot; }

        Transform root; ParticleSystem sparks; MaterialPropertyBlock block;
        readonly Dictionary<object,Pooled> shots=new(new RefEq()),danger=new(new RefEq());
        readonly HashSet<object> seenDanger=new(new RefEq());
        readonly HashSet<object> seenShots=new(new RefEq()),seenEffects=new(new RefEq()),liveEffects=new(new RefEq());
        readonly List<object> drop=new();
        readonly Dictionary<string,Stack<Pooled>> free=new();
        readonly List<Timed> timed=new();
        readonly Dictionary<string,Material> solidByColor=new();
        Mesh arrow,orb,crescent,ring,disc;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        public int ActiveShots=>shots.Count;
        public int ActiveEffects=>timed.Count;
        public event Action<Vector3,Enemy> EnemyShot;   // ranged monster fired (for the recoil pose)

        public void Init(Transform parent)
        {
            if(root)return;
            root=new GameObject("MapFx047").transform;root.SetParent(parent,false);
            block=new MaterialPropertyBlock();
            arrow=ArrowMesh();orb=Icosphere(1);crescent=CrescentMesh(110,24);ring=RingMesh(48,.82f);disc=RingMesh(48,0);
            var ps=new GameObject("Sparks");ps.transform.SetParent(root,false);sparks=ps.AddComponent<ParticleSystem>();
            sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=6000;main.gravityModifier=.9f;main.startSpeed=0;main.startLifetime=.6f;main.startSize=.1f;main.scalingMode=ParticleSystemScalingMode.Shape;
            var em=sparks.emission;em.enabled=false;var sh=sparks.shape;sh.enabled=false;
            var sol=sparks.sizeOverLifetime;sol.enabled=true;sol.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            var col=sparks.colorOverLifetime;col.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.6f),new GradientAlphaKey(0,1)});col.color=g;
            var pr=sparks.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=Particles;pr.renderMode=ParticleSystemRenderMode.Billboard;pr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            sparks.Play();
        }
        public void Clear()
        {
            foreach(var p in shots.Values)Return(p);shots.Clear();
            foreach(var p in danger.Values)Return(p);danger.Clear();
            foreach(var t in timed)Return(t.p);timed.Clear();
            seenEffects.Clear();liveEffects.Clear();
            if(sparks)sparks.Clear();
        }

        // ---- per frame (called by DSWorld while the map is visible) ----
        public void Frame(DSGame g,Func<Vector2,Vector3> world)
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            // projectiles
            seenShots.Clear();
            foreach(var b in g.Shots)
            {
                seenShots.Add(b);
                if(!shots.TryGetValue(b,out var p))
                {
                    string kind=ShotKind(g,b);p=Rent(kind);shots[b]=p;
                    var at=world(b.pos)+Vector3.up*ShotHeight;p.go.transform.position=at;if(p.trail)p.trail.Clear();
                    if(b.owner!=null)Burst(at,ShotColor(kind)*1.2f,6,1.2f,.06f,.25f,0);
                    else
                    {
                        Burst(at,ShotColor(kind),5,.9f,.05f,.2f,0);
                        foreach(var e in g.Enemies)if((e.pos-b.pos).sqrMagnitude<1f){EnemyShot?.Invoke(at,e);break;}
                    }
                }
                var dir=new Vector3(b.dir.x,0,b.dir.y*-1f);   // logical y grows south; world z grows north (DSBlockoutMesh.World)
                var w=world(b.pos);
                p.go.transform.SetPositionAndRotation(w+Vector3.up*ShotHeight,dir.sqrMagnitude>1e-6f?Quaternion.LookRotation(dir):Quaternion.identity);
                float spin=p.kind=="fire"||p.kind=="magic"||p.kind=="enemy"?1+.12f*Mathf.Sin(Time.unscaledTime*30):1;
                p.go.transform.localScale=Vector3.one*ShotScale(p.kind)*spin;
            }
            drop.Clear();foreach(var kv in shots)if(!seenShots.Contains(kv.Key))drop.Add(kv.Key);
            foreach(var k in drop){Return(shots[k]);shots.Remove(k);}
            // danger rings under a bomber's fuse (explosion radius 90) and a charger's windup (replaces the 2D ring)
            seenDanger.Clear();
            foreach(var e in g.Enemies)
            {
                if(e.hp<=0||(e.phase!="fuse"&&e.phase!="windup"))continue;
                seenDanger.Add(e);
                if(!danger.TryGetValue(e,out var p)){p=Rent("danger");p.f.sharedMesh=ring;p.r.sharedMaterial=Fade;danger[e]=p;}
                bool fuse=e.phase=="fuse";float k=1-Mathf.Clamp01(e.phaseTime/(fuse?1.2f:.8f));
                float r=fuse?90*.02f:.9f;float pulse=.85f+.15f*Mathf.Sin(Time.unscaledTime*(fuse?18:12));
                p.go.transform.SetPositionAndRotation(world(e.pos)+Vector3.up*.035f,Quaternion.identity);p.go.transform.localScale=new Vector3(r*pulse,1,r*pulse);
                Tint(p,fuse?new Color(1.4f,.25f,.2f,1):new Color(1.3f,.6f,.2f,1),.25f+.55f*k);
            }
            drop.Clear();foreach(var kv in danger)if(!seenDanger.Contains(kv.Key))drop.Add(kv.Key);
            foreach(var k2 in drop){Return(danger[k2]);danger.Remove(k2);}
            // burst effects (the simulation's Effect list; text effects stay with DSWorld)
            liveEffects.Clear();
            int chainIndex=0;Vector3 chainFrom=default;
            foreach(var e in g.Effects)
            {
                if(e.text!=null)continue;
                liveEffects.Add(e);
                if(!seenEffects.Add(e))continue;
                var at=world(e.pos);float r=e.r*.02f;
                switch(e.color)
                {
                    case "#d9ad83":Slash(g,e,at,r,world);break;                                   // melee / sweep
                    case "#e3d891":Heal(at,r);break;                                              // heal
                    case "#b9a9fa":                                                               // chain hop
                        var from=chainIndex==0?NearestHero(g,e.pos,world):chainFrom;Lightning(from,at);chainFrom=at;chainIndex++;break;
                    case "#a586dc":Shock(at,r,new Color(.75f,.6f,1f),.45f);break;                // pulse aura
                    case "#ffb86d":Explosion(at,r,new Color(1.6f,.75f,.25f),26);break;           // projectile explosion
                    case "#ff6969":Explosion(at,r*.9f,new Color(1.8f,.45f,.3f),40);Smoke(at,r);break;   // bomber
                    case "#e17769":Burst(at+Vector3.up*.3f,new Color(1f,.45f,.45f),24,2.2f,.09f,.6f,1);Shock(at,.9f,new Color(1f,.5f,.5f),.5f);break;   // hero down
                    case "#b3cc76":break;                                                         // enemy death: DSMapActorViews pops the model
                    default:Burst(at+Vector3.up*.2f,ParseColor(e.color),10,1.4f,.07f,.4f,1);break;
                }
            }
            seenEffects.IntersectWith(liveEffects);
            // timed visuals
            for(int i=timed.Count-1;i>=0;i--)
            {
                var t=timed[i];t.age+=dt;float k=Mathf.Clamp01(t.age/t.life);
                if(k>=1){Return(t.p);timed.RemoveAt(i);continue;}
                Animate(t,k);
            }
        }

        static string ShotKind(DSGame g,Shot b)
        {
            if(b.type=="enemy")return "enemy";
            if(b.type!=null&&b.type.Contains("bow"))return "arrow";
            if(b.type!=null&&g.Items.TryGetValue(b.type,out var d)&&d.attackMode=="explosion")return "fire";
            return "magic";
        }
        static Color ShotColor(string kind)=>kind switch{"arrow"=>new Color(1f,.93f,.78f),"fire"=>new Color(2f,.9f,.3f),"enemy"=>new Color(1.7f,.35f,.6f),_=>new Color(.85f,.75f,1.6f)};
        static float ShotScale(string kind)=>kind switch{"arrow"=>1f,"fire"=>.16f,"enemy"=>.11f,_=>.12f};

        // ---- effect makers ----
        void Slash(DSGame g,Effect e,Vector3 at,float r,Func<Vector2,Vector3> world)
        {
            Enemy best=null;float bd=float.MaxValue;
            foreach(var en in g.Enemies){if(en.hp<=0&&en.flash<=0)continue;float d=(en.pos-e.pos).sqrMagnitude;if(d<bd){bd=d;best=en;}}
            var dir=best!=null?world(best.pos)-at:Vector3.forward;dir.y=0;if(dir.sqrMagnitude<1e-6f)dir=Vector3.forward;
            var p=Rent("slash");p.f.sharedMesh=crescent;p.r.sharedMaterial=Fade;
            timed.Add(new Timed{p=p,life=.2f,pos=at+Vector3.up*.32f,size=Mathf.Clamp(r*.8f,.6f,2.2f),color=new Color(1.15f,1.08f,.92f,.55f),kind="slash",rot=Quaternion.LookRotation(dir.normalized)});
            Burst(at+dir.normalized*Mathf.Max(.6f,r)*.8f+Vector3.up*.3f,new Color(1.2f,1.1f,.9f),6,1.6f,.06f,.2f,0);
        }
        void Heal(Vector3 at,float r)
        {
            for(int i=0;i<14;i++)
            {
                var o=UnityEngine.Random.insideUnitCircle*.35f;
                sparks.Emit(new ParticleSystem.EmitParams{position=at+new Vector3(o.x,.1f+UnityEngine.Random.value*.4f,o.y),velocity=Vector3.up*(.8f+UnityEngine.Random.value*.6f),startColor=new Color(.7f,1f,.55f),startSize=.08f+UnityEngine.Random.value*.05f,startLifetime=.7f,applyShapeToPosition=false},1);
            }
            Shock(at,Mathf.Max(.5f,Mathf.Min(r,1.6f)),new Color(.75f,1f,.55f),.5f);
        }
        void Lightning(Vector3 from,Vector3 to)
        {
            var p=Rent("bolt");var lr=p.line;
            int n=7;lr.positionCount=n;var a=from+Vector3.up*.45f;var b=to+Vector3.up*.35f;var side=Vector3.Cross((b-a).normalized,Vector3.up);
            for(int i=0;i<n;i++){float t=i/(n-1f);var q=Vector3.Lerp(a,b,t);if(i>0&&i<n-1)q+=side*UnityEngine.Random.Range(-.18f,.18f)+Vector3.up*UnityEngine.Random.Range(-.08f,.12f);lr.SetPosition(i,q);}
            timed.Add(new Timed{p=p,life=.18f,color=new Color(1.3f,1.1f,1.9f,1),kind="bolt"});
            Burst(b,new Color(1.1f,.95f,1.8f),8,1.5f,.06f,.25f,0);
        }
        void Shock(Vector3 at,float r,Color c,float life)
        {
            var p=Rent("ring");p.f.sharedMesh=ring;p.r.sharedMaterial=Fade;
            timed.Add(new Timed{p=p,life=life,pos=at+Vector3.up*.03f,size=Mathf.Max(.3f,r),color=new Color(c.r,c.g,c.b,.8f),kind="ring"});
        }
        void Explosion(Vector3 at,float r,Color c,int n)
        {
            var p=Rent("blast");p.f.sharedMesh=orb;p.r.sharedMaterial=Fade;
            timed.Add(new Timed{p=p,life=.35f,pos=at+Vector3.up*.25f,size=Mathf.Clamp(r*.75f,.4f,2.2f),color=new Color(c.r,c.g,c.b,.85f),kind="blast"});
            Shock(at,Mathf.Clamp(r,.5f,2.4f),new Color(c.r*.7f,c.g*.7f,c.b*.7f),.45f);
            for(int i=0;i<n;i++)
            {
                var d=UnityEngine.Random.onUnitSphere;d.y=Mathf.Abs(d.y)*.8f+.2f;
                sparks.Emit(new ParticleSystem.EmitParams{position=at+Vector3.up*.25f,velocity=d*(1.5f+UnityEngine.Random.value*2.5f)*Mathf.Clamp(r,.6f,1.8f),startColor=Color.Lerp(c,new Color(1.6f,1.4f,.6f),UnityEngine.Random.value*.6f),startSize=.08f+UnityEngine.Random.value*.09f,startLifetime=.45f+UnityEngine.Random.value*.25f,applyShapeToPosition=false},1);
            }
        }
        void Smoke(Vector3 at,float r)
        {
            for(int i=0;i<10;i++)
            {
                var o=UnityEngine.Random.insideUnitCircle*Mathf.Clamp(r*.5f,.3f,1f);
                sparks.Emit(new ParticleSystem.EmitParams{position=at+new Vector3(o.x,.2f,o.y),velocity=new Vector3(o.x,1.1f+UnityEngine.Random.value,o.y),startColor=new Color(.35f,.33f,.32f,.8f),startSize=.25f+UnityEngine.Random.value*.15f,startLifetime=.8f,applyShapeToPosition=false},1);
            }
        }
        // small spray of sparks; gravity 0..1 via upward bias
        public void Burst(Vector3 at,Color c,int n,float speed,float size,float life,float up)
        {
            if(!sparks)return;
            for(int i=0;i<n;i++)
            {
                var d=UnityEngine.Random.onUnitSphere;d.y=Mathf.Abs(d.y)*up+d.y*(1-up)*.4f;
                sparks.Emit(new ParticleSystem.EmitParams{position=at,velocity=d*speed*(.5f+UnityEngine.Random.value*.6f),startColor=c,startSize=size*(.7f+UnityEngine.Random.value*.6f),startLifetime=life*(.7f+UnityEngine.Random.value*.6f),applyShapeToPosition=false},1);
            }
        }
        // monster pop on death (called by DSWorld from DSMapActorViews)
        public void MonsterDeath(Vector3 feet,Color c,float size)
        {
            Burst(feet+Vector3.up*size*.5f,c,16,2f*Mathf.Max(.6f,size),.09f*Mathf.Max(.7f,size),.45f,1);
            Burst(feet+Vector3.up*size*.4f,new Color(1.3f,1.3f,1.2f),5,1.4f,.06f,.25f,1);
        }
        static Vector3 NearestHero(DSGame g,Vector2 p,Func<Vector2,Vector3> world)
        {
            Survivor best=null;float bd=float.MaxValue;foreach(var c in g.Team){if(c.hp<=0)continue;float d=(c.pos-p).sqrMagnitude;if(d<bd){bd=d;best=c;}}
            return best!=null?world(best.pos):world(p);
        }
        static Color ParseColor(string hex)=>ColorUtility.TryParseHtmlString(hex,out var c)?c:Color.white;

        void Animate(Timed t,float k)
        {
            var tr=t.p.go.transform;
            switch(t.kind)
            {
                case "slash":
                {
                    float s=t.size*Mathf.Lerp(.65f,1.08f,Mathf.Sqrt(k));
                    tr.SetPositionAndRotation(t.pos,t.rot*Quaternion.Euler(0,Mathf.Lerp(-35,35,k),0));tr.localScale=new Vector3(s,1,s);
                    Tint(t.p,t.color,1-k*k);break;
                }
                case "ring":
                {
                    float s=t.size*Mathf.Lerp(.15f,1f,1-(1-k)*(1-k));tr.SetPositionAndRotation(t.pos,Quaternion.identity);tr.localScale=new Vector3(s,1,s);
                    Tint(t.p,t.color,1-k);break;
                }
                case "blast":
                {
                    float s=t.size*Mathf.Lerp(.3f,1f,1-(1-k)*(1-k)*(1-k));tr.SetPositionAndRotation(t.pos,Quaternion.identity);tr.localScale=Vector3.one*s;
                    Tint(t.p,t.color,(1-k)*(1-k));break;
                }
                case "bolt":
                {
                    var c=t.color;c.a=1-k;t.p.line.startColor=c;t.p.line.endColor=c;t.p.line.widthMultiplier=.07f*(1-k*.6f);break;
                }
            }
        }
        void Tint(Pooled p,Color c,float alpha){c.a*=alpha;block.Clear();block.SetColor(BaseColor,c);p.r.SetPropertyBlock(block);}

        // ---- pool ----
        Pooled Rent(string kind)
        {
            if(free.TryGetValue(kind,out var st)&&st.Count>0){var q=st.Pop();q.go.SetActive(true);return q;}
            var go=new GameObject("Fx_"+kind);go.transform.SetParent(root,false);
            var p=new Pooled{go=go,kind=kind};
            if(kind=="bolt")
            {
                p.line=go.AddComponent<LineRenderer>();p.line.sharedMaterial=Particles;p.line.useWorldSpace=true;p.line.numCapVertices=2;p.line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                return p;
            }
            p.f=go.AddComponent<MeshFilter>();p.r=go.AddComponent<MeshRenderer>();p.r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;p.r.receiveShadows=false;
            if(kind is "arrow" or "fire" or "magic" or "enemy")
            {
                p.f.sharedMesh=kind=="arrow"?arrow:orb;p.r.sharedMaterial=SolidColor(ShotColor(kind));
                p.trail=go.AddComponent<TrailRenderer>();p.trail.sharedMaterial=Particles;p.trail.time=kind=="arrow"?.08f:.14f;p.trail.minVertexDistance=.03f;
                p.trail.widthMultiplier=kind=="arrow"?.05f:kind=="fire"?.16f:.1f;p.trail.widthCurve=AnimationCurve.Linear(0,1,1,0);p.trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var c=ShotColor(kind);var grad=new Gradient();grad.SetKeys(new[]{new GradientColorKey(c,0),new GradientColorKey(c,1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});p.trail.colorGradient=grad;
            }
            return p;
        }
        void Return(Pooled p)
        {
            if(p==null||!p.go)return;
            if(p.trail)p.trail.Clear();
            p.go.SetActive(false);
            if(!free.TryGetValue(p.kind,out var st))free[p.kind]=st=new Stack<Pooled>();
            st.Push(p);
        }
        Material SolidColor(Color c)
        {
            string key=c.ToString("F2");
            if(!solidByColor.TryGetValue(key,out var m)){m=new Material(Solid){name="Fx047_"+key};m.SetColor(BaseColor,c);solidByColor[key]=m;}
            return m;
        }
        void OnDestroy(){foreach(var m in solidByColor.Values)if(m)Destroy(m);}

        // ---- meshes (local units; scaled per use) ----
        static Mesh ArrowMesh()
        {
            // shaft along +Z (length .42) with a diamond head and two fletching fins
            var v=new List<Vector3>();var t=new List<int>();
            void Box(Vector3 c,Vector3 s){int b=v.Count;for(int i=0;i<8;i++)v.Add(c+Vector3.Scale(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f),s));
                int[] f={0,2,3,0,3,1, 4,5,7,4,7,6, 0,1,5,0,5,4, 2,6,7,2,7,3, 0,4,6,0,6,2, 1,3,7,1,7,5};foreach(var x in f)t.Add(b+x);}
            Box(new Vector3(0,0,-.03f),new Vector3(.025f,.025f,.38f));
            int h=v.Count;v.Add(new Vector3(0,0,.24f));v.Add(new Vector3(-.05f,0,.15f));v.Add(new Vector3(0,.05f,.15f));v.Add(new Vector3(.05f,0,.15f));v.Add(new Vector3(0,-.05f,.15f));
            t.AddRange(new[]{h,h+1,h+2,h,h+2,h+3,h,h+3,h+4,h,h+4,h+1,h+1,h+4,h+3,h+1,h+3,h+2});
            Box(new Vector3(0,0,-.2f),new Vector3(.1f,.012f,.07f));Box(new Vector3(0,0,-.2f),new Vector3(.012f,.1f,.07f));
            var m=new Mesh{name="Arrow047"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        static Mesh Icosphere(int sub)
        {
            float g=(1+Mathf.Sqrt(5))/2;
            var p=new List<Vector3>{new(-1,g,0),new(1,g,0),new(-1,-g,0),new(1,-g,0),new(0,-1,g),new(0,1,g),new(0,-1,-g),new(0,1,-g),new(g,0,-1),new(g,0,1),new(-g,0,-1),new(-g,0,1)};
            var f=new List<int[]>{new[]{0,11,5},new[]{0,5,1},new[]{0,1,7},new[]{0,7,10},new[]{0,10,11},new[]{1,5,9},new[]{5,11,4},new[]{11,10,2},new[]{10,7,6},new[]{7,1,8},new[]{3,9,4},new[]{3,4,2},new[]{3,2,6},new[]{3,6,8},new[]{3,8,9},new[]{4,9,5},new[]{2,4,11},new[]{6,2,10},new[]{8,6,7},new[]{9,8,1}};
            for(int s=0;s<sub;s++)
            {
                var mid=new Dictionary<long,int>();
                int M(int a,int b){long k=a<b?((long)a<<32)|(uint)b:((long)b<<32)|(uint)a;if(mid.TryGetValue(k,out var i))return i;p.Add((p[a]+p[b])/2);return mid[k]=p.Count-1;}
                var f2=new List<int[]>();foreach(var x in f){int a=M(x[0],x[1]),b=M(x[1],x[2]),c=M(x[2],x[0]);f2.AddRange(new[]{new[]{x[0],a,c},new[]{x[1],b,a},new[]{x[2],c,b},new[]{a,b,c}});}f=f2;
            }
            var v=new List<Vector3>();var t=new List<int>();
            foreach(var x in f){int i=v.Count;v.Add(p[x[0]].normalized);v.Add(p[x[1]].normalized);v.Add(p[x[2]].normalized);t.AddRange(new[]{i,i+1,i+2});}
            var m=new Mesh{name="Orb047"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();
            if(Vector3.Dot(m.normals[0],(v[0]+v[1]+v[2])/3)<0){t.Reverse();m.SetTriangles(t,0);m.RecalculateNormals();}
            m.RecalculateBounds();return m;
        }
        // flat crescent facing +Z, radius 1, arc 'deg' wide, thicker in the middle
        static Mesh CrescentMesh(float deg,int n)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=n;i++)
            {
                float u=i/(float)n,a=Mathf.Deg2Rad*(-deg/2+deg*u),w=.2f*Mathf.Sin(Mathf.PI*u)+.015f;
                var d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));v.Add(d*1f);v.Add(d*(1-w));
                if(i>0){int b=v.Count-4;t.AddRange(new[]{b,b+2,b+1,b+1,b+2,b+3});}
            }
            var m=new Mesh{name="Crescent047"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();
            if(m.normals[0].y<0){t.Reverse();m.SetTriangles(t,0);m.RecalculateNormals();}
            m.RecalculateBounds();return m;
        }
        // ground ring (inner > 0) or disc (inner 0) of radius 1
        static Mesh RingMesh(int n,float inner)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=n;i++){float a=Mathf.PI*2*i/n;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));v.Add(d);v.Add(d*inner);if(i>0){int b=v.Count-4;t.AddRange(new[]{b,b+1,b+2,b+1,b+3,b+2});}}
            var m=new Mesh{name="Ring047"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();
            if(m.normals[0].y<0){t.Reverse();m.SetTriangles(t,0);m.RecalculateNormals();}
            m.RecalculateBounds();return m;
        }
    }
}
