using UnityEngine;

// 범위 발동 고리 임시 도형(풀에서 꺼낸다). 그림은 기준 반경의 바닥 원 크기로 만들고 반경에 맞춰 늘린다
public class BurstView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;

    public void Initialize(Sprite sprite) => bodyRenderer.sprite = sprite;

    // alpha: 남은 시간 비율(사라지며 옅어짐)
    public void UpdateView(Vector2 worldPosition, float scale, float alpha)
    {
        transform.localPosition = worldPosition;
        transform.localScale = new Vector3(scale, scale, 1f);
        var color = bodyRenderer.color;
        color.a = alpha;
        bodyRenderer.color = color;
    }
}
