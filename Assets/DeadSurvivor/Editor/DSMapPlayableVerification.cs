using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace DeadSurvivor.Editor
{
    public static class DSMapPlayableVerification
    {
        public const string Folder="Assets/DeadSurvivor/Scenes/Map021";
        public const string Calibration=Folder+"/Map021_Calibration.unity";
        const string Source=DSMapBlockoutVerification.PrefabPath;
        public static string BuildCalibration()
        {
            var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
            if(File.Exists(Calibration))throw new Exception("DESTINATION_EXISTS");
            try
            {
                if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/DeadSurvivor/Scenes","Map021");
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
                PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var p=root.GetComponent<DSMapBlockoutPreview>();p.Overview=false;p.Elevation=55;p.ImageScale=4;
                AddVisibility(root,p);
                if(!EditorSceneManager.SaveScene(scene,Calibration))throw new Exception("SAVE_FAILED");
                return Calibration;
            }
            finally {Restore(saved);}
        }
        static void Restore(DSMapMotionVerification.SnapshotData s)
        {
            EditorSceneManager.RestoreSceneManagerSetup(s.scenes.Select(x=>new SceneSetup{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive}).ToArray());
            Selection.objects=s.selection.Select(x=>{if(!GlobalObjectId.TryParse(x,out var id))throw new Exception("INVALID_SELECTION");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);}).ToArray();
        }
        public static DSMapVisibility AddVisibility(GameObject root,DSMapBlockoutPreview p)
        {
            var v=root.AddComponent<DSMapVisibility>();v.ViewCamera=p.ViewCamera;v.Preview=p;
            v.Blockers=p.StaticGeometry.GetComponentsInChildren<Renderer>().Where(x=>x.name.StartsWith("OB-",StringComparison.Ordinal)).Concat(p.RockHolder.GetComponentsInChildren<Renderer>()).ToArray();
            if(v.Blockers.Length<64)throw new Exception("BLOCKER_SET_INCOMPLETE");return v;
        }
        public static string Calibrate(string run,string nonce,int scale,int angle)
        {
            var wrapper=DSMapMotionVerification.CreateExternalRoot(run,nonce,"Blockout018_"+nonce);
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source),wrapper.transform);obj.SetActive(false);
            var p=obj.GetComponent<DSMapBlockoutPreview>();p.ImageScale=scale;p.Elevation=angle;p.Overview=false;
            var data=JsonUtility.FromJson<DSBlockoutData>(p.Layout.text);
            p.InitialCenter=new Vector2((data.rock_center_px.x-data.origin_px.x)*scale,(data.objects[0].points_px.Min(x=>x.y)-data.origin_px.y)*scale-70);
            var visibility=AddVisibility(obj,p);wrapper.SetActive(true);obj.SetActive(true);p.Initialize();p.Freeze();p.enabled=false;visibility.Refresh();
            if(p.Error!="")throw new Exception(p.Error);
            DSMapMotionVerification.GuardExternalRoot(run,nonce,wrapper,()=>p.AssertOwnedTree(),()=>{});
            return InspectCalibration();
        }
        public static string VisibilityTests(string run,string nonce)
        {
            var root=DSMapMotionVerification.CreateExternalRoot(run,nonce,"Blockout018_"+nonce);root.SetActive(true);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(root.transform,false);
            var renderer=cube.GetComponent<Renderer>();var original=new Material(Shader.Find("Universal Render Pipeline/Lit"));renderer.sharedMaterial=original;
            var cameraObject=new GameObject("TestCamera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);
            var cam=cameraObject.GetComponent<Camera>();cam.transform.position=new Vector3(0,0,-10);
            var v=root.AddComponent<DSMapVisibility>();v.ViewCamera=cam;v.Blockers=new[]{renderer};v.Focus=new[]{new Vector3(0,0,5)};
            v.Refresh();if(v.FadedCount!=1 || renderer.sharedMaterial==original || Mathf.Abs(renderer.sharedMaterial.color.a-.22f)>.001f)throw new Exception("FADE_TRIGGER");
            v.Focus=Array.Empty<Vector3>();v.Refresh();if(v.FadedCount!=0 || renderer.sharedMaterial!=original || original.GetFloat("_ZWrite")!=1)throw new Exception("FADE_CLEAR");
            v.Focus=new[]{new Vector3(0,0,5)};v.Refresh();UnityEngine.Object.DestroyImmediate(v);
            if(renderer.sharedMaterial!=original || original.color.a!=1)throw new Exception("FADE_DISPOSE");
            DSMapMotionVerification.GuardExternalRoot(run,nonce,root,()=>{},()=>UnityEngine.Object.DestroyImmediate(original));
            return "PASS_TRIGGER_CLEAR_DISPOSE";
        }
        public static string InspectCalibration()
        {
            var p=UnityEngine.Object.FindFirstObjectByType<DSMapBlockoutPreview>();
            var v=p.GetComponent<DSMapVisibility>();v.Refresh();
            return JsonUtility.ToJson(new CalibrationRecord {frame=Time.frameCount,scale=p.ImageScale,camera=p.CameraEvidence(),faded=v.FadedCount,blockers=v.Blockers.Length,rock=v.Blockers.Last().bounds.size});
        }
        [Serializable] public class CalibrationRecord {public int frame,faded,blockers;public float scale;public Vector3 rock;public DSBlockoutCameraRecord camera;}
    }
}
