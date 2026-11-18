using NUnit.Framework;
using UnityEditor;
using UnityEngine;
public class UprightFootShadowTests
{
    [TestCase(0f)]
    [TestCase(.36f)]
    public void RayProjectsToSupportAndSpriteMeshIsReleased(float height)
    {
        var root=new GameObject("Static shadow test");
        var body=root.AddComponent<SpriteRenderer>();
        body.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Decor/tree_02.png");
        body.transform.rotation=Quaternion.Euler(30,0,0);
        body.transform.position=new Vector3(2,height,3);
        var direction=Quaternion.Euler(32,135,0)*Vector3.forward;
        var shadow=root.AddComponent<UprightFootShadow>();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/HD2D_FootShadow.mat");
        Mesh mesh=null;
        try
        {
            var point=new Vector3(2,height+1,3);
            var result=UprightFootShadow.Project(point,direction,height);
            Assert.That(result.y,Is.EqualTo(height+.002f).Within(.00001));
            shadow.Initialize(body,material,height,direction);
            mesh=shadow.Mesh;
            Assert.Greater(mesh.vertexCount,body.sprite.vertices.Length);
            foreach (var v in mesh.vertices)
            {
                Assert.That(root.transform.TransformPoint(v).y,Is.EqualTo(height+.002f).Within(.0001));
            }
            Assert.AreEqual(HD2DBackgroundRig.BackgroundLayer,root.transform.GetChild(0).gameObject.layer);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        Assert.IsTrue(mesh==null);
    }
}
