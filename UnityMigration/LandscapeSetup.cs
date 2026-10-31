using UnityEngine;
using UnityEditor;
public static class LandscapeSetup {
 public static object Run(){
 PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
 PlayerSettings.allowedAutorotateToPortrait=false;
 PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
 PlayerSettings.allowedAutorotateToLandscapeLeft=true;
 PlayerSettings.allowedAutorotateToLandscapeRight=true;
 PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
 AssetDatabase.SaveAssets();
 var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
 type.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(view,3);view.Repaint();
 return $"Desktop {PlayerSettings.defaultScreenWidth}x{PlayerSettings.defaultScreenHeight}; Mobile landscape L/R={PlayerSettings.allowedAutorotateToLandscapeLeft}/{PlayerSettings.allowedAutorotateToLandscapeRight}; portrait={PlayerSettings.allowedAutorotateToPortrait}/{PlayerSettings.allowedAutorotateToPortraitUpsideDown}";
 }
}
