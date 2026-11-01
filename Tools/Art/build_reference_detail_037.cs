using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using DeadSurvivor.Editor;
using Newtonsoft.Json;
public static class ReferenceDetail037
{
 const string Folder="Assets/DeadSurvivor/Scenes/Map037_ReferenceDetail_r002";
 const string Out="output/reference-detail-037/20261102-r001";
 const string SoilTextureSource="docs/art/maps/reference-ground-036/r001/soil-albedo.png";
 const string TextureSource="docs/art/maps/reference-ground-036/r001/meadow-albedo.png";
 static DSBlockoutData data; static Transform visual;static System.Random rng;static int stones; static int fillerStones,internalFragments,bushCount,detailRejects;
 static readonly List<string> created=new List<string>();
 static Vector3 W(Vector2 px,float y=0){var p=DSBlockoutMesh.World(data.Logical(px,4),data.world_scale);p.y=y;return p;}
 static float R(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
 static void Save(UnityEngine.Object o,string name){string p=Folder+"/"+name;if(File.Exists(p))throw new Exception("DESTINATION_EXISTS:"+p);AssetDatabase.CreateAsset(o,p);created.Add(p);}
 static Material Mat(string name,Color color,Texture texture=null)
 {
  var s=Shader.Find("Universal Render Pipeline/Lit");if(!s)throw new Exception("LIT_SHADER_MISSING");var m=new Material(s){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.04f);m.SetFloat("_Metallic",0);if(texture)m.SetTexture("_BaseMap",texture);Save(m,name+".mat");return m;
 }
 static Material SoftSoil(string name,Color color,Texture texture)
 {
  var m=new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetTexture("_BaseMap",texture);m.SetFloat("_Smoothness",0);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ColorMode",0);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;m.SetShaderPassEnabled("ShadowCaster",false);Save(m,name+".mat");return m;
 }
 static Texture2D ImportTexture(string source,string name)
 {
  string target=Folder+"/"+name;File.Copy(source,target);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(target);imp.wrapMode=TextureWrapMode.Repeat;imp.filterMode=FilterMode.Trilinear;imp.mipmapEnabled=true;imp.sRGBTexture=true;imp.maxTextureSize=1024;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
 }
 static Mesh Feather(Vector2[] outer,float y,float opacity=.72f)
 {
  // Concave outline interiors use the existing triangulator. The faded edge remains
  // a purely visual layer and cannot change the collision polygon.
  outer=(Vector2[])outer.Clone();if(DSBlockoutMesh.Area(outer)<0)Array.Reverse(outer);
  var center=outer.Aggregate(Vector2.zero,(a,b)=>a+b)/outer.Length;var inner=outer.Select(p=>Vector2.Lerp(center,p,.54f)).ToArray();
  var v=new List<Vector3>();var c=new List<Color>();var uv=new List<Vector2>();var t=new List<int>();
  foreach(var p in inner){var w=W(p,y);v.Add(w);uv.Add(new Vector2(w.x/2.7f,w.z/2.7f));c.Add(new Color(1,1,1,opacity));}
  t.AddRange(DSBlockoutMesh.Triangulate(inner));
  foreach(var p in outer){var w=W(p,y);v.Add(w);uv.Add(new Vector2(w.x/2.7f,w.z/2.7f));c.Add(new Color(1,1,1,0));}
  int n=inner.Length;for(int i=0;i<n;i++){int j=(i+1)%n;t.AddRange(new[]{i,n+j,j,i,n+i,n+j});}
  var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.SetUVs(0,uv);m.SetColors(c);m.RecalculateNormals();return m;
 }
 static Mesh Beveled(Vector2[] p,float bottom,float top)
 {
  p=(Vector2[])p.Clone();if(DSBlockoutMesh.Area(p)<0)Array.Reverse(p);
  if(p.Length==4){var chipped=new List<Vector2>();for(int i=0;i<4;i++){chipped.Add(Vector2.Lerp(p[i],p[(i+3)%4],R(.12f,.24f)));chipped.Add(Vector2.Lerp(p[i],p[(i+1)%4],R(.12f,.24f)));}p=chipped.ToArray();}
  var center=p.Aggregate(Vector2.zero,(a,b)=>a+b)/p.Length;
  var inset=p.Select(q=>Vector2.Lerp(center,q,.88f)).ToArray();var v=new List<Vector3>();var t=new List<int>();
  foreach(var q in inset)v.Add(W(q,top));t.AddRange(DSBlockoutMesh.Triangulate(inset));
  for(int i=0;i<p.Length;i++){int j=(i+1)%p.Length,n=v.Count;v.AddRange(new[]{W(p[i],bottom),W(p[j],bottom),W(inset[j],top),W(inset[i],top)});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
  var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.uv=v.Select(q=>new Vector2(q.x/.8f,q.z/.8f)).ToArray();m.RecalculateNormals();return m;
 }
 static Vector2[] Blob(Vector2 center,float rx,float ry,int n=11)
 {
  var p=new Vector2[n];for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;float scale=R(.70f,1.1f);p[i]=center+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry)*scale;}return p;
 }
 static bool Contains(DSMapGeometry area,Vector2[] p)
 {
  for(int i=0;i<p.Length;i++)if(!area.SafeSweep(p[i],p[(i+1)%p.Length],.002))return false;return true;
 }
 static void Fragments(List<CombineInstance>[] buckets,DSMapGeometry area,Vector2[] line,float width,float height)
 {
  for(int seg=1;seg<line.Length;seg++){
   var a=line[seg-1];var b=line[seg];var dir=(b-a).normalized;var normal=new Vector2(-dir.y,dir.x);float len=Vector2.Distance(a,b);
   for(float at=0;at<len;){float next=Mathf.Min(len,at+R(4.0f,7.0f));float half=width*R(.4f,.6f);var q0=a+dir*(at+.20f);var q1=a+dir*(next-.20f);
    if(next-at>.8f){var poly=new[]{q0-normal*half,q1-normal*half*R(.8f,1.05f),q1+normal*half,q0+normal*half*R(.8f,1.1f)};
     if(Contains(area,poly)){buckets[rng.Next(buckets.Length)].Add(new CombineInstance{mesh=Beveled(poly,height+.006f,height+R(.04f,.11f)),transform=Matrix4x4.identity});internalFragments++;}else detailRejects++;}
    at=next;
   }
  }
 }
 static Mesh Leaf(Vector2 center,float angle,float length,float width,float baseY,float rise,DSMapGeometry area)
 {
  var forward=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var side=new Vector2(-forward.y,forward.x);
  var root=center+forward*.4f;var mid=center+forward*(length*.43f);var far=center+forward*(length*.79f);var end=center+forward*length;
  // Validate the six actual perimeter edges. The interior ridge is never used
  // as a substitute for a perimeter segment in the containment check.
  var outline=new[]{root,mid-side*width*.5f,far-side*width*.30f,end,far+side*width*.30f,mid+side*width*.5f};if(!Contains(area,outline))return null;
  var v=new List<Vector3>{W(outline[0],baseY),W(outline[1],baseY+rise*.67f),W(outline[2],baseY+rise*.53f),W(outline[3],baseY+rise*.25f),W(outline[4],baseY+rise*.53f),W(outline[5],baseY+rise*.67f),W(mid,baseY+rise)};
  var t=new List<int>();for(int i=0;i<6;i++)t.AddRange(new[]{i,(i+1)%6,6});var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();return m;
 }
 static Mesh FoliageLobe(Vector2 center,float radius,float y,DSMapGeometry area)
 {
  var p=Blob(center,radius,radius*.88f,9);if(!Contains(area,p))return null;
  var v=new List<Vector3>();var t=new List<int>();
  foreach(var q in p)v.Add(W(Vector2.Lerp(center,q,.83f),y));foreach(var q in p)v.Add(W(q,y+.10f));v.Add(W(center,y+.23f));
  for(int i=0;i<9;i++){int j=(i+1)%9;t.AddRange(new[]{i,j,9+j,i,9+j,9+i,9+i,9+j,18});}var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();return m;
 }
 static void Bush(string id,Vector2 center,float radius,float baseY,DSMapGeometry area,Material[] mats,List<Renderer> blockers)
 {
  var groups=mats.Select(m=>new List<CombineInstance>()).ToArray();
  for(int j=0;j<5;j++){
   float angle=j*2.39996f;var q=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius*.28f;var lobe=FoliageLobe(q,radius*.42f,baseY+.025f,area);
   if(lobe)groups[j%2].Add(new CombineInstance{mesh=lobe,transform=Matrix4x4.identity});else detailRejects++;
  }
  for(int i=0;i<45;i++){
   float angle=i*2.39996f;var offset=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*R(0,radius*.23f);
   var leaf=Leaf(center+offset,angle,R(radius*.45f,radius*.82f),R(2.0f,3.8f),baseY+R(.03f,.10f),R(.12f,.22f),area);
   if(leaf)groups[i%groups.Length].Add(new CombineInstance{mesh=leaf,transform=Matrix4x4.identity});else detailRejects++;
  }
  for(int i=0;i<groups.Length;i++)if(groups[i].Count>0){var mr=Combine(id+"_LowFoliage"+i,groups[i],mats[i]);mr.shadowCastingMode=ShadowCastingMode.On;blockers.Add(mr);}
  bushCount++;
 }
 static Mesh WorldPrism(Vector2[] p,float height,bool capOnly=false)
 {
  var m=DSBlockoutMesh.Prism(p.Select(q=>data.Logical(q,4)).ToArray(),data.world_scale,height/data.world_scale,capOnly);m.uv=m.vertices.Select(v=>new Vector2(v.x/4f,v.z/4f)).ToArray();return m;
 }
 static MeshRenderer Object(string name,Mesh mesh,Material mat)
 {
  mesh.name=name;Save(mesh,name+".asset");var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(visual,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var mr=go.GetComponent<MeshRenderer>();mr.sharedMaterial=mat;mr.shadowCastingMode=ShadowCastingMode.Off;return mr;
 }
 // Constant inward offset in source pixels. Winding is normalized; the resulting inset
 // is validated against the original polygon before it is used as a visual green cap.
 static Vector2[] Offset(Vector2[] points,float amount)
 {
  var p=(Vector2[])points.Clone();if(DSBlockoutMesh.Area(p)<0)Array.Reverse(p);var q=new Vector2[p.Length];
  for(int i=0;i<p.Length;i++){
   var a=(p[i]-p[(i+p.Length-1)%p.Length]).normalized;var b=(p[(i+1)%p.Length]-p[i]).normalized;
   var na=new Vector2(-a.y,a.x);var nb=new Vector2(-b.y,b.x);var bis=na+nb;float den=Vector2.Dot(bis,nb);
   if(Mathf.Abs(den)<.05f)throw new Exception("UNSAFE_OFFSET_JOIN");q[i]=p[i]+bis*(amount/den);
  }
  DSBlockoutMesh.Triangulate(q);return q;
 }
 static void Restore(DSMapMotionVerification.SnapshotData saved)
 {
  EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(x=>new SceneSetup{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive}).ToArray());
  Selection.objects=saved.selection.Select(x=>{GlobalObjectId.TryParse(x,out var id);return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);}).Where(x=>x).ToArray();
 }
 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets")throw new Exception("WRONG_PROJECT");
  for(string p=Path.GetFullPath(Folder);p!=null;p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new Exception("SYMLINK_PATH");
  var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
  if(!File.Exists(Out+"/editor-before.json"))File.WriteAllText(Out+"/editor-before.json",JsonUtility.ToJson(saved,true));
  File.WriteAllText(Out+"/editor-build-before-"+Path.GetFileName(Folder)+".json",JsonUtility.ToJson(saved,true));
  if(Directory.Exists(Folder)||File.Exists(Folder+".meta")||!File.Exists(TextureSource)||!File.Exists(SoilTextureSource))throw new Exception("OUTPUT_EXISTS_OR_TEXTURE_MISSING");
  File.WriteAllText(Out+"/build-planned.json",JsonConvert.SerializeObject(new{folder=Folder,started=DateTimeOffset.Now.ToString("o"),failurePolicy="Preserve partial files, record ownership, then use a new revision; never overwrite prior assets."},Formatting.Indented));
  AssetDatabase.CreateFolder("Assets/DeadSurvivor/Scenes","Map037_ReferenceDetail_r002");
  try{
   File.Copy(TextureSource,Folder+"/MeadowAlbedo.png");AssetDatabase.ImportAsset(Folder+"/MeadowAlbedo.png",ImportAssetOptions.ForceSynchronousImport);
   var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"/MeadowAlbedo.png");importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/MeadowAlbedo.png");var soilTexture=ImportTexture(SoilTextureSource,"SoilAlbedo.png");
   var scene=EditorSceneManager.OpenScene(DSMapPlayableVerification.CombatScene,OpenSceneMode.Single);
   var s=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();if(!s)throw new Exception("SESSION_MISSING");
   var root=s.transform.root.gameObject;if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   data=JsonUtility.FromJson<DSBlockoutData>(s.Layout.text);data.Validate();var layoutHash=DSMapBlockoutPreview.Hash(s.Layout.text);
   s.ImageScale=4;s.Elevation=55;s.CombatEnabled=false;s.StartCenter=data.Logical(new Vector2(280,300),4);
   if(!DSMapPartyRules.CanStart(data.Geometry(4),s.StartCenter,4))throw new Exception("UNSAFE_START");
   root.name="Map037_REFERENCE_DETAIL_TRIAL";
   visual=new GameObject("Reference037_VisualOnly").transform;visual.SetParent(s.StaticGeometry,false);rng=new System.Random(36001);
   var grass=Mat("Meadow",new Color(.40f,.57f,.65f),texture);var cap=Mat("MossTop",new Color(.46f,.72f,.54f),texture);
   var soil=SoftSoil("DrySoil",new Color(.65f,.70f,.62f),soilTexture);var soilLight=SoftSoil("DrySoilLight",new Color(.79f,.81f,.72f),soilTexture);var soilEdge=SoftSoil("SoilSoftEdge",new Color(.64f,.72f,.54f),soilTexture);
   var stone=Mat("RemnantStone",new Color(.75f,.93f,1.25f),soilTexture);var seams=Mat("StoneJoints",new Color(.20f,.25f,.22f));
   var rimMats=new[]{stone,Mat("StoneLight",new Color(.90f,1.05f,1.30f),soilTexture),Mat("StoneMoss",new Color(.50f,.75f,.65f),soilTexture)};
   var foliage=new[]{Mat("FoliageDeep",new Color(.055f,.13f,.075f)),Mat("FoliageMid",new Color(.10f,.235f,.12f)),Mat("FoliageTips",new Color(.18f,.34f,.17f))};foreach(var mat in foliage){mat.SetFloat("_Cull",0);UnityEditor.EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);}
   var roadMats=new[]{Mat("CobbleGrey",new Color(.40f,.47f,.50f)),Mat("CobbleCool",new Color(.34f,.41f,.45f)),Mat("CobbleLight",new Color(.49f,.53f,.54f)),Mat("CobbleMoss",new Color(.32f,.40f,.35f))};
   Object("GrassGround",WorldPrism(new[]{new Vector2(80,80),new Vector2(545,80),new Vector2(545,395),new Vector2(80,395)},.003f,true),grass);
   var batches=new List<CombineInstance>[3];for(int i=0;i<3;i++)batches[i]=new List<CombineInstance>();
   // Broad soil flows are visual observations of the original, not path/collision data.
   float[,] patches={{135,274,24,21},{145,247,14,22},{159,215,19,13},{192,299,13,11},{279,139,30,9},{328,164,20,9},{289,207,19,12},{306,228,12,15},{337,238,12,10},{344,280,16,11},{413,239,13,10},{510,270,14,22},{515,166,18,14},{485,137,16,9},{304,351,20,11},{184,351,14,16},{450,352,17,10}};
   for(int j=0;j<patches.GetLength(0);j++){
    var center=new Vector2(patches[j,0],patches[j,1]);float rx=patches[j,2],ry=patches[j,3];
    int pieces=Mathf.Clamp(Mathf.RoundToInt(rx*ry/25),8,20);
    // A very faint connected stain unifies the wear, while the higher-opacity
    // small islands still leave grass between them. There is no full rim halo.
    batches[2].Add(new CombineInstance{mesh=Feather(Blob(center,rx*.82f,ry*.85f,19),.0045f,.23f),transform=Matrix4x4.identity});
    for(int k=0;k<pieces;k++){
     float a=R(0,Mathf.PI*2),dist=Mathf.Sqrt(R(0,1));var q=center+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry)*dist;
     var poly=Blob(q,R(2.5f,rx*.40f),R(2.5f,ry*.43f));
     batches[(j+k)%3].Add(new CombineInstance{mesh=Feather(poly,.006f+.0004f*k,.60f),transform=Matrix4x4.identity});
    }
   }
   var selected=data.objects.Where(o=>new[]{"OB-001","OB-002","OB-003","OB-004","OB-005","OB-006"}.Contains(o.id)).ToArray();
   var blockers=new List<Renderer>();var heights=new List<object>();
   foreach(var o in selected){
    var groups=rimMats.Select(m=>new List<CombineInstance>()).ToArray();
    var old=s.StaticGeometry.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name==o.id);old.enabled=false;
    var p=(Vector2[])o.points_px.Clone();if(DSBlockoutMesh.Area(p)<0)Array.Reverse(p);var inset=Offset(p,3.2f);
    var inside=new DSMapGeometry(p,Array.Empty<Vector2[]>(),.001,16384);
    for(int i=0;i<inset.Length;i++)if(!inside.SafeSweep(inset[i],inset[(i+1)%inset.Length],.001))throw new Exception("CAP_OUTSIDE_ORIGINAL:"+o.id);
    // Local soil wear only. Do not trace a bright band around the entire footprint.
    for(int k=0;k<5;k++){int edge=(k*3+Array.IndexOf(selected,o))%p.Length;var q=Vector2.Lerp(p[edge],p[(edge+1)%p.Length],R(.2f,.8f));batches[k%3].Add(new CombineInstance{mesh=Feather(Blob(q,R(3,7),R(2,5)),.012f+k*.0003f),transform=Matrix4x4.identity});}
    float height=.22f+(.07f*(Array.IndexOf(selected,o)%3));
    var body=Object(o.id+"_ThinStoneRim",WorldPrism(p,height-.06f),seams);body.shadowCastingMode=ShadowCastingMode.On;blockers.Add(body);
    var capMesh=WorldPrism(inset,height+.003f);blockers.Add(Object(o.id+"_GreenTop",capMesh,cap));
    heights.Add(new{id=o.id,nominalHeightMetres=height,greenTopHeight=height+.003f,rimMaximumUpperBound=height+.14f,vegetationMaximumUpperBound=height+.39f,insetPixels=3.2f});
    for(int edge=0;edge<p.Length;edge++){
     var a=p[edge];var b=p[(edge+1)%p.Length];float len=Vector2.Distance(a,b);
     for(float at=0;at<len;){float end=Mathf.Min(len,at+R(4,9));float gap=R(.12f,.50f);float t0=(at+gap*.5f)/len,t1=(end-gap*.5f)/len;
      if(end-at>gap+.25f && rng.NextDouble()>.06){
       var e0=Vector2.Lerp(a,b,t0);var e1=Vector2.Lerp(a,b,t1);var in0=Vector2.Lerp(inset[edge],inset[(edge+1)%p.Length],t0);var in1=Vector2.Lerp(inset[edge],inset[(edge+1)%p.Length],t1);
       var outer0=Vector2.Lerp(e0,in0,R(.03f,.12f));var outer1=Vector2.Lerp(e1,in1,R(.03f,.12f));var inner0=Vector2.Lerp(e0,in0,R(.70f,1));var inner1=Vector2.Lerp(e1,in1,R(.70f,1));
       var poly=new[]{outer0,outer1,inner1,inner0};
       if(Contains(inside,poly))groups[rng.Next(groups.Length)].Add(new CombineInstance{mesh=Beveled(poly,height-.05f,height+R(.03f,.14f)),transform=Matrix4x4.identity});else detailRejects++;
      }at=end;
     }
    }
    if(o.id=="OB-001"){
     Fragments(groups,inside,new[]{new Vector2(210,155),new Vector2(215,168),new Vector2(227,180),new Vector2(239,188)},3.8f,height);
     Fragments(groups,inside,new[]{new Vector2(237,156),new Vector2(230,163),new Vector2(223,169)},3.0f,height);
     Fragments(groups,inside,new[]{new Vector2(203,170),new Vector2(211,172)},3.0f,height);
    }
    // Shallow transverse chips break up the center of the two long curved remnants.
    if(o.id=="OB-002")Fragments(groups,inside,new[]{new Vector2(349,193),new Vector2(354,203)},2.1f,height);
    if(o.id=="OB-003")Fragments(groups,inside,new[]{new Vector2(454,204),new Vector2(459,213)},2.1f,height);
    for(int i=0;i<groups.Length;i++)if(groups[i].Count>0){var mr=Combine(o.id+"_RimBlocks"+(i==0?"":i.ToString()),groups[i],rimMats[i]);mr.shadowCastingMode=ShadowCastingMode.On;blockers.Add(mr);}
    if(o.id=="OB-001")Bush(o.id,new Vector2(224,185),6.5f,height+.003f,inside,foliage,blockers);
    if(o.id=="OB-002")Bush(o.id,new Vector2(342,201),5.5f,height+.003f,inside,foliage,blockers);
    if(o.id=="OB-004")Bush(o.id,new Vector2(240,277),5.7f,height+.003f,inside,foliage,blockers);
    if(o.id=="OB-006")Bush(o.id,new Vector2(466,270),7.0f,height+.003f,inside,foliage,blockers);
   }
   foreach(var mr in s.RockHolder.GetComponentsInChildren<Renderer>(true))mr.enabled=false;
   for(int i=0;i<3;i++)Combine("SoilLayer"+i,batches[i],new[]{soil,soilLight,soilEdge}[i]);
   // RT-003 is the preserved source path, clipped to the representative ground patch.
   var route=data.routes.Single(r=>r.id=="RT-003").points_px;
   Vector2 PathAt(float x){if(x<route[0].x)return Vector2.LerpUnclamped(route[0],route[1],(x-route[0].x)/(route[1].x-route[0].x));for(int i=1;i<route.Length;i++)if(x<=route[i].x)return Vector2.Lerp(route[i-1],route[i],(x-route[i-1].x)/(route[i].x-route[i-1].x));throw new Exception("PATH_OUTSIDE");}
   var roadOutline=new List<Vector2>();for(float x=80;x<=545;x+=5)roadOutline.Add(PathAt(x)+Vector2.up*9.3f);for(float x=545;x>=80;x-=5)roadOutline.Add(PathAt(x)-Vector2.up*9.3f);
   var roadBed=SoftSoil("CoolRoadBed",new Color(.32f,.42f,.48f),soilTexture);Object("CobbleBed",Feather(roadOutline.ToArray(),.011f,.90f),roadBed);
   var rockBatches=roadMats.Select(m=>new List<CombineInstance>()).ToArray();
   // Irregular variable-size packing follows the same RT-003 centerline; it does
   // not create five rows or alter any navigable region. Seeded for reproduction.
   var packed=new List<Vector4>();
   for(int attempt=0;attempt<24000;attempt++){
    float x=R(80,545),offset=R(-9,9),rx=R(1.45f,3.6f),ry=R(1.4f,2.9f);
    if(Mathf.Abs(offset)+ry>9.3f+Mathf.Sin(x*.11f)*.9f)continue;
    var at=PathAt(x)+Vector2.up*offset;
    if(packed.Any(p=>Mathf.Pow((p.x-at.x)/(p.z+rx),2)+Mathf.Pow((p.y-at.y)/(p.w+ry),2)<.81f))continue;
    packed.Add(new Vector4(at.x,at.y,rx,ry));
    var poly=new Vector2[7];float angle=R(0,6.28f);
    for(int j=0;j<7;j++){float a=angle+j*Mathf.PI*2/7;poly[j]=at+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry)*R(.96f,1.13f);}
    var m=Beveled(poly,.008f,R(.065f,.11f));rockBatches[rng.Next(4)].Add(new CombineInstance{mesh=m,transform=Matrix4x4.Translate(new Vector3(0,.012f,0))});stones++;
   }
   for(int attempt=0;attempt<18000;attempt++){
    float x=R(80,545),offset=R(-9.2f,9.2f),radius=R(.58f,1.3f);if(Mathf.Abs(offset)+radius>9.6f+Mathf.Sin(x*.11f)*.9f)continue;var at=PathAt(x)+Vector2.up*offset;
    if(packed.Any(p=>Mathf.Pow((p.x-at.x)/(p.z+radius),2)+Mathf.Pow((p.y-at.y)/(p.w+radius),2)<.84f))continue;
    packed.Add(new Vector4(at.x,at.y,radius,radius));var poly=Blob(at,radius,radius*R(.75f,1.2f),6);
    rockBatches[rng.Next(3)].Add(new CombineInstance{mesh=Beveled(poly,.012f,R(.045f,.078f)),transform=Matrix4x4.identity});fillerStones++;
   }
   for(int i=0;i<4;i++)Combine("PathCobbles"+i,rockBatches[i],roadMats[i]).shadowCastingMode=ShadowCastingMode.On;

   s.Visibility.Blockers=s.StaticGeometry.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.name.StartsWith("OB-",StringComparison.Ordinal)).Concat(blockers).Distinct().ToArray();
   if(visual.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("VISUAL_COLLIDER");
   foreach(var l in root.GetComponentsInChildren<Light>(true)){l.color=new Color(1,.96f,.88f);l.intensity=1.05f;l.transform.rotation=Quaternion.Euler(55,-30,0);l.shadows=LightShadows.Soft;}
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.60f,.65f,.69f);RenderSettings.ambientEquatorColor=new Color(.48f,.52f,.43f);RenderSettings.ambientGroundColor=new Color(.30f,.32f,.25f);
   s.ViewCamera.orthographic=true;s.ViewCamera.orthographicSize=6;s.ViewCamera.nearClipPlane=.1f;s.ViewCamera.farClipPlane=500;s.ViewCamera.transform.rotation=Quaternion.Euler(55,0,0);s.ViewCamera.transform.position=W(new Vector2(280,300))-s.ViewCamera.transform.forward*150;
   string target=Folder+"/Map037_ReferenceDetail.unity";if(!EditorSceneManager.SaveScene(scene,target))throw new Exception("SAVE_FAILED");
   EditorSceneManager.OpenScene(target,OpenSceneMode.Single);var reloaded=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();
   if(!reloaded||reloaded.ExpectedDataSha!=layoutHash||DSMapBlockoutPreview.Hash(reloaded.Layout.text)!=layoutHash||reloaded.CombatEnabled||reloaded.Visibility.Blockers.Any(r=>!r))throw new Exception("SAVED_BINDING_INVALID");
   var result=new{status="PASS_AUTHORING_ONLY",scene=target,source="docs/art/maps/references/VerdantMeadowsFullMap.png",layout=AssetDatabase.GetAssetPath(reloaded.Layout),layoutHash,objects=selected.Select(o=>o.id).ToArray(),heights,road="RT-003",stones,fillerStones,internalFragments,bushCount,detailRejects,created,visualColliderCount=0,notes="Original C and runtime unchanged. Heights are trial proposals. Ground material generated from original; source image is not a whole-map texture."};
   File.WriteAllText(Out+"/build.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
  }catch(Exception e){File.WriteAllText(Out+"/build-failure.json",JsonConvert.SerializeObject(new{status="FAIL_PARTIAL_PRESERVED",error=e.ToString(),folder=Folder,created,files=Directory.Exists(Folder)?Directory.GetFiles(Folder,"*",SearchOption.AllDirectories):Array.Empty<string>(),recovery="Verify owned files, retain failed revision, and build into a fresh revision. Do not delete or overwrite preexisting assets."},Formatting.Indented));throw;}finally{Restore(saved);}
 }
 static MeshRenderer Combine(string name,List<CombineInstance> parts,Material mat)
 {
  var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.ToArray(),true,true);foreach(var part in parts)UnityEngine.Object.DestroyImmediate(part.mesh);return Object(name,mesh,mat);
 }
}
