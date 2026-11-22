using UnityEngine;
using UnityEngine.Rendering;

// Actor owns only its terrain-following mesh. Material/texture are persistent shared assets.
[ExecuteAlways]
public sealed class DQBlobShadow : MonoBehaviour
{
    public Material SharedMaterial => _renderer.sharedMaterial;
    public Mesh OwnedMesh => _mesh;
    public float Width { get; private set; }
    private SpriteRenderer _body;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private TerrainHeightGrid _terrain = TerrainHeightGrid.Flat;
    private Vector3 _lastPosition = Vector3.positiveInfinity;
    private readonly Vector3[] _vertices = new Vector3[25];
    private readonly Color[] _colors = new Color[25];

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
            var uv = new Vector2[25];
            var indices = new int[96];
            for (var z = 0; z < 5; z++)
            {
                for (var x = 0; x < 5; x++)
                {
                    var i = z * 5 + x;
                    uv[i] = new Vector2(x / 4f, z / 4f);
                    if (x < 4 && z < 4)
                    {
                        var t = (z * 4 + x) * 6;
                        indices[t] = i;
                        indices[t + 1] = i + 6;
                        indices[t + 2] = i + 1;
                        indices[t + 3] = i;
                        indices[t + 4] = i + 5;
                        indices[t + 5] = i + 6;
                    }
                }
            }
            _mesh = new Mesh { name = "DQ reusable foot surface", vertices = _vertices, uv = uv, triangles = indices };
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
        for (var z = 0; z < 5; z++)
        {
            for (var x = 0; x < 5; x++)
            {
                var i = z * 5 + x;
                var world = position + new Vector3((x / 4f - .5f) * Width, 0, (z / 4f - .5f) * Width * .65f);
                world.y = TerrainHeightMath.Sample(_terrain, world.x, world.z) + support + .002f;
                _vertices[i] = _renderer.transform.InverseTransformPoint(world);
                _colors[i] = new Color(support > .1f ? 1 : 0, 0, 0, 1);
            }
        }
        _mesh.vertices = _vertices;
        _mesh.colors = _colors;
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
