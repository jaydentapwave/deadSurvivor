using UnityEngine;

// 표현 계산만. 전투 거리·명중 계산에는 관여하지 않는다.
public static class CombatEffectMotion
{
    public static Vector2 Snap(Vector2 position) => new(
        Mathf.Round(position.x * Const.PixelsPerUnit) / Const.PixelsPerUnit,
        Mathf.Round(position.y * Const.PixelsPerUnit) / Const.PixelsPerUnit);

    public static float GetProjectileAngle(Vector2 direction) =>
        Mathf.Round(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg / 90f) * 90f;

    public static int GetSegmentCount(float length, float width) => Mathf.CeilToInt(length / width);

    // 선분을 원본 한 마디 폭으로 나눈다. 끝 마디만 짧아지며 겹치거나 선분 밖으로 나가지 않는다.
    public static void GetSegment(Vector2 from, Vector2 to, float width, int index,
        out Vector2 center, out float scaleX)
    {
        var length = Vector2.Distance(from, to);
        var start = index * width;
        var partLength = Mathf.Min(width, length - start);
        center = from + (to - from).normalized * (start + partLength * 0.5f);
        scaleX = partLength / width;
    }
}
