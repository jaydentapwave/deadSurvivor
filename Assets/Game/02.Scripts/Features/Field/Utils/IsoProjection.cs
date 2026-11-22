using UnityEngine;

// Floor units belong to gameplay; the view uses metres on XZ.
public static class IsoProjection
{
    public static Vector3 FloorToWorld(Vector2 floor, float unitScale, TerrainHeightGrid grid) =>
        new(floor.x * unitScale, TerrainHeightMath.Sample(grid, floor.x * unitScale, floor.y * unitScale), floor.y * unitScale);
    public static Vector3 FloorToWorldSnapped(Vector2 floor, float unitScale, float sinTilt, TerrainHeightGrid grid) =>
        FloorToWorld(floor, unitScale, grid);
    public static Vector2 WorldToFloor(Vector3 world, float unitScale) => new(world.x / unitScale, world.z / unitScale);
    public static Vector2 IsoCellToFloor(Vector3Int cell) => new((cell.x - cell.y) * .5f, (cell.x + cell.y) * .5f);
}
