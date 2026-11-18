using UnityEngine;

// Owns the camera transform. Focus and shake are separate inputs, in either order.
[RequireComponent(typeof(Camera))]
public sealed class TiltedCameraRig : MonoBehaviour
{
    public float Distance { get; private set; }
    public Vector2 ShakeOffset { get; private set; }
    public int Scale { get; private set; }
    private Camera _camera;
    private Vector3 _focus;
    private float _sinTilt;
    private float _targetFraction;
    private int _minScale;
    private int _width;
    private int _height;

    public void Initialize(float tiltDegrees, float distance, float nearClip, float farClip, float targetFraction, int minScale)
    {
        _camera = GetComponent<Camera>();
        Distance = distance;
        _sinTilt = Mathf.Sin(tiltDegrees * Mathf.Deg2Rad);
        _targetFraction = targetFraction;
        _minScale = minScale;
        _width = _height = 0;
        transform.rotation = TiltedViewMath.UprightRotation(tiltDegrees);
        _camera.orthographic = true;
        _camera.nearClipPlane = nearClip;
        _camera.farClipPlane = farClip;
        UpdateScreen(_camera.pixelWidth, _camera.pixelHeight);
        Apply();
    }
    public void UpdateScreen(int width, int height)
    {
        if (width == _width && height == _height) return;
        _width = width; _height = height;
        Scale = PixelScaleMath.Scale(height, _targetFraction, 48, _minScale);
        _camera.orthographicSize = PixelScaleMath.OrthographicSize(height, 48, Scale);
    }
    public void SetFocus(Vector3 focus) { _focus = focus; Apply(); }
    public void SetShakeOffset(Vector2 offset) { ShakeOffset = offset; Apply(); }
    private void Apply()
    {
        transform.position = TiltedViewMath.SnapToTexels(_focus, _sinTilt, 48) - transform.forward * Distance
            + transform.right * ShakeOffset.x + transform.up * ShakeOffset.y;
    }
}
