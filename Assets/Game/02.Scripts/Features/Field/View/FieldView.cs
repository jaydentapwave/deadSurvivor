using UnityEngine;
using UnityEngine.Tilemaps;

// 아이소 바닥 Tilemap(셀 (1, 0.5), 타일 앵커 (0, 0) = 마름모 아래 꼭짓점 — 그림 피벗 아래 가운데)에 풀·흙길 타일을 깐다(아트 기준서 3.1·3.2).
public class FieldView : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;

    public Tilemap Tilemap => tilemap;
    private float _tiltDegrees;
    public void Initialize(float tiltDegrees) => _tiltDegrees = tiltDegrees;

    public void Build(TileChangeData[] tileChangeDatas)
    {
        tilemap.ClearAllTiles();
        tilemap.SetTiles(tileChangeDatas, true);
    }

    // 초기화 때 한 번만 만든다. 시뮬레이션·충돌 컴포넌트 없이 장면과 함께 사라지는 그림뿐이다.
    // 소품은 기존 대원·몬스터와 같은 Default/0 + Pivot, URP 2D의 (0,1,0) 정렬을 그대로 쓴다.
    public SpriteRenderer AddDecor(Sprite sprite, FieldDecorPlacement placement)
    {
        var decor = new GameObject(placement.Sprite);
        decor.transform.SetParent(transform.parent, false);
        var offsetX = placement.PivotOffsetXPixels / (float)Const.PixelsPerUnit;
        var floor = IsoProjection.IsoCellToFloor(placement.Cell);
        var position = new Vector3(floor.x, .001f, floor.y);
        position.x += placement.IsFlipped ? -offsetX : offsetX;
        decor.transform.position = position;
        var renderer = decor.AddComponent<SpriteRenderer>();
        decor.transform.rotation = Quaternion.Euler(placement.IsGround ? 90f : _tiltDegrees, 0f, 0f);
        renderer.sprite = sprite;
        renderer.flipX = placement.IsFlipped;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.sortingOrder = placement.IsGround ? -90 : 0;
        renderer.sharedMaterial = tilemap.GetComponent<TilemapRenderer>().sharedMaterial;
        return renderer;
    }
}
