using UnityEngine;

public static class TiltedCameraUtils
{
    public static void Configure(Camera camera, float angle, bool isOrthographic, float fieldOfView, float heightFraction, Vector3 focus)
    {
        camera.orthographic = isOrthographic;
        camera.fieldOfView = fieldOfView;
        camera.orthographicSize = 1f / (2f * heightFraction);
        var distance = isOrthographic ? 35f : 1f / (2f * heightFraction * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f));
        camera.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
        camera.transform.position = focus - camera.transform.forward * distance;
    }

    public static Vector3 GroundPoint(Camera camera, Vector2 viewport)
    {
        var ray = camera.ViewportPointToRay(viewport);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out var distance))
        {
            throw new System.InvalidOperationException("Camera viewport does not intersect the floor.");
        }
        return ray.GetPoint(distance);
    }
}
