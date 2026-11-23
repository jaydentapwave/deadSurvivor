using UnityEngine;

// The sole camera-transform owner; focus and shake can arrive in either order.
[RequireComponent(typeof(Camera))]
public sealed class TiltedCameraRig : MonoBehaviour
{
    public const float DQDistance = 31.896974f;
    public const float FocusHeight = .95f;
    public float Distance { get; private set; }
    public Vector2 ShakeOffset { get; private set; }
    public int Scale { get; private set; }
    public PerspectiveFrame Frame => new(transform.position, transform.rotation, _camera.fieldOfView,
        _camera.aspect, Mathf.Max(1, _camera.pixelHeight), _camera.nearClipPlane, _camera.farClipPlane);
    private Camera _camera;
    private Vector3 _focus;

    // Legacy call shape retained for scene/probe consumers; no orthographic mode remains.
    public void Initialize(float tiltDegrees, float distance, float nearClip, float farClip, float targetFraction, int minScale)
    {
        _camera = GetComponent<Camera>();
        Distance = distance;
        transform.rotation = TiltedViewMath.UprightRotation(tiltDegrees);
        _camera.orthographic = false;
        _camera.fieldOfView = 20f;
        _camera.nearClipPlane = nearClip;
        _camera.farClipPlane = farClip;
        UpdateScreen(_camera.pixelWidth, _camera.pixelHeight);
        Apply();
    }
    public void UpdateScreen(int width, int height)
    {
        Scale = Mathf.Max(1, Mathf.RoundToInt(height / (2f * Mathf.Tan(10f * Mathf.Deg2Rad) * Distance * 48f)));
    }
    public void SetFocus(Vector3 focus)
    {
        _focus = focus;
        Apply();
    }
    public void SetShakeOffset(Vector2 offset)
    {
        ShakeOffset = offset;
        Apply();
    }
    private void Apply()
    {
        transform.position = _focus + Vector3.up * FocusHeight - transform.forward * Distance
            + transform.right * ShakeOffset.x + transform.up * ShakeOffset.y;
    }
}
