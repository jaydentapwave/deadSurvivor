using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

// 042: isolated test scene comparing the game's 2D billboard sprites with the
// Tripo 3D hero (rigged, idle/run) and 3D melee monster at the game camera
// (orthographic size 6, pitch 55). Does not touch game scenes, code or data.
public static class CharMonster042
{
 const string Out="output/char-monster-042/r001";
 const string Art="Assets/DeadSurvivor/Art/Characters/Test042_r001";
 const string SceneDir="Assets/DeadSurvivor/Scenes/Test042_CharMonster_r001";
 const string ScenePath=SceneDir+"/Test042_CharMonster.unity";
 const string Sprites="Assets/DeadSurvivor/Resources/DeadSurvivor/Sprites";
 const float Pitch=55f,Ortho=6f,GameHeight=.72f;
 const int W=1280,H=720;
 static Camera cam;

 static void CopyNew(string src,string dst){if(File.Exists(dst))throw new Exception("OUTPUT_EXISTS:"+dst);File.Copy(src,dst);}
 static Bounds RendererBounds(GameObject go){var rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new Exception("NO_RENDERER:"+go.name);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
 static float ScreenHeightPx(Bounds b){var ys=new List<float>();for(int i=0;i<8;i++){var p=new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z);ys.Add(cam.WorldToViewportPoint(p).y*H);}return ys.Max()-ys.Min();}
 static float OpaqueFraction(string png){var t=new Texture2D(2,2);t.LoadImage(File.ReadAllBytes(png));var px=t.GetPixels32();int top=-1,bot=-1;for(int y=0;y<t.height;y++){bool any=false;for(int x=0;x<t.width;x++)if(px[y*t.width+x].a>16){any=true;break;}if(any){if(bot<0)bot=y;top=y;}}float f=(top-bot+1)/(float)t.height;UnityEngine.Object.DestroyImmediate(t);return f;}
 static Material Mat(string name,Texture2D tex){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};if(tex)m.SetTexture("_BaseMap",tex);m.SetColor("_BaseColor",Color.white);m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(m,Art+"/"+name+".mat");return m;}
 static Texture2D Tex(string path){var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Default;t.sRGBTexture=true;t.maxTextureSize=1024;t.mipmapEnabled=true;t.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}

 // Wrapper root: model bottom on y=0 and centered, uniform scale so the model is `height` tall.
 static GameObject Wrapped(GameObject asset,Material mat,string name,float height)
 {
  var root=new GameObject(name);var inst=(GameObject)PrefabUtility.InstantiatePrefab(asset);inst.transform.SetParent(root.transform,false);
  foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;}
  foreach(var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))s.updateWhenOffscreen=true;
  var b=RendererBounds(root);float k=height/b.size.y;inst.transform.localScale=Vector3.one*k;
  b=RendererBounds(root);inst.transform.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);
  return root;
 }
 static GameObject Billboard(Sprite s,string name,Vector3 ground,float height)
 {
  var go=new GameObject(name,typeof(SpriteRenderer));var r=go.GetComponent<SpriteRenderer>();r.sprite=s;
  go.transform.rotation=cam.transform.rotation;float scale=height/Mathf.Max(.001f,s.bounds.size.y);go.transform.localScale=Vector3.one*scale;
  go.transform.position=ground+Vector3.up*.01f+cam.transform.up*(-s.bounds.min.y*scale);return go;
 }
 static AnimatorController Controller(string name,AnimationClip clip){var c=AnimatorController.CreateAnimatorControllerAtPath(Art+"/"+name+".controller");var st=c.layers[0].stateMachine.AddState(clip.name);st.motion=clip;c.layers[0].stateMachine.defaultState=st;return c;}

 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE:"+EditorSceneManager.GetSceneAt(i).path);
  if(File.Exists(ScenePath)||Directory.Exists(Art))throw new Exception("OUTPUT_EXISTS");
  var setup=EditorSceneManager.GetSceneManagerSetup();
  var heroFbx=Directory.GetFiles(Out+"/tripo/hero/anim","*.fbx",SearchOption.AllDirectories).OrderBy(x=>x).ToArray();
  var heroTex=Out+"/tripo/hero/fbx/basecolor.png";
  if(heroFbx.Length==0||!File.Exists(heroTex))throw new Exception("MISSING_HERO_INPUT");
  Directory.CreateDirectory(Art);Directory.CreateDirectory(SceneDir);
  var heroPaths=new List<string>();for(int i=0;i<heroFbx.Length;i++){var dst=Art+"/hero_"+Path.GetFileNameWithoutExtension(heroFbx[i])+".fbx";CopyNew(heroFbx[i],dst);heroPaths.Add(dst);}
  CopyNew(heroTex,Art+"/hero_basecolor.png");CopyNew(Out+"/tripo/monster/fbx/model.fbx",Art+"/monster.fbx");CopyNew(Out+"/tripo/monster/fbx/basecolor.png",Art+"/monster_basecolor.png");
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  foreach(var p in heroPaths){var imp=(ModelImporter)AssetImporter.GetAtPath(p);imp.animationType=ModelImporterAnimationType.Human;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.importAnimation=true;imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.importCameras=false;imp.importLights=false;
   var clips=imp.defaultClipAnimations;foreach(var c in clips){c.loopTime=true;c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;}imp.clipAnimations=clips;imp.SaveAndReimport();}
  {var imp=(ModelImporter)AssetImporter.GetAtPath(Art+"/monster.fbx");imp.animationType=ModelImporterAnimationType.None;imp.importAnimation=false;imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.importCameras=false;imp.importLights=false;imp.SaveAndReimport();}
  var heroMat=Mat("Hero042",Tex(Art+"/hero_basecolor.png"));var monMat=Mat("Monster042",Tex(Art+"/monster_basecolor.png"));
  var heroAsset=AssetDatabase.LoadAssetAtPath<GameObject>(heroPaths[0]);var monAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/monster.fbx");
  var clipsAll=heroPaths.SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>()).Where(c=>!c.name.StartsWith("__preview__")).ToList();
  var avatar=heroPaths.Select(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().FirstOrDefault()).FirstOrDefault(a=>a!=null);
  AnimationClip Pick(string key)=>clipsAll.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains(key))??clipsAll.FirstOrDefault(c=>heroPaths.Any(p=>p.ToLowerInvariant().Contains(key)&&AssetDatabase.LoadAllAssetsAtPath(p).Contains(c)));
  var idle=Pick("idle");var run=Pick("run");if(idle==null||run==null)throw new Exception("MISSING_CLIP:"+string.Join(",",clipsAll.Select(c=>c.name)));
  var idleCtl=Controller("Hero042_Idle",idle);var runCtl=Controller("Hero042_Run",run);

  try{
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var camGo=new GameObject("GameCamera042");cam=camGo.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=Ortho;cam.nearClipPlane=.1f;cam.farClipPlane=500;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.18f,.26f,.18f);
  camGo.transform.rotation=Quaternion.Euler(Pitch,0,0);camGo.transform.position=-camGo.transform.forward*20f;
  var sun=new GameObject("Sun042").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(50,-35,0);sun.color=new Color(1f,.96f,.88f);sun.intensity=1.25f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.65f;
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.74f,.85f,.94f);RenderSettings.ambientEquatorColor=new Color(.62f,.76f,.55f);RenderSettings.ambientGroundColor=new Color(.33f,.42f,.27f);
  var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Ground042";ground.transform.localScale=new Vector3(6,1,6);UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
  var gm=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Ground042"};gm.SetColor("_BaseColor",new Color32(0x45,0x9f,0x4a,255));gm.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(gm,Art+"/Ground042.mat");ground.GetComponent<Renderer>().sharedMaterial=gm;

  // Reference heights from the game's billboard sprites (visible, opaque part only).
  string[] heroes={"bruno","hans","lena","yuki"};
  float spriteFrac=OpaqueFraction(Sprites+"/Characters/bruno.png"),monFrac=OpaqueFraction(Sprites+"/Monsters/melee.png");
  float pxPerUnit=H/(2*Ortho);float heroSpritePx=GameHeight*spriteFrac*pxPerUnit,monSpritePx=GameHeight*monFrac*pxPerUnit;
  Vector3[] party={new Vector3(-.45f,0,.45f),new Vector3(.45f,0,.45f),new Vector3(-.45f,0,-.45f),new Vector3(.45f,0,-.45f)};
  Vector3[] foes={new Vector3(-2.1f,0,.6f),new Vector3(-1.9f,0,-.9f),new Vector3(2.0f,0,.8f),new Vector3(2.2f,0,-.7f)};
  var groups=new[]{new{x=-6.5f,label="2D sprite (current game)"},new{x=0f,label="3D literal 0.72"},new{x=6.5f,label="3D screen-matched"}};
  var placed=new List<object>();float heroLiteralPx=0,monLiteralPx=0,heroScale=1,monScale=1;
  AnimationMode.StartAnimationMode();
  try{
   for(int g=0;g<3;g++){var c=new Vector3(groups[g].x,0,0);
    for(int i=0;i<4;i++){
     if(g==0){Billboard(AssetDatabase.LoadAssetAtPath<Sprite>(Sprites+"/Characters/"+heroes[i]+".png"),"Sprite_"+heroes[i],c+party[i],GameHeight);Billboard(AssetDatabase.LoadAssetAtPath<Sprite>(Sprites+"/Monsters/melee.png"),"Sprite_melee_"+i,c+foes[i],GameHeight);continue;}
     var h=Wrapped(heroAsset,heroMat,"Hero3D_g"+g+"_"+i,GameHeight);var m=Wrapped(monAsset,monMat,"Melee3D_g"+g+"_"+i,GameHeight);
     h.transform.position=c+party[i];m.transform.position=c+foes[i];m.transform.rotation=Quaternion.Euler(0,foes[i].x<0?90:-90,0);
     var an=h.GetComponentInChildren<Animator>()??h.transform.GetChild(0).gameObject.AddComponent<Animator>();an.avatar=avatar;bool running=g==2&&i>=2;an.runtimeAnimatorController=running?runCtl:idleCtl;an.applyRootMotion=false;
     AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(an.gameObject,running?run:idle,running?run.length*.25f:idle.length*.3f);AnimationMode.EndSampling();
     if(g==1&&i==0){heroLiteralPx=ScreenHeightPx(RendererBounds(h));monLiteralPx=ScreenHeightPx(RendererBounds(m));heroScale=heroSpritePx/heroLiteralPx;monScale=monSpritePx/monLiteralPx;}
     if(g==2){h.transform.localScale=Vector3.one*heroScale;m.transform.localScale=Vector3.one*monScale;}
     placed.Add(new{group=groups[g].label,hero=h.name,monster=m.name,running});
    }
   }
   float heroMatchedPx=ScreenHeightPx(RendererBounds(GameObject.Find("Hero3D_g2_0"))),monMatchedPx=ScreenHeightPx(RendererBounds(GameObject.Find("Melee3D_g2_0")));
   var rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32){antiAliasing=4};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var shot=new Texture2D(W,H,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,W,H),0,0);shot.Apply();
   Directory.CreateDirectory(Out+"/unity");File.WriteAllBytes(Out+"/unity/capture-day-r001.png",shot.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(shot);
   int Tris(GameObject a)=>a.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3)+a.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(s=>s.sharedMesh.triangles.Length/3);
   var metrics=new{status="BUILT",scene=ScenePath,heroAssets=heroPaths,heroTriangles=Tris(heroAsset),monsterTriangles=Tris(monAsset),avatarValid=avatar!=null&&avatar.isValid,avatarHuman=avatar!=null&&avatar.isHuman,
    clips=clipsAll.Select(c=>new{c.name,c.length,loop=c.isLooping,c.isHumanMotion}).ToArray(),idleClip=idle.name,runClip=run.name,
    camera=new{orthographic=true,size=Ortho,pitch=Pitch},capture=Out+"/unity/capture-day-r001.png",
    heights=new{spriteOpaqueFractionHero=spriteFrac,spriteOpaqueFractionMelee=monFrac,heroSpritePx,monSpritePx,heroLiteralPx,monLiteralPx,heroMatchedPx,monMatchedPx,heroScaleToMatch=heroScale,monsterScaleToMatch=monScale},placed};
   File.WriteAllText(Out+"/unity/build-metrics.json",JsonConvert.SerializeObject(metrics,Formatting.Indented));
  }finally{AnimationMode.StopAnimationMode();}
  EditorSceneManager.SaveScene(scene,ScenePath);
  // Save only assets this script created; a global SaveAssets() also wrote the dynamic TMP font cache (PrototypeFont.asset) in r001.
  foreach(var a in new UnityEngine.Object[]{heroMat,monMat,gm,idleCtl,runCtl})AssetDatabase.SaveAssetIfDirty(a);
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
  return JsonConvert.DeserializeObject(File.ReadAllText(Out+"/unity/build-metrics.json"));
 }
}
