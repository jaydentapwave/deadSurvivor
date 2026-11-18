using UnityEngine;
using UnityEngine.Rendering;

// Initialized after the sprite is assigned. No Awake/Initialize ordering dependency.
public sealed class UprightShadowCaster : MonoBehaviour
{
    private SpriteRenderer _body;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private MaterialPropertyBlock _properties;
    private Sprite _sprite;
    private bool _isFlipped;
    private bool _isCasting = true;
    public void Initialize(SpriteRenderer body, Material material, int shadowLayer)
    {
        _body = body;
        if (_mesh == null)
        {
            var child = new GameObject("Upright shadow mesh");
            child.transform.SetParent(body.transform, false);
            child.layer = shadowLayer;
            _mesh = new Mesh { name = "Upright shadow silhouette" };
            child.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = child.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            _renderer.receiveShadows = false;
            _properties = new MaterialPropertyBlock();
        }
        _renderer.sharedMaterial = material;
        _sprite = null;
        LateUpdate();
    }
    public void SetCastingShadow(bool isCasting)
    {
        _isCasting = isCasting;
        _renderer.enabled = isCasting;
    }
    private void LateUpdate()
    {
        if (_mesh == null)
        {
            return; // Scene component is initialized by its owning view.
        }
        _renderer.enabled = _isCasting && _body.enabled;
        if (_sprite == _body.sprite && _isFlipped == _body.flipX)
        {
            return;
        }
        _sprite = _body.sprite;
        _isFlipped = _body.flipX;
        var source = _sprite.vertices;
        var vertices = new Vector3[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            vertices[i] = new Vector3(source[i].x * (_isFlipped ? -1 : 1), source[i].y, 0);
        }
        var indices = _sprite.triangles;
        var triangles = new int[indices.Length];
        for (var i = 0; i < indices.Length; i++)
        {
            triangles[i] = indices[i];
        }
        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = _sprite.uv;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
        _properties.SetTexture("_MainTex", _sprite.texture);
        _renderer.SetPropertyBlock(_properties);
    }
    private void OnDestroy()
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
}
