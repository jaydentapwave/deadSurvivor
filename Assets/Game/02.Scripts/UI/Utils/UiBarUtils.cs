using UnityEngine;

public static class UiBarUtils
{
    // 채움 폭·높이(F6)를 유지한다. 가죽 틀은 채움 밖에 4/2px씩 덧댄다.
    public static void Initialize(SpriteRenderer back, SpriteRenderer fill, Sprite backSprite, Sprite fillSprite,
        Vector2 size, Vector2 offset, Color backColor, Color fillColor)
    {
        back.sprite = backSprite;
        back.drawMode = SpriteDrawMode.Sliced;
        back.size = size + new Vector2(8f, 4f) / Const.PixelsPerUnit;
        back.transform.localPosition = offset - new Vector2(4f, 0f) / Const.PixelsPerUnit;
        back.color = backColor;
        InitializeFill(fill, fillSprite, size, fillColor);
        fill.transform.localPosition = offset;
    }

    public static void InitializeFill(SpriteRenderer fill, Sprite sprite, Vector2 size, Color color)
    {
        fill.sprite = sprite;
        fill.drawMode = SpriteDrawMode.Sliced;
        fill.size = size;
        fill.color = color;
    }
}
