using UnityEngine;

// 바닥 평면 좌표(예전 단위, x = 화면 오른쪽, y = 화면 위) ↔ 월드 좌표(기획서 E절, 가정 FA4).
// 거리·충돌은 바닥 평면에서만 계산하고 그릴 때만 바꾼다.
public static class IsoProjection
{
    // 2:1 아이소 바닥의 세로 압축
    private const float VerticalCompress = 0.5f;

    public static Vector2 FloorToWorld(Vector2 floor, float unitScale)
    {
        return new Vector2(floor.x * unitScale, floor.y * unitScale * VerticalCompress);
    }

    public static Vector2 WorldToFloor(Vector2 world, float unitScale)
    {
        return new Vector2(world.x / unitScale, world.y / (unitScale * VerticalCompress));
    }
}
