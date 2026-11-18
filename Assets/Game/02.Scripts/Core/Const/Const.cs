using UnityEngine;

public static class Const
{
    // 그림 1유닛 = 아이소 타일 폭 48px (아트 기준서 3.1, K1·K6)
    public const int PixelsPerUnit = 48;

    // 에디터·개발 빌드에서만 로그를 남긴다
    public static bool IsLogEnabled => Debug.isDebugBuild;
}
