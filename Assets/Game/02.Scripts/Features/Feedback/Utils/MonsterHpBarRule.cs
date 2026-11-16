using System.Collections.Generic;

// 몬스터 HP 막대가 보이는 조건(F6 기획서 E절). 몬스터 id·행동 이름을 코드에 쓰지 않고 데이터(alwaysShowBehaviors)로 강한 적을 고른다.
public static class MonsterHpBarRule
{
    // 접촉 거리에 멈춘 적의 실수 오차 여유(ContactBehavior와 같은 값). 붙어서 때리는 거리 판정이 행동과 같게
    private const float ContactTolerance = 0.001f;

    private static readonly System.Comparison<MonsterHpBarCandidate> CandidateOrder = CompareCandidates;

    // 지금 원정대를 공격 중인가. distance = 가장 가까운 산 대원과의 바닥 거리, 산 대원이 없으면 호출하지 않는다
    public static bool IsAttacking(MonsterBehavior behavior, MonsterPhase phase, float distance, float contactDistance,
        float projectileRange)
    {
        switch (behavior)
        {
            case MonsterBehavior.Contact:
            case MonsterBehavior.Armored:
                return distance <= contactDistance + ContactTolerance;
            case MonsterBehavior.Projectile:
                return distance < projectileRange;
            case MonsterBehavior.Charge:
                return phase == MonsterPhase.Windup || phase == MonsterPhase.Dash;
            case MonsterBehavior.Bomb:
                return phase == MonsterPhase.Fuse;
            default:
                GameLogger.LogWarning($"[MonsterHpBarRule] Unknown behavior {behavior}");
                return false;
        }
    }

    // 강한 적이거나, 공격 중이거나, 마지막 공격 중 시각에서 lingerSeconds가 아직 안 지났다
    public static bool IsCandidate(bool isStrong, bool isAttacking, float now, float lastAttackTime, float lingerSeconds) =>
        isStrong || isAttacking || now - lastAttackTime < lingerSeconds;

    // 후보에서 maxActive개까지: 강한 적 먼저, 다음은 가까운 순, 같으면 번호 순. candidateList는 정렬된다
    public static void SelectShown(List<MonsterHpBarCandidate> candidateList, int maxActive, HashSet<int> shownIdSet)
    {
        shownIdSet.Clear();
        if (candidateList.Count > maxActive)
        {
            candidateList.Sort(CandidateOrder);
        }

        for (var i = 0; i < candidateList.Count && i < maxActive; i++)
        {
            shownIdSet.Add(candidateList[i].MonsterId);
        }
    }

    private static int CompareCandidates(MonsterHpBarCandidate a, MonsterHpBarCandidate b)
    {
        if (a.IsStrong != b.IsStrong)
        {
            return a.IsStrong ? -1 : 1;
        }

        var byDistance = a.Distance.CompareTo(b.Distance);
        return byDistance != 0 ? byDistance : a.MonsterId.CompareTo(b.MonsterId);
    }
}

public readonly struct MonsterHpBarCandidate
{
    public readonly int MonsterId;
    public readonly bool IsStrong;
    public readonly float Distance;

    public MonsterHpBarCandidate(int monsterId, bool isStrong, float distance)
    {
        MonsterId = monsterId;
        IsStrong = isStrong;
        Distance = distance;
    }
}
