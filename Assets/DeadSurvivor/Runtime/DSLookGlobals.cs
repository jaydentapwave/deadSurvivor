using UnityEngine;
namespace DeadSurvivor
{
    // 045: sets the 043 shader's global look values for this scene while enabled, and clears them on disable
    // (globals persist across scenes in the editor). Values default to the 043 day capture.
    public sealed class DSLookGlobals : MonoBehaviour
    {
        public Vector4 ShadowTint=new(.80f,.88f,1f,.35f);
        public Vector4 CloudShadow=new(.45f,5.5f,.02f,0);
        public Vector4 CloudDir=new(1f,.35f,0,0);
        public Vector4 GradeTint=Vector4.zero;
        static readonly int Shadow=Shader.PropertyToID("_DS_ShadowTint"),Cloud=Shader.PropertyToID("_DS_CloudShadow"),
            Dir=Shader.PropertyToID("_DS_CloudDir"),Grade=Shader.PropertyToID("_DS_GradeTint");
        void OnEnable()=>Apply();
        void OnDisable()=>Set(Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero);
        public void Apply()=>Set(ShadowTint,CloudShadow,CloudDir,GradeTint);
        static void Set(Vector4 s,Vector4 c,Vector4 d,Vector4 g)
        {
            Shader.SetGlobalVector(Shadow,s);Shader.SetGlobalVector(Cloud,c);Shader.SetGlobalVector(Dir,d);Shader.SetGlobalVector(Grade,g);
        }
    }
}
