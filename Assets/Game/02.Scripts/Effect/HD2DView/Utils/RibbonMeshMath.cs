using System.Collections.Generic;
using UnityEngine;

public static class RibbonMeshMath
{
    public static Mesh Build(Vector2[] points, float width, float height, float endHeight = float.NaN)
    {
        var vertexList = new List<Vector3>();
        var indexList = new List<int>();
        for (var i = 0; i < points.Length; i++)
        {
            var direction = i == 0 ? points[1] - points[0] : i == points.Length - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1];
            var normal = new Vector2(-direction.y, direction.x).normalized * (width / 2);
            var y = float.IsNaN(endHeight) ? height : Mathf.Lerp(height, endHeight, i / (float)(points.Length - 1));
            vertexList.Add(new Vector3(points[i].x + normal.x, y, points[i].y + normal.y));
            vertexList.Add(new Vector3(points[i].x - normal.x, y, points[i].y - normal.y));
            if (i > 0)
            {
                var k = (i - 1) * 2;
                indexList.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            }
        }
        var mesh = new Mesh { name = "Water ribbon" };
        mesh.SetVertices(vertexList);
        mesh.SetTriangles(indexList, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
