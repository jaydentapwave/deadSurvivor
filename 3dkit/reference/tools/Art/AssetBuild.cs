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
using Newtonsoft.Json.Linq;

// 050: Unity side of the official asset pipeline (Tools/Art/assets.py writes the request, this file does the editor work).
// Standard and registry: docs/art/asset-catalog.json, docs/workflows/asset-standard.md.
// Entries (run through assets.py, which writes output/assets/<entry>-request.json first):
//   Move    - one-time migrations with AssetDatabase.MoveAsset (GUIDs kept, so every scene that used the asset keeps working)
//   Build   - per asset: palette texture import (spec size, ASTC on mobile), shared-shader material, monster prefab
//             (root at the feet -> Visual -> model normalised to height 1, front baked to +Z), actor-set entries
//   Check   - measures every registered asset and the dependencies of the build scenes (assets.py judges against the standard)
//   Capture - game-angle and front line-ups of every actor-set character in a temporary additive scene
// Owns: Art/Characters/Heroes/<hero>/, Art/Characters/Monsters/, the hero base prefab's default material and the
// contents of Art/Characters/ActorSet_Main.asset. It never edits a scene: Map046_Full belongs to Tools/Map/build_map_046.cs.
public static class DSAssetBuild
{
 const string Root="<PROJECT_ROOT>";
 const string Out="output/assets";
 static void Guard()
 {
  if(Application.dataPath!=Root+"/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
 }
 static JObject Request(string entry)=>JObject.Parse(File.ReadAllText($"{Out}/{entry}-request.json"));
 static object Done(string entry,object result){File.WriteAllText($"{Out}/{entry}-result.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;}
 static void EnsureFolder(string path)
 {
  if(AssetDatabase.IsValidFolder(path))return;
  var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
  AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
 }

 // ---------------- Move ----------------
 public static object Move()
 {
  Guard();
  for(int i=0;i<EditorSceneManager.sceneCount;i++){var s=EditorSceneManager.GetSceneAt(i);if(s.isDirty)throw new Exception("UNSAVED_SCENE:"+s.path);}
  var rows=new List<object>();
  foreach(var m in Request("Move")["moves"])
  {
   string from=(string)m[0],to=(string)m[1];
   string gFrom=AssetDatabase.AssetPathToGUID(from),gTo=AssetDatabase.AssetPathToGUID(to);
   if(string.IsNullOrEmpty(gFrom)&&!string.IsNullOrEmpty(gTo)){rows.Add(new{from,to,state="ALREADY_MOVED",guid=gTo});continue;}
   if(string.IsNullOrEmpty(gFrom))throw new Exception("MISSING:"+from);
   if(!string.IsNullOrEmpty(gTo))throw new Exception("TARGET_EXISTS:"+to);
   EnsureFolder(Path.GetDirectoryName(to).Replace('\\','/'));
   var err=AssetDatabase.MoveAsset(from,to);if(!string.IsNullOrEmpty(err))throw new Exception("MOVE_FAILED:"+from+":"+err);
   if(AssetDatabase.AssetPathToGUID(to)!=gFrom)throw new Exception("GUID_CHANGED:"+to);
   rows.Add(new{from,to,state="MOVED",guid=gFrom});
  }
  AssetDatabase.SaveAssets();
  return Done("Move",new{moves=rows});
 }

 // ---------------- Build ----------------
 public static object Build()
 {
  Guard();
  var req=Request("Build");
  var shader=Shader.Find((string)req["shader"])??throw new Exception("NO_SHADER:"+req["shader"]);
  var setPath=(string)req["actor_set"];
  var set=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(setPath)??throw new Exception("NO_ACTOR_SET:"+setPath);
  var rows=new List<object>();var mats=new Dictionary<string,Material>();var prefabs=new Dictionary<string,GameObject>();
  foreach(var it in req["items"])
  {
   string id=(string)it["id"],name=(string)it["name"],kind=(string)it["kind"],folder=(string)it["folder"];
   EnsureFolder(folder);
   var tex=ImportTexture((string)it["texture_src"],$"{folder}/{name}_albedo.png",(int)it["texture_size"]);
   var mat=Material(folder+"/"+(string)it["material_name"]+".mat",shader,tex,(JObject)it["preset"]);mats[id]=mat;
   GameObject prefab=null;
   if(it["model_src"]!=null&&it["model_src"].Type!=JTokenType.Null)
   {
    var fbx=ImportModel((string)it["model_src"],$"{folder}/{name}.fbx");
    prefab=Prefab(fbx,$"{folder}/{(string)it["prefab_name"]}.prefab",mat,(float)it["yaw"],(string)it["shadow"]);prefabs[id]=prefab;
   }
   rows.Add(new{id,texture=AssetDatabase.GetAssetPath(tex),material=AssetDatabase.GetAssetPath(mat),prefab=prefab?AssetDatabase.GetAssetPath(prefab):null});
  }
  // actor-set entries: heroes keep the shared base prefab and get their palette material; monsters get the new prefab
  // (its own material) and yawOffset 0 because the front is baked into the model
  var so=new SerializedObject(set);
  foreach(var it in req["items"])
  {
   if(it["actor_list"]==null||it["actor_list"].Type==JTokenType.Null)continue;
   string list=(string)it["actor_list"],aid=(string)it["actor_id"],id=(string)it["id"];
   var arr=so.FindProperty(list);int found=-1;
   for(int i=0;i<arr.arraySize;i++)if(arr.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue==aid)found=i;
   if(found<0)throw new Exception("NO_ACTOR_ENTRY:"+list+"/"+aid);
   var e=arr.GetArrayElementAtIndex(found);
   if(list=="Heroes"){e.FindPropertyRelative("material").objectReferenceValue=mats[id];}
   else{e.FindPropertyRelative("prefab").objectReferenceValue=prefabs[id];e.FindPropertyRelative("material").objectReferenceValue=null;e.FindPropertyRelative("yawOffset").floatValue=0;}
  }
  so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(set);
  // the shared hero prefab's own renderer material (used when an entry has no material) must not point at a trial folder
  object heroDefault=null;
  if(req["hero_prefab"]!=null&&req["hero_prefab"].Type!=JTokenType.Null)
  {
   string hp=(string)req["hero_prefab"];var def=mats[(string)req["hero_default_item"]];
   var root=PrefabUtility.LoadPrefabContents(hp);
   try{foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=Enumerable.Repeat(def,r.sharedMaterials.Length).ToArray();PrefabUtility.SaveAsPrefabAsset(root,hp);}
   finally{PrefabUtility.UnloadPrefabContents(root);}
   heroDefault=new{prefab=hp,material=AssetDatabase.GetAssetPath(def)};
  }
  AssetDatabase.SaveAssets();
  // verify what was saved (046-12 lesson: re-read, do not trust the in-memory object)
  Resources.UnloadAsset(set);var check=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(setPath);var bad=new List<string>();
  foreach(var it in req["items"])
  {
   if(it["actor_list"]==null||it["actor_list"].Type==JTokenType.Null)continue;
   string list=(string)it["actor_list"],aid=(string)it["actor_id"],id=(string)it["id"];
   var e=(list=="Heroes"?check.Heroes:check.Monsters).First(x=>x.id==aid);
   if(list=="Heroes"?e.material!=mats[id]:(e.prefab!=prefabs[id]||e.material||e.yawOffset!=0))bad.Add(list+"/"+aid);
  }
  if(bad.Count>0)throw new Exception("ACTOR_SET_NOT_SAVED:"+string.Join(",",bad));
  return Done("Build",new{items=rows,heroDefault,actorSet=setPath});
 }

 static Texture2D ImportTexture(string src,string dst,int size)
 {
  if(!File.Exists(src))throw new Exception("MISSING_TEXTURE:"+src);
  File.Copy(src,dst,true);AssetDatabase.ImportAsset(dst,ImportAssetOptions.ForceUpdate);
  var ti=(TextureImporter)AssetImporter.GetAtPath(dst);
  ti.textureType=TextureImporterType.Default;ti.sRGBTexture=true;ti.alphaSource=TextureImporterAlphaSource.None;ti.alphaIsTransparency=false;
  ti.mipmapEnabled=true;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;ti.npotScale=TextureImporterNPOTScale.None;
  ti.maxTextureSize=size;ti.textureCompression=TextureImporterCompression.Compressed;ti.isReadable=false;
  foreach(var p in new[]{"Android","iPhone"})
   ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name=p,overridden=true,maxTextureSize=size,format=TextureImporterFormat.ASTC_4x4,textureCompression=TextureImporterCompression.Compressed});
  ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=size,format=TextureImporterFormat.Automatic,textureCompression=TextureImporterCompression.Compressed});
  ti.SaveAndReimport();
  return AssetDatabase.LoadAssetAtPath<Texture2D>(dst)??throw new Exception("TEXTURE_NOT_IMPORTED:"+dst);
 }

 static Material Material(string path,Shader shader,Texture2D tex,JObject preset)
 {
  var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!mat){mat=new Material(shader){name=Path.GetFileNameWithoutExtension(path)};AssetDatabase.CreateAsset(mat,path);}
  mat.shader=shader;mat.SetTexture("_Palette",tex);mat.SetColor("_BaseColor",Color.white);mat.enableInstancing=false;
  foreach(var kv in preset){if(!mat.HasProperty(kv.Key))throw new Exception("NO_PROPERTY:"+kv.Key);mat.SetFloat(kv.Key,(float)kv.Value);}
  if(mat.GetFloat("_FlatShading")>.5f)mat.EnableKeyword("_FLAT_SHADING");else mat.DisableKeyword("_FLAT_SHADING");
  EditorUtility.SetDirty(mat);return mat;
 }

 static GameObject ImportModel(string src,string dst)
 {
  if(!File.Exists(src))throw new Exception("MISSING_MODEL:"+src);
  File.Copy(src,dst,true);AssetDatabase.ImportAsset(dst,ImportAssetOptions.ForceUpdate);
  var mi=(ModelImporter)AssetImporter.GetAtPath(dst);
  mi.materialImportMode=ModelImporterMaterialImportMode.None;mi.importAnimation=false;mi.animationType=ModelImporterAnimationType.None;
  mi.isReadable=false;mi.importBlendShapes=false;mi.importCameras=false;mi.importLights=false;mi.importVisibility=false;
  mi.SaveAndReimport();
  return AssetDatabase.LoadAssetAtPath<GameObject>(dst)??throw new Exception("MODEL_NOT_IMPORTED:"+dst);
 }

 // root (feet, identity) -> "Visual" (DSMapActorViews turns/bobs/scales it every frame) -> model: turned by yaw so the
 // front is +Z, normalised to height 1, feet at 0, centred on XZ
 static GameObject Prefab(GameObject fbx,string path,Material mat,float yaw,string shadow)
 {
  var root=new GameObject(Path.GetFileNameWithoutExtension(path));var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
  try
  {
   var model=(GameObject)PrefabUtility.InstantiatePrefab(fbx);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   model.transform.SetParent(visual.transform,false);model.transform.localRotation=Quaternion.Euler(0,yaw,0);
   var anim=model.GetComponent<Animator>();if(anim)UnityEngine.Object.DestroyImmediate(anim);
   var rs=model.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new Exception("NO_RENDERER:"+path);
   var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
   float s=1f/Mathf.Max(.001f,b.size.y);model.transform.localScale=Vector3.one*s;
   model.transform.localPosition=new Vector3(-b.center.x*s,-b.min.y*s,-b.center.z*s);
   foreach(var r in rs)
   {
    r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
    r.shadowCastingMode=shadow=="real"?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;
    r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
   }
   return PrefabUtility.SaveAsPrefabAsset(root,path);
  }
  finally{UnityEngine.Object.DestroyImmediate(root);}
 }

 // ---------------- Check (measure only; assets.py judges) ----------------
 public static object Check()
 {
  Guard();
  var req=Request("Check");var items=new List<object>();
  foreach(var it in req["items"])
  {
   var row=new Dictionary<string,object>{["id"]=(string)it["id"]};
   string prefab=(string)it["prefab"],material=(string)it["material"],texture=(string)it["texture"],folder=(string)it["folder"];
   row["folder_exists"]=AssetDatabase.IsValidFolder(folder);
   if(!string.IsNullOrEmpty(prefab))row["prefab"]=MeasurePrefab(prefab,(bool?)it["model_child"]??false);
   if(!string.IsNullOrEmpty(material)){var m=AssetDatabase.LoadAssetAtPath<Material>(material);row["material"]=m?new{shader=m.shader.name,palette=m.HasProperty("_Palette")&&m.GetTexture("_Palette")?AssetDatabase.GetAssetPath(m.GetTexture("_Palette")):null,top=m.HasProperty("_TopStrength")?m.GetFloat("_TopStrength"):-1}:null;}
   if(!string.IsNullOrEmpty(texture))row["texture"]=MeasureTexture(texture);
   items.Add(row);
  }
  // build scenes: every dependency path, and the shader of every DeadSurvivor material they use
  var scenes=new List<object>();
  foreach(var sc in req["scenes"])
  {
   string path=(string)sc;var deps=AssetDatabase.GetDependencies(path,true).Where(d=>d.StartsWith("Assets/")&&!d.EndsWith(".cs")).ToArray();
   var matShaders=deps.Where(d=>d.EndsWith(".mat")&&d.StartsWith("Assets/DeadSurvivor/")).Select(d=>{var m=AssetDatabase.LoadAssetAtPath<Material>(d);return new{path=d,shader=m?m.shader.name:null};}).ToArray();
   var tex=deps.Where(d=>d.StartsWith("Assets/DeadSurvivor/Art/")&&AssetDatabase.GetMainAssetTypeAtPath(d)==typeof(Texture2D)).Select(d=>new{path=d,t=MeasureTexture(d)}).ToArray();
   scenes.Add(new{scene=path,deps,materials=matShaders,textures=tex});
  }
  var setPath=(string)req["actor_set"];var set=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(setPath);
  object actors=set?new{heroes=set.Heroes.Select(e=>new{e.id,prefab=AssetDatabase.GetAssetPath(e.prefab),material=e.material?AssetDatabase.GetAssetPath(e.material):null,e.yawOffset}).ToArray(),
                     monsters=set.Monsters.Select(e=>new{e.id,prefab=AssetDatabase.GetAssetPath(e.prefab),material=e.material?AssetDatabase.GetAssetPath(e.material):null,e.yawOffset}).ToArray(),
                     hit=set.HitMaterial?AssetDatabase.GetAssetPath(set.HitMaterial):null,blob=set.MonsterBlobShadow?AssetDatabase.GetAssetPath(set.MonsterBlobShadow):null}:null;
  return Done("Check",new{items,scenes,actors});
 }

 static object MeasurePrefab(string path,bool modelChild)
 {
  if(!File.Exists(path))return new{missing=true};
  if(path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase))
  {
   // a model the map builder places itself (materials come from the builder): triangles only
   var meshes=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
   return new{tris=meshes.Sum(m=>m.triangles.Length/3),bones=0,renderers=meshes.Length,materials=0,shaders=new string[0],shadows=new string[0],height=-1f,feet=-1f,centre=-1f,visual=(string)null,yaw=float.NaN,materialPaths=new string[0]};
  }
  var root=PrefabUtility.LoadPrefabContents(path);
  try
  {
   int tris=0,bones=0;var shaders=new HashSet<string>();var mats=new HashSet<Material>();var shadows=new HashSet<string>();
   foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))if(mf.sharedMesh)tris+=mf.sharedMesh.triangles.Length/3;
   foreach(var sm in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(sm.sharedMesh)tris+=sm.sharedMesh.triangles.Length/3;bones=Math.Max(bones,sm.bones.Length);}
   var rs=root.GetComponentsInChildren<Renderer>(true);
   foreach(var r in rs){foreach(var m in r.sharedMaterials)if(m){mats.Add(m);shaders.Add(m.shader.name);}shadows.Add(r.shadowCastingMode.ToString());}
   float height=-1,feet=-1,centre=-1;
   if(rs.Length>0)
   {
    var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
    if(rs.All(r=>r is SkinnedMeshRenderer)){var sm=(SkinnedMeshRenderer)rs[0];b=sm.bounds;}
    height=b.size.y;feet=b.min.y;centre=new Vector2(b.center.x,b.center.z).magnitude;
   }
   float yaw=float.NaN;var visual=root.transform.childCount>0?root.transform.GetChild(0):null;
   if(modelChild&&visual&&visual.childCount>0)yaw=visual.GetChild(0).localEulerAngles.y;
   return new{tris,bones,renderers=rs.Length,materials=mats.Count,shaders=shaders.ToArray(),shadows=shadows.ToArray(),height,feet,centre,visual=visual?visual.name:null,yaw,
              materialPaths=mats.Select(m=>AssetDatabase.GetAssetPath(m)).ToArray()};
  }
  finally{PrefabUtility.UnloadPrefabContents(root);}
 }

 static object MeasureTexture(string path)
 {
  var ti=AssetImporter.GetAtPath(path) as TextureImporter;var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  if(!ti||!t)return new{missing=true};
  var a=ti.GetPlatformTextureSettings("Android");var i=ti.GetPlatformTextureSettings("iPhone");
  return new{width=t.width,height=t.height,max=ti.maxTextureSize,mip=ti.mipmapEnabled,alpha=ti.alphaSource.ToString(),filter=ti.filterMode.ToString(),
             android=a.overridden?a.format.ToString()+"@"+a.maxTextureSize:"default@"+ti.maxTextureSize,ios=i.overridden?i.format.ToString()+"@"+i.maxTextureSize:"default@"+ti.maxTextureSize};
 }

 // ---------------- Capture ----------------
 // Line-ups in a temporary additive scene far from the map (lit by the open scene's sun), closed without saving.
 public static object Capture()
 {
  Guard();
  var req=Request("Capture");var set=AssetDatabase.LoadAssetAtPath<DSMapActorSet>((string)req["actor_set"])??throw new Exception("NO_ACTOR_SET");
  var active=EditorSceneManager.GetActiveScene();if(active.isDirty)throw new Exception("UNSAVED_SCENE:"+active.path);
  var temp=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);var shots=new List<string>();
  try
  {
   var go=new GameObject("AssetLineup050_Temp");SceneManager_Move(go,temp);float x=0;
   foreach(var (e,hero) in set.Heroes.Select(h=>(h,true)).Concat(set.Monsters.Select(m=>(m,false))))
   {
    var inst=(GameObject)UnityEngine.Object.Instantiate(e.prefab,go.transform);inst.transform.position=new Vector3(500+x,0,500);
    inst.transform.localScale=Vector3.one*(hero?set.HeroHeight:set.MonsterHeight)*2f;
    inst.transform.GetChild(0).localRotation=Quaternion.Euler(0,e.yawOffset,0);
    if(e.material)foreach(var rr in inst.GetComponentsInChildren<Renderer>())rr.sharedMaterials=Enumerable.Repeat(e.material,rr.sharedMaterials.Length).ToArray();
    // note: rigged heroes stay in the edit-mode bind pose (faces sideways, S3-21); judge heroes from a Play capture
    x+=1.5f;
   }
   var camGo=new GameObject("LineupCam");camGo.transform.SetParent(go.transform);var cam=camGo.AddComponent<Camera>();cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.31f,.61f,.2f);
   var c=new Vector3(500+(x-1.5f)/2,.6f,500);
   foreach(var (name,rot,size) in new[]{("lineup_game",Quaternion.Euler(55,0,0),2.6f),("lineup_front",Quaternion.Euler(8,180,0),2.6f)})
   {
    cam.orthographicSize=size;cam.transform.SetPositionAndRotation(c-(rot*Vector3.forward)*30,rot);
    var rt=new RenderTexture(2400,700,24){antiAliasing=4};cam.targetTexture=rt;cam.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
    var tex=new Texture2D(2400,700,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,2400,700),0,0);tex.Apply();RenderTexture.active=prev;cam.targetTexture=null;
    var file=$"{Out}/{(string)req["tag"]}-{name}.png";File.WriteAllBytes(file,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);shots.Add(file);
   }
  }
  finally{EditorSceneManager.CloseScene(temp,true);}
  if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("ACTIVE_SCENE_DIRTIED");
  return Done("Capture",new{shots});
 }
 static void SceneManager_Move(GameObject go,UnityEngine.SceneManagement.Scene s)=>UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,s);
}
