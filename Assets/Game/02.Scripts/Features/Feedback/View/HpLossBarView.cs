using UnityEngine;

// 머리 위 HP 막대의 빨간 구간(F6 기획서 C절). MemberView HP 바탕의 자식으로 장면에 두고(바탕 왼쪽 끝 = 원점),
// 막대와 같은 흰 상자 그림(피벗 왼쪽 가운데)을 지금 비율 끝부터 빨간 구간 길이만큼 늘려 그린다.
public class HpLossBarView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer lossRenderer;

    private float _barWidth;

    // barWidth = 막대 전체 폭(유닛). 그림 한 장의 폭이 막대 폭과 같다
    public void Initialize(Sprite barSprite, Color color, float barWidth)
    {
        lossRenderer.sprite = barSprite;
        lossRenderer.color = color;
        _barWidth = barWidth;
        lossRenderer.enabled = false;
    }

    // 빨간 구간 = 지금 비율 ~ lossFromRatio
    public void UpdateUI(float currentRatio, float lossFromRatio)
    {
        var current = Mathf.Clamp01(currentRatio);
        var length = Mathf.Clamp01(lossFromRatio) - current;
        lossRenderer.enabled = length > 0f;
        if (length <= 0f)
        {
            return;
        }

        transform.localPosition = new Vector3(current * _barWidth, 0f, 0f);
        transform.localScale = new Vector3(length, 1f, 1f);
    }
}
