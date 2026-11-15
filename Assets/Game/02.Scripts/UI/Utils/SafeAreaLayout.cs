using UnityEngine;

// 화면 크기와 Screen.safeArea(픽셀)로 안전 영역 판넬의 앵커(0~1)를 낸다(기획서 I절). 엔진 없이 시험한다.
public static class SafeAreaLayout
{
    public static (Vector2 AnchorMin, Vector2 AnchorMax) Calculate(Vector2 screenSize, Rect safeArea)
    {
        var anchorMin = new Vector2(safeArea.xMin / screenSize.x, safeArea.yMin / screenSize.y);
        var anchorMax = new Vector2(safeArea.xMax / screenSize.x, safeArea.yMax / screenSize.y);
        return (Clamp01(anchorMin), Clamp01(anchorMax));
    }

    private static Vector2 Clamp01(Vector2 value) => new(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
}
