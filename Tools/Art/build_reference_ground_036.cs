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
public static class ReferenceGround036
{
 const string Folder="Assets/DeadSurvivor/Scenes/Map036_ReferenceGround_r003";
 const string Out="output/reference-ground-036/20261102-r001";
 const string SoilTextureSource="docs/art/maps/reference-ground-036/r001/soil-albedo.png";
 const string TextureSource="docs/art/maps/reference-ground-036/r001/meadow-albedo.png";
 static DSBlockoutData data; static Transform visual;static System.Random rng;static int stones;
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
 static Mesh Feather(Vector2[] outer,float y)
 {
  // Concave outline interiors use the existing triangulator. The faded edge remains
  // a purely visual layer and cannot change the collision polygon.
  outer=(Vector2[])outer.Clone();if(DSBlockoutMesh.Area(outer)<0)Array.Reverse(outer);
  var center=outer.Aggregate(Vector2.zero,(a,b)=>a+b)/outer.Length;var inner=outer.Select(p=>Vector2.Lerp(center,p,.72f)).ToArray();
  var v=new List<Vector3>();var c=new List<Color>();var uv=new List<Vector2>();var t=new List<int>();
  foreach(var p in inner){var w=W(p,y);v.Add(w);uv.Add(new Vector2(w.x/2.7f,w.z/2.7f));c.Add(Color.white);}
  t.AddRange(DSBlockoutMesh.Triangulate(inner));
  foreach(var p in outer){var w=W(p,y);v.Add(w);uv.Add(new Vector2(w.x/2.7f,w.z/2.7f));c.Add(new Color(1,1,1,0));}
  int n=inner.Length;for(int i=0;i<n;i++){int j=(i+1)%n;t.AddRange(new[]{i,n+j,j,i,n+i,n+j});}
  var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.SetUVs(0,uv);m.SetColors(c);m.RecalculateNormals();return m;
 }
 static Mesh Beveled(Vector2[] p,float bottom,float top)
 {
  if(DSBlockoutMesh.Area(p)<0)Array.Reverse(p);var center=p.Aggregate(Vector2.zero,(a,b)=>a+b)/p.Length;
  var inset=p.Select(q=>Vector2.Lerp(center,q,.78f)).ToArray();var v=new List<Vector3>();var t=new List<int>();
  foreach(var q in inset)v.Add(W(q,top));t.AddRange(DSBlockoutMesh.Triangulate(inset));
  for(int i=0;i<p.Length;i++){int j=(i+1)%p.Length,n=v.Count;v.AddRange(new[]{W(p[i],bottom),W(p[j],bottom),W(inset[j],top),W(inset[i],top)});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
  var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();return m;
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
  AssetDatabase.CreateFolder("Assets/DeadSurvivor/Scenes","Map036_ReferenceGround_r003");
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
   root.name="Map036_REFERENCE_GROUND_AND_SECTION_TRIAL";
   visual=new GameObject("Reference036_VisualOnly").transform;visual.SetParent(s.StaticGeometry,false);rng=new System.Random(36001);
   var grass=Mat("Meadow",new Color(.40f,.57f,.65f),texture);var cap=Mat("MossTop",new Color(.46f,.72f,.54f),texture);
   var soil=SoftSoil("DrySoil",new Color(.65f,.70f,.62f),soilTexture);var soilLight=SoftSoil("DrySoilLight",new Color(.79f,.81f,.72f),soilTexture);var soilEdge=SoftSoil("SoilSoftEdge",new Color(.64f,.72f,.54f),soilTexture);
   var stone=Mat("RemnantStone",new Color(.49f,.535f,.53f));var seams=Mat("StoneJoints",new Color(.25f,.29f,.26f));
   var roadMats=new[]{Mat("CobbleGrey",new Color(.40f,.47f,.50f)),Mat("CobbleCool",new Color(.34f,.41f,.45f)),Mat("CobbleLight",new Color(.49f,.53f,.54f)),Mat("CobbleMoss",new Color(.32f,.40f,.35f))};
   Object("GrassGround",WorldPrism(new[]{new Vector2(80,80),new Vector2(545,80),new Vector2(545,395),new Vector2(80,395)},.003f,true),grass);
   var batches=new List<CombineInstance>[3];for(int i=0;i<3;i++)batches[i]=new List<CombineInstance>();
   // Broad soil flows are visual observations of the original, not path/collision data.
   float[,] patches={{135,274,24,21},{145,247,14,22},{159,215,19,13},{192,299,13,11},{279,139,30,9},{328,164,20,9},{289,207,19,12},{306,228,12,15},{337,238,12,10},{344,280,16,11},{413,239,13,10},{510,270,14,22},{515,166,18,14},{485,137,16,9},{304,351,20,11},{184,351,14,16},{450,352,17,10}};
   for(int j=0;j<patches.GetLength(0);j++){
    var center=new Vector2(patches[j,0],patches[j,1]);var poly=new Vector2[18];
    for(int i=0;i<poly.Length;i++){float a=i*Mathf.PI*2/poly.Length;float r=R(.72f,1.1f);poly[i]=center+new Vector2(Mathf.Cos(a)*patches[j,2],Mathf.Sin(a)*patches[j,3])*r;}
    var expanded=poly.Select(p=>center+(p-center)*1.09f).ToArray();batches[2].Add(new CombineInstance{mesh=Feather(expanded,.005f),transform=Matrix4x4.identity});
    batches[j%2].Add(new CombineInstance{mesh=Feather(poly,.007f),transform=Matrix4x4.identity});
   }
   var selected=data.objects.Where(o=>new[]{"OB-001","OB-002","OB-003","OB-004","OB-005","OB-006"}.Contains(o.id)).ToArray();
   var blockers=new List<Renderer>();var heights=new List<object>();
   foreach(var o in selected){
    var joins=new List<CombineInstance>();
    var old=s.StaticGeometry.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name==o.id);old.enabled=false;
    var p=(Vector2[])o.points_px.Clone();if(DSBlockoutMesh.Area(p)<0)Array.Reverse(p);var inset=Offset(p,1.9f);
    var inside=new DSMapGeometry(p,Array.Empty<Vector2[]>(),.001,16384);
    for(int i=0;i<inset.Length;i++)if(!inside.SafeSweep(inset[i],inset[(i+1)%inset.Length],.001))throw new Exception("CAP_OUTSIDE_ORIGINAL:"+o.id);
    batches[0].Add(new CombineInstance{mesh=Feather(Offset(p,-5f),.009f),transform=Matrix4x4.identity});
    float height=.22f+(.07f*(Array.IndexOf(selected,o)%3));
    var body=Object(o.id+"_ThinStoneRim",WorldPrism(p,height-.035f),seams);body.shadowCastingMode=ShadowCastingMode.On;blockers.Add(body);
    blockers.Add(Object(o.id+"_GreenTop",WorldPrism(inset,height+.003f,true),cap));heights.Add(new{id=o.id,nominalHeightMetres=height,greenTopHeight=height+.003f,rimMaximumUpperBound=height+.07f,insetPixels=1.9f});
    for(int edge=0;edge<p.Length;edge++){
     var a=p[edge];var b=p[(edge+1)%p.Length];float len=Vector2.Distance(a,b);int count=Mathf.Max(1,Mathf.CeilToInt(len/6));
     for(int i=0;i<count;i++){float t0=(i+.035f)/count,t1=(i+.965f)/count;var outer0=Vector2.Lerp(a,b,t0);var outer1=Vector2.Lerp(a,b,t1);var inner0=Vector2.Lerp(inset[edge],inset[(edge+1)%p.Length],t0);var inner1=Vector2.Lerp(inset[edge],inset[(edge+1)%p.Length],t1);
      joins.Add(new CombineInstance{mesh=Beveled(new[]{outer0,outer1,inner1,inner0},height-.03f,height+R(.015f,.07f)),transform=Matrix4x4.identity});}

    }
    blockers.Add(Combine(o.id+"_RimBlocks",joins,stone));
   }
   foreach(var mr in s.RockHolder.GetComponentsInChildren<Renderer>(true))mr.enabled=false;
   for(int i=0;i<3;i++)Combine("SoilLayer"+i,batches[i],new[]{soil,soilLight,soilEdge}[i]);
   // RT-003 is the preserved source path, clipped to the representative ground patch.
   var route=data.routes.Single(r=>r.id=="RT-003").points_px;
   Vector2 PathAt(float x){if(x<route[0].x)return Vector2.LerpUnclamped(route[0],route[1],(x-route[0].x)/(route[1].x-route[0].x));for(int i=1;i<route.Length;i++)if(x<=route[i].x)return Vector2.Lerp(route[i-1],route[i],(x-route[i-1].x)/(route[i].x-route[i-1].x));throw new Exception("PATH_OUTSIDE");}
   var roadOutline=new List<Vector2>();for(float x=80;x<=545;x+=5)roadOutline.Add(PathAt(x)+Vector2.up*9.3f);for(float x=545;x>=80;x-=5)roadOutline.Add(PathAt(x)-Vector2.up*9.3f);
   Object("CobbleBed",Feather(roadOutline.ToArray(),.011f),soilEdge);
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
    for(int j=0;j<7;j++){float a=angle+j*Mathf.PI*2/7;poly[j]=at+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry)*R(.88f,1.04f);}
    var m=Beveled(poly,.008f,R(.065f,.11f));rockBatches[rng.Next(4)].Add(new CombineInstance{mesh=m,transform=Matrix4x4.Translate(new Vector3(0,.012f,0))});stones++;
   }
   for(int i=0;i<4;i++)Combine("PathCobbles"+i,rockBatches[i],roadMats[i]).shadowCastingMode=ShadowCastingMode.On;

   s.Visibility.Blockers=s.StaticGeometry.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.name.StartsWith("OB-",StringComparison.Ordinal)).Concat(blockers).Distinct().ToArray();
   if(visual.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("VISUAL_COLLIDER");
   foreach(var l in root.GetComponentsInChildren<Light>(true)){l.color=new Color(1,.96f,.88f);l.intensity=1.05f;l.transform.rotation=Quaternion.Euler(55,-30,0);l.shadows=LightShadows.Soft;}
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.60f,.65f,.69f);RenderSettings.ambientEquatorColor=new Color(.48f,.52f,.43f);RenderSettings.ambientGroundColor=new Color(.30f,.32f,.25f);
   s.ViewCamera.orthographic=true;s.ViewCamera.orthographicSize=6;s.ViewCamera.nearClipPlane=.1f;s.ViewCamera.farClipPlane=500;s.ViewCamera.transform.rotation=Quaternion.Euler(55,0,0);s.ViewCamera.transform.position=W(new Vector2(280,300))-s.ViewCamera.transform.forward*150;
   string target=Folder+"/Map036_ReferenceGround.unity";if(!EditorSceneManager.SaveScene(scene,target))throw new Exception("SAVE_FAILED");
   EditorSceneManager.OpenScene(target,OpenSceneMode.Single);var reloaded=UnityEngine.Object.FindAnyObjectByType<DSMapSession>();
   if(!reloaded||reloaded.ExpectedDataSha!=layoutHash||DSMapBlockoutPreview.Hash(reloaded.Layout.text)!=layoutHash||reloaded.CombatEnabled||reloaded.Visibility.Blockers.Any(r=>!r))throw new Exception("SAVED_BINDING_INVALID");
   var result=new{status="PASS_AUTHORING_ONLY",scene=target,source="docs/art/maps/references/VerdantMeadowsFullMap.png",layout=AssetDatabase.GetAssetPath(reloaded.Layout),layoutHash,objects=selected.Select(o=>o.id).ToArray(),heights,road="RT-003",stones,created,visualColliderCount=0,notes="Original C and runtime unchanged. Heights are trial proposals. Ground material generated from original; source image is not a whole-map texture."};
   File.WriteAllText(Out+"/build.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
  }catch(Exception e){File.WriteAllText(Out+"/build-failure.json",JsonConvert.SerializeObject(new{status="FAIL_PARTIAL_PRESERVED",error=e.ToString(),folder=Folder,created,files=Directory.Exists(Folder)?Directory.GetFiles(Folder,"*",SearchOption.AllDirectories):Array.Empty<string>(),recovery="Verify owned files, retain failed revision, and build into a fresh revision. Do not delete or overwrite preexisting assets."},Formatting.Indented));throw;}finally{Restore(saved);}
 }
 static MeshRenderer Combine(string name,List<CombineInstance> parts,Material mat)
 {
  var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.ToArray(),true,true);foreach(var part in parts)UnityEngine.Object.DestroyImmediate(part.mesh);return Object(name,mesh,mat);
 }
}
