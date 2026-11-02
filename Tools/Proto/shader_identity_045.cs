using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

// 045 1-C check: Map043 rendered with the edited shader vs the pre-045 shader source (temporary asset, deleted after),
// same camera/scene/material values, time-dependent effects off (wind 0, _DS_ globals 0). Also SRP Batcher codes.
public static class ShaderIdentity045
{
 const string Scene043="Assets/DeadSurvivor/Scenes/Map043_LowPolyShader_r001/Map043_LowPolyShader.unity";
 const string ShaderPath="Assets/DeadSurvivor/Art/LowPoly043/Shaders/DSLowPolyPalette.shader";
 const string TmpPath="Assets/DeadSurvivor/Art/LowPoly043/Shaders/_Before045_TMP.shader";
 const string Out="output/keyart-target-045/r001";
 public static object Run()
 {
  if(File.Exists(TmpPath))throw new Exception("TMP_EXISTS");
  var src=File.ReadAllText(Out+"/DSLowPolyPalette.shader.before").Replace("Shader \"DeadSurvivor/LowPolyPalette\"","Shader \"Hidden/DeadSurvivor/LowPolyPalette_Before045\"");
  if(!src.Contains("Hidden/DeadSurvivor/LowPolyPalette_Before045"))throw new Exception("RENAME_FAILED");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE");
  if(EditorApplication.isPlaying)throw new Exception("PLAYING");
  bool asyncBefore=ShaderUtil.allowAsyncCompilation;var setup=EditorSceneManager.GetSceneManagerSetup();var temp=new List<UnityEngine.Object>();RenderTexture rt=null;object result;Dictionary<Renderer,Material[]> original=null;
  try
  {
   asyncBefore=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
   File.WriteAllText(TmpPath,src);AssetDatabase.ImportAsset(TmpPath,ImportAssetOptions.ForceUpdate);
   var before=AssetDatabase.LoadAssetAtPath<Shader>(TmpPath);var now=AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
   if(!before||ShaderUtil.ShaderHasError(before)||ShaderUtil.ShaderHasError(now))throw new Exception("SHADER_ERROR");
   foreach(var n in new[]{"_DS_ShadowTint","_DS_CloudShadow","_DS_CloudDir","_DS_GradeTint"})Shader.SetGlobalVector(n,Vector4.zero);
   var scene=EditorSceneManager.OpenScene(Scene043,OpenSceneMode.Single);   // main editor scene (preview scenes rendered black under URP)
   var view=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First();
   var camGo=new GameObject("Identity045Camera");temp.Add(camGo);var cam=camGo.AddComponent<Camera>();cam.CopyFrom(view);cam.enabled=false;cam.aspect=1280f/720f;   // same as the 043 capture path
   cam.orthographic=true;cam.orthographicSize=6;cam.transform.rotation=Quaternion.Euler(55,0,0);
   cam.transform.position=new Vector3(-22.8f,0,22.4f)-cam.transform.forward*30;
   rt=new RenderTexture(1280,720,24);cam.targetTexture=rt;
   var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Where(r=>r.sharedMaterials.Any(m=>m&&m.shader==now)).ToArray();
   original=renderers.ToDictionary(r=>r,r=>r.sharedMaterials);
   Texture2D Shot(Shader s)
   {
    foreach(var r in renderers)r.sharedMaterials=original[r].Select(m=>{if(!m||m.shader!=now)return m;var c=new Material(m);c.shader=s;c.SetFloat("_WindStrength",0);temp.Add(c);return c;}).ToArray();
    cam.Render();cam.Render();   // first render after a material/shader swap can be blank (shader warm-up); compare the second
    var t=new Texture2D(1280,720,TextureFormat.RGBA32,false);RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();RenderTexture.active=null;temp.Add(t);return t;
   }
   string Probe(string tag){cam.Render();var t=new Texture2D(8,8,TextureFormat.RGB24,false);RenderTexture.active=rt;t.ReadPixels(new Rect(636,356,8,8),0,0);t.Apply();RenderTexture.active=null;var c=t.GetPixel(4,4);UnityEngine.Object.DestroyImmediate(t);return tag+"="+c;}
   var debug=new List<string>{"view="+view.name+" clear="+cam.clearFlags+" bg="+cam.backgroundColor,Probe("noSwap")};
   var a=Shot(now);debug.Add(Probe("afterSwapNow"));var b=Shot(before);debug.Add(Probe("afterSwapBefore"));
   var pa=a.GetPixels32();var pb=b.GetPixels32();int diff=0,maxd=0;
   for(int i=0;i<pa.Length;i++){int d=Math.Abs(pa[i].r-pb[i].r)+Math.Abs(pa[i].g-pb[i].g)+Math.Abs(pa[i].b-pb[i].b);if(d>0)diff++;if(d>maxd)maxd=d;}
   File.WriteAllBytes(Out+"/shader-identity-now.png",a.EncodeToPNG());File.WriteAllBytes(Out+"/shader-identity-before.png",b.EncodeToPNG());
   var nonBlank=pa.Count(p=>p.r+p.g+p.b>30);
   object srp="NOT_RUN";
   try{var mi=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",BindingFlags.NonPublic|BindingFlags.Static);
    if(mi!=null)srp=Enumerable.Range(0,now.subshaderCount).Select(i=>(int)mi.Invoke(null,new object[]{now,i})).ToArray();}catch(Exception e){srp="NOT_RUN: "+e.Message;}
   result=new{debug,status=diff==0&&nonBlank>100000?"PASS":"FAIL",renderers=renderers.Length,pixels=pa.Length,nonBlankPixels=nonBlank,diffPixels=diff,maxDiff=maxd,srpBatcherCodes=srp,
    shaderMessages=ShaderUtil.GetShaderMessages(now).Select(m=>m.severity+":"+m.message).ToArray(),checkedAt=DateTimeOffset.Now.ToString("o")};
   File.WriteAllText(Out+"/shader-identity.json",JsonConvert.SerializeObject(result,Formatting.Indented));
  }
  finally
  {
   foreach(var o in temp)if(o)UnityEngine.Object.DestroyImmediate(o);
   if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
   if(original!=null)foreach(var kv in original)if(kv.Key)kv.Key.sharedMaterials=kv.Value;
   var active=EditorSceneManager.GetActiveScene();if(active.path==Scene043&&active.isDirty)Debug.LogWarning("045 identity: Map043 marked dirty; discarded by restore (not saved)");
   EditorSceneManager.RestoreSceneManagerSetup(setup);
   AssetDatabase.DeleteAsset(TmpPath);ShaderUtil.allowAsyncCompilation=asyncBefore;
  }
  return result;
 }
}
