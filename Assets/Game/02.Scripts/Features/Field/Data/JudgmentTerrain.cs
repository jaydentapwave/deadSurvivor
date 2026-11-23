// The gameplay height grid (field_height.json) as loaded, kept apart from the drawn grid.
// Movement input projection, spawn projection and the camera focus use this; everything drawn
// uses the TerrainHeightGrid registration (judgment + MeadowHillMath hills).
public sealed class JudgmentTerrain
{
    public TerrainHeightGrid Grid { get; }

    public JudgmentTerrain(TerrainHeightGrid grid)
    {
        Grid = grid;
    }
}
