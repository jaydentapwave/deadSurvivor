using NUnit.Framework;
using UnityEngine;

// F2 AC14 — 화면 → 바닥 방향, 이동 속도, 들판 마름모 경계
public class PartyMoveMathTests
{
    private const float Tolerance = 1e-4f;

    [Test]
    public void ScreenInputBecomesFloorDirection()
    {
        AssertVector(new Vector2(1f, 0f), PartyMoveMath.ScreenToFloorDirection(new Vector2(1f, 0f)));
        AssertVector(new Vector2(0f, 1f), PartyMoveMath.ScreenToFloorDirection(new Vector2(0f, 1f)));
        var diagonal = new Vector2(1f, 1f) / Mathf.Sqrt(2f);
        AssertVector(new Vector2(1f, 2f) / Mathf.Sqrt(5f), PartyMoveMath.ScreenToFloorDirection(diagonal));
        AssertVector(Vector2.zero, PartyMoveMath.ScreenToFloorDirection(Vector2.zero));
    }

    // 바닥 방향을 화면에 그리면(IsoProjection) 민 방향과 같은 각이다
    [Test]
    public void FloorDirectionLooksLikePushedDirectionOnScreen()
    {
        var unitScale = new TestRunContext().RunRules.UnitScale;
        foreach (var screenInput in new[] { new Vector2(1f, 1f), new Vector2(-0.3f, 0.8f), new Vector2(0.6f, -0.2f) })
        {
            var floorDirection = PartyMoveMath.ScreenToFloorDirection(screenInput);
            var drawn = TiltedViewMath.FloorToScreenPlane(floorDirection * unitScale, .5f);
            AssertVector(screenInput.normalized, drawn.normalized);
        }
    }

    // 1초 이동 = 이동 속도 × 입력 세기(바닥 단위), 방향과 상관없이
    [Test]
    public void SpeedIsMoveSpeedTimesInputStrength()
    {
        foreach (var input in new[] { new Vector2(1f, 0f), new Vector2(0f, 0.5f), new Vector2(-0.3f, -0.4f) })
        {
            var context = StartRun();
            var start = context.PartyModel.Center;
            context.PartyMoveService.SetMoveInput(input);
            StepSeconds(context, 1f);

            var moved = (context.PartyModel.Center - start).magnitude;
            Assert.AreEqual(context.MoveRules.MoveSpeed * input.magnitude, moved, 0.01f, $"input {input}");
        }
    }

    [Test]
    public void ClampToDiamondKeepsInsidePoints()
    {
        AssertVector(new Vector2(300f, -400f), PartyMoveMath.ClampToDiamond(new Vector2(300f, -400f), 1000f));
        AssertVector(new Vector2(-500f, 500f), PartyMoveMath.ClampToDiamond(new Vector2(-500f, 500f), 1000f));
    }

    [Test]
    public void ClampToDiamondMovesOutsidePointsToNearestEdgePoint()
    {
        AssertVector(new Vector2(1000f, 0f), PartyMoveMath.ClampToDiamond(new Vector2(2000f, 0f), 1000f));
        AssertVector(new Vector2(0f, -1000f), PartyMoveMath.ClampToDiamond(new Vector2(30f, -2000f), 1000f));
        // 변 바깥(600, 500) → 변 x + y = 1000 위의 수선 발 (550, 450)
        AssertVector(new Vector2(550f, 450f), PartyMoveMath.ClampToDiamond(new Vector2(600f, 500f), 1000f));
        AssertVector(new Vector2(-550f, -450f), PartyMoveMath.ClampToDiamond(new Vector2(-600f, -500f), 1000f));
    }

    [Test]
    public void ClampToDiamondWithNonPositiveLimitIsCenter()
    {
        AssertVector(Vector2.zero, PartyMoveMath.ClampToDiamond(new Vector2(10f, 5f), 0f));
        AssertVector(Vector2.zero, PartyMoveMath.ClampToDiamond(new Vector2(10f, 5f), -50f));
    }

    // 중심은 들판 마름모(R − hw − hh) 밖으로 나가지 않고, 벽에 비스듬히 밀면 벽을 따라 움직인다
    [Test]
    public void CenterStaysInsideFieldAndSlidesAlongWall()
    {
        var context = StartRun();
        var view = context.ReferenceViewHalfExtent;
        var limit = context.RunRules.FieldCells / (2f * context.RunRules.UnitScale) - view.x - view.y;
        Assert.Greater(limit, 0f);

        context.PartyMoveService.SetMoveInput(new Vector2(1f, 0f));
        StepSeconds(context, limit / context.MoveRules.MoveSpeed + 5f);
        var atWall = context.PartyModel.Center;
        Assert.AreEqual(limit, atWall.x, 0.01f);
        Assert.AreEqual(0f, atWall.y, 0.01f);

        // 오른쪽 위로 비스듬히 밀면 벽(x + y = limit)을 따라 위로 미끄러진다
        context.PartyMoveService.SetMoveInput(new Vector2(1f, 1f).normalized);
        StepSeconds(context, 2f);
        var slid = context.PartyModel.Center;
        Assert.Greater(slid.y, atWall.y + 10f);
        Assert.Less(slid.x, atWall.x);
        Assert.AreEqual(limit, Mathf.Abs(slid.x) + Mathf.Abs(slid.y), 0.01f);
    }

    // 보이는 영역이 들판보다 크면 중심은 가운데에 머문다
    [Test]
    public void CenterStaysAtFieldCenterWhenViewIsLargerThanField()
    {
        var context = StartRun();
        var fieldHalfDiagonal = context.RunRules.FieldCells / (2f * context.RunRules.UnitScale);
        context.PartyMoveService.SetViewHalfExtent(new Vector2(fieldHalfDiagonal, fieldHalfDiagonal));
        context.PartyMoveService.SetMoveInput(new Vector2(1f, 0.3f));

        StepSeconds(context, 2f);

        AssertVector(Vector2.zero, context.PartyModel.Center);
    }

    private static TestRunContext StartRun()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, 0f);
        return context;
    }

    // 웨이브 시간과 상관없이 이동만 돌린다
    private static void StepSeconds(TestRunContext context, float seconds)
    {
        var steps = Mathf.RoundToInt(seconds / context.RunRules.SimStep);
        for (var i = 0; i < steps; i++)
        {
            context.PartyMoveService.Step(context.RunRules.SimStep);
        }
    }

    private static void AssertVector(Vector2 expected, Vector2 actual)
    {
        Assert.AreEqual(expected.x, actual.x, Tolerance, $"x of {actual}");
        Assert.AreEqual(expected.y, actual.y, Tolerance, $"y of {actual}");
    }
}
