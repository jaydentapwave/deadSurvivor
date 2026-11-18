using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class PixelLookdevAssets
{
    public const string Root = "Assets/Game/03.Resources/Art/HD2D/Round2";

    [MenuItem("DeadSurvivor/HD2D/Round2/Build Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Build assets before entering Play Mode.");
        }
        var process = Process.Start(new ProcessStartInfo("/usr/bin/python3", "Assets/Game/02.Scripts/Editor/HD2D/Round2/GenerateTextures.py") { WorkingDirectory = Directory.GetParent(Application.dataPath).FullName, UseShellExecute = false });
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Texture generation failed.");
        }
        AssetDatabase.Refresh();
        foreach (var file in new[] { "F2Ground.png", "F2Pattern.png", "F2Density.png" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/" + file);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        for (var i = 0; i < 4; i++)
        {
            var material = Material("Surface_" + i, "HD2D/Round2/ToonSurface");
            material.SetFloat("_Category", i);
            material.SetFloat("_Density", 48f);
            material.SetTexture("_Ground", Load<Texture2D>("F2Ground.png"));
            material.SetTexture("_Pattern", Load<Texture2D>("F2Pattern.png"));
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/DeadSurvivor/Art/Environment/Map/Foliage045s2.mat");
            material.SetTexture("_Palette", source.GetTexture("_Palette"));
            Save(material);
        }
        Save(Material("BrownHull", "HD2D/Round2/BrownHull"));
        Save(Material("BandBlur", "HD2D/Round2/BandBlur"));
        var rendererPath = Root + "/Renderer.asset";
        var renderer = Load<UniversalRendererData>("Renderer.asset");
        if (renderer == null)
        {
            renderer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset"));
            renderer.name = "F2 Renderer";
            renderer.rendererFeatures.Clear();
            AssetDatabase.CreateAsset(renderer, rendererPath);
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "F2 visible screen band and vignette";
            feature.passMaterial = Load<Material>("BandBlur.mat");
            feature.fetchColorBuffer = true;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            feature.Create();
            Save(feature);
            Save(renderer);
        }
    }

    public static T Load<T>(string path) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(Root + "/" + path);
    }

    private static Material Material(string name, string shader)
    {
        var path = Root + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
