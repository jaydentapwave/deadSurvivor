using UnityEngine;

// 아이콘도 BagRules와 같이 화면 아래가 +y, 한 번 회전은 시계 방향이다.
public static class ItemIconUtils
{
    public const int CellPixels = 16;

    public static UI_ItemIconView Create(Transform parent)
    {
        var view = new GameObject("ItemIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UI_ItemIconView))
            .GetComponent<UI_ItemIconView>();
        view.transform.SetParent(parent, false);
        view.gameObject.layer = parent.gameObject.layer;
        view.rectTransform.anchorMin = Vector2.zero;
        view.rectTransform.anchorMax = Vector2.one;
        view.rectTransform.offsetMin = Vector2.zero;
        view.rectTransform.offsetMax = Vector2.zero;
        view.raycastTarget = false;
        return view;
    }

    // 회전된 캔버스의 정규 좌표 → 원본 스프라이트 UV. UV의 y는 위가 +다.
    public static Vector2 GetSourceUv(Vector2 uv, int rotation)
    {
        switch (((rotation % 4) + 4) % 4)
        {
            case 1:
                return new Vector2(1f - uv.y, uv.x);
            case 2:
                return Vector2.one - uv;
            case 3:
                return new Vector2(uv.y, 1f - uv.x);
            default:
                return uv;
        }
    }

    public static Vector2Int GetSize(Sprite icon, int rotation)
    {
        var size = new Vector2Int(Mathf.RoundToInt(icon.rect.width / CellPixels),
            Mathf.RoundToInt(icon.rect.height / CellPixels));
        return rotation % 2 == 0 ? size : new Vector2Int(size.y, size.x);
    }

    public static Rect GetCellUv(Vector2Int cell, Vector2Int size) =>
        new((float)cell.x / size.x, 1f - (float)(cell.y + 1) / size.y, 1f / size.x, 1f / size.y);
}
