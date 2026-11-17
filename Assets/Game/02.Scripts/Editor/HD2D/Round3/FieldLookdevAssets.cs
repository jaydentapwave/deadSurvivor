using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class FieldLookdevAssets
{
    public const string Root = "Assets/Game/03.Resources/Art/HD2D/Round3";

    [MenuItem("DeadSurvivor/HD2D/Round3/Build Assets")]
    public static void Build()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Build field assets in Edit Mode.");
        }
        var process = Process.Start(new ProcessStartInfo("/usr/bin/python3", "-B Assets/Game/02.Scripts/Editor/HD2D/Round3/GenerateFieldTextures.py") { UseShellExecute = false });
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Original field texture generation failed.");
        }
        AssetDatabase.Refresh();
        foreach (var file in new[] { "FieldGround", "FieldGrassOnly", "FieldWater", "StonePattern", "FieldTree", "FieldBush" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/" + file + ".png");
            importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat;
            if (file == "FieldTree" || file == "FieldBush")
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 48;
                importer.spritePivot = new Vector2(.5f,0);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
        }
        for (var i = 0; i < 5; i++)
        {
            var material = CreateMaterial("Surface_" + i,"HD2D/Round3/FieldSurface");
            material.SetFloat("_Category",i);
            material.SetTexture("_Ground",Load<Texture2D>("FieldGround.png"));
            material.SetTexture("_Water",Load<Texture2D>("FieldWater.png"));
            material.SetTexture("_Pattern",Load<Texture2D>("StonePattern.png"));
            Save(material);
        }
        Save(CreateMaterial("BrownHull","HD2D/Round3/BrownHull"));
        Save(CreateMaterial("BandBlur","HD2D/Round3/BandBlur"));
        if (Load<Mesh>("RoundBlock.asset") == null)
        {
            AssetDatabase.CreateAsset(RoundedBlock(),Root + "/RoundBlock.asset");
            AssetDatabase.CreateAsset(RoundedStone(),Root + "/RoundStone.asset");
        }
        if (Load<UniversalRendererData>("Renderer.asset") == null)
        {
            var renderer = Object.Instantiate(PixelLookdevAssets.Load<UniversalRendererData>("Renderer.asset"));
            renderer.name = "F3 field renderer";
            renderer.rendererFeatures.Clear();
            AssetDatabase.CreateAsset(renderer,Root + "/Renderer.asset");
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "Gentle field focus band";
            feature.passMaterial = Load<Material>("BandBlur.mat");
            feature.fetchColorBuffer = true;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            AssetDatabase.AddObjectToAsset(feature,renderer);
            renderer.rendererFeatures.Add(feature);
            feature.Create();
            Save(feature);
            Save(renderer);
        }
    }

    public static T Load<T>(string file) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Root + "/" + file);

    private static Material CreateMaterial(string name,string shader)
    {
        var material = Load<Material>(name + ".mat");
        if (material == null)
        {
            material = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(material,Root + "/" + name + ".mat");
        }
        return material;
    }

    private static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    private static Mesh RoundedBlock()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var heights = new[] { 0f,.385f,.43f,.45f };
        var radii = new[] { .99f,1f,.98f,.94f };
        for (var ring = 0; ring < heights.Length; ring++)
        {
            for (var i = 0; i < 32; i++)
            {
                var angle = i * Mathf.PI / 16;
                var x = Mathf.Cos(angle);
                var z = Mathf.Sin(angle);
                var point = new Vector3(Mathf.Sign(x)*Mathf.Pow(Mathf.Abs(x),.28f),0,Mathf.Sign(z)*Mathf.Pow(Mathf.Abs(z),.28f));
                point *= .5f*radii[ring];
                point.y = heights[ring];
                vertices.Add(point);
                if (ring > 0)
                {
                    var a = (ring-1)*32+i;
                    var b = (ring-1)*32+(i+1)%32;
                    triangles.AddRange(new[] { a,b+32,b,a,a+32,b+32 });
                }
            }
        }
        vertices.Add(new Vector3(0,.45f,0));
        for (var i = 0; i < 32; i++)
        {
            triangles.AddRange(new[] { 128,96+(i+1)%32,96+i });
        }
        var mesh = new Mesh { name = "Rounded grass lip block" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh RoundedStone()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (var y = 0; y <= 12; y++)
        {
            var phi = y*Mathf.PI/12;
            for (var x = 0; x <= 24; x++)
            {
                var theta = x*Mathf.PI/12;
                vertices.Add(new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta))*.5f);
                if (y < 12 && x < 24)
                {
                    var a = y*25+x;
                    triangles.AddRange(new[] { a,a+1,a+26,a,a+26,a+25 });
                }
            }
        }
        var mesh = new Mesh { name = "Rounded fieldstone" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
