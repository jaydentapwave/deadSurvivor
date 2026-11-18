using UnityEngine;

// 방향을 굽힌 그림을 고른다. 뿌리=바닥 점, 세운 그림과 바닥 그림자는 별도 자식이다.
public class ProjectileView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform upright;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private Material litMaterial;
    [SerializeField] private Material glowMaterial;
    public SpriteRenderer BodyRenderer => bodyRenderer;
    public string EffectKey { get; private set; }
    public int FrameNumber { get; private set; }

    public void Initialize(Sprite sprite)
    {
        bodyRenderer.sprite = sprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.flipX = false;
        bodyRenderer.flipY = false;
        bodyRenderer.transform.localRotation = Quaternion.identity;
        bodyRenderer.transform.localScale = Vector3.one;
        bodyRenderer.transform.localPosition = Vector3.zero;
        EffectKey = string.Empty;
        FrameNumber = 1;
    }

    public void SetFrame(Sprite sprite, string key, int frameNumber, bool isGlowing)
    {
        bodyRenderer.sprite = sprite;
        bodyRenderer.sharedMaterial = isGlowing ? glowMaterial : litMaterial;
        EffectKey = key;
        FrameNumber = frameNumber;
    }

    public void UpdateView(Vector3 worldPosition, Vector2 direction)
    {
        var facing = EffectFrameMath.DirectionSprite(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        bodyRenderer.flipX = facing.flipX;
        bodyRenderer.flipY = facing.flipY;
        bodyRenderer.transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        transform.localPosition = TiltedViewMath.SnapToTexels(worldPosition, .5f, Const.PixelsPerUnit);
        // 뿌리/그림자를 움직이지 않고 그림의 왼쪽 아래 모서리 위상만 보정한다.
        var sprite = bodyRenderer.sprite;
        var pivot = new Vector2(facing.flipX ? sprite.rect.width - sprite.pivot.x : sprite.pivot.x,
            facing.flipY ? sprite.rect.height - sprite.pivot.y : sprite.pivot.y);
        bodyRenderer.transform.position = CardSnapMath.Snap(upright.position, pivot, sprite.pixelsPerUnit, upright.eulerAngles.x);
    }
}
