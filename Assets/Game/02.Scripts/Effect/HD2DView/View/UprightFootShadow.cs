// Adapted from Assets/Game/02.Scripts/Effect/HD2DView/View/UprightContactShadow.cs SHA256 d1ddb67fc6827f8f5591cdf2a2d8019523432e1930483519853424875889a9dd
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Static F8 silhouette and actual bottom-row contact, initialized after sprite assignment.
[ExecuteAlways]
public sealed class UprightFootShadow : MonoBehaviour
{
    private readonly Dictionary<Sprite, Vector2[]> _footPixelDict = new();
    private Mesh _mesh;
    private Texture2D _readback;
    public Mesh Mesh => _mesh;
    public static Vector3 Project(Vector3 point, Vector3 direction, float supportY)
    {
        var projected = point + direction * ((supportY - point.y) / direction.y);
        projected.y = supportY + .002f;
        return projected;
    }

    public void Initialize(SpriteRenderer body, Material material, float supportY, Vector3 direction)
    {
        var child = new GameObject("Foot anchored shadow");
        child.transform.SetParent(transform, false);
        child.layer = HD2DBackgroundRig.BackgroundLayer;
        var sprite = body.sprite;
        var vertexList = new List<Vector3>();
        var uvList = new List<Vector2>(sprite.uv);
        var indexList = new List<int>();
        foreach (var vertex in sprite.vertices)
        {
            var p = body.transform.TransformPoint(new Vector3(vertex.x * (body.flipX ? -1 : 1), vertex.y, 0));
            vertexList.Add(child.transform.InverseTransformPoint(Project(p, direction, supportY)));
        }
        foreach (var index in sprite.triangles)
        {
            indexList.Add(index);
        }
        var along = new Vector3(direction.x, 0, direction.z).normalized;
        var side = Vector3.right / (sprite.pixelsPerUnit * 2);
        foreach (var pixel in GetFootPixels(sprite))
        {
            var local = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
            var point = body.transform.TransformPoint(new Vector3(local.x * (body.flipX ? -1 : 1), local.y, 0));
            point.y = supportY + .002f;
            var index = vertexList.Count;
            foreach (var corner in new[] { point-side-along*.04f, point+side-along*.04f, point+side+along*.15f, point-side+along*.15f })
            {
                vertexList.Add(child.transform.InverseTransformPoint(corner));
                uvList.Add(new Vector2(-1, -1));
            }
            indexList.InsertRange(0, new[] { index,index+1,index+2,index,index+2,index+3 });
        }
        _mesh = new Mesh { name = "Static support silhouette" };
        _mesh.SetVertices(vertexList);
        _mesh.SetUVs(0, uvList);
        _mesh.SetTriangles(indexList, 0);
        _mesh.RecalculateBounds();
        child.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", sprite.texture);
        properties.SetVector("_Contact", new Vector4(transform.position.x, transform.position.z, 0, supportY));
        renderer.SetPropertyBlock(properties);
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
        foreach (var value in new Object[] { _mesh, _readback })
        {
            if (value == null)
            {
                continue;
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
}
