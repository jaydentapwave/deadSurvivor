using UnityEngine;

// 바닥 점과 세운 포자 그림을 분리한다. 풀에서 꺼낼 때 프레임·반전을 초기화한다.
public class EnemyShotView : MonoBehaviour, IEffectFrameSource
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform upright;
    [SerializeField] private Material terrainShadowMaterial;
    private TerrainHeightGrid _terrain = TerrainHeightGrid.Flat;
    [SerializeField] private SpriteRenderer shadowRenderer;
    private Sprite[] _frames;
    private float _fps;
    public SpriteRenderer BodyRenderer => bodyRenderer;
    public Renderer ActiveRenderer => bodyRenderer;
    public Sprite SourceSprite => bodyRenderer.sprite;
    public float Alpha => bodyRenderer.color.a;
    public bool IsShown => gameObject.activeInHierarchy && bodyRenderer.enabled;
    public void SetTerrain(TerrainHeightGrid terrain) => _terrain = terrain;

    private void UpdateGroundShadow()
    {
        if (_terrain.IsFlat)
        {
            return;
        }
        var normal = TerrainHeightMath.Normal(_terrain, transform.position.x, transform.position.z);
        shadowRenderer.gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
        shadowRenderer.sharedMaterial = terrainShadowMaterial;
        shadowRenderer.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(90, 0, 0);
        shadowRenderer.transform.localPosition = normal * .001f;
    }
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
        UpdateGroundShadow();
    }
}
