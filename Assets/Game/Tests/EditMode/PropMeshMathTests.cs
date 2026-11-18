using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
public class PropMeshMathTests
{
    [Test]
    public void WoodHasExactNormalizedBoundsAndFlatFiniteNormals()
    {
        var mesh=PropMeshMath.RoundedBox();
        try
        {
            Assert.That(Vector3.Distance(mesh.bounds.size,Vector3.one),Is.LessThan(.0001));
            Assert.IsTrue(mesh.normals.All(n=>n.sqrMagnitude>.99f && !float.IsNaN(n.x)));
            Assert.Greater(mesh.normals.Select(n=>Mathf.RoundToInt(n.y*100)).Distinct().Count(),2);
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [TestCase("Bridge",1.8f,.75f,4f,588)]
    [TestCase("Tent",1.744f,1.6f,2.320f,490)]
    public void ImportedUvModelsRetainMeasuredSize(string name,float x,float y,float z,int triangles)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/03.Resources/Models/HD2D/"+name+".fbx");
        var go=Object.Instantiate(prefab);
        try
        {
            var renderers=go.GetComponentsInChildren<MeshRenderer>();
            var bounds=renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            Assert.That(bounds.size.x,Is.EqualTo(x).Within(.01));
            Assert.That(bounds.size.y,Is.EqualTo(y).Within(.01));
            Assert.That(bounds.size.z,Is.EqualTo(z).Within(.01));
            var meshes=go.GetComponentsInChildren<MeshFilter>();
            Assert.AreEqual(triangles,meshes.Sum(m=>(int)m.sharedMesh.GetIndexCount(0)/3));
            Assert.IsTrue(meshes.All(m=>m.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
