using UnityEngine;

public static class UiDotScale
{
    public static float CanvasPerTexel(float desiredCanvasPerTexel, float canvasScale)
    {
        return Mathf.Max(1, Mathf.FloorToInt(desiredCanvasPerTexel * canvasScale)) / canvasScale;
    }
}
