using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EnemyShotViewTests
{
    [Test]
    public void EnemyShotLoopsOpaqueFramesAndResetsOnReuse()
    {
        var prefab = Resources.Load<EnemyShotView>("Prefab/EnemyShotView");
        var root = Object.Instantiate(prefab);
        try
        {
            var frames = new Sprite[3];
            for (var i = 0; i < frames.Length; i++)
            {
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Game/03.Resources/Art/HD2D/Effects/spore_{i + 1}.png");
            }
            root.Initialize(frames, 8);
            for (var i = 0; i < 6; i++)
            {
                root.UpdateView(new Vector3(1, 0, 2), i / 8f);
                Assert.AreSame(frames[i % 3], root.BodyRenderer.sprite);
                Assert.AreEqual(1, root.BodyRenderer.color.a);
            }
            root.BodyRenderer.flipX = true;
            root.Initialize(frames, 8);
            Assert.IsFalse(root.BodyRenderer.flipX);
            Assert.AreEqual(1, root.FrameNumber);
            Assert.AreEqual(1.6f, root.BodyRenderer.sharedMaterial.GetFloat("_CoreIntensity"));
            Assert.AreEqual(new Vector3(1, 2, 1), root.transform.Find("Shadow").localScale);
        }
        finally
        {
            Object.DestroyImmediate(root.gameObject);
        }
    }
}
