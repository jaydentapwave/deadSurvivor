using UnityEngine;

public enum WaveNodeState { Remaining, Current, Passed }

public static class WaveProgressMath
{
    public static WaveNodeState Resolve(int index, int wave, int waveCount)
    {
        var completed = Mathf.Clamp(wave - 1, 0, waveCount);
        return index < completed ? WaveNodeState.Passed
            : index == completed && wave > 0 && wave <= waveCount ? WaveNodeState.Current : WaveNodeState.Remaining;
    }
}
