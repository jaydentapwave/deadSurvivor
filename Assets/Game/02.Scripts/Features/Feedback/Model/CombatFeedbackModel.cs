using System;
using System.Collections.Generic;
using UnityEngine;

// 이번 프레임에 쌓인 피해 사실과 이번 판 피드백 셈(F6 기획서 A·H절, 저장 안 함).
// 쓰는 쪽: PartyService·MonsterService(피해 적용 끝의 기록), 피드백 Presenter(읽고 비움·셈).
// 원정대 기록은 HpLossPresenter만 비우고, 흔들림은 기록 대신 HasPendingHitReaction 깃발을 쓴다 — 두 Presenter의 순서에 기대지 않는다.
public class CombatFeedbackModel
{
    private readonly List<MemberHitRecord> _memberHitList = new(16);
    private readonly List<MonsterHitRecord> _monsterHitList = new(256);

    public event Action<MonsterHitRecord> MonsterHit;

    public IReadOnlyList<MemberHitRecord> MemberHitList => _memberHitList;
    public IReadOnlyList<MonsterHitRecord> MonsterHitList => _monsterHitList;
    // 마지막 소비 뒤 받음 > 0인 원정대 피격이 있었다
    public bool HasPendingHitReaction { get; private set; }

    // 이번 판 셈
    public int DamageNumberShownTotal { get; private set; }
    public int DamageNumberPeak { get; private set; }
    public int ShakeCount { get; private set; }

    // taken = 방어를 뺀 받음(보호막 흡수 전)
    public void AddMemberHit(int memberIndex, float taken, float hpBefore, float hpAfter)
    {
        _memberHitList.Add(new MemberHitRecord(memberIndex, taken, hpBefore, hpAfter));
        if (taken > 0f)
        {
            HasPendingHitReaction = true;
        }
    }

    // position = 맞은 순간의 바닥 좌표, data = 몬스터 데이터(숫자 자리 = 그 몸 그림의 머리 위)
    public void AddMonsterHit(int monsterId, float amount, Vector2 position, MonsterData data)
    {
        var record = new MonsterHitRecord(monsterId, amount, position, data);
        _monsterHitList.Add(record);
        MonsterHit?.Invoke(record);
    }

    public void ClearMemberHits() => _memberHitList.Clear();

    public void ClearMonsterHits() => _monsterHitList.Clear();

    public bool ConsumeHitReaction()
    {
        var hasHit = HasPendingHitReaction;
        HasPendingHitReaction = false;
        return hasHit;
    }

    public void AddDamageNumbersShown(int count, int visibleCount)
    {
        DamageNumberShownTotal += count;
        DamageNumberPeak = Mathf.Max(DamageNumberPeak, visibleCount);
    }

    public void ResetDamageNumberCounts()
    {
        DamageNumberShownTotal = 0;
        DamageNumberPeak = 0;
    }

    public void AddShake() => ShakeCount++;

    public void ResetShakeCount() => ShakeCount = 0;
}

public readonly struct MemberHitRecord
{
    public readonly int MemberIndex;
    public readonly float Taken;
    public readonly float HpBefore;
    public readonly float HpAfter;

    public MemberHitRecord(int memberIndex, float taken, float hpBefore, float hpAfter)
    {
        MemberIndex = memberIndex;
        Taken = taken;
        HpBefore = hpBefore;
        HpAfter = hpAfter;
    }
}

public readonly struct MonsterHitRecord
{
    public readonly int MonsterId;
    public readonly float Amount;
    public readonly Vector2 Position;
    public readonly MonsterData Data;

    public MonsterHitRecord(int monsterId, float amount, Vector2 position, MonsterData data)
    {
        MonsterId = monsterId;
        Amount = amount;
        Position = position;
        Data = data;
    }
}
