using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using DeadSurvivor;
using Newtonsoft.Json;
public static class ReferenceGroundCapture036
{
 public static object Capture(string path,float elevation,float ortho,float px,float py,int width,int height)
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/DeadSurvivor/Scenes/Map036_ReferenceGround_r003/Map036_ReferenceGround.unity")throw new Exception("WRONG_SCENE");
  if(!path.StartsWith("output/reference-ground-036/20261102-r001/",StringComparison.Ordinal)||path.Contains("..")||File.Exists(path))throw new Exception("UNSAFE_CAPTURE_PATH");
  var s=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();var data=JsonUtility.FromJson<DSBlockoutData>(s.Layout.text);var go=new GameObject("036 temporary comparison camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();
  var rt=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
  try {
   cam.CopyFrom(s.ViewCamera);cam.enabled=false;cam.orthographic=true;cam.orthographicSize=ortho;cam.aspect=(float)width/height;cam.transform.rotation=Quaternion.Euler(elevation,0,0);cam.transform.position=DSBlockoutMesh.World(data.Logical(new Vector2(px,py),4),data.world_scale)-cam.transform.forward*150;cam.targetTexture=rt;
   cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
   var record=new{status="PASS_CAPTURE_ONLY",source="Unity render of actual saved scene geometry",path,elevation,ortho,centerPixels=new[]{px,py},width,height,play=EditorApplication.isPlaying,frame=Time.frameCount,scope="Comparison render; actual gameplay input tested separately"};File.WriteAllText(Path.ChangeExtension(path,".json"),JsonConvert.SerializeObject(record,Formatting.Indented));return record;
  }finally{RenderTexture.active=previous;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
 }
}
