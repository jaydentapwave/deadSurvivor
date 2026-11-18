using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private Material ditherSpriteMaterial;
    [SerializeField] private Material ditherWoodMaterial;
    [SerializeField] private Material ditherTentMaterial;
    [SerializeField] private Material flagMaterial;
    [SerializeField] private Material ditherFlagMaterial;
    [SerializeField] private FieldModelCatalog modelCatalog;
    [SerializeField] private bool useModelScenery;
    public bool UsesModelScenery => useModelScenery;
    private int _modelSeed;
    private readonly Dictionary<string, Vector3[]> _modelPointDict = new();
    private readonly List<FieldOccluder> _occluderList = new();
    public IReadOnlyList<FieldOccluder> Occluders => _occluderList;
    public void RegisterOccluder(Transform anchor, Renderer[] renderers)
    {
        _occluderList.Add(new FieldOccluder(anchor, renderers, original =>
            original == cardMaterial ? ditherSpriteMaterial :
            original == woodMaterial ? ditherWoodMaterial :
            original == tentMaterial ? ditherTentMaterial :
            original == flagMaterial ? ditherFlagMaterial : modelCatalog.Entries.First(e => e.Material == original).FadedMaterial));
    }
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
        _modelSeed = layout.seed;
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
            var mesh = bank.runs != null
                ? BankMeshMath.Build(bank.runs, bank.cell, bank.height)
                : BankMeshMath.Build(bank.rectangles, bank.cell, bank.height, bank.seed);
            AddPart("bank", mesh, new[] { groundMaterial, bankMaterial });
        }
        foreach (var water in layout.water)
        {
            if (water.name == "pond")
            {
                if (water.points.Length == 0)
                {
                    AddPart("pond", pondMesh, new[] { waterMaterial }, false);
                    continue;
                }
                var min = water.points[0];
                var max = min;
                foreach (var point in water.points)
                {
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                // The sampled contour defines the pond; padding covers sub-cell contour jitter.
                min -= Vector2.one * .25f;
                max += Vector2.one * .25f;
                var pond = new Mesh { name = "PCG pond water plane" };
                pond.vertices = new[] { new Vector3(min.x, water.height, min.y), new Vector3(min.x, water.height, max.y), new Vector3(max.x, water.height, max.y), new Vector3(max.x, water.height, min.y) };
                pond.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                pond.RecalculateNormals();
                pond.RecalculateBounds();
                AddPart("pond", pond, new[] { waterMaterial });
            }
            else
            {
                AddPart(water.name, RibbonMeshMath.Build(water.points, water.width + (water.name == "waterfall" ? 0 : .25f), water.height, water.name == "waterfall" ? -.03f : float.NaN), new[] { waterMaterial });
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
        if (useModelScenery)
        {
            renderer.enabled = false;
            if (!placement.group.StartsWith("wall_"))
            {
                AddModel(sprite, placement);
            }
            return renderer;
        }
        if (placement.isCastingShadow)
        {
            card.AddComponent<UprightFootShadow>().Initialize(renderer, footShadowMaterial, placement.height, lightDirection);
        }
        if (placement.sprite.StartsWith("tree_") || placement.sprite.StartsWith("bush_") || placement.sprite == "boulder_01")
        {
            RegisterOccluder(card.transform, new Renderer[] { renderer });
        }
        if (placement.group.StartsWith("wall_"))
        {
            if (!_partDict.TryGetValue("wall",out var walls))
            {
                walls=new List<Renderer>();_partDict.Add("wall",walls);
            }
            walls.Add(renderer);
        }
        return renderer;
    }

    private void AddModel(Sprite sprite, FieldLayoutPlacement placement)
    {
        var seed = FieldModelMath.Seed(_modelSeed, placement);
        var entry = modelCatalog.Find(FieldModelMath.ModelId(placement.sprite, seed));
        var yaw = FieldModelMath.Yaw(seed);
        if (!_modelPointDict.TryGetValue(entry.Id, out var vertices))
        {
            vertices = entry.Prefab.GetComponentsInChildren<MeshFilter>()
                .SelectMany(f => f.sharedMesh.vertices.Select(v => entry.Prefab.transform.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
            _modelPointDict.Add(entry.Id, vertices);
        }
        var footprint = FieldModelMath.ProjectedSize(vertices, yaw, _tiltDegrees);
        var heightScale = sprite.bounds.size.y / footprint.y;
        var widthScale = sprite.bounds.size.x / (footprint.x * heightScale);
        var variation = placement.sprite.StartsWith("rock_") ? FieldModelMath.StoneScale(seed) : 1f;
        var root = new GameObject("3D:" + placement.group + ":" + entry.Id);
        root.transform.SetParent(_landscape, false);
        root.transform.localPosition = new Vector3(placement.x, placement.height, placement.y);
        root.transform.localScale = new Vector3(widthScale, 1, 1) * (variation * placement.scale);
        var model = Instantiate(entry.Prefab, root.transform);
        model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        model.transform.localScale = Vector3.one * heightScale;
        if (placement.sprite.StartsWith("tree_") || placement.sprite.StartsWith("bush_") || placement.sprite.StartsWith("boulder_"))
        {
            RegisterOccluder(root.transform, model.GetComponentsInChildren<Renderer>());
        }
    }

    public void FinishScenery(FieldLayoutData layout)
    {
        if (!useModelScenery)
        {
            return;
        }
        foreach (var group in layout.placements.Where(p => p.group.StartsWith("wall_")).GroupBy(p => p.group))
        {
            var first = group.First();
            var last = group.Last();
            var direction = last.Floor - first.Floor;
            var yaw = direction.sqrMagnitude > .000001f ? Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg : 0;
            var centers = FieldModelMath.WallCenters(first.Floor, last.Floor);
            var seed = FieldModelMath.Seed(layout.seed, first);
            for (var i = 0; i < centers.Length; i++)
            {
                var entry = modelCatalog.Find((seed + i) % 2 == 0 ? "wall_a" : "wall_b");
                var model = Instantiate(entry.Prefab, _landscape);
                model.name = "3D:" + group.Key + ":" + i;
                model.transform.localPosition = new Vector3(centers[i].x, first.height, centers[i].y);
                model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (!_partDict.TryGetValue("wall", out var walls))
                {
                    walls = new List<Renderer>();
                    _partDict.Add("wall", walls);
                }
                walls.AddRange(renderers);
                RegisterOccluder(model.transform, renderers);
            }
        }
        CombineSmallScenery();
    }

    private void CombineSmallScenery()
    {
        // 4m spatial chunks retain local culling. Only the non-occluding small plants merge.
        var roots = _landscape.Cast<Transform>().Where(t => t.name.StartsWith("3D:flowers:")).ToArray();
        var groups = roots.SelectMany(t => t.GetComponentsInChildren<MeshFilter>())
            .GroupBy(f => (Material: f.GetComponent<MeshRenderer>().sharedMaterial,
                X: Mathf.FloorToInt(f.transform.position.x / 4), Z: Mathf.FloorToInt(f.transform.position.z / 4)));
        foreach (var group in groups)
        {
            var mesh = new Mesh { name = "Small scenery " + group.Key.X + ":" + group.Key.Z };
            var parts = group.Select(f => new CombineInstance
            {
                mesh = f.sharedMesh,
                transform = _landscape.worldToLocalMatrix * f.transform.localToWorldMatrix
            }).ToArray();
            mesh.CombineMeshes(parts, true, true);
            AddPart("3D plant batch " + group.Key.Material.name + ":" + group.Key.X + ":" + group.Key.Z,
                mesh, new[] { group.Key.Material });
        }
        foreach (var root in roots)
        {
            root.gameObject.SetActive(false);
        }
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
                    if (!useModelScenery)
                    {
                        var blob = new GameObject("Fence foot shadow").AddComponent<SpriteRenderer>();
                        blob.transform.SetParent(root.transform, false);
                        blob.transform.localPosition = new Vector3(x,.004f,0);
                        blob.transform.localRotation = Quaternion.Euler(90,0,0);
                        blob.transform.localScale = new Vector3(.8f,.6f,1);
                        blob.gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
                        blob.sprite = shadow;
                        blob.sharedMaterial = hardShadowMaterial;
                    }
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
                var cloth = new GameObject("Flag cloth");
                cloth.transform.SetParent(root.transform,false);
                cloth.transform.localPosition = new Vector3(flag.rect.width / flag.pixelsPerUnit / 2,1.6f - flag.rect.height / flag.pixelsPerUnit,0);
                cloth.transform.rotation = Quaternion.Euler(_tiltDegrees,0,0);
                if (useModelScenery)
                {
                    var mesh = new Mesh { name = "Flag cloth UV mesh" };
                    mesh.vertices = flag.vertices.Select(v => new Vector3(v.x, v.y, 0)).ToArray();
                    mesh.uv = flag.vertices.Select(v => (v * flag.pixelsPerUnit + flag.pivot) / flag.rect.size).ToArray();
                    mesh.triangles = flag.triangles.Select(i => (int)i).ToArray();
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    _ownedMeshList.Add(mesh);
                    cloth.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = cloth.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = flagMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                    cloth.layer = HD2DBackgroundRig.BackgroundLayer;
                }
                else
                {
                    var renderer = cloth.AddComponent<SpriteRenderer>();
                    renderer.sprite = flag;
                    renderer.sharedMaterial = cardMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            if (!_partDict.TryGetValue(placement.prop,out var partList))
            {
                partList = new List<Renderer>();
                _partDict.Add(placement.prop,partList);
            }
            partList.Add(root.GetComponentInChildren<Renderer>());
            if (placement.prop == "tent" || placement.prop == "flag" || placement.prop == "sign")
            {
                RegisterOccluder(root.transform, root.GetComponentsInChildren<Renderer>());
            }
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
        renderer.shadowCastingMode = useModelScenery ? ShadowCastingMode.On : ShadowCastingMode.Off;
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

// One group per scenery object, with shared asset references captured once at construction.
public sealed class FieldOccluder
{
    public Transform Anchor { get; }
    private readonly Renderer[] _renderers;
    private readonly Material[][] _normal;
    private readonly Material[][] _faded;
    private readonly MaterialPropertyBlock _properties = new();
    public const int MaxOcclusionRects = 64;
    private readonly Vector4[] _cutouts = new Vector4[MaxOcclusionRects];
    public bool IsFaded { get; private set; }
    public Bounds Bounds
    {
        get
        {
            var bounds = _renderers[0].bounds;
            for (var i = 1; i < _renderers.Length; i++) bounds.Encapsulate(_renderers[i].bounds);
            return bounds;
        }
    }
    public FieldOccluder(Transform anchor, Renderer[] renderers, System.Func<Material, Material> fadedMaterial)
    {
        Anchor = anchor;
        _renderers = renderers;
        _normal = new Material[renderers.Length][];
        _faded = new Material[renderers.Length][];
        for (var i = 0; i < renderers.Length; i++)
        {
            _normal[i] = renderers[i].sharedMaterials;
            _faded[i] = new Material[_normal[i].Length];
            for (var j = 0; j < _normal[i].Length; j++) _faded[i][j] = fadedMaterial(_normal[i][j]);
        }
    }
    public void SetOcclusionRects(IReadOnlyList<Rect> viewportRects)
    {
        var count = Mathf.Min(MaxOcclusionRects, viewportRects.Count);
        for (var i = 0; i < viewportRects.Count; i++)
        {
            var rect = viewportRects[i];
            var value = new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
            if (i < MaxOcclusionRects)
            {
                _cutouts[i] = value;
                continue;
            }
            // An unusually dense crowd still reveals every actor. Merge only the nearest
            // existing window on overflow, rather than cutting gaps between all actors.
            var nearest = 0;
            var distance = float.PositiveInfinity;
            for (var j = 0; j < count; j++)
            {
                var old = _cutouts[j];
                var delta = new Vector2(old.x + old.z - value.x - value.z, old.y + old.w - value.y - value.w);
                if (delta.sqrMagnitude < distance) { nearest = j; distance = delta.sqrMagnitude; }
            }
            var window = _cutouts[nearest];
            _cutouts[nearest] = new Vector4(Mathf.Min(window.x, value.x), Mathf.Min(window.y, value.y),
                Mathf.Max(window.z, value.z), Mathf.Max(window.w, value.w));
        }
        foreach (var renderer in _renderers)
        {
            renderer.GetPropertyBlock(_properties);
            _properties.SetInt("_OcclusionRectCount", count);
            _properties.SetVectorArray("_OcclusionRects", _cutouts);
            renderer.SetPropertyBlock(_properties);
        }
    }
    public void SetFaded(bool faded)
    {
        if (IsFaded == faded) return;
        IsFaded = faded;
        for (var i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterials = faded ? _faded[i] : _normal[i];
    }
}
