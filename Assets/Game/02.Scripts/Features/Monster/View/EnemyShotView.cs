using UnityEngine;

// 바닥 점과 세운 포자 그림을 분리한다. 풀에서 꺼낼 때 프레임·반전을 초기화한다.
public class EnemyShotView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform upright;
    [SerializeField] private SpriteRenderer shadowRenderer;
    private Sprite[] _frames;
    private float _fps;
    public SpriteRenderer BodyRenderer => bodyRenderer;
    public int FrameNumber { get; private set; }

    public void Initialize(Sprite[] frames, float fps)
    {
        _frames = frames;
        _fps = fps;
        FrameNumber = 1;
        bodyRenderer.sprite = frames[0];
        bodyRenderer.color = Color.white;
        bodyRenderer.flipX = false;
        bodyRenderer.flipY = false;
        bodyRenderer.transform.localRotation = Quaternion.identity;
    }

    public void UpdateView(Vector3 worldPosition, float elapsed)
    {
        var index = EffectFrameMath.FrameAt(elapsed, _frames.Length, _fps, true);
        FrameNumber = index + 1;
        bodyRenderer.sprite = _frames[index];
        transform.localPosition = worldPosition;
    }
}
