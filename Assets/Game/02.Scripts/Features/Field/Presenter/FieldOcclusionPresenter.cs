using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

// B7 reveals party members only. Monsters behind scenery do not fade that scenery.
public sealed class FieldOcclusionPresenter : ILateTickable
{
    private readonly FieldView _field;
    private readonly Camera _camera;
    private readonly IReadOnlyList<MemberView> _members;
    private readonly List<Rect> _cutouts = new();
    public int FadedCount { get; private set; }

    public FieldOcclusionPresenter(FieldView field, Camera camera, IReadOnlyList<MemberView> members)
    {
        _field = field;
        _camera = camera;
        _members = members;
    }

    // Kept for existing explicit callers/tests; the registered lifecycle is LateTick.
    public void Tick() => LateTick();
    public void LateTick()
    {
        FadedCount = 0;
        foreach (var prop in _field.Occluders)
        {
            _cutouts.Clear();
            var bounds = prop.Bounds;
            var rect = OcclusionMath.ProjectBounds(_camera, bounds);
            var depth = _camera.WorldToViewportPoint(prop.Anchor.position).z;
            foreach (var member in _members)
            {
                IncludeCoveredActor(member.BodyRenderer, member.transform, rect, depth);
            }
            var isCovered = _cutouts.Count > 0;
            if (isCovered)
            {
                prop.SetOcclusionRects(_cutouts);
            }
            prop.SetFaded(isCovered);
            if (isCovered)
            {
                FadedCount++;
            }
        }
    }

    private void IncludeCoveredActor(Renderer body, Transform actor, Rect propRect, float propDepth)
    {
        if (!body.enabled || !body.gameObject.activeInHierarchy)
        {
            return;
        }
        var actorRect = OcclusionMath.ProjectBounds(_camera, body.bounds);
        if (!OcclusionMath.Covers(propRect, propDepth, actorRect, _camera.WorldToViewportPoint(actor.position).z))
        {
            return;
        }
        // A small two-output-pixel margin reveals feet and hair without fading the whole prop.
        var margin = 2f / Mathf.Max(1, _camera.pixelHeight);
        actorRect = Rect.MinMaxRect(actorRect.xMin - margin, actorRect.yMin - margin,
            actorRect.xMax + margin, actorRect.yMax + margin);
        _cutouts.Add(actorRect);
    }
}
