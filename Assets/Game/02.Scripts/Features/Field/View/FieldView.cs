using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// 아이소 바닥 Tilemap(셀 (1, 0.5), 타일 앵커 (0, 0) = 마름모 아래 꼭짓점 — 그림 피벗 아래 가운데)에 풀·흙길 타일을 깐다(아트 기준서 3.1·3.2).
[ExecuteAlways]
public class FieldView : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Material groundMaterial;
    [SerializeField] private Material bankMaterial;
    [SerializeField] private Material waterMaterial;
    [SerializeField] private Mesh pondMesh;
    [SerializeField] private Material cardMaterial;
    [SerializeField] private Material footShadowMaterial;
    [SerializeField] private Material woodMaterial;
    [SerializeField] private Material bridgeMaterial;
    [SerializeField] private Material tentMaterial;
    [SerializeField] private Material hardShadowMaterial;
    [SerializeField] private GameObject bridgeModel;
    [SerializeField] private GameObject tentModel;
    private Mesh _woodMesh;
    public Material CardMaterial => cardMaterial;
    private readonly List<Mesh> _ownedMeshList = new();
    private readonly Dictionary<string, List<Renderer>> _partDict = new();
    private Transform _landscape;
    public Transform Landscape => _landscape;
    public int PartCount(string kind) => _partDict.TryGetValue(kind, out var partList) ? partList.Count : 0;
    public int GroundVisibleCount(Camera camera) => PartVisibleCount("ground", camera);
    public int PartVisibleCount(string kind, Camera camera)
    {
        if (!_partDict.TryGetValue(kind, out var partList))
        {
            return 0;
        }
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        var count = 0;
        foreach (var renderer in partList)
        {
            if (renderer.enabled && GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
            {
                count++;
            }
        }
        return count;
    }

    public void BuildLandscape(FieldLayoutData layout)
    {
        _landscape = new GameObject("HD2D Field Landscape").transform;
        _landscape.SetParent(transform, true);
        tilemap.GetComponent<TilemapRenderer>().enabled = false;
        var h = layout.groundHalfDiagonal;
        var ground = new Mesh { name = "Field and flat rim L1 36" };
        ground.vertices = new[] { new Vector3(0, 0, h), new Vector3(h, 0, 0), new Vector3(0, 0, -h), new Vector3(-h, 0, 0) };
        ground.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        ground.RecalculateNormals();
        ground.RecalculateBounds();
        AddPart("ground", ground, new[] { groundMaterial });
        foreach (var bank in layout.banks)
        {
            AddPart("bank", BankMeshMath.Build(bank.rectangles, bank.cell, bank.height, bank.seed), new[] { bankMaterial, bankMaterial });
        }
        foreach (var water in layout.water)
        {
            if (water.name == "pond")
            {
                AddPart("pond", pondMesh, new[] { waterMaterial }, false);
            }
            else
            {
                AddPart(water.name, RibbonMeshMath.Build(water.points, water.width, water.height, water.name == "waterfall" ? -.03f : float.NaN), new[] { waterMaterial });
            }
        }
    }

    public SpriteRenderer AddCard(Sprite sprite, FieldLayoutPlacement placement, Vector3 lightDirection)
    {
        var card = new GameObject(placement.group + ":" + placement.sprite);
        card.transform.SetParent(_landscape, false);
        card.transform.rotation = Quaternion.Euler(_tiltDegrees, 0, 0);
        card.transform.position = CardSnapMath.Snap(new Vector3(placement.x, placement.height + .001f, placement.y), sprite.pivot, sprite.pixelsPerUnit, _tiltDegrees);
        var renderer = card.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.flipX = placement.isFlipped;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.sharedMaterial = cardMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (placement.sprite.StartsWith("rock_") || placement.sprite == "boulder_01")
        {
            renderer.color = new Color(.72f, .72f, .72f, 1);
        }
        if (placement.isCastingShadow)
        {
            card.AddComponent<UprightFootShadow>().Initialize(renderer, footShadowMaterial, placement.height, lightDirection);
        }
        return renderer;
    }

    public void BuildProps(FieldLayoutData layout, Sprite flag, Sprite shadow)
    {
        _woodMesh = PropMeshMath.RoundedBox();
        _ownedMeshList.Add(_woodMesh);
        foreach (var placement in layout.placements)
        {
            if (placement.kind != "prop")
            {
                continue;
            }
            var root = new GameObject(placement.prop);
            root.transform.SetParent(_landscape, false);
            root.transform.localPosition = new Vector3(placement.x, placement.height, placement.y);
            root.transform.localRotation = Quaternion.Euler(0, placement.yaw, 0);
            root.transform.localScale = Vector3.one * placement.scale;
            if (placement.prop == "bridge" || placement.prop == "tent")
            {
                var model = Instantiate(placement.prop == "bridge" ? bridgeModel : tentModel, root.transform);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
                    renderer.sharedMaterial = placement.prop == "bridge" ? bridgeMaterial : tentMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
            }
            else if (placement.prop == "fence")
            {
                foreach (var x in new[] { -.4f, .4f })
                {
                    AddWood(root.transform, new Vector3(x,.3f,0), new Vector3(.12f,.6f,.12f));
                    var blob = new GameObject("Fence foot shadow").AddComponent<SpriteRenderer>();
                    blob.transform.SetParent(root.transform, false);
                    blob.transform.localPosition = new Vector3(x,.004f,0);
                    blob.transform.localRotation = Quaternion.Euler(90,0,0);
                    blob.transform.localScale = new Vector3(.8f,.6f,1);
                    blob.gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
                    blob.sprite = shadow;
                    blob.sharedMaterial = hardShadowMaterial;
                }
                foreach (var y in new[] { .2f, .42f })
                {
                    AddWood(root.transform,new Vector3(0,y,0),new Vector3(.9f,.09f,.09f));
                }
            }
            else if (placement.prop == "sign")
            {
                AddWood(root.transform,new Vector3(0,.5f,0),new Vector3(.10f,1,.1f));
                AddWood(root.transform,new Vector3(0,.82f,0),new Vector3(.8f,.36f,.08f));
            }
            else if (placement.prop == "flag")
            {
                AddWood(root.transform,new Vector3(0,.8f,0),new Vector3(.065f,1.6f,.065f));
                var cloth = new GameObject("Flag cloth").AddComponent<SpriteRenderer>();
                cloth.transform.SetParent(root.transform,false);
                cloth.transform.localPosition = new Vector3(flag.rect.width / flag.pixelsPerUnit / 2,1.6f - flag.rect.height / flag.pixelsPerUnit,0);
                cloth.transform.rotation = Quaternion.Euler(_tiltDegrees,0,0);
                cloth.sprite = flag;
                cloth.sharedMaterial = cardMaterial;
                cloth.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (!_partDict.TryGetValue(placement.prop,out var partList))
            {
                partList = new List<Renderer>();
                _partDict.Add(placement.prop,partList);
            }
            partList.Add(root.GetComponentInChildren<Renderer>());
        }
    }

    private void AddWood(Transform parent, Vector3 position, Vector3 scale)
    {
        var part = new GameObject("Wood");
        part.layer = HD2DBackgroundRig.BackgroundLayer;
        part.transform.SetParent(parent,false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.AddComponent<MeshFilter>().sharedMesh = _woodMesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = woodMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private MeshRenderer AddPart(string kind, Mesh mesh, Material[] materials, bool ownsMesh = true)
    {
        var part = new GameObject(kind);
        part.layer = HD2DBackgroundRig.BackgroundLayer;
        part.transform.SetParent(_landscape, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (!_partDict.TryGetValue(kind, out var partList))
        {
            partList = new List<Renderer>();
            _partDict.Add(kind, partList);
        }
        partList.Add(renderer);
        if (ownsMesh)
        {
            _ownedMeshList.Add(mesh);
        }
        return renderer;
    }

    private void OnDestroy()
    {
        foreach (var mesh in _ownedMeshList)
        {
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }
    }

    public Tilemap Tilemap => tilemap;
    private float _tiltDegrees;
    public void Initialize(float tiltDegrees) => _tiltDegrees = tiltDegrees;

    public void Build(TileChangeData[] tileChangeDatas)
    {
        tilemap.ClearAllTiles();
        tilemap.SetTiles(tileChangeDatas, true);
    }

    // 초기화 때 한 번만 만든다. 시뮬레이션·충돌 컴포넌트 없이 장면과 함께 사라지는 그림뿐이다.
    // 소품은 기존 대원·몬스터와 같은 Default/0 + Pivot, URP 2D의 (0,1,0) 정렬을 그대로 쓴다.
    public SpriteRenderer AddDecor(Sprite sprite, FieldDecorPlacement placement)
    {
        var decor = new GameObject(placement.Sprite);
        decor.layer = placement.IsGround ? HD2DBackgroundRig.BackgroundLayer : 0;
        decor.transform.SetParent(transform.parent, false);
        var offsetX = placement.PivotOffsetXPixels / (float)Const.PixelsPerUnit;
        var floor = IsoProjection.IsoCellToFloor(placement.Cell);
        var position = new Vector3(floor.x, .001f, floor.y);
        position.x += placement.IsFlipped ? -offsetX : offsetX;
        decor.transform.position = position;
        var renderer = decor.AddComponent<SpriteRenderer>();
        decor.transform.rotation = Quaternion.Euler(placement.IsGround ? 90f : _tiltDegrees, 0f, 0f);
        renderer.sprite = sprite;
        renderer.flipX = placement.IsFlipped;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.sortingOrder = placement.IsGround ? -90 : 0;
        renderer.sharedMaterial = tilemap.GetComponent<TilemapRenderer>().sharedMaterial;
        return renderer;
    }
}
