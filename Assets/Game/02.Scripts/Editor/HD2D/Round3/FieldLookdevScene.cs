using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class FieldLookdevScene
{
    public static GameObject Root { get; private set; }
    public static GameObject Ledge { get; private set; }
    public static Material[] SurfaceList { get; private set; }

    [MenuItem("DeadSurvivor/HD2D/Round3/Prepare Play Scene")]
    public static void Prepare()
    {
        PixelLookdevSceneUtils.Prepare();
        PixelLookdevSceneUtils.Scope.Restore();
        PixelLookdevSceneUtils.Scope.Initialize(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"),FieldLookdevAssets.Load<UniversalRendererData>("Renderer.asset"));
        PixelLookdevSceneUtils.Configure("F-int");
        Root = new GameObject("F3 original field materials");
        SurfaceList = Enumerable.Range(0,5).Select(i => FieldLookdevAssets.Load<Material>("Surface_" + i + ".mat")).ToArray();
        GameObject.Find("48 texels per unit grass").GetComponent<MeshRenderer>().sharedMaterial = SurfaceList[0];
        GameObject.Find("046_OB-006_Water").GetComponent<MeshRenderer>().sharedMaterial = SurfaceList[2];
        var shore = GameObject.Find("046_OB-006_Stones").GetComponent<MeshRenderer>();
        shore.sharedMaterials = Enumerable.Repeat(SurfaceList[1],shore.GetComponent<MeshFilter>().sharedMesh.subMeshCount).ToArray();
        shore.transform.Find("Brown outline hull").GetComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(FieldLookdevAssets.Load<Material>("BrownHull.mat"),shore.GetComponent<MeshFilter>().sharedMesh.subMeshCount).ToArray();
        foreach (var sprite in PixelLookdevSceneUtils.TreeSprites.GetComponentsInChildren<SpriteRenderer>())
        {
            sprite.sprite = FieldLookdevAssets.Load<Sprite>("FieldTree.png");
            if (sprite.transform.position.x < 0 && sprite.transform.position.z > 6)
            {
                sprite.transform.position += Vector3.up*.45f;
            }
        }
        foreach (var rock in PixelLookdevSceneUtils.RockMeshes.transform.Cast<Transform>().ToArray())
        {
            var renderers = rock.GetComponentsInChildren<MeshRenderer>().Where(r => r.shadowCastingMode != ShadowCastingMode.Off).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            rock.gameObject.SetActive(false);
            var rounded = Mesh("Fieldstone",FieldLookdevAssets.Load<Mesh>("RoundStone.asset"),SurfaceList[1],bounds.center,bounds.size);
            rounded.transform.SetParent(rock.parent,true);
        }
        var extras = GameObject.Find("Fence flowers and grass");
        foreach (var sprite in extras.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sprite.name.Contains("fence_"))
            {
                Fence(sprite.transform.position);
                sprite.gameObject.SetActive(false);
            }
        }
        foreach (var side in new[] { -1f,1f })
        {
            for (var i = 0; i < 5; i++)
            {
                Decor("FieldBush.png",new Vector3(side*(5.3f+i%2*.9f),0,side < 0 ? -5+i*1.1f : -3+i*2.1f),Root.transform);
            }
            for (var i = 0; i < 22; i++)
            {
                var x = -9f+i*.85f;
                var z = 2.8f+.24f*Mathf.Sin(x*.73f)+.12f*Mathf.Sin(x*1.91f)+side*.78f;
                var body = LookdevSceneBuilder.AddSprite("Path tuft",LookdevSceneBuilder.SpritePath + "/Decor/grass_tuft_01.png",new Vector3(x,0,z),AssetDatabase.LoadAssetAtPath<Material>(LookdevSceneBuilder.ArtPath + "/PixelSprite.mat"),false);
                body.transform.SetParent(Root.transform,true);
            }
        }
        Ledge = Mesh("Rounded grass terrace left",FieldLookdevAssets.Load<Mesh>("RoundBlock.asset"),SurfaceList[3],new Vector3(-8.4f,0,-4.8f),new Vector3(3f,1,3.4f));
        Mesh("Rounded grass terrace right",FieldLookdevAssets.Load<Mesh>("RoundBlock.asset"),SurfaceList[3],new Vector3(8.5f,0,-5.3f),new Vector3(3f,1,3.1f));
        Mesh("Pond outer grass terrace",FieldLookdevAssets.Load<Mesh>("RoundBlock.asset"),SurfaceList[3],new Vector3(-7.6f,0,8.8f),new Vector3(3.1f,1,1.3f));
        Mesh("Pond tree bank",FieldLookdevAssets.Load<Mesh>("RoundBlock.asset"),SurfaceList[3],new Vector3(-4.5f,0,8.1f),new Vector3(2.5f,1,4.6f));
        foreach (var animation in Object.FindObjectsByType<PixelWalkPreview>(FindObjectsSortMode.None))
        {
            animation.enabled = false;
        }
        PixelLookdevSceneUtils.Pollen.Pause();
        RenderSettings.sun.color = new Color(1,.98f,.92f);
        RenderSettings.sun.transform.rotation = Quaternion.Euler(32,135,0);
        var volume = Object.FindAnyObjectByType<Volume>().profile;
        volume.TryGet<Bloom>(out var bloom);
        bloom.intensity.value = .1f;
        bloom.threshold.value = 2f;
        volume.TryGet<ColorAdjustments>(out var adjustments);
        adjustments.saturation.value = 0;
        adjustments.postExposure.value = 0;
    }

    public static GameObject Closeup()
    {
        var group = PixelLookdevSceneUtils.CloseupProps("F-int");
        // The Round2 rock source is disabled after replacing its visible shape.
        Object.DestroyImmediate(group.transform.GetChild(1).gameObject);
        var rock = Mesh("Close fieldstone",FieldLookdevAssets.Load<Mesh>("RoundStone.asset"),SurfaceList[1],new Vector3(1.25f,.17f,-.4f),new Vector3(.45f,.34f,.4f));
        rock.transform.SetParent(group.transform,true);
        var ledge = Mesh("Close grass lip",FieldLookdevAssets.Load<Mesh>("RoundBlock.asset"),SurfaceList[3],new Vector3(1.25f,0,.9f),new Vector3(1.15f,1,.8f));
        ledge.transform.SetParent(group.transform,true);
        foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
        }
        return group;
    }

    private static SpriteRenderer Decor(string file,Vector3 position,Transform parent)
    {
        var sprite = new GameObject("Field bush").AddComponent<SpriteRenderer>();
        sprite.transform.SetParent(parent,false);
        sprite.transform.position = position;
        sprite.transform.rotation = Quaternion.Euler(30,0,0);
        sprite.sprite = FieldLookdevAssets.Load<Sprite>(file);
        sprite.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LookdevSceneBuilder.ArtPath + "/PixelSprite.mat");
        sprite.shadowCastingMode = ShadowCastingMode.Off;
        sprite.gameObject.SetActive(false);
        sprite.gameObject.AddComponent<PixelShadowCaster>().Initialize(sprite,AssetDatabase.LoadAssetAtPath<Material>(LookdevSceneBuilder.ArtPath + "/PixelShadow.mat"),true);
        sprite.gameObject.SetActive(true);
        return sprite;
    }

    private static void Fence(Vector3 position)
    {
        foreach (var x in new[] { -.4f,.4f })
        {
            Mesh("Rounded fence post",FieldLookdevAssets.Load<Mesh>("RoundStone.asset"),SurfaceList[4],position+new Vector3(x,.3f,0),new Vector3(.12f,.6f,.12f));
        }
        foreach (var y in new[] { .2f,.42f })
        {
            Mesh("Rounded fence rail",FieldLookdevAssets.Load<Mesh>("RoundStone.asset"),SurfaceList[4],position+new Vector3(0,y,0),new Vector3(.9f,.09f,.09f));
        }
    }

    private static GameObject Mesh(string name,Mesh mesh,Material material,Vector3 position,Vector3 scale)
    {
        var body = new GameObject(name);
        body.transform.SetParent(Root.transform,false);
        body.transform.position = position;
        body.transform.localScale = scale;
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        body.AddComponent<MeshRenderer>().sharedMaterial = material;
        var hull = new GameObject("Thin brown hull");
        hull.transform.SetParent(body.transform,false);
        hull.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = hull.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = FieldLookdevAssets.Load<Material>("BrownHull.mat");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return body;
    }
}
