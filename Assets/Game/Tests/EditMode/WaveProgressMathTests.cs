using NUnit.Framework;

public class WaveProgressMathTests
{
    [TestCase(0, 0, WaveNodeState.Remaining)]
    [TestCase(0, 1, WaveNodeState.Current)]
    [TestCase(0, 2, WaveNodeState.Passed)]
    [TestCase(1, 2, WaveNodeState.Current)]
    [TestCase(9, 10, WaveNodeState.Current)]
    [TestCase(9, 11, WaveNodeState.Passed)]
    public void NodesTrackOneBasedWave(int index, int wave, WaveNodeState expected)
    {
        Assert.AreEqual(expected, WaveProgressMath.Resolve(index, wave, 10));
    }
}
