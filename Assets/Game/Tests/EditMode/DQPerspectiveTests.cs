using NUnit.Framework;
using UnityEngine;

[Category("DQScreen")]
public class DQPerspectiveTests
{
    private static PerspectiveFrame Frame(int width, int height)
    {
        var rotation = Quaternion.Euler(18, 0, 0);
        return new PerspectiveFrame(new Vector3(0, .95f, 0) - rotation * Vector3.forward * TiltedCameraRig.DQDistance,
            rotation, 20, (float)width / height, height);
    }

    [TestCase(1920, 1080)]
    [TestCase(2340, 1080)]
    [TestCase(2048, 1536)]
    public void GroundRoundTripAndAsymmetricFootprint(int width, int height)
    {
        var frame = Frame(width, height);
        foreach (var world in new[] { Vector3.zero, new Vector3(-10, 0, -8), new Vector3(8, 0, 15), new Vector3(0, 0, 32) })
        {
            var screen = frame.Project(world);
            Assert.That(frame.TryPlane(new Vector2(screen.x / width, screen.y / height), 0, out var restored), Is.True);
            Assert.That(Vector3.Distance(world, restored), Is.LessThan(.001f));
        }
        var bounds = new FloorViewBounds(frame, 0);
        Assert.That(bounds.Min.y, Is.EqualTo(-10.01136f).Within(.001f));
        Assert.That(bounds.Max.y, Is.EqualTo(46.5579f).Within(.002f));
        Assert.That(frame.Project(new Vector3(0, 0, 32)).y / height, Is.EqualTo(.90684f).Within(.0001f));
    }

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(-1, -1)]
    [TestCase(0, 0)]
    public void JoystickPreservesScreenDirectionAndWorldSpeed(float x, float y)
    {
        var frame = Frame(2340, 1080);
        var input = new Vector2(x, y);
        var origin = new Vector3(3, .25f, 4);
        var normal = new Vector3(-.04f, 1, -.07f).normalized;
        var direction = frame.InputDirection(origin, input, normal);
        if (input == Vector2.zero)
        {
            Assert.That(direction, Is.EqualTo(Vector2.zero));
            return;
        }
        var delta = new Vector3(direction.x, -(normal.x * direction.x + normal.z * direction.y) / normal.y, direction.y) * .001f;
        var screenDirection = frame.Direction(origin, delta).normalized;
        Assert.That(Vector2.Dot(screenDirection, input.normalized), Is.GreaterThan(.999f));
        Assert.That(direction.magnitude, Is.EqualTo(1).Within(1e-6f));
    }

    [Test]
    public void PickingUsesActualTerrainTrianglesAndFirstSurface()
    {
        var grid = TerrainHeightGrid.FromMillimetres(1, -2, -2, 5, 5,
            new[] {0,0,0,0,0, 0,100,200,100,0, 0,200,500,200,0, 0,100,200,100,0, 0,0,0,0,0});
        var frame = Frame(1920, 1080);
        foreach (var x in new[] { -.8f, .2f, .7f })
        {
            var original = new Vector3(x, TerrainHeightMath.Sample(grid, x, .3f), .3f);
            var pixel = frame.Project(original);
            Assert.That(PerspectiveViewMath.TryTerrain(frame, new Vector2(pixel.x / 1920, pixel.y / 1080), grid, out var actual), Is.True);
            Assert.That(Vector3.Distance(original, actual), Is.LessThan(.001f));
        }
    }

    [Test]
    public void BehindAndParallelRaysAreRejected()
    {
        var horizontal = new PerspectiveFrame(new Vector3(0, 1, 0), Quaternion.identity, 20, 1, 1080);
        Assert.That(horizontal.TryPlane(new Vector2(.5f, .5f), 0, out _), Is.False);
        Assert.That(horizontal.TryPlane(new Vector2(.5f, 1), 0, out _), Is.False);
    }

    [Test]
    public void AppearanceAndDirectionDoNotAlterGameplayVisibility()
    {
        Assert.That(SpawnAppearanceMath.Fraction(0), Is.Zero);
        Assert.That(SpawnAppearanceMath.Fraction(.15f), Is.EqualTo(.5f));
        Assert.That(SpawnAppearanceMath.Fraction(.3f), Is.EqualTo(1));
        Assert.That(SpawnAppearanceMath.Fraction(5), Is.EqualTo(1));
        Assert.That(SpawnAppearanceMath.Direction(Vector2.up), Is.Zero);
        Assert.That(SpawnAppearanceMath.Direction(Vector2.right), Is.EqualTo(1));
        Assert.That(SpawnAppearanceMath.Direction(Vector2.left), Is.EqualTo(2));
        Assert.That(SpawnAppearanceMath.Direction(Vector2.down), Is.EqualTo(3));
    }

    [Test]
    public void CameraFocusAndShakeAreOrderIndependentAndRestored()
    {
        var go = new GameObject("DQ rig test", typeof(Camera), typeof(TiltedCameraRig));
        try
        {
            var rig = go.GetComponent<TiltedCameraRig>();
            rig.Initialize(18, TiltedCameraRig.DQDistance, .1f, 180, .1f, 1);
            var focus = new Vector3(1, .25f, 3);
            var shake = new Vector2(.03f, -.02f);
            rig.SetFocus(focus);
            rig.SetShakeOffset(shake);
            var first = go.transform.position;
            rig.SetShakeOffset(Vector2.zero);
            rig.SetShakeOffset(shake);
            rig.SetFocus(focus);
            Assert.That(Vector3.Distance(first, go.transform.position), Is.LessThan(1e-6f));
            var restored = first + go.transform.forward * rig.Distance - go.transform.right * shake.x - go.transform.up * shake.y - Vector3.up * TiltedCameraRig.FocusHeight;
            Assert.That(Vector3.Distance(restored, focus), Is.LessThan(.00001f));
            Assert.That(go.GetComponent<Camera>().orthographic, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
    [Test]
    public void MovementFixtureRestoresCurrentLimitAfterBoundsChangeAndException()
    {
        var context = new TestRunContext();
        var service = context.PartyMoveService;
        service.SetViewHalfExtent(new Vector2(20, 30));
        var original = service.CenterLimit;
        try
        {
            using (service.OverrideMovementLimit())
            {
                Assert.That(service.CenterLimit, Is.GreaterThan(original));
                service.SetViewHalfExtent(new Vector2(40, 50));
                throw new System.InvalidOperationException("fixture abort");
            }
        }
        catch (System.InvalidOperationException)
        {
            Assert.That(service.CenterLimit, Is.EqualTo(original - 40).Within(.0001f));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void VisualSnapAlignsOddWidthAndMirroredCornerWithoutChangingDepth(bool flipped)
    {
        var frame = Frame(1920, 1080);
        var pivot = new Vector2(flipped ? 31 - 12 : 12, 3);
        var original = new Vector3(1.013f, .7f, 4.027f);
        var snapped = frame.SnapCard(original, pivot, 48);
        var corner = frame.Project(snapped - frame.Right * pivot.x / 48 - frame.Up * pivot.y / 48);
        var pitch = frame.FocalPixels / (corner.z * 48);
        Assert.That(Mathf.Abs(corner.x / pitch - Mathf.Round(corner.x / pitch)), Is.LessThan(.0002f));
        Assert.That(Mathf.Abs(corner.y / pitch - Mathf.Round(corner.y / pitch)), Is.LessThan(.0002f));
        Assert.That(frame.Project(snapped).z, Is.EqualTo(frame.Project(original).z).Within(.00001f));
    }

    [Test]
    public void RenderApronHasNoTrianglesInsideOriginalTerrain()
    {
        var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/03.Resources/Models/HD2D/DQ_VisualApron.asset");
        var layout = new ResourceService().LoadJson<FieldLayoutData>("Data/Field/field_layout");
        var vertices = mesh.vertices;
        var indices = mesh.triangles;
        Assert.That(indices.Length, Is.GreaterThan(0));
        for (var i = 0; i < indices.Length; i += 3)
        {
            var p = (vertices[indices[i]] + vertices[indices[i + 1]] + vertices[indices[i + 2]]) / 3f;
            Assert.That(Mathf.Abs(p.x) + Mathf.Abs(p.z), Is.GreaterThan(layout.groundHalfDiagonal));
        }
        Assert.That(mesh.uv2.Length, Is.EqualTo(vertices.Length));
    }

    [TestCase(1920, 1080)]
    [TestCase(2340, 1080)]
    [TestCase(2048, 1536)]
    public void LogicalGameEnvelopeIsPreservedAndDoesNotUseFarVisualFrustum(int width, int height)
    {
        var rules = new ResourceService().LoadJson<ViewRulesData>("Data/View/view_rules");
        var game = rules.GameplayHalfExtent(width, height);
        var scale = Mathf.Max(1, Mathf.RoundToInt(height * .1f / 48));
        var halfHeight = height / (96f * scale);
        Assert.That(game.x, Is.EqualTo(halfHeight * width / height).Within(.00001f));
        Assert.That(game.y, Is.EqualTo(halfHeight * 2 + .907692f).Within(.00001f));
        Assert.That(game.y, Is.LessThan(new FloorViewBounds(Frame(width, height), 0).Max.y));
    }

    [Test]
    public void BotFloorHeadingAndJoystickUseTheSamePerspectiveInverse()
    {
        var rules = new ResourceService().LoadJson<ViewRulesData>("Data/View/view_rules");
        rules.SetProjection(Frame(1920, 1080), .0113f, TerrainHeightGrid.Flat);
        var center = new Vector2(100, -200);
        foreach (var direction in new[] { Vector2.right, Vector2.up, Vector2.one.normalized, new Vector2(-1, .3f).normalized })
        {
            var screen = rules.Direction(center, direction).normalized;
            var world = new Vector3(center.x, 0, center.y) * .0113f;
            var restored = rules.Frame.InputDirection(world, screen);
            Assert.That(Vector2.Dot(restored, direction), Is.GreaterThan(.99999f));
        }
    }

    [Test]
    public void IndicatorLayerEntersCameraCullingAndIsExcludedFromNormalRendererPasses()
    {
        var scene = System.IO.File.ReadAllText("Assets/Game/01.Scene/Main.unity");
        var blocks = System.Text.RegularExpressions.Regex.Split(scene, "(?=^--- !u!)", System.Text.RegularExpressions.RegexOptions.Multiline);
        var cameraBlock = System.Array.Find(blocks, b => b.StartsWith("--- !u!20 "));
        var bits = System.Text.RegularExpressions.Regex.Match(cameraBlock, @"m_CullingMask:\s+serializedVersion: \d+\s+m_Bits: (\d+)").Groups[1].Value;
        Assert.That(uint.Parse(bits) & (1u << 26), Is.Not.Zero, "Postprocess RenderObjects still requires the camera's culling results.");
        var renderer = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Game/03.Resources/Settings/DQ_MainRenderer.asset");
        Assert.That(renderer.opaqueLayerMask.value & (1 << 26), Is.Zero);
        Assert.That(renderer.transparentLayerMask.value & (1 << 26), Is.Zero);
    }

}
