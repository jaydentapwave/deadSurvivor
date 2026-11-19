using UnityEngine;

// 알파 클립 프레임 효과. 임포트 중심/시작점 피벗을 그대로 배치한다.
public class BurstView : MonoBehaviour, IEffectFrameSource
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform upright;
    [SerializeField] private Material[] effectMaterials;
    [SerializeField] private Material[] terrainEffectMaterials;
    private TerrainHeightGrid _terrain = TerrainHeightGrid.Flat;
    private ConformedDecal _groundDecal;
    private Quaternion _uprightRotation;
    public Renderer ActiveRenderer => _groundDecal != null && !bodyRenderer.enabled ? _groundDecal.ActiveRenderer : bodyRenderer;
    public Sprite SourceSprite => bodyRenderer.sprite;
    public float Alpha => bodyRenderer.color.a;
    public bool IsShown => gameObject.activeInHierarchy && ActiveRenderer != null && ActiveRenderer.enabled;
    public void SetTerrain(TerrainHeightGrid terrain) => _terrain = terrain;
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
        // 화면 자리는 그대로 두고 베기 그림만 카메라 뒤쪽으로. 판정·뿌리 좌표는 유지한다.
        var isSlash = key == "slash_sword" || key == "slash_axe";
        bodyRenderer.transform.localPosition = isSlash ? Vector3.forward * .6f : Vector3.zero;
        bodyRenderer.transform.localRotation = Quaternion.identity;
        bodyRenderer.transform.localScale = new Vector3(1, isGround ? 1f / sinTilt : 1f, 1);
        upright.localRotation = isGround ? Quaternion.Euler(90, 0, 0) : _uprightRotation;
        transform.localPosition = TiltedViewMath.SnapScreenPlane(worldPosition, sinTilt, Const.PixelsPerUnit);
        var isConformed = isGround && !_terrain.IsFlat;
        bodyRenderer.enabled = !isConformed;
        if (_groundDecal != null)
        {
            _groundDecal.SetVisible(false);
        }
        if (isConformed)
        {
            if (_groundDecal == null)
            {
                var child = new GameObject("Terrain ground effect");
                child.transform.SetParent(transform, false);
                _groundDecal = child.AddComponent<ConformedDecal>();
            }
            _groundDecal.UpdateView(_terrain, worldPosition, frame, terrainEffectMaterials[materialIndex], new Vector2(1, 1f / sinTilt), isFlipped);
        }
        else if (isGround)
        {
            var position = transform.localPosition;
            position.y = .001f;
            transform.localPosition = position;
        }
    }

    public void Initialize(Sprite sprite)
    {
        _uprightRotation = TiltedViewMath.UprightRotation(30f);
        bodyRenderer.transform.localPosition = Vector3.zero;
        bodyRenderer.sprite = sprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.flipX = false;
        bodyRenderer.flipY = false;
    }

}
