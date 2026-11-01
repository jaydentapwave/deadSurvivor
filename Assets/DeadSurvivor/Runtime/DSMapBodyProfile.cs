using System;
using System.Linq;
namespace DeadSurvivor
{
    // Logical collision units. This is independent of image/world display scales.
    public sealed class DSMapBodyProfile
    {
        readonly float[] radii;
        public static readonly DSMapBodyProfile Default8=new(new[]{8f,8f,8f,8f},0);
        public int FixedCount {get;}
        public float CenterRadius {get;}
        public bool IsDefault8=>FixedCount==0;
        DSMapBodyProfile(float[] values,int fixedCount)
        {
            if(values==null||values.Length<1||values.Length>4||values.Any(v=>!DSMapGeometry.Finite(v)||v<=0||v>1024)||fixedCount!=0&&fixedCount!=values.Length)throw new ArgumentException("MAP_BODY_PROFILE_INVALID");
            radii=(float[])values.Clone();FixedCount=fixedCount;CenterRadius=radii.Max();
        }
        public static DSMapBodyProfile Fixture(float[] values)=>new(values,values?.Length??-1);
        public bool Supports(int count)=>count>=1&&count<=4&&(FixedCount==0||FixedCount==count);
        public float Radius(int slot,int count)
        {if(!Supports(count)||slot<0||slot>=count)throw new InvalidOperationException("MAP_BODY_SLOT_MISMATCH");return radii[slot];}
        public float[] CopyRadii(int count)
        {if(!Supports(count))throw new InvalidOperationException("MAP_BODY_SLOT_MISMATCH");return Enumerable.Range(0,count).Select(i=>radii[i]).ToArray();}
    }
}
