using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using Newtonsoft.Json;

// 046: four Tripo monsters (ranged, charger, armored, bomber) into Unity as prefabs like the 042 monster (root at the feet ->
// "Visual" -> model normalised to height 1, centred), a new actor set ActorSet046 (heroes and the melee monster unchanged
// from ActorSet045s3b) assigned to Map046_Full only. Rebuildable: replaces only Art/Characters/Monster046.
// 049: Build() ends with ApplyPerf() (blob shadows for monsters; GPU instancing measured and left off).
// Inputs: output/monster-046/tripo/<asset>/fbx/{model.fbx,basecolor.png} (Tools/Art/convert_glb_042.py),
//         optional output/monster-046/yaw.json {"ranged": deg, ...} when a model's front is not +Z.
// Run: unity command script run --project-path <root> -- --file Tools/Map/build_monsters_046.cs --entry Monster046Build.Build
public static class Monster046Build
{
 const string Dir="Assets/DeadSurvivor/Art/Characters/Monster046";
 const string Src="output/monster-046";
 const string Scene="Assets/DeadSurvivor/Scenes/Map046/Map046_Full.unity";
 const string BaseSet="Assets/DeadSurvivor/Scenes/Proto045/Art_s3b/ActorSet045s3b.asset";
 const string BaseMat="Assets/DeadSurvivor/Art/Characters/Test042_r001/Monster042.mat";
 static readonly (string id,string asset)[] M={("ranged","monster_ranged046"),("charger","monster_charger046"),("armored","monster_armored046"),("bomber","monster_bomber046")};

// 050: superseded by the official asset pipeline (Tools/Art/assets.py prep/unity, docs/workflows/asset-standard.md). The monsters now
// live in Art/Characters/Monsters/<id>/, the actor set is Art/Characters/ActorSet_Main.asset and the blob shadow is in
// Art/Characters/Shared/. Build would delete Art/Characters/Monster046 and re-point the scene, so both entries refuse.
 public static object Build()
 {
  throw new Exception("SUPERSEDED_BY_050: use /usr/bin/python3 Tools/Art/assets.py prep|unity (docs/workflows/asset-standard.md)");
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++){var s=EditorSceneManager.GetSceneAt(i);if(s.isDirty)throw new Exception("UNSAVED_SCENE:"+s.path);}
  var yaw=File.Exists(Src+"/yaw.json")?JsonConvert.DeserializeObject<Dictionary<string,float>>(File.ReadAllText(Src+"/yaw.json")):new Dictionary<string,float>();
  foreach(var (id,asset) in M)foreach(var f in new[]{"model.fbx","basecolor.png"})if(!File.Exists($"{Src}/tripo/{asset}/fbx/{f}"))throw new Exception("MISSING_INPUT:"+asset+"/"+f);
  if(AssetDatabase.IsValidFolder(Dir))AssetDatabase.DeleteAsset(Dir);
  AssetDatabase.CreateFolder("Assets/DeadSurvivor/Art/Characters","Monster046");
  var baseMat=AssetDatabase.LoadAssetAtPath<Material>(BaseMat)??throw new Exception("MISSING:"+BaseMat);
  var report=new Dictionary<string,object>();var prefabs=new Dictionary<string,GameObject>();
  foreach(var (id,asset) in M)
  {
   var d=Dir+"/"+id;AssetDatabase.CreateFolder(Dir,id);
   File.Copy($"{Src}/tripo/{asset}/fbx/model.fbx",$"{d}/{id}.fbx");File.Copy($"{Src}/tripo/{asset}/fbx/basecolor.png",$"{d}/{id}_basecolor.png");
   AssetDatabase.ImportAsset($"{d}/{id}.fbx");AssetDatabase.ImportAsset($"{d}/{id}_basecolor.png");
   var tex=AssetDatabase.LoadAssetAtPath<Texture2D>($"{d}/{id}_basecolor.png");
   var mat=new Material(baseMat){name="Monster046_"+id};mat.SetTexture("_BaseMap",tex);if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",tex);mat.SetColor("_BaseColor",Color.white);
   AssetDatabase.CreateAsset(mat,$"{d}/Monster046_{id}.mat");
   var fbx=AssetDatabase.LoadAssetAtPath<GameObject>($"{d}/{id}.fbx");
   var (prefab,info)=Prefab(fbx,$"{d}/Monster046_{id}.prefab",mat);prefabs[id]=prefab;report[id]=info;
  }
  var set=ScriptableObject.Instantiate(AssetDatabase.LoadAssetAtPath<DSMapActorSet>(BaseSet)??throw new Exception("MISSING:"+BaseSet));
  // yaw.json also applies to entries kept from the 045 set (the 042 melee model faces -X like the new ones)
  set.Monsters=set.Monsters.Select(e=>new DSMapActorSet.Entry{id=e.id,prefab=prefabs.TryGetValue(e.id,out var p)?p:e.prefab,material=prefabs.ContainsKey(e.id)?null:e.material,yawOffset=yaw.TryGetValue(e.id,out var y)?y:e.yawOffset}).ToArray();
  AssetDatabase.CreateAsset(set,Dir+"/ActorSet046.asset");
  var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
  var pres=UnityEngine.Object.FindObjectsByType<DSMapPresentation>(FindObjectsInactive.Include).Single();
  var so=new SerializedObject(pres);so.FindProperty("Actors").objectReferenceValue=set;so.ApplyModifiedPropertiesWithoutUndo();
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
  AssetDatabase.SaveAssetIfDirty(set);
  report["lineup"]=Lineup(set);
  // 046-12: the saved scene once kept Actors = null after a rebuild; reload and verify the reference
  EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
  var check=UnityEngine.Object.FindObjectsByType<DSMapPresentation>(FindObjectsInactive.Include).Single();
  if(check.Actors!=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(Dir+"/ActorSet046.asset"))throw new Exception("ACTORS_NOT_SAVED");
  report["monsters"]=set.Monsters.Select(e=>new{e.id,prefab=e.prefab?e.prefab.name:null,material=e.material?e.material.name:null,e.yawOffset}).ToArray();
  report["perf"]=ApplyPerf();   // 049: the perf settings belong to the set this builder owns (builder rules 1)
  File.WriteAllText(Src+"/unity-build.json",JsonConvert.SerializeObject(report,Formatting.Indented));
  return report;
 }

 // 049 performance (docs/workflows/performance.md): monsters cast no real shadow — a soft blob under the feet instead.
 // Overwrites in place (keeps GUIDs, builder rules 3).
 // Run alone: unity command script run --project-path <root> -- --file Tools/Map/build_monsters_046.cs --entry Monster046Build.ApplyPerf
 const string BlobPath=Dir+"/BlobShadow046";
 public static object ApplyPerf()
 {
  throw new Exception("SUPERSEDED_BY_050: the blob shadow material is kept in Art/Characters/Shared/ and set on Art/Characters/ActorSet_Main.asset");
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  var set=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(Dir+"/ActorSet046.asset")??throw new Exception("NO_SET");
  // soft round blob: alpha 1 in the centre falling to 0 at the rim (smoothstep), 64 px
  int n=64;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(n/2f,n/2f))/(n/2f);float a=Mathf.Clamp01(1-d);a=a*a*(3-2*a);t.SetPixel(x,y,new Color(1,1,1,a));}
  File.WriteAllBytes(BlobPath+".png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(BlobPath+".png");
  var ti=(TextureImporter)AssetImporter.GetAtPath(BlobPath+".png");ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.wrapMode=TextureWrapMode.Clamp;ti.SaveAndReimport();
  var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(BlobPath+".png");
  var unlit=Shader.Find("Universal Render Pipeline/Unlit")??throw new Exception("NO_URP_UNLIT");
  var mat=AssetDatabase.LoadAssetAtPath<Material>(BlobPath+".mat");
  if(!mat){mat=new Material(unlit){name="BlobShadow046"};AssetDatabase.CreateAsset(mat,BlobPath+".mat");}
  mat.shader=unlit;mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);
  mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=(int)RenderQueue.Transparent-50;mat.SetOverrideTag("RenderType","Transparent");
  mat.SetShaderPassEnabled("ShadowCaster",false);mat.SetShaderPassEnabled("DepthOnly",false);
  mat.SetTexture("_BaseMap",tex);mat.SetColor("_BaseColor",new Color(0,0,0,.42f));mat.enableInstancing=true;EditorUtility.SetDirty(mat);
  // GPU instancing stays OFF (049 measurement, docs/reviews/perf-049.md): it cut draw calls (load 628 -> 239) but the
  // render time grew (9.2 -> 12.1 ms) because it takes the monsters off the SRP Batcher. To try it again: turn
  // enableInstancing on for the monster + hit materials and set MonsterInstanced = true.
  var so=new SerializedObject(set);so.FindProperty("MonsterBlobShadow").objectReferenceValue=mat;so.FindProperty("MonsterInstanced").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
  EditorUtility.SetDirty(set);AssetDatabase.SaveAssets();
  var check=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(Dir+"/ActorSet046.asset");
  if(check.MonsterBlobShadow!=mat||check.MonsterInstanced)throw new Exception("PERF_NOT_SAVED");
  return new{blob=AssetDatabase.GetAssetPath(mat),instanced=check.MonsterInstanced};
 }

 // root (feet) -> "Visual" (identity; receives turn/bob) -> model normalised to height 1, feet at 0, centred (as 045 Prefab)
 static (GameObject,object) Prefab(GameObject fbx,string path,Material mat)
 {
  var root=new GameObject(Path.GetFileNameWithoutExtension(path));var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
  var model=(GameObject)PrefabUtility.InstantiatePrefab(fbx);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.transform.SetParent(visual.transform,false);
  var rs=model.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
  float s=1f/Mathf.Max(.001f,b.size.y);model.transform.localScale=Vector3.one*s;
  model.transform.localPosition=new Vector3(-b.center.x*s,-b.min.y*s,-b.center.z*s);
  var anim=model.GetComponent<Animator>();if(anim)UnityEngine.Object.DestroyImmediate(anim);
  foreach(var r in rs){r.shadowCastingMode=ShadowCastingMode.On;r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();}
  int tris=model.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh?f.sharedMesh.triangles.Length/3:0)+model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(f=>f.sharedMesh?f.sharedMesh.triangles.Length/3:0);
  var info=new{size=(b.size*s).ToString("F2"),triangles=tris};
  var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return (prefab,info);
 }

 // stills: the five monsters in a row seen from the game camera angle and from the front (+Z facing the camera)
 static object Lineup(DSMapActorSet set)
 {
  var go=new GameObject("Lineup046_Temp");var shots=new List<string>();
  try
  {
   float x=0;foreach(var e in set.Monsters)
   {
    var inst=(GameObject)UnityEngine.Object.Instantiate(e.prefab,go.transform);inst.transform.position=new Vector3(500+x,0,500);
    float r=e.id=="armored"?25:18;inst.transform.localScale=Vector3.one*set.MonsterHeight*r/set.MonsterReferenceRadius*2.2f;
    inst.transform.GetChild(0).localRotation=Quaternion.Euler(0,e.yawOffset,0);
    if(e.material)foreach(var rr in inst.GetComponentsInChildren<Renderer>())rr.sharedMaterials=Enumerable.Repeat(e.material,rr.sharedMaterials.Length).ToArray();
    x+=1.6f;
   }
   var camGo=new GameObject("LineupCam");camGo.transform.SetParent(go.transform);var cam=camGo.AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.31f,.61f,.2f);
   var c=new Vector3(500+(x-1.6f)/2,.5f,500);
   foreach(var (name,rot,size) in new[]{("lineup_game",Quaternion.Euler(55,0,0),2.2f),("lineup_front",Quaternion.Euler(8,180,0),1.6f)})
   {
    cam.orthographicSize=size;cam.transform.SetPositionAndRotation(c-(rot*Vector3.forward)*30,rot);
    var rt=new RenderTexture(1600,600,24){antiAliasing=4};cam.targetTexture=rt;cam.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
    var tex=new Texture2D(1600,600,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,600),0,0);tex.Apply();RenderTexture.active=prev;cam.targetTexture=null;
    File.WriteAllBytes($"{Src}/{name}.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);shots.Add($"{Src}/{name}.png");
   }
  }
  finally{UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());}
  return shots;
 }
}
