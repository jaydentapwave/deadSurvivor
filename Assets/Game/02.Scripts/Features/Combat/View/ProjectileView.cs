using UnityEngine;

// T1: 오른쪽을 향한 원본을 90도 정수 회전으로만 돌린다. 위치는 가상 픽셀에 맞춘다.
public class ProjectileView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    public SpriteRenderer BodyRenderer => bodyRenderer;

    public void Initialize(Sprite sprite)
    {
        bodyRenderer.sprite = sprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.flipX = false;
        bodyRenderer.flipY = false;
    }

    public void UpdateView(Vector2 worldPosition, Vector2 direction)
    {
        var angle = CombatEffectMotion.GetProjectileAngle(direction);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = Vector3.one;
        var center = transform.localRotation * bodyRenderer.sprite.bounds.center;
        transform.localPosition = CombatEffectMotion.Snap(worldPosition - (Vector2)center);
    }
}
