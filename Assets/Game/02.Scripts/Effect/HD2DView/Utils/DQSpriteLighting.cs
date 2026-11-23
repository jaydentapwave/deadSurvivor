using UnityEngine;

// Called synchronously with every body sprite assignment, preserving the owner's property block.
public static class DQSpriteLighting
{
    private static readonly int RectId = Shader.PropertyToID("_DQSpriteUVRect");
    public static Vector4 InnerRect(Sprite sprite)
    {
        var rect = sprite.textureRect;
        var texture = sprite.texture;
        return new Vector4((rect.xMin + .5f) / texture.width, (rect.yMin + .5f) / texture.height,
            (rect.xMax - .5f) / texture.width, (rect.yMax - .5f) / texture.height);
    }

    public static void Apply(SpriteRenderer renderer, Sprite sprite, ref MaterialPropertyBlock properties)
    {
        renderer.sprite = sprite;
        if (sprite == null)
        {
            return; // Cleared body during pool initialization has no texture to sample.
        }
        properties ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetVector(RectId, InnerRect(sprite));
        renderer.SetPropertyBlock(properties);
    }
}
