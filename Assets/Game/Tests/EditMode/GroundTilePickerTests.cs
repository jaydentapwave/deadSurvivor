using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

// 바닥 넣기 T1·T2: 흙길 고르는 표(Ground/path/source.txt "이웃 네 칸 → 고를 타일")의 행마다, 풀 섞기, 길 데이터 파일
public class GroundTilePickerTests
{
    private const string PathFolder = "Assets/Game/03.Resources/Art/AIGenerated/Ground/path";
    private const string GrassFolder = "Assets/Game/03.Resources/Art/AIGenerated/Ground/grass";

    // 순서: ↗ ↘ ↙ ↖
    [TestCase(true, false, true, false, "path_ne", false)]
    [TestCase(false, true, false, true, "path_ne", true)]
    [TestCase(true, false, false, true, "path_corner_n", false)]
    [TestCase(true, true, false, false, "path_corner_e", false)]
    [TestCase(false, true, true, false, "path_corner_s", false)]
    [TestCase(false, false, true, true, "path_corner_w", false)]
    [TestCase(true, false, false, false, "path_end_ne", false)]
    [TestCase(false, true, false, false, "path_end_se", false)]
    [TestCase(false, false, true, false, "path_end_sw", false)]
    [TestCase(false, false, false, true, "path_end_nw", false)]
    public void PathTableRowPicksTile(bool hasNorthEast, bool hasSouthEast, bool hasSouthWest, bool hasNorthWest,
        string spriteName, bool isFlipped)
    {
        var choice = GroundTilePicker.PickPath(hasNorthEast, hasSouthEast, hasSouthWest, hasNorthWest);

        Assert.IsTrue(choice.HasTile);
        Assert.AreEqual(spriteName, choice.SpriteName);
        Assert.AreEqual(isFlipped, choice.IsFlipped);
        Assert.IsTrue(File.Exists($"{PathFolder}/{spriteName}.png"), spriteName);
    }

    // 표의 "없음(외딴 칸)"·"셋·넷(갈림)" 행: 타일 없음
    [TestCase(false, false, false, false)]
    [TestCase(true, true, true, false)]
    [TestCase(true, true, false, true)]
    [TestCase(true, false, true, true)]
    [TestCase(false, true, true, true)]
    [TestCase(true, true, true, true)]
    public void PathTableRowWithoutTile(bool hasNorthEast, bool hasSouthEast, bool hasSouthWest, bool hasNorthWest)
    {
        Assert.IsFalse(GroundTilePicker.PickPath(hasNorthEast, hasSouthEast, hasSouthWest, hasNorthWest).HasTile);
    }

    // 칸 이웃 방향: +x ↗, −y ↘, −x ↙, +y ↖
    [Test]
    public void CellNeighboursMapToDirections()
    {
        var pathCellSet = new HashSet<Vector2Int> { new(0, 0), new(1, 0), new(0, -1) };

        Assert.AreEqual("path_corner_e", GroundTilePicker.PickPath(new Vector2Int(0, 0), pathCellSet).SpriteName);
        Assert.AreEqual("path_end_sw", GroundTilePicker.PickPath(new Vector2Int(1, 0), pathCellSet).SpriteName);
        Assert.AreEqual("path_end_nw", GroundTilePicker.PickPath(new Vector2Int(0, -1), pathCellSet).SpriteName);
    }

    [Test]
    public void GrassIsSameForSameCellAndUsesAllFour()
    {
        var countList = new int[GroundTilePicker.GrassCount];
        for (var y = -32; y < 32; y++)
        {
            for (var x = -32; x < 32; x++)
            {
                var index = GroundTilePicker.PickGrass(x, y, GroundTilePicker.GrassCount);
                Assert.AreEqual(index, GroundTilePicker.PickGrass(x, y, GroundTilePicker.GrassCount));
                countList[index]++;
            }
        }

        for (var i = 0; i < countList.Length; i++)
        {
            // 64×64칸에 고르게(각 1/4 ± 1/16)
            Assert.That(countList[i], Is.InRange(768, 1280), $"grass {i}");
            Assert.IsTrue(File.Exists($"{GrassFolder}/{GroundTilePicker.GetGrassSpriteName(i)}.png"), $"grass {i}");
        }
    }

    // 길 데이터: 들판 안, 모든 칸이 표의 타일을 고르고(갈림·외딴 칸 없음), 한 번 이상 꺾이며, 양 끝이 들판 가장자리
    [Test]
    public void PathDataPicksTileForEveryCell()
    {
        var context = new TestRunContext();
        var half = context.RunRules.FieldCells / 2;
        var path = new ResourceService().LoadJson<GroundPathData>("Data/Field/ground_path");
        var pathCellSet = new HashSet<Vector2Int>();
        foreach (var cell in path.Cells)
        {
            Assert.IsTrue(pathCellSet.Add(new Vector2Int(cell.X, cell.Y)), $"duplicate {cell.X},{cell.Y}");
        }

        var cornerCount = 0;
        var endCount = 0;
        foreach (var cell in pathCellSet)
        {
            Assert.That(cell.x, Is.InRange(-half, half - 1), cell.ToString());
            Assert.That(cell.y, Is.InRange(-half, half - 1), cell.ToString());
            var choice = GroundTilePicker.PickPath(cell, pathCellSet);
            Assert.IsTrue(choice.HasTile, cell.ToString());
            if (choice.SpriteName.StartsWith("path_corner_"))
            {
                cornerCount++;
            }

            if (choice.SpriteName.StartsWith("path_end_"))
            {
                endCount++;
                var isOnEdge = Mathf.Abs(cell.x) == half || cell.x == half - 1 || Mathf.Abs(cell.y) == half || cell.y == half - 1;
                Assert.IsTrue(isOnEdge, $"end {cell} not on field edge");
            }
        }

        Assert.That(cornerCount, Is.GreaterThanOrEqualTo(1));
        Assert.AreEqual(2, endCount);
    }
}
