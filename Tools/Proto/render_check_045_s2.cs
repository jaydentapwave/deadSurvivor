using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using Newtonsoft.Json;

// 045 stage 2 (edit mode, works in a batch-mode editor): render the game-camera start view of the stage-1 and stage-2
// scenes (same camera as play: ortho 6, 55 deg, distance 30, look globals on) and check the water shader:
// judged inside the pond's screen mask (not black, not blown out, changes with the time offset) and outside it per pixel
// (impl review r001 #6): wind is frozen during the water renders so nothing else may change; a control render proves it.
// The judge is a pure function that is also run on black-pond / white-pond / outside-change counterexamples.
public static class RenderCheck045S2
{
 const string S1="Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main.unity",S2="Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main_s2.unity";
 const string Out="output/keyart-target-045/r002/render";
 static Texture2D Grab(Camera cam){var t=new Texture2D(1280,720,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};var prev=RenderTexture.active;RenderTexture.active=cam.targetTexture;t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();RenderTexture.active=prev;return t;}
 // screen triangle -> mask (screen y is bottom-up, same as ReadPixels rows), dilated by 2 px
 static void Raster(bool[] mask,Vector2 a,Vector2 b,Vector2 c)
 {
  int x0=Mathf.Max(0,(int)Mathf.Min(a.x,Mathf.Min(b.x,c.x))-2),x1=Mathf.Min(1279,(int)Mathf.Max(a.x,Mathf.Max(b.x,c.x))+2);
  int y0=Mathf.Max(0,(int)Mathf.Min(a.y,Mathf.Min(b.y,c.y))-2),y1=Mathf.Min(719,(int)Mathf.Max(a.y,Mathf.Max(b.y,c.y))+2);
  float E(Vector2 p,Vector2 q,Vector2 r)=>(q.x-p.x)*(r.y-p.y)-(q.y-p.y)*(r.x-p.x);float area=E(a,b,c);if(Mathf.Abs(area)<1e-6f)return;
  for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
  {
   bool hit=false;for(int dy=-2;dy<=2&&!hit;dy+=2)for(int dx=-2;dx<=2&&!hit;dx+=2){var p=new Vector2(x+.5f+dx,y+.5f+dy);float w0=E(b,c,p)/area,w1=E(c,a,p)/area,w2=E(a,b,p)/area;hit=w0>=0&&w1>=0&&w2>=0;}
   if(hit)mask[y*1280+x]=true;
  }
 }
 public class Verdict{public string status;public int maskPixels,pondDark,pondBlown,pondDark5,pondBlown5,pondChanged,outsideChanged,controlOutsideChanged;public float pondMeanLum,pondMeanLum5;public string[] fails;}
 static int D(Color32 p,Color32 q)=>Math.Abs(p.r-q.r)+Math.Abs(p.g-q.g)+Math.Abs(p.b-q.b);
 public static Verdict Judge(Color32[] t0,Color32[] t5,Color32[] t0again,bool[] mask)
 {
  // impl review r002 #2: both time samples are judged for dark / blown-out / mean brightness, not only t0
  var v=new Verdict();double lum=0,lum5=0;
  for(int i=0;i<t0.Length;i++)
  {
   if(mask[i]){v.maskPixels++;var p=t0[i];int sum=p.r+p.g+p.b;lum+=sum/3.0;if(sum<60)v.pondDark++;if(p.r>245&&p.g>245&&p.b>245)v.pondBlown++;
    var q=t5[i];int sum5=q.r+q.g+q.b;lum5+=sum5/3.0;if(sum5<60)v.pondDark5++;if(q.r>245&&q.g>245&&q.b>245)v.pondBlown5++;if(D(t0[i],t5[i])>6)v.pondChanged++;}
   else{if(D(t0[i],t5[i])>6)v.outsideChanged++;if(D(t0[i],t0again[i])>6)v.controlOutsideChanged++;}
  }
  v.pondMeanLum=v.maskPixels>0?(float)(lum/v.maskPixels):0;v.pondMeanLum5=v.maskPixels>0?(float)(lum5/v.maskPixels):0;var f=new System.Collections.Generic.List<string>();
  if(v.maskPixels<10000)f.Add("MASK_TOO_SMALL");
  if(v.pondDark>v.maskPixels*.05)f.Add("POND_DARK");
  if(v.pondBlown>v.maskPixels*.02)f.Add("POND_BLOWN_OUT");
  if(v.pondMeanLum<40||v.pondMeanLum>230)f.Add("POND_MEAN_OUT_OF_RANGE");
  if(v.pondDark5>v.maskPixels*.05)f.Add("POND_DARK_T5");
  if(v.pondBlown5>v.maskPixels*.02)f.Add("POND_BLOWN_OUT_T5");
  if(v.pondMeanLum5<40||v.pondMeanLum5>230)f.Add("POND_MEAN_OUT_OF_RANGE_T5");
  if(v.pondChanged<v.maskPixels*.10)f.Add("POND_NOT_ANIMATED");
  if(v.controlOutsideChanged>30)f.Add("TIME_EFFECTS_NOT_FROZEN");
  if(v.outsideChanged>30)f.Add("WATER_CHANGES_OUTSIDE_POND");
  v.fails=f.ToArray();v.status=f.Count==0?"PASS":"FAIL";return v;
 }
 public static object Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE");
  Directory.CreateDirectory(Out);bool async0=ShaderUtil.allowAsyncCompilation;var setup=EditorSceneManager.GetSceneManagerSetup();object result;
  try
  {
   ShaderUtil.allowAsyncCompilation=false;
   Texture2D Shot(string scenePath,string file,Action<DSMapSession,Camera> before=null)
   {
    EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
    var s=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();var look=s.GetComponent<DSLookGlobals>();if(look)look.Apply();
    var go=new GameObject("cap045"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.CopyFrom(s.ViewCamera);cam.enabled=false;cam.aspect=1280f/720f;
    cam.orthographic=true;cam.orthographicSize=6;cam.transform.rotation=Quaternion.Euler(55,0,0);
    cam.transform.position=new Vector3(-22.8f,0,22.4f)-cam.transform.forward*30;
    var rt=new RenderTexture(1280,720,24);cam.targetTexture=rt;before?.Invoke(s,cam);cam.Render();cam.Render();
    var t=new Texture2D(1280,720,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};var prev=RenderTexture.active;RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();RenderTexture.active=prev;
    File.WriteAllBytes(Out+"/"+file,t.EncodeToPNG());cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
    if(look){foreach(var n in new[]{"_DS_ShadowTint","_DS_CloudShadow","_DS_CloudDir","_DS_GradeTint"})Shader.SetGlobalVector(n,Vector4.zero);}
    return t;
   }
   var s1=Shot(S1,"start-s1.png");var s2=Shot(S2,"start-s2.png");
   // water: two renders back-to-back in the same scene load (no reload between them, clouds off) so only _TimeOffset differs;
   // the pond mask is the water mesh projected with the capture camera (pixels inside its screen-space triangles, +2 px).
   Texture2D w0=null,w5=null,w0c=null;var mask=new bool[1280*720];var windFrozen=0;
   Shot(S2,"water-t0.png",(s,cam)=>{
    Shader.SetGlobalVector("_DS_CloudShadow",Vector4.zero);
    // freeze wind on every material in the scene for these renders only (restored and un-dirtied below)
    var windMats=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).SelectMany(x=>x.sharedMaterials).Where(m=>m&&m.HasProperty("_WindStrength")).Distinct().ToArray();
    var wind0=windMats.Select(m=>m.GetFloat("_WindStrength")).ToArray();windFrozen=windMats.Length;
    try{foreach(var wm in windMats)wm.SetFloat("_WindStrength",0);
    var r=s.GetComponentsInChildren<Renderer>(true).First(x=>x.name=="OB-006_Water045");var water=r.sharedMaterial;
    var mesh=r.GetComponent<MeshFilter>().sharedMesh;var m=r.transform.localToWorldMatrix;var tri=mesh.triangles;var vs=mesh.vertices.Select(v=>(Vector2)cam.WorldToScreenPoint(m.MultiplyPoint3x4(v))).ToArray();
    for(int i=0;i<tri.Length;i+=3)Raster(mask,vs[tri[i]],vs[tri[i+1]],vs[tri[i+2]]);
    water.SetFloat("_TimeOffset",0);cam.Render();w0=Grab(cam);water.SetFloat("_TimeOffset",5);cam.Render();w5=Grab(cam);water.SetFloat("_TimeOffset",0);cam.Render();w0c=Grab(cam);EditorUtility.ClearDirty(water);}
    finally{for(int i=0;i<windMats.Length;i++){windMats[i].SetFloat("_WindStrength",wind0[i]);EditorUtility.ClearDirty(windMats[i]);}}});
   File.WriteAllBytes(Out+"/water-t5.png",w5.EncodeToPNG());File.WriteAllBytes(Out+"/water-t0b.png",w0.EncodeToPNG());
   var a=w0.GetPixels32();var b=w5.GetPixels32();var c0=w0c.GetPixels32();
   var real=Judge(a,b,c0,mask);
   // counterexamples must FAIL: black pond, white pond, 500 changed pixels outside the pond
   Color32[] Paint(Color32[] src,Func<int,bool> where,Color32 col){var x=(Color32[])src.Clone();for(int i=0;i<x.Length;i++)if(where(i))x[i]=col;return x;}
   var outsideIdx=Enumerable.Range(0,mask.Length).Where(i=>!mask[i]).Where((i,n)=>n%997==0).Take(500).ToHashSet();
   var blackPond=Paint(a,i=>mask[i],new Color32(0,0,0,255));var whitePond=Paint(a,i=>mask[i],new Color32(255,255,255,255));
   var self=new{
    blackPond=Judge(blackPond,Paint(b,i=>mask[i],new Color32(0,0,0,255)),c0,mask),
    whitePond=Judge(whitePond,Paint(b,i=>mask[i],new Color32(255,255,255,255)),c0,mask),
    outsideChange=Judge(a,Paint(b,i=>outsideIdx.Contains(i),new Color32(255,0,255,255)),c0,mask),
    blackPondT5Only=Judge(a,Paint(b,i=>mask[i],new Color32(0,0,0,255)),c0,mask),
    whitePondT5Only=Judge(a,Paint(b,i=>mask[i],new Color32(255,255,255,255)),c0,mask)};
   bool selfOk=self.blackPond.status=="FAIL"&&self.whitePond.status=="FAIL"&&self.outsideChange.status=="FAIL"&&self.blackPondT5Only.status=="FAIL"&&self.whitePondT5Only.status=="FAIL";
   int bright=s2.GetPixels32().Count(p=>p.r+p.g+p.b>40),blown=s2.GetPixels32().Count(p=>p.r>250&&p.g>250&&p.b>250);
   bool screenOk=bright>800000&&blown<20000;
   result=new{status=real.status=="PASS"&&selfOk&&screenOk&&windFrozen>0?"PASS":"FAIL",water=real,selfTest=self,selfTestOk=selfOk,windMaterialsFrozen=windFrozen,
    startScreen=new{nonBlank=bright,blownOut=blown,ok=screenOk},files=new[]{"start-s1.png","start-s2.png","water-t0.png","water-t5.png","water-t0b.png"}};
   File.WriteAllText(Out+"/render-check.json",JsonConvert.SerializeObject(result,Formatting.Indented));
  }
  finally{ShaderUtil.allowAsyncCompilation=async0;if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);}
  return result;
 }
}
