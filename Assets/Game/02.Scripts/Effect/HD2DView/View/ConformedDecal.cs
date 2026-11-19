using UnityEngine;
using UnityEngine.Rendering;

// Owns one terrain-following mesh; rebuild only for changed geometry or sprite frame.
public sealed class ConformedDecal : MonoBehaviour
{
    public Renderer ActiveRenderer => _renderer;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private MaterialPropertyBlock _properties;
    private TerrainHeightGrid _grid;
    private Sprite _sprite;
    private Vector3 _position;
    private Vector2 _scale;
    private bool _isFlipped;
    private float _yawDegrees;

    public void UpdateView(TerrainHeightGrid grid, Vector3 foot, Sprite sprite, Material material, Vector2 scale, bool isFlipped = false, float yawDegrees = 0)
    {
        if (_renderer == null)
        {
            gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
            gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _properties = new MaterialPropertyBlock();
        }
        _renderer.enabled = true;
        _renderer.sharedMaterial = material;
        if (_grid == grid && _sprite == sprite && _position == foot && _scale == scale && _isFlipped == isFlipped && _yawDegrees == yawDegrees)
        {
            return;
        }
        _grid = grid;
        _sprite = sprite;
        _position = foot;
        _scale = scale;
        _isFlipped = isFlipped;
        _yawDegrees = yawDegrees;
        var rect = sprite.textureRect;
        var pivot = sprite.pivot - sprite.textureRectOffset;
        var size = rect.size / sprite.pixelsPerUnit * scale;
        var offset = (rect.size * .5f - pivot) / sprite.pixelsPerUnit * scale;
        if (isFlipped)
        {
            offset.x = -offset.x;
        }
        ReleaseMesh();
        _mesh = GroundDecalMath.Build(grid, foot + Quaternion.Euler(0, yawDegrees, 0) * new Vector3(offset.x, 0, offset.y), size, yawDegrees, .25f, .002f);
        var vertices = _mesh.vertices;
        var uv = _mesh.uv;
        var colors = new Color[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = transform.InverseTransformPoint(vertices[i]);
            var u = isFlipped ? 1f - uv[i].x : uv[i].x;
            uv[i] = new Vector2((rect.x + u * rect.width) / sprite.texture.width, (rect.y + uv[i].y * rect.height) / sprite.texture.height);
            colors[i] = Color.white;
        }
        _mesh.vertices = vertices;
        _mesh.uv = uv;
        _mesh.colors = colors;
        _mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        _properties.SetTexture("_MainTex", sprite.texture);
        _properties.SetVector("unity_SpriteProps", Vector4.one);
        _properties.SetColor("unity_SpriteColor", Color.white);
        _renderer.SetPropertyBlock(_properties);
    }

    public void SetVisible(bool isVisible)
    {
        if (_renderer != null)
        {
            _renderer.enabled = isVisible;
        }
    }

    private void ReleaseMesh()
    {
        if (_mesh == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(_mesh);
        }
        else
        {
            DestroyImmediate(_mesh);
        }
    }
    private void OnDestroy() => ReleaseMesh();
}
