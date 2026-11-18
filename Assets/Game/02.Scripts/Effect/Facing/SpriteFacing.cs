using UnityEngine;

public enum FacingDirection
{
    Down, DownRight, Right, UpRight, Up
}

public readonly struct SpriteFacing
{
    public static readonly SpriteFacing Front = new(FacingDirection.DownRight, false);
    public FacingDirection Direction
    {
        get;
    }
    public bool IsFlipped
    {
        get;
    }
    public string Label => Direction switch
    {
        FacingDirection.Down => "down",
        FacingDirection.Up => "up",
        FacingDirection.Right => IsFlipped ? "left" : "right",
        FacingDirection.UpRight => IsFlipped ? "up-left" : "up-right",
        _ => IsFlipped ? "down-left" : "down-right"
    };
    public SpriteFacing(FacingDirection direction, bool isFlipped)
    {
        Direction = direction;
        IsFlipped = isFlipped;
    }
    public static SpriteFacing Next(SpriteFacing last, Vector2 delta, float minMove, float holdDegrees)
    {
        if (Mathf.Abs(delta.x) < minMove && Mathf.Abs(delta.y) < minMove)
        {
            return last;
        }
        var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        var center = ((int)last.Direction - 2) * 45f;
        if (last.IsFlipped)
        {
            center = 180f - center;
        }
        if (Mathf.Abs(Mathf.DeltaAngle(center, angle)) <= 22.5f + holdDegrees)
        {
            return last;
        }
        var sector = Mathf.RoundToInt(angle / 45f);
        return sector switch
        {
            0 => new(FacingDirection.Right, false),
            -1 => new(FacingDirection.DownRight, false),
            -2 => new(FacingDirection.Down, last.IsFlipped),
            1 => new(FacingDirection.UpRight, false),
            2 => new(FacingDirection.Up, last.IsFlipped),
            -3 => new(FacingDirection.DownRight, true),
            3 => new(FacingDirection.UpRight, true),
            _ => new(FacingDirection.Right, true)
        };
    }
    // The five source pictures cover -90..90 degrees. Lower direction wins a tie.
    public static FacingDirection ResolveAvailable(FacingDirection wanted, int availableMask)
    {
        var best = wanted;
        var distance = int.MaxValue;
        for (var i = 0; i < 5; i++)
        {
            if ((availableMask & (1 << i)) == 0)
            {
                continue;
            }
            var candidate = Mathf.Abs(i - (int)wanted);
            if (candidate >= distance)
            {
                continue;
            }
            distance = candidate;
            best = (FacingDirection)i;
        }
        return best;
    }
}
