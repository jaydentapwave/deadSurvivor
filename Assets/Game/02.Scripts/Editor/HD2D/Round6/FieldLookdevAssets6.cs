using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class FieldLookdevAssets6
{
    public const string Root = "Assets/Game/03.Resources/Art/HD2D/Round6";

    [MenuItem("DeadSurvivor/HD2D/Round6/Build Assets")]
    public static void Build()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Build field assets in Edit Mode.");
        }
        var process = Process.Start(new ProcessStartInfo("/usr/bin/python3", "-B Assets/Game/02.Scripts/Editor/HD2D/Round6/GenerateFieldTextures.py") { UseShellExecute = false });
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Original field texture generation failed.");
        }
        AssetDatabase.Refresh();
        foreach (var file in new[] { "FieldGround", "FieldGrassOnly", "FieldWater", "StonePattern", "FieldSoil", "FieldTree", "FieldBush" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/" + file + ".png");
            importer.filterMode = FilterMode.Point;
            importer.sRGBTexture = file != "StonePattern";
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
            var material = CreateMaterial("Surface_" + i,"HD2D/Round6/FieldSurface");
            material.SetFloat("_Category",i);
            material.SetTexture("_Ground",Load<Texture2D>("FieldGround.png"));
            material.SetTexture("_Water",Load<Texture2D>("FieldWater.png"));
            material.SetTexture("_Pattern",Load<Texture2D>("StonePattern.png"));
            material.SetTexture("_Soil",Load<Texture2D>("FieldSoil.png"));
            Save(material);
        }
        Save(CreateMaterial("BackgroundOutline","HD2D/Round6/BackgroundOutline"));
        Save(CreateMaterial("BackgroundDepth","HD2D/Round6/BackgroundDepth"));
        Save(CreateMaterial("BackgroundComposite","HD2D/Round6/BackgroundComposite"));
        Save(CreateMaterial("BandBlur","HD2D/Round6/BandBlur"));
        Save(CreateMaterial("HardShadowCard","HD2D/Round6/HardShadowCard"));
        Save(CreateMaterial("FootShadow","HD2D/Round6/FootShadow"));
        var monsterMaterial = CreateMaterial("StableMonster","HD2D/Round6/StableSpriteLit");
        monsterMaterial.SetFloat("_ReceiveShadows",0);
        monsterMaterial.SetFloat("_HueShift",.68f);
        Save(monsterMaterial);

        SaveMesh(BankStrip(false),"RoundBlock.asset");
        SaveMesh(BankStrip(true),"CloseBank.asset");
        SaveMesh(RoundedStone(),"RoundStone.asset");
        if (Load<UniversalRendererData>("Renderer.asset") == null)
        {
            var renderer = Object.Instantiate(PixelLookdevAssets.Load<UniversalRendererData>("Renderer.asset"));
            renderer.name = "F6 field renderer";
            renderer.rendererFeatures.Clear();
            AssetDatabase.CreateAsset(renderer,Root + "/Renderer.asset");
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "Gentle field focus band";
            feature.passMaterial = Load<Material>("BandBlur.mat");
            feature.fetchColorBuffer = true;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            AssetDatabase.AddObjectToAsset(feature,renderer);
            var composite = ScriptableObject.CreateInstance<PixelBackgroundFeature>();
            composite.name = "Native sprite foreground over point background";
            composite.compositeMaterial = Load<Material>("BackgroundComposite.mat");
            AssetDatabase.AddObjectToAsset(composite,renderer);
            renderer.rendererFeatures.Add(composite);
            composite.Create();
            renderer.rendererFeatures.Add(feature);
            feature.Create();
            Save(feature);
            Save(renderer);
        }
        if (Load<UniversalRendererData>("BackgroundRenderer.asset") == null)
        {
            var renderer = Object.Instantiate(PixelLookdevAssets.Load<UniversalRendererData>("Renderer.asset"));
            renderer.name = "F6 low resolution background";
            renderer.rendererFeatures.Clear();
            AssetDatabase.CreateAsset(renderer,Root + "/BackgroundRenderer.asset");
            var feature = ScriptableObject.CreateInstance<PixelBackgroundFeature>();
            feature.name = "One low pixel depth normal boundary";
            feature.captureBackground = true;
            feature.outlineMaterial = Load<Material>("BackgroundOutline.mat");
            feature.depthMaterial = Load<Material>("BackgroundDepth.mat");
            AssetDatabase.AddObjectToAsset(feature,renderer);
            renderer.rendererFeatures.Add(feature);
            feature.Create();
            Save(feature);Save(renderer);
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

    private static void SaveMesh(Mesh mesh,string file)
    {
        var existing = Load<Mesh>(file);
        if (existing == null) AssetDatabase.CreateAsset(mesh,Root + "/" + file);
        else
        {
            EditorUtility.CopySerialized(mesh,existing);
            Object.DestroyImmediate(mesh);
            Save(existing);
        }
    }

    private static Mesh BankStrip(bool closeup)
    {
        var step = closeup ? 1f/24 : .125f;
        var cells = new HashSet<Vector2Int>();
        for (var x = -55; x < 55; x++)
        {
            for (var z = -34; z < 76; z++)
            {
                var px = (x+.5f)*step;
                var pz = (z+.5f)*step;
                var inside = closeup ? Mathf.Abs(px)<.6f-step*((z%5+5)%5<2 ? 1 : 0) && Mathf.Abs(pz)<.4f-step*((x%5+5)%5<2 ? 1 : 0) :
                    (px>-6.75f && px<-1.125f && pz>8.25f+step*((x%5+5)%5<2 ? 1 : 0) && pz<9.25f) ||
                    (px>-5.5f+step*((z%7+7)%7<3 ? 1 : 0) && px<-3.5f-step*((z%5+5)%5<2 ? 1 : 0) && pz>6+step*((x%5+5)%5<2 ? 1 : 0) && pz<8.5f);
                if (inside) cells.Add(new Vector2Int(x,z));
            }
        }
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            var index = vertices.Count;
            vertices.AddRange(new[] { a,b,c,d });
            triangles.AddRange(new[] { index,index+1,index+2,index,index+2,index+3 });
        }
        foreach (var cell in cells)
        {
            var x = cell.x*step;
            var z = cell.y*step;
            Quad(new Vector3(x,.36f,z),new Vector3(x,.36f,z+step),new Vector3(x+step,.36f,z+step),new Vector3(x+step,.36f,z));
            var corners = new[] { new Vector3(x,0,z),new Vector3(x,0,z+step),new Vector3(x+step,0,z+step),new Vector3(x+step,0,z) };
            var adjacent = new[] { Vector2Int.left,Vector2Int.up,Vector2Int.right,Vector2Int.down };
            for (var side = 0; side < 4; side++)
            {
                if (cells.Contains(cell+adjacent[side])) continue;
                var a = corners[side];
                var b = corners[(side+1)%4];
                // Flat normals at the top edge: no rounded slab or smooth side gradient.
                Quad(a,b,b+Vector3.up*.36f,a+Vector3.up*.36f);
            }
        }
        var mesh = new Mesh { name = closeup ? "Stepped grass bank sample" : "Connected perimeter grass banks", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh RoundedStone()
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var rings = new[] { -.5f,-.27f,.12f,.38f,.5f };
        var radii = new[] { .30f,.49f,.5f,.37f,.12f };
        var sides = 9;
        void Triangle(Vector3 a,Vector3 b,Vector3 c)
        {
            var index = vertices.Count;
            vertices.AddRange(new[] { a,b,c });
            indices.AddRange(new[] { index,index+1,index+2 });
        }
        Vector3 Point(int ring,int side)
        {
            var angle = side*Mathf.PI*2/sides;
            var radius = radii[ring]*(1+.12f*Mathf.Sin(side*2.7f+ring*.8f));
            return new Vector3(Mathf.Cos(angle)*radius,rings[ring],Mathf.Sin(angle)*radius*.88f);
        }
        for (var ring = 0; ring < rings.Length-1; ring++)
        {
            for (var side = 0; side < sides; side++)
            {
                var next = (side+1)%sides;
                Triangle(Point(ring,side),Point(ring+1,side),Point(ring+1,next));
                Triangle(Point(ring,side),Point(ring+1,next),Point(ring,next));
            }
        }
        for (var side = 0; side < sides; side++)
        {
            Triangle(Vector3.up*.5f,Point(4,(side+1)%sides),Point(4,side));
            Triangle(Vector3.down*.5f,Point(0,side),Point(0,(side+1)%sides));
        }
        var mesh = new Mesh { name = "Angular layered fieldstone" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
