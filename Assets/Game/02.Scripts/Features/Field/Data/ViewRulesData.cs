using System;
using UnityEngine;

[Serializable]
public class ViewRulesData
{
    // Projection snapshot supplied by the camera owner. Services never depend on Camera.
    public PerspectiveFrame Frame { get; set; }
    public bool HasFrame => Frame.Height > 0;
    public float WorldScale { get; private set; } = .0113f;
    public TerrainHeightGrid Terrain { get; private set; } = TerrainHeightGrid.Flat;
    public void SetProjection(PerspectiveFrame frame, float worldScale, TerrainHeightGrid terrain)
    {
        Frame = frame;
        WorldScale = worldScale;
        Terrain = terrain;
    }
    public Vector2 Direction(Vector2 floor, Vector2 delta)
    {
        var from = IsoProjection.FloorToWorld(floor, WorldScale, Terrain);
        var to = IsoProjection.FloorToWorld(floor + delta, WorldScale, Terrain);
        return Frame.Direction(from, to - from);
    }
    public Vector3 IndicatorPosition(Vector3 foot, float bodyHeight, float offsetSourcePixels)
    {
        var head = foot + TiltedViewMath.UprightOffset(bodyHeight, TiltDegrees);
        return head + (HasFrame ? Frame.PixelOffset(head, Vector2.up * offsetSourcePixels * Frame.Height / 540f)
            : TiltedViewMath.UprightOffset(offsetSourcePixels / Const.PixelsPerUnit, TiltDegrees));
    }
    public Vector3 IndicatorOffset(Vector3 origin, Vector2 sourcePixels)
    {
        return HasFrame ? Frame.PixelOffset(origin, sourcePixels * Frame.Height / 540f)
            : TiltedViewMath.UprightRotation(TiltDegrees) * ((Vector3)sourcePixels / Const.PixelsPerUnit);
    }
    public Vector2 GameplayHalfExtent(int width, int height)
    {
        // Existing logical game envelope, independent of the new visual frustum.
        // Preserve spawn/recycle/centre rules at every aspect and output height.
        var scale = PixelScaleMath.Scale(height, targetFraction, 48, minScale);
        var halfHeight = height / (2f * 48 * scale);
        return new Vector2(halfHeight * width / height, halfHeight / .5f + terrainDepthPad);
    }

    [SerializeField] private float tiltDegrees;
    [SerializeField] private float distance;
    [SerializeField] private float nearClip;
    [SerializeField] private float farClip;
    [SerializeField] private float targetFraction;
    [SerializeField] private int minScale;
    [SerializeField] private float facingHoldDegrees;
    [SerializeField] private float terrainDepthPad;
    public float TerrainDepthPad => terrainDepthPad;
    public float TiltDegrees => tiltDegrees;
    public float SinTilt => Mathf.Sin(tiltDegrees * Mathf.Deg2Rad);
    public float Distance => distance;
    public float NearClip => nearClip;
    public float FarClip => farClip;
    public float TargetFraction => targetFraction;
    public int MinScale => minScale;
    public float FacingHoldDegrees => facingHoldDegrees;
}
