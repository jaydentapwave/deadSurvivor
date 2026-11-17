using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class FieldLookdevScene7
{
    public static GameObject Root { get; private set; }
    public static GameObject Ledge { get; private set; }
    public static PixelBackgroundRig Rig { get; private set; }
    public static Material[] SurfaceList { get; private set; }

    [MenuItem("DeadSurvivor/HD2D/Round7/Prepare Play Scene")]
    public static void Prepare()
    {
        PixelLookdevSceneUtils.Prepare();
        PixelLookdevSceneUtils.Scope.Restore();
        PixelLookdevSceneUtils.Scope.Initialize(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"),FieldLookdevAssets7.Load<UniversalRendererData>("Renderer.asset"));
        PixelLookdevSceneUtils.Configure("F-int");
        Root = new GameObject("F7 original field materials");
        SurfaceList = Enumerable.Range(0,5).Select(i => FieldLookdevAssets7.Load<Material>("Surface_" + i + ".mat")).ToArray();
        GameObject.Find("48 texels per unit grass").GetComponent<MeshRenderer>().sharedMaterial = SurfaceList[0];
        GameObject.Find("046_OB-006_Water").GetComponent<MeshRenderer>().sharedMaterial = SurfaceList[2];
        var shore = GameObject.Find("046_OB-006_Stones").GetComponent<MeshRenderer>();
        shore.sharedMaterials = Enumerable.Repeat(SurfaceList[1],shore.GetComponent<MeshFilter>().sharedMesh.subMeshCount).ToArray();
        Object.DestroyImmediate(shore.transform.Find("Brown outline hull").gameObject);
        foreach (var sprite in PixelLookdevSceneUtils.TreeSprites.GetComponentsInChildren<SpriteRenderer>())
        {
            sprite.sprite = FieldLookdevAssets7.Load<Sprite>("FieldTree.png");
            if (sprite.transform.position.x < 0 && sprite.transform.position.z > 6)
            {
                sprite.transform.position += Vector3.up*.36f;
            }
        }
        PixelLookdevSceneUtils.RockMeshes.SetActive(false);
        PixelLookdevSceneUtils.RockSprites.SetActive(true);
        var rocks = PixelLookdevSceneUtils.RockSprites.GetComponentsInChildren<SpriteRenderer>();
        foreach (var rock in rocks) rock.color = new Color(.72f,.72f,.72f,1);
        var large = rocks.First(r => r.transform.position.x>0 && r.transform.position.z>0 && r.transform.position.z<4);
        Mesh("Large field boulder",FieldLookdevAssets7.Load<Mesh>("Boulder.asset"),SurfaceList[1],large.transform.position+Vector3.up*.4f,new Vector3(1.1f,.8f,.9f));
        large.gameObject.SetActive(false);
        var extras = GameObject.Find("Fence flowers and grass");
        foreach (var sprite in extras.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sprite.name.Contains("fence_"))
            {
                sprite.gameObject.SetActive(false);
            }
        }
        Fence(new Vector3(-8.2f,0,.3f));
        Fence(new Vector3(8.2f,0,.3f));
        foreach (var side in new[] { -1f,1f })
        {
            for (var i = 0; i < 5; i++)
            {
                Decor("FieldBush.png",new Vector3(side*(4.4f+i%2*.6f),0,side < 0 ? -5+i*1.1f : -3+i*2.1f),Root.transform);
            }
        }
        var random = new System.Random(181106);
        var clusterList = new[] { new Vector2(-7,-7),new Vector2(-2,-5.8f),new Vector2(3,-7.5f),new Vector2(7,-4.8f),new Vector2(-7,1),new Vector2(1,1.2f),new Vector2(7,4.3f) };
        for (var i = 0; i < 112; i++)
        {
            var center = clusterList[i%clusterList.Length];
            var angle = (float)random.NextDouble()*Mathf.PI*2;
            var radius = Mathf.Sqrt((float)random.NextDouble())*(i%3==0 ? 1.3f : .65f);
            var x = center.x+Mathf.Cos(angle)*radius;
            var z = center.y+Mathf.Sin(angle)*radius;
            var file = i%7==0 ? "flower_01.png" : i%7==1 ? "flower_02.png" : "grass_tuft_01.png";
            var body = LookdevSceneBuilder.AddSprite("Clustered " + file,LookdevSceneBuilder.SpritePath + "/Decor/"+file,new Vector3(x,0,z),AssetDatabase.LoadAssetAtPath<Material>(LookdevSceneBuilder.ArtPath + "/PixelSprite.mat"),false);
            body.transform.SetParent(Root.transform,true);
        }
        // Replace the old regimented flowers and tufts, keeping original fences only.
        foreach (var sprite in extras.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!sprite.name.Contains("fence_")) sprite.gameObject.SetActive(false);
        }
        Ledge = Mesh("Connected perimeter banks",FieldLookdevAssets7.Load<Mesh>("RoundBlock.asset"),SurfaceList[3],Vector3.zero,Vector3.one);
        Ledge.AddComponent<MeshCollider>().sharedMesh = Ledge.GetComponent<MeshFilter>().sharedMesh;
        var floor = GameObject.Find("48 texels per unit grass");
        floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
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
        RenderSettings.sun.shadowBias = .05f;
        RenderSettings.sun.shadowNormalBias = .02f;
        RenderSettings.sun.shadows = LightShadows.Hard;
        SnapCamera(Camera.main);
        foreach (var renderer in Root.GetComponentsInChildren<MeshRenderer>()) renderer.gameObject.layer = PixelBackgroundRig.BackgroundLayer;
        foreach (var name in new[] { "48 texels per unit grass","046_OB-006_Water","046_OB-006_Stones" }) GameObject.Find(name).layer = PixelBackgroundRig.BackgroundLayer;
        foreach (var sprite in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (sprite.name == "Blob shadow")
            {
                sprite.gameObject.layer = PixelBackgroundRig.BackgroundLayer;
                sprite.sharedMaterial = FieldLookdevAssets7.Load<Material>("HardShadowCard.mat");
                continue;
            }
            if (sprite.name.StartsWith("Monster_"))
            {
                sprite.sharedMaterial = FieldLookdevAssets7.Load<Material>("StableMonster.mat");
                // Native pixels must occupy whole logical pixels, including large monsters.
                var scale = sprite.transform.localScale;
                var step = sprite.sprite.pixelsPerUnit/48f;
                sprite.transform.localScale = new Vector3(Mathf.Max(step,Mathf.Round(scale.x/step)*step),Mathf.Max(step,Mathf.Round(scale.y/step)*step),scale.z);
            }
            if (Mathf.Abs(Mathf.DeltaAngle(sprite.transform.eulerAngles.x,30))<.1f) SnapSprite(sprite);
            if (!sprite.name.StartsWith("Monster_") && sprite.GetComponent<PixelShadowCaster>() != null) GroundShadow(sprite);
        }
        Rig = Camera.main.gameObject.AddComponent<PixelBackgroundRig>();
        Rig.Initialize(FieldLookdevAssets7.Load<UniversalRendererData>("BackgroundRenderer.asset"));

    }

    public static GameObject Closeup()
    {
        var group = PixelLookdevSceneUtils.CloseupProps("F-int");
        // The Round2 rock source is disabled after replacing its visible shape.
        Object.DestroyImmediate(group.transform.GetChild(1).gameObject);
        var rock = LookdevSceneBuilder.AddSprite("Close native small rock",LookdevSceneBuilder.SpritePath + "/Decor/rock_02.png",new Vector3(1.25f,0,-.4f),AssetDatabase.LoadAssetAtPath<Material>(LookdevSceneBuilder.ArtPath + "/PixelSprite.mat"),false);
        var boulder = Mesh("Close large boulder",FieldLookdevAssets7.Load<Mesh>("Boulder.asset"),SurfaceList[1],new Vector3(1.9f,.3f,1.8f),new Vector3(.6f,.6f,.6f));
        boulder.transform.SetParent(group.transform,true);
        rock.color = new Color(.72f,.72f,.72f,1);
        rock.transform.SetParent(group.transform,true);
        var ledge = Mesh("Close grass lip",FieldLookdevAssets7.Load<Mesh>("CloseBank.asset"),SurfaceList[3],new Vector3(1.25f,0,.9f),Vector3.one);
        ledge.transform.SetParent(group.transform,true);
        foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
        }
        ledge.AddComponent<MeshCollider>().sharedMesh = ledge.GetComponent<MeshFilter>().sharedMesh;
        foreach (var sprite in group.GetComponentsInChildren<SpriteRenderer>())
        {
            SnapSprite(sprite);
            GroundShadow(sprite);
        }
        return group;
    }

    public static void SnapCamera(Camera camera,int width = 1920,int height = 1080)
    {
        var right = camera.transform.right;
        var up = camera.transform.up;
        var position = camera.transform.position;
        var scale = Mathf.Max(1,Mathf.RoundToInt(height/(camera.orthographicSize*2*48)));
        var phaseX = Mathf.Repeat((float)width/(2*scale),1);
        var phaseY = Mathf.Repeat((float)height/(2*scale),1);
        position += right*((Mathf.Round(Vector3.Dot(position,right)*48-phaseX)+phaseX)/48-Vector3.Dot(position,right));
        position += up*((Mathf.Round(Vector3.Dot(position,up)*48-phaseY)+phaseY)/48-Vector3.Dot(position,up));
        camera.transform.position = position;
    }

    public static void SnapSprite(SpriteRenderer sprite)
    {
        var camera = Camera.main;
        var path = AssetDatabase.GetAssetPath(sprite.sprite);
        if (!string.IsNullOrEmpty(path))
        {
            var original = sprite.sprite;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var pivot = new Vector2(original.pivot.x/original.rect.width,original.pivot.y/original.rect.height);
            sprite.sprite = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),pivot,original.pixelsPerUnit,0,SpriteMeshType.FullRect);
            sprite.sprite.name = original.name;
            sprite.sprite.hideFlags = HideFlags.DontSave;
        }
        var position = sprite.transform.position;
        var corner = sprite.transform.TransformPoint(new Vector3(-sprite.sprite.pivot.x/48,-sprite.sprite.pivot.y/48,0));
        var x = Vector3.Dot(corner,camera.transform.right);
        var y = Vector3.Dot(corner,camera.transform.up);
        position.x += Mathf.Round(x*48)/48-x;
        position.z += (Mathf.Round(y*48)/48-y)/camera.transform.up.z;
        sprite.transform.position = position;
    }

    private static void GroundShadow(SpriteRenderer sprite)
    {
        var caster = sprite.GetComponent<PixelShadowCaster>();
        if (caster != null)
        {
            caster.SetCastingShadow(false);
            caster.enabled = false;
        }
        foreach (var shadow in sprite.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (shadow.name == "Explicit sprite shadow") shadow.gameObject.SetActive(false);
        }
        var source = new Mesh { name = "Sprite silhouette source" };
        source.vertices = sprite.sprite.vertices.Select(v => new Vector3(v.x*(sprite.flipX ? -1 : 1),v.y,0)).ToArray();
        source.uv = sprite.sprite.uv;
        source.triangles = sprite.sprite.triangles.Select(v => (int)v).ToArray();
        var shadowComponent = sprite.GetComponent<PixelFootShadow>();
        if (shadowComponent == null) shadowComponent = sprite.gameObject.AddComponent<PixelFootShadow>();
        var pixels = new Texture2D(2,2,TextureFormat.RGBA32,false);
        pixels.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.sprite.texture)));
        var colors = pixels.GetPixels32();
        var bottom = 0;
        while (!Enumerable.Range(0,pixels.width).Any(x => colors[bottom*pixels.width+x].a>127)) bottom++;
        var feet = Enumerable.Range(0,pixels.width).Where(x => colors[bottom*pixels.width+x].a>127).Select(x => new Vector2(x+.5f,bottom+.5f)).ToArray();
        shadowComponent.Initialize(source,source.uv,sprite.sprite.texture,FieldLookdevAssets7.Load<Material>("FootShadow.mat"),sprite.transform.position.y,0,feet,sprite.sprite);
        Object.DestroyImmediate(pixels);
        Object.DestroyImmediate(source);
    }

    private static SpriteRenderer Decor(string file,Vector3 position,Transform parent)
    {
        var sprite = new GameObject("Field bush").AddComponent<SpriteRenderer>();
        sprite.transform.SetParent(parent,false);
        sprite.transform.position = position;
        sprite.transform.rotation = Quaternion.Euler(30,0,0);
        sprite.sprite = FieldLookdevAssets7.Load<Sprite>(file);
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
            Mesh("Rounded fence post",FieldLookdevAssets7.Load<Mesh>("RoundStone.asset"),SurfaceList[4],position+new Vector3(x,.3f,0),new Vector3(.12f,.6f,.12f));
        }
        foreach (var y in new[] { .2f,.42f })
        {
            Mesh("Rounded fence rail",FieldLookdevAssets7.Load<Mesh>("RoundStone.asset"),SurfaceList[4],position+new Vector3(0,y,0),new Vector3(.9f,.09f,.09f));
        }
    }

    private static GameObject Mesh(string name,Mesh mesh,Material material,Vector3 position,Vector3 scale)
    {
        var body = new GameObject(name);
        body.transform.SetParent(Root.transform,false);
        body.transform.position = position;
        body.transform.localScale = scale;
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = body.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = mesh.subMeshCount == 2 ? new[] { SurfaceList[0],material } : new[] { material };
        if (name == "Large field boulder" || name == "Close large boulder")
        {
            var shadow = body.AddComponent<PixelFootShadow>();
            var uv = new Vector2[mesh.vertexCount];
            var supportY = position.y-scale.y*.5f;
            shadow.Initialize(mesh,uv,Texture2D.whiteTexture,FieldLookdevAssets7.Load<Material>("FootShadow.mat"),supportY,scale.x*.7f);
        }
        body.layer = PixelBackgroundRig.BackgroundLayer;
        return body;
    }
}
