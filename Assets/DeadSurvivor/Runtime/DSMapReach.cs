using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace DeadSurvivor
{
    // 046: connected regions of the navigation grid (same 32-unit cells and swept-disc rule as DSMapNavigation), built once
    // per geometry and body radius. Spawn checks ask "is this point in the same region as a living hero" here instead of
    // running a budgeted path search per spawn (045 S3: late-wave spawns failed on the search limit, not on reachability).
    // Kept outside DSGame/DSMapNavigation instance fields on purpose: their field sets are pinned by the 031 snapshot contract.
    public sealed class DSMapReach
    {
        static readonly ConditionalWeakTable<DSMapGeometry,Dictionary<float,DSMapReach>> cache=new();
        public static DSMapReach For(DSMapGeometry map,float radius)
        {
            var byRadius=cache.GetOrCreateValue(map);
            if(!byRadius.TryGetValue(radius,out var reach))byRadius[radius]=reach=new DSMapReach(map,radius);
            return reach;
        }

        const float Cell=32;
        readonly DSMapGeometry map;readonly float radius;
        readonly int x0,y0,w,h;readonly int[] label;   // -1 blocked, >=0 region id
        public int Regions {get;}
        public float BuildMilliseconds {get;}

        DSMapReach(DSMapGeometry geometry,float bodyRadius)
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            map=geometry;radius=bodyRadius;
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            foreach(var p in map.Boundary){minX=Mathf.Min(minX,p.x);minY=Mathf.Min(minY,p.y);maxX=Mathf.Max(maxX,p.x);maxY=Mathf.Max(maxY,p.y);}
            x0=Mathf.FloorToInt(minX/Cell)-1;y0=Mathf.FloorToInt(minY/Cell)-1;
            w=Mathf.CeilToInt(maxX/Cell)+2-x0;h=Mathf.CeilToInt(maxY/Cell)+2-y0;
            label=new int[w*h];
            for(int j=0;j<h;j++)for(int i=0;i<w;i++)label[j*w+i]=Safe(Point(i,j))?int.MaxValue:-1;
            int next=0;var queue=new Queue<int>();
            for(int s=0;s<label.Length;s++)
            {
                if(label[s]!=int.MaxValue)continue;
                label[s]=next;queue.Enqueue(s);
                while(queue.Count>0)
                {
                    int n=queue.Dequeue();int i=n%w,j=n/w;
                    for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
                    {
                        if(dx==0&&dy==0)continue;
                        int u=i+dx,v=j+dy;if(u<0||v<0||u>=w||v>=h)continue;
                        int m=v*w+u;if(label[m]!=int.MaxValue)continue;
                        if(!map.SafeSweep(Point(i,j),Point(u,v),radius))continue;
                        label[m]=next;queue.Enqueue(m);
                    }
                }
                next++;
            }
            Regions=next;BuildMilliseconds=(float)clock.Elapsed.TotalMilliseconds;
        }
        Vector2 Point(int i,int j)=>new((i+x0)*Cell,(j+y0)*Cell);
        bool Safe(Vector2 p)=>map.ValidCoordinate(p)&&map.SafeSweep(p,p,radius);

        // Regions of the grid nodes next to p that p can sweep to directly (as DSMapNavigation.TryPath starts its search).
        void RegionsNear(Vector2 p,List<int> into)
        {
            into.Clear();
            if(!Safe(p))return;
            int ci=Mathf.RoundToInt(p.x/Cell)-x0,cj=Mathf.RoundToInt(p.y/Cell)-y0;
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++)
            {
                int i=ci+dx,j=cj+dy;if(i<0||j<0||i>=w||j>=h)continue;
                int r=label[j*w+i];if(r<0||into.Contains(r))continue;
                if(map.SafeSweep(p,Point(i,j),radius))into.Add(r);
            }
        }
        readonly List<int> ra=new(),rb=new();
        // true/false when both points touch the grid; null when either does not (caller falls back to a path search)
        public bool? Connected(Vector2 a,Vector2 b)
        {
            RegionsNear(a,ra);RegionsNear(b,rb);
            if(ra.Count==0||rb.Count==0)return null;
            foreach(var r in ra)if(rb.Contains(r))return true;
            return false;
        }
    }
}
