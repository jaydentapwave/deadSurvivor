using UnityEngine;
using UnityEngine.Rendering;

// SpriteRenderer's 3D shadow pass does not consistently bind per-sprite data in URP.
// A shadow-only mesh carries explicit sprite geometry/flip/alpha instead.
public sealed class PixelShadowCaster : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Material shadowMaterial;
    [SerializeField] private bool isCastingShadow;
    private MeshRenderer _shadowRenderer;
    private Mesh _mesh;
    private Sprite _sprite;
    private bool _isFlipped;
    private MaterialPropertyBlock _properties;

    public void Initialize(SpriteRenderer body, Material material, bool canCastShadow)
    {
        bodyRenderer = body;
        shadowMaterial = material;
        isCastingShadow = canCastShadow;
    }

    public void SetCastingShadow(bool canCastShadow)
    {
        isCastingShadow = canCastShadow;
    }

    private void Awake()
    {
        var copiedShadow = transform.Find("Explicit sprite shadow");
        if (copiedShadow != null)
        {
            copiedShadow.gameObject.SetActive(false);
            Destroy(copiedShadow.gameObject);
        }
        var shadow = new GameObject("Explicit sprite shadow");
        shadow.transform.SetParent(transform, false);
        _mesh = new Mesh { name = "Sprite shadow geometry" };
        shadow.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _shadowRenderer = shadow.AddComponent<MeshRenderer>();
        _shadowRenderer.sharedMaterial = shadowMaterial;
        _shadowRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        _shadowRenderer.receiveShadows = false;
        _properties = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        _shadowRenderer.enabled = isCastingShadow;
        if (!_shadowRenderer.enabled)
        {
            return;
        }
        if (_sprite == bodyRenderer.sprite && _isFlipped == bodyRenderer.flipX)
        {
            return;
        }
        _sprite = bodyRenderer.sprite;
        _isFlipped = bodyRenderer.flipX;
        var source = _sprite.vertices;
        var vertices = new Vector3[source.Length];
        var colors = new Color[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            vertices[i] = new Vector3(source[i].x * (_isFlipped ? -1f : 1f), source[i].y, 0f);
            colors[i] = Color.white;
        }
        var sourceTriangles = _sprite.triangles;
        var triangles = new int[sourceTriangles.Length];
        for (var i = 0; i < triangles.Length; i++)
        {
            triangles[i] = sourceTriangles[i];
        }
        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = _sprite.uv;
        _mesh.colors = colors;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
        _properties.SetTexture("_MainTex", _sprite.texture);
        _shadowRenderer.SetPropertyBlock(_properties);
    }

    private void OnDestroy()
    {
        Destroy(_mesh);
    }
}
