using System;

// Three discrete poses, ping-pong at quarter-second intervals. No sub-cell phase.
public static class AmbientMotionMath
{
    public static int FrameAt(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        // Reduce before scaling to keep arbitrarily long valid times bounded.
        var phase = (int)Math.Floor((seconds % 1.0) * 4);
        return phase < 3 ? phase : 1;
    }
}
