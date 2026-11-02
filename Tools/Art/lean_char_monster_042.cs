using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

// 042 r005: capture-only variant. Lean the screen-matched 3D heroes 25 degrees BACK (top away from camera; r004 leaned the wrong way)
// toward the camera (a common top-down readability technique) at the game
// pitch 55. The saved scene is reloaded from disk afterwards (not modified).
public static class CharMonster042Lean
{
 const string Out="output/char-monster-042/r001";
 const string ScenePath="Assets/DeadSurvivor/Scenes/Test042_CharMonster_r001/Test042_CharMonster.unity";
 public static object Run()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE:"+EditorSceneManager.GetSceneAt(i).path);
  var file=Out+"/unity/capture-pitch55-leanback25-r005.png";if(File.Exists(file))throw new Exception("OUTPUT_EXISTS");
  var setup=EditorSceneManager.GetSceneManagerSetup();
  try{
  EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
  AnimationMode.StartAnimationMode();
  try{
   var cam=GameObject.Find("GameCamera042").GetComponent<Camera>();
   foreach(var an in UnityEngine.Object.FindObjectsByType<Animator>()){var c=an.runtimeAnimatorController;var clip=c.animationClips[0];bool run=c.name.Contains("Run");AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(an.gameObject,clip,run?clip.length*.25f:clip.length*.3f);AnimationMode.EndSampling();}
   foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>().Where(t=>t.parent==null&&t.name.StartsWith("Hero3D_g2_")))t.rotation=Quaternion.Euler(-25,180,0);
   var rt=new RenderTexture(1280,720,24){antiAliasing=4};var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
   cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());
   cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
  }finally{AnimationMode.StopAnimationMode();}
  EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
  return new{status="CAPTURED",file,lean=-25,pitch=55,sceneSaved=false};
 }
}
