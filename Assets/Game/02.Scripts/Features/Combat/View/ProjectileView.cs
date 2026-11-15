using UnityEngine;

// 투사체 임시 도형(풀에서 꺼낸다). 효과 그림은 아트 단계가 바꾼다
public class ProjectileView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;

    public void Initialize(Sprite sprite) => bodyRenderer.sprite = sprite;

    public void UpdateView(Vector2 worldPosition) => transform.localPosition = worldPosition;
}
