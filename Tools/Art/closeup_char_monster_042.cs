using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

// 042 r006/r007: capture-only (r007 adds the 042 test rim/outline shader). For orthographic sizes 6 / 4.5 / 4 at pitch 55, center the camera on
// the 2D sprite group and on the screen-matched 3D group, with the 3D models in (a) the current
// URP Lit material and (b) a rim material using the 043 shader (read-only use; new material in
// the 042 folder). The scene is reloaded from disk (not saved) and the previous setup restored.
public static class CharMonster042Closeup
{
 const string Out="output/char-monster-042/r001/unity/r007";
 const string Art="Assets/DeadSurvivor/Art/Characters/Test042_r001";
 const string ScenePath="Assets/DeadSurvivor/Scenes/Test042_CharMonster_r001/Test042_CharMonster.unity";
 // Filled from the 043 session's answer. Empty RimShader => rim variant NOT_RUN.
 const string RimShader="DeadSurvivor/Test042/RimOutline";
 const string ShaderSrc="Tools/Art/rim_outline_042.shader";
 static readonly (string name,float value)[] RimFloats={("_RimStrength",.5f),("_RimPower",3f),("_OutlineWidth",.025f)};
 static readonly (string name,Color value)[] RimColors={("_RimColor",new Color(1f,.95f,.82f)),("_OutlineColor",new Color(.12f,.09f,.07f))};
 const string RimTexture="_BaseMap";
 static readonly float[] Sizes={6f,4.5f,4f};
 const int W=1280,H=720;

 static void Shot(Camera cam,string file){var rt=new RenderTexture(W,H,24){antiAliasing=4};var t=new Texture2D(W,H,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,W,H),0,0);t.Apply();File.WriteAllBytes(file,t.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(t);}}
 static Material RimMat(string name,Material src)
 {
  var path=Art+"/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
  var sh=AssetDatabase.LoadAssetAtPath<Shader>(Art+"/RimOutline042.shader")??Shader.Find(RimShader);if(!sh)throw new Exception("RIM_SHADER_NOT_FOUND:"+RimShader);
  var m=new Material(sh){name=name};if(m.HasProperty(RimTexture))m.SetTexture(RimTexture,src.GetTexture("_BaseMap"));
  foreach(var f in RimFloats)if(m.HasProperty(f.name))m.SetFloat(f.name,f.value);foreach(var c in RimColors)if(m.HasProperty(c.name))m.SetColor(c.name,c.value);
  AssetDatabase.CreateAsset(m,path);AssetDatabase.SaveAssetIfDirty(m);return m;
 }
 public static object Run()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE:"+EditorSceneManager.GetSceneAt(i).path);
  if(Directory.Exists(Out))throw new Exception("OUTPUT_EXISTS:"+Out);
  Directory.CreateDirectory(Out);
  // r007: install the test shader into the 042 test folder only (outside 043 assets) and fail on compile errors.
  var shaderAsset=Art+"/RimOutline042.shader";
  if(!File.Exists(shaderAsset)){File.Copy(ShaderSrc,shaderAsset);AssetDatabase.ImportAsset(shaderAsset,ImportAssetOptions.ForceSynchronousImport);}
  var sh=AssetDatabase.LoadAssetAtPath<Shader>(shaderAsset);if(!sh||ShaderUtil.ShaderHasError(sh))throw new Exception("RIM_SHADER_COMPILE:"+string.Join(" | ",ShaderUtil.GetShaderMessages(sh).Select(m=>m.message)));
  var setup=EditorSceneManager.GetSceneManagerSetup();var files=new System.Collections.Generic.List<string>();bool rim=RimShader.Length>0;
  try{
   EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
   AnimationMode.StartAnimationMode();
   try{
    var cam=GameObject.Find("GameCamera042").GetComponent<Camera>();
    foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>().Where(t=>t.parent==null&&t.name.StartsWith("Hero3D_")))t.rotation=Quaternion.Euler(0,180,0);
    foreach(var an in UnityEngine.Object.FindObjectsByType<Animator>()){var c=an.runtimeAnimatorController;var clip=c.animationClips[0];bool run=c.name.Contains("Run");AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(an.gameObject,clip,run?clip.length*.25f:clip.length*.3f);AnimationMode.EndSampling();}
    var heroR=UnityEngine.Object.FindObjectsByType<Renderer>().Where(r=>r.transform.root.name.StartsWith("Hero3D_")).ToArray();
    var monR=UnityEngine.Object.FindObjectsByType<Renderer>().Where(r=>r.transform.root.name.StartsWith("Melee3D_")).ToArray();
    var heroLit=heroR[0].sharedMaterial;var monLit=monR[0].sharedMaterial;
    var variants=rim?new[]{"lit","rim"}:new[]{"lit"};
    foreach(var v in variants){
     if(v=="rim"){var hm=RimMat("Hero042_Rim043",heroLit);var mm=RimMat("Monster042_Rim043",monLit);foreach(var r in heroR)r.sharedMaterials=Enumerable.Repeat(hm,r.sharedMaterials.Length).ToArray();foreach(var r in monR)r.sharedMaterials=Enumerable.Repeat(mm,r.sharedMaterials.Length).ToArray();}
     foreach(var size in Sizes)foreach(var g in new[]{("sprite2d",-6.5f),("matched3d",6.5f)}){
      if(g.Item1=="sprite2d"&&v=="rim")continue;
      cam.orthographicSize=size;cam.transform.rotation=Quaternion.Euler(55,0,0);cam.transform.position=new Vector3(g.Item2,0,0)-cam.transform.forward*20f;
      var f=$"{Out}/{g.Item1}-{v}-ortho{size:0.0}.png";Shot(cam,f);files.Add(f);
     }
    }
   }finally{AnimationMode.StopAnimationMode();}
   EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
  var result=new{status="CAPTURED",rim=rim?"RUN":"NOT_RUN (043 shader not provided)",rimShader=RimShader,sizes=Sizes,pitch=55,sceneSaved=false,files};
  File.WriteAllText(Out+"/closeup-metrics.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
