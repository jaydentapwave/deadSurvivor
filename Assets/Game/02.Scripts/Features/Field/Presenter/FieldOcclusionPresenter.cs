using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

// Only foreground scenery changes material. Party art and simulation stay untouched.
public sealed class FieldOcclusionPresenter : ITickable
{
    private readonly FieldView _field;
    private readonly Camera _camera;
    private readonly IReadOnlyList<MemberView> _members;
    public int FadedCount { get; private set; }

    public FieldOcclusionPresenter(FieldView field, Camera camera, IReadOnlyList<MemberView> members)
    {
        _field = field;
        _camera = camera;
        _members = members;
    }

    public void Tick()
    {
        FadedCount = 0;
        foreach (var prop in _field.Occluders)
        {
            var isCovered = false;
            var bounds = prop.Bounds;
            var rect = OcclusionMath.ProjectBounds(_camera, bounds);
            var depth = _camera.WorldToViewportPoint(prop.Anchor.position).z;
            foreach (var member in _members)
            {
                var body = member.BodyRenderer;
                if (!body.enabled || !body.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (OcclusionMath.Covers(rect, depth, OcclusionMath.ProjectBounds(_camera, body.bounds),
                    _camera.WorldToViewportPoint(member.transform.position).z))
                {
                    isCovered = true;
                    break;
                }
            }
            prop.SetFaded(isCovered);
            if (isCovered)
            {
                FadedCount++;
            }
        }
    }
}
