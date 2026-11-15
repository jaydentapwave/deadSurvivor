using UnityEngine;

// 임시 적 그림(풀에서 꺼낸다). F4가 몬스터 5종 그림으로 바꾼다.
public class MonsterView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;

    public void Initialize(Sprite sprite) => bodyRenderer.sprite = sprite;

    public void UpdateView(Vector2 worldPosition) => transform.localPosition = worldPosition;
}
