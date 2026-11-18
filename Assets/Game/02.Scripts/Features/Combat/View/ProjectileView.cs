using UnityEngine;

// T1: 오른쪽 원본의 8방향 표현. 45도 간격과 좌우 반전으로 회전을 -45~90도 안에 두고, 위치는 가상 픽셀에 맞춘다.
public class ProjectileView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform upright;
    public SpriteRenderer BodyRenderer => bodyRenderer;

    public void Initialize(Sprite sprite)
    {
        bodyRenderer.sprite = sprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.flipX = false;
        bodyRenderer.flipY = false;
    }

    public void UpdateView(Vector3 worldPosition, Vector2 direction)
    {
        var angle = CombatEffectMotion.GetProjectileAngle(direction);
        var isFlipped = angle > 90f || angle <= -90f;
        if (angle > 90f)
        {
            angle -= 180f;
        }
        else if (angle <= -90f)
        {
            angle += 180f;
        }

        bodyRenderer.flipX = isFlipped;
        bodyRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = Vector3.one;
        var spriteCenter = bodyRenderer.sprite.bounds.center;
        spriteCenter.x *= isFlipped ? -1f : 1f;
        var center = upright.localRotation * bodyRenderer.transform.localRotation * spriteCenter;
        transform.localPosition = TiltedViewMath.SnapToTexels(worldPosition - center, .5f, Const.PixelsPerUnit);
    }
}
