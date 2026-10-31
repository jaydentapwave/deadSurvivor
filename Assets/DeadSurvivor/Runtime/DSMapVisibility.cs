using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace DeadSurvivor
{
    // Bounds are a deliberately conservative visual heuristic; collision is never changed.
    public sealed class DSMapVisibility : MonoBehaviour
    {
        public Camera ViewCamera;
        public Renderer[] Blockers=Array.Empty<Renderer>();
        public DSMapBlockoutPreview Preview;
        public float FadedAlpha=.22f;
        public int FadedCount {get;private set;}
        public Vector3[] Focus=Array.Empty<Vector3>();
        readonly List<Material[]> originals=new(), copies=new();
        bool initialized;
        void Initialize()
        {
            if(initialized)return;
            foreach(var renderer in Blockers)
            {
                var source=renderer.sharedMaterials;originals.Add(source);
                var mats=Array.ConvertAll(source,m=>new Material(m));copies.Add(mats);
                foreach(var m in mats)
                {
                    m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=(int)RenderQueue.Transparent;
                    m.SetShaderPassEnabled("ShadowCaster",false);
                }

            }
            initialized=true;
        }
        public void Refresh()
        {
            if(!ViewCamera)return;
            Initialize();FadedCount=0;
            var points=Focus;
            if(Preview && Preview.Ready)points=Array.ConvertAll(Preview.Actual,p=>DSBlockoutMesh.World(p,Preview.Data.world_scale)+Vector3.up*.36f);
            for(int i=0;i<Blockers.Length;i++)
            {
                bool faded=false;
                foreach(var p in points)
                {
                    Vector3 delta=p-ViewCamera.transform.position;
                    if(delta.sqrMagnitude>0 && Blockers[i].bounds.IntersectRay(new Ray(ViewCamera.transform.position,delta.normalized),out float d) && d<delta.magnitude-.01f)
                    {faded=true;break;}
                }
                if(faded)FadedCount++;
                Blockers[i].sharedMaterials=faded?copies[i]:originals[i];
                for(int j=0;j<copies[i].Length;j++)
                {
                    Color c=originals[i][j].GetColor("_BaseColor");c.a*=faded?FadedAlpha:1;
                    copies[i][j].SetColor("_BaseColor",c);
                }
            }
        }
        void LateUpdate()=>Refresh();
        void OnDestroy()
        {
            if(!initialized)return;
            for(int i=0;i<Blockers.Length;i++)
            {
                if(Blockers[i])Blockers[i].sharedMaterials=originals[i];
                foreach(var m in copies[i])if(m)Destroy(m);
            }
        }
    }
}
