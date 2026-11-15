using UnityEngine;
using UnityEngine.Tilemaps;

// 아이소 바닥 Tilemap에 임시 두 색 마름모 타일을 바둑판으로 깐다(기획서 F절). 그림 단계가 같은 Tilemap에 풀·흙길 타일을 넣는다.
public class FieldView : MonoBehaviour
{
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Color firstTileColor = new(0.761f, 0.678f, 0.373f, 1f);
    [SerializeField] private Color secondTileColor = new(0.631f, 0.596f, 0.365f, 1f);

    public Color FirstTileColor => firstTileColor;
    public Color SecondTileColor => secondTileColor;
    public Vector2 CellSize => tilemap.layoutGrid.cellSize;

    // 원점을 가운데로 cells × cells칸
    public void Build(int cells, TileBase firstTile, TileBase secondTile)
    {
        tilemap.ClearAllTiles();
        var half = cells / 2;
        var positions = new Vector3Int[cells * cells];
        var tiles = new TileBase[cells * cells];
        var index = 0;
        for (var y = -half; y < cells - half; y++)
        {
            for (var x = -half; x < cells - half; x++)
            {
                positions[index] = new Vector3Int(x, y, 0);
                tiles[index] = ((x + y) & 1) == 0 ? firstTile : secondTile;
                index++;
            }
        }

        tilemap.SetTiles(positions, tiles);
    }
}
