using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace DeadSurvivor
{
    public static class DSBlockoutMesh
    {
        public static double Area(Vector2[] p)
        {
            double a=0;for(int i=0;i<p.Length;i++){var b=p[(i+1)%p.Length];a+=(double)p[i].x*b.y-(double)p[i].y*b.x;}return a/2;
        }
        static double Cross(Vector2 a,Vector2 b,Vector2 c) => ((double)b.x-a.x)*((double)c.y-a.y)-((double)b.y-a.y)*((double)c.x-a.x);
        public static int[] Triangulate(Vector2[] polygon)
        {
            // Geometry validates the original polygon, including folds and crossings.
            new DSMapGeometry(polygon,Array.Empty<Vector2[]>(),.001,16384);
            var remaining=Enumerable.Range(0,polygon.Length).ToList();
            if(Area(polygon)<0) remaining.Reverse();
            var result=new List<int>();
            while(remaining.Count>3)
            {
                bool removed=false;
                for(int n=0;n<remaining.Count;n++)
                {
                    int a=remaining[(n+remaining.Count-1)%remaining.Count],b=remaining[n],c=remaining[(n+1)%remaining.Count];
                    if(Cross(polygon[a],polygon[b],polygon[c])<=0) continue;
                    bool contains=false;
                    foreach(int i in remaining)
                        if(i!=a && i!=b && i!=c && Cross(polygon[a],polygon[b],polygon[i])>=0 &&
                            Cross(polygon[b],polygon[c],polygon[i])>=0 && Cross(polygon[c],polygon[a],polygon[i])>=0)
                        {contains=true;break;}
                    if(contains) continue;
                    result.AddRange(new[]{a,b,c});remaining.RemoveAt(n);removed=true;break;
                }
                if(!removed) throw new ArgumentException("TRIANGULATION_FAILED");
            }
            if(Cross(polygon[remaining[0]],polygon[remaining[1]],polygon[remaining[2]])<=0) throw new ArgumentException("DEGENERATE_CAP");
            result.AddRange(remaining);return result.ToArray();
        }
        public static Vector3 World(Vector2 q,float k,float height=0) => new Vector3(q.x*k,height*k,-q.y*k);
        public static Mesh Prism(Vector2[] points,float k,float height,bool ground=false)
        {
            if(!DSMapGeometry.Finite(k) || k<=0 || !DSMapGeometry.Finite(height) || height<0) throw new ArgumentException("INVALID_MESH_SCALE");
            var p=(Vector2[])points.Clone();if(Area(p)<0)Array.Reverse(p);
            int n=p.Length;var cap=Triangulate(p);
            var v=new List<Vector3>();var t=new List<int>();
            foreach(var q in p)v.Add(World(q,k,ground?height:0));
            if(ground) t.AddRange(cap);
            else
            {
                if(height<=0)throw new ArgumentException("INVALID_PRISM_HEIGHT");
                foreach(var q in p)v.Add(World(q,k,height));
                for(int i=0;i<cap.Length;i+=3)
                {
                    t.AddRange(new[]{cap[i+2],cap[i+1],cap[i]});
                    t.AddRange(new[]{cap[i]+n,cap[i+1]+n,cap[i+2]+n});
                }
                for(int i=0;i<n;i++)
                {
                    int j=(i+1)%n,b=v.Count;
                    v.AddRange(new[]{World(p[i],k),World(p[j],k),World(p[j],k,height),World(p[i],k,height)});
                    t.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});
                }
            }
            var mesh=new Mesh {name=ground?"Blockout ground":"Blockout prism"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Mesh Perimeter(Vector2[] points,float k,float height)
        {
            var p=(Vector2[])points.Clone();if(Area(p)<0)Array.Reverse(p);
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<p.Length;i++)
            {
                int n=v.Count,j=(i+1)%p.Length;
                v.AddRange(new[]{World(p[i],k),World(p[j],k),World(p[j],k,height),World(p[i],k,height)});
                t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            var m=new Mesh {name="PERIM-01"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        public static Mesh Cylinder(float radius,float height)
        {
            var points=Enumerable.Range(0,16).Select(i=>new Vector2(Mathf.Cos(i*Mathf.PI/8)*radius,Mathf.Sin(i*Mathf.PI/8)*radius)).ToArray();
            return Prism(points,1,height);
        }
    }
}
