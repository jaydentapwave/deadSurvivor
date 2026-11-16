using UnityEngine;

// 원정대원 그림(그림 넣기 기획서 E절) + 머리 위 HP 막대. 그림이 없으면 F1 임시 도형(원).
// 몸 그림은 자식(Body)이라 대기·피격 오프셋은 몸 자식만 옮기고 HP 막대는 따라가지 않는다. 번쩍임은 몸 자식의 자식(같은 스프라이트·flipX, 흰 재질).
public class MemberView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer flashRenderer;
    [SerializeField] private SpriteRenderer hpBackRenderer;
    [SerializeField] private SpriteRenderer hpFillRenderer;
    // HP 바탕의 자식, 빨간 구간(F6)
    [SerializeField] private HpLossBarView hpLossBarView;
    [SerializeField] private int bodyDiameterPixels = 14;
    // 그림이 있을 때 막대 자리의 몸 키(아트 기준서 K1 캐릭터 키)
    [SerializeField] private int artHeightPixels = 48;
    [SerializeField] private Vector2Int hpBarPixels = new(14, 2);
    [SerializeField] private int hpBarGapPixels = 2;
    [SerializeField] private Color hpBackColor = new(0.15f, 0.08f, 0.05f, 1f);
    [SerializeField] private Color hpFillColor = new(0.36f, 0.82f, 0.36f, 1f);
    [SerializeField] private float downedAlpha = 0.35f;
    // 대기·피격·방향 값(기획서 E절, 아트 기준서 3.10, 가정 AI2·AI4)
    [SerializeField] private float idlePeriodSeconds = 0.8f;
    [SerializeField] private float idleStaggerSeconds = 0.2f;
    [SerializeField] private float hitFlashSeconds = 0.1f;
    [SerializeField] private float minMovePixels = 0.25f;
    [SerializeField] private float facingAxisRatio = 0.25f;

    private Sprite _frontSprite;
    private Sprite _backSprite;

    public int BodyDiameterPixels => bodyDiameterPixels;
    public Vector2Int HpBarPixels => hpBarPixels;
    public HpLossBarView HpLossBarView => hpLossBarView;
    public float IdlePeriodSeconds => idlePeriodSeconds;
    public float IdleStaggerSeconds => idleStaggerSeconds;
    public float HitFlashSeconds => hitFlashSeconds;
    public float MinMovePixels => minMovePixels;
    public float FacingAxisRatio => facingAxisRatio;
    public bool HasArt { get; private set; }
    public SpriteFacing Facing { get; private set; } = SpriteFacing.Front;
    public bool IsFlashOn => flashRenderer.enabled;
    public Vector3 BodyOffset => bodyRenderer.transform.localPosition;

    // 그림이 없으면 frontSprite = backSprite = 임시 원, hasArt = false. hpBarSprite: 피벗이 왼쪽 가운데인 흰 상자
    public void Initialize(Sprite frontSprite, Sprite backSprite, Sprite hpBarSprite, bool hasArt)
    {
        _frontSprite = frontSprite;
        _backSprite = backSprite;
        HasArt = hasArt;
        hpBackRenderer.sprite = hpBarSprite;
        hpFillRenderer.sprite = hpBarSprite;
        hpBackRenderer.color = hpBackColor;
        hpFillRenderer.color = hpFillColor;
        var bodyHeightPixels = hasArt ? artHeightPixels : bodyDiameterPixels;
        var barY = (bodyHeightPixels + hpBarGapPixels) / (float)Const.PixelsPerUnit;
        var barX = -hpBarPixels.x * 0.5f / Const.PixelsPerUnit;
        hpBackRenderer.transform.localPosition = new Vector3(barX, barY, 0f);
        hpFillRenderer.transform.localPosition = new Vector3(barX, barY, 0f);
        ApplyMotion(SpriteFacing.Front, Vector2Int.zero, false);
    }

    public void UpdateView(Vector2 worldPosition, float hpRatio, bool isAlive)
    {
        transform.localPosition = worldPosition;
        hpFillRenderer.transform.localScale = new Vector3(Mathf.Clamp01(hpRatio), 1f, 1f);
        hpBackRenderer.enabled = isAlive;
        hpFillRenderer.enabled = isAlive;
        var bodyColor = bodyRenderer.color;
        bodyColor.a = isAlive ? 1f : downedAlpha;
        bodyRenderer.color = bodyColor;
    }

    // 방향(앞·뒤 스프라이트, 좌우 반전)·몸 오프셋(정수 픽셀)·흰 번쩍임을 한 번에 적용
    public void ApplyMotion(SpriteFacing facing, Vector2Int bodyOffsetPixels, bool isFlashOn)
    {
        Facing = facing;
        var sprite = facing.IsBack ? _backSprite : _frontSprite;
        bodyRenderer.sprite = sprite;
        bodyRenderer.flipX = facing.IsFlipped;
        bodyRenderer.transform.localPosition = SpriteMotion.ToUnits(bodyOffsetPixels);
        flashRenderer.sprite = sprite;
        flashRenderer.flipX = facing.IsFlipped;
        flashRenderer.enabled = isFlashOn;
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);
}
