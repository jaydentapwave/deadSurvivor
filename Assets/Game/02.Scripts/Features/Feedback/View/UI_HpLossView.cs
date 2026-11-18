using UnityEngine;
using UnityEngine.UI;

// 하단 HP 막대의 빨간 구간(uGUI Image, 채움 뒤, F6 기획서 C절). 앵커 비율로 길이를 정한다.
public class UI_HpLossView : MonoBehaviour
{
    [SerializeField] private Image lossImage;

    public void Initialize(Color color)
    {
        lossImage.color = color;
        lossImage.enabled = false;
    }

    // 빨간 구간 = 지금 비율 ~ lossFromRatio
    public void UpdateUI(float currentRatio, float lossFromRatio)
    {
        var current = Mathf.Clamp01(currentRatio);
        var lossFrom = Mathf.Clamp01(lossFromRatio);
        lossImage.enabled = lossFrom > current;
        if (lossFrom <= current)
        {
            return;
        }

        var rect = lossImage.rectTransform;
        rect.anchorMin = new Vector2(current, 0f);
        rect.anchorMax = new Vector2(lossFrom, 1f);
    }
}
