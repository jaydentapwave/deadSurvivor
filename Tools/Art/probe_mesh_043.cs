using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor.Editor;
using Newtonsoft.Json;
// 043 read-only probe: mesh/transform/material facts for the 038 renderers (no save, scene setup restored).
public static class LowPolyMeshProbe043
{
 public static object Run()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("DIRTY_SCENE_OPEN");
  var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
  try{
   EditorSceneManager.OpenScene("Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003/Map038_TripoPair.unity",OpenSceneMode.Single);
   var rows=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r=>!r.name.StartsWith("model-sized")).Select(r=>{var f=r.GetComponent<MeshFilter>();var m=f?f.sharedMesh:null;var t=r.transform;
    return new{path=(t.parent?t.parent.name+"/":"")+r.name,enabled=r.enabled&&r.gameObject.activeInHierarchy,mesh=m?m.name:null,meshAsset=m?AssetDatabase.GetAssetPath(m):null,verts=m?m.vertexCount:0,subs=m?m.subMeshCount:0,uv=m&&m.uv.Length>0,colors=m&&m.colors32.Length>0,
     lmin=m?new[]{m.bounds.min.x,m.bounds.min.y,m.bounds.min.z}:null,lmax=m?new[]{m.bounds.max.x,m.bounds.max.y,m.bounds.max.z}:null,
     pos=new[]{t.position.x,t.position.y,t.position.z},rot=new[]{t.eulerAngles.x,t.eulerAngles.y,t.eulerAngles.z},scale=new[]{t.lossyScale.x,t.lossyScale.y,t.lossyScale.z},
     mats=r.sharedMaterials.Select(x=>x?new{x.name,shader=x.shader.name,queue=x.renderQueue,color=x.HasProperty("_BaseColor")?"#"+ColorUtility.ToHtmlStringRGBA(x.GetColor("_BaseColor")):null,tex=x.HasProperty("_BaseMap")&&x.GetTexture("_BaseMap")?AssetDatabase.GetAssetPath(x.GetTexture("_BaseMap")):null}:null).ToArray()};}).ToArray();
   var groups=rows.GroupBy(x=>x.meshAsset+"|"+string.Join(",",x.mats.Select(m=>m==null?"null":m.name))).Select(g=>new{key=g.Key,count=g.Count(),first=g.First()}).ToArray();
   var cam=UnityEngine.Object.FindAnyObjectByType<Camera>();var data=cam?cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>():null;
   return JsonConvert.SerializeObject(new{groups,camera=cam?new{cam.name,post=data?data.renderPostProcessing:false,cam.clearFlags,bg="#"+ColorUtility.ToHtmlStringRGB(cam.backgroundColor)}:null},Formatting.Indented);
  }finally{EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(x=>new SceneSetup{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive}).ToArray());}
 }
}
