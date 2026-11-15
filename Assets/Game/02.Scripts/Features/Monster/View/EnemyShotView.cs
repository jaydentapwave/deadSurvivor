using UnityEngine;

// 적 투사체 임시 그림(작은 원, 풀에서 꺼낸다, 기획서 F절)
public class EnemyShotView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;

    public void Initialize(Sprite sprite) => bodyRenderer.sprite = sprite;

    public void UpdateView(Vector2 worldPosition) => transform.localPosition = worldPosition;
}
