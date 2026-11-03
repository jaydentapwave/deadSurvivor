using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using Newtonsoft.Json;

// 047: (1) FX materials + DSMapFx on Map046_Full, (3) hero attack clips (Tripo slash/shoot) as a humanoid upper-body layer
// on Hero045v2.controller (triggers "Slash", "Shoot"). Rebuildable: replaces Art/Fx047 and Art/Characters/Hero047, and
// removes/re-adds only its own controller layer and parameters.
// Run: unity command script run --project-path <root> -- --file Tools/Map/build_fx_047.cs --entry Fx047Build.Build
public static class Fx047Build
{
 const string FxDir="Assets/DeadSurvivor/Art/Fx047";
 const string HeroDir="Assets/DeadSurvivor/Art/Characters/Hero047";
 const string Scene="Assets/DeadSurvivor/Scenes/Map046/Map046_Full.unity";
 const string HeroFbx="Assets/DeadSurvivor/Art/Characters/Heroes/hero_base/hero045v2.fbx";
 const string Controller="Assets/DeadSurvivor/Art/Characters/Heroes/hero_base/Hero045v2.controller";
 const string AttackSrc="output/keyart-target-045/r003/tripo/hero_base045v2/attack/model.fbx";
 const string Layer="Attack047";

 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++){var s=EditorSceneManager.GetSceneAt(i);if(s.isDirty)throw new Exception("UNSAVED_SCENE:"+s.path);}
  if(!File.Exists(AttackSrc))throw new Exception("MISSING:"+AttackSrc);
  var report=new Dictionary<string,object>();
  foreach(var d in new[]{FxDir,HeroDir})if(AssetDatabase.IsValidFolder(d))AssetDatabase.DeleteAsset(d);
  AssetDatabase.CreateFolder("Assets/DeadSurvivor/Art","Fx047");AssetDatabase.CreateFolder("Assets/DeadSurvivor/Art/Characters","Hero047");

  // --- (1) materials ---
  var unlit=Shader.Find("Universal Render Pipeline/Unlit")??throw new Exception("NO_URP_UNLIT");
  var particles=Shader.Find("Universal Render Pipeline/Particles/Unlit")??throw new Exception("NO_URP_PARTICLES");
  var solid=new Material(unlit){name="Fx047_Solid"};solid.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(solid,FxDir+"/Fx047_Solid.mat");
  var fade=new Material(unlit){name="Fx047_Fade"};Transparent(fade,false);fade.SetColor("_BaseColor",Color.white);fade.SetFloat("_Cull",0);AssetDatabase.CreateAsset(fade,FxDir+"/Fx047_Fade.mat");
  var soft=SoftDot(FxDir+"/Fx047_Dot.png");
  var part=new Material(particles){name="Fx047_Particles"};Transparent(part,true);part.SetTexture("_BaseMap",soft);part.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(part,FxDir+"/Fx047_Particles.mat");
  var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
  var session=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single();
  var fx=session.GetComponent<DSMapFx>()??session.gameObject.AddComponent<DSMapFx>();
  var so=new SerializedObject(fx);so.FindProperty("Solid").objectReferenceValue=solid;so.FindProperty("Fade").objectReferenceValue=fade;so.FindProperty("Particles").objectReferenceValue=part;so.ApplyModifiedPropertiesWithoutUndo();
  var dn=session.GetComponent<DSDayNight>()??session.gameObject.AddComponent<DSDayNight>();var dso=new SerializedObject(dn);
  dso.FindProperty("Sun").objectReferenceValue=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include).FirstOrDefault(l=>l.type==LightType.Directional);
  dso.FindProperty("Look").objectReferenceValue=UnityEngine.Object.FindObjectsByType<DSLookGlobals>(FindObjectsInactive.Include).FirstOrDefault();dso.ApplyModifiedPropertiesWithoutUndo();
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
  scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
  var fx2=UnityEngine.Object.FindObjectsByType<DSMapFx>(FindObjectsInactive.Include).Single();
  if(fx2.Solid!=solid||fx2.Fade!=fade||fx2.Particles!=part)throw new Exception("FX_REFS_NOT_SAVED");   // 046-12 lesson
  report["fx"]=new{solid=solid.name,fade=fade.name,particles=part.name};

  // --- (3) hero attack clips ---
  var heroAvatar=AssetDatabase.LoadAllAssetsAtPath(HeroFbx).OfType<Avatar>().FirstOrDefault()??throw new Exception("NO_HERO_AVATAR");
  var dst=HeroDir+"/hero_attack.fbx";File.Copy(AttackSrc,dst);AssetDatabase.ImportAsset(dst);
  var imp=(ModelImporter)AssetImporter.GetAtPath(dst);
  imp.animationType=ModelImporterAnimationType.Human;imp.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;imp.sourceAvatar=heroAvatar;imp.importAnimation=true;
  imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.importCameras=false;imp.importLights=false;
  // 047-E1: the Tripo presets are long (slash 6.6 s, shoot 9.0 s; shoot drops to a crouch after 1 s). Keep one strike:
  // slash 1.2-2.3 s (wind-up + downward swing, hand speed peak 3.2 at 1.75-2.25 s), shoot 0.15-0.95 s (standing aim/extend).
  var defs=imp.defaultClipAnimations;float fps=AssetDatabase.LoadAllAssetsAtPath(dst).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview")).frameRate;
  foreach(var c in defs){c.loopTime=false;c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;
   if(c.name.ToLowerInvariant().Contains("slash")){c.firstFrame=1.2f*fps;c.lastFrame=2.3f*fps;}else if(c.name.ToLowerInvariant().Contains("shoot")){c.firstFrame=.15f*fps;c.lastFrame=.95f*fps;}}
  imp.clipAnimations=defs;imp.SaveAndReimport();
  var clips=AssetDatabase.LoadAllAssetsAtPath(dst).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
  report["clips"]=clips.Select(c=>new{c.name,length=Math.Round(c.length,3),c.isHumanMotion}).ToArray();
  var slash=clips.SingleOrDefault(c=>c.name.ToLowerInvariant().Contains("slash"))??throw new Exception("NO_SLASH:"+string.Join(",",clips.Select(c=>c.name)));
  var shoot=clips.SingleOrDefault(c=>c.name.ToLowerInvariant().Contains("shoot"))??throw new Exception("NO_SHOOT");
  var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller)??throw new Exception("NO_CONTROLLER");
  // remove a previous 047 layer / parameters, then add again
  for(int i=ctrl.layers.Length-1;i>=0;i--)if(ctrl.layers[i].name==Layer)ctrl.RemoveLayer(i);
  foreach(var n in new[]{"Slash","Shoot"}){var prm=ctrl.parameters.FirstOrDefault(x=>x.name==n);if(prm!=null)ctrl.RemoveParameter(prm);}
  foreach(var o in AssetDatabase.LoadAllAssetsAtPath(Controller).OfType<AvatarMask>().Where(m=>m.name=="UpperBody047").ToArray())AssetDatabase.RemoveObjectFromAsset(o);
  ctrl.AddParameter("Slash",AnimatorControllerParameterType.Trigger);ctrl.AddParameter("Shoot",AnimatorControllerParameterType.Trigger);
  var mask=new AvatarMask{name="UpperBody047"};
  foreach(AvatarMaskBodyPart part2 in Enum.GetValues(typeof(AvatarMaskBodyPart)))if(part2!=AvatarMaskBodyPart.LastBodyPart)
   mask.SetHumanoidBodyPartActive(part2,part2 is AvatarMaskBodyPart.Body or AvatarMaskBodyPart.Head or AvatarMaskBodyPart.LeftArm or AvatarMaskBodyPart.RightArm or AvatarMaskBodyPart.LeftFingers or AvatarMaskBodyPart.RightFingers or AvatarMaskBodyPart.LeftHandIK or AvatarMaskBodyPart.RightHandIK);
  AssetDatabase.AddObjectToAsset(mask,ctrl);
  var sm=new AnimatorStateMachine{name=Layer};AssetDatabase.AddObjectToAsset(sm,ctrl);sm.hideFlags=HideFlags.HideInHierarchy;
  var empty=sm.AddState("Empty");sm.defaultState=empty;
  foreach(var (clip,trig) in new[]{(slash,"Slash"),(shoot,"Shoot")})
  {
   var st=sm.AddState(trig);st.motion=clip;st.speed=trig=="Slash"?2f:1.6f;
   var tin=sm.AddAnyStateTransition(st);tin.AddCondition(AnimatorConditionMode.If,0,trig);tin.hasExitTime=false;tin.duration=.06f;tin.canTransitionToSelf=true;
   var tout=st.AddTransition(empty);tout.hasExitTime=true;tout.exitTime=.9f;tout.duration=.1f;
  }
  var layers=ctrl.layers.ToList();layers.Add(new AnimatorControllerLayer{name=Layer,stateMachine=sm,avatarMask=mask,blendingMode=AnimatorLayerBlendingMode.Override,defaultWeight=1});ctrl.layers=layers.ToArray();
  EditorUtility.SetDirty(ctrl);AssetDatabase.SaveAssetIfDirty(ctrl);
  report["controller"]=new{layers=ctrl.layers.Select(l=>l.name).ToArray(),parameters=ctrl.parameters.Select(p=>p.name).ToArray()};
  // stills of the two attacks at 40% of the clip, game camera angle (temporary instance)
  report["stills"]=Stills(slash,shoot);
  File.WriteAllText("output/map-full-046/fx047-build.json",JsonConvert.SerializeObject(report,Formatting.Indented));
  return report;
 }

 static void Transparent(Material m,bool particles)
 {
  m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);
  m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=(int)RenderQueue.Transparent;m.SetOverrideTag("RenderType","Transparent");
  m.SetShaderPassEnabled("ShadowCaster",false);m.SetShaderPassEnabled("DepthOnly",false);
 }
 // 32x32 soft round dot for particles
 static Texture2D SoftDot(string path)
 {
  int n=32;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);
  for(int y=0;y<n;y++)for(int x=0;x<n;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(n/2f,n/2f))/(n/2f);float a=Mathf.Clamp01(1-d);a=a*a*(3-2*a);t.SetPixel(x,y,new Color(1,1,1,a));}
  File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
  var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.wrapMode=TextureWrapMode.Clamp;ti.SaveAndReimport();
  return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
 }
 static object Stills(AnimationClip slash,AnimationClip shoot)
 {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeadSurvivor/Art/Characters/Heroes/hero_base/Hero045v2.prefab");if(!prefab)return "NO_PREFAB";
  var go=new GameObject("Stills047_Temp");var shots=new List<string>();
  try
  {
   float x=0;foreach(var (clip,t) in new[]{(slash,.2f),(slash,.45f),(slash,.7f),(shoot,.2f),(shoot,.45f),(shoot,.7f)})
   {
    var h=(GameObject)UnityEngine.Object.Instantiate(prefab,go.transform);h.transform.position=new Vector3(500+x,0,500);h.transform.rotation=Quaternion.Euler(0,150,0);x+=1.1f;
    var an=h.GetComponentInChildren<Animator>();AnimationMode.StartAnimationMode();AnimationMode.SampleAnimationClip(an.gameObject,clip,clip.length*t);
   }
   var camGo=new GameObject("Cam");camGo.transform.SetParent(go.transform);var cam=camGo.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=1.1f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.31f,.61f,.2f);
   var rot=Quaternion.Euler(20,180,0);var c=new Vector3(500+(x-1.1f)/2,.6f,500);cam.transform.SetPositionAndRotation(c-(rot*Vector3.forward)*30,rot);
   var rt=new RenderTexture(1600,500,24){antiAliasing=4};cam.targetTexture=rt;cam.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
   var tex=new Texture2D(1600,500,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,500),0,0);tex.Apply();RenderTexture.active=prev;cam.targetTexture=null;
   File.WriteAllBytes("output/map-full-046/hero_attack_stills.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);rt.Release();shots.Add("output/map-full-046/hero_attack_stills.png");
  }
  finally{if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());}
  return shots;
 }
}
