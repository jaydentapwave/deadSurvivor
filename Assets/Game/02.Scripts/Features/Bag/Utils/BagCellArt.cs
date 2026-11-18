public enum BagCellArtState { Empty, Filled, Selected, Droppable, Blocked }

public static class BagCellArt
{
    public static BagCellArtState Resolve(BagCellDisplay display)
    {
        return display.Mark == BagCellMark.Valid ? BagCellArtState.Droppable
            : display.Mark == BagCellMark.Invalid ? BagCellArtState.Blocked
            : display.IsPicked ? BagCellArtState.Selected
            : display.Uid >= 0 ? BagCellArtState.Filled : BagCellArtState.Empty;
    }
}
