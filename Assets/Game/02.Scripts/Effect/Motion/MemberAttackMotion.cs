using UnityEngine;

// CombatService 소유 시계. 준비 중 새 무기는 같은 타격에 합류하고, 회복 중 새 발동은 회복만 끊는다.
public class MemberAttackMotion
{
    public long AttackId { get; private set; }
    public float Elapsed
    {
        get; private set;
    }
    public float Duration
    {
        get; private set;
    }
    public float HitSeconds
    {
        get; private set;
    }
    public SpriteFacing Facing { get; private set; } = SpriteFacing.Front;
    public bool HasHit
    {
        get; private set;
    }
    public bool IsPlaying => Elapsed < Duration;
    public bool IsPreparing => IsPlaying && !HasHit;

    public void Start(MemberMotionData data, SpriteFacing facing, float shortestPeriod)
    {
        AttackId++;
        Facing = facing;
        Elapsed = 0f;
        HasHit = false;
        Duration = Mathf.Min(data.AttackSeconds, shortestPeriod);
        HitSeconds = data.HitSeconds(facing.Direction) * Duration / data.AttackSeconds;
    }

    // 한 번만 타격 경계를 보고한다. 고정 걸음보다 짧은 클립도 누락하지 않는다.
    public bool Advance(float deltaSeconds)
    {
        if (!IsPlaying)
        {
            return false;
        }

        Elapsed += deltaSeconds;
        if (HasHit || Elapsed + 1e-5f < HitSeconds)
        {
            return false;
        }

        HasHit = true;
        return true;
    }

    public void Stop()
    {
        Elapsed = 0f;
        Duration = 0f;
        HasHit = false;
    }

    public int GetFrame(int frameCount) => Mathf.Min(frameCount - 1, Mathf.FloorToInt(Elapsed / Duration * frameCount + 1e-5f));
}
