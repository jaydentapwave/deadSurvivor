using UnityEngine;
using UnityEngine.InputSystem;
namespace DeadSurvivor
{
    // 047: day <-> night look for map scenes (presentation only). Night values are the 043 night capture
    // (docs/design/lowpoly-shader-043.md): dim blue sun, blue trilight ambient, stronger blue shadow tint, grade tint.
    // A warm point light follows the party at night so heroes stay readable. Toggle: key N in battle, or the settings
    // button (DSUI). Day values are read from the scene at start, so the day look is unchanged when night is off.
    [DisallowMultipleComponent]
    public sealed class DSDayNight : MonoBehaviour
    {
        public Light Sun;
        public DSLookGlobals Look;
        public float BlendSeconds=1.5f;
        public Color NightSunColor=new Color32(0x9F,0xB6,0xE8,255);
        public float NightSunIntensity=.2f;
        public Color NightSky=new Color32(0x3A,0x4C,0x74,255),NightEquator=new Color32(0x2A,0x36,0x52,255),NightGround=new Color32(0x16,0x1C,0x28,255);
        public Vector4 NightShadowTint=new(.55f,.66f,1f,.6f),NightGradeTint=new(.55f,.78f,.95f,.6f);
        public Color LanternColor=new Color32(0xFF,0xC0,0x70,255);
        public float LanternIntensity=3.2f,LanternRange=5.5f;
        public bool Night {get;private set;}
        float k;   // 0 day .. 1 night
        bool captured;Color daySun,daySky,dayEquator,dayGround,dayFlat;float daySunIntensity;Vector4 dayShadow,dayCloud,dayGrade;
        Light lantern;DSGame game;
        static readonly int Shadow=Shader.PropertyToID("_DS_ShadowTint"),Cloud=Shader.PropertyToID("_DS_CloudShadow"),Grade=Shader.PropertyToID("_DS_GradeTint");

        public void Toggle()=>Night=!Night;
        public void SetNight(bool on)=>Night=on;
        void Capture()
        {
            if(captured)return;captured=true;
            if(!Sun)foreach(var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))if(l.type==LightType.Directional){Sun=l;break;}
            if(!Look)Look=GetComponent<DSLookGlobals>()??FindAnyObjectByType<DSLookGlobals>();
            if(Sun){daySun=Sun.color;daySunIntensity=Sun.intensity;}
            dayFlat=RenderSettings.ambientLight;daySky=RenderSettings.ambientSkyColor;dayEquator=RenderSettings.ambientEquatorColor;dayGround=RenderSettings.ambientGroundColor;
            dayShadow=Look?Look.ShadowTint:Vector4.zero;dayCloud=Look?Look.CloudShadow:Vector4.zero;dayGrade=Look?Look.GradeTint:Vector4.zero;
            var go=new GameObject("PartyLantern047");go.transform.SetParent(transform,false);lantern=go.AddComponent<Light>();
            lantern.type=LightType.Point;lantern.color=LanternColor;lantern.range=LanternRange;lantern.shadows=LightShadows.None;lantern.intensity=0;lantern.enabled=false;
            game=GetComponentInParent<DSGame>()??FindAnyObjectByType<DSGame>();
        }
        void Start()=>Capture();
        void Update()
        {
            Capture();
            var kb=Keyboard.current;
            if(kb!=null&&kb.nKey.wasPressedThisFrame&&game&&game.State=="battle")Toggle();
            float target=Night?1:0;if(k==target)return;
            k=Mathf.MoveTowards(k,target,Time.unscaledDeltaTime/Mathf.Max(.01f,BlendSeconds));
            Apply();
        }
        void LateUpdate()
        {
            if(!lantern||!lantern.enabled||!game||!game.MapSession)return;
            lantern.transform.position=game.MapSession.World(game.Center)+Vector3.up*1.4f;
        }
        void Apply()
        {
            if(Sun){Sun.color=Color.Lerp(daySun,NightSunColor,k);Sun.intensity=Mathf.Lerp(daySunIntensity,NightSunIntensity,k);}
            if(RenderSettings.ambientMode==UnityEngine.Rendering.AmbientMode.Flat)RenderSettings.ambientLight=Color.Lerp(dayFlat,NightEquator,k);
            RenderSettings.ambientSkyColor=Color.Lerp(daySky,NightSky,k);RenderSettings.ambientEquatorColor=Color.Lerp(dayEquator,NightEquator,k);RenderSettings.ambientGroundColor=Color.Lerp(dayGround,NightGround,k);
            Shader.SetGlobalVector(Shadow,Vector4.Lerp(dayShadow,NightShadowTint,k));
            Shader.SetGlobalVector(Cloud,Vector4.Lerp(dayCloud,Vector4.zero,k));
            Shader.SetGlobalVector(Grade,Vector4.Lerp(dayGrade,NightGradeTint,k));
            lantern.enabled=k>0;lantern.intensity=LanternIntensity*k;
        }
        void OnDisable()
        {
            if(!captured)return;
            k=0;Apply();   // leave the day look (and the globals DSLookGlobals owns) as the scene had them
            if(Look&&Look.isActiveAndEnabled)Look.Apply();
        }
    }
}
