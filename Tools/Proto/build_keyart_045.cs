using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using Newtonsoft.Json;

// 045 stage 1: build Proto045_Main from Map043 with the key-art layout, placeholder structures, look settings and 3D actors.
// Owns Assets/DeadSurvivor/Scenes/Proto045 (refuses if it exists). Never overwrites; on failure deletes only what it created.
public static class KeyArt045Build
{
 const string Source="Assets/DeadSurvivor/Scenes/Map043_LowPolyShader_r001/Map043_LowPolyShader.unity";
 const string Dir="Assets/DeadSurvivor/Scenes/Proto045";
 const string ArtDir=Dir+"/Art";
 const string Target=Dir+"/Proto045_Main.unity";
 const string Out="output/keyart-target-045/r001";
 const string LayoutIn=Out+"/layout/Layout045.json";
 const string Mat043="Assets/DeadSurvivor/Art/LowPoly043/Materials/LowPoly043_";
 const string Shader043="Assets/DeadSurvivor/Art/LowPoly043/Shaders/DSLowPolyPalette.shader";
 const string Char042="Assets/DeadSurvivor/Art/Characters/Test042_r001/";
 static readonly Vector2 Origin=new(565,580);
 static readonly List<string> created=new();

 static Vector3 W(Vector2 px,float y=0)=>new((px.x-Origin.x)*.08f,y,-(px.y-Origin.y)*.08f);
 static Vector2 Px(Vector3 w)=>new(w.x/.08f+Origin.x,-w.z/.08f+Origin.y);
 static Vector2 Cell(int c)=>new(((c%8)+.5f)/8f,(3-(c/8)+.5f)/4f);
 static string Sha(byte[] b){using var s=SHA256.Create();return string.Concat(s.ComputeHash(b).Select(x=>x.ToString("x2")));}

 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE:"+EditorSceneManager.GetSceneAt(i).path);
  if(AssetDatabase.IsValidFolder(Dir)||Directory.Exists(Dir))throw new Exception("DESTINATION_EXISTS");
  if(File.Exists(Out+"/build.json"))throw new Exception("OUTPUT_EXISTS");
  var report=JsonConvert.DeserializeObject<Dictionary<string,object>>(File.ReadAllText(Out+"/layout/layout-report.json"));
  var layoutBytes=File.ReadAllBytes(LayoutIn);var layoutSha=Sha(layoutBytes);
  if((string)report["status"]!="PASS"||(string)report["layout_sha256"]!=layoutSha)throw new Exception("LAYOUT_REPORT_MISMATCH");
  // shader edited outside the editor: force import and verify
  AssetDatabase.ImportAsset(Shader043,ImportAssetOptions.ForceUpdate);
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(Shader043);
  if(!shader||ShaderUtil.ShaderHasError(shader)||shader.FindPropertyIndex("_DitherFade")<0||shader.FindPropertyIndex("_BaseColor")<0)throw new Exception("SHADER_NOT_READY");
  var setup=EditorSceneManager.GetSceneManagerSetup();var selection=Selection.objects;
  var checks=new Dictionary<string,object>();object result;created.Clear();
  try
  {
   AssetDatabase.CreateFolder("Assets/DeadSurvivor/Scenes","Proto045");created.Add(Dir);AssetDatabase.CreateFolder(Dir,"Art");
   File.WriteAllBytes(Dir+"/Layout045.json",layoutBytes);AssetDatabase.ImportAsset(Dir+"/Layout045.json");
   var layoutAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Dir+"/Layout045.json");
   if(Sha(System.Text.Encoding.UTF8.GetBytes(layoutAsset.text))!=layoutSha)throw new Exception("LAYOUT_COPY_SHA");
   var data=JsonUtility.FromJson<DSBlockoutData>(layoutAsset.text);data.Geometry(4f);   // Validate + polygon rules
   if(!AssetDatabase.CopyAsset(Source,Target))throw new Exception("COPY_FAILED");
   var scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   var sessions=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include);var games=UnityEngine.Object.FindObjectsByType<DSGame>(FindObjectsInactive.Include);
   if(sessions.Length!=1||games.Length!=1)throw new Exception("UNEXPECTED_SCENE_SHAPE");
   var session=sessions[0];var vis=session.Visibility;var root=session.transform;
   if(root.position!=Vector3.zero||root.rotation!=Quaternion.identity||root.lossyScale!=Vector3.one)throw new Exception("ROOT_NOT_IDENTITY");
   var dupes=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).GroupBy(r=>Path(r.transform)).Where(g=>g.Count()>1).Select(g=>g.Key).ToArray();
   if(dupes.Length>0)throw new Exception("DUPLICATE_RENDERER_PATHS:"+string.Join(",",dupes.Take(5)));
   var allBefore=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).ToDictionary(r=>Path(r.transform),r=>r.transform.position);

   // --- materials (045 copies; dither fade on for occluders) ---
   Material Copy(string preset,string name,bool dither){var m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Mat043+preset+".mat")){name=name};m.SetFloat("_DitherFade",dither?1:0);m.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(m,ArtDir+"/"+name+".mat");return m;}
   var stoneM=Copy("Stone","Stone045",true);var bushM=Copy("Foliage","Foliage045",true);var waterM=Copy("Ground","Water045",false);var soilM=Copy("Soil","Soil045",false);
   waterM.SetFloat("_NoiseStrength",.03f);

   // --- remove old OB-001..006 visuals (rewire occluders first) ---
   var removed=new List<GameObject>();
   foreach(var t in root.GetComponentsInChildren<Transform>(true))
    if(t.parent&&(t.parent.name=="Reference037_VisualOnly"||t.parent.name=="Tripo038_VisualOnly")&&System.Text.RegularExpressions.Regex.IsMatch(t.name,"^OB-00[1-6]_"))removed.Add(t.gameObject);
   var soil=new[]{"SoilLayer0","SoilLayer1","SoilLayer2"}.Select(n=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==n).gameObject).ToArray();
   var removedRenderers=new HashSet<Renderer>(removed.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)));
   var art=new GameObject("KeyArt045_VisualOnly");art.transform.SetParent(root,false);
   var meshes=new List<Mesh>();var occluders=new List<Renderer>();var contain=new List<object>();
   var rng=new System.Random(4501);float J(float a)=>(float)(rng.NextDouble()*2-1)*a;
   Renderer Add(string name,Mesh mesh,Material m,Transform parent){mesh.name=name;meshes.Add(mesh);var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=m;return r;}
   foreach(var o in data.objects.Where(o=>string.CompareOrdinal(o.id,"OB-006")<=0))
   {
    var poly=o.points_px;var c=new Vector2(poly.Average(p=>p.x),poly.Average(p=>p.y));
    var g=new GameObject(o.id+"_KeyArt");g.transform.SetParent(art.transform,false);
    bool pond=o.id=="OB-006";
    var stones=Stones(poly,c,.3f,rng);var sr=Add(o.id+"_Stones",stones,stoneM,g.transform);occluders.Add(sr);
    var verts=new List<Vector3>(stones.vertices);
    if(pond){var water=Flat(Inset(poly,c,.6f),.02f,26,27,rng);Add(o.id+"_Water",water,waterM,g.transform);}
    else
    {
     float rx=poly.Max(p=>p.x)-poly.Min(p=>p.x),ry=poly.Max(p=>p.y)-poly.Min(p=>p.y);
     float r=Mathf.Min(rx,ry)*.08f*.5f*.42f;var bush=Bush(r,J);var br=Add(o.id+"_Bush",bush,bushM,g.transform);
     br.transform.position=W(c);occluders.Add(br);verts.AddRange(bush.vertices.Select(v=>v+W(c)));
    }
    var pxs=verts.Select(v=>Px(v)).ToArray();int outside=pxs.Count(p=>!Inside(p,poly));
    float maxGap=poly.Max(p=>pxs.Min(q=>Vector2.Distance(p,q)));
    contain.Add(new{o.id,artVertices=pxs.Length,outside,maxPolygonVertexToArtPx=Math.Round(maxGap,2)});
   }
   // dirt patches (key-art positions projected + a few in the open north) and border bushes south of the road
   var dirt=new GameObject("Dirt045");dirt.transform.SetParent(art.transform,false);int di=0;
   foreach(var (u,v,w,h) in new[]{(445,45,150,110),(425,200,95,60),(1085,205,135,95),(0,365,145,90),(645,410,115,90),(430,515,75,45)})
   {var cc=K(u+w/2f,v+h/2f);Dirt(cc,w/2f*.2083f,h/2f*.2543f);}
   foreach(var (x,y,rx,ry) in new[]{(180f,140f,14f,9f),(330f,120f,16f,10f),(455f,150f,12f,8f),(240f,190f,10f,7f)})Dirt(new Vector2(x,y),rx,ry);
   void Dirt(Vector2 cc,float rx,float ry){var blob=Blob(cc,rx,ry,rng);Add("DirtRim_"+di,Flat(blob.Select(p=>cc+(p-cc)*1.14f).ToArray(),.011f,11,11,rng),soilM,dirt.transform);Add("Dirt_"+di++,Flat(blob,.012f,12,12,rng),soilM,dirt.transform);}
   var border=new GameObject("Border045_VisualOnly");border.transform.SetParent(art.transform,false);
   for(float x=150;x<=420;x+=13f){var b=Bush(.42f+J(.12f),J);var r=Add("Border_"+x,b,bushM,border.transform);r.transform.position=W(new Vector2(x+J(3),384+J(4)));occluders.Add(r);}
   // road: translate the existing cobble road to the key-art road position (visual only)
   float roadDz=-3.276f;foreach(var n in new[]{"CobbleBed","PathCobbles0","PathCobbles1","PathCobbles2","PathCobbles3"})root.GetComponentsInChildren<Transform>(true).First(t=>t.name==n).position+=new Vector3(0,0,roadDz);
   // mesh asset
   AssetDatabase.CreateAsset(meshes[0],ArtDir+"/Meshes045.asset");foreach(var m in meshes.Skip(1))AssetDatabase.AddObjectToAsset(m,meshes[0]);

   // occluders: rewire before destroying
   var blockers=vis.Blockers.Where(r=>r&&!removedRenderers.Contains(r)).Concat(occluders).ToArray();
   // 045-I1: legacy occluders (OB-007..064, rims, PERIM) use the shared 043 materials (_DitherFade 0 -> they never fade).
   // In this scene only, point them at dither-enabled copies; the shared 043 materials stay untouched.
   var ditherCopies=new Dictionary<Material,Material>();int relinked=0;
   foreach(var r in blockers.Where(r=>!occluders.Contains(r)))
   {
    var mats=r.sharedMaterials;bool relink=false;
    for(int i=0;i<mats.Length;i++)
    {
     var m=mats[i];if(!m||!AssetDatabase.GetAssetPath(m).StartsWith("Assets/DeadSurvivor/Art/LowPoly043/Materials/"))continue;
     if(!ditherCopies.TryGetValue(m,out var c)){c=new Material(m){name=m.name.Replace("LowPoly043_","")+"045D"};c.SetFloat("_DitherFade",1);c.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(c,ArtDir+"/"+c.name+".mat");ditherCopies[m]=c;}
     mats[i]=c;relink=true;
    }
    if(relink){r.sharedMaterials=mats;relinked++;}
   }
   checks["legacyOccludersRelinked"]=relinked;checks["ditherCopies"]=ditherCopies.Values.Select(m=>m.name).ToArray();
   var vso=new SerializedObject(vis);var bp=vso.FindProperty("Blockers");bp.arraySize=blockers.Length;for(int i=0;i<blockers.Length;i++)bp.GetArrayElementAtIndex(i).objectReferenceValue=blockers[i];vso.ApplyModifiedPropertiesWithoutUndo();
   foreach(var g in removed)UnityEngine.Object.DestroyImmediate(g);
   foreach(var g in soil)g.SetActive(false);
   if(vis.Blockers.Any(r=>!r))throw new Exception("BLOCKER_LOST");

   // session: layout, combat, presentation, look
   var so=new SerializedObject(session);so.FindProperty("Layout").objectReferenceValue=layoutAsset;so.FindProperty("ExpectedDataSha").stringValue=layoutSha;so.FindProperty("CombatEnabled").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
   var pres=session.gameObject.AddComponent<DSMapPresentation>();pres.CameraDistance=30;pres.Actors=ActorSet();
   session.gameObject.AddComponent<DSLookGlobals>();
   var cam=session.ViewCamera;cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
   // key-art light: sun from the north/upper-left so cast shadows fall toward the camera (visible), as in day.png
   var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Single(l=>l.type==LightType.Directional);
   checks["sunBefore"]=new{rot=sun.transform.eulerAngles.ToString("F1"),sun.intensity,shadows=sun.shadows.ToString(),sun.shadowStrength};
   sun.transform.rotation=Quaternion.Euler(50,155,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.85f;
   var volGo=new GameObject("KeyArt045_Volume");volGo.transform.SetParent(root,false);var vol=volGo.AddComponent<Volume>();vol.isGlobal=true;vol.priority=1;vol.sharedProfile=Profile();

   // checks
   if(art.GetComponentsInChildren<Collider>(true).Length!=0||UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length!=0)throw new Exception("COLLIDER_PRESENT");
   var moved=new HashSet<string>{"CobbleBed","PathCobbles0","PathCobbles1","PathCobbles2","PathCobbles3"};
   var after=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).Where(r=>!r.transform.IsChildOf(art.transform)).ToDictionary(r=>Path(r.transform),r=>r.transform.position);
   var changed=after.Where(kv=>!moved.Contains(kv.Key.Split('/').Last())&&(!allBefore.TryGetValue(kv.Key,out var p)||p!=kv.Value)).Select(kv=>kv.Key).ToArray();
   if(changed.Length>0)throw new Exception("UNEXPECTED_RENDERER_CHANGE:"+string.Join(",",changed.Take(5)));
   if(contain.Any(c=>(int)c.GetType().GetProperty("outside").GetValue(c)!=0))throw new Exception("ART_OUTSIDE_POLYGON:"+JsonConvert.SerializeObject(contain));
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
   // save only what this tool created (a global SaveAssets also writes unrelated dirty assets such as the TMP font cache)
   foreach(var guid in AssetDatabase.FindAssets("",new[]{Dir}))foreach(var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))if(o)AssetDatabase.SaveAssetIfDirty(o);
   // reload check
   scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   var s2=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include)[0];
   if(s2.ExpectedDataSha!=layoutSha||s2.Visibility.Blockers.Any(r=>!r)||!s2.GetComponent<DSMapPresentation>()||!s2.GetComponent<DSMapPresentation>().Actors)throw new Exception("RELOAD_CHECK_FAILED");
   checks["layoutSha"]=layoutSha;checks["blockers"]=s2.Visibility.Blockers.Length;checks["removedObjects"]=removed.Count;checks["newOccluders"]=occluders.Count;checks["containment"]=contain;checks["meshes"]=meshes.Count;checks["unchangedRenderers"]=after.Count-moved.Count;
   result=new{status="BUILT",source=Source,target=Target,checks,created=created.ToArray()};
   File.WriteAllText(Out+"/build.json",JsonConvert.SerializeObject(result,Formatting.Indented));
  }
  catch(Exception ex)
  {
   File.WriteAllText(Out+"/build-failure.json",JsonConvert.SerializeObject(new{error=ex.ToString(),created},Formatting.Indented));
   EditorSceneManager.RestoreSceneManagerSetup(setup);
   foreach(var p in created.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(p);
   throw;
  }
  finally{if(EditorSceneManager.GetActiveScene().path==Target||EditorSceneManager.GetSceneManagerSetup().Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);Selection.objects=selection;}
  return result;
 }

 static Vector2 K(float u,float v)=>new(280+(u-640)*0.20833f,300+(v-360)*0.25434f);
 static string Path(Transform t)=>t.parent?Path(t.parent)+"/"+t.name:t.name;
 static bool Inside(Vector2 p,Vector2[] poly){bool r=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)r=!r;return r;}
 static Vector2[] Inset(Vector2[] poly,Vector2 c,float px)=>poly.Select(p=>p+(c-p).normalized*px).ToArray();
 static Vector2[] Blob(Vector2 c,float rx,float ry,System.Random rng){int n=12;var pts=new Vector2[n];for(int i=0;i<n;i++){float a=Mathf.PI*2*i/n,k=.78f+(float)rng.NextDouble()*.3f;pts[i]=c+new Vector2(Mathf.Cos(a)*rx*k,Mathf.Sin(a)*ry*k);}return pts;}

 // flat polygon (fan from centroid; inputs are star-shaped), up normals, palette UV per triangle alternating two cells
 static Mesh Flat(Vector2[] poly,float y,int cellA,int cellB,System.Random rng)
 {
  var c=new Vector2(poly.Average(p=>p.x),poly.Average(p=>p.y));var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
  for(int i=0;i<poly.Length;i++)
  {
   var cell=Cell(cellA);int b=v.Count;   // one cell per patch (alternating cells read as pie slices)
   v.Add(W(c,y));v.Add(W(poly[(i+1)%poly.Length],y));v.Add(W(poly[i],y));uv.Add(cell);uv.Add(cell);uv.Add(cell);
   t.AddRange(new[]{b,b+1,b+2});
  }
  var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.SetColors(v.Select(_=>Color.white).ToList());m.RecalculateNormals();m.RecalculateBounds();
  if(m.normals.Any(n=>n.y<0)){t.Reverse();m.SetTriangles(t,0);m.RecalculateNormals();}
  return m;
 }
 // chunky stones (truncated boxes) along the polygon outline, inset toward the centroid; one mesh per structure
 static Mesh Stones(Vector2[] poly,Vector2 c,float insetW,System.Random rng)
 {
  var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();int dropped=0;
  var w=poly.Select(p=>W(p)).ToArray();var cw=W(c);
  float per=0;for(int i=0;i<w.Length;i++)per+=Vector3.Distance(w[i],w[(i+1)%w.Length]);
  int n=Mathf.Max(6,Mathf.RoundToInt(per/.44f));float step=per/n;
  for(int k=0;k<n;k++)
  {
   float d=k*step+(float)rng.NextDouble()*.06f,acc=0;int e=0;
   while(e<w.Length&&acc+Vector3.Distance(w[e],w[(e+1)%w.Length])<d){acc+=Vector3.Distance(w[e],w[(e+1)%w.Length]);e++;}
   e%=w.Length;var a=w[e];var b=w[(e+1)%w.Length];float f=(d-acc)/Mathf.Max(1e-4f,Vector3.Distance(a,b));
   var p=Vector3.Lerp(a,b,Mathf.Clamp01(f));var tan=(b-a).normalized;
   var inward=new Vector3(-tan.z,0,tan.x);if(Vector3.Dot(inward,cw-p)<0)inward=-inward;   // edge normal toward the inside
   float len=.34f+(float)rng.NextDouble()*.12f,wid=.26f+(float)rng.NextDouble()*.08f,h=.24f+(float)rng.NextDouble()*.14f;
   var rot=Quaternion.LookRotation(tan,Vector3.up)*Quaternion.Euler(0,(float)(rng.NextDouble()*2-1)*12,0);
   var cell=Cell(rng.NextDouble()<.6?22:21);
   // keep every corner inside the collision polygon: shrink, then push inward, else drop the stone
   bool placed=false;
   foreach(var (scale,extra) in new[]{(1f,0f),(.85f,.08f),(.7f,.16f)})
   {
    var q=p+inward*(insetW+extra);float L=len*scale,Wd=wid*scale;
    var corners=new[]{new Vector3(-Wd/2,0,-L/2),new Vector3(Wd/2,0,-L/2),new Vector3(Wd/2,0,L/2),new Vector3(-Wd/2,0,L/2)}.Select(x=>q+rot*x);
    if(corners.All(x=>Inside(Px(x),poly))){Box(v,uv,t,q,rot,L,Wd,h,cell);placed=true;break;}
   }
   if(!placed)dropped++;
  }
  if(dropped>n/4)throw new Exception("TOO_MANY_STONES_DROPPED:"+dropped+"/"+n);
  var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.SetColors(v.Select(_=>Color.white).ToList());m.RecalculateNormals();m.RecalculateBounds();return m;
 }
 static void Box(List<Vector3> v,List<Vector2> uv,List<int> t,Vector3 c,Quaternion r,float len,float wid,float h,Vector2 cell)
 {
  float ib=.78f;   // top inset: truncated box reads as a chiselled block
  Vector3 P(float x,float y,float z)=>c+r*new Vector3(x,y,z);
  var bot=new[]{P(-wid/2,0,-len/2),P(wid/2,0,-len/2),P(wid/2,0,len/2),P(-wid/2,0,len/2)};
  var top=new[]{P(-wid/2*ib,h,-len/2*ib),P(wid/2*ib,h,-len/2*ib),P(wid/2*ib,h,len/2*ib),P(-wid/2*ib,h,len/2*ib)};
  void Q(Vector3 a,Vector3 b,Vector3 cc,Vector3 d){int i=v.Count;v.AddRange(new[]{a,b,cc,d});uv.AddRange(new[]{cell,cell,cell,cell});t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
  Q(top[0],top[3],top[2],top[1]);
  for(int i=0;i<4;i++){int j=(i+1)%4;Q(bot[i],top[i],top[j],bot[j]);}
 }
 // faceted bush: subdivided icosahedron, flat shaded, slight jitter, squashed, sitting on the ground
 static Mesh Bush(float r,Func<float,float> J)
 {
  float g=(1+Mathf.Sqrt(5))/2;
  var p=new List<Vector3>{new(-1,g,0),new(1,g,0),new(-1,-g,0),new(1,-g,0),new(0,-1,g),new(0,1,g),new(0,-1,-g),new(0,1,-g),new(g,0,-1),new(g,0,1),new(-g,0,-1),new(-g,0,1)};
  var f=new List<int[]>{new[]{0,11,5},new[]{0,5,1},new[]{0,1,7},new[]{0,7,10},new[]{0,10,11},new[]{1,5,9},new[]{5,11,4},new[]{11,10,2},new[]{10,7,6},new[]{7,1,8},new[]{3,9,4},new[]{3,4,2},new[]{3,2,6},new[]{3,6,8},new[]{3,8,9},new[]{4,9,5},new[]{2,4,11},new[]{6,2,10},new[]{8,6,7},new[]{9,8,1}};
  var mid=new Dictionary<long,int>();
  int M(int a,int b){long k=a<b?((long)a<<32)|(uint)b:((long)b<<32)|(uint)a;if(mid.TryGetValue(k,out var i))return i;p.Add((p[a]+p[b])/2);return mid[k]=p.Count-1;}
  var f2=new List<int[]>();foreach(var x in f){int a=M(x[0],x[1]),b=M(x[1],x[2]),c=M(x[2],x[0]);f2.AddRange(new[]{new[]{x[0],a,c},new[]{x[1],b,a},new[]{x[2],c,b},new[]{a,b,c}});}
  var q=p.Select(x=>x.normalized*(1+J(.07f))).Select(x=>new Vector3(x.x*r,(x.y*.85f+.78f)*r,x.z*r)).ToList();
  var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
  foreach(var x in f2){var cell=Cell(x[0]%5==0?6:5);int i=v.Count;v.Add(q[x[0]]);v.Add(q[x[1]]);v.Add(q[x[2]]);uv.Add(cell);uv.Add(cell);uv.Add(cell);t.AddRange(new[]{i,i+1,i+2});}
  var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.SetColors(v.Select(_=>Color.white).ToList());m.RecalculateNormals();
  if(Vector3.Dot(m.normals[0],(v[0]+v[1]+v[2])/3-new Vector3(0,.78f*r,0))<0){t.Reverse();m.SetTriangles(t,0);m.RecalculateNormals();}
  m.RecalculateBounds();return m;
 }

 // --- actors (placeholders from 042) ---
 static DSMapActorSet ActorSet()
 {
  var heroFbx=AssetDatabase.LoadAssetAtPath<GameObject>(Char042+"hero_model.fbx");var monFbx=AssetDatabase.LoadAssetAtPath<GameObject>(Char042+"monster.fbx");
  var clips=AssetDatabase.LoadAllAssetsAtPath(Char042+"hero_model.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
  var idle=clips.First(c=>c.name.EndsWith("idle"));var run=clips.First(c=>c.name.EndsWith("run"));
  var ctrl=AnimatorController.CreateAnimatorControllerAtPath(ArtDir+"/Hero045.controller");
  ctrl.AddParameter("Run",AnimatorControllerParameterType.Bool);var sm=ctrl.layers[0].stateMachine;
  var si=sm.AddState("Idle");si.motion=idle;var sr=sm.AddState("Run");sr.motion=run;sm.defaultState=si;
  var t1=si.AddTransition(sr);t1.AddCondition(AnimatorConditionMode.If,0,"Run");t1.hasExitTime=false;t1.duration=.08f;
  var t2=sr.AddTransition(si);t2.AddCondition(AnimatorConditionMode.IfNot,0,"Run");t2.hasExitTime=false;t2.duration=.12f;
  var hero=Prefab(heroFbx,"Hero045",ctrl,AssetDatabase.LoadAssetAtPath<Material>(Char042+"Hero042.mat"));
  var mon=Prefab(monFbx,"Monster045",null,AssetDatabase.LoadAssetAtPath<Material>(Char042+"Monster042.mat"));
  var hit=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Hit045"};hit.SetColor("_BaseColor",new Color(1,.93f,.9f));AssetDatabase.CreateAsset(hit,ArtDir+"/Hit045.mat");
  var proto=JsonUtility.FromJson<PrototypeData>(Resources.Load<TextAsset>("DeadSurvivor/PrototypeData").text);
  var set=ScriptableObject.CreateInstance<DSMapActorSet>();
  set.Heroes=proto.characters.Select(c=>new DSMapActorSet.Entry{id=c.id,prefab=hero}).ToArray();
  var baseMon=AssetDatabase.LoadAssetAtPath<Material>(Char042+"Monster042.mat");
  set.Monsters=proto.monsters.Select(m=>{
   if(m.id=="melee")return new DSMapActorSet.Entry{id=m.id,prefab=mon};
   var mat=new Material(baseMon){name="Monster045_"+m.id};mat.SetTexture("_BaseMap",null);mat.SetColor("_BaseColor",DSWorld.ColorOf(m.color));AssetDatabase.CreateAsset(mat,ArtDir+"/"+mat.name+".mat");
   return new DSMapActorSet.Entry{id=m.id,prefab=mon,material=mat};}).ToArray();
  set.HitMaterial=hit;
  AssetDatabase.CreateAsset(set,ArtDir+"/ActorSet045.asset");return set;
 }
 // root (feet) -> "Visual" (identity; receives turn/bob) -> model normalised to height 1, feet at 0, centred
 static GameObject Prefab(GameObject fbx,string name,RuntimeAnimatorController ctrl,Material mat)
 {
  var root=new GameObject(name);var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);
  var model=(GameObject)PrefabUtility.InstantiatePrefab(fbx);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.transform.SetParent(visual.transform,false);
  var rs=model.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
  float s=1f/Mathf.Max(.001f,b.size.y);model.transform.localScale=Vector3.one*s;
  model.transform.localPosition=new Vector3(-b.center.x*s,-b.min.y*s,-b.center.z*s);
  var anim=model.GetComponent<Animator>();if(ctrl){if(!anim)anim=model.AddComponent<Animator>();anim.runtimeAnimatorController=ctrl;anim.applyRootMotion=false;anim.cullingMode=AnimatorCullingMode.CullUpdateTransforms;}
  else if(anim)UnityEngine.Object.DestroyImmediate(anim);
  if(!mat)throw new Exception("ACTOR_MATERIAL_MISSING:"+name);
  foreach(var r in rs){r.shadowCastingMode=ShadowCastingMode.On;r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();}   // 042 textured materials (FBX defaults are untextured grey)
  var prefab=PrefabUtility.SaveAsPrefabAsset(root,ArtDir+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
 }
 static VolumeProfile Profile()
 {
  var p=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(p,ArtDir+"/Look045.asset");
  T Add<T>()where T:VolumeComponent{var c=p.Add<T>(true);c.name=typeof(T).Name;AssetDatabase.AddObjectToAsset(c,p);return c;}
  var bloom=Add<Bloom>();bloom.threshold.value=.95f;bloom.intensity.value=.3f;bloom.scatter.value=.6f;
  var ca=Add<ColorAdjustments>();ca.postExposure.value=.08f;ca.contrast.value=8;ca.saturation.value=12;
  var wb=Add<WhiteBalance>();wb.temperature.value=6;
  var tm=Add<Tonemapping>();tm.mode.value=TonemappingMode.Neutral;
  EditorUtility.SetDirty(p);return p;
 }
}
