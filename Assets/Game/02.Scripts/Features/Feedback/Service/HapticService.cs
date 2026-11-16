using UnityEngine;

// 원정대 피격 진동(F6 기획서 D절). 이번 판 호출 수를 세고, Android·iOS 기기 실행에서만 Handheld.Vibrate를 부른다.
// 에디터·PC는 호출 수만 남는다(판정 경로가 이것으로 확인한다). 세기는 고를 수 없다(FA5).
public class HapticService
{
    private readonly HapticRulesData _rules;

    private float _lastPlayTime = float.NegativeInfinity;

    public int PlayCount { get; private set; }

    public HapticService(HapticRulesData rules)
    {
        _rules = rules;
    }

    // now = 피드백 시계. 꺼져 있거나 지난 진동에서 minInterval이 안 지났으면 부르지 않는다
    public bool TryPlay(float now)
    {
        if (!_rules.Enabled || now - _lastPlayTime < _rules.MinInterval)
        {
            return false;
        }

        _lastPlayTime = now;
        PlayCount++;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
        return true;
    }

    public void ResetRun()
    {
        PlayCount = 0;
        _lastPlayTime = float.NegativeInfinity;
    }
}
