using UnityEngine;

// 카메라 흔들림 순수 계산(F6 기획서 D절). 난수를 쓰지 않는다
public static class ShakeMath
{
    // 프레임마다 번갈아 쓰는 방향(축마다 ±1)
    private static readonly Vector2Int[] Directions =
    {
        new(1, 1), new(-1, -1), new(1, -1), new(-1, 1),
    };

    // 축마다 round(amplitudePx × 남은 시간 ÷ seconds) 픽셀, 1/PPU 유닛 배수. 남은 시간이 0 이하면 0
    public static Vector2 GetOffset(float remaining, float seconds, int amplitudePx, int step)
    {
        if (remaining <= 0f || seconds <= 0f)
        {
            return Vector2.zero;
        }

        var pixels = Mathf.RoundToInt(amplitudePx * Mathf.Clamp01(remaining / seconds));
        var direction = Directions[((step % Directions.Length) + Directions.Length) % Directions.Length];
        return new Vector2(direction.x * pixels, direction.y * pixels) / Const.PixelsPerUnit;
    }

    // 이번 프레임에 다른 쪽이 카메라를 옮겼으면(지금 위치 ≠ 지난번 얹은 위치) 그 위치가 새 기준, 아니면 지난 기준
    public static Vector3 ResolveBase(Vector3 current, Vector3 lastApplied, Vector3 lastBase) =>
        current != lastApplied ? current : lastBase;

    // 지난 시작에서 minInterval이 지났는가
    public static bool CanStart(float now, float lastStart, float minInterval) => now - lastStart >= minInterval;
}
