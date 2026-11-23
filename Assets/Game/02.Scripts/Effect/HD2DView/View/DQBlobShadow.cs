using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Actor owns only its terrain-following mesh. Material/texture are persistent shared assets.
[ExecuteAlways]
public sealed class DQBlobShadow : MonoBehaviour
{
    public Material SharedMaterial => _renderer.sharedMaterial;
    public Mesh OwnedMesh => _mesh;
    public MeshRenderer Renderer => _renderer;
    public float Width { get; private set; }
    private SpriteRenderer _body;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private TerrainHeightGrid _terrain = TerrainHeightGrid.Flat;
    private Vector3 _lastPosition = Vector3.positiveInfinity;
    private readonly List<Vector3> _vertices = new(64);
    private readonly List<Vector2> _uv = new(64);
    private readonly List<Color> _colors = new(64);
    private readonly List<int> _indices = new(128);
    private List<Vector2> _polygon = new(8);
    private List<Vector2> _clipped = new(8);

    public void Initialize(SpriteRenderer body, Material material, float width)
    {
        _body = body;
        Width = width;
        if (_mesh == null)
        {
            var child = new GameObject("DQ soft foot shadow", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(transform, false);
            child.layer = HD2DBackgroundRig.BackgroundLayer;
            _renderer = child.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _mesh = new Mesh { name = "DQ reusable foot surface" };
            _mesh.MarkDynamic();
            child.GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
        _renderer.sharedMaterial = material;
        _lastPosition = Vector3.positiveInfinity;
    }

    public void SetGround(TerrainHeightGrid terrain, Material material)
    {
        if (_terrain != terrain)
        {
            _terrain = terrain;
            _lastPosition = Vector3.positiveInfinity;
        }
        _renderer.sharedMaterial = material;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (_mesh == null)
        {
            return; // Owner has not initialized this optional DQ path yet.
        }
        _renderer.enabled = _body.enabled;
        var position = transform.position;
        if (_lastPosition == position)
        {
            return;
        }
        _lastPosition = position;
        var baseHeight = TerrainHeightMath.Sample(_terrain, position.x, position.z);
        // The grid is the walkable reference; raised banks use the actor's actual support.
        var support = position.y - baseHeight;
        _vertices.Clear(); _uv.Clear(); _colors.Clear(); _indices.Clear();
        var rectangle = new Rect(position.x - Width * .5f, position.z - Width * .325f, Width, Width * .65f);
        var minX = Mathf.FloorToInt((rectangle.xMin - _terrain.OriginX) / _terrain.Cell);
        var maxX = Mathf.FloorToInt((rectangle.xMax - _terrain.OriginX) / _terrain.Cell);
        var minZ = Mathf.FloorToInt((rectangle.yMin - _terrain.OriginZ) / _terrain.Cell);
        var maxZ = Mathf.FloorToInt((rectangle.yMax - _terrain.OriginZ) / _terrain.Cell);
        // Clip each actual terrain triangle. No interpolated triangle can bridge a grid crease.
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
        {
            var a = new Vector2(_terrain.OriginX + x * _terrain.Cell, _terrain.OriginZ + z * _terrain.Cell);
            var b = a + new Vector2(_terrain.Cell, 0);
            var c = a + Vector2.one * _terrain.Cell;
            var d = a + new Vector2(0, _terrain.Cell);
            AddTriangle(a, c, b); AddTriangle(a, d, c);
        }
        _mesh.Clear();
        _mesh.SetVertices(_vertices); _mesh.SetUVs(0, _uv); _mesh.SetColors(_colors); _mesh.SetTriangles(_indices, 0);

        void AddTriangle(Vector2 a, Vector2 b, Vector2 c)
        {
            _polygon.Clear(); _polygon.Add(a); _polygon.Add(b); _polygon.Add(c);
            Clip(0, rectangle.xMin, true); Clip(0, rectangle.xMax, false);
            Clip(1, rectangle.yMin, true); Clip(1, rectangle.yMax, false);
            var first = _vertices.Count;
            foreach (var p in _polygon)
            {
                var world = new Vector3(p.x, TerrainHeightMath.Sample(_terrain, p.x, p.y) + support + .002f, p.y);
                _vertices.Add(_renderer.transform.InverseTransformPoint(world));
                _uv.Add(new Vector2((p.x - rectangle.xMin) / rectangle.width, (p.y - rectangle.yMin) / rectangle.height));
                _colors.Add(new Color(support > .1f ? 1 : 0, 0, 0, 1));
            }
            for (var i = 1; i + 1 < _polygon.Count; i++) { _indices.Add(first); _indices.Add(first + i); _indices.Add(first + i + 1); }
        }
        void Clip(int axis, float edge, bool above)
        {
            _clipped.Clear();
            for (var i = 0; i < _polygon.Count; i++)
            {
                var a = _polygon[i]; var b = _polygon[(i + 1) % _polygon.Count];
                var ai = above ? a[axis] >= edge : a[axis] <= edge;
                var bi = above ? b[axis] >= edge : b[axis] <= edge;
                if (ai) _clipped.Add(a);
                if (ai != bi) _clipped.Add(Vector2.Lerp(a, b, (edge - a[axis]) / (b[axis] - a[axis])));
            }
            (_polygon, _clipped) = (_clipped, _polygon);
        }
        _mesh.RecalculateBounds();
    }

    private void OnDisable()
    {
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }
        }
    }
}
