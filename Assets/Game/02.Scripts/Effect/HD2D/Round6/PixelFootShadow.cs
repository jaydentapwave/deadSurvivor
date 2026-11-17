using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

// A point-sampled silhouette on its support plane. Receiver depth clips the
// silhouette at bank edges instead of stretching triangles across two heights.
public sealed class PixelFootShadow : MonoBehaviour
{
    private Mesh _mesh;

    public void Initialize(Mesh source, Vector2[] uv, Texture texture, Material material, float supportY, float contactRadius = 0, Vector2[] footPixels = null, Sprite sprite = null)
    {
        var old = transform.Find("Foot anchored shadow");
        if (old != null)
        {
            DestroyImmediate(old.gameObject);
        }
        var child = new GameObject("Foot anchored shadow");
        child.transform.SetParent(transform, false);
        child.layer = PixelBackgroundRig.BackgroundLayer;
        var direction = RenderSettings.sun.transform.forward;
        var vertices = source.vertices;
        for (var i = 0; i < vertices.Length; i++)
        {
            var point = transform.TransformPoint(vertices[i]);
            var projected = point + direction * ((supportY - point.y) / direction.y);
            projected.y = supportY + .002f;
            vertices[i] = child.transform.InverseTransformPoint(projected);
        }
        var pointList = new List<Vector3>(vertices);
        var uvList = new List<Vector2>(uv);
        var indexList = new List<int>(source.triangles);
        if (footPixels != null)
        {
            // Extend each actual opaque bottom texel into the silhouette. This is
            // sized in world texels, so the contact remains joined at every zoom.
            foreach (var pixel in footPixels)
            {
                var local = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
                var point = transform.TransformPoint(new Vector3(local.x * (GetComponent<SpriteRenderer>().flipX ? -1 : 1),local.y,0));
                point.y = supportY + .002f;
                var along = new Vector3(direction.x,0,direction.z).normalized;
                var side = Vector3.right / (sprite.pixelsPerUnit * 2);
                var index = pointList.Count;
                foreach (var corner in new[] { point-side-along*.04f,point+side-along*.04f,point+side+along*.15f,point-side+along*.15f })
                {
                    pointList.Add(child.transform.InverseTransformPoint(corner));
                    uvList.Add(new Vector2(-1,-1));
                }
                indexList.InsertRange(0,new[] { index,index+1,index+2,index,index+2,index+3 });
            }
        }
        _mesh = new Mesh { name = "Support plane silhouette" };
        _mesh.SetVertices(pointList);
        _mesh.SetUVs(0,uvList);
        _mesh.SetTriangles(indexList,0);
        _mesh.RecalculateBounds();
        child.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", texture);
        properties.SetVector("_Contact", new Vector4(transform.position.x, transform.position.z, contactRadius, 0));
        renderer.SetPropertyBlock(properties);
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            Destroy(_mesh);
        }
    }
}
