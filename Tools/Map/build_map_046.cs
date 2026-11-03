using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using Newtonsoft.Json;

// 046 full map scene: Map046_Full from Proto045_Main_s3b (heroes v2, look, camera, combat) with Layout046 and the whole map
// dressed from Props046 (Tools/Map/layout_046.py). Rebuildable: replaces only its own scene and Art folder.
// Run: unity command script run --project-path <root> -- --file Tools/Map/build_map_046.cs --entry Map046Build.Build
//      ... --entry Map046Build.Capture   (overview + game-angle stills into output/map-full-046/)
public static class Map046Build
{
 const string Dir="Assets/DeadSurvivor/Scenes/Map046";
 const string ArtDir=Dir+"/Art";
 const string Target=Dir+"/Map046_Full.unity";
 const string Source="Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main_s3b.unity";
 const string Out="output/map-full-046";
 const string P45="Assets/DeadSurvivor/Scenes/Proto045/";
 const string Mat043="Assets/DeadSurvivor/Art/LowPoly043/Materials/LowPoly043_";
 const string Env="Assets/DeadSurvivor/Art/Environment/";   // 050: official folders (docs/art/asset-catalog.json)
 static readonly Vector2 Origin=new(565,580);
 const float Chunk=240;   // px; combined meshes are grouped per chunk for culling

 class XY{public float x,y;public Vector2 V=>new(x,y);}
 class LObj{public string id,kind;public List<XY> points_px;}
 class LDoc{public List<XY> boundary_px;public List<LObj> objects;}
 class PDoc{public List<float> start_px;public float road_width_px;public Dictionary<string,List<XY>> roads;public List<Dirt> dirt;public List<Prop> props;}
 class Dirt{public float x,y,rx,ry,rot;}
 class Prop{public string model,group;public float x,y,s,r,yaw;public bool occluder,outside;public int v;}

 static Vector3 W(Vector2 px,float y=0)=>new((px.x-Origin.x)*.08f,y,-(px.y-Origin.y)*.08f);
 static Vector2 Px(Vector3 w)=>new(w.x/.08f+Origin.x,-w.z/.08f+Origin.y);
 static Vector2 Cell(int c)=>new(((c%8)+.5f)/8f,(3-(c/8)+.5f)/4f);
 static bool Inside(Vector2 p,Vector2[] poly){bool r=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)r=!r;return r;}
 static float SegDist(Vector2 p,Vector2 a,Vector2 b){var d=b-a;float t=d.sqrMagnitude<1e-9f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}
 static float EdgeDist(Vector2 p,Vector2[] poly){float m=float.MaxValue;for(int i=0;i<poly.Length;i++)m=Mathf.Min(m,SegDist(p,poly[i],poly[(i+1)%poly.Length]));return m;}
 static float LineDist(Vector2 p,Vector2[] line){float m=float.MaxValue;for(int i=0;i+1<line.Length;i++)m=Mathf.Min(m,SegDist(p,line[i],line[i+1]));return m;}
 static Vector2 Centroid(Vector2[] p)=>new(p.Average(q=>q.x),p.Average(q=>q.y));

 // ---- chunked mesh accumulators ----
 class Acc{public List<Vector3> v=new();public List<Vector2> uv=new();public List<int> t=new();public List<CombineInstance> ci=new();}
 class Bucket
 {
  public readonly Dictionary<(string,int,int),Acc> map=new();
  public Acc At(string layer,Vector2 px){float size=layer is "rim" or "outside_tree" or "outside_bush"?Chunk/2:Chunk;var k=(layer,Mathf.FloorToInt(px.x/size),Mathf.FloorToInt(px.y/size));if(!map.TryGetValue(k,out var a))map[k]=a=new Acc();return a;}
 }

 static System.Random rng;
 static float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
 static Material mGround,mSoil,mPath,mCobble,mStoneT,mStoneBox,mBush,mTree,mWater,mDecor;
 static Transform visual;
 static readonly List<Mesh> meshes=new();
 static readonly List<Renderer> blockers=new();
 static readonly Dictionary<string,int> tris=new();
 static GameObject stoneModel,boulderModel,treeModel;

 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++){var s=EditorSceneManager.GetSceneAt(i);if(s.isDirty&&s.path!=Target)throw new Exception("UNSAVED_SCENE:"+s.path);}
  var clock=System.Diagnostics.Stopwatch.StartNew();
  rng=new System.Random(4602);meshes.Clear();blockers.Clear();tris.Clear();
  Directory.CreateDirectory(Out);
  // own outputs only
  if(EditorSceneManager.GetActiveScene().path==Target)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  bool rebuild=File.Exists(Target);   // 047-E11: keep the scene GUID (build settings refer to it); overwrite the content instead of delete + copy
  if(AssetDatabase.IsValidFolder(ArtDir))AssetDatabase.DeleteAsset(ArtDir);
  AssetDatabase.CreateFolder(Dir,"Art");
  AssetDatabase.ImportAsset(Dir+"/Layout046.json",ImportAssetOptions.ForceUpdate);
  var layoutAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Dir+"/Layout046.json");
  var data=JsonUtility.FromJson<DSBlockoutData>(layoutAsset.text);var geometry=data.Geometry(4f);
  var L=JsonConvert.DeserializeObject<LDoc>(layoutAsset.text);
  var P=JsonConvert.DeserializeObject<PDoc>(File.ReadAllText(Dir+"/Props046.json"));
  var boundary=L.boundary_px.Select(p=>p.V).ToArray();
  var obstacles=L.objects.ToDictionary(o=>o.id,o=>o.points_px.Select(p=>p.V).ToArray());
  var roads=P.roads.Values.Select(r=>r.Select(p=>p.V).ToArray()).ToArray();

  mGround=Load<Material>(Mat043+"Ground.mat");mPath=Load<Material>(Mat043+"Path.mat");mCobble=Load<Material>(Mat043+"Stone.mat");
  mSoil=Load<Material>(Env+"Map/Soil045.mat");mStoneBox=Load<Material>(Env+"Map/Stone045.mat");mBush=Load<Material>(Env+"Map/Foliage045.mat");
  mStoneT=Load<Material>(Env+"Map/Stone045s2.mat");mTree=Load<Material>(Env+"Map/Foliage045s2.mat");mWater=Load<Material>(Env+"Map/Water045s2.mat");mDecor=Load<Material>(Env+"Map/Decor045s2.mat");
  stoneModel=Load<GameObject>(P45+"Art_s2/Tripo/stone045.fbx");boulderModel=Load<GameObject>(Env+"Props/boulder045/boulder045.fbx");treeModel=Load<GameObject>(Env+"Props/tree045/tree045.fbx");

  string guidBefore=rebuild?AssetDatabase.AssetPathToGUID(Target):null;
  if(rebuild){File.Copy(Source,Target,true);AssetDatabase.ImportAsset(Target,ImportAssetOptions.ForceUpdate);}
  else if(!AssetDatabase.CopyAsset(Source,Target))throw new Exception("COPY_FAILED");
  if(rebuild&&AssetDatabase.AssetPathToGUID(Target)!=guidBefore)throw new Exception("SCENE_GUID_CHANGED");
  var scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
  var session=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single();var root=session.transform;var vis=session.Visibility;
  // remove the old map dressing (blockout + 037/038/045 layers); keep camera, sun, volume, game, rock holder
  foreach(Transform c in session.StaticGeometry.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(c.gameObject);
  int rocksRemoved=StripTrialRock(session);   // 050
  foreach(var n in new[]{"KeyArt045_VisualOnly","KeyArt045_S2_VisualOnly"}){var t=root.Find(n);if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);}
  var so=new SerializedObject(session);so.FindProperty("Layout").objectReferenceValue=layoutAsset;so.FindProperty("ExpectedDataSha").stringValue=DSMapBlockoutPreview.Hash(layoutAsset.text);
  so.FindProperty("StartCenter").vector2Value=(new Vector2(P.start_px[0],P.start_px[1])-Origin)*4;so.ApplyModifiedPropertiesWithoutUndo();
  visual=new GameObject("Map046_Visual").transform;visual.SetParent(root,false);

  var report=new Dictionary<string,object>();
  var bucket=new Bucket();
  Ground(bucket,boundary);
  foreach(var d in P.dirt)DirtPatch(bucket,d);
  foreach(var r in roads)Road(bucket,r,P.road_width_px);
  var groups=new Dictionary<string,Transform>();
  Transform Group(string id){if(!groups.TryGetValue(id,out var g)){g=new GameObject(id).transform;g.SetParent(visual,false);groups[id]=g;}return g;}
  int stonesPlaced=0,stonesDropped=0;
  foreach(var o in L.objects)
  {
   var poly=obstacles[o.id];var g=Group(o.id);
   switch(o.kind)
   {
    case "keyart_ring":case "ring":{var m=StoneRing(poly,true,.28f,out int p,out int d);stonesPlaced+=p;stonesDropped+=d;Block(Add(g,o.id+"_Stones",m,mStoneT,"stones",false));break;}
    case "arc":{var m=StoneRing(poly,false,.28f,out int p,out int d);stonesPlaced+=p;stonesDropped+=d;Block(Add(g,o.id+"_Stones",m,mStoneT,"stones",false));break;}
    case "row":{Block(Add(g,o.id+"_Stones",RowStones(poly),mStoneT,"stones",false));break;}
    case "lake":case "pond":
    {
     var c=Centroid(poly);var water=Water(poly.Select(p=>p+(c-p).normalized*8f).ToArray(),c);Add(g,o.id+"_Water",water,mWater,"water",false);
     var m=StoneRing(poly,true,.32f,out int p,out int d);stonesPlaced+=p;stonesDropped+=d;Block(Add(g,o.id+"_Stones",m,mStoneT,"stones",false));break;
    }
    case "grove":{Add(g,o.id+"_Floor",Flat(poly.Select(p=>p+(Centroid(poly)-p).normalized*3f).ToArray(),.012f,2),mGround,"ground",false);break;}
    case "ruin":{Block(Add(g,o.id+"_Wall",RuinWall(poly),mStoneBox,"ruins",true));break;}
    case "pillar":{Block(Add(g,o.id+"_Pillar",Pillar(poly),mStoneBox,"ruins",true));break;}
   }
  }
  // props (trees, boulders, bush domes); outside rows 2+ are combined per chunk
  int outsideCombined=0;
  foreach(var p in P.props)
  {
   var pos=W(new Vector2(p.x,p.y));
   if(p.model=="bush")
   {
    var mesh=Bush(p.r,p.v);
    if(p.outside&&!p.occluder){var a=bucket.At("outside_bush",new Vector2(p.x,p.y));a.ci.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.Translate(pos)});outsideCombined++;continue;}
    var r=Add(p.outside?Group("Outside"):Group(p.group),"Bush",mesh,mBush,"bushes",true);r.transform.position=pos;if(p.occluder)Block(r);continue;
   }
   var model=p.model=="tree045"?treeModel:boulderModel;var mat=p.model=="tree045"?mTree:mStoneT;string layer=p.model=="tree045"?"trees":"boulders";
   if(p.outside&&!p.occluder)
   {
    var tmp=Instance(model,Vector3.zero,p.yaw,p.s);var lift=Lift(tmp);var m=Matrix4x4.Translate(pos+Vector3.up*lift);
    var a=bucket.At("outside_tree",new Vector2(p.x,p.y));foreach(var mf in tmp.GetComponentsInChildren<MeshFilter>())a.ci.Add(new CombineInstance{mesh=mf.sharedMesh,transform=m*mf.transform.localToWorldMatrix});
    UnityEngine.Object.DestroyImmediate(tmp);outsideCombined++;continue;
   }
   var go=Instance(model,pos,p.yaw,p.s);go.name=p.model.Replace("045","");go.transform.SetParent(p.outside?Group("Outside"):Group(p.group),true);go.transform.position+=Vector3.up*Lift(go);
   foreach(var r in go.GetComponentsInChildren<Renderer>()){r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();r.shadowCastingMode=ShadowCastingMode.On;Count(layer,r);if(p.occluder)Block(r);}
  }
  RimStones(bucket,boundary);
  Decor(bucket,boundary,obstacles.Values.ToArray(),roads);
  // flush chunk buckets
  foreach(var kv in bucket.map)
  {
   var (layer,cx,cy)=kv.Key;var a=kv.Value;Mesh m;
   if(a.ci.Count>0){m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(a.ci.ToArray(),true,true);m.RecalculateBounds();}
   else{if(a.t.Count==0)continue;m=MakeMesh(a.v,a.uv,a.t);if(layer=="decor"){m.SetNormals(a.v.Select(_=>Vector3.up).ToList());}}
   var mat=layer switch{"pebbles"=>mPath,"ground"=>mGround,"dirt"=>mSoil,"road_bed"=>mPath,"cobbles"=>mPath,"rim"=>mStoneT,"decor"=>mDecor,"outside_tree"=>mTree,"outside_bush"=>mBush,_=>throw new Exception("LAYER:"+layer)};
   bool shadow=layer is "outside_tree" or "outside_bush";
   Add(Group("Chunks_"+layer),$"{layer}_{cx}_{cy}",m,mat,layer,shadow);
  }
  // 047: re-attach what later builders added (this builder starts from a fresh copy of the s3b scene): monster actor set,
  // combat effects (DSMapFx + Fx047 materials) and day/night. Missing assets are skipped (first build).
  var set046=AssetDatabase.LoadAssetAtPath<DSMapActorSet>("Assets/DeadSurvivor/Art/Characters/ActorSet_Main.asset");
  if(set046){var pso=new SerializedObject(session.GetComponent<DSMapPresentation>());pso.FindProperty("Actors").objectReferenceValue=set046;pso.ApplyModifiedPropertiesWithoutUndo();}
  var fxSolid=AssetDatabase.LoadAssetAtPath<Material>("Assets/DeadSurvivor/Art/Fx047/Fx047_Solid.mat");
  if(fxSolid){var fx=session.GetComponent<DSMapFx>()??session.gameObject.AddComponent<DSMapFx>();var fso=new SerializedObject(fx);fso.FindProperty("Solid").objectReferenceValue=fxSolid;
   fso.FindProperty("Fade").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>("Assets/DeadSurvivor/Art/Fx047/Fx047_Fade.mat");fso.FindProperty("Particles").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>("Assets/DeadSurvivor/Art/Fx047/Fx047_Particles.mat");fso.ApplyModifiedPropertiesWithoutUndo();}
  { var dn=session.GetComponent<DSDayNight>()??session.gameObject.AddComponent<DSDayNight>();var dso=new SerializedObject(dn);
    dso.FindProperty("Sun").objectReferenceValue=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include).FirstOrDefault(l=>l.type==LightType.Directional);
    dso.FindProperty("Look").objectReferenceValue=UnityEngine.Object.FindObjectsByType<DSLookGlobals>(FindObjectsInactive.Include).FirstOrDefault();dso.ApplyModifiedPropertiesWithoutUndo(); }
  report["reattached"]=new{actors=set046?set046.name:null,fx=fxSolid!=null,dayNight=true,trialRocksRemoved=rocksRemoved};
  // occluders
  var vso=new SerializedObject(vis);var bp=vso.FindProperty("Blockers");var bl=blockers.Where(r=>r).Distinct().ToArray();bp.arraySize=bl.Length;for(int i=0;i<bl.Length;i++)bp.GetArrayElementAtIndex(i).objectReferenceValue=bl[i];vso.ApplyModifiedPropertiesWithoutUndo();
  if(UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length!=0)throw new Exception("COLLIDER_PRESENT");
  // walkable-space check: low vertices of every prop/obstacle visual must not stand in walkable space (inside boundary, outside all obstacles)
  var obsArr=obstacles.Values.ToArray();int intrude=0;var intrudeGroups=new HashSet<string>();
  foreach(var g in groups.Where(kv=>kv.Key.StartsWith("OB-")))foreach(var mf in g.Value.GetComponentsInChildren<MeshFilter>())
  {
   if(mf.name.EndsWith("_Floor")||mf.name.EndsWith("_Water"))continue;var m=mf.transform.localToWorldMatrix;var own=obstacles[g.Key];
   foreach(var v in mf.sharedMesh.vertices){var w=m.MultiplyPoint3x4(v);if(w.y>1f)continue;var q=Px(w);if(Inside(q,own))continue;if(Inside(q,boundary)&&EdgeDist(q,own)>1.5f&&!obsArr.Any(o=>Inside(q,o))){intrude++;intrudeGroups.Add(g.Key);}}
  }
  AssetDatabase.CreateAsset(meshes[0],ArtDir+"/Meshes046.asset");foreach(var m in meshes.Skip(1))AssetDatabase.AddObjectToAsset(m,meshes[0]);
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
  foreach(var o in AssetDatabase.LoadAllAssetsAtPath(ArtDir+"/Meshes046.asset"))if(o)AssetDatabase.SaveAssetIfDirty(o);
  // 046-12 lesson: reload and check the references survived the save
  scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);var s2=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single();visual=s2.transform.Find("Map046_Visual");
  if(set046&&s2.GetComponent<DSMapPresentation>().Actors!=set046)throw new Exception("ACTORS_NOT_SAVED");
  if(fxSolid&&(!s2.GetComponent<DSMapFx>()||s2.GetComponent<DSMapFx>().Solid!=fxSolid))throw new Exception("FX_NOT_SAVED");
  if(!s2.GetComponent<DSDayNight>()||!s2.GetComponent<DSDayNight>().Sun)throw new Exception("DAYNIGHT_NOT_SAVED");
  var all=visual.GetComponentsInChildren<Renderer>(true);
  report["status"]="BUILT";report["target"]=Target;report["seconds"]=Math.Round(clock.Elapsed.TotalSeconds,1);
  report["obstacles"]=L.objects.Count;report["renderers"]=all.Length;report["triangles_by_layer"]=tris;report["triangles_total"]=tris.Values.Sum();
  report["blockers"]=bl.Length;report["meshes"]=meshes.Count;report["stones"]=new{placed=stonesPlaced,dropped=stonesDropped};report["outside_combined"]=outsideCombined;
  report["walkable_intrusion_vertices"]=intrude;report["intrusion_groups"]=intrudeGroups.OrderBy(x=>x).ToArray();
  File.WriteAllText(Out+"/build.json",JsonConvert.SerializeObject(report,Formatting.Indented));
  return report;
 }

 // 050 (docs/design/asset-standard-050.md, design review M1/M2): the OB-001 rock under the rock holder is the 012 Tripo
 // transfer-test rock from Art/Tests. Nobody sees it (its renderer has been off since 036/037; the obstacle is dressed by the
 // keyart_ring stones), but it kept the build scene depending on a trial folder. The instance goes, the holder stays
 // (DSMapSession requires it). This builder owns the scene, so the step lives here and Build() runs it too.
 static int StripTrialRock(DSMapSession session)
 {
  int n=0;foreach(Transform c in session.RockHolder.Cast<Transform>().ToArray()){UnityEngine.Object.DestroyImmediate(c.gameObject);n++;}
  return n;
 }
 // Apply only the 050 steps to the saved scene (no full rebuild): strip the trial rock, point Actors at the official set.
 // Run: unity command script run --project-path <root> -- --file Tools/Map/build_map_046.cs --entry Map046Build.Apply050
 public static object Apply050()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++){var s=EditorSceneManager.GetSceneAt(i);if(s.isDirty)throw new Exception("UNSAVED_SCENE:"+s.path);}
  const string SetPath="Assets/DeadSurvivor/Art/Characters/ActorSet_Main.asset";
  var set=AssetDatabase.LoadAssetAtPath<DSMapActorSet>(SetPath)??throw new Exception("MISSING:"+SetPath);
  var scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
  var session=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single();
  int removed=StripTrialRock(session);
  var vso=new SerializedObject(session.Visibility);var bp=vso.FindProperty("Blockers");int nulls=0;
  for(int i=bp.arraySize-1;i>=0;i--)if(!bp.GetArrayElementAtIndex(i).objectReferenceValue){bp.DeleteArrayElementAtIndex(i);nulls++;}
  vso.ApplyModifiedPropertiesWithoutUndo();
  var pso=new SerializedObject(session.GetComponent<DSMapPresentation>());pso.FindProperty("Actors").objectReferenceValue=set;pso.ApplyModifiedPropertiesWithoutUndo();
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
  // 046-12: reopen and verify what was saved
  EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
  var check=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single();
  if(!check.RockHolder||check.RockHolder.childCount!=0)throw new Exception("ROCK_NOT_STRIPPED");
  if(check.GetComponent<DSMapPresentation>().Actors!=set)throw new Exception("ACTORS_NOT_SAVED");
  return new{removed,blockerNullsRemoved=nulls,actors=SetPath};
 }
 static T Load<T>(string p)where T:UnityEngine.Object{var a=AssetDatabase.LoadAssetAtPath<T>(p);if(!a)throw new Exception("MISSING:"+p);return a;}
 static void Count(string layer,Renderer r){var mf=r.GetComponent<MeshFilter>();int n=mf&&mf.sharedMesh?mf.sharedMesh.triangles.Length/3:0;tris[layer]=(tris.TryGetValue(layer,out var x)?x:0)+n;}
 static void Block(Renderer r){if(r)blockers.Add(r);}
 static MeshRenderer Add(Transform parent,string name,Mesh mesh,Material mat,string layer,bool shadow)
 {
  mesh.name=name;meshes.Add(mesh);var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
  var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;Count(layer,r);go.isStatic=false;return r;
 }
 static Mesh MakeMesh(List<Vector3> v,List<Vector2> uv,List<int> t)
 {
  var m=new Mesh{indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.SetColors(v.Select(_=>Color.white).ToList());m.RecalculateNormals();m.RecalculateBounds();return m;
 }
 static GameObject Instance(GameObject model,Vector3 pos,float yaw,float scale)
 {
  var g=(GameObject)PrefabUtility.InstantiatePrefab(model);PrefabUtility.UnpackPrefabInstance(g,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  g.transform.position=pos;g.transform.rotation=Quaternion.Euler(0,yaw,0)*g.transform.rotation;g.transform.localScale*=scale;return g;
 }
 static float Lift(GameObject g){var rs=g.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return -b.min.y;}
 static void Tri(Acc a,Vector3 p0,Vector3 p1,Vector3 p2,int cell){int i=a.v.Count;var c=Cell(cell);a.v.Add(p0);a.v.Add(p1);a.v.Add(p2);a.uv.Add(c);a.uv.Add(c);a.uv.Add(c);
  // up-facing winding for flat ground pieces
  if(Vector3.Cross(p1-p0,p2-p0).y>0)a.t.AddRange(new[]{i,i+1,i+2});else a.t.AddRange(new[]{i,i+2,i+1});}

 // ---- ground: jittered triangle grid, palette patches; darker outside the boundary ----
 static void Ground(Bucket b,Vector2[] boundary)
 {
  float x0=boundary.Min(p=>p.x)-280,x1=boundary.Max(p=>p.x)+280,y0=boundary.Min(p=>p.y)-280,y1=boundary.Max(p=>p.y)+280,G=30;
  int nx=Mathf.CeilToInt((x1-x0)/G),ny=Mathf.CeilToInt((y1-y0)/G);var V=new Vector3[nx+1,ny+1];
  for(int i=0;i<=nx;i++)for(int j=0;j<=ny;j++){bool edge=i==0||j==0||i==nx||j==ny;var p=new Vector2(x0+i*G+(edge?0:R(-7,7)),y0+j*G+(edge?0:R(-7,7)));V[i,j]=W(p,R(0,.004f));}
  float N(Vector2 p)=>Mathf.Sin(p.x*.012f+1.3f)+Mathf.Sin(p.y*.010f+.4f)+.8f*Mathf.Sin((p.x+p.y)*.017f+2.1f)+.6f*Mathf.Sin((p.x-p.y)*.023f);
  for(int i=0;i<nx;i++)for(int j=0;j<ny;j++)
  {
   foreach(var (a,c,d) in new[]{(V[i,j],V[i+1,j],V[i+1,j+1]),(V[i,j],V[i+1,j+1],V[i,j+1])})
   {
    var cen=Px((a+c+d)/3);bool inside=Inside(cen,boundary);float n=N(cen)+R(-.35f,.35f);
    int cell=inside?3:(n>.8f?2:1);
    Tri(b.At("ground",cen),a,c,d,cell);
   }
  }
 }
 static Vector2[] Blob(Vector2 c,float rx,float ry,float rot,int n=18)
 {
  var pts=new Vector2[n];float ca=Mathf.Cos(rot*Mathf.Deg2Rad),sa=Mathf.Sin(rot*Mathf.Deg2Rad);float f1=R(0,6.3f),f2=R(0,6.3f),f3=R(0,6.3f);
  for(int i=0;i<n;i++){float a=Mathf.PI*2*i/n,k=1+.15f*Mathf.Sin(2*a+f1)+.09f*Mathf.Sin(3*a+f2)+.05f*Mathf.Sin(5*a+f3)+R(-.03f,.03f);var q=new Vector2(Mathf.Cos(a)*rx*k,Mathf.Sin(a)*ry*k);pts[i]=c+new Vector2(q.x*ca-q.y*sa,q.x*sa+q.y*ca);}
  return pts;
 }
 static void FlatInto(Acc a,Vector2[] poly,float y,int cell){var c=Centroid(poly);for(int i=0;i<poly.Length;i++)Tri(a,W(c,y),W(poly[(i+1)%poly.Length],y),W(poly[i],y),cell);}
 static Mesh Flat(Vector2[] poly,float y,int cell){var a=new Acc();FlatInto(a,poly,y,cell);return MakeMesh(a.v,a.uv,a.t);}
 static void DirtPatch(Bucket b,Dirt d)
 {
  var c=new Vector2(d.x,d.y);var blob=Blob(c,d.rx,d.ry,d.rot);var acc=b.At("dirt",c);
  FlatInto(acc,blob.Select(p=>c+(p-c)*1.14f).ToArray(),.016f,11);FlatInto(acc,blob,.017f,13);
 }

 // ---- roads: soil bed + cobbles (palette stone cells), chunked ----
 static void Road(Bucket b,Vector2[] line,float width)
 {
  float half=width/2;
  for(int i=0;i+1<line.Length;i++)
  {
   var a=line[i];var c=line[i+1];var dir=(c-a).normalized;var n=new Vector2(-dir.y,dir.x);
   var pa=i>0?(line[i+1]-line[i-1]).normalized:dir;var pc=i+2<line.Length?(line[i+2]-line[i]).normalized:dir;var na=new Vector2(-pa.y,pa.x);var nc=new Vector2(-pc.y,pc.x);
   var acc=b.At("road_bed",(a+c)/2);
   foreach(var (w,y,cell) in new[]{(half+3.5f,.018f,9),(half,.019f,10)})
   {
    Vector3 A=W(a+na*w,y),B=W(a-na*w,y),C=W(c-nc*w,y),D=W(c+nc*w,y);
    Tri(acc,A,B,C,cell);Tri(acc,A,C,D,cell);
   }
  }
  // cobbles by arc length
  float total=0;var cum=new float[line.Length];for(int i=1;i<line.Length;i++){total+=Vector2.Distance(line[i-1],line[i]);cum[i]=total;}
  int seg=0;
  for(float s=1;s<total;s+=4.4f)
  {
   while(seg+1<line.Length-1&&cum[seg+1]<s)seg++;
   var a=line[seg];var c=line[seg+1];var f=(s-cum[seg])/Mathf.Max(1e-4f,cum[seg+1]-cum[seg]);var p=Vector2.Lerp(a,c,f);var dir=(c-a).normalized;var n=new Vector2(-dir.y,dir.x);
   foreach(float lane in new[]{-6.6f,-2.2f,2.2f,6.6f})
   {
    if(rng.NextDouble()<.06)continue;
    var q=p+n*(lane+R(-.9f,.9f))+dir*R(-1.1f,1.1f);
    Cobble(b.At("cobbles",q),W(q,.019f),R(.15f,.2f),R(.025f,.05f),R(0,360),new[]{20,21,21,22,22,23}[rng.Next(6)]);
   }
  }
 }
 static void Cobble(Acc a,Vector3 c,float r,float h,float yaw,int cell)
 {
  int n=7;var bot=new Vector3[n];var top=new Vector3[n];
  for(int i=0;i<n;i++){float t=(yaw+i*360f/n)*Mathf.Deg2Rad;float k=R(.82f,1.08f);var d=new Vector3(Mathf.Cos(t),0,Mathf.Sin(t))*r*k;bot[i]=c+d;top[i]=c+d*.78f+Vector3.up*h;}
  var ct=c+Vector3.up*h*1.08f;
  for(int i=0;i<n;i++){int j=(i+1)%n;Tri(a,ct,top[i],top[j],cell);Quad(a,bot[i],bot[j],top[j],top[i],cell,c);}
 }
 // side quad facing away from the prism axis through `centre`
 static void Quad(Acc a,Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,int cell,Vector3 centre){int i=a.v.Count;var c=Cell(cell);a.v.AddRange(new[]{p0,p1,p2,p3});a.uv.AddRange(new[]{c,c,c,c});
  var mid=(p0+p1+p2+p3)/4;var outward=new Vector3(mid.x-centre.x,0,mid.z-centre.z);
  if(Vector3.Dot(Vector3.Cross(p1-p0,p2-p0),outward)>0)a.t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});else a.t.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});}

 // ---- obstacle visuals ----
 // Tripo stones along the outline (closed) or the open polyline (arc), every vertex inside the polygon, combined
 static Mesh StoneRing(Vector2[] poly,bool closed,float inset,out int placed,out int dropped)
 {
  var temp=Instance(stoneModel,Vector3.zero,0,1);temp.transform.position=Vector3.zero;var mfs=temp.GetComponentsInChildren<MeshFilter>();
  var bb=temp.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in temp.GetComponentsInChildren<Renderer>())bb.Encapsulate(r.bounds);
  float baseYaw=bb.size.z>bb.size.x?90:0;float len=Mathf.Max(bb.size.x,bb.size.z);float lift=-bb.min.y;
  var w=poly.Select(p=>W(p)).ToArray();var cw=W(Centroid(poly));int segs=closed?w.Length:w.Length-1;
  float per=0;for(int i=0;i<segs;i++)per+=Vector3.Distance(w[i],w[(i+1)%w.Length]);
  int n=Mathf.Max(5,Mathf.RoundToInt(per/(len*1.2f)));float step=per/(closed?n:n-1);var combine=new List<CombineInstance>();dropped=0;
  for(int k=0;k<n;k++)
  {
   float d=Mathf.Min(k*step,per-1e-3f),acc=0;int e=0;while(e<segs-1&&acc+Vector3.Distance(w[e],w[(e+1)%w.Length])<d){acc+=Vector3.Distance(w[e],w[(e+1)%w.Length]);e++;}
   var a=w[e];var b2=w[(e+1)%w.Length];var p=Vector3.Lerp(a,b2,Mathf.Clamp01((d-acc)/Mathf.Max(1e-4f,Vector3.Distance(a,b2))));
   var tan=(b2-a).normalized;var inward=new Vector3(-tan.z,0,tan.x);if(Vector3.Dot(inward,cw-p)<0)inward=-inward;
   float yaw=Quaternion.LookRotation(tan).eulerAngles.y+90-baseYaw+R(-14,14);float s0=R(1.0f,1.3f);bool ok=false;
   foreach(var (sc,extra) in new[]{(1f,0f),(.85f,.08f),(.7f,.16f)})
   {
    var pos=p+inward*(inset+extra)+Vector3.up*(lift*s0*sc);var m=Matrix4x4.TRS(pos,Quaternion.Euler(0,yaw,0),Vector3.one*s0*sc);
    var parts=mfs.Select(mf=>new CombineInstance{mesh=mf.sharedMesh,transform=m*mf.transform.localToWorldMatrix}).ToArray();
    if(parts.All(ci=>ci.mesh.vertices.All(v=>Inside(Px(ci.transform.MultiplyPoint3x4(v)),poly)))){combine.AddRange(parts);ok=true;break;}
   }
   if(!ok)dropped++;
  }
  UnityEngine.Object.DestroyImmediate(temp);placed=n-dropped;
  var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine.ToArray(),true,true);mesh.RecalculateBounds();return mesh;
 }
 // fallen wall: big Tripo stones along the long axis of a capsule
 static Mesh RowStones(Vector2[] poly)
 {
  var c=Centroid(poly);float best=0;Vector2 axis=Vector2.right;
  for(int i=0;i<poly.Length;i++)for(int j=i+1;j<poly.Length;j++){float d=Vector2.Distance(poly[i],poly[j]);if(d>best){best=d;axis=(poly[j]-poly[i]).normalized;}}
  float wpx=poly.Max(p=>Mathf.Abs(Vector2.Dot(p-c,new Vector2(-axis.y,axis.x))))*2;
  var temp=Instance(stoneModel,Vector3.zero,0,1);var mfs=temp.GetComponentsInChildren<MeshFilter>();var bb=temp.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in temp.GetComponentsInChildren<Renderer>())bb.Encapsulate(r.bounds);
  float baseYaw=bb.size.z>bb.size.x?90:0;float lift=-bb.min.y;var combine=new List<CombineInstance>();
  float yawAxis=Mathf.Atan2(axis.x,-axis.y)*Mathf.Rad2Deg;   // px y is -z
  float half=best/2-wpx*.35f;
  for(float s=-half;s<=half;s+=R(5.2f,6.6f))
  {
   var p=c+axis*s+new Vector2(-axis.y,axis.x)*R(-1.2f,1.2f);
   foreach(var sc in new[]{R(1.7f,2.3f),1.4f,1.1f})
   {
    var m=Matrix4x4.TRS(W(p)+Vector3.up*lift*sc,Quaternion.Euler(0,yawAxis+90-baseYaw+R(-20,20),0),Vector3.one*sc);
    var parts=mfs.Select(mf=>new CombineInstance{mesh=mf.sharedMesh,transform=m*mf.transform.localToWorldMatrix}).ToArray();
    if(parts.All(ci=>ci.mesh.vertices.All(v=>Inside(Px(ci.transform.MultiplyPoint3x4(v)),poly)))){combine.AddRange(parts);break;}
   }
  }
  UnityEngine.Object.DestroyImmediate(temp);
  var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine.ToArray(),true,true);mesh.RecalculateBounds();return mesh;
 }
 static void Box(Acc a,Vector3 c,Quaternion r,float len,float wid,float h,int cell,float ib=.8f)
 {
  Vector3 P(float x,float y,float z)=>c+r*new Vector3(x,y,z);
  var bot=new[]{P(-wid/2,0,-len/2),P(wid/2,0,-len/2),P(wid/2,0,len/2),P(-wid/2,0,len/2)};
  var top=new[]{P(-wid/2*ib,h,-len/2*ib),P(wid/2*ib,h,-len/2*ib),P(wid/2*ib,h,len/2*ib),P(-wid/2*ib,h,len/2*ib)};
  void Q(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3){int i=a.v.Count;var cc=Cell(cell);a.v.AddRange(new[]{p0,p1,p2,p3});a.uv.AddRange(new[]{cc,cc,cc,cc});a.t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
  Q(top[0],top[3],top[2],top[1]);for(int i=0;i<4;i++){int j=(i+1)%4;Q(bot[i],top[i],top[j],bot[j]);}
 }
 static bool BoxInside(Vector3 c,Quaternion r,float len,float wid,Vector2[] poly)=>new[]{new Vector3(-wid/2,0,-len/2),new Vector3(wid/2,0,-len/2),new Vector3(wid/2,0,len/2),new Vector3(-wid/2,0,len/2)}.All(x=>Inside(Px(c+r*x),poly));
 // broken ruin wall along the middle line of a thick arc (outer points then inner points reversed)
 static Mesh RuinWall(Vector2[] poly)
 {
  int n=poly.Length/2;var mid=Enumerable.Range(0,n).Select(i=>(poly[i]+poly[2*n-1-i])/2).ToArray();var a=new Acc();
  float thick=Vector2.Distance(poly[0],poly[2*n-1]);
  float total=0;for(int i=1;i<n;i++)total+=Vector2.Distance(mid[i-1],mid[i]);
  for(float s=3;s<total-3;s+=R(6.2f,7.2f))
  {
   float acc=0;int e=0;while(e<n-2&&acc+Vector2.Distance(mid[e],mid[e+1])<s){acc+=Vector2.Distance(mid[e],mid[e+1]);e++;}
   var p=Vector2.Lerp(mid[e],mid[e+1],Mathf.Clamp01((s-acc)/Mathf.Max(1e-4f,Vector2.Distance(mid[e],mid[e+1]))));var dir=(mid[e+1]-mid[e]).normalized;var nrm=new Vector2(-dir.y,dir.x);
   var rot=Quaternion.LookRotation(new Vector3(dir.x,0,-dir.y))*Quaternion.Euler(0,R(-6,6),0);
   foreach(float off in new[]{-thick/4,thick/4})
   {
    var c=W(p+nrm*off);float len=R(.46f,.56f),wid=thick*.08f*.42f;float h1=R(.3f,.46f);
    if(!BoxInside(c,rot,len,wid,poly))continue;
    int cell=new[]{20,21,21,22}[rng.Next(4)];Box(a,c,rot,len,wid,h1,cell,.86f);
    if(rng.NextDouble()<.62){float h2=R(.22f,.38f);var c2=c+Vector3.up*h1+rot*new Vector3(R(-.04f,.04f),0,R(-.06f,.06f));Box(a,c2,rot*Quaternion.Euler(0,R(-8,8),0),len*.86f,wid*.86f,h2,new[]{20,21,22}[rng.Next(3)],.84f);
     if(rng.NextDouble()<.3)Box(a,c2+Vector3.up*h2,rot,len*.6f,wid*.7f,R(.15f,.25f),22,.8f);}
   }
  }
  return MakeMesh(a.v,a.uv,a.t);
 }
 static Mesh Pillar(Vector2[] poly)
 {
  var c=W(Centroid(poly));var a=new Acc();var rot=Quaternion.Euler(0,R(0,90),0);float y=0;
  foreach(var (s,h) in new[]{(.8f,.36f),(.66f,R(.42f,.55f)),(.58f,R(.25f,.4f))})
  {
   if(y>0&&rng.NextDouble()<.12)break;
   Box(a,c+Vector3.up*y,rot*Quaternion.Euler(0,R(-10,10),0),s,s,h,new[]{20,21,22}[rng.Next(3)],.9f);y+=h;
  }
  return MakeMesh(a.v,a.uv,a.t);
 }
 // faceted water: fan from the centroid, subdivided, small height jitter for per-face normals
 static Mesh Water(Vector2[] poly0,Vector2 c)
 {
  // outline subdivided to <= 12 px edges, then rings scaled toward the centre; jittered inner rings, faceted heights
  var poly=new List<Vector2>();for(int e=0;e<poly0.Length;e++){var a0=poly0[e];var b0=poly0[(e+1)%poly0.Length];int k=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a0,b0)/12f));for(int i=0;i<k;i++)poly.Add(Vector2.Lerp(a0,b0,i/(float)k));}
  float rmax=poly.Max(p=>Vector2.Distance(p,c));int rings=Mathf.Max(2,Mathf.RoundToInt(rmax/16f));int n=poly.Count;
  // ring j keeps ~k of the outline vertices so facets stay even toward the centre; rings stitched by outline parameter
  var v=new List<Vector3>();var t=new List<int>();var ring=new List<int[]>();
  for(int j=0;j<rings;j++)
  {
   float k=1-j/(float)rings;int nj=j==0?n:Mathf.Max(5,Mathf.RoundToInt(n*k));var ids=new int[nj];
   for(int i=0;i<nj;i++){float u=i*n/(float)nj;int i0=Mathf.FloorToInt(u)%n;var q0=Vector2.Lerp(poly[i0],poly[(i0+1)%n],u-Mathf.Floor(u));var q=c+(q0-c)*k;if(j>0)q+=new Vector2(R(-2.5f,2.5f),R(-2.5f,2.5f))*k;v.Add(W(q,.04f+R(-.03f,.03f)));ids[i]=v.Count-1;}
   ring.Add(ids);
  }
  v.Add(W(c,.04f+R(-.03f,.03f)));ring.Add(new[]{v.Count-1});
  void T(int x,int y,int z){if(Vector3.Cross(v[y]-v[x],v[z]-v[x]).y>0)t.AddRange(new[]{x,y,z});else t.AddRange(new[]{x,z,y});}
  for(int j=0;j<rings;j++)
  {
   var A=ring[j];var B=ring[j+1];int na=A.Length,nb=B.Length,ia=0,ib=0;
   if(nb==1){for(int i=0;i<na;i++)T(A[i],A[(i+1)%na],B[0]);continue;}
   while(ia<na||ib<nb)
   {
    if(ib>=nb||(ia<na&&(ia+1)/(float)na<(ib+1)/(float)nb)){T(A[ia%na],A[(ia+1)%na],B[ib%nb]);ia++;}
    else{T(A[ia%na],B[(ib+1)%nb],B[ib%nb]);ib++;}
   }
  }
  var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
 }
 static Mesh WaterFan(Vector2[] poly,Vector2 c)
 {
  var v=new List<Vector3>();var t=new List<int>();int n=5;var cache=new Dictionary<(int,int,int),int>();
  int Vtx(Vector2 px,(int,int,int) key){if(cache.TryGetValue(key,out var i))return i;v.Add(W(px,.04f+R(-.04f,.04f)));cache[key]=v.Count-1;return v.Count-1;}
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
 // faceted bush dome (as 045 stage 1)
 // 047 variants: 0 green, 1 dark green, 2 flowering (white/orange/yellow dots on top), 3 tall (two stacked domes)
 static Mesh Bush(float r,int variant)
 {
  if(variant==3)
  {
   var low=Bush(r,0);var top=Bush(r*.68f,1);var ci=new[]{new CombineInstance{mesh=low,transform=Matrix4x4.identity},new CombineInstance{mesh=top,transform=Matrix4x4.Translate(new Vector3(R(-.08f,.08f)*r,r*.95f,R(-.08f,.08f)*r))}};
   var tall=new Mesh();tall.CombineMeshes(ci,true,true);tall.RecalculateBounds();UnityEngine.Object.DestroyImmediate(low);UnityEngine.Object.DestroyImmediate(top);return tall;
  }
  int cA=variant==1?2:5,cB=variant==1?3:6;
  float J(float x)=>R(-x,x);float g=(1+Mathf.Sqrt(5))/2;
  var p=new List<Vector3>{new(-1,g,0),new(1,g,0),new(-1,-g,0),new(1,-g,0),new(0,-1,g),new(0,1,g),new(0,-1,-g),new(0,1,-g),new(g,0,-1),new(g,0,1),new(-g,0,-1),new(-g,0,1)};
  var f=new List<int[]>{new[]{0,11,5},new[]{0,5,1},new[]{0,1,7},new[]{0,7,10},new[]{0,10,11},new[]{1,5,9},new[]{5,11,4},new[]{11,10,2},new[]{10,7,6},new[]{7,1,8},new[]{3,9,4},new[]{3,4,2},new[]{3,2,6},new[]{3,6,8},new[]{3,8,9},new[]{4,9,5},new[]{2,4,11},new[]{6,2,10},new[]{8,6,7},new[]{9,8,1}};
  var mid=new Dictionary<long,int>();
  int M(int a,int b){long k=a<b?((long)a<<32)|(uint)b:((long)b<<32)|(uint)a;if(mid.TryGetValue(k,out var i))return i;p.Add((p[a]+p[b])/2);return mid[k]=p.Count-1;}
  var f2=new List<int[]>();foreach(var x in f){int a=M(x[0],x[1]),b=M(x[1],x[2]),c=M(x[2],x[0]);f2.AddRange(new[]{new[]{x[0],a,c},new[]{x[1],b,a},new[]{x[2],c,b},new[]{a,b,c}});}
  var q=p.Select(x=>x.normalized*(1+J(.07f))).Select(x=>new Vector3(x.x*r,(x.y*.85f+.78f)*r,x.z*r)).ToList();
  var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();int shade=rng.Next(3);
  foreach(var x in f2){var cell=Cell(x[0]%5==shade?cB:cA);int i=v.Count;v.Add(q[x[0]]);v.Add(q[x[1]]);v.Add(q[x[2]]);uv.Add(cell);uv.Add(cell);uv.Add(cell);t.AddRange(new[]{i,i+1,i+2});}
  if(variant==2)
  {
   int fc=new[]{23,28,31}[rng.Next(3)];var top=q.Where(x=>x.y>r*1.1f).ToList();int nf=Mathf.Min(top.Count,7+rng.Next(5));
   for(int k=0;k<nf;k++){var p0=top[rng.Next(top.Count)]*1.03f;float s=r*R(.09f,.13f);var cell=Cell(fc);int i=v.Count;
    v.Add(p0+new Vector3(-s,0,0));v.Add(p0+new Vector3(0,0,s));v.Add(p0+new Vector3(s,0,0));v.Add(p0+new Vector3(0,0,-s));v.Add(p0+Vector3.up*s*.8f);for(int j=0;j<5;j++)uv.Add(cell);
    t.AddRange(new[]{i,i+4,i+1,i+1,i+4,i+2,i+2,i+4,i+3,i+3,i+4,i});}
  }
  var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.SetColors(v.Select(_=>Color.white).ToList());m.RecalculateNormals();
  if(Vector3.Dot(m.normals[0],(v[0]+v[1]+v[2])/3-new Vector3(0,.78f*r,0))<0){t.Reverse();m.SetTriangles(t,0);m.RecalculateNormals();}
  m.RecalculateBounds();return m;
 }
 // stone rim just outside the boundary (every vertex outside), chunked
 static void RimStones(Bucket b,Vector2[] boundary)
 {
  var temp=Instance(stoneModel,Vector3.zero,0,1);var mfs=temp.GetComponentsInChildren<MeshFilter>();var bb=temp.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in temp.GetComponentsInChildren<Renderer>())bb.Encapsulate(r.bounds);
  float baseYaw=bb.size.z>bb.size.x?90:0;float lift=-bb.min.y;var c=Centroid(boundary);
  for(int e=0;e<boundary.Length;e++)
  {
   var a=boundary[e];var d=boundary[(e+1)%boundary.Length];float L=Vector2.Distance(a,d);var dir=(d-a)/L;var nrm=new Vector2(-dir.y,dir.x);if(Vector2.Dot(nrm,a-c)<0)nrm=-nrm;
   for(float s=R(0,3);s<L;s+=R(4.4f,5.6f))
   {
    var p=a+dir*s;float sc=R(1.05f,1.6f);float yaw=Mathf.Atan2(dir.x,-dir.y)*Mathf.Rad2Deg-baseYaw+90+R(-25,25);
    foreach(float off in new[]{3.6f,5f,6.5f})
    {
     var q=p+nrm*(off*sc/1.2f);var m=Matrix4x4.TRS(W(q)+Vector3.up*lift*sc,Quaternion.Euler(0,yaw,0),Vector3.one*sc);
     var parts=mfs.Select(mf=>new CombineInstance{mesh=mf.sharedMesh,transform=m*mf.transform.localToWorldMatrix}).ToArray();
     if(parts.All(ci=>ci.mesh.vertices.All(v=>!Inside(Px(ci.transform.MultiplyPoint3x4(v)),boundary)))){b.At("rim",q).ci.AddRange(parts);break;}
    }
   }
  }
  UnityEngine.Object.DestroyImmediate(temp);
 }
 // grass / flower clumps over walkable ground (as 045 stage 2), denser near obstacles and roads
 static void Decor(Bucket b,Vector2[] boundary,Vector2[][] obstacles,Vector2[][] roads)
 {
  float x0=boundary.Min(p=>p.x),x1=boundary.Max(p=>p.x),y0=boundary.Min(p=>p.y),y1=boundary.Max(p=>p.y);
  var boxes=obstacles.Select(o=>(o,x0:o.Min(p=>p.x),x1:o.Max(p=>p.x),y0:o.Min(p=>p.y),y1:o.Max(p=>p.y))).ToArray();
  for(float x=x0;x<x1;x+=6f)for(float y=y0;y<y1;y+=6f)
  {
   var p=new Vector2(x+R(-2.8f,2.8f),y+R(-2.8f,2.8f));
   if(!Inside(p,boundary)||EdgeDist(p,boundary)<4)continue;
   float dObs=999;bool inObs=false;
   foreach(var q in boxes){if(p.x<q.x0-30||p.x>q.x1+30||p.y<q.y0-30||p.y>q.y1+30)continue;if(Inside(p,q.o)){inObs=true;break;}dObs=Mathf.Min(dObs,EdgeDist(p,q.o));}
   if(inObs)continue;
   float dRoad=roads.Min(r=>LineDist(p,r));if(dRoad<12)continue;
   float dEdge=EdgeDist(p,boundary);
   float prob=.035f+(dObs<12?.72f:dObs<26?.25f:0)+(dRoad<18?.22f:0)+(dEdge<16?.3f:0);
   // 047: pebbles near roads and stones (low, visual only)
   if((dRoad<22||dObs<20)&&rng.NextDouble()<.05)Pebbles(b.At("pebbles",p),W(p));
   if(rng.NextDouble()>prob)continue;
   bool flower=(dObs<16||dEdge<14)&&rng.NextDouble()<.7;var acc=b.At("decor",p);
   Clump(acc,W(p),flower);
  }
 }
 static void Pebbles(Acc a,Vector3 c)
 {
  int n=2+rng.Next(3);for(int i=0;i<n;i++){var q=c+new Vector3(R(-.14f,.14f),0,R(-.14f,.14f));Cobble(a,q+Vector3.up*.012f,R(.035f,.065f),R(.02f,.045f),R(0,360),new[]{19,20,21,22}[rng.Next(4)]);}
 }
 static void Clump(Acc a,Vector3 c,bool flower)
 {
  int blades=flower?2:4;
  for(int i=0;i<blades;i++)
  {
   float an=R(0,Mathf.PI*2),h=R(.16f,.28f),w=R(.035f,.055f);var d=new Vector3(Mathf.Cos(an),0,Mathf.Sin(an));var side=new Vector3(-d.z,0,d.x)*w;
   var b0=c+d*R(0,.06f);var cell=Cell(new[]{2,3,5,6}[rng.Next(4)]);int k=a.v.Count;
   a.v.Add(b0-side);a.v.Add(b0+side);a.v.Add(b0+d*R(.02f,.05f)+Vector3.up*h);a.uv.Add(cell);a.uv.Add(cell);a.uv.Add(cell);a.t.AddRange(new[]{k,k+1,k+2});
  }
  if(!flower)return;
  int cellF=new[]{23,23,23,23,28,28,31}[rng.Next(7)];int nf=rng.Next(4,8);
  for(int i=0;i<nf;i++)
  {
   var p=c+new Vector3(R(-.14f,.14f),R(.08f,.15f),R(-.14f,.14f));float s=R(.065f,.095f);var cell=Cell(cellF);int k=a.v.Count;
   a.v.Add(p+new Vector3(-s,0,0));a.v.Add(p+new Vector3(0,0,s));a.v.Add(p+new Vector3(s,0,0));a.v.Add(p+new Vector3(0,0,-s));a.v.Add(p+Vector3.up*s*.7f);
   for(int j=0;j<5;j++)a.uv.Add(cell);
   a.t.AddRange(new[]{k,k+4,k+1,k+1,k+4,k+2,k+2,k+4,k+3,k+3,k+4,k});
  }
 }

 // ---- stills for the user: whole-map top view + game-angle views (no characters in edit mode) ----
 public static object Capture()
 {
  if(EditorSceneManager.GetActiveScene().path!=Target)EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
  var session=UnityEngine.Object.FindObjectsByType<DSMapSession>(FindObjectsInactive.Include).Single();var cam=session.ViewCamera;
  var saved=(cam.transform.position,cam.transform.rotation,cam.orthographic,cam.orthographicSize,cam.nearClipPlane,cam.farClipPlane,cam.targetTexture);
  var shots=new List<string>();Directory.CreateDirectory(Out);
  void Shot(string name,Vector3 pos,Quaternion rotq,float size,int w,int h)
  {
   cam.transform.SetPositionAndRotation(pos,rotq);cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.1f;cam.farClipPlane=500;
   var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){antiAliasing=4};cam.targetTexture=rt;cam.Render();
   var prev=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();RenderTexture.active=prev;
   cam.targetTexture=null;File.WriteAllBytes($"{Out}/{name}.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);shots.Add($"{Out}/{name}.png");
  }
  try
  {
   var data=JsonUtility.FromJson<DSBlockoutData>(session.Layout.text);
   float x0=data.boundary_px.Min(p=>p.x),x1=data.boundary_px.Max(p=>p.x),y0=data.boundary_px.Min(p=>p.y),y1=data.boundary_px.Max(p=>p.y);
   var c=W(new Vector2((x0+x1)/2,(y0+y1)/2));float half=Mathf.Max(x1-x0,y1-y0)*.08f/2+6;
   Shot("overview_top",c+Vector3.up*200,Quaternion.Euler(90,0,0),half,2048,2048);
   var rot=Quaternion.Euler(55,0,0);var fwd=rot*Vector3.forward;
   foreach(var (name,px) in new[]{("view_start",new Vector2(280,300)),("view_center",new Vector2(600,560)),("view_ruins",new Vector2(262,790)),("view_grove",new Vector2(880,900)),("view_lake",new Vector2(880,250)),("view_east",new Vector2(920,520))})
    Shot(name,W(px)-fwd*30,rot,6,1280,720);
   Shot("overview_angle",c-fwd*200,rot,half*1.05f,2048,1600);
  }
  finally
  {
   cam.transform.SetPositionAndRotation(saved.Item1,saved.Item2);cam.orthographic=saved.Item3;cam.orthographicSize=saved.Item4;cam.nearClipPlane=saved.Item5;cam.farClipPlane=saved.Item6;cam.targetTexture=saved.Item7;
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  }
  return shots;
 }
}
