using UnityEngine;

// 알파 클립 프레임 효과. 임포트 중심/시작점 피벗을 그대로 배치한다.
public class BurstView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform upright;
    [SerializeField] private Material[] effectMaterials;
    private Quaternion _uprightRotation;
    public SpriteRenderer BodyRenderer => bodyRenderer;

    public string EffectKey { get; private set; }
    public int FrameNumber { get; private set; }

    public void UpdateView(Vector3 worldPosition, Sprite frame, string key, int frameNumber,
        int materialIndex, bool isFlipped = false, bool isGround = false, float sinTilt = .5f)
    {
        EffectKey = key;
        FrameNumber = frameNumber;
        bodyRenderer.sprite = frame;
        bodyRenderer.sharedMaterial = effectMaterials[materialIndex];
        bodyRenderer.color = Color.white;
        bodyRenderer.flipX = isFlipped;
        bodyRenderer.flipY = false;
        bodyRenderer.transform.localRotation = Quaternion.identity;
        bodyRenderer.transform.localScale = new Vector3(1, isGround ? 1f / sinTilt : 1f, 1);
        upright.localRotation = isGround ? Quaternion.Euler(90, 0, 0) : _uprightRotation;
        transform.localPosition = TiltedViewMath.SnapToTexels(worldPosition, sinTilt, Const.PixelsPerUnit);
        if (isGround)
        {
            var position = transform.localPosition;
            position.y = .001f;
            transform.localPosition = position;
        }
    }

    public void Initialize(Sprite sprite)
    {
        _uprightRotation = TiltedViewMath.UprightRotation(30f);
        bodyRenderer.sprite = sprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.flipX = false;
        bodyRenderer.flipY = false;
    }

}
