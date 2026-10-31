using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor
{
    // Immutable geometry/radius cache; every path edge is certified by the same swept disc policy.
    public sealed class DSMapNavigation
    {
        readonly DSMapGeometry map;
        readonly float radius;
        const float Cell=32;
        readonly Dictionary<Vector2Int,bool> nodes=new();
        readonly Dictionary<(Vector2Int,Vector2Int),bool> edges=new();
        public int LastVisited {get;private set;}
        public DSMapNavigation(DSMapGeometry geometry,float bodyRadius){map=geometry;radius=bodyRadius;}
        Vector2 Point(Vector2Int n)=>new Vector2(n.x*Cell,n.y*Cell);
        bool Safe(Vector2 p)=>map.ValidCoordinate(p) && map.SafeSweep(p,p,radius);
        bool Node(Vector2Int n){if(!nodes.TryGetValue(n,out bool ok))nodes[n]=ok=Safe(Point(n));return ok;}
        bool Edge(Vector2Int a,Vector2Int b)
        {
            var key=a.x<b.x || a.x==b.x && a.y<b.y?(a,b):(b,a);
            if(!edges.TryGetValue(key,out bool ok))edges[key]=ok=map.SafeSweep(Point(a),Point(b),radius);return ok;
        }
        public bool TryPath(Vector2 start,Vector2 target,out List<Vector2> path,int budget=2048)
        {
            path=new List<Vector2>();LastVisited=0;
            if(!Safe(start) || !map.ValidCoordinate(target) || budget<1)return false;
            Vector2 goal=target;
            if(!Safe(goal))
            {
                bool found=false;
                for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI/8;var p=target+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(radius+4);
                    if(Safe(p) && map.SafeSweep(p,target,.01)){goal=p;found=true;break;}
                }
                if(!found)return false;
            }
            if(map.SafeSweep(start,goal,radius)){path.Add(goal);return true;}
            var open=new SortedSet<(float,int,int)>();var cost=new Dictionary<Vector2Int,float>();var parent=new Dictionary<Vector2Int,Vector2Int>();var closed=new HashSet<Vector2Int>();
            var origin=new Vector2Int(Mathf.RoundToInt(start.x/Cell),Mathf.RoundToInt(start.y/Cell));
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
            {
                var n=origin+new Vector2Int(x,y);if(!Node(n) || !map.SafeSweep(start,Point(n),radius))continue;
                float g=Vector2.Distance(start,Point(n));cost[n]=g;parent[n]=n;open.Add((g+Vector2.Distance(Point(n),goal),n.x,n.y));
            }
            while(open.Count>0 && LastVisited<budget)
            {
                var item=open.Min;open.Remove(item);var n=new Vector2Int(item.Item2,item.Item3);
                if(!closed.Add(n))continue;LastVisited++;
                var p=Point(n);
                if(Vector2.Distance(p,goal)<Cell*1.6f && map.SafeSweep(p,goal,radius))
                {
                    path.Add(goal);var at=n;
                    while(true){path.Add(Point(at));var previous=parent[at];if(previous==at)break;at=previous;}
                    path.Reverse();return true;
                }
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
                {
                    if(x==0 && y==0)continue;
                    var next=n+new Vector2Int(x,y);if(closed.Contains(next)||!Node(next)||!Edge(n,next))continue;
                    float g=cost[n]+Cell*(x!=0 && y!=0?1.41421356237f:1);
                    if(cost.TryGetValue(next,out float old) && g>=old)continue;
                    cost[next]=g;parent[next]=n;open.Add((g+Vector2.Distance(Point(next),goal),next.x,next.y));
                }
            }
            return false;
        }
        public static Vector2 Clamp(DSMapGeometry map,Vector2 from,Vector2 requested,float radius)
        {
            if(!map.ValidCoordinate(from)||!map.SafeSweep(from,from,radius))return from;
            if(map.ValidCoordinate(requested)&&map.SafeSweep(from,requested,radius))return requested;
            float lo=0,hi=1;
            for(int i=0;i<12;i++){float t=(lo+hi)*.5f;var p=Vector2.Lerp(from,requested,t);if(map.ValidCoordinate(p)&&map.SafeSweep(from,p,radius))lo=t;else hi=t;}
            var result=Vector2.Lerp(from,requested,lo);return map.SafeSweep(from,result,radius)?result:from;
        }
    }
}
