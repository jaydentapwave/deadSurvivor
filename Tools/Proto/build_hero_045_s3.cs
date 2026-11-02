using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using Newtonsoft.Json;

// 045 stage 3 hero test (bruno only). Owns Scenes/Proto045/Art_s3 and the new scene Proto045_Main_s3 (copy of s2) whose
// actor set swaps bruno to the Tripo 045 hero; every other entry and every stage-1/s2 file stays as it is (fingerprinted
// before/after). Then renders, in edit mode with the game camera (ortho 6, 55 deg, distance 30), the 042 placeholder
// hero and the 045 hero side by side (4 facings x idle/run) and discards those temporary instances.
// Size: the 042 prefab was normalised with renderer.bounds (loose for skinned meshes), so its real mesh height is below 1.
// The 045 hero is normalised to that same real mesh height so both draw at the same on-screen size (approved look).
// Refuses if Art_s3, the scene or the output folder exists; on failure deletes only what it created.
public static class Hero045BuildS3
{
 const string Dir="Assets/DeadSurvivor/Scenes/Proto045";
 const string Art=Dir+"/Art_s3";
 const string Source=Dir+"/Proto045_Main_s2.unity";
 const string Target=Dir+"/Proto045_Main_s3.unity";
 const string In="output/keyart-target-045/r003/tripo/hero_bruno045";
 const string Out="output/keyart-target-045/r003/unity";
 static string Sha(string p){using var s=SHA256.Create();return string.Concat(s.ComputeHash(File.ReadAllBytes(p)).Select(x=>x.ToString("x2")));}
 // protected scope (impl review s3 r001 #4): every stage-1/s2 file under Scenes/Proto045 (except this tool's own Art_s3 and
 // scene) plus the 042 originals the s1/s2 actor set references; the before/after hash lists are saved to the output
 const string Char042="Assets/DeadSurvivor/Art/Characters/Test042_r001";
 static Dictionary<string,string> Snapshot()=>Directory.GetFiles(Dir,"*",SearchOption.AllDirectories).Where(f=>!f.Contains("/Art_s3")&&!f.StartsWith(Target))
  .Concat(Directory.GetFiles(Char042,"*",SearchOption.AllDirectories)).OrderBy(f=>f).ToDictionary(f=>f,Sha);

 // exact world-space vertices / bounds of all meshes under g (skinned meshes baked in their current pose)
 static Bounds Exact(GameObject g){var pts=Verts(g);var b=new Bounds(pts[0],Vector3.zero);foreach(var p in pts)b.Encapsulate(p);return b;}
 static List<Vector3> Verts(GameObject g)
 {
  var pts=new List<Vector3>();
  // BakeMesh(useScale:true) + localToWorldMatrix is the only combination consistent across both heroes (probe 2026-11-02:
  // the 042 SMR node has scale 100 under a 0.684 root; renderer.bounds of skinned meshes is ~1.45x too tall)
  foreach(var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)){var m=new Mesh();smr.BakeMesh(m,true);var M=smr.transform.localToWorldMatrix;pts.AddRange(m.vertices.Select(v=>M.MultiplyPoint3x4(v)));UnityEngine.Object.DestroyImmediate(m);}
  foreach(var mf in g.GetComponentsInChildren<MeshFilter>(true)){var M=mf.transform.localToWorldMatrix;pts.AddRange(mf.sharedMesh.vertices.Select(v=>M.MultiplyPoint3x4(v)));}
  if(pts.Count==0)throw new Exception("NO_VERTICES:"+g.name);
  return pts;
 }

 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE");
  if(AssetDatabase.IsValidFolder(Art)||Directory.Exists(Art)||File.Exists(Target))throw new Exception("DESTINATION_EXISTS");
  if(Directory.Exists(Out))throw new Exception("OUTPUT_EXISTS");
  if(!File.Exists(In+"/retarget/model.fbx")||!File.Exists(In+"/unity-input/basecolor-fixed.png")||!File.Exists(Source))throw new Exception("INPUT_MISSING");
  var before=Snapshot();
  var setup=EditorSceneManager.GetSceneManagerSetup();var created=new List<string>();var checks=new Dictionary<string,object>();object result;bool animMode=false;
  Directory.CreateDirectory(Out);
  try
  {
   AssetDatabase.CreateFolder(Dir,"Art_s3");created.Add(Art);
   // texture: Tripo base colour with the eyes painted on the back of the head removed (Tools/Proto/fix_hero_back_eyes_045.py)
   File.Copy(In+"/retarget/model.fbx",Art+"/hero_bruno045.fbx");File.Copy(In+"/unity-input/basecolor-fixed.png",Art+"/hero_bruno045_basecolor.png");
   checks["inputs"]=new{fbx=Sha(In+"/retarget/model.fbx"),basecolorOriginal=Sha(In+"/unity-input/basecolor.png"),basecolorFixed=Sha(In+"/unity-input/basecolor-fixed.png")};
   AssetDatabase.ImportAsset(Art+"/hero_bruno045_basecolor.png");
   var ti=(TextureImporter)AssetImporter.GetAtPath(Art+"/hero_bruno045_basecolor.png");ti.maxTextureSize=512;ti.SaveAndReimport();
   AssetDatabase.ImportAsset(Art+"/hero_bruno045.fbx");
   // same import rules as the 042 hero: humanoid avatar from this model, looping clips, root locked, no FBX materials
   var imp=(ModelImporter)AssetImporter.GetAtPath(Art+"/hero_bruno045.fbx");
   imp.animationType=ModelImporterAnimationType.Human;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.importAnimation=true;
   imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.importCameras=false;imp.importLights=false;
   var defs=imp.defaultClipAnimations;foreach(var c in defs){c.loopTime=true;c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;}imp.clipAnimations=defs;imp.SaveAndReimport();
   var fbx=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/hero_bruno045.fbx");var all=AssetDatabase.LoadAllAssetsAtPath(Art+"/hero_bruno045.fbx");
   var avatar=all.OfType<Avatar>().FirstOrDefault();if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("AVATAR_INVALID");
   var clips=all.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
   var idle=clips.Single(c=>c.name.ToLowerInvariant().EndsWith("idle"));var run=clips.Single(c=>c.name.ToLowerInvariant().EndsWith("run"));
   checks["clips"]=clips.Select(c=>new{c.name,c.length,c.isLooping,c.isHumanMotion}).ToArray();
   var ctrl=AnimatorController.CreateAnimatorControllerAtPath(Art+"/Hero045s3.controller");   // same graph as stage-1 Hero045.controller
   ctrl.AddParameter("Run",AnimatorControllerParameterType.Bool);var sm=ctrl.layers[0].stateMachine;
   var si=sm.AddState("Idle");si.motion=idle;var sr=sm.AddState("Run");sr.motion=run;sm.defaultState=si;
   var t1=si.AddTransition(sr);t1.AddCondition(AnimatorConditionMode.If,0,"Run");t1.hasExitTime=false;t1.duration=.08f;
   var t2=sr.AddTransition(si);t2.AddCondition(AnimatorConditionMode.IfNot,0,"Run");t2.hasExitTime=false;t2.duration=.12f;
   var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Hero045s3"};   // same shader/settings as Hero042.mat (fair comparison)
   mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/hero_bruno045_basecolor.png"));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.12f);
   AssetDatabase.CreateAsset(mat,Art+"/Hero045s3.mat");

   // prefab: root (feet) -> "Visual" -> model faced to +Z, normalised to height 1, feet at 0, centred (stage-1 layout)
   var root=new GameObject("Hero045_bruno");var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
   var model=(GameObject)PrefabUtility.InstantiatePrefab(fbx);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.transform.SetParent(visual.transform,false);
   Transform Bone(string n)=>model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==n)??throw new Exception("BONE_MISSING:"+n);
   float Facing(string l,string r){var right=Bone(r).position-Bone(l).position;right.y=0;return Vector3.SignedAngle(Vector3.Cross(right.normalized,Vector3.up),Vector3.forward,Vector3.up);}
   float fe=Facing("Left_Eye","Right_Eye"),fh=Facing("Left_Hand","Right_Hand");
   if(Mathf.Abs(Mathf.DeltaAngle(fe,fh))>20)throw new Exception("FACING_AMBIGUOUS:"+fe+"/"+fh);
   float yawFix=Mathf.Abs(Mathf.DeltaAngle(fe,Mathf.Round(fe/90f)*90f))<15?Mathf.Round(fe/90f)*90f:fe;
   model.transform.localRotation=Quaternion.Euler(0,yawFix,0)*model.transform.localRotation;
   var oldPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Art/Hero045.prefab");var probe=(GameObject)PrefabUtility.InstantiatePrefab(oldPrefab);
   float oldH=Exact(probe).size.y;if(oldH<.3f||oldH>1.2f)throw new Exception("OLD_HEIGHT_UNEXPECTED:"+oldH);
   // the 042 placeholder's real front (hand bones, bind pose): stage 1 used yawOffset 0, so it walked facing sideways if != 0
   float old042Face;{var pt=probe.GetComponentsInChildren<Transform>(true);var r42=pt.First(t=>t.name=="Right_Hand").position-pt.First(t=>t.name=="Left_Hand").position;r42.y=0;
    old042Face=Vector3.SignedAngle(Vector3.Cross(r42.normalized,Vector3.up),Vector3.forward,Vector3.up);}
   UnityEngine.Object.DestroyImmediate(probe);
   float old042Fix=Mathf.Abs(Mathf.DeltaAngle(old042Face,Mathf.Round(old042Face/90f)*90f))<15?Mathf.Round(old042Face/90f)*90f:old042Face;
   checks["facing042"]=new{measuredDeg=old042Face,appliedYawOffset=old042Fix,note="s3 actor set copy only; stage-1/s2 actor set unchanged"};
   var b0=Exact(model);float s=oldH/Mathf.Max(.001f,b0.size.y);model.transform.localScale*=s;
   var b1=Exact(model);model.transform.position-=new Vector3(b1.center.x,b1.min.y,b1.center.z);var b2=Exact(model);
   checks["facing"]=new{eyesDeg=fe,handsDeg=fh,appliedYaw=yawFix};
   checks["size"]=new{old042RealHeight=oldH,rawSize=b0.size.ToString("F3"),normalisedSize=b2.size.ToString("F3"),feetY=b2.min.y,note="prefab-space height (x HeroHeight in game)"};
   var anim=model.GetComponent<Animator>();if(!anim)anim=model.AddComponent<Animator>();
   anim.avatar=avatar;anim.runtimeAnimatorController=ctrl;anim.applyRootMotion=false;anim.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
   var rs=model.GetComponentsInChildren<Renderer>(true);foreach(var r in rs){r.shadowCastingMode=ShadowCastingMode.On;r.sharedMaterials=Enumerable.Repeat((Material)mat,r.sharedMaterials.Length).ToArray();}
   var smrs=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
   checks["model"]=new{renderers=rs.Length,skinned=smrs.Length,triangles=smrs.Sum(x=>x.sharedMesh.triangles.Length/3),bones=smrs.Sum(x=>x.bones.Length),texture=ti.maxTextureSize};
   var prefab=PrefabUtility.SaveAsPrefabAsset(root,Art+"/Hero045_bruno.prefab");UnityEngine.Object.DestroyImmediate(root);

   // actor set: copy of the stage-1 set, only bruno's prefab replaced
   var src=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(Dir+"/Art/ActorSet045.asset");if(!src)throw new Exception("ACTOR_SET_MISSING");
   var set=UnityEngine.Object.Instantiate(src);set.name="ActorSet045s3";
   DSMapActorSet.Entry Copy(DSMapActorSet.Entry e,GameObject p)=>new DSMapActorSet.Entry{id=e.id,prefab=p,material=e.material,yawOffset=p==oldPrefab?e.yawOffset+old042Fix:e.yawOffset};
   set.Heroes=src.Heroes.Select(e=>Copy(e,e.id=="bruno"?prefab:e.prefab)).ToArray();set.Monsters=src.Monsters.Select(e=>Copy(e,e.prefab)).ToArray();
   if(set.Heroes.Count(e=>e.id=="bruno"&&e.prefab==prefab)!=1||set.Heroes.Count(e=>e.prefab==prefab)!=1)throw new Exception("BRUNO_ENTRY");
   AssetDatabase.CreateAsset(set,Art+"/ActorSet045s3.asset");
   checks["actorSet"]=set.Heroes.Select(e=>e.id+"="+(e.prefab?e.prefab.name:"null")+"@"+e.yawOffset).ToArray();

   if(!AssetDatabase.CopyAsset(Source,Target))throw new Exception("COPY_FAILED");created.Add(Target);
   var scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   var pres=UnityEngine.Object.FindObjectsByType<DSMapPresentation>(FindObjectsInactive.Include).Single();
   // re-load the set by path: the in-memory object from CreateAsset can be destroyed by a later import (CopyAsset), and a
   // destroyed object is serialised as null while still comparing equal to null (first s3 build saved Actors = null)
   const string SetPath=Art+"/ActorSet045s3.asset";set=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(SetPath);if(!set)throw new Exception("SET_NOT_LOADED");
   var so=new SerializedObject(pres);so.FindProperty("Actors").objectReferenceValue=set;so.ApplyModifiedPropertiesWithoutUndo();
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
   foreach(var guid in AssetDatabase.FindAssets("",new[]{Art}))foreach(var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))if(o)AssetDatabase.SaveAssetIfDirty(o);
   var targetSha=Sha(Target);

   // comparison render (temporary instances, discarded by reopening the saved scene)
   scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   var session=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();var look=session.GetComponent<DSLookGlobals>();if(look)look.Apply();
   {var a=UnityEngine.Object.FindObjectsByType<DSMapPresentation>(FindObjectsInactive.Include).Single().Actors;var guid=AssetDatabase.AssetPathToGUID(SetPath);
    if(ReferenceEquals(a,null)||!a||AssetDatabase.GetAssetPath(a)!=SetPath||string.IsNullOrEmpty(guid)||!File.ReadAllText(Target).Contains("Actors: {fileID: 11400000, guid: "+guid))
     throw new Exception("RELOAD_ACTORS");
    if(a.Heroes.Count(e=>e.prefab&&AssetDatabase.GetAssetPath(e.prefab)==Art+"/Hero045_bruno.prefab")!=1)throw new Exception("RELOAD_BRUNO");
    checks["sceneActors"]=new{path=AssetDatabase.GetAssetPath(a),guid};}
   var old=oldPrefab;var oldCtl=old.GetComponentInChildren<Animator>(true).runtimeAnimatorController;
   var oldClips=oldCtl.animationClips;var oldIdle=oldClips.First(c=>c.name.EndsWith("idle"));var oldRun=oldClips.First(c=>c.name.EndsWith("run"));
   var centre=new Vector3(-22.8f,0,22.4f);var yaws=new[]{180f,135f,90f,0f};   // toward camera, 3/4, side, away
   var inst=new List<GameObject>();AnimationMode.StartAnimationMode();animMode=true;AnimationMode.BeginSampling();
   for(int row=0;row<2;row++)for(int col=0;col<8;col++)
   {
    var g=(GameObject)PrefabUtility.InstantiatePrefab(row==0?old:prefab);inst.Add(g);
    g.transform.position=centre+new Vector3((col-3.5f)*.95f,0,(row==0?.75f:-.75f));g.transform.localScale=Vector3.one*src.HeroHeight;
    g.transform.GetChild(0).localRotation=Quaternion.Euler(0,yaws[col/2]+(row==0?old042Fix:0),0);   // as the game: yaw + entry.yawOffset
    var an=g.GetComponentInChildren<Animator>(true);bool isRun=col%2==1;
    var clip=row==0?(isRun?oldRun:oldIdle):(isRun?run:idle);AnimationMode.SampleAnimationClip(an.gameObject,clip,isRun?clip.length*.25f:clip.length*.3f);
   }
   AnimationMode.EndSampling();
   var go=new GameObject("cap045s3"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.CopyFrom(session.ViewCamera);cam.enabled=false;cam.aspect=1280f/720f;
   cam.orthographic=true;cam.orthographicSize=6;cam.transform.rotation=Quaternion.Euler(55,0,0);cam.transform.position=centre-cam.transform.forward*30;
   var rt=new RenderTexture(1280,720,24);cam.targetTexture=rt;cam.Render();cam.Render();
   var tex=new Texture2D(1280,720,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};var prev=RenderTexture.active;RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();RenderTexture.active=prev;
   File.WriteAllBytes(Out+"/compare-042-045-full.png",tex.EncodeToPNG());
   // crop around the grid (screen rows are bottom-up) and save 1x (game size) and 4x (nearest) versions
   var sp=inst.SelectMany(g=>{var b=Exact(g);return new[]{b.min,b.max,new Vector3(b.min.x,b.max.y,b.min.z),new Vector3(b.max.x,b.min.y,b.max.z)};}).Select(p=>cam.WorldToScreenPoint(p)).ToArray();
   int x0=Mathf.Max(0,(int)sp.Min(p=>p.x)-12),x1=Mathf.Min(1279,(int)sp.Max(p=>p.x)+12),y0=Mathf.Max(0,(int)sp.Min(p=>p.y)-12),y1=Mathf.Min(719,(int)sp.Max(p=>p.y)+12);
   var crop=new Texture2D(x1-x0+1,y1-y0+1,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};crop.SetPixels(tex.GetPixels(x0,y0,crop.width,crop.height));crop.Apply();
   File.WriteAllBytes(Out+"/compare-042-045-1x.png",crop.EncodeToPNG());
   var big=new Texture2D(crop.width*4,crop.height*4,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};var cp=crop.GetPixels();var bp=new Color[big.width*big.height];
   for(int y=0;y<big.height;y++)for(int x=0;x<big.width;x++)bp[y*big.width+x]=cp[(y/4)*crop.width+x/4];big.SetPixels(bp);big.Apply();
   File.WriteAllBytes(Out+"/compare-042-045-4x.png",big.EncodeToPNG());
   // on-screen height of each hero (px, game camera) for the 30 px target
   checks["screenHeightPx"]=inst.Select((g,i)=>{var ys=Verts(g).Select(p=>cam.WorldToScreenPoint(p).y).ToArray();
    return new{row=i<8?"042":"045",col=i%8,px=(float)Math.Round(ys.Max()-ys.Min(),1)};}).ToArray();
   checks["layout"]="rows: top=042 placeholder, bottom=045 bruno; columns: (toward camera, 3/4, side, away) x (idle, run); crop "+x0+","+y0+" "+crop.width+"x"+crop.height;
   cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
   AnimationMode.StopAnimationMode();animMode=false;
   foreach(var g in inst)UnityEngine.Object.DestroyImmediate(g);
   if(look){foreach(var n in new[]{"_DS_ShadowTint","_DS_CloudShadow","_DS_CloudDir","_DS_GradeTint"})Shader.SetGlobalVector(n,Vector4.zero);}
   EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);   // discard the temporary instances
   if(Sha(Target)!=targetSha)throw new Exception("SCENE_CHANGED_BY_RENDER");
   var after=Snapshot();
   if(!after.SequenceEqual(before))throw new Exception("STAGE1_S2_FILES_CHANGED:"+string.Join(",",after.Keys.Except(before.Keys).Concat(before.Keys.Where(k=>!after.TryGetValue(k,out var v)||v!=before[k])).Take(5)));
   checks["protectedFilesUnchanged"]=before.Count;checks["protectedScope"]=new[]{Dir+" (except Art_s3 and the s3 scene)",Char042};
   File.WriteAllText(Out+"/build-s3-fingerprints.json",JsonConvert.SerializeObject(new{scope=checks["protectedScope"],before,after},Formatting.Indented));
   result=new{status="BUILT",target=Target,checks,created=created.ToArray()};
   File.WriteAllText(Out+"/build-s3.json",JsonConvert.SerializeObject(result,Formatting.Indented));
  }
  catch(Exception ex)
  {
   if(animMode)AnimationMode.StopAnimationMode();
   File.WriteAllText(Out+"/build-s3-failure-"+DateTime.Now.ToString("HHmmss")+".json",JsonConvert.SerializeObject(new{error=ex.ToString(),created},Formatting.Indented));
   if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   foreach(var p in created.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(p);
   throw;
  }
  finally{if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);}
  return result;
 }
}
