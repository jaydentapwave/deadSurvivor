using System;
using System.Linq;
using UnityEngine;

namespace DeadSurvivor
{
    [Serializable] public class DSBlockoutObject { public string id;public Vector2[] points_px;public float height_logical; }
    [Serializable] public class DSBlockoutRoute { public string id;public Vector2[] points_px; }
    [Serializable] public class DSBlockoutData
    {
        public string package,revision,document_role,source_layout_sha256,source_image_sha256,rock_object_id;
        public Vector2 origin_px,rock_center_px;
        public Vector2[] boundary_px;
        public DSBlockoutObject[] objects;
        public DSBlockoutRoute[] routes;
        public float[] scale_profiles;
        public float world_scale,build_scale,body_radius,body_height,epsilon;
        public Vector2 Logical(Vector2 p,float scale) => (p-origin_px)*scale;
        public DSMapGeometry Geometry(float scale)
        {
            Validate();
            if(!scale_profiles.Contains(scale)) throw new ArgumentException("UNDECLARED_SCALE");
            return new DSMapGeometry(boundary_px.Select(p=>Logical(p,scale)).ToArray(),
                objects.Select(o=>o.points_px.Select(p=>Logical(p,scale)).ToArray()).ToArray(),epsilon,16384);
        }
        public void Validate()
        {
            if(package!="MAP-BLOCKOUT-018" || revision!="r001" || document_role!="PREVIEW_ONLY" ||
                source_layout_sha256?.Length!=64 || source_image_sha256?.Length!=64 || origin_px!=new Vector2(565,580) ||
                world_scale!=.02f || build_scale!=4 || body_radius!=8 || body_height!=36 || epsilon!=.001f ||
                scale_profiles==null || !scale_profiles.SequenceEqual(new[]{4f,6f,8f}) || objects==null || objects.Length!=64 ||
                !objects.Select(o=>o.id).OrderBy(s=>s).SequenceEqual(Enumerable.Range(1,64).Select(i=>"OB-"+i.ToString("000"))) ||
                objects.Any(o=>o.points_px==null || o.points_px.Length<3 || !new[]{16f,24f,32f}.Contains(o.height_logical)) ||
                rock_object_id!="OB-001" || !DSMapGeometry.ValidPoint(rock_center_px-origin_px))
                throw new ArgumentException("INVALID_BLOCKOUT_DATA");
        }
    }
    [Serializable] public class DSBlockoutFrame
    {
        public int step_id,frame_id,device_id;
        public float actual_dt,applied_dt,alpha;
        public bool held;
        public Vector2 input,before_center,after_center;
        public Vector2[] before_actual,after_actual,before_reserved,after_reserved;
    }
    [Serializable] public class DSBlockoutCameraRecord
    {
        public float elevation,orthographicSize;
        public bool overview,focusRock;
        public int width,height;
        public Vector3 position,rotation;
        public Vector3[] groundCorners,feet,heads;
    }
}
