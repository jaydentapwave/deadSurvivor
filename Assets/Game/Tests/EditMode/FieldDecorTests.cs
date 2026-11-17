using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

// 장식 T1~T5. 에디터 없는 갈래에서 작성만 하며, 합친 뒤 루트 에디터에서 실행한다(D29·D30).
public class FieldDecorTests
{
    private const string DecorFolder = "Assets/Game/03.Resources/Art/AIGenerated/Decor";

    [Test]
    public void GroundAtlasIncludesEveryDecorSpriteAtOriginalScale()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>("Assets/Game/03.Resources/Resources/Art/Ground.spriteatlasv2");
        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        foreach (var path in Directory.GetFiles(DecorFolder, "*.png"))
        {
            var sprite = atlas.GetSprite(Path.GetFileNameWithoutExtension(path));
            Assert.IsNotNull(sprite, path);
            try
            {
                Assert.AreEqual(Const.PixelsPerUnit, sprite.pixelsPerUnit);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
            }
        }
    }

    [Test]
    public void PlacementsStayInEdgeBandAndClearOfPathAndPartyStart()
    {
        var resources = new ResourceService();
        var data = resources.LoadJson<FieldDecorData>("Data/Field/decor");
        var rules = resources.LoadJson<RunRulesData>("Data/Run/run_rules");
        var path = resources.LoadJson<GroundPathData>("Data/Field/ground_path");
        var half = rules.FieldCells / 2;
        Assert.That(data.EdgeBandCells, Is.InRange(1, half - 1));
        var cellSet = new HashSet<Vector3Int>();
        var spriteNameSet = new HashSet<string>();
        foreach (var placement in data.Placements)
        {
            var cell = placement.Cell;
            Assert.IsTrue(cellSet.Add(cell), $"duplicate {cell}");
            Assert.That(cell.x, Is.InRange(-half, half - 1));
            Assert.That(cell.y, Is.InRange(-half, half - 1));
            var edgeDistance = Mathf.Min(cell.x + half, cell.y + half, half - 1 - cell.x, half - 1 - cell.y);
            Assert.That(edgeDistance, Is.LessThan(data.EdgeBandCells), cell.ToString());
            foreach (var pathCell in path.Cells)
            {
                var distance = Mathf.Max(Mathf.Abs(cell.x - pathCell.X), Mathf.Abs(cell.y - pathCell.Y));
                Assert.That(distance, Is.GreaterThanOrEqualTo(3), $"path near {cell}");
            }

            Assert.IsTrue(File.Exists($"{DecorFolder}/{placement.Sprite}.png"));
            spriteNameSet.Add(placement.Sprite);
        }

        CollectionAssert.AreEquivalent(new[] { "flower_01", "flower_02", "flower_03", "grass_tuft_01", "grass_tuft_02",
            "tree_01", "rock_01", "rock_02", "fence_01" }, spriteNameSet);
        // 두 번 읽어도 순서·좌표·종류가 같다(난수 없음).
        var again = resources.LoadJson<FieldDecorData>("Data/Field/decor");
        CollectionAssert.AreEqual(data.Placements, again.Placements);
    }

    [Test]
    public void PlacementsClearInitialSpawnCircleWithSpriteFootprint()
    {
        var resources = new ResourceService();
        var rules = resources.LoadJson<RunRulesData>("Data/Run/run_rules");
        var spawn = resources.LoadJson<SpawnRulesData>("Data/Spawn/spawn_rules");
        // 실제 생성은 원정대 중심 + 화면 바닥 반대각선 + SpawnMargin. 고정 출현 지점은 없다.
        // 기준 화면 640×360의 시작 원 바깥에 그림 폭만큼도 여유를 둔다.
        var halfWorld = new Vector2(640f, 360f) / (2f * Const.PixelsPerUnit);
        var spawnRadius = (TiltedViewMath.Footprint(halfWorld.y, 16f/9f, .5f) / rules.UnitScale).magnitude + spawn.SpawnMargin;
        var gridObject = CreateField(out var field);
        try
        {
            foreach (var placement in resources.LoadJson<FieldDecorData>("Data/Field/decor").Placements)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{DecorFolder}/{placement.Sprite}.png");
                var floor = IsoProjection.IsoCellToFloor(placement.Cell) / rules.UnitScale;
                var clearance = sprite.rect.width / sprite.pixelsPerUnit / rules.UnitScale;
                Assert.That(floor.magnitude - clearance, Is.GreaterThan(spawnRadius), placement.Sprite);
            }
        }
        finally
        {
            Object.DestroyImmediate(gridObject);
        }
    }

    [TestCase(false, false, 0)]
    [TestCase(false, true, 0)]
    [TestCase(true, false, -90)]
    [TestCase(true, true, -90)]
    public void DecorUsesFootSortingSharedMaterialAndNoCollision(bool isGround, bool isFlipped, int order)
    {
        var gridObject = CreateField(out var field);
        try
        {
            var placement = JsonUtility.FromJson<FieldDecorPlacement>("{\"sprite\":\"fence_01\",\"x\":17,\"y\":14," +
                "\"pivotOffsetXPixels\":8,\"isGround\":" + isGround.ToString().ToLowerInvariant() +
                ",\"isFlipped\":" + isFlipped.ToString().ToLowerInvariant() + "}");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{DecorFolder}/fence_01.png");
            var renderer = field.AddDecor(sprite, placement);
            var cell = IsoProjection.IsoCellToFloor(placement.Cell);
            var foot = new Vector3(cell.x, .001f, cell.y);
            Assert.AreEqual(order, renderer.sortingOrder);
            Assert.AreEqual(0, renderer.sortingLayerID);
            Assert.AreEqual(SpriteSortPoint.Pivot, renderer.spriteSortPoint);
            Assert.AreSame(field.Tilemap.GetComponent<TilemapRenderer>().sharedMaterial, renderer.sharedMaterial);
            Assert.AreEqual(Vector3.one, renderer.transform.localScale);
            Assert.AreEqual(foot.y, renderer.transform.position.y);
            Assert.That(renderer.transform.position.x - foot.x,
                Is.EqualTo((isFlipped ? -8f : 8f) / Const.PixelsPerUnit).Within(0.00001f));
            Assert.AreEqual(isFlipped, renderer.flipX);
            Assert.IsEmpty(renderer.GetComponentsInChildren<Collider2D>());
            Assert.IsEmpty(renderer.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(renderer.GetComponentsInChildren<Rigidbody2D>());
        }
        finally
        {
            Object.DestroyImmediate(gridObject);
        }
    }

    [Test]
    public void FenceRunsJoinAtTwentyFourByTwelvePixelsInBothDirections()
    {
        var gridObject = CreateField(out var field);
        try
        {
            var data = new ResourceService().LoadJson<FieldDecorData>("Data/Field/decor");
            var fenceSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{DecorFolder}/fence_01.png");
            var lastPositionDict = new Dictionary<bool, Vector3>();
            var countDict = new Dictionary<bool, int> { [false] = 0, [true] = 0 };
            foreach (var placement in data.Placements)
            {
                if (placement.Sprite != "fence_01")
                {
                    continue;
                }

                var position = field.AddDecor(fenceSprite, placement).transform.position;
                if (lastPositionDict.TryGetValue(placement.IsFlipped, out var last))
                {
                    var delta = position - last;
                    var pixels = TiltedViewMath.FloorToScreenPlane(new Vector2(delta.x, delta.z), .5f) * Const.PixelsPerUnit;
                    Assert.That(pixels.x, Is.EqualTo(placement.IsFlipped ? -24f : 24f).Within(0.0001f));
                    Assert.That(pixels.y, Is.EqualTo(12f).Within(0.0001f));
                }

                lastPositionDict[placement.IsFlipped] = position;
                countDict[placement.IsFlipped]++;
            }

            Assert.That(countDict[false], Is.GreaterThanOrEqualTo(2));
            Assert.That(countDict[true], Is.GreaterThanOrEqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(gridObject);
        }
    }

    private static GameObject CreateField(out FieldView field)
    {
        var root = new GameObject("DecorTestGrid");
        var grid = root.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = new Vector3(1f, 0.5f, 1f);
        var child = new GameObject("Field");
        child.transform.SetParent(root.transform, false);
        var tilemap = child.AddComponent<Tilemap>();
        tilemap.tileAnchor = Vector3.zero;
        child.AddComponent<TilemapRenderer>();
        field = child.AddComponent<FieldView>();
        var serialized = new SerializedObject(field);
        serialized.FindProperty("tilemap").objectReferenceValue = tilemap;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }
}
