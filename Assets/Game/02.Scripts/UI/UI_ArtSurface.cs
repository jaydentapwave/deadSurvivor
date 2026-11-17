using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// Image 참조·레이캐스트·레이아웃은 그대로 두고 메시만 그린다. 임포터 border를 바꾸지 않는다.
[RequireComponent(typeof(Image))]
public class UI_ArtSurface : BaseMeshEffect
{
    [SerializeField] private string spriteKey;
    [SerializeField] private bool borderOnly;

    public void Initialize(string key, bool isBorderOnly = false)
    {
        spriteKey = key;
        borderOnly = isBorderOnly;
        graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive())
        {
            return;
        }

        var image = (Image)graphic;
        var sprite = image.sprite;
        var rect = image.GetPixelAdjustedRect();
        if (spriteKey == "hp_bar_bg" || spriteKey == "cooldown_bg"
            || spriteKey == "hp_bar_fill" || spriteKey == "cooldown_fill")
        {
            // HP 틀6/쿨타임 틀4/채움2px. 정수 좌표에서 가로만 늘린다.
            rect = new Rect(rect.x, Mathf.Round(rect.center.y) - sprite.rect.height / 2, rect.width, sprite.rect.height);
        }

        var uv = DataUtility.GetOuterUV(sprite);
        var border = UiArtLayout.Border(spriteKey);
        var size = sprite.rect.size;
        // 그림의 픽셀과 Canvas의 가상 좌표를 1:1로 맞춘다(PPU 48의 영향을 받지 않음).
        var x = new[] { rect.xMin, rect.xMin + Mathf.Min(border.x, rect.width / 2), rect.xMax - Mathf.Min(border.z, rect.width / 2), rect.xMax };
        var y = new[] { rect.yMin, rect.yMin + Mathf.Min(border.y, rect.height / 2), rect.yMax - Mathf.Min(border.w, rect.height / 2), rect.yMax };
        var u = new[] { uv.x, Mathf.Lerp(uv.x, uv.z, border.x / size.x), Mathf.Lerp(uv.x, uv.z, 1 - border.z / size.x), uv.z };
        var v = new[] { uv.y, Mathf.Lerp(uv.y, uv.w, border.y / size.y), Mathf.Lerp(uv.y, uv.w, 1 - border.w / size.y), uv.w };
        if (spriteKey == "panel")
        {
            // 높이22인 상단 표시줄도 8px 장식은 줄이지 않는다.
            x = PanelAxis(rect.xMin, rect.width);
            y = PanelAxis(rect.yMin, rect.height);
            u = new[] { uv.x, Mathf.Lerp(uv.x, uv.z, 8f / 48), Mathf.Lerp(uv.x, uv.z, 12f / 48),
                Mathf.Lerp(uv.x, uv.z, 36f / 48), Mathf.Lerp(uv.x, uv.z, 40f / 48), uv.z };
            v = new[] { uv.y, Mathf.Lerp(uv.y, uv.w, 8f / 48), Mathf.Lerp(uv.y, uv.w, 12f / 48),
                Mathf.Lerp(uv.y, uv.w, 36f / 48), Mathf.Lerp(uv.y, uv.w, 40f / 48), uv.w };
        }

        vh.Clear();
        for (var row = 0; row < y.Length - 1; row++)
        {
            for (var col = 0; col < x.Length - 1; col++)
            {
                if (borderOnly && row == 1 && col == 1)
                {
                    continue;
                }

                AddQuad(vh, new Rect(x[col], y[row], x[col + 1] - x[col], y[row + 1] - y[row]),
                    new Vector4(u[col], v[row], u[col + 1], v[row + 1]), image.color);
            }
        }

        if (spriteKey == "panel")
        {
            DrawStitches(vh, rect, uv, size, image.color);
        }
    }

    private static float[] PanelAxis(float start, float length)
    {
        var extension = Mathf.Min(4f, (length - 16f) / 2);
        return new[] { start, start + 8, start + 8 + extension,
            start + length - 8 - extension, start + length - 8, start + length };
    }

    private static void DrawStitches(VertexHelper vh, Rect rect, Vector4 uv, Vector2 size, Color color)
    {
        var width = Mathf.RoundToInt(rect.width);
        var height = Mathf.RoundToInt(rect.height);
        // 가죽색/땀색을 원본의 한 픽셀 UV로 샘플링하므로 아틀라스에서도 별도 재질이 필요 없다.
        var leather = Sample(uv, size, 10, 2);
        var stitch = Sample(uv, size, 8, 2);
        for (var axis = 0; axis < 2; axis++)
        {
            var length = axis == 0 ? width : height;
            for (var side = 0; side < 2; side++)
            {
                var rail = side == 0 ? 2 : (axis == 0 ? height : width) - 3;
                for (var p = 8; p < length - 8; p++)
                {
                    var pixel = axis == 0 ? new Rect(rect.xMin + p, rect.yMin + rail, 1, 1)
                        : new Rect(rect.xMin + rail, rect.yMin + p, 1, 1);
                    AddQuad(vh, pixel, UiArtLayout.HasStitch(p, length) ? stitch : leather, color);
                }
            }
        }
    }

    private static Vector4 Sample(Vector4 uv, Vector2 size, int x, int y)
    {
        var u = Mathf.Lerp(uv.x, uv.z, (x + 0.5f) / size.x);
        var v = Mathf.Lerp(uv.y, uv.w, (y + 0.5f) / size.y);
        return new Vector4(u, v, u, v);
    }

    private static void AddQuad(VertexHelper vh, Rect rect, Vector4 uv, Color color)
    {
        var start = vh.currentVertCount;
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, new Vector2(uv.x, uv.y));
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, new Vector2(uv.x, uv.w));
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, new Vector2(uv.z, uv.w));
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, new Vector2(uv.z, uv.y));
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
