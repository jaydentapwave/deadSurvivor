using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        sealed class EnemyRoute {public List<Vector2> points;public int index;public Vector2 target;public float retry;}
        readonly Dictionary<float,DSMapNavigation> mapNavigators=new();
        readonly Dictionary<Enemy,EnemyRoute> mapRoutes=new();
        int mapSearches,mapSpawnSearches,mapEnemyCursor;
        public int MapSpawnFailures {get;private set;}
        public bool MapLine(Vector2 a,Vector2 b)=>!MapSession || MapSession.Geometry.ValidCoordinate(a)&&MapSession.Geometry.ValidCoordinate(b)&&MapSession.Geometry.SafeSweep(a,b,.01);
        DSMapNavigation Navigator(float radius)
        {if(!mapNavigators.TryGetValue(radius,out var nav))mapNavigators[radius]=nav=new DSMapNavigation(MapSession.Geometry,radius);return nav;}
        void ResetMapCombat(){mapRoutes.Clear();mapSearches=mapSpawnSearches=mapEnemyCursor=0;}
        Vector2 EnemyDisplace(Enemy e,Vector2 requested)=>MapSession?DSMapNavigation.Clamp(MapSession.Geometry,e.pos,requested,Monsters[e.type].radius):requested;
        Vector2 EnemyAdvance(Enemy e,Vector2 target,Vector2 direction,float distance)
        {
            if(!MapSession)return e.pos+direction*distance;
            float radius=Monsters[e.type].radius;var map=MapSession.Geometry;
            if(map.ValidCoordinate(target)&&map.SafeSweep(e.pos,target,radius))return EnemyDisplace(e,e.pos+direction*distance);
            if(!mapRoutes.TryGetValue(e,out var route))mapRoutes[e]=route=new EnemyRoute();
            bool stale=route.points==null || (target-route.target).sqrMagnitude>64*64 || route.index>=route.points.Count;
            if(stale && TotalTime>=route.retry && mapSearches<2)
            {
                mapSearches++;route.target=target;route.index=0;route.retry=TotalTime+.5f;
                route.points=Navigator(radius).TryPath(e.pos,target,out var points)?points:null;
            }
            if(route.points==null)return e.pos;
            while(route.index<route.points.Count && Vector2.Distance(e.pos,route.points[route.index])<1)route.index++;
            if(route.index>=route.points.Count)return e.pos;
            return EnemyDisplace(e,Vector2.MoveTowards(e.pos,route.points[route.index],distance));
        }
        bool TryMapSpawn(string type,float angle,float distance,out Vector2 position)
        {
            position=Vector2.zero;float radius=Monsters[type].radius;
            var targets=Team.Where(c=>c.hp>0).ToArray();if(targets.Length==0)return false;
            for(int i=0;i<16;i++)
            {
                float offset=(i%2==0?1:-1)*((i+1)/2)*Mathf.PI/8;
                var p=Center+Direction(angle+offset)*distance;
                if(!MapSession.Geometry.ValidCoordinate(p)||!MapSession.Geometry.SafeSweep(p,p,radius))continue;
                foreach(var target in targets.OrderBy(c=>(c.pos-p).sqrMagnitude))
                {
                    if(mapSpawnSearches>=1)return false;mapSpawnSearches++;
                    if(Navigator(radius).TryPath(p,target.pos,out var path)){position=p;return true;}
                }
            }
            return false;
        }
    }
}
