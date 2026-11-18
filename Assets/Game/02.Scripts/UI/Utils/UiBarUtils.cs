using UnityEngine;

public static class UiBarUtils
{
    // Preserve F6 fill dimensions; optional border is outside the fill. HD2D bars have no border.
    public static void Initialize(SpriteRenderer back, SpriteRenderer fill, Sprite backSprite, Sprite fillSprite,
        Vector2 size, Vector2 offset, Color backColor, Color fillColor)
    {
        back.sprite = backSprite;
        back.drawMode = SpriteDrawMode.Sliced;
        back.size = size + new Vector2(backSprite.border.x + backSprite.border.z, backSprite.border.y + backSprite.border.w) / backSprite.pixelsPerUnit;
        back.transform.localPosition = offset - new Vector2(backSprite.border.x, 0) / backSprite.pixelsPerUnit;
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
