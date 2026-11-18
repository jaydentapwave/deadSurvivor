using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

// Only foreground scenery changes material. Party art and simulation stay untouched.
public sealed class FieldOcclusionPresenter : ITickable
{
    private readonly FieldView _field;
    private readonly Camera _camera;
    private readonly IReadOnlyList<MemberView> _members;
    private readonly MonsterViewRegistry _monsters;
    public int FadedCount { get; private set; }

    public FieldOcclusionPresenter(FieldView field, Camera camera, IReadOnlyList<MemberView> members,
        MonsterViewRegistry monsters)
    {
        _field = field;
        _camera = camera;
        _members = members;
        _monsters = monsters;
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
            var cutout = new Rect();
            foreach (var member in _members)
            {
                IncludeCoveredActor(member.BodyRenderer, member.transform, rect, depth, ref cutout, ref isCovered);
            }
            foreach (var monster in _monsters.Views)
            {
                IncludeCoveredActor(monster.BodyRenderer, monster.transform, rect, depth, ref cutout, ref isCovered);
            }
            if (isCovered)
            {
                prop.SetOcclusionRect(cutout);
            }
            prop.SetFaded(isCovered);
            if (isCovered)
            {
                FadedCount++;
            }
        }
    }

    private void IncludeCoveredActor(Renderer body, Transform actor, Rect propRect, float propDepth,
        ref Rect cutout, ref bool hasCutout)
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
        cutout = hasCutout ? Rect.MinMaxRect(Mathf.Min(cutout.xMin, actorRect.xMin), Mathf.Min(cutout.yMin, actorRect.yMin),
            Mathf.Max(cutout.xMax, actorRect.xMax), Mathf.Max(cutout.yMax, actorRect.yMax)) : actorRect;
        hasCutout = true;
    }
}
