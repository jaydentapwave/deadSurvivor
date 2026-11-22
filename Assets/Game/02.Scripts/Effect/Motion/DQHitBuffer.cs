using System;
using System.Collections.Generic;

public readonly struct DQHitRecord
{
    public readonly long AttackId;
    public readonly long Step;
    public readonly SpriteFacing Facing;
    public DQHitRecord(long attackId, long step, SpriteFacing facing)
    {
        AttackId = attackId;
        Step = step;
        Facing = facing;
    }
}

public readonly struct DQHitDisposition
{
    public readonly DQHitRecord Hit;
    public readonly long AppliedAttackId;
    public readonly int Batch;
    public readonly string Reason;
    public DQHitDisposition(DQHitRecord hit, long appliedAttackId, int batch, string reason)
    {
        Hit = hit;
        AppliedAttackId = appliedAttackId;
        Batch = batch;
        Reason = reason;
    }
}

// The newest actual hit wins a rendered batch. Acknowledging a camera render never
// consumes records that arrived after that batch was applied.
public sealed class DQHitBuffer
{
    private readonly List<DQHitRecord> _pendingList = new();
    public event Action<DQHitDisposition> Recorded;
    public int Count => _pendingList.Count;
    public int Received { get; private set; }
    public int Rendered { get; private set; }
    public int Coalesced { get; private set; }
    public int Suppressed { get; private set; }
    public DQHitRecord Latest => _pendingList[_pendingList.Count - 1];

    public void Add(DQHitRecord hit)
    {
        _pendingList.Add(hit);
        Received++;
    }

    public void Acknowledge(long appliedAttackId)
    {
        var count = 0;
        foreach (var hit in _pendingList)
        {
            if (hit.AttackId <= appliedAttackId)
            {
                Recorded?.Invoke(new DQHitDisposition(hit, appliedAttackId, Rendered + 1,
                    hit.AttackId == appliedAttackId ? "rendered" : "coalesced"));
                count++;
            }
        }
        _pendingList.RemoveAll(hit => hit.AttackId <= appliedAttackId);
        if (count > 0)
        {
            Rendered++;
            Coalesced += count - 1;
        }
    }

    public void Clear(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            throw new ArgumentException("A visual hit suppression requires its cause.", nameof(reason));
        }
        foreach (var hit in _pendingList)
        {
            Recorded?.Invoke(new DQHitDisposition(hit, 0, 0, reason));
        }
        Suppressed += _pendingList.Count;
        _pendingList.Clear();
    }
}
