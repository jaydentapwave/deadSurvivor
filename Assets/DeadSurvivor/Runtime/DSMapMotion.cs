using System;
using UnityEngine;

namespace DeadSurvivor
{
    public sealed class DSMotionProposal
    {
        public readonly Vector2 Center;
        public readonly Vector2[] Actual;
        public readonly float Alpha;
        public readonly bool Held;
        public DSMotionProposal(Vector2 center, Vector2[] actual, float alpha, bool held)
        { Center=center;Actual=actual;Alpha=alpha;Held=held; }
    }

    public static class DSMapMotion
    {
        public static DSMotionProposal Propose(DSMapGeometry map, Vector2 center, Vector2[] actual,
            Vector2[] offsets, bool[] alive, float[] radii, float centerRadius, Vector2 input, float dt)
        {
            if(map==null || actual==null || offsets==null || alive==null || radii==null || actual.Length<1 || actual.Length>4 ||
                offsets.Length!=actual.Length || alive.Length!=actual.Length || radii.Length!=actual.Length)
                throw new ArgumentException("INVALID_MOTION_BINDING");
            if(!map.ValidCoordinate(center) || !DSMapGeometry.ValidPoint(input) || input.sqrMagnitude>1.000001f ||
                !DSMapGeometry.Finite(dt) || dt<0 || dt>1 || !DSMapGeometry.Finite(centerRadius) || centerRadius<=0)
                throw new ArgumentException("INVALID_MOTION_REQUEST");
            if(!map.SafeSweep(center,center,centerRadius)) throw new InvalidOperationException("INVALID_INITIAL_OCCUPANCY");
            for(int i=0;i<actual.Length;i++)
            {
                if(!map.ValidCoordinate(actual[i]) || !map.ValidCoordinate(offsets[i])) throw new ArgumentException("INVALID_BODY");
                if(!map.SafeSweep(center+offsets[i],center+offsets[i],radii[i]) ||
                    alive[i] && !map.SafeSweep(actual[i],actual[i],radii[i])) throw new InvalidOperationException("INVALID_INITIAL_OCCUPANCY");
            }
            for(int j=256;j>=0;j--)
            {
                float alpha=j/256f;
                Vector2 next=center+input*(140*dt*alpha);
                var result=(Vector2[])actual.Clone();
                bool Safe(Vector2 from,Vector2 to,float r) => map.ValidCoordinate(to) && map.SafeSweep(from,to,r);
                bool safe=Safe(center,next,centerRadius);
                for(int i=0;i<actual.Length && safe;i++)
                {
                    Vector2 target=next+offsets[i];
                    safe=Safe(center+offsets[i],target,radii[i]);
                    if(alive[i])
                    {
                        result[i]=Vector2.Lerp(actual[i],target,Mathf.Min(1,12*dt));
                        safe &= Safe(actual[i],result[i],radii[i]);
                    }
                }
                if(safe) return new DSMotionProposal(next,result,alpha,false);
            }
            return new DSMotionProposal(center,(Vector2[])actual.Clone(),0,true);
        }
    }
}
