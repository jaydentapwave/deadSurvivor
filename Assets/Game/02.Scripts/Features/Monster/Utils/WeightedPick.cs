using System.Collections.Generic;

// 가중치 뽑기(순수 함수). 예전 판의 100칸 유형 목록에서 고르게 뽑기와 같은 뜻(기획서 적용안 "바꿀 것").
public static class WeightedPick
{
    // roll ∈ [0, 1). 누적 가중치가 roll × 합을 처음 넘는 칸의 몬스터 id. 실수 오차로 끝까지 가면 마지막 칸
    public static string Pick(IReadOnlyList<TypeWeightData> weightList, double roll)
    {
        var total = 0d;
        foreach (var entry in weightList)
        {
            total += entry.Weight;
        }

        var threshold = roll * total;
        var cumulative = 0d;
        foreach (var entry in weightList)
        {
            cumulative += entry.Weight;
            if (threshold < cumulative)
            {
                return entry.Monster;
            }
        }

        return weightList[weightList.Count - 1].Monster;
    }
}
