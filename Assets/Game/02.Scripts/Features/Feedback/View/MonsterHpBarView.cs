using UnityEngine;

// 강한 적·공격 중인 적의 HP 막대(바탕·채움 두 장, 정렬 55, F6 기획서 E절). 풀에서 꺼낸다.
public class MonsterHpBarView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backRenderer;
    [SerializeField] private SpriteRenderer fillRenderer;

    // barSprite: 피벗 왼쪽 가운데 흰 상자(막대 폭·높이), barOffset: 막대 왼쪽 끝 자리(뿌리 기준)
    public void Initialize(Sprite barSprite, Sprite fillSprite, Color backColor, Color fillColor, Vector2 barOffset, Vector2 size)
    {
        UiBarUtils.Initialize(backRenderer, fillRenderer, barSprite, fillSprite, size, barOffset, backColor, fillColor);
    }

    public void UpdateView(Vector2 worldPosition, float hpRatio)
    {
        transform.localPosition = worldPosition;
        fillRenderer.transform.localScale = new Vector3(Mathf.Clamp01(hpRatio), 1f, 1f);
    }
}
