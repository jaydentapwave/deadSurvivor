using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Unity strips excluded quality levels on Play startup. Include the existing Mobile
// level only in memory for this editor session, then restore the original exclusion.
[InitializeOnLoad]
public static class PixelQualityScopeUtils
{
    private const string Key = "HD2D.Round2.MobileInclusion";
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static PixelQualityScopeUtils()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DeadSurvivor/HD2D/Round2/Enable Mobile For Play In Memory")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Include Mobile before Play startup; do not rename PC.");
        }
        var mobile = Array.IndexOf(QualitySettings.names,"Mobile");
        if (mobile < 0)
        {
            throw new InvalidOperationException("Existing Mobile level is missing in Edit Mode.");
        }
        var isIncluded = (bool)typeof(QualitySettings).GetMethod("IsPlatformIncluded",Flags).Invoke(null,new object[] { "Standalone",mobile });
        SessionState.SetInt(Key + ".index",mobile);
        SessionState.SetBool(Key,!isIncluded);
        var settings = (UnityEngine.Object)typeof(QualitySettings).GetMethod("GetQualitySettings",Flags).Invoke(null,null);
        SessionState.SetBool(Key + ".dirty",EditorUtility.IsDirty(settings));
        if (!isIncluded)
        {
            typeof(QualitySettings).GetMethod("IncludePlatformAt",Flags).Invoke(null,new object[] { "Standalone",mobile });
        }
    }

    public static void Restore()
    {
        if (!SessionState.GetBool(Key,false))
        {
            return;
        }
        var mobile = SessionState.GetInt(Key + ".index",0);
        typeof(QualitySettings).GetMethod("ExcludePlatformAt",Flags).Invoke(null,new object[] { "Standalone",mobile });
        SessionState.SetBool(Key,false);
        if (!SessionState.GetBool(Key + ".dirty",false))
        {
            var settings = (UnityEngine.Object)typeof(QualitySettings).GetMethod("GetQualitySettings",Flags).Invoke(null,null);
            EditorUtility.ClearDirty(settings);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            Restore();
        }
    }
}
