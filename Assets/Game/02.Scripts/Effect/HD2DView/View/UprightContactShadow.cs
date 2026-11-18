using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Production adaptation of Round6 PixelFootShadow / Round8 opaque-bottom extraction.
// The support is the current flat game floor; stage 5 supplies raised terrain receivers.
public sealed class UprightContactShadow : MonoBehaviour
{
    private readonly Dictionary<Sprite, Vector2[]> _footPixelDict = new();
    private SpriteRenderer _body;
    private Mesh _mesh;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _properties;
    private Texture2D _readback;
    private bool _isVisible = true;

    public void Initialize(SpriteRenderer body, Material material)
    {
        _body = body;
        if (_mesh == null)
        {
            var child = new GameObject("Opaque foot contact shadow");
            child.transform.SetParent(transform, false);
            child.layer = HD2DBackgroundRig.BackgroundLayer;
            _mesh = new Mesh { name = "Opaque foot support projection" };
            child.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = child.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _properties = new MaterialPropertyBlock();
        }
        _renderer.sharedMaterial = material;
        LateUpdate();
    }

    public void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;
        _renderer.enabled = isVisible;
    }

    private void LateUpdate()
    {
        if (_mesh == null)
        {
            return; // Initialized by the owning view after its first sprite assignment.
        }
        _renderer.enabled = _isVisible && _body.enabled;
        var sprite = _body.sprite;
        var direction = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : Vector3.down;
        var supportY = transform.position.y;
        var vertexList = new List<Vector3>();
        var uvList = new List<Vector2>();
        var indexList = new List<int>();
        var source = sprite.vertices;
        foreach (var vertex in source)
        {
            var local = new Vector3(vertex.x * (_body.flipX ? -1f : 1f), vertex.y, 0f);
            var point = _body.transform.TransformPoint(local);
            var projected = point + direction * ((supportY - point.y) / direction.y);
            projected.y = supportY + .002f;
            vertexList.Add(_renderer.transform.InverseTransformPoint(projected));
        }
        uvList.AddRange(sprite.uv);
        foreach (var index in sprite.triangles)
        {
            indexList.Add(index);
        }
        // Bridge each actual opaque bottom texel to the projected silhouette, as in F8.
        var along = new Vector3(direction.x, 0f, direction.z).normalized;
        var side = Vector3.right / (sprite.pixelsPerUnit * 2f);
        foreach (var pixel in GetFootPixels(sprite))
        {
            var local = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
            var point = _body.transform.TransformPoint(new Vector3(local.x * (_body.flipX ? -1f : 1f), local.y, 0f));
            point.y = supportY + .002f;
            var index = vertexList.Count;
            foreach (var corner in new[] { point - side - along * .04f, point + side - along * .04f,
                         point + side + along * .15f, point - side + along * .15f })
            {
                vertexList.Add(_renderer.transform.InverseTransformPoint(corner));
                uvList.Add(new Vector2(-1f, -1f));
            }
            indexList.InsertRange(0, new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
        }
        _mesh.Clear();
        _mesh.SetVertices(vertexList);
        _mesh.SetUVs(0, uvList);
        _mesh.SetTriangles(indexList, 0);
        _mesh.RecalculateBounds();
        _properties.SetTexture("_MainTex", sprite.texture);
        _properties.SetVector("_Contact", new Vector4(transform.position.x, transform.position.z, 0f, 0f));
        _renderer.SetPropertyBlock(_properties);
    }

    private Vector2[] GetFootPixels(Sprite sprite)
    {
        if (_footPixelDict.TryGetValue(sprite, out var feet))
        {
            return feet;
        }
        // Atlas textures are not CPU-readable. Read only this sprite once and cache each animation pose.
        var rect = sprite.textureRect;
        var target = RenderTexture.GetTemporary(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(sprite.texture, target, new Vector2(rect.width / sprite.texture.width, rect.height / sprite.texture.height),
                new Vector2(rect.x / sprite.texture.width, rect.y / sprite.texture.height));
            RenderTexture.active = target;
            if (_readback == null)
            {
                _readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            }
            else
            {
                _readback.Reinitialize(target.width, target.height);
            }
            _readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            var colors = _readback.GetPixels32();
            var pixelList = new List<Vector2>();
            for (var y = 0; y < target.height && pixelList.Count == 0; y++)
            {
                for (var x = 0; x < target.width; x++)
                {
                    if (colors[y * target.width + x].a > 127)
                    {
                        pixelList.Add(new Vector2(x + .5f, y + .5f) + sprite.textureRectOffset);
                    }
                }
            }
            feet = pixelList.ToArray();
            _footPixelDict.Add(sprite, feet);
            return feet;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private void OnDestroy()
    {
        DestroyOwned(_mesh);
        DestroyOwned(_readback);
    }

    private static void DestroyOwned(Object value)
    {
        if (value == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Destroy(value);
        }
        else
        {
            DestroyImmediate(value);
        }
    }
}
