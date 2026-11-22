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

// The newest actual hit wins a rendered batch. Acknowledging a camera render never
// consumes records that arrived after that batch was applied.
public sealed class DQHitBuffer
{
    private readonly List<DQHitRecord> _pendingList = new();
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
        var count = _pendingList.RemoveAll(hit => hit.AttackId <= appliedAttackId);
        if (count > 0)
        {
            Rendered++;
            Coalesced += count - 1;
        }
    }

    public void Clear()
    {
        Suppressed += _pendingList.Count;
        _pendingList.Clear();
    }
}
