using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using Newtonsoft.Json;

// 045 stage 2: new scene Proto045_Main_s2 from the stage-1 scene with Tripo stones/boulders/trees, faceted water and
// flower/grass decor. Tall props are fitted by their real mesh (see LowPart). Owns only Scenes/Proto045/Art_s2 and the new scene (refuses if either exists); stage-1 files are
// fingerprinted before/after and must not change. On failure deletes only what it created. Saves only its own assets.
public static class KeyArt045BuildS2
{
 public class ContainRec{public string id;public int placed,dropped,outside;public float gapPx;}
 public class FitRec{public string name,rule;public float movedPx,scale;public int lowVerts,crossings,violations;}
 // impl review r001 #1: tall visual-only props are placed by their real mesh, not their centre. The part a character can
 // walk into (height <= LowPart, character ~0.87 m = 30 px) must be inside the obstacle polygon (pond props) or outside
 // the map boundary (outside trees); boulders are entirely low. Canopy above LowPart may overhang (occlusion dither).
 const float LowPart=1.0f;
 static Material flatStone,flatFoliage;
 const string Dir="Assets/DeadSurvivor/Scenes/Proto045";
 const string Art=Dir+"/Art_s2";
 const string Source=Dir+"/Proto045_Main.unity";
 const string Target=Dir+"/Proto045_Main_s2.unity";
 const string Out="output/keyart-target-045/r002";
 const string Mat043="Assets/DeadSurvivor/Art/LowPoly043/Materials/LowPoly043_";
 const string Palette="Assets/DeadSurvivor/Art/LowPoly043/Palette/palette-r001.png";
 static readonly Vector2 Origin=new(565,580);
 static Vector3 W(Vector2 px,float y=0)=>new((px.x-Origin.x)*.08f,y,-(px.y-Origin.y)*.08f);
 static Vector2 Px(Vector3 w)=>new(w.x/.08f+Origin.x,-w.z/.08f+Origin.y);
 static Vector2 Cell(int c)=>new(((c%8)+.5f)/8f,(3-(c/8)+.5f)/4f);
 static string Sha(string p){using var s=SHA256.Create();return string.Concat(s.ComputeHash(File.ReadAllBytes(p)).Select(x=>x.ToString("x2")));}
 static bool Inside(Vector2 p,Vector2[] poly){bool r=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)r=!r;return r;}
 static float SegDist(Vector2 p,Vector2 a,Vector2 b){var d=b-a;float t=d.sqrMagnitude<1e-9f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}

 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE");
  if(AssetDatabase.IsValidFolder(Art)||Directory.Exists(Art)||File.Exists(Target))throw new Exception("DESTINATION_EXISTS");
  if(File.Exists(Out+"/build-s2.json"))throw new Exception("OUTPUT_EXISTS");
  var assets=new[]{"stone045","boulder045","tree045"};
  foreach(var a in assets){var rp=JsonConvert.DeserializeObject<Dictionary<string,object>>(File.ReadAllText($"{Out}/tripo/{a}/repaint/repaint.json"));if(!File.Exists($"{Out}/tripo/{a}/repaint/model.fbx"))throw new Exception("MISSING_REPAINT:"+a);}
  var stage1=Directory.GetFiles(Dir,"*",SearchOption.AllDirectories).Where(f=>!f.Contains("/Art_s2")).OrderBy(f=>f).ToDictionary(f=>f,Sha);
  var setup=EditorSceneManager.GetSceneManagerSetup();var created=new List<string>();var checks=new Dictionary<string,object>();object result;
  try
  {
   AssetDatabase.CreateFolder(Dir,"Art_s2");created.Add(Art);AssetDatabase.CreateFolder(Art,"Tripo");
   foreach(var a in assets){File.Copy($"{Out}/tripo/{a}/repaint/model.fbx",$"{Art}/Tripo/{a}.fbx");AssetDatabase.ImportAsset($"{Art}/Tripo/{a}.fbx");}
   File.Copy("Tools/Proto/Water045.shader",Art+"/Water045.shader");AssetDatabase.ImportAsset(Art+"/Water045.shader",ImportAssetOptions.ForceUpdate);
   var waterShader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"/Water045.shader");
   if(!waterShader||ShaderUtil.ShaderHasError(waterShader))throw new Exception("WATER_SHADER_ERROR:"+string.Join("|",ShaderUtil.GetShaderMessages(waterShader).Select(m=>m.message)));
   var waterM=new Material(waterShader){name="Water045s2"};waterM.SetTexture("_Palette",AssetDatabase.LoadAssetAtPath<Texture2D>(Palette));AssetDatabase.CreateAsset(waterM,Art+"/Water045s2.mat");
   var stoneM=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Art/Stone045.mat");var foliageM=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Art/Foliage045.mat");
   if(!stoneM||!foliageM)throw new Exception("STAGE1_MATERIALS_MISSING");
   Material Flat(Material src,string name){var m=new Material(src){name=name};m.SetFloat("_FlatShading",1);m.EnableKeyword("_FLAT_SHADING");AssetDatabase.CreateAsset(m,Art+"/"+name+".mat");return m;}
   stoneM=Flat(stoneM,"Stone045s2");foliageM=Flat(foliageM,"Foliage045s2");flatStone=stoneM;flatFoliage=foliageM;
   var decorM=new Material(AssetDatabase.LoadAssetAtPath<Material>(Mat043+"Foliage.mat")){name="Decor045s2"};decorM.SetFloat("_Cull",0);decorM.SetFloat("_DitherFade",0);decorM.SetFloat("_WindStrength",.015f);
   AssetDatabase.CreateAsset(decorM,Art+"/Decor045s2.mat");
   if(!AssetDatabase.CopyAsset(Source,Target))throw new Exception("COPY_FAILED");created.Add(Target);
   var scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   var session=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single();var vis=session.Visibility;var root=session.transform;
   var data=JsonUtility.FromJson<DSBlockoutData>(session.Layout.text);
   var art1=root.Find("KeyArt045_VisualOnly");if(!art1)throw new Exception("STAGE1_ART_MISSING");
   var before=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).ToDictionary(r=>Path(r.transform),r=>r.transform.position);
   var removed=art1.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.EndsWith("_Stones")||r.name=="OB-006_Water").ToList();
   // stage-1 baseline of the boundary-gap metric (S2D6) before the stones are replaced
   var gapBaseline=new Dictionary<string,float>();
   foreach(var r in removed.Where(r=>r.name.EndsWith("_Stones"))){var id=r.name.Substring(0,6);var poly=data.objects.First(o=>o.id==id).points_px;gapBaseline[id]=Gap(poly,WorldVerts(r).Select(Px).ToArray());}
   checks["stage1GapPx"]=gapBaseline;
   var s2=new GameObject("KeyArt045_S2_VisualOnly");s2.transform.SetParent(root,false);
   var meshes=new List<Mesh>();var occluders=new List<Renderer>();var contain=new List<ContainRec>();var fits=new List<FitRec>();int treesDropped=0;var rng=new System.Random(4502);
   float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
   var models=assets.ToDictionary(a=>a,a=>AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/Tripo/{a}.fbx"));
   // stones per structure (combined), boulders/trees as prefab instances
   foreach(var o in data.objects.Where(o=>string.CompareOrdinal(o.id,"OB-006")<=0))
   {
    var poly=o.points_px;var c=new Vector2(poly.Average(p=>p.x),poly.Average(p=>p.y));
    var parent=new GameObject(o.id+"_S2");parent.transform.SetParent(s2.transform,false);
    var ring=StoneRing(models["stone045"],poly,c,o.id=="OB-006"?.32f:.28f,R,out int dropped,out int placed);
    ring.name=o.id+"_TripoStones";meshes.Add(ring);
    var rr=Add(parent.transform,o.id+"_TripoStones",ring,stoneM);occluders.Add(rr);
    var pv=ring.vertices.Select(v=>Px(v)).ToArray();
    contain.Add(new ContainRec{id=o.id,placed=placed,dropped=dropped,outside=pv.Count(p=>!Inside(p,poly)),gapPx=(float)Math.Round(Gap(poly,pv),2)});
    if(o.id=="OB-006")
    {
     var waterPoly=poly.Select(p=>p+(c-p).normalized*8f).ToArray();
     var water=Water(waterPoly,c,rng);water.name="OB-006_Water045";meshes.Add(water);Add(parent.transform,"OB-006_Water045",water,waterM).shadowCastingMode=ShadowCastingMode.Off;
     // bank props on the far (north-east) side, kept inside the obstacle polygon
     var far=poly.OrderBy(p=>p.y-p.x*.6f).Take(3).ToArray();
     int k=0;foreach(var v in far){var p=v+(c-v).normalized*7f;var b=Instance(models["boulder045"],parent.transform,"Boulder_"+k,W(p),R(0,360),R(.8f,1.2f));
      fits.Add(Fit(b,poly,true,(c-p).normalized,float.MaxValue,"inside OB-006, whole mesh")??throw new Exception("FIT_FAILED:Boulder_"+k));occluders.AddRange(b.GetComponentsInChildren<Renderer>());k++;}
     var tp=far[0]+(c-far[0]).normalized*12f;var t=Instance(models["tree045"],parent.transform,"Tree_pond",W(tp),R(0,360),R(.9f,1.05f));
     fits.Add(Fit(t,poly,true,(c-tp).normalized,LowPart,"inside OB-006, height<=1.0")??throw new Exception("FIT_FAILED:Tree_pond"));occluders.AddRange(t.GetComponentsInChildren<Renderer>());
    }
   }
   // trees outside the map boundary (west/north-west of the representative area)
   var boundary=data.boundary_px;var outside=new GameObject("Trees_OutsideBoundary");outside.transform.SetParent(s2.transform,false);int tk=0;
   foreach(var b in boundary.Where(p=>p.x<220&&p.y>90&&p.y<420))
   {
    var outward=(b-new Vector2(300,250)).normalized;var p=b+outward*18f;if(Inside(p,boundary))continue;
    var t=Instance(models["tree045"],outside.transform,"Tree_out_"+tk++,W(p),R(0,360),R(.85f,1.1f));
    var f=Fit(t,boundary,false,outward,LowPart,"outside boundary, height<=1.0");if(f==null){UnityEngine.Object.DestroyImmediate(t);treesDropped++;continue;}
    fits.Add(f);occluders.AddRange(t.GetComponentsInChildren<Renderer>());
   }
   checks["treesOutside"]=tk-treesDropped;checks["treesOutsideDropped"]=treesDropped;
   // flower / grass decor in walkable space (low, no collider, no shadow casting)
   var obstacles=data.objects.Select(o=>o.points_px).ToArray();var structures=data.objects.Where(o=>string.CompareOrdinal(o.id,"OB-006")<=0).Select(o=>o.points_px).ToArray();
   var road=data.routes.First(r=>r.id=="RT-003").points_px;
   var decorRoot=new GameObject("Decor045");decorRoot.transform.SetParent(s2.transform,false);int decorTris=0,decorRenderers=0,clumps=0;
   for(int cx=0;cx<7;cx++)for(int cy=0;cy<5;cy++)
   {
    float x0=100+cx*60,y0=100+cy*60;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
    for(float x=x0;x<x0+60;x+=5.5f)for(float y=y0;y<y0+60&&y<400;y+=5.5f)
    {
     var p=new Vector2(x+R(-2.5f,2.5f),y+R(-2.5f,2.5f));
     if(!Inside(p,boundary)||obstacles.Any(o=>Inside(p,o)))continue;
     float dObs=structures.Min(o=>Enumerable.Range(0,o.Length).Min(i=>SegDist(p,o[i],o[(i+1)%o.Length])));
     float dRoad=Enumerable.Range(0,road.Length-1).Min(i=>SegDist(p,road[i],road[i+1]));
     if(dRoad<10)continue;
     float prob=.12f+(dObs<14?.6f:dObs<28?.25f:0)+(dRoad<16?.3f:0);
     if(rng.NextDouble()>prob)continue;
     bool flower=rng.NextDouble()<(dObs<14?.55f:.3f);
     Clump(v,uv,t,W(p),flower,rng);clumps++;
    }
    if(t.Count==0)continue;
    var m=new Mesh{name=$"Decor_{cx}_{cy}"};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.SetColors(v.Select(_=>Color.white).ToList());
    m.SetNormals(v.Select(_=>Vector3.up).ToList());m.RecalculateBounds();meshes.Add(m);
    var r=Add(decorRoot.transform,m.name,m,decorM);r.shadowCastingMode=ShadowCastingMode.Off;decorTris+=t.Count/3;decorRenderers++;
   }
   checks["decor"]=new{clumps,triangles=decorTris,renderers=decorRenderers};
   if(decorTris>60000||decorRenderers>40)throw new Exception("DECOR_BUDGET:"+decorTris+"/"+decorRenderers);
   AssetDatabase.CreateAsset(meshes[0],Art+"/Meshes045s2.asset");foreach(var m in meshes.Skip(1))AssetDatabase.AddObjectToAsset(m,meshes[0]);
   // occluders: rewire before destroying the stage-1 stones
   var removedSet=new HashSet<Renderer>(removed);
   var blockers=vis.Blockers.Where(r=>r&&!removedSet.Contains(r)).Concat(occluders).ToArray();
   var so=new SerializedObject(vis);var bp=so.FindProperty("Blockers");bp.arraySize=blockers.Length;for(int i=0;i<blockers.Length;i++)bp.GetArrayElementAtIndex(i).objectReferenceValue=blockers[i];so.ApplyModifiedPropertiesWithoutUndo();
   foreach(var r in removed)UnityEngine.Object.DestroyImmediate(r.gameObject);
   if(vis.Blockers.Any(r=>!r))throw new Exception("BLOCKER_LOST");
   // checks
   if(UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length!=0)throw new Exception("COLLIDER_PRESENT");
   if(contain.Any(c=>c.outside!=0||c.gapPx>6f))throw new Exception("STONE_CONTAINMENT_OR_GAP:"+JsonConvert.SerializeObject(contain));
   var after=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).Where(r=>!r.transform.IsChildOf(s2.transform)).ToDictionary(r=>Path(r.transform),r=>r.transform.position);
   var changed=after.Where(kv=>!before.TryGetValue(kv.Key,out var p)||p!=kv.Value).Select(kv=>kv.Key).ToArray();
   if(changed.Length>0)throw new Exception("UNEXPECTED_RENDERER_CHANGE:"+string.Join(",",changed.Take(5)));
   var tall=s2.GetComponentsInChildren<Renderer>(true).Where(r=>r.shadowCastingMode!=ShadowCastingMode.Off).ToArray();
   var missing=tall.Where(r=>!vis.Blockers.Contains(r)).Select(r=>r.name).ToArray();
   if(missing.Length>0)throw new Exception("OCCLUDER_NOT_REGISTERED:"+string.Join(",",missing));
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
   foreach(var guid in AssetDatabase.FindAssets("",new[]{Art}))foreach(var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))if(o)AssetDatabase.SaveAssetIfDirty(o);
   scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   var vis2=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single().Visibility;
   if(vis2.Blockers.Any(r=>!r))throw new Exception("RELOAD_BLOCKER_NULL");
   // re-check every tall prop and stone ring from the saved scene (not from the in-memory build)
   var s2r=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single().transform.Find("KeyArt045_S2_VisualOnly");
   var pond=data.objects.First(o=>o.id=="OB-006").points_px;var saved=new List<FitRec>();
   foreach(Transform g in s2r.Find("OB-006_S2"))if(g.name.StartsWith("Boulder_")||g.name=="Tree_pond")
    saved.Add(Check(g.gameObject,pond,true,g.name=="Tree_pond"?LowPart:float.MaxValue,g.name=="Tree_pond"?"inside OB-006, height<=1.0":"inside OB-006, whole mesh"));
   foreach(Transform g in s2r.Find("Trees_OutsideBoundary"))saved.Add(Check(g.gameObject,boundary,false,LowPart,"outside boundary, height<=1.0"));
   foreach(var o in data.objects.Where(o=>string.CompareOrdinal(o.id,"OB-006")<=0))saved.Add(Check(s2r.Find(o.id+"_S2/"+o.id+"_TripoStones").gameObject,o.points_px,true,float.MaxValue,"stone ring inside, whole mesh"));
   checks["savedSceneContainment"]=saved;checks["checkSelfTest"]=CheckSelfTest(pond);
   if(saved.Any(f=>f.violations!=0||f.lowVerts==0))throw new Exception("SAVED_CONTAINMENT:"+JsonConvert.SerializeObject(saved.Where(f=>f.violations!=0||f.lowVerts==0)));
   if(saved.Count(f=>f.name.StartsWith("Boulder_")||f.name=="Tree_pond")!=4)throw new Exception("SAVED_POND_PROPS_MISSING");
   var stage1After=Directory.GetFiles(Dir,"*",SearchOption.AllDirectories).Where(f=>!f.Contains("/Art_s2")&&f!=Target&&f!=Target+".meta").OrderBy(f=>f).ToDictionary(f=>f,Sha);
   if(!stage1After.SequenceEqual(stage1))throw new Exception("STAGE1_FILES_CHANGED");
   checks["containment"]=contain;checks["fits"]=fits;checks["occluders"]=occluders.Count;checks["blockers"]=vis2.Blockers.Length;checks["tallRegistered"]=tall.Length;checks["stage1FilesUnchanged"]=stage1.Count;
   checks["models"]=assets.ToDictionary(a=>a,a=>{var g=models[a];var mf=g.GetComponentsInChildren<MeshFilter>();var b=new Bounds();bool f=true;foreach(var r in g.GetComponentsInChildren<Renderer>()){if(f){b=r.bounds;f=false;}else b.Encapsulate(r.bounds);}
    return (object)new{meshes=mf.Length,materials=g.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().Count(),triangles=mf.Sum(x=>x.sharedMesh.triangles.Length/3),size=b.size.ToString("F3")};});
   result=new{status="BUILT",target=Target,checks,created=created.ToArray()};
   File.WriteAllText(Out+"/build-s2.json",JsonConvert.SerializeObject(result,Formatting.Indented));
  }
  catch(Exception ex)
  {
   File.WriteAllText(Out+"/build-s2-failure-"+DateTime.Now.ToString("HHmmss")+".json",JsonConvert.SerializeObject(new{error=ex.ToString(),created},Formatting.Indented));
   if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);
   foreach(var p in created.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(p);
   throw;
  }
  finally{if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);}   // batch-mode editors start with no scene
  return result;
 }

 static string Path(Transform t)=>t.parent?Path(t.parent)+"/"+t.name:t.name;
 // the part of every mesh under g at height <= maxH, as projected points (px): vertices below maxH plus the points where
 // triangle edges cross the maxH plane (impl review r002 #1: a face crossing the plane has a low part between vertices).
 // violations = points on the wrong side of the polygon. For the convex pond polygon this bounds the whole low part.
 static FitRec Check(GameObject g,Vector2[] poly,bool inside,float maxH,string rule)
 {
  var pts=new List<Vector2>();int crossings=0;
  foreach(var mf in g.GetComponentsInChildren<MeshFilter>(true))
  {
   var m=mf.transform.localToWorldMatrix;var w=mf.sharedMesh.vertices.Select(v=>m.MultiplyPoint3x4(v)).ToArray();var tri=mf.sharedMesh.triangles;
   pts.AddRange(w.Where(x=>x.y<=maxH).Select(Px));
   if(maxH<float.MaxValue)for(int i=0;i<tri.Length;i+=3)for(int e=0;e<3;e++)
   {
    var a=w[tri[i+e]];var b=w[tri[i+(e+1)%3]];
    if((a.y-maxH)*(b.y-maxH)<0){pts.Add(Px(Vector3.Lerp(a,b,(maxH-a.y)/(b.y-a.y))));crossings++;}
   }
  }
  return new FitRec{name=g.name,rule=rule,lowVerts=pts.Count,crossings=crossings,violations=pts.Count(p=>Inside(p,poly)!=inside),scale=1};
 }
 // counterexample for Check: one triangle with both low vertices inside the polygon and its high vertex far outside, so
 // only the crossing point at maxH lies outside. A vertex-only check reports 0 violations; Check must report > 0.
 static object CheckSelfTest(Vector2[] poly)
 {
  var c=new Vector2(poly.Average(p=>p.x),poly.Average(p=>p.y));var far=new Vector2(c.x,poly.Min(p=>p.y)-200);
  var mesh=new Mesh();mesh.vertices=new[]{W(c+new Vector2(-2,0),0),W(c+new Vector2(2,0),0),W(far,2*LowPart)};mesh.triangles=new[]{0,1,2};
  var go=new GameObject("checkSelfTest"){hideFlags=HideFlags.HideAndDontSave};go.AddComponent<MeshFilter>().sharedMesh=mesh;
  var r=Check(go,poly,true,LowPart,"selftest");int vertexOnly=new[]{W(c+new Vector2(-2,0),0),W(c+new Vector2(2,0),0)}.Count(v=>!Inside(Px(v),poly));
  UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mesh);
  if(r.violations==0||vertexOnly!=0)throw new Exception("CHECK_SELFTEST_FAILED");
  return new{crossingViolations=r.violations,vertexOnlyViolations=vertexOnly,status="PASS"};
 }
 // move along dir (1 px steps, up to 40 px), then shrink (0.9, 0.8) and retry; deterministic (no rng). null = no fit
 static FitRec Fit(GameObject g,Vector2[] poly,bool inside,Vector2 dir,float maxH,string rule)
 {
  var p0=g.transform.position;var s0=g.transform.localScale;
  foreach(var sc in new[]{1f,.9f,.8f})for(int off=0;off<=40;off++)
  {
   g.transform.localScale=s0*sc;g.transform.position=new Vector3(p0.x,0,p0.z)+new Vector3(dir.x,0,-dir.y)*off*.08f;
   var b=g.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in g.GetComponentsInChildren<Renderer>())b.Encapsulate(r.bounds);g.transform.position+=Vector3.up*(-b.min.y);
   var f=Check(g,poly,inside,maxH,rule);if(f.lowVerts>0&&f.violations==0){f.movedPx=off;f.scale=sc;return f;}
  }
  g.transform.position=p0;g.transform.localScale=s0;return null;
 }
 static Vector3[] WorldVerts(Renderer r){var mf=r.GetComponent<MeshFilter>();var m=r.transform.localToWorldMatrix;return mf.sharedMesh.vertices.Select(v=>m.MultiplyPoint3x4(v)).ToArray();}
 // max over the polygon boundary (sampled every 2 px) of the distance to the nearest art vertex (px)
 static float Gap(Vector2[] poly,Vector2[] art)
 {
  float worst=0;
  for(int i=0;i<poly.Length;i++){var a=poly[i];var b=poly[(i+1)%poly.Length];int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/2f));
   for(int k=0;k<=n;k++){var p=Vector2.Lerp(a,b,k/(float)n);float d=float.MaxValue;foreach(var q in art){float e=(q-p).sqrMagnitude;if(e<d)d=e;}worst=Mathf.Max(worst,Mathf.Sqrt(d));}}
  return worst;
 }
 static MeshRenderer Add(Transform parent,string name,Mesh mesh,Material m){var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=m;return r;}
 static GameObject Instance(GameObject model,Transform parent,string name,Vector3 pos,float yaw,float scale)
 {
  var g=(GameObject)PrefabUtility.InstantiatePrefab(model,parent);PrefabUtility.UnpackPrefabInstance(g,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  g.name=name;g.transform.position=pos;g.transform.rotation=Quaternion.Euler(0,yaw,0)*g.transform.rotation;g.transform.localScale*=scale;
  var mat=name.StartsWith("Tree")?flatFoliage:flatStone;
  foreach(var r in g.GetComponentsInChildren<Renderer>()){r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();r.shadowCastingMode=ShadowCastingMode.On;}
  // sit on the ground: shift so the lowest point is at y=0
  var b=g.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in g.GetComponentsInChildren<Renderer>())b.Encapsulate(r.bounds);g.transform.position+=Vector3.up*(-b.min.y);
  return g;
 }
 // Tripo stones along the polygon outline, every corner kept inside (shrink/push/drop), combined into one mesh
 static Mesh StoneRing(GameObject stone,Vector2[] poly,Vector2 c,float inset,Func<float,float,float> R,out int dropped,out int placed)
 {
  var temp=(GameObject)PrefabUtility.InstantiatePrefab(stone);PrefabUtility.UnpackPrefabInstance(temp,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  temp.transform.position=Vector3.zero;var mfs=temp.GetComponentsInChildren<MeshFilter>();
  var b=temp.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in temp.GetComponentsInChildren<Renderer>())b.Encapsulate(r.bounds);
  float baseYaw=b.size.z>b.size.x?90:0;float len=Mathf.Max(b.size.x,b.size.z);float lift=-b.min.y;
  var w=poly.Select(p=>W(p)).ToArray();var cw=W(c);float per=0;for(int i=0;i<w.Length;i++)per+=Vector3.Distance(w[i],w[(i+1)%w.Length]);
  int n=Mathf.Max(6,Mathf.RoundToInt(per/(len*1.25f)));float step=per/n;var combine=new List<CombineInstance>();dropped=0;
  for(int k=0;k<n;k++)
  {
   float d=k*step,acc=0;int e=0;while(e<w.Length&&acc+Vector3.Distance(w[e],w[(e+1)%w.Length])<d){acc+=Vector3.Distance(w[e],w[(e+1)%w.Length]);e++;}
   e%=w.Length;var a=w[e];var bb=w[(e+1)%w.Length];var p=Vector3.Lerp(a,bb,Mathf.Clamp01((d-acc)/Mathf.Max(1e-4f,Vector3.Distance(a,bb))));
   var tan=(bb-a).normalized;var inward=new Vector3(-tan.z,0,tan.x);if(Vector3.Dot(inward,cw-p)<0)inward=-inward;
   float yaw=Quaternion.LookRotation(tan).eulerAngles.y+90-baseYaw+R(-14,14);float s0=R(1.0f,1.3f);bool ok=false;
   foreach(var (sc,extra) in new[]{(1f,0f),(.85f,.08f),(.7f,.16f)})
   {
    var pos=p+inward*(inset+extra)+Vector3.up*(lift*s0*sc);var m=Matrix4x4.TRS(pos,Quaternion.Euler(0,yaw,0),Vector3.one*s0*sc);
    var parts=mfs.Select(mf=>new CombineInstance{mesh=mf.sharedMesh,transform=m*mf.transform.localToWorldMatrix}).ToArray();   // temp sits at the origin: keep the FBX root rotation/scale
    if(parts.All(ci=>ci.mesh.vertices.All(v=>Inside(Px(ci.transform.MultiplyPoint3x4(v)),poly)))){combine.AddRange(parts);ok=true;break;}
   }
   if(!ok)dropped++;
  }
  UnityEngine.Object.DestroyImmediate(temp);
  placed=n-dropped;if(dropped>n/4)throw new Exception("TOO_MANY_STONES_DROPPED:"+dropped+"/"+n);
  var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine.ToArray(),true,true);mesh.RecalculateBounds();return mesh;
 }
 // faceted water: fan from the centroid, each triangle subdivided, small deterministic height jitter for per-face normals
 static Mesh Water(Vector2[] poly,Vector2 c,System.Random rng)
 {
  var v=new List<Vector3>();var t=new List<int>();int n=5;var cache=new Dictionary<(int,int,int),int>();
  int Vtx(Vector2 px,(int,int,int) key){if(cache.TryGetValue(key,out var i))return i;var w=W(px,.04f+(float)(rng.NextDouble()*.08-.04));v.Add(w);cache[key]=v.Count-1;return v.Count-1;}
  for(int e=0;e<poly.Length;e++)
  {
   var a=poly[e];var b=poly[(e+1)%poly.Length];
   for(int i=0;i<n;i++)for(int j=0;j<n-i;j++)
   {
    Vector2 P(int ii,int jj){float u=ii/(float)n,vv=jj/(float)n;return c+(a-c)*u+(b-c)*vv;}
    (int,int,int) Key(int ii,int jj){if(ii==0&&jj==0)return(-1,0,0);if(jj==0)return(e,ii,-1);if(ii==0)return((e+1)%poly.Length,jj,-1);return(e,ii,jj);}
    int p0=Vtx(P(i,j),Key(i,j)),p1=Vtx(P(i+1,j),Key(i+1,j)),p2=Vtx(P(i,j+1),Key(i,j+1));t.AddRange(new[]{p0,p2,p1});
    if(i+j<n-1){int p3=Vtx(P(i+1,j+1),Key(i+1,j+1));t.AddRange(new[]{p1,p2,p3});}
   }
  }
  var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();if(m.normals.Count(x=>x.y<0)>m.normals.Length/2){t.Reverse();m.SetTriangles(t,0);m.RecalculateNormals();}
  m.RecalculateBounds();return m;
 }
 // one decor clump: grass blades (green cells) or a few flowers (cream / orange / red / yellow cells) with blades
 static void Clump(List<Vector3> v,List<Vector2> uv,List<int> t,Vector3 c,bool flower,System.Random rng)
 {
  float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
  int blades=flower?2:4;
  for(int i=0;i<blades;i++)
  {
   float a=R(0,Mathf.PI*2),h=R(.16f,.28f),w=R(.035f,.055f);var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-d.z,0,d.x)*w;
   var base0=c+d*R(0,.06f);var cell=Cell(new[]{2,3,5,6}[rng.Next(4)]);int k=v.Count;
   v.Add(base0-side);v.Add(base0+side);v.Add(base0+d*R(.02f,.05f)+Vector3.up*h);uv.Add(cell);uv.Add(cell);uv.Add(cell);t.AddRange(new[]{k,k+1,k+2});
  }
  if(!flower)return;
  int cellF=new[]{23,23,23,28,29,31}[rng.Next(6)];int nf=rng.Next(3,7);
  for(int i=0;i<nf;i++)
  {
   var p=c+new Vector3(R(-.12f,.12f),R(.08f,.14f),R(-.12f,.12f));float s=R(.05f,.075f);var cell=Cell(cellF);int k=v.Count;
   v.Add(p+new Vector3(-s,0,0));v.Add(p+new Vector3(0,0,s));v.Add(p+new Vector3(s,0,0));v.Add(p+new Vector3(0,0,-s));v.Add(p+Vector3.up*s*.7f);
   for(int j=0;j<5;j++)uv.Add(cell);
   t.AddRange(new[]{k,k+4,k+1, k+1,k+4,k+2, k+2,k+4,k+3, k+3,k+4,k});
  }
 }
}
