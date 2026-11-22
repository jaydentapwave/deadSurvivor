using System;
using UnityEngine;

// Immutable projection snapshot. No game state, Camera.main, or global mutable camera.
public readonly struct PerspectiveFrame
{
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly float FieldOfView;
    public readonly float Aspect;
    public readonly int Height;
    public readonly float Near;
    public readonly float Far;
    public Vector3 Forward => Rotation * Vector3.forward;
    public Vector3 Up => Rotation * Vector3.up;
    public Vector3 Right => Rotation * Vector3.right;
    public float FocalPixels => Height / (2f * Mathf.Tan(FieldOfView * Mathf.Deg2Rad * .5f));

    public PerspectiveFrame(Vector3 position, Quaternion rotation, float fieldOfView, float aspect, int height, float near = .1f, float far = 180f)
    {
        if (!(fieldOfView > 0 && fieldOfView < 180 && aspect > 0 && height > 0 && near > 0 && far > near))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldOfView), "Finite perspective dimensions and near/far planes required.");
        }
        Position = position;
        Rotation = rotation;
        FieldOfView = fieldOfView;
        Aspect = aspect;
        Height = height;
        Near = near;
        Far = far;
    }

    // Pixel position measured from bottom left; z is camera-space depth.
    public Vector3 Project(Vector3 world)
    {
        var local = Quaternion.Inverse(Rotation) * (world - Position);
        return new Vector3(Height * Aspect * .5f + local.x * FocalPixels / local.z,
            Height * .5f + local.y * FocalPixels / local.z, local.z);
    }

    public Ray Ray(Vector2 viewport)
    {
        var tangent = Mathf.Tan(FieldOfView * Mathf.Deg2Rad * .5f);
        return new Ray(Position, Rotation * new Vector3((2 * viewport.x - 1) * tangent * Aspect,
            (2 * viewport.y - 1) * tangent, 1));
    }

    public bool TryPlane(Vector2 viewport, float groundHeight, out Vector3 world)
    {
        var ray = Ray(viewport);
        world = default;
        if (Mathf.Abs(ray.direction.y) < 1e-7f)
        {
            return false;
        }
        var distance = (groundHeight - Position.y) / ray.direction.y;
        var candidate = ray.GetPoint(distance);
        var depth = Vector3.Dot(candidate - Position, Forward);
        if (distance < 0 || depth < Near || depth > Far)
        {
            return false;
        }
        world = candidate;
        return true;
    }

    public Vector2 Direction(Vector3 origin, Vector3 delta)
    {
        return (Vector2)(Project(origin + delta) - Project(origin));
    }

    // Invert the exact local XZ Jacobian, then preserve gameplay speed by normalizing.
    public Vector2 InputDirection(Vector3 floor, Vector2 input, Vector3? groundNormal = null)
    {
        if (input == Vector2.zero)
        {
            return Vector2.zero;
        }
        var local = Quaternion.Inverse(Rotation) * (floor - Position);
        var normal = groundNormal ?? Vector3.up;
        var x = Quaternion.Inverse(Rotation) * new Vector3(1, -normal.x / normal.y, 0);
        var z = Quaternion.Inverse(Rotation) * new Vector3(0, -normal.z / normal.y, 1);
        var a = x.x * local.z - local.x * x.z;
        var b = z.x * local.z - local.x * z.z;
        var c = x.y * local.z - local.y * x.z;
        var d = z.y * local.z - local.y * z.z;
        var determinant = a * d - b * c;
        if (Mathf.Abs(determinant) < 1e-7f)
        {
            throw new InvalidOperationException("[PerspectiveFrame] Ground input Jacobian is singular.");
        }
        return new Vector2((d * input.x - b * input.y) / determinant,
            (a * input.y - c * input.x) / determinant).normalized;
    }

    // Snap only a visual child in its camera-parallel plane, never its gameplay foot.
    public Vector3 SnapCard(Vector3 position, Vector2 pivot, float pixelsPerUnit)
    {
        var corner = position - Right * (pivot.x / pixelsPerUnit) - Up * (pivot.y / pixelsPerUnit);
        var pixel = Project(corner);
        var pitch = FocalPixels / (pixel.z * pixelsPerUnit);
        var snap = new Vector2(Mathf.Round(pixel.x / pitch) * pitch - pixel.x,
            Mathf.Round(pixel.y / pitch) * pitch - pixel.y) * (pixel.z / FocalPixels);
        return position + Right * snap.x + Up * snap.y;
    }

    public Vector3 PixelOffset(Vector3 anchor, Vector2 pixels)
    {
        var depth = Vector3.Dot(anchor - Position, Forward);
        return (Right * pixels.x + Up * pixels.y) * (depth / FocalPixels);
    }
}

public readonly struct FloorViewBounds
{
    public readonly Vector2 Min;
    public readonly Vector2 Max;
    public FloorViewBounds(PerspectiveFrame frame, float groundHeight)
    {
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < 4; i++)
        {
            if (!frame.TryPlane(new Vector2(i & 1, (i >> 1) & 1), groundHeight, out var point))
            {
                throw new InvalidOperationException("[FloorViewBounds] Screen corner misses the floor within clip planes.");
            }
            min = Vector2.Min(min, new Vector2(point.x, point.z));
            max = Vector2.Max(max, new Vector2(point.x, point.z));
        }
        Min = min;
        Max = max;
    }
    public bool Contains(Vector2 world) => world.x >= Min.x && world.x <= Max.x && world.y >= Min.y && world.y <= Max.y;
}

public static class PerspectiveViewMath
{
    // Exact first intersection with the same two triangles per cell as TerrainMeshMath.
    // Used for picking/verification, not per-monster per-frame projection.
    public static bool TryTerrain(PerspectiveFrame frame, Vector2 viewport, TerrainHeightGrid grid, out Vector3 point)
    {
        var ray = frame.Ray(viewport);
        var nearest = float.PositiveInfinity;
        point = default;
        for (var j = 0; j < grid.Depth - 1; j++)
        {
            for (var i = 0; i < grid.Width - 1; i++)
            {
                var a = new Vector3(grid.OriginX + i * grid.Cell, grid.Height(i, j), grid.OriginZ + j * grid.Cell);
                var b = new Vector3(a.x + grid.Cell, grid.Height(i + 1, j), a.z);
                var c = new Vector3(b.x, grid.Height(i + 1, j + 1), a.z + grid.Cell);
                var d = new Vector3(a.x, grid.Height(i, j + 1), c.z);
                Check(a, b, c);
                Check(a, c, d);
            }
        }
        if (float.IsPositiveInfinity(nearest))
        {
            return false;
        }
        point = ray.GetPoint(nearest);
        return true;
        void Check(Vector3 a, Vector3 b, Vector3 c)
        {
            var e1 = b - a;
            var e2 = c - a;
            var h = Vector3.Cross(ray.direction, e2);
            var determinant = Vector3.Dot(e1, h);
            if (Mathf.Abs(determinant) < 1e-7f)
            {
                return;
            }
            var s = ray.origin - a;
            var u = Vector3.Dot(s, h) / determinant;
            var q = Vector3.Cross(s, e1);
            var v = Vector3.Dot(ray.direction, q) / determinant;
            var t = Vector3.Dot(e2, q) / determinant;
            var depth = t * Vector3.Dot(ray.direction, frame.Forward);
            if (u >= -1e-6f && v >= -1e-6f && u + v <= 1.000001f && depth >= frame.Near && depth <= frame.Far && t < nearest)
            {
                nearest = t;
            }
        }
    }
}
