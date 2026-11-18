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
    }
}
