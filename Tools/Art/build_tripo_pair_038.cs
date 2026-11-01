using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using DeadSurvivor.Editor;
using Newtonsoft.Json;
public static class TripoPair038
{
 const string Out="output/tripo-pair-038/20261102-r001";
 const string Art="Assets/DeadSurvivor/Art/Environment/TripoPair038_r003";
 const string Folder="Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003";
 const string Source="Assets/DeadSurvivor/Scenes/Map037_ReferenceDetail_r002/Map037_ReferenceDetail.unity";
 static DSBlockoutData data;
 static readonly List<object> bindings=new List<object>();
 static Vector3 W(Vector2 px,float y){var w=DSBlockoutMesh.World(data.Logical(px,4),data.world_scale);w.y=y;return w;}
 static Vector2 Px(Vector3 p)=>new Vector2(p.x/(data.world_scale*4)+565,-p.z/(data.world_scale*4)+580);
 static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new Exception("NO_RENDERER");var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
 static bool Fits(GameObject go,Vector2[] polygon,Bounds local){var area=new DSMapGeometry(polygon,Array.Empty<Vector2[]>(),.001,16384);var points=new[]{new Vector3(local.min.x,0,local.min.z),new Vector3(local.max.x,0,local.min.z),new Vector3(local.max.x,0,local.max.z),new Vector3(local.min.x,0,local.max.z)}.Select(p=>Px(go.transform.TransformPoint(p))).ToArray();for(int i=0;i<4;i++)if(!area.SafeSweep(points[i],points[(i+1)%4],.02))return false;return true;}

 static GameObject Import(string kind,Vector3 size,int maxTriangles)
 {
  var dir=Art+"/"+kind;Directory.CreateDirectory(dir);
  foreach(var file in new[]{"model-sized.fbx","basecolor.png","normal.png"}){var target=dir+"/"+file;if(File.Exists(target))throw new Exception("OUTPUT_EXISTS:"+target);File.Copy(Out+"/"+kind+"/delivery/"+file,target);}
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  foreach(var name in new[]{"basecolor","normal"}){var t=(TextureImporter)AssetImporter.GetAtPath(dir+"/"+name+".png");t.textureType=name=="normal"?TextureImporterType.NormalMap:TextureImporterType.Default;t.sRGBTexture=name=="basecolor";t.maxTextureSize=1024;t.mipmapEnabled=true;t.textureCompression=TextureImporterCompression.Uncompressed;t.SaveAndReimport();}
  var imp=(ModelImporter)AssetImporter.GetAtPath(dir+"/model-sized.fbx");imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.isReadable=true;imp.SaveAndReimport();
  var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="038_"+kind};m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/basecolor.png"));m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/normal.png"));m.EnableKeyword("_NORMALMAP");m.SetColor("_BaseColor",Color.white);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.08f);AssetDatabase.CreateAsset(m,dir+"/Surface.mat");
  var root=new GameObject("038_"+kind);var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dir+"/model-sized.fbx"));instance.transform.SetParent(root.transform,false);
  foreach(var mr in root.GetComponentsInChildren<Renderer>(true)){mr.sharedMaterials=Enumerable.Repeat(m,mr.sharedMaterials.Length).ToArray();mr.shadowCastingMode=ShadowCastingMode.On;mr.receiveShadows=true;}
  string path=dir+"/"+kind+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var check=PrefabUtility.LoadPrefabContents(path);
  try{var b=BoundsOf(check);int tris=check.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);bool uv=check.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.uv.Length==f.sharedMesh.vertexCount);var comps=check.GetComponentsInChildren<Component>(true);bool allowed=comps.All(c=>c is Transform || c is MeshFilter || c is MeshRenderer);var mats=check.GetComponentsInChildren<Renderer>().SelectMany(x=>x.sharedMaterials).Distinct().ToArray();
   if((b.size-size).magnitude>.015f||Mathf.Abs(b.min.y)>.01f||Mathf.Abs(b.center.x)>.01f||Mathf.Abs(b.center.z)>.01f||tris>maxTriangles||!uv||!allowed||mats.Length!=1||mats[0]!=m)throw new Exception("PREFAB_CONTRACT:"+kind);
   File.WriteAllText(Out+"/"+kind+"/unity-metrics.json",JsonConvert.SerializeObject(new{status="PASS_SAVED_PREFAB",prefab=path,size=new[]{b.size.x,b.size.y,b.size.z},minimumY=b.min.y,triangles=tris,uv,materials=mats.Length,allowedComponents=allowed,colliders=check.GetComponentsInChildren<Collider>(true).Length,basecolor=AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")),normal=AssetDatabase.GetAssetPath(m.GetTexture("_BumpMap"))},Formatting.Indented));
  }finally{PrefabUtility.UnloadPrefabContents(check);}return prefab;
 }
 static void Place(DSMapSession s,Transform parent,GameObject prefab,string kind,string id,float px,float py,float angle,float scale,int index)
 {
  var o=data.objects.Single(x=>x.id==id);var cap=s.StaticGeometry.GetComponentsInChildren<Renderer>(true).Single(r=>r.name==id+"_GreenTop");float y=cap.bounds.max.y-.012f;
  var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var localBounds=BoundsOf(go);if(go.transform.position!=Vector3.zero||go.transform.rotation!=Quaternion.identity||go.transform.localScale!=Vector3.one)throw new Exception("NON_IDENTITY_PREFAB_ROOT");go.name=id+"_Tripo038_"+kind+"_"+index;go.transform.SetParent(parent,false);go.transform.rotation=Quaternion.Euler(0,angle,0);go.transform.localScale=Vector3.one*scale;
  Vector2 chosen=new Vector2(px,py);bool found=false;
  // Keep the entire oriented prefab bounding rectangle inside the existing C. Small
  // position adjustments are visual proposals, never changes to map geometry.
  foreach(float factor in new[]{1f,.95f,.90f}){
   go.transform.localScale=Vector3.one*(scale*factor);
   foreach(var offset in new[]{Vector2.zero,new Vector2(0,2),new Vector2(0,-2),new Vector2(2,0),new Vector2(-2,0),new Vector2(2,2),new Vector2(-2,-2),new Vector2(0,4),new Vector2(0,-4)}){
    chosen=new Vector2(px,py)+offset;go.transform.position=W(chosen,y);if(Fits(go,o.points_px,localBounds)){found=true;break;}
   }if(found)break;
  }
  if(!found){UnityEngine.Object.DestroyImmediate(go);throw new Exception("NO_SAFE_PLACEMENT:"+id+":"+kind+":"+index);}
  var b=BoundsOf(go);bindings.Add(new{name=go.name,kind,parentOB=id,prefab=AssetDatabase.GetAssetPath(prefab),sourcePixelProposal=new[]{px,py},placedPixel=new[]{chosen.x,chosen.y},position=new[]{go.transform.position.x,go.transform.position.y,go.transform.position.z},rotationY=angle,uniformScale=go.transform.localScale.x,boundsSize=new[]{b.size.x,b.size.y,b.size.z},minY=b.min.y,supportY=cap.bounds.max.y,existingC=data.objects.Single(x=>x.id==id).points_px.Select(v=>new[]{v.x,v.y}).ToArray(),containment="Oriented prefab bounds perimeter fully inside existing C",collision="Existing C owns blocking; no added Collider"});
 }
 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET");var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
  foreach(var target in new[]{Art,Folder}){if(Directory.Exists(target)||File.Exists(target+".meta"))throw new Exception("DESTINATION_EXISTS");for(string p=Path.GetFullPath(target);p!=null;p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new Exception("SYMLINK_PATH");}
  File.WriteAllText(Out+"/build-planned.json",JsonConvert.SerializeObject(new{art=Art,scene=Folder,started=DateTimeOffset.Now.ToString("o")},Formatting.Indented));
  try{
   var scene=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);var s=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();data=JsonUtility.FromJson<DSBlockoutData>(s.Layout.text);data.Validate();string hash=DSMapBlockoutPreview.Hash(s.Layout.text);
   var shrub=Import("shrub",new Vector3(1,.4f,.9f),9000);var stone=Import("stone",new Vector3(1.4f,.32f,.55f),2000);
   s.transform.root.name="Map038_TRIPO_PAIR_TRIAL";var parent=new GameObject("Tripo038_VisualOnly").transform;parent.SetParent(s.StaticGeometry,false);
   foreach(var old in s.StaticGeometry.GetComponentsInChildren<Renderer>(true).Where(x=>x.name.Contains("_LowFoliage")))old.enabled=false;
   Place(s,parent,shrub,"shrub","OB-001",224,185,18,1,0);Place(s,parent,shrub,"shrub","OB-002",342,201,125,.90f,1);Place(s,parent,shrub,"shrub","OB-004",240,277,228,.90f,2);Place(s,parent,shrub,"shrub","OB-006",466,270,310,1.10f,3);
   var placements=new[]{new[]{1f,212,160,55,1},new[]{1f,237,163,70,.9f},new[]{1f,208,177,15,.85f},new[]{2f,325,218,-45,.85f},new[]{2f,369,193,0,.9f},new[]{2f,388,194,15,.85f},new[]{3f,435,212,10,.85f},new[]{3f,467,207,-30,.85f},new[]{3f,489,195,-40,.85f},new[]{4f,226,276,75,.7f},new[]{4f,265,270,-35,.9f},new[]{4f,287,250,-35,.85f},new[]{5f,377,249,20,.9f},new[]{5f,391,267,70,.9f},new[]{6f,450,270,70,.9f},new[]{6f,474,279,-25,.85f}};
   for(int i=0;i<placements.Length;i++){var a=placements[i];Place(s,parent,stone,"stone","OB-"+((int)a[0]).ToString("000"),a[1],a[2],a[3],a[4],i);}
   s.Visibility.Blockers=s.Visibility.Blockers.Where(r=>r&&r.enabled).Concat(parent.GetComponentsInChildren<Renderer>()).Distinct().ToArray();
   if(parent.GetComponentsInChildren<Collider>(true).Length>0||hash!=s.ExpectedDataSha)throw new Exception("CHANGED_COLLISION_CONTRACT");
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);string target=Folder+"/Map038_TripoPair.unity";if(!EditorSceneManager.SaveScene(scene,target))throw new Exception("SAVE_FAILED");
   EditorSceneManager.OpenScene(target,OpenSceneMode.Single);var reload=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();if(reload.ExpectedDataSha!=hash||reload.Visibility.Blockers.Any(x=>!x))throw new Exception("SAVED_BINDING_INVALID");
   var result=new{status="PASS_AUTHORING_CANDIDATE",scene=target,sourceScene=Source,layout=AssetDatabase.GetAssetPath(reload.Layout),layoutSha=hash,generatedModels=2,shrubInstances=4,stoneInstances=placements.Length,bindings,collidersAdded=0,combat=reload.CombatEnabled};File.WriteAllText(Out+"/build.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
  }catch(Exception ex){File.WriteAllText(Out+"/build-failure.json",JsonConvert.SerializeObject(new{status="FAIL_PARTIAL_PRESERVED",error=ex.ToString(),art=Art,scene=Folder},Formatting.Indented));throw;}
  finally{EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(x=>new SceneSetup{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive}).ToArray());Selection.objects=saved.selection.Select(x=>{GlobalObjectId.TryParse(x,out var id);return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);}).Where(x=>x).ToArray();}
 }
}
