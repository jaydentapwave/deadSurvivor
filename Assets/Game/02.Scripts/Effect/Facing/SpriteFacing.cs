using UnityEngine;

// 그림이 보는 방향(그림 넣기 기획서 E절, 아트 기준서 K14·3.1): 앞(오른쪽 아래)·뒤(오른쪽 위) + 좌우 반전 = 4방향.
// 이번 프레임 화면(월드) 이동량으로 축마다 따로 정하고, 기준 아래인 축은 지난 값을 둔다.
public readonly struct SpriteFacing
{
    // 한 판 시작·풀에서 꺼냄: 앞 + 반전 없음(오른쪽 아래, 기준서 정면)
    public static readonly SpriteFacing Front = new(false, false);

    public bool IsBack { get; }
    public bool IsFlipped { get; }

    // 판정 경로 표기: "front-right"·"front-left"·"back-right"·"back-left"
    public string Label => (IsBack ? "back" : "front") + (IsFlipped ? "-left" : "-right");

    public SpriteFacing(bool isBack, bool isFlipped)
    {
        IsBack = isBack;
        IsFlipped = isFlipped;
    }

    // canFaceBack = false(몬스터)면 늘 앞이고 좌우만 바뀐다
    public static SpriteFacing Next(SpriteFacing last, Vector2 delta, float minMove, float axisRatio, bool canFaceBack)
    {
        var absX = Mathf.Abs(delta.x);
        var absY = Mathf.Abs(delta.y);
        var isBack = last.IsBack;
        if (!canFaceBack)
        {
            isBack = false;
        }
        else if (absY >= minMove && absY >= axisRatio * absX)
        {
            isBack = delta.y > 0f;
        }

        var isFlipped = last.IsFlipped;
        if (absX >= minMove && absX >= axisRatio * absY)
        {
            isFlipped = delta.x < 0f;
        }

        return new SpriteFacing(isBack, isFlipped);
    }
}
