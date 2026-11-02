using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeadSurvivor;
using Newtonsoft.Json;

// 044: create the 3D-map main scene from Map038 (art + map) with combat enabled and
// make it the first build scene. Runtime code and data are not modified.
public static class ProtoMain044Build
{
 const string Source="Assets/DeadSurvivor/Scenes/Map038_TripoPair_r003/Map038_TripoPair.unity";
 const string Dir="Assets/DeadSurvivor/Scenes/Proto044";
 const string Target=Dir+"/Proto044_Main.unity";
 const string Legacy2D="Assets/DeadSurvivor/Scenes/DeadSurvivor.unity";
 const string Out="output/proto-main-044/r001";

 public static object Build()
 {
  if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||EditorApplication.isPlaying)throw new Exception("WRONG_TARGET_OR_PLAYING");
  for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE:"+EditorSceneManager.GetSceneAt(i).path);
  if(File.Exists(Target)||File.Exists(Out+"/build.json"))throw new Exception("OUTPUT_EXISTS");
  var buildBefore=EditorBuildSettings.scenes.Select(s=>new{s.path,s.enabled}).ToArray();
  if(buildBefore.Length!=1||buildBefore[0].path!=Legacy2D)throw new Exception("UNEXPECTED_BUILD_SCENES:"+JsonConvert.SerializeObject(buildBefore));
  Directory.CreateDirectory(Out);
  var setup=EditorSceneManager.GetSceneManagerSetup();
  object result;
  try{
   if(!AssetDatabase.IsValidFolder(Dir))AssetDatabase.CreateFolder("Assets/DeadSurvivor/Scenes","Proto044");
   if(!AssetDatabase.CopyAsset(Source,Target))throw new Exception("COPY_FAILED");
   var scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
   var sessions=UnityEngine.Object.FindObjectsByType<DSMapSession>();var games=UnityEngine.Object.FindObjectsByType<DSGame>();
   if(sessions.Length!=1||games.Length!=1||games[0].MapSession!=sessions[0])throw new Exception("UNEXPECTED_SCENE_SHAPE:"+sessions.Length+"/"+games.Length);
   var so=new SerializedObject(sessions[0]);var combat=so.FindProperty("CombatEnabled");bool before=combat.boolValue;combat.boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
   EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("SAVE_FAILED");
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Target,true),new EditorBuildSettingsScene(Legacy2D,true)};
   AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<SceneAsset>(Target));
   var s=sessions[0];
   result=new{status="BUILT",source=Source,target=Target,combatBefore=before,combatAfter=s.CombatEnabled,startCenter=new[]{s.StartCenter.x,s.StartCenter.y},imageScale=s.ImageScale,elevation=s.Elevation,expectedDataSha=s.ExpectedDataSha,
    buildBefore,buildAfter=EditorBuildSettings.scenes.Select(x=>new{x.path,x.enabled}).ToArray()};
   File.WriteAllText(Out+"/build.json",JsonConvert.SerializeObject(result,Formatting.Indented));
  }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
  return result;
 }
}
