using UnityEngine;

// 원정대 이동의 순수 계산(F2 기획서 A·C절). 바닥 평면: x = 화면 오른쪽, y = 화면 위.
public static class PartyMoveMath
{
    // 아이소 바닥은 세로가 절반으로 그려진다(IsoProjection). 화면에서 민 각도 그대로 보이게 바닥 방향은 (sx, 2·sy)
    private const float ScreenToFloorVertical = 2f;

    // 화면 기준 입력 → 바닥 방향(길이 1). 입력이 0이면 0
    public static Vector2 ScreenToFloorDirection(Vector2 screenInput)
    {
        return new Vector2(screenInput.x, screenInput.y * ScreenToFloorVertical).normalized;
    }

    // 따라가기 한 걸음: 목표 쪽으로 남은 거리의 min(1, ratio)만큼 다가간다(넘치지 않는다)
    public static Vector2 FollowStep(Vector2 position, Vector2 target, float ratio)
    {
        return position + (target - position) * Mathf.Min(1f, ratio);
    }

    // 가운데 기준 좌표를 마름모 |x| + |y| ≤ limit 안의 가장 가까운 점으로 옮긴다(벽을 따라 미끄러짐). limit ≤ 0이면 가운데
    public static Vector2 ClampToDiamond(Vector2 offset, float limit)
    {
        if (limit <= 0f)
        {
            return Vector2.zero;
        }

        var absX = Mathf.Abs(offset.x);
        var absY = Mathf.Abs(offset.y);
        if (absX + absY <= limit)
        {
            return offset;
        }

        var t = Mathf.Clamp((absX - absY + limit) * 0.5f, 0f, limit);
        return new Vector2(Mathf.Sign(offset.x) * t, Mathf.Sign(offset.y) * (limit - t));
    }
}
