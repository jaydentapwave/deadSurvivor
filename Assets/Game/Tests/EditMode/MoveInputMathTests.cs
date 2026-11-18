using NUnit.Framework;
using UnityEngine;

// F2 AC15 — 조이스틱 입력, WASD 입력
public class MoveInputMathTests
{
    private const float Tolerance = 1e-4f;
    private static readonly Vector2 Press = new(400f, 300f);

    // 반지름 안에서는 끈 거리에 비례(반지름 = joystickRadius × 캔버스 배율)
    [Test]
    public void JoystickIsProportionalInsideRadius()
    {
        var radius = 18f * 3f;
        AssertVector(new Vector2(0.5f, 0f), MoveInputMath.JoystickInput(Press, Press + new Vector2(27f, 0f), radius));
        AssertVector(new Vector2(0f, -0.25f), MoveInputMath.JoystickInput(Press, Press + new Vector2(0f, -13.5f), radius));
        AssertVector(new Vector2(0.3f, 0.4f), MoveInputMath.JoystickInput(Press, Press + new Vector2(16.2f, 21.6f), radius));
        AssertVector(Vector2.zero, MoveInputMath.JoystickInput(Press, Press, radius));
    }

    // 반지름 밖에서는 길이 1
    [Test]
    public void JoystickIsUnitLengthOutsideRadius()
    {
        var radius = 18f * 2f;
        AssertVector(new Vector2(1f, 0f), MoveInputMath.JoystickInput(Press, Press + new Vector2(200f, 0f), radius));
        var diagonal = MoveInputMath.JoystickInput(Press, Press + new Vector2(-100f, 100f), radius);
        Assert.AreEqual(1f, diagonal.magnitude, Tolerance);
        AssertVector(new Vector2(-1f, 1f).normalized, diagonal);
    }

    // WASD 8방향, 대각선 길이 1
    [Test]
    public void KeyboardHasEightDirections()
    {
        AssertVector(new Vector2(0f, 1f), MoveInputMath.KeyboardInput(true, false, false, false));
        AssertVector(new Vector2(-1f, 0f), MoveInputMath.KeyboardInput(false, true, false, false));
        AssertVector(new Vector2(0f, -1f), MoveInputMath.KeyboardInput(false, false, true, false));
        AssertVector(new Vector2(1f, 0f), MoveInputMath.KeyboardInput(false, false, false, true));
        AssertVector(new Vector2(1f, 1f).normalized, MoveInputMath.KeyboardInput(true, false, false, true));
        AssertVector(new Vector2(-1f, 1f).normalized, MoveInputMath.KeyboardInput(true, true, false, false));
        AssertVector(new Vector2(-1f, -1f).normalized, MoveInputMath.KeyboardInput(false, true, true, false));
        AssertVector(new Vector2(1f, -1f).normalized, MoveInputMath.KeyboardInput(false, false, true, true));
        AssertVector(Vector2.zero, MoveInputMath.KeyboardInput(false, false, false, false));
    }

    // 반대 키를 함께 누르면 그 축이 0
    [Test]
    public void OppositeKeysCancelThatAxis()
    {
        AssertVector(Vector2.zero, MoveInputMath.KeyboardInput(true, false, true, false));
        AssertVector(Vector2.zero, MoveInputMath.KeyboardInput(false, true, false, true));
        AssertVector(new Vector2(0f, 1f), MoveInputMath.KeyboardInput(true, true, false, true));
        AssertVector(new Vector2(-1f, 0f), MoveInputMath.KeyboardInput(true, true, true, false));
    }

    private static void AssertVector(Vector2 expected, Vector2 actual)
    {
        Assert.AreEqual(expected.x, actual.x, Tolerance, $"x of {actual}");
        Assert.AreEqual(expected.y, actual.y, Tolerance, $"y of {actual}");
    }
}
