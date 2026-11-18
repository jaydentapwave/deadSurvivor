using UnityEngine;

// 대원 하나의 HP 감소 구간 빨간 강조 상태(F6 기획서 C절). 비율은 HP ÷ 최대 HP(0~1).
// 빨간 구간 = 지금 비율 ~ LossFrom. 머무는 동안 그대로, 그 뒤 처음 길이 ÷ shrinkSeconds 속도로 줄어 지금 비율에 닿으면 끝.
public class HpLossTrail
{
    private float _currentRatio = 1f;
    private float _shrinkPerSecond;
    private bool _isShrinking;

    public float LossFrom { get; private set; } = 1f;
    public float HoldRemaining { get; private set; }
    public float Length => Mathf.Max(0f, LossFrom - _currentRatio);

    // HP가 줄었을 때만 시작점을 높은 쪽으로 올리고 머무름을 처음부터. HP가 그대로(보호막이 다 흡수)면 아무것도 안 한다
    public void Hit(float beforeRatio, float afterRatio, float holdSeconds)
    {
        if (afterRatio >= beforeRatio)
        {
            return;
        }

        LossFrom = Mathf.Max(LossFrom, beforeRatio);
        HoldRemaining = holdSeconds;
        _isShrinking = false;
    }

    // 프레임마다. HP가 올랐거나(회복·부활) 지금 비율이 시작점 이상이면 바로 끝
    public void Tick(float deltaSeconds, float currentRatio, float shrinkSeconds)
    {
        var hasRisen = currentRatio > _currentRatio;
        _currentRatio = currentRatio;
        if (hasRisen || currentRatio >= LossFrom)
        {
            Reset(currentRatio);
            return;
        }

        var leftSeconds = deltaSeconds;
        if (HoldRemaining > 0f)
        {
            HoldRemaining -= leftSeconds;
            if (HoldRemaining > 0f)
            {
                return;
            }

            leftSeconds = -HoldRemaining;
            HoldRemaining = 0f;
        }

        if (!_isShrinking)
        {
            _isShrinking = true;
            _shrinkPerSecond = (LossFrom - currentRatio) / shrinkSeconds;
        }

        LossFrom -= _shrinkPerSecond * leftSeconds;
        if (LossFrom <= currentRatio)
        {
            Reset(currentRatio);
        }
    }

    public void Reset(float currentRatio)
    {
        _currentRatio = currentRatio;
        LossFrom = currentRatio;
        HoldRemaining = 0f;
        _isShrinking = false;
    }
}
