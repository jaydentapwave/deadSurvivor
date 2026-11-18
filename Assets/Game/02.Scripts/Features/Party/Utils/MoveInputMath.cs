using UnityEngine;

// 이동 입력의 순수 계산(F2 기획서 D절). 결과는 화면 기준(x 오른쪽, y 위), 길이 ≤ 1.
public static class MoveInputMath
{
    // (지금 − 누른 자리) ÷ 반지름(화면 px), 길이 1로 자른다
    public static Vector2 JoystickInput(Vector2 pressPosition, Vector2 currentPosition, float radiusPixels)
    {
        return Vector2.ClampMagnitude((currentPosition - pressPosition) / radiusPixels, 1f);
    }

    // (D − A, W − S), 길이 1로 자른다. 반대 키를 함께 누르면 그 축은 0
    public static Vector2 KeyboardInput(bool isUpPressed, bool isLeftPressed, bool isDownPressed, bool isRightPressed)
    {
        var x = (isRightPressed ? 1f : 0f) - (isLeftPressed ? 1f : 0f);
        var y = (isUpPressed ? 1f : 0f) - (isDownPressed ? 1f : 0f);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }
}
