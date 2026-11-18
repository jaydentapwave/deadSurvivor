using UnityEngine;

// frames-source.txt의 목적 좌표 위상. 8px 장식 뒤 2px 땀/2px 가죽, 끝 땀은 자르지 않는다.
public static class UiArtLayout
{
    public static bool HasStitch(int coordinate, int length)
    {
        var offset = coordinate - 8;
        var start = coordinate - offset % 4;
        return coordinate >= 8 && coordinate < length - 8 && offset % 4 < 2 && start + 1 < length - 8;
    }

    public static Vector4 Border(string key)
    {
        switch (key)
        {
            case "panel": return new Vector4(12, 12, 12, 12);
            case "button": return new Vector4(4, 4, 4, 4);
            case "bag_cell": return new Vector4(3, 3, 3, 3);
            case "hp_bar_bg": return new Vector4(4, 2, 4, 2);
            case "cooldown_bg": return new Vector4(2, 1, 2, 1);
            default: return Vector4.zero;
        }
    }

    public static string Digits(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
