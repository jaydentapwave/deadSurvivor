using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Portable production wiring of the adopted F8 background and the W3 sprite cards.
public static class MainSceneHD2DEffectsMigration
{
    private const string Settings = "Assets/Game/03.Resources/Settings/";
    private const string Materials = "Assets/Game/03.Resources/Materials/HD2D/HD2D_";

    [MenuItem("DeadSurvivor/HD2D/Migrate Main Effects")]
    public static void Apply()
    {
        throw new InvalidOperationException("This orthographic migration is retired. Use DeadSurvivor/DQ/Apply Main screen.");
    }

    private static void AddBackgroundFeature(UniversalRendererData renderer, bool isCapture)
    {
        HD2DBackgroundFeature feature = null;
        foreach (var item in renderer.rendererFeatures)
        {
            if (item is HD2DBackgroundFeature match)
            {
                feature = match;
            }
        }
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<HD2DBackgroundFeature>();
            feature.name = isCapture ? "Capture point background" : "Compose point background";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        feature.captureBackground = isCapture;
        feature.outlineMaterial = Material("BackgroundOutline");
        feature.depthMaterial = Material("BackgroundDepth");
        feature.compositeMaterial = Material("BackgroundComposite");
        feature.Create();
        renderer.SetDirty();
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(renderer);
    }

    private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
