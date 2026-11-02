using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using DeadSurvivor.Editor;
using Newtonsoft.Json;
// 043 read-only probe of the 038 scene: renderers, materials, light, volumes, render settings.
// Opens 038 without saving and restores the previous scene setup.
public static class LowPolyProbe043
{
 public static object Run()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("DIRTY_SCENE_OPEN");
  var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
  try{
   EditorSceneManager.OpenScene("Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003/Map038_TripoPair.unity",OpenSceneMode.Single);
   var rs=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
   var groups=rs.GroupBy(r=>new{type=r.GetType().Name,enabled=r.enabled&&r.gameObject.activeInHierarchy,mats=string.Join("|",r.sharedMaterials.Select(m=>m?m.name+"("+(m.shader?m.shader.name:"null")+")":"null"))})
     .Select(g=>new{g.Key.type,g.Key.enabled,g.Key.mats,count=g.Count(),examples=g.Take(3).Select(r=>r.transform.parent?r.transform.parent.name+"/"+r.name:r.name).ToArray()}).OrderByDescending(x=>x.count).ToArray();
   var light=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Select(l=>new{l.name,type=l.type.ToString(),color=ColorUtility.ToHtmlStringRGB(l.color),l.intensity,rot=l.transform.eulerAngles,shadows=l.shadows.ToString(),l.colorTemperature,l.useColorTemperature}).ToArray();
   var vols=UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude).Select(v=>new{v.name,v.isGlobal,profile=v.sharedProfile?AssetDatabase.GetAssetPath(v.sharedProfile):null,components=v.sharedProfile?v.sharedProfile.components.Select(c=>c.GetType().Name+(c.active?"":"(off)")).ToArray():new string[0]}).ToArray();
   var rp=GraphicsSettings.currentRenderPipeline;
   var s=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();
   return JsonConvert.SerializeObject(new{
    renderers=groups,lights=light,volumes=vols,
    renderSettings=new{ambientMode=RenderSettings.ambientMode.ToString(),sky=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor),equator=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientEquatorColor),ground=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientGroundColor),flat=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight),RenderSettings.ambientIntensity,fog=RenderSettings.fog,skybox=RenderSettings.skybox?RenderSettings.skybox.name:null},
    pipeline=rp?AssetDatabase.GetAssetPath(rp):null,
    colorSpace=PlayerSettings.colorSpace.ToString(),
    session=s?new{root=s.transform.root.name,staticGeometry=s.StaticGeometry?s.StaticGeometry.name:null,blockers=s.Visibility.Blockers.Length,s.ExpectedDataSha,viewCam=s.ViewCamera?s.ViewCamera.name:null}:null,
    colliders=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length
   },Formatting.Indented,new JsonSerializerSettings{ReferenceLoopHandling=ReferenceLoopHandling.Ignore});
  }finally{EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(x=>new SceneSetup{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive}).ToArray());}
 }
}
