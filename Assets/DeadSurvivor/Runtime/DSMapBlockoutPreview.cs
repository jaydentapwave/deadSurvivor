using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Rendering.Universal;

namespace DeadSurvivor
{
    // Separate, labelled authoring preview. It never starts a DSGame battle or state transition.
    public sealed class DSMapBlockoutPreview : MonoBehaviour
    {
        public TextAsset Layout;
        public string ExpectedDataSha;
        public Transform StaticGeometry,RockHolder;
        public Vector3 RockPlacementOffset;
        public Camera ViewCamera;
        public float ImageScale=4,Elevation=55;
        public int Formation;
        public bool Overview=true,FocusRock;
        public Vector2 InitialCenter;
        public bool Record;
        public int RecordSteps=30,ExpectedDevice;
        public bool FixedInput;
        public Vector2 TestInput=Vector2.right;
        public float TestDt=.05f;
        public int ReverseAt=-1;
        public readonly List<DSBlockoutFrame> Frames=new();
        public string Error { get; private set; }="";
        public bool Ready { get; private set; }
        public bool Finished { get; private set; }
        public Vector2 Center {get;private set;}
        public Vector2[] Actual {get;private set;}
        public DSBlockoutData Data {get;private set;}
        public DSMapGeometry Map {get;private set;}
        public event Action Completed;
        readonly List<UnityEngine.Object> owned=new();
        Transform[] actors;
        Vector2[] slots;
        float boundScale;
        int boundFormation;
        TMP_FontAsset previewFont;
        HashSet<int> sealedObjects;
        readonly Dictionary<Transform,Matrix4x4> immutableTransforms=new();
        TextMeshProUGUI status;
        Canvas overlay;
        bool frozen;
        int deviceWarmup;
        public static string Hash(string text)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();
        }
        void Start() {if(!Ready && Error=="") Initialize();}
        public void Initialize()
        {
            if(Ready)throw new InvalidOperationException("ALREADY_INITIALIZED");
            try
            {
                if(!Layout || Hash(Layout.text)!=ExpectedDataSha || !StaticGeometry || !RockHolder || !ViewCamera ||
                    transform.position!=Vector3.zero || transform.rotation!=Quaternion.identity || transform.lossyScale!=Vector3.one || Formation<0 || Formation>2)
                    throw new ArgumentException("PREVIEW_INPUT_MISMATCH");
                Data=JsonUtility.FromJson<DSBlockoutData>(Layout.text);Data.Validate();Map=Data.Geometry(ImageScale);
                boundScale=ImageScale;boundFormation=Formation;
                slots=Enumerable.Range(0,4).Select(i=>DSGame.FormationOffset(Formation,i,4)).ToArray();
                Center=InitialCenter;Actual=slots.Select(p=>p+Center).ToArray();
                DSMapMotion.Propose(Map,Center,Actual,slots,Enumerable.Repeat(true,4).ToArray(),Enumerable.Repeat(Data.body_radius,4).ToArray(),Data.body_radius,Vector2.zero,0);
                StaticGeometry.localScale=new Vector3(ImageScale/Data.build_scale,1,ImageScale/Data.build_scale);
                RockHolder.localPosition=DSBlockoutMesh.World(Data.Logical(Data.rock_center_px,ImageScale),Data.world_scale)+RockPlacementOffset;
                if(Record && (RecordSteps<1 || RecordSteps>10000 || !DSMapGeometry.Finite(TestDt) || TestDt<0 || TestDt>.25 || TestInput.sqrMagnitude>1.000001f))
                    throw new ArgumentException("INVALID_RECORDING_REQUEST");
                var party=new GameObject("PreviewParty").transform;party.SetParent(transform,false);
                actors=new Transform[4];
                var colors=new[]{new Color(.95f,.65f,.3f),new Color(.4f,.7f,.9f),new Color(.55f,.8f,.4f),new Color(.85f,.5f,.65f)};
                var mesh=DSBlockoutMesh.Cylinder(Data.body_radius*Data.world_scale,Data.body_height*Data.world_scale);owned.Add(mesh);
                for(int i=0;i<4;i++)
                {
                    var go=new GameObject("PartySlot"+i,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(party,false);
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=colors[i];owned.Add(mat);
                    go.GetComponent<MeshRenderer>().sharedMaterial=mat;actors[i]=go.transform;
                }
                // URP otherwise adds this lazily during the first render, after ownership is sealed.
                foreach(var light in GetComponentsInChildren<Light>(true))light.GetUniversalAdditionalLightData();
                CreateOverlay();UpdateActors();FrameCamera();
                foreach(var t in StaticGeometry.GetComponentsInChildren<Transform>(true).Concat(RockHolder.GetComponentsInChildren<Transform>(true))) immutableTransforms[t]=t.localToWorldMatrix;
                foreach(var text in GetComponentsInChildren<TextMeshProUGUI>(true))text.ForceMeshUpdate(true,true);
                sealedObjects=GetComponentsInChildren<Component>(true).Select(c=>c.GetInstanceID()).ToHashSet();
                Ready=true;
            }
            catch(Exception e) {sealedObjects=GetComponentsInChildren<Component>(true).Select(c=>c.GetInstanceID()).ToHashSet();Fail(e.Message);}
        }
        public void AssertOwnedTree()
        {
            foreach(var pair in immutableTransforms)
                if(!pair.Key || pair.Key.localToWorldMatrix!=pair.Value)throw new InvalidOperationException("PREVIEW_TRANSFORM_CONFLICT");
            if(actors!=null && Actual!=null)
                for(int i=0;i<actors.Length;i++)if(actors[i].localPosition!=DSBlockoutMesh.World(Actual[i],Data.world_scale))throw new InvalidOperationException("PREVIEW_ACTOR_DISPLAY_CONFLICT");
            if(sealedObjects!=null && !sealedObjects.SetEquals(GetComponentsInChildren<Component>(true).Select(c=>c.GetInstanceID())))
                throw new InvalidOperationException("PREVIEW_TREE_CONFLICT");
        }
        void Update()
        {
            if(!Ready || Error!="") return;
            try
            {
                AssertOwnedTree();
                if(ImageScale!=boundScale || Formation!=boundFormation)throw new InvalidOperationException("PROFILE_CHANGED_RESTART_REQUIRED");
                var keyboard=Keyboard.current;
                if(!Record)
                {
                    if(keyboard!=null && keyboard.tabKey.wasPressedThisFrame) Overview=!Overview;
                    if(keyboard!=null && keyboard.cKey.wasPressedThisFrame) Elevation=Elevation==45?55:45;
                    if(keyboard!=null && keyboard.vKey.wasPressedThisFrame) {FocusRock=!FocusRock;Overview=false;}
                }
                if(!frozen)
                {
                    if(Record && !FixedInput && (keyboard==null || keyboard.deviceId!=ExpectedDevice)) throw new InvalidOperationException("INPUT_OWNER_MISMATCH");
                    if(Record && FixedInput && keyboard!=null && keyboard.anyKey.isPressed) throw new InvalidOperationException("EXTERNAL_INPUT");
                    var expected=ReverseAt>=0 && Frames.Count>=ReverseAt?-TestInput:TestInput;
                    var input=Record && FixedInput ? expected : Vector2.ClampMagnitude(DSGame.ReadKeyboardMovement(keyboard),1);
                    if(Record && !FixedInput && input!=expected)
                    {
                        if(Frames.Count==0 && deviceWarmup++<10)return;
                        throw new InvalidOperationException("DEVICE_INPUT_MISMATCH");
                    }
                    float dt=Mathf.Min(Record && FixedInput?TestDt:Time.unscaledDeltaTime,.05f);
                    var before=Center;var actual=(Vector2[])Actual.Clone();var reserved=slots.Select(p=>p+Center).ToArray();
                    var proposal=DSMapMotion.Propose(Map,Center,Actual,slots,Enumerable.Repeat(true,4).ToArray(),Enumerable.Repeat(Data.body_radius,4).ToArray(),Data.body_radius,input,dt);
                    Center=proposal.Center;Actual=proposal.Actual;
                    if(Record)
                    {
                        Frames.Add(new DSBlockoutFrame {step_id=Frames.Count,frame_id=Time.frameCount,device_id=keyboard?.deviceId??0,
                            actual_dt=Time.unscaledDeltaTime,applied_dt=dt,input=input,alpha=proposal.Alpha,held=proposal.Held,
                            before_center=before,after_center=Center,before_actual=actual,after_actual=(Vector2[])Actual.Clone(),before_reserved=reserved,after_reserved=slots.Select(p=>p+Center).ToArray()});
                        if(Frames.Count==RecordSteps) {frozen=true;Finished=true;}
                    }
                }
                UpdateActors();FrameCamera();
                if(status) status.text=$"MAP 018 / PREVIEW ONLY     S {ImageScale} / K {Data.world_scale}     CAMERA {(Overview?90:Elevation)}     64 CANDIDATE FOOTPRINTS";
                if(Finished) {Finished=false;Completed?.Invoke();}
            }
            catch(Exception e) {Fail(e.Message);}
        }
        public void Freeze() {frozen=true;}
        public void CancelRecording() {if(Record && !frozen)Fail("CANCELLED");}
        void Fail(string error) {Error=error;frozen=true;Completed?.Invoke();}
        void UpdateActors() {for(int i=0;i<4;i++)actors[i].localPosition=DSBlockoutMesh.World(Actual[i],Data.world_scale);}
        public void FrameCamera()
        {
            if(Data==null)return;
            float angle=Overview?90:Elevation;
            if(angle!=45 && angle!=55 && angle!=90)throw new ArgumentException("INVALID_CAMERA_ELEVATION");
            var target=FocusRock?DSBlockoutMesh.World(Data.Logical(Data.rock_center_px,ImageScale),Data.world_scale):DSBlockoutMesh.World(Center,Data.world_scale);
            ViewCamera.orthographic=true;ViewCamera.nearClipPlane=.1f;ViewCamera.farClipPlane=500;
            ViewCamera.transform.rotation=Quaternion.Euler(angle,0,0);
            if(Overview)
            {
                var points=Data.boundary_px.Select(p=>DSBlockoutMesh.World(Data.Logical(p,ImageScale),Data.world_scale)).ToArray();
                var min=new Vector3(points.Min(p=>p.x),0,points.Min(p=>p.z));var max=new Vector3(points.Max(p=>p.x),0,points.Max(p=>p.z));
                target=(min+max)*.5f;
                ViewCamera.orthographicSize=Mathf.Max((max.z-min.z)/2,(max.x-min.x)/(2*ViewCamera.aspect))*1.25f;
            }
            else ViewCamera.orthographicSize=6;
            float distance=150;
            ViewCamera.transform.position=target-ViewCamera.transform.forward*distance;
        }
        public DSBlockoutCameraRecord CameraEvidence()
        {
            var corners=new List<Vector3>();
            foreach(var v in new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)})
            {
                var ray=ViewCamera.ViewportPointToRay(v);float t=-ray.origin.y/ray.direction.y;
                if(!DSMapGeometry.Finite(t) || t<0)throw new InvalidOperationException("INVALID_GROUND_FRUSTUM");corners.Add(ray.GetPoint(t));
            }
            return new DSBlockoutCameraRecord {elevation=Overview?90:Elevation,overview=Overview,focusRock=FocusRock,orthographicSize=ViewCamera.orthographicSize,
                width=ViewCamera.pixelWidth,height=ViewCamera.pixelHeight,position=ViewCamera.transform.position,rotation=ViewCamera.transform.eulerAngles,
                groundCorners=corners.ToArray(),feet=Actual.Select(p=>ViewCamera.WorldToScreenPoint(DSBlockoutMesh.World(p,Data.world_scale))).ToArray(),
                heads=Actual.Select(p=>ViewCamera.WorldToScreenPoint(DSBlockoutMesh.World(p,Data.world_scale,Data.body_height))).ToArray()};
        }
        void CreateOverlay()
        {
            var source=Resources.Load<Font>("DeadSurvivor/UI/NotoSansKR");
            if(!source)throw new InvalidOperationException("PREVIEW_FONT_SOURCE_MISSING");
            previewFont=TMP_FontAsset.CreateFontAsset(source,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,true);
            if(!previewFont)throw new InvalidOperationException("PREVIEW_FONT_CREATION_FAILED");
            owned.Add(previewFont);
            var go=new GameObject("PreviewOverlay",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));go.transform.SetParent(transform,false);
            overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);
            void Label(string name,string text,bool top)
            {
                var panel=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(go.transform,false);
                var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,top?1:0);rect.anchorMax=new Vector2(1,top?1:0);rect.pivot=new Vector2(.5f,top?1:0);rect.sizeDelta=new Vector2(0,62);
                var image=panel.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.04f,.07f,.08f,.93f);image.raycastTarget=false;
                var label=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(panel.transform,false);
                var tr=label.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(24,6);tr.offsetMax=new Vector2(-24,-6);
                var tmp=label.GetComponent<TextMeshProUGUI>();tmp.font=previewFont;tmp.fontSize=22;tmp.color=new Color(.86f,.93f,.75f);tmp.alignment=TextAlignmentOptions.MidlineLeft;tmp.text=text;tmp.raycastTarget=false;
                if(top)status=tmp;
            }
            Label("Status","MAP 018 / PREVIEW ONLY",true);
            Label("Controls","WASD / ARROWS: MOVE     TAB: MAP / FOLLOW     C: 45 / 55     V: ROCK / PARTY     COLLISION + SCALE: CANDIDATES",false);
        }
        void OnDestroy()
        {
            if(previewFont)
            {
                if(previewFont.material)Destroy(previewFont.material);
                foreach(var atlas in previewFont.atlasTextures)if(atlas)Destroy(atlas);
            }
            foreach(var obj in owned)if(obj)Destroy(obj);
        }
    }
}
