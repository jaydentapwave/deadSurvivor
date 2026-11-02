using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

// 042 r001 adjust: re-match the "screen-matched" 3D group by measured silhouette
// pixels (the bounds-based first pass over-shrank it) and capture at the game
// pitch 55 plus a 40-degree comparison. Restores the previous scene setup.
public static class CharMonster042Adjust
{
 const string Out="output/char-monster-042/r001";
 const string ScenePath="Assets/DeadSurvivor/Scenes/Test042_CharMonster_r001/Test042_CharMonster.unity";
 const int W=1280,H=720;
 static Camera cam;

 static int[] Rows(GameObject target)
 {
  var all=UnityEngine.Object.FindObjectsByType<Renderer>();var prev=all.ToDictionary(r=>r,r=>r.enabled);
  var bg=cam.backgroundColor;var rt=new RenderTexture(W,H,24);var t=new Texture2D(W,H,TextureFormat.RGB24,false);
  try{
   foreach(var r in all)r.enabled=r.transform.IsChildOf(target.transform);cam.backgroundColor=Color.black;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,W,H),0,0);t.Apply();
   var px=t.GetPixels32();int top=-1,bot=-1;for(int y=0;y<H;y++){bool any=false;for(int x=0;x<W;x++){var c=px[y*W+x];if(c.r+c.g+c.b>24){any=true;break;}}if(any){if(bot<0)bot=y;top=y;}}
   return new[]{bot,top};
  }finally{foreach(var kv in prev)kv.Key.enabled=kv.Value;cam.backgroundColor=bg;cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(t);}
 }
 static float Px(GameObject g){var r=Rows(g);if(r[0]<0)throw new Exception("NOT_VISIBLE:"+g.name);return r[1]-r[0]+1;}
 static void Capture(string file){var rt=new RenderTexture(W,H,24){antiAliasing=4};var t=new Texture2D(W,H,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,W,H),0,0);t.Apply();File.WriteAllBytes(file,t.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(t);}}
 static void Aim(float pitch)
 {
  var oldUp=cam.transform.up;cam.transform.rotation=Quaternion.Euler(pitch,0,0);cam.transform.position=-cam.transform.forward*20f;
  foreach(var sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>()){var s=sr.sprite;float k=sr.transform.localScale.y;var ground=sr.transform.position-Vector3.up*.01f-oldUp*(-s.bounds.min.y*k);sr.transform.rotation=cam.transform.rotation;sr.transform.position=ground+Vector3.up*.01f+cam.transform.up*(-s.bounds.min.y*k);}
 }

 public static object Run(){return RunRev("r003");}
 public static object RunRev(string rev)
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE:"+EditorSceneManager.GetSceneAt(i).path);
  foreach(var f in new[]{"capture-pitch55-","capture-pitch40-","adjust-metrics-"})foreach(var ext in new[]{".png",".json"})if(File.Exists(Out+"/unity/"+f+rev+ext))throw new Exception("OUTPUT_EXISTS:"+f+rev+ext);
  var setup=EditorSceneManager.GetSceneManagerSetup();
  try{
  var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
  object result;
  AnimationMode.StartAnimationMode();
  try{
   cam=GameObject.Find("GameCamera042").GetComponent<Camera>();
   // r003: heroes face the camera (-Z). r002 left them facing away (+Z), showing only the back of the head.
   foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>().Where(t=>t.parent==null&&t.name.StartsWith("Hero3D_")))t.rotation=Quaternion.Euler(0,180,0);
   // Re-apply the sampled poses (AnimationMode poses are not saved in the scene).
   foreach(var an in UnityEngine.Object.FindObjectsByType<Animator>()){var ctl=an.runtimeAnimatorController;var clip=ctl.animationClips[0];bool run=ctl.name.Contains("Run");AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(an.gameObject,clip,run?clip.length*.25f:clip.length*.3f);AnimationMode.EndSampling();}
   float heroTarget=Px(GameObject.Find("Sprite_bruno")),monTarget=Px(GameObject.Find("Sprite_melee_0"));
   float heroLiteral=Px(GameObject.Find("Hero3D_g1_0")),monLiteral=Px(GameObject.Find("Melee3D_g1_0"));
   var h0=GameObject.Find("Hero3D_g2_0");var m0=GameObject.Find("Melee3D_g2_0");
   float hs=h0.transform.localScale.x,ms=m0.transform.localScale.x;
   for(int pass=0;pass<2;pass++){hs*=heroTarget/Px(h0);ms*=monTarget/Px(m0);
    foreach(var go in UnityEngine.Object.FindObjectsByType<Transform>().Where(t=>t.parent==null)){if(go.name.StartsWith("Hero3D_g2_"))go.localScale=Vector3.one*hs;if(go.name.StartsWith("Melee3D_g2_"))go.localScale=Vector3.one*ms;}}
   float heroMatched=Px(h0),monMatched=Px(m0);
   Capture(Out+"/unity/capture-pitch55-"+rev+".png");
   Aim(40);float hero40=Px(GameObject.Find("Hero3D_g2_0")),sprite40=Px(GameObject.Find("Sprite_bruno"));Capture(Out+"/unity/capture-pitch40-"+rev+".png");
   Aim(55);
   result=new{status="ADJUSTED",method="silhouette pixel rows, 1280x720, isolated render",heroSpritePx=heroTarget,monsterSpritePx=monTarget,heroLiteralPx=heroLiteral,monsterLiteralPx=monLiteral,heroMatchedPx=heroMatched,monsterMatchedPx=monMatched,heroMatchedScale=hs,monsterMatchedScale=ms,heroMatchedWorldHeight=.72f*hs,monsterMatchedWorldHeight=.72f*ms,pitch40=new{heroMatchedPx=hero40,heroSpritePx=sprite40},captures=new[]{Out+"/unity/capture-pitch55-"+rev+".png",Out+"/unity/capture-pitch40-"+rev+".png"}};
   File.WriteAllText(Out+"/unity/adjust-metrics-"+rev+".json",JsonConvert.SerializeObject(result,Formatting.Indented));
  }finally{AnimationMode.StopAnimationMode();}
  EditorSceneManager.SaveScene(scene);
  return result;
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
}
