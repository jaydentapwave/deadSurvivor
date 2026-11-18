using System.Collections.Generic;
using UnityEngine;

// 바닥 칸 → 그림 이름 고르기(아트 기준서 3.3, 그림 Ground/path/source.txt "이웃 네 칸 → 고를 타일" 표).
// 아이소 Tilemap 칸 이웃: +x = ↗(오른쪽 위 변), −y = ↘, −x = ↙, +y = ↖.
public static class GroundTilePicker
{
    public const int GrassCount = 4;

    private const string GrassPrefix = "grass_0";
    private const string PathStraight = "path_ne";

    // 풀 변형 번호(0..count−1)는 칸 좌표로만 정해진다(판마다 같음)
    public static int PickGrass(int x, int y, int count)
    {
        unchecked
        {
            var hash = x * 73856093 ^ y * 19349663;
            hash ^= hash >> 13;
            hash *= 0x5bd1e995;
            hash ^= hash >> 15;
            return (hash & int.MaxValue) % count;
        }
    }

    public static string GetGrassSpriteName(int index) => GrassPrefix + (index + 1);

    // 길 칸의 이웃 넷 중 길인 것 → 타일. 이웃이 0개·3개 이상이면 표에 없음(HasTile false)
    public static GroundTileChoice PickPath(bool hasNorthEast, bool hasSouthEast, bool hasSouthWest, bool hasNorthWest)
    {
        var mask = (hasNorthEast ? 1 : 0) | (hasSouthEast ? 2 : 0) | (hasSouthWest ? 4 : 0) | (hasNorthWest ? 8 : 0);
        switch (mask)
        {
            case 1 | 4:
                return new GroundTileChoice(PathStraight, false);
            case 8 | 2:
                return new GroundTileChoice(PathStraight, true);
            case 1 | 8:
                return new GroundTileChoice("path_corner_n", false);
            case 1 | 2:
                return new GroundTileChoice("path_corner_e", false);
            case 2 | 4:
                return new GroundTileChoice("path_corner_s", false);
            case 4 | 8:
                return new GroundTileChoice("path_corner_w", false);
            case 1:
                return new GroundTileChoice("path_end_ne", false);
            case 2:
                return new GroundTileChoice("path_end_se", false);
            case 4:
                return new GroundTileChoice("path_end_sw", false);
            case 8:
                return new GroundTileChoice("path_end_nw", false);
            default:
                return default;
        }
    }

    public static GroundTileChoice PickPath(Vector2Int cell, HashSet<Vector2Int> pathCellSet)
    {
        return PickPath(
            pathCellSet.Contains(cell + Vector2Int.right),
            pathCellSet.Contains(cell + Vector2Int.down),
            pathCellSet.Contains(cell + Vector2Int.left),
            pathCellSet.Contains(cell + Vector2Int.up));
    }
}

public readonly struct GroundTileChoice
{
    public string SpriteName { get; }
    public bool IsFlipped { get; }
    public bool HasTile => SpriteName != null;

    public GroundTileChoice(string spriteName, bool isFlipped)
    {
        SpriteName = spriteName;
        IsFlipped = isFlipped;
    }
}
