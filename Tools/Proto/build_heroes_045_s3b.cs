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

// 045 stage 3 v2: all six heroes drawn by the one v2 base hero (Tripo multiview, facing-fixed rig) with per-hero recoloured
// textures. Owns Scenes/Proto045/Art_s3b and the new scene Proto045_Main_s3b (copy of s2); everything else is fingerprinted
// before/after (Proto045 incl. s1/s2/s3 files, 042 originals). One prefab + six materials through DSMapActorSet.Entry.material.
// Run clip: a copy whose spine/chest/upper-chest front-back and neck/head nod muscle curves and the body (RootQ) forward tilt
// are scaled by k (0.4, 0.3, ... 0)
// until the maximum lean over 30 samples of the loop is <= 20 deg (design D3). Refuses if the outputs exist; on failure
// deletes only what it created.
public static class Heroes045BuildS3b
{
 const string Dir="Assets/DeadSurvivor/Scenes/Proto045";
 const string Art=Dir+"/Art_s3b";
 const string Source=Dir+"/Proto045_Main_s2.unity";
 const string Target=Dir+"/Proto045_Main_s3b.unity";
 const string In="output/keyart-target-045/r003/tripo/hero_base045v2";
 const string Out="output/keyart-target-045/r003/unity-v2";
 const string Char042="Assets/DeadSurvivor/Art/Characters/Test042_r001";
 static readonly string[] Ids={"bruno","hans","lena","yuki","bolt","mira"};
 static readonly string[] LeanMuscles={"Spine Front-Back","Chest Front-Back","UpperChest Front-Back","Neck Nod Down-Up","Head Nod Down-Up"};
 static string Sha(string p){using var s=SHA256.Create();return string.Concat(s.ComputeHash(File.ReadAllBytes(p)).Select(x=>x.ToString("x2")));}
 static Dictionary<string,string> Snapshot()=>Directory.GetFiles(Dir,"*",SearchOption.AllDirectories).Where(f=>!f.Contains("/Art_s3b")&&!f.StartsWith(Target))
  .Concat(Directory.GetFiles(Char042,"*",SearchOption.AllDirectories)).OrderBy(f=>f).ToDictionary(f=>f,Sha);
 static List<Vector3> Verts(GameObject g)
 {
  var pts=new List<Vector3>();
  foreach(var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)){var m=new Mesh();smr.BakeMesh(m,true);var M=smr.transform.localToWorldMatrix;pts.AddRange(m.vertices.Select(v=>M.MultiplyPoint3x4(v)));UnityEngine.Object.DestroyImmediate(m);}
  if(pts.Count==0)throw new Exception("NO_VERTICES:"+g.name);return pts;
 }
 static Bounds Exact(GameObject g){var p=Verts(g);var b=new Bounds(p[0],Vector3.zero);foreach(var x in p)b.Encapsulate(x);return b;}

 // lean (deg) = angle of Hips->Head in the plane of the model's forward/up, over n samples of the clip
 static float[] Lean(GameObject inst,AnimationClip clip,int n)
 {
  var an=inst.GetComponentInChildren<Animator>(true);var res=new float[n];
  if(!an||!an.avatar||!an.avatar.isHuman)throw new Exception("LEAN_NEEDS_HUMANOID_INSTANCE");
  for(int i=0;i<n;i++)
  {
   AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(an.gameObject,clip,clip.length*i/n);AnimationMode.EndSampling();
   var v=an.GetBoneTransform(HumanBodyBones.Head).position-an.GetBoneTransform(HumanBodyBones.Hips).position;
   var rh=an.GetBoneTransform(HumanBodyBones.RightUpperLeg).position-an.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;rh.y=0;
   var fwd=Vector3.Cross(rh.normalized,Vector3.up);   // body front of this pose (independent of how the object is rotated)
   res[i]=Mathf.Atan2(Vector3.Dot(v,fwd),Vector3.Dot(v,Vector3.up))*Mathf.Rad2Deg;
  }
  return res;
 }
 static AnimationClip ScaledCopy(AnimationClip src,float k,string path)
 {
  var c=new AnimationClip{name=Path.GetFileNameWithoutExtension(path)};
  foreach(var b in AnimationUtility.GetCurveBindings(src))
  {
   var cur=AnimationUtility.GetEditorCurve(src,b);
   if(LeanMuscles.Contains(b.propertyName)){var keys=cur.keys;for(int i=0;i<keys.Length;i++){keys[i].value*=k;keys[i].inTangent*=k;keys[i].outTangent*=k;}cur.keys=keys;}
   AnimationUtility.SetEditorCurve(c,b,cur);
  }
  // most of the lean is the body tilt (RootQ), not the spine muscles (measured: muscles only 34 -> 30 deg at k=0.4):
  // rotate RootQ about the humanoid right axis so its forward tilt becomes k * tilt, key by key
  var rq=new[]{"x","y","z","w"}.Select(a=>AnimationUtility.GetCurveBindings(src).FirstOrDefault(b=>b.propertyName=="RootQ."+a)).ToArray();
  if(rq.All(b=>b.propertyName!=null))
  {
   var cv=rq.Select(b=>AnimationUtility.GetEditorCurve(src,b)).ToArray();
   var times=cv.SelectMany(x=>x.keys.Select(k2=>k2.time)).Distinct().OrderBy(t=>t).ToArray();var nk=new List<Keyframe>[4];for(int i=0;i<4;i++)nk[i]=new List<Keyframe>();
   foreach(var t in times)
   {
    // direction-agnostic: the rotation taking the body's up back to world up, applied by (1-k) (the first version assumed
    // forward = +Z and rotated about X, which barely changed this clip)
    var q=new Quaternion(cv[0].Evaluate(t),cv[1].Evaluate(t),cv[2].Evaluate(t),cv[3].Evaluate(t)).normalized;
    var q2=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(q*Vector3.up,Vector3.up),1-k)*q;
    nk[0].Add(new Keyframe(t,q2.x));nk[1].Add(new Keyframe(t,q2.y));nk[2].Add(new Keyframe(t,q2.z));nk[3].Add(new Keyframe(t,q2.w));
   }
   for(int i=0;i<4;i++){var curve=new AnimationCurve(nk[i].ToArray());for(int j=0;j<curve.length;j++){AnimationUtility.SetKeyLeftTangentMode(curve,j,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,j,AnimationUtility.TangentMode.ClampedAuto);}
    AnimationUtility.SetEditorCurve(c,rq[i],curve);}
  }
  AnimationUtility.SetAnimationClipSettings(c,AnimationUtility.GetAnimationClipSettings(src));
  AssetDatabase.CreateAsset(c,path);return c;
 }

 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE");
  if(AssetDatabase.IsValidFolder(Art)||Directory.Exists(Art)||File.Exists(Target))throw new Exception("DESTINATION_EXISTS");
  if(Directory.Exists(Out))throw new Exception("OUTPUT_EXISTS");
  var fixedFbx=In+"/fixed/model.fbx";
  if(!File.Exists(fixedFbx)||Ids.Any(id=>!File.Exists(In+"/recolor/"+id+".png"))||!File.Exists(Source))throw new Exception("INPUT_MISSING");
  var rc=JsonConvert.DeserializeObject<Dictionary<string,object>>(File.ReadAllText(In+"/recolor/recolor.json"));if((string)rc["status"]!="PASS")throw new Exception("RECOLOR_NOT_PASS");
  var fx=JsonConvert.DeserializeObject<Dictionary<string,object>>(File.ReadAllText(In+"/fixed/fix-report.json"));if((string)fx["status"]!="PASS")throw new Exception("FACING_FIX_NOT_PASS");
  var before=Snapshot();
  var setup=EditorSceneManager.GetSceneManagerSetup();var created=new List<string>();var checks=new Dictionary<string,object>();object result;bool animMode=false;
  Directory.CreateDirectory(Out);
  try
  {
   AssetDatabase.CreateFolder(Dir,"Art_s3b");created.Add(Art);
   File.Copy(fixedFbx,Art+"/hero045v2.fbx");
   foreach(var id in Ids)File.Copy(In+"/recolor/"+id+".png",Art+"/hero045v2_"+id+".png");
   checks["inputs"]=new{fbx=Sha(fixedFbx),textures=Ids.ToDictionary(id=>id,id=>Sha(In+"/recolor/"+id+".png"))};
   foreach(var id in Ids){var p=Art+"/hero045v2_"+id+".png";AssetDatabase.ImportAsset(p);var ti=(TextureImporter)AssetImporter.GetAtPath(p);ti.maxTextureSize=512;ti.SaveAndReimport();}
   AssetDatabase.ImportAsset(Art+"/hero045v2.fbx");
   var imp=(ModelImporter)AssetImporter.GetAtPath(Art+"/hero045v2.fbx");
   imp.animationType=ModelImporterAnimationType.Human;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.importAnimation=true;
   imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.importCameras=false;imp.importLights=false;
   var defs=imp.defaultClipAnimations;foreach(var c in defs){c.loopTime=true;c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;}imp.clipAnimations=defs;imp.SaveAndReimport();
   var fbx=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/hero045v2.fbx");var all=AssetDatabase.LoadAllAssetsAtPath(Art+"/hero045v2.fbx");
   var avatar=all.OfType<Avatar>().FirstOrDefault();if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("AVATAR_INVALID");
   var clips=all.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
   var idle=clips.Single(c=>c.name.ToLowerInvariant().EndsWith("idle"));var run=clips.Single(c=>c.name.ToLowerInvariant().EndsWith("run"));
   checks["clips"]=clips.Select(c=>new{c.name,c.length,c.isLooping,c.isHumanMotion}).ToArray();

   // prefab (model faced to +Z by eye/hand bones, normalised to the 042 real mesh height = same on-screen size as before)
   var root=new GameObject("Hero045v2");var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
   var model=(GameObject)PrefabUtility.InstantiatePrefab(fbx);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.transform.SetParent(visual.transform,false);
   Transform Bone(string n)=>model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==n)??throw new Exception("BONE_MISSING:"+n);
   float Facing(string l,string r){var right=Bone(r).position-Bone(l).position;right.y=0;return Vector3.SignedAngle(Vector3.Cross(right.normalized,Vector3.up),Vector3.forward,Vector3.up);}
   float fe=Facing("Left_Eye","Right_Eye"),fh=Facing("Left_Hand","Right_Hand");
   if(Mathf.Abs(Mathf.DeltaAngle(fe,fh))>20)throw new Exception("FACING_AMBIGUOUS:"+fe+"/"+fh);
   // the model object is NOT rotated: humanoid playback turns the body to the Animator object's +Z in Play (measured: a
   // -90 rotation here made the heroes face 68 deg off their movement, facing_check_045). The bind-pose facing (fe) is kept
   // only to orient the edit-mode renders, where sampling keeps the bind orientation.
   float yawFix=Mathf.Abs(Mathf.DeltaAngle(fe,Mathf.Round(fe/90f)*90f))<15?Mathf.Round(fe/90f)*90f:fe;
   var old042=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Art/Hero045.prefab");var probe=(GameObject)PrefabUtility.InstantiatePrefab(old042);
   float oldH=Exact(probe).size.y;UnityEngine.Object.DestroyImmediate(probe);if(oldH<.3f||oldH>1.2f)throw new Exception("OLD_HEIGHT_UNEXPECTED:"+oldH);
   var b0=Exact(model);float s=oldH/Mathf.Max(.001f,b0.size.y);model.transform.localScale*=s;
   var b1=Exact(model);model.transform.position-=new Vector3(b1.center.x,b1.min.y,b1.center.z);var b2=Exact(model);
   checks["facing"]=new{eyesDeg=fe,handsDeg=fh,modelRotation=0,renderOnlyYaw=yawFix,note="Play facing is checked by the runner (body vs movement)"};
   checks["size"]=new{old042RealHeight=oldH,rawSize=b0.size.ToString("F3"),normalisedSize=b2.size.ToString("F3")};

   // the prefab is saved first; run lean is measured on an instance of the SAVED prefab (an in-memory clone gave ~5 deg
   // for a clip that leans 34 deg on the saved prefab: miss S3-15)
   {var an0=model.GetComponent<Animator>();if(!an0)an0=model.AddComponent<Animator>();an0.avatar=avatar;an0.applyRootMotion=false;}
   PrefabUtility.SaveAsPrefabAsset(root,Art+"/Hero045v2.prefab");
   var savedPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Hero045v2.prefab");
   var tmp=(GameObject)PrefabUtility.InstantiatePrefab(savedPrefab);AnimationMode.StartAnimationMode();animMode=true;
   var baseLean=Lean(tmp,run,30);var leanLog=new List<object>{new{k=1f,max=baseLean.Max(),min=baseLean.Min(),mean=baseLean.Average()}};
   if(baseLean.Max()<15f)throw new Exception("LEAN_BASELINE_IMPLAUSIBLE:"+baseLean.Max());   // v1 and Blender both ~35 deg
   // impl review v2 r001 #3: the v1 hero measured with the same function and conditions (saved prefab instance, 30 samples)
   object v1Base;{
    const string v1Prefab=Dir+"/Art_s3/Hero045_bruno.prefab",v1Fbx=Dir+"/Art_s3/hero_bruno045.fbx";
    if(!File.Exists(v1Prefab)||!File.Exists(v1Fbx))throw new Exception("V1_BASELINE_MISSING");
    var v1run=AssetDatabase.LoadAllAssetsAtPath(v1Fbx).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview")&&c.name.ToLowerInvariant().EndsWith("run"));
    var v1i=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(v1Prefab));var l1=Lean(v1i,v1run,30);UnityEngine.Object.DestroyImmediate(v1i);
    v1Base=new{max=l1.Max(),min=l1.Min(),mean=l1.Average(),prefab=v1Prefab,prefabSha=Sha(v1Prefab),fbxSha=Sha(v1Fbx),clip=v1run.name};}
   AnimationClip runUsed=null;float kUsed=-1;
   foreach(var k in new[]{.4f,.3f,.2f,.1f,0f})
   {
    var path=Art+"/run_lean_k"+Mathf.RoundToInt(k*10)+".anim";var c=ScaledCopy(run,k,path);var l=Lean(tmp,c,30);
    leanLog.Add(new{k,max=l.Max(),min=l.Min(),mean=l.Average(),clip=path});
    if(l.Max()<=20f){runUsed=c;kUsed=k;break;}
   }
   AnimationMode.StopAnimationMode();animMode=false;UnityEngine.Object.DestroyImmediate(tmp);
   checks["runLean"]=new{samples=30,limitDeg=20,log=leanLog,kUsed,v1Baseline=v1Base};
   if(!runUsed)throw new Exception("RUN_LEAN_NOT_REDUCED:"+JsonConvert.SerializeObject(leanLog));
   foreach(var p in AssetDatabase.FindAssets("t:AnimationClip",new[]{Art}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".anim")&&p!=AssetDatabase.GetAssetPath(runUsed)).ToArray())AssetDatabase.DeleteAsset(p);

   var ctrl=AnimatorController.CreateAnimatorControllerAtPath(Art+"/Hero045v2.controller");
   ctrl.AddParameter("Run",AnimatorControllerParameterType.Bool);var sm=ctrl.layers[0].stateMachine;
   var si=sm.AddState("Idle");si.motion=idle;var sr=sm.AddState("Run");sr.motion=runUsed;sm.defaultState=si;
   var t1=si.AddTransition(sr);t1.AddCondition(AnimatorConditionMode.If,0,"Run");t1.hasExitTime=false;t1.duration=.08f;
   var t2=sr.AddTransition(si);t2.AddCondition(AnimatorConditionMode.IfNot,0,"Run");t2.hasExitTime=false;t2.duration=.12f;
   var anim=model.GetComponent<Animator>();if(!anim)anim=model.AddComponent<Animator>();
   anim.avatar=avatar;anim.runtimeAnimatorController=ctrl;anim.applyRootMotion=false;anim.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
   var baseMat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Hero045v2_bruno"};
   var mats=new Dictionary<string,Material>();
   foreach(var id in Ids)
   {
    var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Hero045v2_"+id};
    m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/hero045v2_"+id+".png"));m.SetColor("_BaseColor",Color.white);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.12f);
    AssetDatabase.CreateAsset(m,Art+"/Hero045v2_"+id+".mat");mats[id]=m;
   }
   var rs=model.GetComponentsInChildren<Renderer>(true);foreach(var r in rs){r.shadowCastingMode=ShadowCastingMode.On;r.sharedMaterials=Enumerable.Repeat(mats["bruno"],r.sharedMaterials.Length).ToArray();}
   var smrs=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
   checks["model"]=new{renderers=rs.Length,skinned=smrs.Length,triangles=smrs.Sum(x=>x.sharedMesh.triangles.Length/3),bones=smrs.Sum(x=>x.bones.Length)};
   PrefabUtility.SaveAsPrefabAsset(root,Art+"/Hero045v2.prefab");UnityEngine.Object.DestroyImmediate(root);
   UnityEngine.Object.DestroyImmediate(baseMat);

   // actor set: copy of the stage-1 set; every hero -> v2 prefab + its own material; monsters unchanged
   foreach(var guid in AssetDatabase.FindAssets("",new[]{Art}))foreach(var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))if(o)AssetDatabase.SaveAssetIfDirty(o);
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Hero045v2.prefab");
   var src=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(Dir+"/Art/ActorSet045.asset");if(!src)throw new Exception("ACTOR_SET_MISSING");
   var set=UnityEngine.Object.Instantiate(src);set.name="ActorSet045s3b";
   if(src.Heroes.Select(e=>e.id).OrderBy(x=>x).SequenceEqual(Ids.OrderBy(x=>x))==false)throw new Exception("HERO_IDS_DIFFER");
   set.Heroes=src.Heroes.Select(e=>new DSMapActorSet.Entry{id=e.id,prefab=prefab,material=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Hero045v2_"+e.id+".mat"),yawOffset=0}).ToArray();
   set.Monsters=src.Monsters.Select(e=>new DSMapActorSet.Entry{id=e.id,prefab=e.prefab,material=e.material,yawOffset=e.yawOffset}).ToArray();
   AssetDatabase.CreateAsset(set,Art+"/ActorSet045s3b.asset");

   if(!AssetDatabase.CopyAsset(Source,Target))throw new Exception("COPY_FAILED");created.Add(Target);
   var scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   const string SetPath=Art+"/ActorSet045s3b.asset";var setAsset=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(SetPath);if(!setAsset)throw new Exception("SET_NOT_LOADED");
   var pres=UnityEngine.Object.FindObjectsByType<DSMapPresentation>(FindObjectsInactive.Include).Single();
   var so=new SerializedObject(pres);so.FindProperty("Actors").objectReferenceValue=setAsset;so.ApplyModifiedPropertiesWithoutUndo();
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
   foreach(var guid in AssetDatabase.FindAssets("",new[]{Art}))foreach(var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))if(o)AssetDatabase.SaveAssetIfDirty(o);

   // reload and verify references by path / GUID / file contents (design D4)
   scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   var a=UnityEngine.Object.FindObjectsByType<DSMapPresentation>(FindObjectsInactive.Include).Single().Actors;var setGuid=AssetDatabase.AssetPathToGUID(SetPath);
   if(ReferenceEquals(a,null)||!a||AssetDatabase.GetAssetPath(a)!=SetPath||!File.ReadAllText(Target).Contains("Actors: {fileID: 11400000, guid: "+setGuid))throw new Exception("RELOAD_ACTORS");
   var refs=new Dictionary<string,object>();
   foreach(var e in a.Heroes)
   {
    var pp=e.prefab?AssetDatabase.GetAssetPath(e.prefab):"null";var mp=e.material?AssetDatabase.GetAssetPath(e.material):"null";
    var tp=e.material&&e.material.GetTexture("_BaseMap")?AssetDatabase.GetAssetPath(e.material.GetTexture("_BaseMap")):"null";
    bool ok=pp==Art+"/Hero045v2.prefab"&&mp==Art+"/Hero045v2_"+e.id+".mat"&&tp==Art+"/hero045v2_"+e.id+".png"&&e.yawOffset==0;
    refs[e.id]=new{prefab=pp,material=mp,texture=tp,ok};if(!ok)throw new Exception("RELOAD_HERO_REF:"+e.id);
   }
   var ctl2=AssetDatabase.LoadAssetAtPath<AnimatorController>(Art+"/Hero045v2.controller");
   var runState=ctl2.layers[0].stateMachine.states.Single(x=>x.state.name=="Run").state;
   if(AssetDatabase.GetAssetPath(runState.motion)!=AssetDatabase.GetAssetPath(runUsed))throw new Exception("RELOAD_RUN_CLIP");
   checks["sceneRefs"]=new{set=SetPath,setGuid,heroes=refs,runClip=AssetDatabase.GetAssetPath(runState.motion)};

   // comparison render: 6 rows (heroes) x 4 facings x (idle, run) at the game camera; temporary instances discarded
   var targetSha=Sha(Target);
   var session=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();var look=session.GetComponent<DSLookGlobals>();if(look)look.Apply();
   var centre=new Vector3(-22.8f,0,22.4f);var yaws=new[]{180f,135f,90f,0f};var inst=new List<GameObject>();
   AnimationMode.StartAnimationMode();animMode=true;AnimationMode.BeginSampling();
   for(int row=0;row<Ids.Length;row++)for(int col=0;col<8;col++)
   {
    var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);inst.Add(g);
    g.transform.position=centre+new Vector3((col-3.5f)*.95f,0,(2.5f-row)*1.0f);g.transform.localScale=Vector3.one*src.HeroHeight;
    g.transform.GetChild(0).localRotation=Quaternion.Euler(0,yaws[col/2]+yawFix,0);   // render-only: edit sampling keeps the bind facing
    foreach(var r in g.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=Enumerable.Repeat(mats[Ids[row]],r.sharedMaterials.Length).ToArray();
    var an=g.GetComponentInChildren<Animator>(true);var clip=col%2==1?runUsed:idle;AnimationMode.SampleAnimationClip(an.gameObject,clip,col%2==1?clip.length*.25f:clip.length*.3f);
   }
   AnimationMode.EndSampling();
   var go=new GameObject("cap045s3b"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.CopyFrom(session.ViewCamera);cam.enabled=false;cam.aspect=1280f/720f;
   cam.orthographic=true;cam.orthographicSize=6;cam.transform.rotation=Quaternion.Euler(55,0,0);cam.transform.position=centre-cam.transform.forward*30;
   var rt=new RenderTexture(1280,720,24);cam.targetTexture=rt;cam.Render();cam.Render();
   var tex=new Texture2D(1280,720,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};var prev=RenderTexture.active;RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();RenderTexture.active=prev;
   File.WriteAllBytes(Out+"/heroes-v2-full.png",tex.EncodeToPNG());
   var sp=inst.SelectMany(g=>Verts(g)).Select(p=>cam.WorldToScreenPoint(p)).ToArray();
   int x0=Mathf.Max(0,(int)sp.Min(p=>p.x)-12),x1=Mathf.Min(1279,(int)sp.Max(p=>p.x)+12),y0=Mathf.Max(0,(int)sp.Min(p=>p.y)-12),y1=Mathf.Min(719,(int)sp.Max(p=>p.y)+12);
   var crop=new Texture2D(x1-x0+1,y1-y0+1,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};crop.SetPixels(tex.GetPixels(x0,y0,crop.width,crop.height));crop.Apply();
   File.WriteAllBytes(Out+"/heroes-v2-1x.png",crop.EncodeToPNG());
   var big=new Texture2D(crop.width*3,crop.height*3,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};var cp=crop.GetPixels();var bp=new Color[big.width*big.height];
   for(int y=0;y<big.height;y++)for(int x=0;x<big.width;x++)bp[y*big.width+x]=cp[(y/3)*crop.width+x/3];big.SetPixels(bp);big.Apply();
   File.WriteAllBytes(Out+"/heroes-v2-3x.png",big.EncodeToPNG());
   checks["screenHeightPx"]=inst.Select((g,i)=>{var ys=Verts(g).Select(p=>cam.WorldToScreenPoint(p).y).ToArray();return new{hero=Ids[i/8],col=i%8,px=(float)Math.Round(ys.Max()-ys.Min(),1)};}).ToArray();
   checks["layout"]="rows (top->bottom): "+string.Join(",",Ids)+"; columns: (toward camera, 3/4, side, away) x (idle, run k="+kUsed+")";
   cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
   AnimationMode.StopAnimationMode();animMode=false;foreach(var g in inst)UnityEngine.Object.DestroyImmediate(g);
   if(look){foreach(var n in new[]{"_DS_ShadowTint","_DS_CloudShadow","_DS_CloudDir","_DS_GradeTint"})Shader.SetGlobalVector(n,Vector4.zero);}
   EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   if(Sha(Target)!=targetSha)throw new Exception("SCENE_CHANGED_BY_RENDER");
   var after=Snapshot();
   if(!after.SequenceEqual(before))throw new Exception("PROTECTED_FILES_CHANGED:"+string.Join(",",after.Keys.Except(before.Keys).Concat(before.Keys.Where(k=>!after.TryGetValue(k,out var v)||v!=before[k])).Take(5)));
   var newFiles=Directory.GetFiles(Art,"*",SearchOption.AllDirectories).Concat(new[]{Target,Target+".meta"}).OrderBy(f=>f).ToDictionary(f=>f,Sha);
   checks["protectedFilesUnchanged"]=before.Count;checks["newFiles"]=newFiles.Count;
   File.WriteAllText(Out+"/build-s3b-fingerprints.json",JsonConvert.SerializeObject(new{protectedScope=new[]{Dir+" (except Art_s3b and the s3b scene)",Char042},before,after,newFiles},Formatting.Indented));
   result=new{status="BUILT",target=Target,checks,created=created.ToArray()};
   File.WriteAllText(Out+"/build-s3b.json",JsonConvert.SerializeObject(result,Formatting.Indented));
  }
  catch(Exception ex)
  {
   if(animMode)AnimationMode.StopAnimationMode();
   File.WriteAllText(Out+"/build-s3b-failure-"+DateTime.Now.ToString("HHmmss")+".json",JsonConvert.SerializeObject(new{error=ex.ToString(),created},Formatting.Indented));
   if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   foreach(var p in created.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(p);
   throw;
  }
  finally{if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);}
  return result;
 }
}
