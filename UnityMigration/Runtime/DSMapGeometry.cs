using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadSurvivor
{
    // Logical XY geometry. Copies of the input are owned here, never by a renderer.
    public sealed class DSMapGeometry
    {
        readonly Vector2[] boundary;
        readonly Vector2[][] obstacles;
        public double Epsilon { get; }
        public double CoordinateLimit { get; }
        public Vector2[] Boundary => (Vector2[])boundary.Clone();
        public Vector2[][] Obstacles => Array.ConvertAll(obstacles, p => (Vector2[])p.Clone());

        public DSMapGeometry(Vector2[] outer, Vector2[][] solids, double epsilon, double coordinateLimit=1024)
        {
            if(!Finite(coordinateLimit) || coordinateLimit<=0 || coordinateLimit>16384) throw new ArgumentException("INVALID_COORDINATE_LIMIT");
            CoordinateLimit=coordinateLimit;
            if (!Finite(epsilon) || epsilon < .0001 || epsilon > 1)
                throw new ArgumentException("INVALID_EPSILON");
            if (solids == null || solids.Length > 128) throw new ArgumentException("INVALID_OBSTACLES");
            Epsilon = epsilon;
            boundary = ValidatePolygon(outer);
            obstacles = Array.ConvertAll(solids, ValidatePolygon);
        }

        public static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        public static bool ValidPoint(Vector2 p) => Finite(p.x) && Finite(p.y) && Math.Abs(p.x) <= 1024 && Math.Abs(p.y) <= 1024;
        public bool ValidCoordinate(Vector2 p) => Finite(p.x) && Finite(p.y) && Math.Abs(p.x)<=CoordinateLimit && Math.Abs(p.y)<=CoordinateLimit;
        static double Cross(Vector2 a, Vector2 b, Vector2 c) =>
            ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
        static bool On(Vector2 p, Vector2 a, Vector2 b) => Cross(a, b, p) == 0 &&
            p.x >= Math.Min(a.x,b.x) && p.x <= Math.Max(a.x,b.x) && p.y >= Math.Min(a.y,b.y) && p.y <= Math.Max(a.y,b.y);
        static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            double x=Cross(a,b,c), y=Cross(a,b,d), z=Cross(c,d,a), w=Cross(c,d,b);
            if (x==0 && On(c,a,b) || y==0 && On(d,a,b) || z==0 && On(a,c,d) || w==0 && On(b,c,d)) return true;
            return (x<0 && y>0 || x>0 && y<0) && (z<0 && w>0 || z>0 && w<0);
        }
        static double PointDistanceSq(Vector2 p, Vector2 a, Vector2 b)
        {
            double dx=(double)b.x-a.x, dy=(double)b.y-a.y, px=(double)p.x-a.x, py=(double)p.y-a.y;
            double n=dx*dx+dy*dy;
            double t=n==0 ? 0 : Math.Max(0,Math.Min(1,(px*dx+py*dy)/n));
            double x=px-t*dx,y=py-t*dy;
            return x*x+y*y;
        }
        static double SegmentDistanceSq(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => Intersects(a,b,c,d) ? 0 :
            Math.Min(Math.Min(PointDistanceSq(a,c,d),PointDistanceSq(b,c,d)),Math.Min(PointDistanceSq(c,a,b),PointDistanceSq(d,a,b)));
        Vector2[] ValidatePolygon(Vector2[] source)
        {
            if (source==null || source.Length<3 || source.Length>256) throw new ArgumentException("INVALID_POLYGON");
            var p=(Vector2[])source.Clone();
            var seen=new HashSet<Vector2>(); double area=0;
            for(int i=0;i<p.Length;i++)
            {
                Vector2 a=p[i],b=p[(i+1)%p.Length];
                if(!ValidCoordinate(a) || !seen.Add(a) || PointDistanceSq(a,b,b)<1e-8) throw new ArgumentException("INVALID_VERTEX");
                area+=(double)a.x*b.y-(double)a.y*b.x;
                for(int j=0;j<i;j++)
                {
                    Vector2 c=p[j],d=p[(j+1)%p.Length];
                    bool adjacent=j==i-1 || j==0 && i==p.Length-1;
                    if(!adjacent && Intersects(a,b,c,d)) throw new ArgumentException("SELF_INTERSECTION");
                    if(adjacent)
                    {
                        // Adjacent edges may meet once, but cannot fold back over each other.
                        Vector2 shared=j==i-1 ? a : b, u=j==i-1 ? b : a, v=j==i-1 ? c : d;
                        if(On(u,shared,v) || On(v,shared,u)) throw new ArgumentException("OVERLAPPING_EDGES");
                    }
                }
            }
            if(Math.Abs(area)<1e-8) throw new ArgumentException("ZERO_AREA");
            return p;
        }
        static bool Inside(Vector2 p, Vector2[] polygon)
        {
            bool inside=false;
            for(int i=0;i<polygon.Length;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Length];
                if(On(p,a,b)) return false;
                if((a.y>p.y)!=(b.y>p.y) && p.x < a.x+((double)p.y-a.y)*((double)b.x-a.x)/((double)b.y-a.y)) inside=!inside;
            }
            return inside;
        }
        public bool SafeSweep(Vector2 a, Vector2 b, double radius)
        {
            if(!ValidCoordinate(a) || !ValidCoordinate(b) || !Finite(radius) || radius<=0 || radius>1024) throw new ArgumentException("INVALID_SWEEP");
            double margin=radius+2*Epsilon;
            bool Clear(Vector2[] p)
            {
                for(int i=0;i<p.Length;i++)
                    if(SegmentDistanceSq(a,b,p[i],p[(i+1)%p.Length])<=margin*margin) return false;
                return true;
            }
            if(!Inside(a,boundary) || !Inside(b,boundary) || !Clear(boundary)) return false;
            foreach(var p in obstacles)
                if(Inside(a,p) || Inside(b,p) || !Clear(p)) return false;
            return true;
        }
    }
}
