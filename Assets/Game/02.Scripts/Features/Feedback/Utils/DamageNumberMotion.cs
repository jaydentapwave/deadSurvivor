using System.Collections.Generic;
using UnityEngine;

// 피해 숫자 순수 계산(F6 기획서 B절): 한 프레임 합치기, 표시 값, 나이에 따른 올라감·투명도, 상한 교체 수
public static class DamageNumberMotion
{
    // 같은 몬스터 번호의 기록을 하나로 합친다. 자리·반지름은 그 번호의 첫 기록, 순서는 첫 기록 순서.
    // indexDict는 호출 쪽이 미리 잡아 둔 작업용(번호 → result 안 위치)
    public static void Merge(IReadOnlyList<MonsterHitRecord> recordList, List<MonsterHitRecord> resultList,
        Dictionary<int, int> indexDict)
    {
        resultList.Clear();
        indexDict.Clear();
        foreach (var record in recordList)
        {
            if (indexDict.TryGetValue(record.MonsterId, out var index))
            {
                var first = resultList[index];
                resultList[index] = new MonsterHitRecord(first.MonsterId, first.Amount + record.Amount, first.Position, first.Data);
                continue;
            }

            indexDict.Add(record.MonsterId, resultList.Count);
            resultList.Add(record);
        }
    }

    // max(1, 반올림(합)). 반올림은 0.5에서 올린다
    public static int GetValue(float sum) => Mathf.Max(1, Mathf.FloorToInt(sum + 0.5f));

    // 시작 자리에서 위로 올라간 거리(가상 해상도 픽셀)
    public static float GetRisePx(float age, DamageNumberRulesData rules) =>
        rules.RisePx * Mathf.Clamp01(age / rules.Lifetime);

    // fadeStartRatio 전에는 1, 그 뒤 lifetime에서 0이 되게 줄어든다
    public static float GetAlpha(float age, DamageNumberRulesData rules)
    {
        var fadeStart = rules.Lifetime * rules.FadeStartRatio;
        if (age < fadeStart)
        {
            return 1f;
        }

        var fadeSeconds = rules.Lifetime - fadeStart;
        return fadeSeconds > 0f ? Mathf.Clamp01(1f - (age - fadeStart) / fadeSeconds) : 0f;
    }

    // 같은 대상의 새 숫자는 아래, 오래된 숫자는 글자 높이 + 2픽셀 간격으로 위에 쌓는다.
    // 대상이 움직여도 번호로 묶고, 다른 대상과 이전 호출의 슬롯 상태는 섞지 않는다.
    public static Vector2 GetStackOffsetPx(IReadOnlyList<DamageNumberEntry> entryList, int index, float fontSizePx)
    {
        var monsterId = entryList[index].MonsterId;
        if (monsterId < 0)
        {
            return Vector2.zero;
        }

        var rank = 0;
        for (var i = index + 1; i < entryList.Count; i++)
        {
            if (entryList[i].MonsterId == monsterId)
            {
                rank++;
            }
        }

        var horizontal = rank == 0 ? 0 : rank % 2 == 1 ? 3 : -3;
        return new Vector2(horizontal, rank * (Mathf.Ceil(fontSizePx) + 2));
    }

    // 이동 중인 대상도 한 열을 공유해야 이전 발 위치 차이로 줄 간격이 사라지지 않는다.
    public static Vector3 GetStackOrigin(IReadOnlyList<DamageNumberEntry> entryList, int index)
    {
        var entry = entryList[index];
        if (entry.MonsterId >= 0)
        {
            for (var i = entryList.Count - 1; i > index; i--)
            {
                if (entryList[i].MonsterId == entry.MonsterId)
                {
                    return entryList[i].StartPosition;
                }
            }
        }

        return entry.StartPosition;
    }

    // 새 숫자 하나를 내기 전 거둘 가장 오래된 숫자 수
    public static int GetEvictCount(int activeCount, int maxActive) => activeCount >= maxActive ? activeCount - maxActive + 1 : 0;
}
