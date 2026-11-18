using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// 같은 아틀라스 스프라이트를 전체 또는 가방 한 칸만 그린다. 새 Sprite/Material을 만들지 않는다.
// 아틀라스는 기존 Characters 설정(회전·tight packing 끔)을 따른다.
public class UI_ItemIconView : MaskableGraphic
{
    private Sprite _icon;
    private int _rotation;
    private Rect _uvRect;

    public override Texture mainTexture => _icon != null ? _icon.texture : Texture2D.whiteTexture;

    public void UpdateUI(Sprite icon, int rotation, Rect uvRect, Color tint)
    {
        _icon = icon;
        _rotation = rotation;
        _uvRect = uvRect;
        color = tint;
        enabled = icon != null;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_icon == null)
        {
            return;
        }

        var rect = GetPixelAdjustedRect();
        var size = ItemIconUtils.GetSize(_icon, _rotation);
        var aspect = size.x * _uvRect.width / (size.y * _uvRect.height);
        if (rect.width > rect.height * aspect)
        {
            var width = rect.height * aspect;
            rect.x += (rect.width - width) * 0.5f;
            rect.width = width;
        }
        else
        {
            var height = rect.width / aspect;
            rect.y += (rect.height - height) * 0.5f;
            rect.height = height;
        }

        var texels = new Vector2(size.x * ItemIconUtils.CellPixels * _uvRect.width, size.y * ItemIconUtils.CellPixels * _uvRect.height);
        var scale = UiDotScale.CanvasPerTexel(Mathf.Min(rect.width / texels.x, rect.height / texels.y), canvas.scaleFactor);
        var integerSize = texels * scale;
        rect = new Rect(rect.center - integerSize * 0.5f, integerSize);
        var outerUv = DataUtility.GetOuterUV(_icon);
        AddVertex(vh, rect, outerUv, Vector2.zero);
        AddVertex(vh, rect, outerUv, Vector2.up);
        AddVertex(vh, rect, outerUv, Vector2.one);
        AddVertex(vh, rect, outerUv, Vector2.right);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    private void AddVertex(VertexHelper vh, Rect rect, Vector4 outerUv, Vector2 corner)
    {
        var uv = ItemIconUtils.GetSourceUv(_uvRect.min + Vector2.Scale(corner, _uvRect.size), _rotation);
        var atlasUv = new Vector2(Mathf.Lerp(outerUv.x, outerUv.z, uv.x), Mathf.Lerp(outerUv.y, outerUv.w, uv.y));
        vh.AddVert(rect.min + Vector2.Scale(corner, rect.size), color, atlasUv);
    }
}
