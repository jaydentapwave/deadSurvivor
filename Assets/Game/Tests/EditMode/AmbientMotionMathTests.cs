using NUnit.Framework;
using UnityEngine;

public class AmbientMotionMathTests
{
    [TestCase(0, 0)]
    [TestCase(.249999, 0)]
    [TestCase(.25, 1)]
    [TestCase(.5, 2)]
    [TestCase(.75, 1)]
    [TestCase(1, 0)]
    [TestCase(12345.5, 2)]
    public void DiscreteQuarterSecondPoses(double seconds, int expected)
    {
        Assert.AreEqual(expected, AmbientMotionMath.FrameAt(seconds));
    }

    [Test]
    public void InvalidTimesAreRejectedAndLongTimeStaysBounded()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => AmbientMotionMath.FrameAt(-.1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => AmbientMotionMath.FrameAt(double.NaN));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => AmbientMotionMath.FrameAt(double.PositiveInfinity));
        Assert.That(AmbientMotionMath.FrameAt(double.MaxValue), Is.InRange(0, 2));
    }

    [Test]
    public void NormalAndFadeFlagMaterialsEnableDiscreteWind()
    {
        foreach (var suffix in new[] { "", "_Fade" })
        {
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/03.Resources/Materials/HD2D/Scenery/flag_cloth" + suffix + ".mat");
            Assert.NotNull(material, suffix);
            Assert.That(material.GetFloat("_FlagWind"), Is.EqualTo(1f), suffix);
        }
    }

    [Test]
    public void NativeParticleSystemKeepsCapAndAuthoredRate()
    {
        var prefab = Resources.Load<ParticleSystem>("Prefab/HD2DAmbientDust");
        Assert.NotNull(prefab);
        Assert.AreEqual(16, prefab.main.maxParticles); // DQ K44: fewer floating lights; tighter cap than the previous 36.
        Assert.AreEqual(1.4f, prefab.emission.rateOverTime.constant, .00001f);
        Assert.AreEqual(18f, prefab.main.startLifetime.constant);
        var particles = Object.Instantiate(prefab);
        try
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Emit(100);
            Assert.That(particles.particleCount, Is.InRange(1, 16));
            Assert.AreEqual(0, particles.gameObject.layer);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, particles.GetComponent<ParticleSystemRenderer>().shadowCastingMode);
        }
        finally { Object.DestroyImmediate(particles.gameObject); }
    }
}
