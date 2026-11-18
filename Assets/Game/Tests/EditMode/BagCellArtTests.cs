using NUnit.Framework;

public class BagCellArtTests
{
    [TestCase(-1, false, BagCellMark.None, BagCellArtState.Empty)]
    [TestCase(2, false, BagCellMark.None, BagCellArtState.Filled)]
    [TestCase(2, true, BagCellMark.None, BagCellArtState.Selected)]
    [TestCase(2, true, BagCellMark.Invalid, BagCellArtState.Blocked)]
    [TestCase(2, true, BagCellMark.Valid, BagCellArtState.Droppable)]
    [TestCase(-1, false, BagCellMark.Valid, BagCellArtState.Droppable)]
    public void PreviewTakesPriorityOverSelectionAndItem(int uid, bool picked, BagCellMark mark, BagCellArtState expected)
    {
        Assert.AreEqual(expected, BagCellArt.Resolve(new BagCellDisplay { Uid = uid, IsPicked = picked, Mark = mark }));
    }
}
