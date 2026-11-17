using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

// Project the sprite silhouette onto receiving geometry from its actual foot point.
// This avoids shadow-map bias and subtexel cascades separating a sprite from the floor.
public sealed class PixelProjectedShadow : MonoBehaviour
{
    private Mesh _mesh;
    private MeshRenderer _renderer;
    public float FootGap { get; private set; }
    public float Length { get; private set; }

    public void Initialize(SpriteRenderer body,Material material)
    {
        var child = new GameObject("Foot anchored silhouette");
        child.transform.SetParent(transform,false);
        _mesh = new Mesh { name = "Ground projected sprite silhouette" };
        child.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = child.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = material;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        var source = body.sprite.vertices;
        var vertices = new Vector3[source.Length];
        var ray = RenderSettings.sun.transform.forward;
        var feet = transform.position;
        for (var i = 0; i < source.Length; i++)
        {
            var point = transform.TransformPoint(new Vector3(source[i].x*(body.flipX ? -1 : 1),source[i].y,0));
            var projected = point-ray*(point.y/ray.y);
            if (Physics.Raycast(point-ray*.002f,ray,out var hit,100f,1<<0,QueryTriggerInteraction.Ignore))
            {
                projected = hit.point;
            }
            projected.y += .006f;
            vertices[i] = child.transform.InverseTransformPoint(projected);
            Length = Mathf.Max(Length,Vector2.Distance(new Vector2(feet.x,feet.z),new Vector2(projected.x,projected.z)));
        }
        _mesh.vertices = vertices;
        _mesh.uv = body.sprite.uv;
        var triangles = new int[body.sprite.triangles.Length];
        for (var i = 0; i < triangles.Length; i++) triangles[i] = body.sprite.triangles[i];
        _mesh.triangles = triangles;
        if (feet.y > .01f)
        {
            // A single silhouette triangle cannot span both a raised bank and the
            // lower floor. Subdivide in source texels before projecting each cell.
            var points = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            var rect = body.sprite.textureRect;
            var pivot = body.sprite.pivot;
            for (var y = 0; y < body.sprite.rect.height; y += 2)
            {
                for (var x = 0; x < body.sprite.rect.width; x += 2)
                {
                    var index = points.Count;
                    var corners = new[] { new Vector2(x,y),new Vector2(Mathf.Min(x+2,body.sprite.rect.width),y),new Vector2(Mathf.Min(x+2,body.sprite.rect.width),Mathf.Min(y+2,body.sprite.rect.height)),new Vector2(x,Mathf.Min(y+2,body.sprite.rect.height)) };
                    foreach (var pixel in corners)
                    {
                        var point = transform.TransformPoint(new Vector3((pixel.x-pivot.x)/body.sprite.pixelsPerUnit*(body.flipX ? -1 : 1),(pixel.y-pivot.y)/body.sprite.pixelsPerUnit,0));
                        var projected = point-ray*(point.y/ray.y);
                        if (Physics.Raycast(point-ray*.002f,ray,out var cellHit,100f,1<<0,QueryTriggerInteraction.Ignore)) projected = cellHit.point;
                        projected.y += .006f;
                        points.Add(child.transform.InverseTransformPoint(projected));
                        uv.Add(new Vector2((rect.x+pixel.x)/body.sprite.texture.width,(rect.y+pixel.y)/body.sprite.texture.height));
                    }
                    indices.AddRange(new[] { index,index+1,index+2,index,index+2,index+3 });
                }
            }
            _mesh.Clear();
            _mesh.SetVertices(points);
            _mesh.SetUVs(0,uv);
            _mesh.SetTriangles(indices,0);
        }
        _mesh.RecalculateBounds();
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex",body.sprite.texture);
        _renderer.SetPropertyBlock(properties);
        var foot = feet-ray*(feet.y/ray.y);
        if (Physics.Raycast(feet-ray*.002f,ray,out var footHit,100f,1<<0,QueryTriggerInteraction.Ignore)) foot = footHit.point;
        FootGap = Vector3.Distance(feet,foot);
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }
}
