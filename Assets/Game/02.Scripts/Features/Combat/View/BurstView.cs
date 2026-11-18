using UnityEngine;

// T1~T3: 한 장짜리 효과. 피벗은 임포트 규약 그대로, 표시 중심만 보정한다.
public class BurstView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform upright;
    private Quaternion _uprightRotation;
    public SpriteRenderer BodyRenderer => bodyRenderer;

    public void Initialize(Sprite sprite)
    {
        _uprightRotation = TiltedViewMath.UprightRotation(30f);
        bodyRenderer.sprite = sprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.flipX = false;
        bodyRenderer.flipY = false;
    }

    public void UpdateView(Vector3 worldPosition, Vector2 scale, float alpha, float angle = 0f, bool isFlipped = false,
        bool snapToPixels = true, bool isGround = false)
    {
        upright.localRotation = isGround ? Quaternion.Euler(90f, 0f, 0f) : _uprightRotation;
        bodyRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        bodyRenderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        bodyRenderer.flipX = isFlipped;
        bodyRenderer.color = new Color(1f, 1f, 1f, alpha);
        var center = bodyRenderer.sprite.bounds.center;
        center.x *= isFlipped ? -scale.x : scale.x;
        center.y *= scale.y;
        var position = worldPosition - upright.localRotation * bodyRenderer.transform.localRotation * center;
        if (isGround)
        {
            position.y = 0.001f;
        }
        // 연결 마디의 반 픽셀 중심은 유지해야 한다. 마디마다 스냅하면 짧은 끝 마디에 겹침·빈틈이 생긴다.
        transform.localPosition = snapToPixels ? TiltedViewMath.SnapToTexels(position, .5f, Const.PixelsPerUnit) : position;
    }
}
