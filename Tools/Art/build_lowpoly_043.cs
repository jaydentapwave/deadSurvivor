using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using DeadSurvivor.Editor;
using Newtonsoft.Json;
// 043: shared low-poly shader + palette applied to a copy of the 038 representative area.
// Run: unity command script run --file Tools/Art/build_lowpoly_043.cs --entry LowPoly043.Build
// Captures: same file, --entry LowPolyCapture043.<Base038|Stage1|Stage2|Stage3Day|Stage3Night|...>
// (script run compiles one file, so build presets and captures share this file).
// Never modifies 037/038 sources; creates Art/LowPoly043 and Scenes/Map043_LowPolyShader_r001.
public static class LowPoly043
{
 public const string Out="output/lowpoly-shader-043/r001";
 public const string Art="Assets/DeadSurvivor/Art/LowPoly043";
 public const string SceneFolder="Assets/DeadSurvivor/Scenes/Map043_LowPolyShader_r001";
 public const string ScenePath=SceneFolder+"/Map043_LowPolyShader.unity";
 const string Source="Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003/Map038_TripoPair.unity";
 const string Tripo038="Assets/DeadSurvivor/Art/Environment/TripoPair038_r003";
 public static readonly string[] Presets={"Ground","Soil","Path","Stone","Foliage"};

 // Source material -> palette cell (index = row*8+col) and preset. Agent proposal based on the
 // matching regions of day.png; see docs/design/lowpoly-shader-043.md.
 static readonly Dictionary<string,(int cell,string preset)> Role=new Dictionary<string,(int,string)>{
  {"Meadow",(3,"Ground")},{"Ground",(3,"Ground")},{"MossTop",(5,"Ground")},
  {"DrySoil",(11,"Soil")},{"DrySoilLight",(12,"Soil")},{"SoilSoftEdge",(10,"Soil")},{"CoolRoadBed",(18,"Soil")},
  {"CobbleGrey",(20,"Path")},{"CobbleCool",(21,"Path")},{"CobbleLight",(22,"Path")},{"CobbleMoss",(4,"Path")},
  {"RemnantStone",(21,"Stone")},{"StoneLight",(22,"Stone")},{"StoneMoss",(20,"Stone")},{"StoneJoints",(17,"Stone")},
  {"Candidate Stone",(19,"Stone")},{"Candidate Boundary",(19,"Stone")},
  {"FoliageDeep",(1,"Foliage")},{"FoliageMid",(2,"Foliage")},{"FoliageTips",(6,"Foliage")},
 };

 public static Vector2 CellUV(int index){int c=index%8,r=index/8;return new Vector2((c+.5f)/8f,(3-r+.5f)/4f);}

 // Style parameters per stage. Stage 1 = palette + lighting only; 2 adds height gradient and
 // top projection; 3 adds wind. Globals (cloud shadow, shadow tint) are set by the capture step.
 public static void ApplyStage(Material m,string preset,int stage)
 {
  m.SetFloat("_GradientBottom",1);m.SetFloat("_GradientHeight",1);m.SetFloat("_TopStrength",0);m.SetVector("_TopCell",new Vector4(3,0,0,0));
  m.SetFloat("_TopMin",.7f);m.SetFloat("_TopMax",.95f);m.SetFloat("_ShadeWrap",.5f);m.SetFloat("_ShadeSoftness",1);m.SetFloat("_AmbientStrength",1);
  m.SetFloat("_WindStrength",0);m.SetFloat("_WindFrequency",1.3f);m.SetFloat("_RimStrength",0);m.SetFloat("_EmissionStrength",2);
  bool clip=preset=="Soil";m.SetFloat("_VertexAlphaClip",clip?1:0);m.SetFloat("_Cutoff",.15f);if(clip)m.EnableKeyword("_VERTEX_ALPHA_CLIP");else m.DisableKeyword("_VERTEX_ALPHA_CLIP");
  m.SetFloat("_FlatShading",0);m.DisableKeyword("_FLAT_SHADING");m.SetFloat("_NoiseStrength",0);m.SetFloat("_NoiseScale",4);
  if(stage>=2){
   if(preset=="Path"){m.SetFloat("_GradientBottom",.8f);m.SetFloat("_GradientHeight",.12f);}
   if(preset=="Stone"){m.SetFloat("_GradientBottom",.7f);m.SetFloat("_GradientHeight",.5f);m.SetFloat("_TopStrength",.3f);m.SetVector("_TopCell",new Vector4(3,0,0,0));}
   if(preset=="Foliage"){m.SetFloat("_GradientBottom",.55f);m.SetFloat("_GradientHeight",.45f);}
  }
  if(stage>=3&&preset=="Foliage")m.SetFloat("_WindStrength",.02f);
  if(stage>=3&&(preset=="Ground"||preset=="Soil"))m.SetFloat("_NoiseStrength",.08f);
 }

 static void Guard()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("DIRTY_SCENE_OPEN");
 }
 public static void Restore(DSMapMotionVerification.SnapshotData saved){EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(x=>new SceneSetup{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive}).ToArray());Selection.objects=saved.selection.Select(x=>{GlobalObjectId.TryParse(x,out var id);return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);}).Where(x=>x).ToArray();}
 static string Folder(string parent,string name){var p=parent+"/"+name;if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(parent,name);return p;}
 static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
 static string Path3(Transform t){var s=t.name;for(var p=t.parent;p;p=p.parent)s=p.name+"/"+s;return s;}

 static object ShaderReport(Shader shader)
 {
  var msgs=ShaderUtil.GetShaderMessages(shader).Select(m=>new{severity=m.severity.ToString(),m.message,m.line,m.platform}).ToArray();
  object srp="NOT_RUN: internal API unavailable";
  try{var mi=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",BindingFlags.NonPublic|BindingFlags.Static);var why=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason",BindingFlags.NonPublic|BindingFlags.Static);
   if(mi!=null){var codes=new List<object>();for(int i=0;i<shader.subshaderCount;i++){int code=(int)mi.Invoke(null,new object[]{shader,i});codes.Add(new{subShader=i,code,compatible=code==0,reason=why!=null&&code!=0?why.Invoke(null,new object[]{shader,i,code}):null});}srp=codes;}}catch(Exception e){srp="NOT_RUN: "+e.GetType().Name+": "+e.Message;}
  return new{name=shader.name,hasError=ShaderUtil.ShaderHasError(shader),supported=shader.isSupported,passes=shader.passCount,messages=msgs,srpBatcher=srp};
 }

 static GameObject MakePrefab(string kind,Material mat,string folder)
 {
  string fbx=folder+"/model.fbx";
  var imp=(ModelImporter)AssetImporter.GetAtPath(fbx);imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.isReadable=true;imp.SaveAndReimport();
  var root=new GameObject("043_"+kind);var inst=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));inst.transform.SetParent(root.transform,false);
  foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
  string path=folder+"/"+kind+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return AssetDatabase.LoadAssetAtPath<GameObject>(path);
 }

 // Removes ONLY this tool's partial output after a recorded failure (no build.json yet).
 public static object CleanPartial()
 {
  Guard();
  if(File.Exists(Out+"/build.json")||!File.Exists(Out+"/build-failure.json"))throw new Exception("NOT_A_FAILED_RUN");
  var files=new[]{Art,SceneFolder}.Where(Directory.Exists).SelectMany(d=>Directory.GetFiles(d,"*",SearchOption.AllDirectories)).OrderBy(x=>x).Select(f=>new{f,sha=BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(f))).Replace("-","").ToLowerInvariant()}).ToArray();
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).path.StartsWith(SceneFolder))throw new Exception("PARTIAL_SCENE_OPEN");
  bool a=!AssetDatabase.IsValidFolder(Art)||AssetDatabase.DeleteAsset(Art);bool b=!AssetDatabase.IsValidFolder(SceneFolder)||AssetDatabase.DeleteAsset(SceneFolder);
  var stamp=DateTime.Now.ToString("HHmmss");File.Move(Out+"/build-failure.json",Out+"/build-failure-"+stamp+".json");
  File.WriteAllText(Out+"/cleanup-"+stamp+".json",JsonConvert.SerializeObject(new{status=a&&b?"CLEANED_OWN_PARTIAL_OUTPUT":"CLEANUP_INCOMPLETE",deleted=files},Formatting.Indented));
  return JsonConvert.SerializeObject(new{a,b,count=files.Length});
 }

 public static object Build()
 {
  Guard();
  foreach(var target in new[]{Art,SceneFolder})if(Directory.Exists(target)||File.Exists(target+".meta"))throw new Exception("DESTINATION_EXISTS:"+target);
  var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
  var log=new List<string>();
  File.WriteAllText(Out+"/build-planned.json",JsonConvert.SerializeObject(new{art=Art,scene=ScenePath,source=Source,started=DateTimeOffset.Now.ToString("o")},Formatting.Indented));
  try{
   // 1. files
   string art=Folder("Assets/DeadSurvivor/Art","LowPoly043");string fShader=Folder(art,"Shaders"),fPal=Folder(art,"Palette"),fMat=Folder(art,"Materials"),fMesh=Folder(art,"Meshes"),fProps=Folder(art,"Props");
   string fStone=Folder(fProps,"stone"),fShrub=Folder(fProps,"shrub");
   var copies=new[]{(Out+"/src/DSLowPolyPalette.shader",fShader+"/DSLowPolyPalette.shader"),(Out+"/palette/palette.png",fPal+"/palette-r001.png"),(Out+"/palette/palette.json",fPal+"/palette-r001.json"),
    (Out+"/repaint-v3/stone/model.fbx",fStone+"/model.fbx"),(Out+"/repaint-v3/shrub/model.fbx",fShrub+"/model.fbx"),(Out+"/repaint-v3/stone/repaint.json",fStone+"/repaint.json"),(Out+"/repaint-v3/shrub/repaint.json",fShrub+"/repaint.json")};
   foreach(var (from,to) in copies){if(File.Exists(to))throw new Exception("OUTPUT_EXISTS:"+to);File.Copy(from,to);AssetDatabase.ImportAsset(to,ImportAssetOptions.ForceSynchronousImport);}
   var ti=(TextureImporter)AssetImporter.GetAtPath(fPal+"/palette-r001.png");ti.textureType=TextureImporterType.Default;ti.sRGBTexture=true;ti.mipmapEnabled=false;ti.filterMode=FilterMode.Point;ti.wrapMode=TextureWrapMode.Clamp;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.npotScale=TextureImporterNPOTScale.None;ti.alphaIsTransparency=false;ti.maxTextureSize=64;ti.SaveAndReimport();
   var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(fPal+"/palette-r001.png");
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(fShader+"/DSLowPolyPalette.shader");if(!shader)throw new Exception("SHADER_NOT_IMPORTED");
   var shaderReport=ShaderReport(shader);if(ShaderUtil.ShaderHasError(shader)){File.WriteAllText(Out+"/shader-errors.json",JsonConvert.SerializeObject(shaderReport,Formatting.Indented));throw new Exception("SHADER_HAS_ERROR");}

   // 2. materials (one per preset, same shader + palette)
   var mats=new Dictionary<string,Material>();
   foreach(var p in Presets){var m=new Material(shader){name="LowPoly043_"+p};m.SetTexture("_Palette",palette);ApplyStage(m,p,3);AssetDatabase.CreateAsset(m,fMat+"/"+m.name+".mat");mats[p]=m;}

   // 3. repainted Tripo prefabs
   var stone=MakePrefab("stone",mats["Stone"],fStone);var shrub=MakePrefab("shrub",mats["Foliage"],fShrub);
   var prefabCheck=new List<object>();
   foreach(var (kind,prefab) in new[]{("stone",stone),("shrub",shrub)}){
    var a=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));var b=PrefabUtility.LoadPrefabContents(Tripo038+"/"+kind+"/"+kind+".prefab");
    try{var ba=BoundsOf(a);var bb=BoundsOf(b);int ta=a.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3),tb=b.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
     var uvs=a.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.uv).Distinct().Count();
     bool ok=(ba.size-bb.size).magnitude<.01f&&(ba.center-bb.center).magnitude<.01f&&ta==tb;
     prefabCheck.Add(new{kind,ok,size043=new[]{ba.size.x,ba.size.y,ba.size.z},size038=new[]{bb.size.x,bb.size.y,bb.size.z},center043=new[]{ba.center.x,ba.center.y,ba.center.z},center038=new[]{bb.center.x,bb.center.y,bb.center.z},triangles043=ta,triangles038=tb,distinctUV=uvs});
     if(!ok)throw new Exception("PREFAB_MISMATCH:"+kind);}
    finally{PrefabUtility.UnloadPrefabContents(a);PrefabUtility.UnloadPrefabContents(b);}
   }

   // 4. scene copy
   Folder("Assets/DeadSurvivor/Scenes","Map043_LowPolyShader_r001");if(!AssetDatabase.CopyAsset(Source,ScenePath))throw new Exception("SCENE_COPY_FAILED");
   var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var s=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();string layoutSha=DSMapBlockoutPreview.Hash(s.Layout.text);
   var before=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).ToDictionary(r=>r,r=>new{path=Path3(r.transform),pos=r.transform.position,rot=r.transform.rotation,scale=r.transform.lossyScale,enabled=r.enabled&&r.gameObject.activeInHierarchy});
   int collidersBefore=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length;

   // 4a. Tripo instances -> 043 prefabs at identical transforms
   var remap=new Dictionary<Renderer,Renderer>();var replaced=new List<object>();
   var tripoRoots=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t=>t.name.Contains("_Tripo038_")&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray();
   foreach(var old in tripoRoots){
    string kind=old.name.Contains("_Tripo038_stone_")?"stone":old.name.Contains("_Tripo038_shrub_")?"shrub":throw new Exception("UNKNOWN_TRIPO:"+old.name);
    var go=(GameObject)PrefabUtility.InstantiatePrefab(kind=="stone"?stone:shrub,old.parent);go.name=old.name.Replace("_Tripo038_","_LowPoly043_");go.transform.SetSiblingIndex(old.GetSiblingIndex());
    go.transform.localPosition=old.localPosition;go.transform.localRotation=old.localRotation;go.transform.localScale=old.localScale;
    var ob=BoundsOf(old.gameObject);var nb=BoundsOf(go);if((ob.center-nb.center).magnitude>.01f||(ob.size-nb.size).magnitude>.01f)throw new Exception("PLACEMENT_MISMATCH:"+old.name);
    var oldR=old.GetComponentsInChildren<Renderer>(true);var newR=go.GetComponentsInChildren<Renderer>(true);if(oldR.Length!=newR.Length)throw new Exception("RENDERER_COUNT:"+old.name);for(int i=0;i<oldR.Length;i++)remap[oldR[i]]=newR[i];
    replaced.Add(new{from=old.name,to=go.name,center=new[]{nb.center.x,nb.center.y,nb.center.z},sizeDelta=(ob.size-nb.size).magnitude});
   }
   // Remap visibility blockers while the old renderers still exist, then delete the old instances.
   int blockersBefore=s.Visibility.Blockers.Length;
   s.Visibility.Blockers=s.Visibility.Blockers.Select(r=>r&&remap.TryGetValue(r,out var n)?n:r).ToArray();
   foreach(var old in tripoRoots)UnityEngine.Object.DestroyImmediate(old.gameObject);
   if(s.Visibility.Blockers.Length!=blockersBefore||s.Visibility.Blockers.Any(r=>!r))throw new Exception("BLOCKER_LOST");

   // 4b. generated meshes -> palette UV copies + preset material
   var meshCopies=new Dictionary<Mesh,Mesh>();var converted=new List<object>();var skipped=new List<object>();Mesh container=null;string meshAsset=fMesh+"/Meshes043.asset";
   foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include)){
    if(remap.ContainsValue(r))continue;
    var src=r.sharedMaterials.Length==1?r.sharedMaterials[0]:null;var f=r.GetComponent<MeshFilter>();
    if(src==null||f==null||!f.sharedMesh||!Role.TryGetValue(src.name,out var role)){skipped.Add(new{path=Path3(r.transform),material=src?src.name:"(multi/none)",enabled=r.enabled&&r.gameObject.activeInHierarchy});continue;}
    if(!meshCopies.TryGetValue(f.sharedMesh,out var copy)){
     copy=UnityEngine.Object.Instantiate(f.sharedMesh);copy.name=f.sharedMesh.name+"_043_c"+role.cell;copy.uv=Enumerable.Repeat(CellUV(role.cell),copy.vertexCount).ToArray();copy.uv2=null;
     if(container==null){AssetDatabase.CreateAsset(copy,meshAsset);container=copy;}else AssetDatabase.AddObjectToAsset(copy,container);
     meshCopies[f.sharedMesh]=copy;
    }
    converted.Add(new{path=Path3(r.transform),fromMaterial=src.name,fromMesh=AssetDatabase.GetAssetPath(f.sharedMesh)+"#"+f.sharedMesh.name,cell=role.cell,preset=role.preset,enabled=r.enabled&&r.gameObject.activeInHierarchy});
    f.sharedMesh=copy;r.sharedMaterials=new[]{mats[role.preset]};
   }

   // 4c. scene lighting calibrated so a sunlit top face shows roughly its palette colour
   var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Single(l=>l.type==LightType.Directional);
   var lightBefore=new{sun.intensity,color=ColorUtility.ToHtmlStringRGB(sun.color),ambientSky=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor),ambientEquator=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientEquatorColor),ambientGround=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientGroundColor)};
   sun.intensity=.55f;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color32(0xBA,0xC4,0xCE,255);RenderSettings.ambientEquatorColor=new Color32(0xA4,0xAC,0x96,255);RenderSettings.ambientGroundColor=new Color32(0x66,0x6C,0x58,255);
   s.transform.root.name="Map043_LOWPOLY_SHADER_TRIAL";

   // 5. invariants
   var after=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
   var moved=before.Where(kv=>kv.Key&&(kv.Key.transform.position!=kv.Value.pos||kv.Key.transform.rotation!=kv.Value.rot||kv.Key.transform.lossyScale!=kv.Value.scale||(kv.Key.enabled&&kv.Key.gameObject.activeInHierarchy)!=kv.Value.enabled)).Select(kv=>kv.Value.path).ToArray();
   var foreign=after.Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sharedMaterials.Any(m=>!m||m.shader!=shader)).Select(r=>Path3(r.transform)).ToArray();
   int collidersAfter=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length;
   if(moved.Length>0||foreign.Length>0||collidersAfter!=collidersBefore||DSMapBlockoutPreview.Hash(s.Layout.text)!=layoutSha||layoutSha!=s.ExpectedDataSha)throw new Exception("INVARIANT:"+JsonConvert.SerializeObject(new{moved,foreign,collidersBefore,collidersAfter}));

   if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new Exception("SAVE_FAILED");
   foreach(var m in mats.Values)AssetDatabase.SaveAssetIfDirty(m);if(container)AssetDatabase.SaveAssetIfDirty(container);
   EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var reload=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();
   if(reload.ExpectedDataSha!=layoutSha||reload.Visibility.Blockers.Any(x=>!x))throw new Exception("SAVED_BINDING_INVALID");
   var enabledR=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r=>r.enabled).ToArray();
   var result=new{status="PASS_AUTHORING_CANDIDATE",scene=ScenePath,source=Source,layoutSha,shader=shaderReport,palette=new{path=AssetDatabase.GetAssetPath(palette),palette.width,palette.height,palette.filterMode,mip=palette.mipmapCount},
    materials=mats.Values.Select(AssetDatabase.GetAssetPath).ToArray(),prefabCheck,replacedTripo=replaced.Count,replaced,converted=converted.Count,uniqueMeshCopies=meshCopies.Count,convertedDetail=converted,skipped,
    enabledRenderers=enabledR.Length,enabledUsing043=enabledR.Count(r=>r.sharedMaterials.All(m=>m&&m.shader==shader)),collidersBefore,collidersAfter,blockers=reload.Visibility.Blockers.Length,lightBefore,
    lightAfter=new{intensity=.55f,ambientSky="BAC4CE",ambientEquator="A4AC96",ambientGround="666C58"},finished=DateTimeOffset.Now.ToString("o")};
   File.WriteAllText(Out+"/build.json",JsonConvert.SerializeObject(result,Formatting.Indented));
   return JsonConvert.SerializeObject(new{result.status,result.replacedTripo,result.converted,result.uniqueMeshCopies,result.enabledRenderers,result.enabledUsing043,skipped=skipped.Count,shaderError=ShaderUtil.ShaderHasError(shader),srp=shaderReport},Formatting.Indented);
  }catch(Exception ex){File.WriteAllText(Out+"/build-failure.json",JsonConvert.SerializeObject(new{status="FAIL_PARTIAL_PRESERVED",error=ex.ToString(),art=Art,scene=ScenePath},Formatting.Indented));throw;}
  finally{Restore(saved);}
 }
}

// 043 comparison captures. Same camera recipe as Tools/Art/capture_tripo_pair_038.cs.
// Entries: Base038, Stage1, Stage2, Stage3Day, Stage3Night, Stage3DayWind, SrpCheck. No scene is saved;
// material parameters are always returned to the saved stage-3 values; scene setup is restored.
public static class LowPolyCapture043
{
 const string Out="output/lowpoly-shader-043/r001/captures";
 const string Art="Assets/DeadSurvivor/Art/LowPoly043";
 const string Scene043="Assets/DeadSurvivor/Scenes/Map043_LowPolyShader_r001/Map043_LowPolyShader.unity";
 const string Scene038="Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003/Map038_TripoPair.unity";
 static readonly (string name,float elevation,float ortho,float px,float py,int w,int h)[] Views={
  ("overview",55,8.6f,310,230,1920,980),("angle",55,6f,265,275,1560,960),("top",90,10.4f,310,230,1680,1040)};

 static object Capture(DSMapSession s,DSBlockoutData data,string file,(string name,float elevation,float ortho,float px,float py,int w,int h) v,float distance=150)
 {
  var go=new GameObject("043 temporary comparison camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();
  var rt=new RenderTexture(v.w,v.h,24);var image=new Texture2D(v.w,v.h,TextureFormat.RGB24,false);var previous=RenderTexture.active;
  try{
   cam.CopyFrom(s.ViewCamera);cam.enabled=false;cam.orthographic=true;cam.orthographicSize=v.ortho;cam.aspect=(float)v.w/v.h;cam.transform.rotation=Quaternion.Euler(v.elevation,0,0);
   cam.transform.position=DSBlockoutMesh.World(data.Logical(new Vector2(v.px,v.py),4),data.world_scale)-cam.transform.forward*distance;cam.targetTexture=rt;
   cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,v.w,v.h),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());
   return new{file,v.name,v.elevation,v.ortho,center=new[]{v.px,v.py},v.w,v.h,distance,shadowDistance=QualitySettings.shadowDistance,near=cam.nearClipPlane,far=cam.farClipPlane};
  }finally{RenderTexture.active=previous;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
 }

 // Opens the scene, runs setup, then body (default: the three standard views). Output paths are
 // checked before anything is modified; materials, globals, scene setup and selection are
 // restored in finally whether the capture succeeds or fails (043-V2).
 static object Run(string scenePath,string tag,Action<DSMapSession> setup,Func<DSMapSession,DSBlockoutData,object> body=null,string[] names=null,float distance=150)
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("DIRTY_SCENE_OPEN");
  Directory.CreateDirectory(Out);
  var planned=(names??Views.Select(v=>v.name).ToArray()).Select(n=>Out+"/"+tag+"-"+n+".png").Concat(new[]{Out+"/"+tag+".json"}).ToArray();
  foreach(var f in planned)if(f.Contains("..")||File.Exists(f))throw new Exception("UNSAFE_CAPTURE_PATH:"+f);
  var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
  try{
   EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);var s=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();var data=JsonUtility.FromJson<DSBlockoutData>(s.Layout.text);
   setup?.Invoke(s);
   object shots=body!=null?body(s,data):Views.Select(v=>Capture(s,data,Out+"/"+tag+"-"+v.name+".png",v,distance)).ToArray();
   var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Single(l=>l.type==LightType.Directional);
   var record=new{status="PASS_CAPTURE_ONLY",scene=scenePath,tag,shots,editorTime=Time.realtimeSinceStartup,sun=new{sun.intensity,color=ColorUtility.ToHtmlStringRGB(sun.color)},
    ambient=new{sky=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor),equator=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientEquatorColor),ground=ColorUtility.ToHtmlStringRGB(RenderSettings.ambientGroundColor)},
    globals=new{grade=Shader.GetGlobalVector("_DS_GradeTint"),shadowTint=Shader.GetGlobalVector("_DS_ShadowTint"),cloud=Shader.GetGlobalVector("_DS_CloudShadow"),cloudDir=Shader.GetGlobalVector("_DS_CloudDir")},captured=DateTimeOffset.Now.ToString("o")};
   File.WriteAllText(Out+"/"+tag+".json",JsonConvert.SerializeObject(record,Formatting.Indented,new JsonSerializerSettings{ReferenceLoopHandling=ReferenceLoopHandling.Ignore}));
   return JsonConvert.SerializeObject(new{tag,files=planned.Length-1});
  }finally{
   if(scenePath==Scene043)ResetFinal();else ClearGlobals();
   LowPoly043.Restore(saved); // the scene is never saved; reopening discards in-memory light changes
  }
 }

 static Material[] Mats()=>LowPoly043.Presets.Select(p=>AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/LowPoly043_"+p+".mat")).ToArray();
 static void Stage(int n){foreach(var p in LowPoly043.Presets){var m=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/LowPoly043_"+p+".mat");LowPoly043.ApplyStage(m,p,n);}}
 static void ResetFinal(){Stage(3);foreach(var m in Mats())AssetDatabase.SaveAssetIfDirty(m);ClearGlobals();}
 static void ClearGlobals(){Shader.SetGlobalVector("_DS_GradeTint",Vector4.zero);Shader.SetGlobalVector("_DS_ShadowTint",Vector4.zero);Shader.SetGlobalVector("_DS_CloudShadow",Vector4.zero);Shader.SetGlobalVector("_DS_CloudDir",Vector4.zero);}
 static void DayGlobals(){Shader.SetGlobalVector("_DS_GradeTint",Vector4.zero);Shader.SetGlobalVector("_DS_CloudShadow",new Vector4(.45f,5.5f,.02f,0));Shader.SetGlobalVector("_DS_CloudDir",new Vector4(1f,.35f,0,0));Shader.SetGlobalVector("_DS_ShadowTint",new Vector4(.80f,.88f,1f,.35f));}
 static void Night(DSMapSession s){
  var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Single(l=>l.type==LightType.Directional);sun.intensity=.16f;sun.color=new Color32(0x9F,0xB6,0xE8,255);
  RenderSettings.ambientSkyColor=new Color32(0x3A,0x4C,0x74,255);RenderSettings.ambientEquatorColor=new Color32(0x2A,0x36,0x52,255);RenderSettings.ambientGroundColor=new Color32(0x16,0x1C,0x28,255);
  Shader.SetGlobalVector("_DS_ShadowTint",new Vector4(.55f,.66f,1f,.6f));Shader.SetGlobalVector("_DS_CloudShadow",Vector4.zero);
  Shader.SetGlobalVector("_DS_GradeTint",new Vector4(.55f,.78f,.95f,.65f));
 }

 public static object Base038()=>Run(Scene038,"038-base",null);
 public static object Stage1()=>Run(Scene043,"043-stage1",s=>{ClearGlobals();Stage(1);});
 public static object Stage2()=>Run(Scene043,"043-stage2",s=>{ClearGlobals();Stage(2);});
 public static object Stage3Day()=>Run(Scene043,"043-stage3-day",s=>{Stage(3);DayGlobals();});
 // 043-V4: camera inside the 50-unit shadow distance (orthographic, so framing is unchanged).
 // Cloud shadows off so only real shadow casting differs from the stage captures.
 public static object Shadow038()=>Run(Scene038,"038-shadow-d25",null,distance:25);
 public static object Shadow043()=>Run(Scene043,"043-shadow-d25",s=>{Stage(3);DayGlobals();Shader.SetGlobalVector("_DS_CloudShadow",Vector4.zero);},distance:25);
 // 043-V4: shadow-only difference. Same call, same camera (distance 25): sun shadows on vs off.
 static object ShadowPair(DSMapSession s,DSBlockoutData data,string tag){var v=Views.Single(x=>x.name=="angle");var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Single(l=>l.type==LightType.Directional);var mode=sun.shadows;
  try{sun.shadows=LightShadows.Soft;var on=Capture(s,data,Out+"/"+tag+"-on.png",v,25);sun.shadows=LightShadows.None;var off=Capture(s,data,Out+"/"+tag+"-off.png",v,25);return new{on,off,originalMode=mode.ToString(),QualitySettings.shadowDistance,QualitySettings.shadowCascades};}finally{sun.shadows=mode;}}
 // 043-V5/V7: additional (point) light path on the active renderer (PC_Renderer = Forward+, cluster loop).
 // Night look, temporary point light near the centre remnant, on vs off in one call; nothing is saved.
 public static object PointLightPair043()=>Run(Scene043,"043-pointlight",s=>{Stage(3);Night(s);},(s,data)=>{
  var v=Views.Single(x=>x.name=="angle");var go=new GameObject("043 temporary point light"){hideFlags=HideFlags.DontSave};var l=go.AddComponent<Light>();
  try{l.type=LightType.Point;l.color=new Color32(0xFF,0xC0,0x70,255);l.intensity=3f;l.range=4f;l.shadows=LightShadows.None;
   go.transform.position=DSBlockoutMesh.World(data.Logical(new Vector2(260,268),4),data.world_scale)+Vector3.up*1.2f;
   var on=Capture(s,data,Out+"/043-pointlight-on.png",v);l.enabled=false;var off=Capture(s,data,Out+"/043-pointlight-off.png",v);
   var ur=(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
   return new{on,off,lightPos=new[]{go.transform.position.x,go.transform.position.y,go.transform.position.z},l.range,l.intensity,pipeline=ur?ur.name:null};}
  finally{UnityEngine.Object.DestroyImmediate(go);}
 },new[]{"on","off"});
 public static object ShadowPair038()=>Run(Scene038,"038-shadowpair",null,(s,d)=>ShadowPair(s,d,"038-shadowpair"),new[]{"on","off"});
 public static object ShadowPair043()=>Run(Scene043,"043-shadowpair",s=>{Stage(3);DayGlobals();Shader.SetGlobalVector("_DS_CloudShadow",Vector4.zero);},(s,d)=>ShadowPair(s,d,"043-shadowpair"),new[]{"on","off"});
 // Wind proof at one editor time: identical state except foliage wind 0 vs exaggerated 0.2.
 static void Wind(float w){var m=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/LowPoly043_Foliage.mat");m.SetFloat("_WindStrength",w);}
 // 043-V3: both renders inside ONE editor call (same _Time), every time-dependent global off,
 // only foliage wind differs. Records each shrub renderer's screen rectangle for the diff check.
 public static object WindProof()=>Run(Scene043,"043-windproof",s=>{Stage(3);ClearGlobals();},(s,data)=>{
  var v=Views.Single(x=>x.name=="angle");var t0=Time.realtimeSinceStartup;
  Wind(0);var a=Capture(s,data,Out+"/043-windproof-w000.png",v);Wind(.2f);var b=Capture(s,data,Out+"/043-windproof-w020.png",v);var t1=Time.realtimeSinceStartup;
  var go=new GameObject("043 rect camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();
  try{cam.CopyFrom(s.ViewCamera);cam.orthographic=true;cam.orthographicSize=v.ortho;cam.aspect=(float)v.w/v.h;cam.transform.rotation=Quaternion.Euler(v.elevation,0,0);
   cam.transform.position=DSBlockoutMesh.World(data.Logical(new Vector2(v.px,v.py),4),data.world_scale)-cam.transform.forward*150;cam.aspect=(float)v.w/v.h; // viewport projection only: independent of Game view size
   var rects=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(r=>r.sharedMaterial&&r.sharedMaterial.name=="LowPoly043_Foliage").Select(r=>{var bb=r.bounds;bb.Expand(.25f);
    var pts=new[]{0,1}.SelectMany(i=>new[]{0,1}.SelectMany(j=>new[]{0,1}.Select(k=>Vector3.Scale(cam.WorldToViewportPoint(new Vector3(i==0?bb.min.x:bb.max.x,j==0?bb.min.y:bb.max.y,k==0?bb.min.z:bb.max.z)),new Vector3(v.w,v.h,1))))).ToArray();
    return new{name=r.transform.parent?r.transform.parent.name:r.name,x0=pts.Min(p=>p.x),x1=pts.Max(p=>p.x),y0=v.h-pts.Max(p=>p.y),y1=v.h-pts.Min(p=>p.y)};}).ToArray();
   return new{a,b,sameEditorTime=t0==t1,t0,t1,shrubRectsTopLeftOrigin=rects,boundsMargin=.25f};}
  finally{UnityEngine.Object.DestroyImmediate(go);}
 },new[]{"w000","w020"});
 public static object Stage3Night()=>Run(Scene043,"043-stage3-night",s=>{Stage(3);Night(s);});
 public static object Stage3NightNoTint()=>Run(Scene043,"043-stage3-night-notint",s=>{Stage(3);Night(s);Shader.SetGlobalVector("_DS_ShadowTint",Vector4.zero);Shader.SetGlobalVector("_DS_GradeTint",Vector4.zero);});
 // 043-V2 recovery probe: reports the saved stage-3 material values and global values.
 public static object StateCheck(){var f=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/LowPoly043_Foliage.mat");var g=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/LowPoly043_Ground.mat");
  return JsonConvert.SerializeObject(new{foliageWind=f.GetFloat("_WindStrength"),foliageDirty=EditorUtility.IsDirty(f),groundNoise=g.GetFloat("_NoiseStrength"),cloud=Shader.GetGlobalVector("_DS_CloudShadow").ToString(),grade=Shader.GetGlobalVector("_DS_GradeTint").ToString(),scene=EditorSceneManager.GetActiveScene().path,selection=Selection.objects.Length});}

 public static object SrpCheck()
 {
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"/Shaders/DSLowPolyPalette.shader");
  var mi=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",BindingFlags.NonPublic|BindingFlags.Static);var why=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason",BindingFlags.NonPublic|BindingFlags.Static);
  var lit=Shader.Find("Universal Render Pipeline/Lit");
  object Codes(Shader sh){var list=new List<object>();for(int i=0;i<sh.subshaderCount;i++){int code=(int)mi.Invoke(null,new object[]{sh,i});list.Add(new{subShader=i,code,reason=code!=0&&why!=null?why.Invoke(null,new object[]{sh,i,code}):"compatible"});}return list;}
  return JsonConvert.SerializeObject(new{ours=Codes(shader),urpLitForReference=Codes(lit),subshaders=shader.subshaderCount,hasError=ShaderUtil.ShaderHasError(shader),messages=ShaderUtil.GetShaderMessages(shader).Length},Formatting.Indented);
 }
}
