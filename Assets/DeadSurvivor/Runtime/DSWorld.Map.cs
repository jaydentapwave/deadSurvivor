using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSWorld
    {
        // 045: optional presentation (DSMapPresentation next to DSMapSession). Looked up once; absent = legacy path.
        DSMapPresentation presentation;bool presentationLooked;DSMapActorViews actorViews;DSMapFx fx;
        readonly Dictionary<Gear,float> gearFlash=new();   // 047: rising activationFlash = a weapon fired this frame
        public int MapActorViewCount=>actorViews?.LiveCount??-1;
        public int MapActorRenderedFrame=>actorViews?.RenderedFrame??-1;
        public string MapActorKeyOf(object owner)=>actorViews?.KeyOf(owner);
        void LookupPresentation()
        {
            if(presentationLooked)return;
            presentationLooked=true;
            if(g.MapSession.IsFixture)return;
            presentation=g.MapSession.GetComponent<DSMapPresentation>();
            if(!presentation)return;
            if(presentation.Actors)actorViews=new DSMapActorViews(presentation.Actors,root);
            if(g.MapSession.BoundVisibility)g.MapSession.BoundVisibility.ParallelRays=true;
            fx=g.MapSession.GetComponent<DSMapFx>();
            if(fx)
            {
                fx.Init(root);
                if(actorViews!=null)
                {
                    fx.EnemyShot+=(at,e)=>actorViews.MonsterShot(e);
                    actorViews.MonsterDied=(feet,type,h)=>fx.MonsterDeath(feet,g.Monsters.TryGetValue(type,out var m)?ColorOf(m.color):Color.white,h);
                }
            }
        }
        SpriteRenderer MapSprite(Sprite sprite,Vector2 p,float height,Color color,int order,float lift=0)
        {
            var r=Draw(sprite,p,Vector2.one,color,order);
            r.transform.rotation=cam.transform.rotation;
            float scale=height/Mathf.Max(.001f,sprite.bounds.size.y);
            r.transform.localScale=Vector3.one*scale;
            r.transform.position=g.MapSession.World(p)+Vector3.up*.01f+cam.transform.up*(lift-sprite.bounds.min.y*scale);
            return r;
        }
        // 047: a weapon whose activationFlash rose since last frame fired now -> upper-body Slash (melee/sweep) or Shoot
        void HeroAttacks()
        {
            if(actorViews==null)return;
            foreach(var c in g.Team)
            {
                if(c.hp<=0)continue;
                foreach(var it in c.items)
                {
                    float was=gearFlash.TryGetValue(it,out var f)?f:0;gearFlash[it]=it.activationFlash;
                    if(it.activationFlash<=was+.01f||!g.Items.TryGetValue(it.type,out var d)||string.IsNullOrEmpty(d.attackMode))continue;
                    bool melee=d.attackMode=="melee"||d.attackMode=="sweep";
                    Enemy best=null;float bd=float.MaxValue;foreach(var e in g.Enemies){if(e.hp<=0)continue;float q=(e.pos-c.pos).sqrMagnitude;if(q<bd){bd=q;best=e;}}
                    actorViews.HeroAttack(c,melee,g.MapSession.World(best!=null?best.pos:c.pos));
                }
            }
        }
        void RenderMap()
        {
            cursor=textCursor=0;
            bool visible=g.State=="battle" || g.State=="prep" || g.State=="result";
            root.gameObject.SetActive(visible);
            g.MapSession.Frame(g.Center);
            LookupPresentation();
            if(presentation)cam.transform.position=g.MapSession.World(g.Center)-cam.transform.forward*presentation.CameraDistance;
            cam.backgroundColor=ColorOf("#192326");
            g.ViewWidth=cam.orthographicSize*2*cam.aspect/g.MapSession.BoundWorldScale;
            g.ViewHeight=cam.orthographicSize*2/Mathf.Sin(g.MapSession.BoundElevation*Mathf.Deg2Rad)/g.MapSession.BoundWorldScale;
            if(!visible){actorViews?.Clear();fx?.Clear();gearFlash.Clear();return;}
            actorViews?.Begin();
            foreach(var e in g.Enemies)
            {
                if(actorViews==null || !actorViews.Monster(e,e.type,g.Monsters[e.type].radius,g.MapSession.World(e.pos),e.flash>0,e.phase,e.phaseTime,e.cd))
                    MapSprite(Load("Monsters/"+e.type+(e.flash>0?"_hit":"")),e.pos,g.Monsters[e.type].radius*.04f,Color.white,20);
                if(!fx&&(e.phase=="fuse" || e.phase=="windup"))MapSprite(ring,e.pos,.35f,ColorOf("#ff8b54"),21,.8f);
            }
            foreach(var c in g.Team)
            {
                if(g.MapSession.IsFixture)
                {
                    int slot=g.Team.IndexOf(c);float radius=g.MapSession.BodyProfile.Radius(slot,g.Team.Count)*g.MapSession.BoundWorldScale;
                    var mark=MapSprite(c.hp>0?disc:ring,c.pos,radius*2,ColorOf(c.hp>0?"#c7ed83":"#666666"),30);
                    mark.transform.rotation=Quaternion.Euler(90,0,0);mark.transform.position=g.MapSession.World(c.pos)+Vector3.up*.015f;
                    var reserve=MapSprite(ring,g.Center+g.Offset(slot),radius*2,ColorOf("#73b5ed"),31);
                    reserve.transform.rotation=Quaternion.Euler(90,0,0);reserve.transform.position=g.MapSession.World(g.Center+g.Offset(slot))+Vector3.up*.016f;
                    continue;
                }
                bool model=c.hp>0 && actorViews!=null && actorViews.Hero(c,g.Def(c).id,g.MapSession.World(c.pos),c.flash>0);
                if(!model)MapSprite(Load(c.hp>0?"Characters/"+g.Def(c).id+(c.flash>0?"_hit":""):"Markers/dead"),c.pos,c.hp>0?.72f:.24f,Color.white,30);
                if(c.hp<=0)continue;
                float barLift=model?actorViews.Set.BarLift:.84f;
                var bar=MapSprite(square,c.pos,.04f,ColorOf("#3c4847"),40,barLift);bar.transform.localScale=new Vector3(.72f,.04f,1);
                var fill=MapSprite(square,c.pos,.04f,ColorOf("#c7ed83"),41,barLift);fill.transform.localScale=new Vector3(.72f*c.hp/c.maxhp,.04f,1);
            }
            if(!fx)foreach(var b in g.Shots)MapSprite(disc,b.pos,.09f,b.type=="enemy"?ColorOf("#ff7777"):ColorOf("#fff2a8"),50,.2f);
            if(fx)
            {
                fx.Frame(g,p=>g.MapSession.World(p));
                HeroAttacks();
            }
            // Only actual hit feedback is drawn; wall-unclipped radius rings would imply damage through walls.
            foreach(var e in g.Effects)if(e.text!=null)
            {
                Text(e.text,e.pos,12,ColorOf(e.color));var t=texts[textCursor-1];t.transform.rotation=cam.transform.rotation;t.transform.position=g.MapSession.World(e.pos)+cam.transform.up;
            }
            float focusLift=actorViews!=null?actorViews.Set.FocusLift:.36f;
            if(g.MapSession.BoundVisibility)g.MapSession.BoundVisibility.Focus=g.Team.Where(c=>c.hp>0).Select(c=>g.MapSession.World(c.pos)+Vector3.up*.01f+cam.transform.up*focusLift).ToArray();
            actorViews?.End();
            for(int i=cursor;i<pool.Count;i++)pool[i].gameObject.SetActive(false);
            for(int i=textCursor;i<texts.Count;i++)texts[i].gameObject.SetActive(false);
        }
    }
}
