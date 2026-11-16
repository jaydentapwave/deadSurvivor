using UnityEngine;

// 그림 움직임 계산(그림 넣기 기획서 E절, 아트 기준서 3.10). 모두 정수 픽셀 — 회전·확대·소수 이동 없음(2d-animation.md 5절).
// 시간 → 프레임(두 프레임 순환) → 픽셀 오프셋. 픽셀을 유닛으로 바꾸는 것은 ToUnits(1/48의 정수배).
public static class SpriteMotion
{
    // 움직임 시계: 전투 중이고 일시정지가 아닐 때만 흐른다(F6 피드백 시계와 같은 규칙)
    public static float Advance(float clock, float deltaTime, bool isRunning) => isRunning ? clock + deltaTime : clock;

    // 두 프레임 순환: 주기 앞 절반 0, 뒤 절반 1. startOffset으로 대상마다 시작을 어긋낸다
    public static int GetCycleFrame(float time, float period, float startOffset)
    {
        var phase = Mathf.Repeat(time + startOffset, period);
        return phase < period * 0.5f ? 0 : 1;
    }

    // 대원 대기: 0px / +1px(위)
    public static int GetIdleOffsetY(float time, float period, float startOffset) => GetCycleFrame(time, period, startOffset);

    // 몬스터 이동: hop 0px / +1px(통통 뛰기), squash 0px / −1px(눌림)
    public static int GetMoveOffsetY(MonsterMoveStyle moveStyle, float time, float period, float startOffset)
    {
        var frame = GetCycleFrame(time, period, startOffset);
        return moveStyle == MonsterMoveStyle.Hop ? frame : -frame;
    }

    // 피격 밀림: 번쩍이는 동안 보는 쪽 반대로 가로 1px(반전 없음 = 오른쪽을 봄 → −1)
    public static int GetHitOffsetX(bool isFlashOn, bool isFlipped)
    {
        if (!isFlashOn)
        {
            return 0;
        }

        return isFlipped ? 1 : -1;
    }

    public static Vector3 ToUnits(Vector2Int pixels) =>
        new(pixels.x / (float)Const.PixelsPerUnit, pixels.y / (float)Const.PixelsPerUnit, 0f);
}

// 피격 흰 번쩍임 한 대원의 상태. 번쩍이는 중 또 맞으면 처음부터(남은 시간·경과를 다시 센다)
public struct HitFlash
{
    // 프레임 시간 합의 부동소수 오차(0.1초 = 1/60초 × 6이 0.1보다 조금 작게 더해짐)로 한 프레임 더 켜지지 않게 하는 여유
    private const float EndTolerance = 1e-5f;

    public float Remaining { get; private set; }
    // 이번 번쩍임(마지막 시작부터)이 켜져 있던 시간
    public float Elapsed { get; private set; }
    public bool IsOn => Remaining > 0f;

    public void Start(float seconds)
    {
        Remaining = seconds;
        Elapsed = 0f;
    }

    // 꺼진 프레임이면 true(이번 번쩍임이 끝남)
    public bool Advance(float deltaSeconds)
    {
        if (!IsOn)
        {
            return false;
        }

        Elapsed += deltaSeconds;
        Remaining -= deltaSeconds;
        if (Remaining > EndTolerance)
        {
            return false;
        }

        Remaining = 0f;
        return true;
    }

    public void Stop()
    {
        Remaining = 0f;
        Elapsed = 0f;
    }
}
