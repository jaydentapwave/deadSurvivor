using System;
using UnityEngine;

[Serializable]
public class DQClipCatalog
{
    public DQActorData[] actors;
}

[Serializable]
public class DQActorData
{
    public string actorId;
    public bool hero;
    public string atlasPath;
    public string sheet;
    public int columns;
    public int stride;
    public DQSpriteData[] sprites;
    public DQClipData[] clips;
    public string[] directionSources;
    public string[] staticKeys;
    public string lyingKey;
    public DQPhaseBinding[] phaseBindings;
}

[Serializable]
public class DQSpriteData
{
    public string key;
    public int index;
    public int size;
    public int pivotX;
    public int pivotY;
}

[Serializable]
public class DQPhaseBinding
{
    public string phase;
    public string action;
}

[Serializable]
public class DQClipData
{
    public string id;
    public string[] frameKeys;
    public int[] durationsMs;
    public bool loop;
    public int eventFrame;
    public string sourceClip;
    public string quality;

    public float Seconds
    {
        get
        {
            var ms = 0;
            foreach (var duration in durationsMs)
            {
                ms += duration;
            }
            return ms / 1000f;
        }
    }

    public void Validate()
    {
        if (frameKeys == null || durationsMs == null || frameKeys.Length == 0 || frameKeys.Length != durationsMs.Length)
        {
            throw new InvalidOperationException($"Invalid DQ clip length: {id}");
        }
        for (var i = 0; i < durationsMs.Length; i++)
        {
            if (durationsMs[i] <= 0 || string.IsNullOrEmpty(frameKeys[i]))
            {
                throw new InvalidOperationException($"Invalid DQ clip frame: {id}/{i}");
            }
        }
        if (eventFrame < -1 || eventFrame >= frameKeys.Length || (id.StartsWith("attack_") && eventFrame < 0))
        {
            throw new InvalidOperationException($"Invalid DQ event frame: {id}");
        }
    }
}

public static class DQClipMath
{
    public static int FrameAt(DQClipData clip, float seconds)
    {
        var ms = Mathf.Max(0f, seconds) * 1000f;
        if (clip.loop)
        {
            ms = Mathf.Repeat(ms, clip.Seconds * 1000f);
        }
        var end = 0;
        for (var i = 0; i < clip.durationsMs.Length; i++)
        {
            end += clip.durationsMs[i];
            if (ms < end)
            {
                return i;
            }
        }
        return clip.durationsMs.Length - 1;
    }

    public static int AttackFrame(DQClipData clip, float elapsed, float hitSeconds, bool hasHit)
    {
        var sourceHit = 0f;
        for (var i = 0; i < clip.eventFrame; i++)
        {
            sourceHit += clip.durationsMs[i] / 1000f;
        }
        if (!hasHit && elapsed < hitSeconds)
        {
            if (hitSeconds <= 0f || sourceHit <= 0f)
            {
                return 0;
            }
            return Mathf.Min(clip.eventFrame, FrameAt(clip, sourceHit * Mathf.Clamp01(elapsed / hitSeconds)));
        }
        return FrameAt(clip, sourceHit + Mathf.Max(0f, elapsed - hitSeconds));
    }
}
