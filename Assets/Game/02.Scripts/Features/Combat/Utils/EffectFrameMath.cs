using System;
using UnityEngine;

// 표현 시간과 방향만 계산한다. 전투 시계·명중·거리에는 관여하지 않는다.
public static class EffectFrameMath
{
    public static int FrameAt(float elapsed, int frameCount, float fps, bool loop)
    {
        if (frameCount <= 0 || fps <= 0f || float.IsNaN(fps) || float.IsInfinity(fps) || float.IsNaN(elapsed) || float.IsInfinity(elapsed))
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        }
        var frame = Math.Floor(Math.Max(0, elapsed) * (double)fps + 0.000001);
        return loop ? (int)(frame % frameCount) : (int)Math.Min(frame, frameCount - 1);
    }

    public static (int index, bool flipX, bool flipY) DirectionSprite(float screenAngle)
    {
        var sector = ((Mathf.RoundToInt(screenAngle / 45f) % 8) + 8) % 8;
        var index = sector % 4;
        if (index == 3)
        {
            index = 1;
        }
        return (index, sector > 2 && sector < 6, sector >= 5);
    }

    public static string DirectionName(int index) => index == 0 ? "right" : index == 1 ? "upright" : "up";
}
