using UnityEngine;

// 선분과 점 거리(순수 함수, 예전 DSGame.SegmentDistance). 돌진·적 투사체 명중에 쓴다.
public static class SegmentMath
{
    // 길이 0 선분은 a와 p의 거리
    public static float Distance(Vector2 a, Vector2 b, Vector2 p)
    {
        var segment = b - a;
        var t = Mathf.Clamp01(Vector2.Dot(p - a, segment) / Mathf.Max(segment.sqrMagnitude, 1e-8f));
        return Vector2.Distance(p, a + segment * t);
    }
}
