using System.Collections.Generic;
using UnityEngine;

// Bevelled flat-normal wood, normalized bounds retained for rails, boards and posts.
public static class PropMeshMath
{
    public static Mesh RoundedBox()
    {
        var vertexList = new List<Vector3>();
        var indexList = new List<int>();
        var ringList = new[] { new Vector2(-.5f,.42f), new Vector2(-.42f,.5f), new Vector2(.42f,.5f), new Vector2(.5f,.42f) };
        var outline = new[] { new Vector2(-1,-.8f), new Vector2(-.8f,-1), new Vector2(.8f,-1), new Vector2(1,-.8f), new Vector2(1,.8f), new Vector2(.8f,1), new Vector2(-.8f,1), new Vector2(-1,.8f) };
        Vector3 Point(int ring, int side) => new(outline[side].x * ringList[ring].y, ringList[ring].x, outline[side].y * ringList[ring].y);
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var k = vertexList.Count;
            vertexList.AddRange(new[] { a,b,c });
            indexList.AddRange(new[] { k,k+1,k+2 });
        }
        for (var ring = 0; ring < 3; ring++)
        {
            for (var side = 0; side < 8; side++)
            {
                var next = (side + 1) % 8;
                Triangle(Point(ring,side),Point(ring+1,side),Point(ring+1,next));
                Triangle(Point(ring,side),Point(ring+1,next),Point(ring,next));
            }
        }
        for (var side = 0; side < 8; side++)
        {
            Triangle(Vector3.up*.5f,Point(3,(side+1)%8),Point(3,side));
            Triangle(Vector3.down*.5f,Point(0,side),Point(0,(side+1)%8));
        }
        var mesh = new Mesh { name = "Bevelled field wood" };
        mesh.SetVertices(vertexList);
        mesh.SetTriangles(indexList,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
