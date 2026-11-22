using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Review-specific evidence. No saved scene/material edits; all fixtures restore in finally.
public static class DQBackgroundRecheck
{
    private const string Out = "output/hd2d-dq/apply/6/recheck";
    public static void CheckGeometryAndFirstRunRollback()
    {
        Directory.CreateDirectory(Out);
        var layout = new FieldLayoutData { path = new FieldPathLayout { points = new[] {new Vector2(-10,-5),new Vector2(10,-5)}, branch = Array.Empty<Vector2>() }, water = Array.Empty<FieldWaterLayout>(), placements = Array.Empty<FieldLayoutPlacement>() };
        var roi = new Rect(1,-6.5f,2,3);
        var pathReject = !DQBackgroundVerification.IsGrassRectangleClear(layout, roi);
        layout.path.points = Array.Empty<Vector2>();
        layout.water = new[] {new FieldWaterLayout {name="pond",points=new[]{new Vector2(-10,-10),new Vector2(10,-10),new Vector2(10,10),new Vector2(-10,10)}}};
        var pondReject = !DQBackgroundVerification.IsGrassRectangleClear(layout, roi);
        layout.water = Array.Empty<FieldWaterLayout>();
        layout.path.points = new[] {new Vector2(2,-5),new Vector2(2,-5)};
        var duplicateReject = !DQBackgroundVerification.IsGrassRectangleClear(layout, roi);
        var method = typeof(MainSceneDQBackgroundMigration).GetMethod("PathDistance",BindingFlags.Static|BindingFlags.NonPublic);
        var pointDistance = (float)method.Invoke(null,new object[]{Vector2.zero,new[]{new Vector2(3,4)}});
        var duplicateDistance = (float)method.Invoke(null,new object[]{Vector2.zero,new[]{new Vector2(3,4),new Vector2(3,4)}});
        var root = MainSceneDQBackgroundMigration.Root;
        var backup = Out + "/first-run-backup";
        if (Directory.Exists(backup)) throw new InvalidOperationException("Rollback backup already exists.");
        var meta = File.ReadAllBytes(root+".meta");
        var scenePath = EditorSceneManager.GetActiveScene().path;
        var sceneBytes = File.ReadAllBytes(MainSceneDQScreenMigration.ScenePath);
        var dataPaths = new[]{"Assets/Game/03.Resources/Resources/Data/Field/field_height.json","Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"};
        var dataBytes = dataPaths.ToDictionary(p=>p,File.ReadAllBytes);
        bool absent = false, caught = false;
        try
        {
            // Relocate owned generated outputs, without deleting any game/source data.
            Directory.Move(root,backup); File.Move(root+".meta",backup+".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            try { MainSceneDQBackgroundMigration.ApplyForFailureTest("textures"); }
            catch(InvalidOperationException e) when(e.Message.Contains("Injected background texture failure")) { caught=true; }
            absent = !Directory.Exists(root) && !File.Exists(root+".meta");
        }
        finally
        {
            if (Directory.Exists(root)) throw new InvalidOperationException("Failed root remains; backup retained at "+backup);
            Directory.Move(backup,root); File.Move(backup+".meta",root+".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(scenePath);
        }
        var bytesRestored = File.ReadAllBytes(root+".meta").SequenceEqual(meta) && File.ReadAllBytes(MainSceneDQScreenMigration.ScenePath).SequenceEqual(sceneBytes) && dataBytes.All(p=>File.ReadAllBytes(p.Key).SequenceEqual(p.Value));
        var result = new {utc=DateTime.UtcNow.ToString("O"),pathReject,pondReject,duplicateReject,pointDistance,duplicateDistance,firstRunFailureCaught=caught,newRootAndMetaAbsentBeforeRestore=absent,bytesRestored};
        File.WriteAllText(Out+"/geometry-and-first-run.json",JsonConvert.SerializeObject(result,Formatting.Indented));
        if(!pathReject||!pondReject||!duplicateReject||pointDistance!=5||duplicateDistance!=5||!caught||!absent||!bytesRestored) throw new InvalidOperationException("Review-specific regression checks failed.");
    }

    private static Mesh Panel(Vector3 center, Vector3 right, Vector3 up, float referenceHeight)
    {
        var mesh = new Mesh {name="Nonzero terrain reference fixture", hideFlags=HideFlags.DontSave};
        mesh.vertices=new[]{center-right-up,center-right+up,center+right+up,center+right-up};
        mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
        mesh.uv2=Enumerable.Repeat(new Vector2(referenceHeight,0),4).ToArray();
        mesh.triangles=new[]{0,1,2,0,2,3,2,1,0,3,2,0};mesh.RecalculateNormals();return mesh;
    }
    public static void CaptureGpuDepth()
    {
        if(!Application.isPlaying) throw new InvalidOperationException("Requires Play.");
        Directory.CreateDirectory(Out);
        var camera=Camera.main;
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldAspect=camera.aspect;var oldBackground=camera.backgroundColor;
        var data=camera.GetUniversalAdditionalCameraData();var oldPost=data.renderPostProcessing;
        var features=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MainSceneDQScreenMigration.RendererPath).rendererFeatures;
        var states=features.Select(f=>f.isActive).ToArray();
        var renderers=Object.FindObjectsByType<Renderer>();var enabled=renderers.Select(r=>r.enabled).ToArray();
        var created=new List<Object>();var rows=new List<object>();
        Texture2D Render(string label)
        {
            var rt=RenderTexture.GetTemporary(640,480,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var active=RenderTexture.active;
            var image=new Texture2D(640,480,TextureFormat.RGBA32,false);created.Add(image);
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,480),0,0);image.Apply();File.WriteAllBytes(Out+"/gpu-"+label+".png",image.EncodeToPNG());return image;}
            finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }
        GameObject Make(string name,Mesh mesh,Material material)
        {
            // Transparent Cull Off surfaces must not contain duplicate reverse faces:
            // both windings would blend twice, unlike the production water meshes.
            if(material.shader.name=="DeadSurvivor/DQ/Main water")mesh.triangles=mesh.triangles.Take(6).ToArray();
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)){hideFlags=HideFlags.DontSave,layer=HD2DBackgroundRig.BackgroundLayer};created.Add(go);created.Add(mesh);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        try
        {
            foreach(var r in renderers)r.enabled=false;foreach(var feature in features)feature.SetActive(false);data.renderPostProcessing=false;
            camera.aspect=4f/3;camera.backgroundColor=Color.black;camera.transform.rotation=Quaternion.Euler(18,0,0);camera.transform.position=new Vector3(0,1+10*Mathf.Tan(18*Mathf.Deg2Rad),-10);
            var center=new Vector3(0,1,0);var right=camera.transform.right*.45f;var up=camera.transform.up*.45f;var forward=camera.transform.forward;
            var red=new Material(Shader.Find("Universal Render Pipeline/Unlit"));red.SetColor("_BaseColor",Color.red);created.Add(red);
            var marker=Make("Between terrain-reference and zero-reference depth",Panel(center+forward*.17f,right,up,0),red);
            foreach(var name in new[]{"Wood","Wood_Fade","Ground"})
            {
                var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(MainSceneDQBackgroundMigration.Root+"/"+name+".mat"));created.Add(material);
                material.SetFloat("_FlatDepth",1);material.SetFloat("_Category",5);material.SetFloat("_DQModel",0);material.SetTexture("_MainTex",Texture2D.whiteTexture);material.SetColor("_AlbedoTint",Color.blue);material.SetFloat("_Cull",0);
                if(name=="Wood_Fade"){material.SetInt("_OcclusionRectCount",1);material.SetVectorArray("_OcclusionRects",new[]{new Vector4(0,0,1,1)});}
                Mesh probe;
                if(name=="Ground")
                {
                    var mm=Enumerable.Range(0,25).Select(i=>750+(i%5-2)*10+(i/5-2)*20).ToArray();
                    var grid=TerrainHeightGrid.FromMillimetres(1,-2,-2,5,5,mm);
                    probe=GroundDecalMath.Build(grid,Vector3.zero,new Vector2(.9f,3),0,.25f,-.025f);
                    probe.uv2=probe.vertices.Select(v=>new Vector2(v.y+.025f,0)).ToArray();
                    camera.transform.position-=Vector3.up*.275f;
                    var comparison=Object.Instantiate(probe);created.Add(comparison);
                    comparison.vertices=comparison.vertices.Select(v=>v+forward*.13f).ToArray();
                    marker.GetComponent<MeshFilter>().sharedMesh=comparison;
                }
                else probe=Panel(center,right,up,.75f);
                var panel=Make(name+" height .75",probe,material);
                material.EnableKeyword("_TERRAIN_HEIGHT");var a=Render(name+"-height075");
                material.DisableKeyword("_TERRAIN_HEIGHT");var b=Render(name+"-zero-control");
                var colors=a.GetPixels();var zeros=b.GetPixels();int blue=0,redControl=0;
                for(var y=215;y<265;y++)for(var x=295;x<345;x++){var c=colors[y*640+x];var z=zeros[y*640+x];if(c.b>c.r*2&&c.b>.1f)blue++;if(z.r>z.b*2&&z.r>.1f)redControl++;}
                rows.Add(new{slot=name,referenceHeight=.75f,physicalCenterHeight=name=="Ground"?.725f:1,blueReferencePixels=blue,redZeroControlPixels=redControl,pass=blue>500&&redControl>2400, meshKind=name=="Ground"?"Production apron builder, nonflat grid, lift -.025, uv2=vertex.y+.025":"Panel with terrain reference UV2"});panel.SetActive(false);
                if(name=="Ground"){camera.transform.position+=Vector3.up*.275f;}
            }
            marker.SetActive(false);
            // Actual actor shader + unchanged sprite/tilt; repeated with water disabled
            // and enabled at front/back depths. Bridge uses its production material.
            var body=Object.FindObjectsByType<MemberView>().First().BodyRenderer;
            var actorGo=new GameObject("GPU actual actor sprite",typeof(SpriteRenderer)){hideFlags=HideFlags.DontSave,layer=body.gameObject.layer};created.Add(actorGo);
            var actor=actorGo.GetComponent<SpriteRenderer>();actor.sprite=body.sprite;actor.sharedMaterial=body.sharedMaterial;actor.color=body.color;actorGo.transform.rotation=body.transform.rotation;actorGo.transform.localScale=body.transform.lossyScale;
            actorGo.transform.position=center;
            var waterMaterial=new Material(AssetDatabase.LoadAssetAtPath<Material>(MainSceneDQBackgroundMigration.Root+"/Water.mat"));created.Add(waterMaterial);waterMaterial.SetTexture("_WaterMask",Texture2D.whiteTexture);
            var water=Make("Actual DQ water",Panel(center,right*3,up*3,0),waterMaterial);
            foreach(var kind in new[]{"actor","bridge"})
            {
                GameObject bridge=null;
                if(kind=="bridge")
                {
                    actorGo.SetActive(false);var bridgeMaterial=new Material(AssetDatabase.LoadAssetAtPath<Material>(MainSceneDQBackgroundMigration.Root+"/Bridge.mat"));created.Add(bridgeMaterial);bridgeMaterial.SetFloat("_FlatDepth",1);bridgeMaterial.EnableKeyword("_TERRAIN_HEIGHT");
                    bridge=Make("Bridge surface sample",Panel(center,right,up,1),bridgeMaterial);
                }
                water.SetActive(false);var baseline=Render(kind+"-baseline");water.SetActive(true);
                foreach(var side in new[]{"behind","front"})
                {
                    water.transform.position=forward*(side=="behind"?3f:-.5f);
                    var image=Render(kind+"-water-"+side);var pixels=image.GetPixels();var before=baseline.GetPixels();double diff=0;int count=0;
                    for(var y=200;y<280;y++)for(var x=290;x<350;x++) {var i=y*640+x;if(before[i].maxColorComponent<.1f)continue;diff+=Math.Abs(pixels[i].r-before[i].r)+Math.Abs(pixels[i].g-before[i].g)+Math.Abs(pixels[i].b-before[i].b);count++;}
                    rows.Add(new{slot=kind,waterSide=side,waterQueue=waterMaterial.renderQueue,comparedPixels=count,meanRgbDelta=count>0?diff/count:0});
                }
                if(bridge!=null)bridge.SetActive(false);
            }
            water.SetActive(false);
            var horizontalWater=Make("Horizontal production water",Panel(new Vector3(0,1,0),Vector3.right*1.5f,Vector3.forward*1.5f,1),waterMaterial);
            horizontalWater.SetActive(false);actorGo.SetActive(true);actorGo.transform.position=new Vector3(0,.75f,0);
            var actorBaseline=Render("actor-horizontal-baseline");horizontalWater.SetActive(true);var actorCross=Render("actor-horizontal-cross");
            var ab=actorBaseline.GetPixels();var ac=actorCross.GetPixels();var occupied=Enumerable.Range(0,ab.Length).Where(i=>ab[i].maxColorComponent>.1f).ToArray();
            var low=occupied.Min(i=>i/640);var high=occupied.Max(i=>i/640);
            double Delta(IEnumerable<int> pixels){var list=pixels.ToArray();return list.Average(i=>(double)(Mathf.Abs(ab[i].r-ac[i].r)+Mathf.Abs(ab[i].g-ac[i].g)+Mathf.Abs(ab[i].b-ac[i].b)));}
            var bottomDelta=Delta(occupied.Where(i=>i/640<low+(high-low)/3));var topDelta=Delta(occupied.Where(i=>i/640>high-(high-low)/3));
            rows.Add(new{slot="actor-horizontal-cross",waterHeight=1f,actorFootHeight=.75f,bottomThirdRgbDelta=bottomDelta,topThirdRgbDelta=topDelta});
            actorGo.SetActive(false);horizontalWater.SetActive(false);
            var topMaterial=new Material(AssetDatabase.LoadAssetAtPath<Material>(MainSceneDQBackgroundMigration.Root+"/Bridge.mat"));created.Add(topMaterial);topMaterial.EnableKeyword("_TERRAIN_HEIGHT");
            var top=Make("Bridge horizontal top",Panel(new Vector3(0,1.2f,0),Vector3.right*.6f,Vector3.forward*1.2f,.75f),topMaterial);
            var tb=Render("bridge-horizontal-baseline");horizontalWater.SetActive(true);var tc=Render("bridge-horizontal-cross");
            var tba=tb.GetPixels();var tca=tc.GetPixels();var indices=Enumerable.Range(0,tba.Length).Where(i=>tba[i].maxColorComponent>.1f).ToArray();
            rows.Add(new{slot="bridge-horizontal-cross",bridgeTopHeight=1.2f,waterHeight=1f,bridgeReferenceHeight=.75f,meanRgbDelta=indices.Average(i=>(double)(Mathf.Abs(tba[i].r-tca[i].r)+Mathf.Abs(tba[i].g-tca[i].g)+Mathf.Abs(tba[i].b-tca[i].b)))});
            // Regression for translucent shore masks: preserve alpha, change only RGB.
            top.SetActive(false);horizontalWater.SetActive(false);
            var greenMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));created.Add(greenMaterial);greenMaterial.SetColor("_BaseColor",Color.green);
            Make("Alpha denominator ground control",Panel(center+forward,right*3,up*3,0),greenMaterial);
            var greenBaseline=Render("shore-alpha-baseline");
            var openingTexture=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);created.Add(openingTexture);
            waterMaterial.SetTexture("_WaterMask",openingTexture);waterMaterial.SetFloat("_DiagnosticMask",1);water.transform.position=Vector3.zero;water.SetActive(true);
            foreach(var opening in new[]{.04f,.1f,1f})
            {
                openingTexture.SetPixel(0,0,new Color(opening,0,0,1));openingTexture.Apply();
                var image=Render("shore-alpha-"+opening.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
                var baselineGreen=Mathf.GammaToLinearSpace(greenBaseline.GetPixel(320,240).g);
                var greenRatio=Mathf.GammaToLinearSpace(image.GetPixel(320,240).g)/baselineGreen;
                var t=Mathf.Clamp01((opening-.05f)/.35f);var alpha=t*t*(3-2*t)*.78f;
                var expectedGreen=opening<.05f?1:1-alpha;
                rows.Add(new{slot="shore-alpha",opening,alpha,greenRatio,expectedGreen,pass=Mathf.Abs(greenRatio-expectedGreen)<.01f});
            }
            File.WriteAllText(Out+"/gpu-depth.json",JsonConvert.SerializeObject(new{utc=DateTime.UtcNow.ToString("O"),method="Actual production shaders; controlled geometry at nonzero reference height .75; red comparison plane between ref and zero depth; actual sprite shader and production bridge/water material. Diagnostic fixture separate from normal route.",rows},Formatting.Indented));
        }
        finally
        {
            foreach(var item in created.AsEnumerable().Reverse())if(item!=null)Object.DestroyImmediate(item);
            for(var i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=enabled[i];
            for(var i=0;i<features.Count;i++)features[i].SetActive(states[i]);data.renderPostProcessing=oldPost;
            camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.aspect=oldAspect;camera.backgroundColor=oldBackground;
        }
    }
}
