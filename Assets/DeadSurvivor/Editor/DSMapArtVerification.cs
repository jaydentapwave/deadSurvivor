using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace DeadSurvivor.Editor
{
    // Isolated authoring experiment. Layout and gameplay geometry remain the 018 source.
    public static class DSMapArtVerification
    {
        public const string Folder="Assets/DeadSurvivor/Scenes/Map028_ArtExperimental_r004";
        public const string BasicPrefab=Folder+"/Basic.prefab",ArtPrefab=Folder+"/Art.prefab";
        static readonly Rect Region=Rect.MinMaxRect(140,105,420,340);
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        [Serializable] public class Row {public string path,sha256;}
        [Serializable] public class BuildRecord {public string output_root=Folder;public List<string> planned=new();public string status="BUILDING",scope="REPRESENTATIVE_ART_PROTOTYPE_ONLY",error;public string[] objects;public Vector2 start_px;public int seed=28001;public List<Row> files=new();}
        static BuildRecord record;static string journal;
        static void Journal(){File.WriteAllText(journal,JsonUtility.ToJson(record,true));}
        static void Confirm(string path){foreach(var p in new[]{path,path+".meta"})if(File.Exists(p))record.files.Add(new Row{path=p,sha256=Hash(p)});Journal();}
        static void Asset(UnityEngine.Object obj,string name){
            if(obj is Mesh mesh){var v=mesh.vertices;var t=mesh.triangles;if(v.Length==0 || t.Length==0 || t.Length%3!=0 || v.Any(q=>!DSMapGeometry.Finite(q.x)||!DSMapGeometry.Finite(q.y)||!DSMapGeometry.Finite(q.z)) || t.Any(i=>i<0||i>=v.Length))throw new Exception("INVALID_GENERATED_MESH:"+name);for(int i=0;i<t.Length;i+=3)if(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude<1e-12f)throw new Exception("DEGENERATE_GENERATED_MESH:"+name);}
            string path=Folder+"/"+name;record.planned.Add(path);Journal();AssetDatabase.CreateAsset(obj,path);Confirm(path);}
        static Material Mat(string name,Color c)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)throw new Exception("URP_LIT_MISSING");
            var m=new Material(shader){name=name};m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.08f);Asset(m,name+".mat");return m;
        }
        static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material mat)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
        }
        static void Restore(DSMapMotionVerification.SnapshotData s)
        {
            EditorSceneManager.RestoreSceneManagerSetup(s.scenes.Select(x=>new SceneSetup{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive}).ToArray());
            Selection.objects=s.selection.Select(x=>{if(!GlobalObjectId.TryParse(x,out var id))throw new Exception("SELECTION_INVALID");return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);}).ToArray();
        }
        static void NoLinks(string path)
        {
            string absolute=Path.GetFullPath(path);string project=Path.GetFullPath(Application.dataPath+"/..");
            if(absolute!=project && !absolute.StartsWith(project+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new Exception("PATH_OUTSIDE_PROJECT");
            for(var p=absolute;p!=null;p=Path.GetDirectoryName(p))
            {
                try{if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new Exception("SYMLINK_ANCESTOR:"+p);}
                catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
            }
        }
        public static string Lighting()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.47f,.56f,.62f);RenderSettings.ambientEquatorColor=new Color(.39f,.43f,.35f);RenderSettings.ambientGroundColor=new Color(.20f,.19f,.15f);
            return "ART028_TRILIGHT_R001";
        }
        public static string Build()
        {
            if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets")throw new Exception("WRONG_PROJECT");
            NoLinks(Folder);NoLinks("output/map-art-028");
            var saved=(DSMapMotionVerification.SnapshotData)DSMapMotionVerification.Snapshot();
            if(Directory.Exists(Folder) || File.Exists(Folder+".meta"))throw new Exception("DESTINATION_EXISTS");
            string output="output/map-art-028/build-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(output);journal=output+"/created-files.json";
            record=new BuildRecord();Journal();
            var original=AssetDatabase.GetDependencies(DSMapPlayableVerification.CombatScene,true).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)).SelectMany(p=>new[]{p,p+".meta"}).Where(File.Exists).Distinct().OrderBy(p=>p).Select(p=>new Row{path=p,sha256=Hash(p)}).ToArray();
            File.WriteAllText(output+"/originals.json",JsonUtility.ToJson(new OriginalRows{files=original},true));
            try
            {
                AssetDatabase.CreateFolder("Assets/DeadSurvivor/Scenes","Map028_ArtExperimental_r004");Confirm(Folder);
                var scene=EditorSceneManager.OpenScene(DSMapPlayableVerification.CombatScene,OpenSceneMode.Single);
                var session=UnityEngine.Object.FindFirstObjectByType<DSMapSession>();if(!session)throw new Exception("SESSION_MISSING");
                var data=JsonUtility.FromJson<DSBlockoutData>(session.Layout.text);data.Validate();var geometry=data.Geometry(4);
                var startPx=new Vector2(280,230);var center=data.Logical(startPx,4);
                if(!DSMapPartyRules.CanStart(geometry,center,4))throw new Exception("ART_START_UNSAFE");session.StartCenter=center;record.start_px=startPx;
                var root=session.gameObject;root.name="Map028_REPRESENTATIVE_EXPERIMENT";
                foreach(var light in root.GetComponentsInChildren<Light>(true)){light.color=new Color(1,.95f,.86f);light.intensity=1.15f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(43,-35,0);}
                // Scene-local comparison lighting, identical for both variants. No pipeline asset edits.
                Lighting();
                session.ViewCamera.orthographic=true;session.ViewCamera.orthographicSize=6;session.ViewCamera.nearClipPlane=.1f;session.ViewCamera.farClipPlane=500;
                session.ViewCamera.transform.rotation=Quaternion.Euler(55,0,0);
                session.ViewCamera.transform.position=DSBlockoutMesh.World(center,data.world_scale)-session.ViewCamera.transform.forward*150;
                Save(root,scene,"Basic",BasicPrefab);
                var visual=new GameObject("Art028_VisualOnly").transform;visual.SetParent(session.StaticGeometry,false);
                var grass=Mat("MeadowGreen",new Color(.24f,.34f,.19f));var grassLight=Mat("MeadowTufts",new Color(.29f,.38f,.21f));var dirt=Mat("DryGround",new Color(.34f,.39f,.25f));
                var contact=Mat("StoneContact",new Color(.15f,.20f,.13f));
                var stones=new[]{Mat("StoneWarm",new Color(.52f,.56f,.52f)),Mat("StoneLight",new Color(.61f,.63f,.58f)),Mat("StoneShade",new Color(.44f,.49f,.45f))};
                var selected=data.objects.Where(o=>o.points_px.Any(p=>Region.Contains(p))).ToArray();record.objects=selected.Select(o=>o.id).ToArray();Journal();
                foreach(var o in selected)
                {
                    var old=session.StaticGeometry.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name==o.id);old.enabled=false;
                    var p=o.points_px.Select(v=>data.Logical(v,4)).ToArray();
                    var foot=DSBlockoutMesh.Prism(p,data.world_scale,.11f,true);foot.name=o.id+"_Contact";Asset(foot,foot.name+".asset");MeshObject("Contact_"+o.id,visual,foot,contact);
                    var mesh=Stone(p,data.world_scale,o.height_logical);mesh.name=o.id+"_Faceted";Asset(mesh,mesh.name+".asset");
                    MeshObject(o.id+"_Art",visual,mesh,stones[Array.IndexOf(selected,o)%3]);
                    // Small low vegetation clusters share the stone palette; no separate floating cap.

                }
                foreach(var r in session.RockHolder.GetComponentsInChildren<Renderer>(true))r.enabled=false;
                var corners=new[]{new Vector2(140,105),new Vector2(420,105),new Vector2(420,340),new Vector2(140,340)}.Select(p=>data.Logical(p,4)).ToArray();
                var floor=DSBlockoutMesh.Prism(corners,data.world_scale,.08f,true);Asset(floor,"MeadowFloor.asset");MeshObject("MeadowFloor",visual,floor,grass);
                var tuft=new Mesh{name="Low grass tufts"};tuft.vertices=new[]{new Vector3(-1,0,0),new Vector3(1,0,0),new Vector3(.1f,.08f,0),new Vector3(0,0,-1),new Vector3(0,0,1),new Vector3(0,.065f,.1f),new Vector3(-.6f,0,-.6f),new Vector3(.6f,0,.6f),new Vector3(.1f,.095f,.1f)};tuft.triangles=Enumerable.Range(0,9).ToArray();tuft.RecalculateNormals();tuft.RecalculateBounds();Asset(tuft,"GrassTuft.asset");grassLight.SetFloat("_Cull",0);EditorUtility.SetDirty(grassLight);
                var rng=new System.Random(28001);var sharedPatch=DSBlockoutMesh.Prism(Enumerable.Range(0,12).Select(i=>{float angle=i*Mathf.PI/6;float r=1+.19f*Mathf.Sin(i*2.7f);return new Vector2(Mathf.Cos(angle)*r,Mathf.Sin(angle)*r);}).ToArray(),1,.005f);Asset(sharedPatch,"GroundPatch.asset");
                for(int i=0;i<340;i++)
                {
                    var px=new Vector2(145+(float)rng.NextDouble()*270,110+(float)rng.NextDouble()*225);var q=data.Logical(px,4);
                    
                    bool dry=i%4==0;float radius=dry?.18f+(float)rng.NextDouble()*.26f:.025f+(float)rng.NextDouble()*.06f;float aspect=.55f+(float)rng.NextDouble()*.5f;
                    if(!geometry.SafeSweep(q,q,radius*1.19f*Mathf.Max(1,aspect)/data.world_scale+1))continue;
                    var go=MeshObject((dry?"DryPatch":"GrassPatch")+i,visual,dry?sharedPatch:tuft,dry?dirt:grassLight);
                    go.transform.localPosition=DSBlockoutMesh.World(q,data.world_scale,.16f);
                    go.transform.localScale=new Vector3(radius,1,radius*aspect);
                }
                var vis=session.Visibility;vis.Blockers=session.StaticGeometry.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled && (r.name.StartsWith("OB-",StringComparison.Ordinal))).Concat(session.RockHolder.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled)).Distinct().ToArray();
                if(vis.Blockers.Any(r=>!r || r.sharedMaterials.Any(m=>!m || !m.HasProperty("_BaseColor"))))throw new Exception("BLOCKER_MATERIAL_INVALID");
                if(root.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("UNEXPECTED_COLLIDER");
                Save(root,scene,"Art",ArtPrefab);
                AssetDatabase.SaveAssets();
                foreach(string variant in new[]{"Basic","Art"})
                {
                    EditorSceneManager.OpenScene(Folder+"/Map028_"+variant+".unity",OpenSceneMode.Single);
                    var bound=UnityEngine.Object.FindFirstObjectByType<DSMapSession>();
                    if(!bound || !bound.Layout || !bound.ViewCamera || !bound.StaticGeometry || !bound.RockHolder || !bound.Visibility || bound.StartCenter!=center || bound.ExpectedDataSha!=DSMapBlockoutPreview.Hash(bound.Layout.text))throw new Exception("SAVED_BINDING_INVALID:"+variant);
                    if(bound.Visibility.Blockers.Any(r=>!r || r.sharedMaterials.Any(m=>!m)))throw new Exception("SAVED_RENDERER_INVALID");
                    if(RenderSettings.ambientMode!=AmbientMode.Trilight || RenderSettings.ambientSkyColor!=new Color(.47f,.56f,.62f))throw new Exception("SAVED_LIGHTING_INVALID");
                }
                foreach(var row in original)if(Hash(row.path)!=row.sha256)throw new Exception("ORIGINAL_CHANGED:"+row.path);
                foreach(var row in record.files){NoLinks(row.path);if(!File.Exists(row.path))throw new Exception("OWNED_OUTPUT_MISSING:"+row.path);row.sha256=Hash(row.path);}
                record.status="PASS_AUTHORING_ONLY";Journal();return output;
            }
            catch(Exception e){record.status="FAILED_PARTIAL_PRESERVED";record.error=e.Message;Journal();throw;}
            finally {Restore(saved);}
        }
        [Serializable] class OriginalRows {public Row[] files;}
        static void Save(GameObject root,UnityEngine.SceneManagement.Scene scene,string name,string prefab)
        {
            record.planned.Add(prefab);Journal();PrefabUtility.SaveAsPrefabAsset(root,prefab,out bool ok);if(!ok)throw new Exception("PREFAB_SAVE_FAILED");Confirm(prefab);
            string path=Folder+"/Map028_"+name+".unity";record.planned.Add(path);Journal();if(!EditorSceneManager.SaveScene(scene,path))throw new Exception("SCENE_SAVE_FAILED");Confirm(path);
        }
        [Serializable] public class VertexBits {public string id;public int[] xyz;}
        [Serializable] public class MeshExport {public string scope="VISUAL_MESH_CONTAINMENT_ONLY",layout_sha256,exporter_sha256;public double k;public VertexBits[] bits;public Row[] files;public DSMapBlockoutVerification.MeshRecord[] meshes;}
        public static string ExportMeshes()
        {
            NoLinks(Folder);NoLinks("output/map-art-028");
            string layout="Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json";
            var meshes=AssetDatabase.FindAssets("t:Mesh",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("_Faceted.asset",StringComparison.Ordinal)).OrderBy(p=>p).ToArray();
            if(meshes.Length!=5)throw new Exception("ART_MESH_SET_MISSING");
            var export=new MeshExport{layout_sha256=Hash(layout),exporter_sha256=Hash("Assets/DeadSurvivor/Editor/DSMapArtVerification.cs"),k=(double).02f,files=meshes.Select(p=>new Row{path=p,sha256=Hash(p)}).ToArray(),meshes=meshes.Select(p=>{var m=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(EditorUtility.IsDirty(m))throw new Exception("DIRTY_EXPORTED_MESH");return new DSMapBlockoutVerification.MeshRecord{id=m.name,vertices=m.vertices,triangles=m.triangles};}).ToArray()};
            export.bits=export.meshes.Select(m=>new VertexBits{id=m.id,xyz=m.vertices.SelectMany(v=>new[]{v.x,v.y,v.z}).Select(v=>BitConverter.ToInt32(BitConverter.GetBytes(v),0)).ToArray()}).ToArray();
            foreach(var file in export.files)if(Hash(file.path)!=file.sha256 || EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<Mesh>(file.path)))throw new Exception("EXPORT_SOURCE_CHANGED");
            string output="output/map-art-028/mesh-export-"+Guid.NewGuid().ToString("N")+".json";if(File.Exists(output))throw new Exception("AUDIT_DESTINATION_EXISTS");File.WriteAllText(output,JsonUtility.ToJson(export,true));return output;
        }
        static List<Vector2> Clip(List<Vector2> p,int axis,float bound,bool lower)
        {
            var result=new List<Vector2>();if(p.Count==0)return result;
            float V(Vector2 q)=>axis==0?q.x:q.y;
            bool In(Vector2 q)=>lower?V(q)>=bound:V(q)<=bound;
            var a=p[p.Count-1];bool ai=In(a);
            foreach(var b in p){bool bi=In(b);if(ai!=bi){float t=(bound-V(a))/(V(b)-V(a));var cut=Vector2.LerpUnclamped(a,b,t);if(axis==0)cut.x=bound;else cut.y=bound;result.Add(cut);}if(bi)result.Add(b);a=b;ai=bi;}
            for(int i=result.Count-1;i>0;i--)if((result[i]-result[i-1]).sqrMagnitude<1e-10f)result.RemoveAt(i);
            if(result.Count>1 && (result[0]-result[result.Count-1]).sqrMagnitude<1e-10f)result.RemoveAt(result.Count-1);
            return result;
        }
        static List<Vector2> Hull(IEnumerable<Vector2> points)
        {
            var p=points.Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();var lower=new List<Vector2>();var upper=new List<Vector2>();
            double Cross(Vector2 a,Vector2 b,Vector2 c)=>((double)b.x-a.x)*((double)c.y-a.y)-((double)b.y-a.y)*((double)c.x-a.x);
            foreach(var q in p){while(lower.Count>1 && Cross(lower[lower.Count-2],lower[lower.Count-1],q)<=0)lower.RemoveAt(lower.Count-1);lower.Add(q);}
            foreach(var q in p.Reverse()){while(upper.Count>1 && Cross(upper[upper.Count-2],upper[upper.Count-1],q)<=0)upper.RemoveAt(upper.Count-1);upper.Add(q);}
            if(lower.Count>0)lower.RemoveAt(lower.Count-1);if(upper.Count>0)upper.RemoveAt(upper.Count-1);lower.AddRange(upper);return lower;
        }
        static Mesh Stone(Vector2[] p,float k,float height)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var source=DSBlockoutMesh.Triangulate(p);
            var cells=new Dictionary<(int x,int y),List<List<Vector2>>>();const float cell=42;
            var sourceMap=new DSMapGeometry(p,Array.Empty<Vector2[]>(),.001,16384);
            for(int t=0;t<source.Length;t+=3)
            {
                var tri=new[]{p[source[t]],p[source[t+1]],p[source[t+2]]};
                for(int x=Mathf.FloorToInt(tri.Min(v=>v.x)/cell);x<=Mathf.FloorToInt(tri.Max(v=>v.x)/cell);x++)
                for(int y=Mathf.FloorToInt(tri.Min(v=>v.y)/cell);y<=Mathf.FloorToInt(tri.Max(v=>v.y)/cell);y++)
                {
                    var poly=Clip(tri.ToList(),0,x*cell,true);poly=Clip(poly,0,(x+1)*cell,false);poly=Clip(poly,1,y*cell,true);poly=Clip(poly,1,(y+1)*cell,false);
                    if(poly.Count<3 || Math.Abs(DSBlockoutMesh.Area(poly.ToArray()))<.01)continue;
                    if(!cells.TryGetValue((x,y),out var pieces))cells[(x,y)]=pieces=new List<List<Vector2>>();pieces.Add(poly);
                }
            }
            foreach(var entry in cells.OrderBy(c=>c.Key.x).ThenBy(c=>c.Key.y))
            {
                var hull=Hull(entry.Value.SelectMany(v=>v));double area=entry.Value.Sum(v=>Math.Abs(DSBlockoutMesh.Area(v.ToArray())));
                // Merge only when the hull has no meaningful extra area over the disjoint source cover.
                var hc=hull.Aggregate(Vector2.zero,(a,b)=>a+b)/hull.Count;
                var candidate=hull.Select(v=>Vector2.Lerp(hc,v,.96f)).ToArray();
                bool contained=Enumerable.Range(0,candidate.Length).All(i=>sourceMap.SafeSweep(candidate[i],candidate[(i+1)%candidate.Length],.01));
                var pieces=contained && Math.Abs(Math.Abs(DSBlockoutMesh.Area(hull.ToArray()))-area)<.0001?new List<List<Vector2>>{hull}:entry.Value;
                foreach(var poly in pieces)
                {
                    if(DSBlockoutMesh.Area(poly.ToArray())<0)poly.Reverse();
                    var center=poly.Aggregate(Vector2.zero,(a,b)=>a+b)/poly.Count;
                    var rounded=new List<Vector2>();
                    for(int i=0;i<poly.Count;i++){rounded.Add(Vector2.Lerp(poly[i],poly[(i+poly.Count-1)%poly.Count],.20f));rounded.Add(Vector2.Lerp(poly[i],poly[(i+1)%poly.Count],.20f));}
                    float top=height*.70f+((entry.Key.x*17+entry.Key.y*31)&7);int n=rounded.Count;
                    var ring=rounded.Select(v=>Vector2.Lerp(center,v,.96f)).ToArray();var upper=ring.Select(v=>Vector2.Lerp(center,v,.78f)).ToArray();
                    void Face(Vector3 a,Vector3 b,Vector3 c){if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-12f)return;int v=vertices.Count;vertices.AddRange(new[]{a,b,c});triangles.AddRange(new[]{v,v+1,v+2});}
                    for(int i=0;i<n;i++)
                    {
                        int j=(i+1)%n;var a=DSBlockoutMesh.World(ring[i],k,0);var b=DSBlockoutMesh.World(ring[j],k,0);var c=DSBlockoutMesh.World(upper[j],k,top);var d=DSBlockoutMesh.World(upper[i],k,top);
                        Face(a,b,c);Face(a,c,d);Face(d,c,DSBlockoutMesh.World(center,k,top));
                    }
                }
            }
            var mesh=new Mesh{name="Rounded low stone clusters"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
