using UnityEngine;
using UnityEngine.Tilemaps;

// 아이소 바닥 Tilemap(셀 (1, 0.5), 타일 앵커 (0, 0) = 마름모 아래 꼭짓점 — 그림 피벗 아래 가운데)에 풀·흙길 타일을 깐다(아트 기준서 3.1·3.2).
public class FieldView : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;

    public Tilemap Tilemap => tilemap;

    public void Build(TileChangeData[] tileChangeDatas)
    {
        tilemap.ClearAllTiles();
        tilemap.SetTiles(tileChangeDatas, true);
    }
}
