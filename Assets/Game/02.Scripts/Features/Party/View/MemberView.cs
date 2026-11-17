using UnityEngine;

// 원정대원 그림(그림 넣기 기획서 E절) + 머리 위 HP 막대. 그림이 없으면 F1 임시 도형(원).
// 쓰러지면 쓰러진 그림(좌우 반전은 쓰러질 때 방향)으로 바꾸고 downedDimSeconds 동안 downedColor까지 어두워진다(아트 기준서 3.10). 부활하면 방향 그림으로 돌아온다.
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
    // 쓰러짐(아트 기준서 3.10 사망: 0.3초 어둡게)
    [SerializeField] private float downedDimSeconds = 0.3f;
    [SerializeField] private Color downedColor = new(0.45f, 0.45f, 0.45f, 1f);
    // 대기·피격·방향 값(기획서 E절, 아트 기준서 3.10, 가정 AI2·AI4)
    [SerializeField] private float idlePeriodSeconds = 0.8f;
    [SerializeField] private float idleStaggerSeconds = 0.2f;
    [SerializeField] private float hitFlashSeconds = 0.1f;
    [SerializeField] private float minMovePixels = 0.25f;
    [SerializeField] private float facingAxisRatio = 0.25f;

    private Sprite _frontSprite;
    private Sprite _backSprite;
    private Sprite _downedSprite;
    private Sprite _motionSprite;
    private Sprite[] _walkFrontFrames;
    private Sprite[] _walkBackFrames;
    private Sprite[] _attackFrontFrames;
    private Sprite[] _attackBackFrames;
    private MemberMotionData _motionData;
    private FootShadowView _shadow;

    public int BodyDiameterPixels => bodyDiameterPixels;
    public Vector2Int HpBarPixels => hpBarPixels;
    public HpLossBarView HpLossBarView => hpLossBarView;
    public float IdlePeriodSeconds => idlePeriodSeconds;
    public float IdleStaggerSeconds => idleStaggerSeconds;
    public float HitFlashSeconds => hitFlashSeconds;
    public float MinMovePixels => minMovePixels;
    public float FacingAxisRatio => facingAxisRatio;
    public bool HasArt { get; private set; }
    public bool IsDowned { get; private set; }
    public string BodySpriteName => bodyRenderer.sprite.name;
    public Color BodyColor => bodyRenderer.color;
    public SpriteFacing Facing { get; private set; } = SpriteFacing.Front;
    public bool IsFlashOn => flashRenderer.enabled;
    public Vector3 BodyOffset => bodyRenderer.transform.localPosition;

    // 그림이 없으면 frontSprite = backSprite = downedSprite = 임시 원, hasArt = false. HP 틀·흰 채움: 피벗 왼쪽 가운데
    public void Initialize(Sprite frontSprite, Sprite backSprite, Sprite downedSprite, Sprite hpBarSprite, Sprite hpFillSprite, bool hasArt)
    {
        _motionSprite = null;
        _frontSprite = frontSprite;
        _backSprite = backSprite;
        _downedSprite = downedSprite;
        HasArt = hasArt;
        IsDowned = false;
        bodyRenderer.color = Color.white;
        var bodyHeightPixels = hasArt ? artHeightPixels : bodyDiameterPixels;
        var barY = (bodyHeightPixels + hpBarGapPixels) / (float)Const.PixelsPerUnit;
        var barX = -hpBarPixels.x * 0.5f / Const.PixelsPerUnit;
        UiBarUtils.Initialize(hpBackRenderer, hpFillRenderer, hpBarSprite, hpFillSprite,
            (Vector2)hpBarPixels / Const.PixelsPerUnit, new Vector2(barX, barY), hpBackColor, hpFillColor);
        ApplyMotion(SpriteFacing.Front, Vector2Int.zero, false);
    }

    // downedSeconds: 쓰러진 뒤 흐른 시간(산 대원이면 쓰지 않는다)
    public void UpdateView(Vector2 worldPosition, float hpRatio, bool isAlive, float downedSeconds)
    {
        transform.localPosition = worldPosition;
        hpFillRenderer.transform.localScale = new Vector3(Mathf.Clamp01(hpRatio), 1f, 1f);
        hpBackRenderer.enabled = isAlive;
        hpFillRenderer.enabled = isAlive;
        bodyRenderer.color = isAlive ? Color.white : Color.Lerp(Color.white, downedColor, downedSeconds / downedDimSeconds);
        if (IsDowned == isAlive)
        {
            IsDowned = !isAlive;
            ApplySprite();
        }
    }

    // 방향(앞·뒤 스프라이트, 좌우 반전)·몸 오프셋(정수 픽셀)·흰 번쩍임을 한 번에 적용
    public void ApplyMotion(SpriteFacing facing, Vector2Int bodyOffsetPixels, bool isFlashOn)
    {
        Facing = facing;
        ApplySprite();
        bodyRenderer.transform.localPosition = SpriteMotion.ToUnits(bodyOffsetPixels);
        flashRenderer.enabled = isFlashOn;
    }

    public void InitializeClips(MemberMotionData data, Sprite[] walkFront, Sprite[] walkBack, Sprite[] attackFront, Sprite[] attackBack)
    {
        _motionData = data;
        _walkFrontFrames = walkFront;
        _walkBackFrames = walkBack;
        _attackFrontFrames = attackFront;
        _attackBackFrames = attackBack;
    }

    public void InitializeShadow(Sprite sprite)
    {
        if (_shadow == null)
        {
            var child = new GameObject("Shadow");
            child.transform.SetParent(transform, false);
            _shadow = child.AddComponent<FootShadowView>();
        }

        _shadow.Initialize(sprite, _frontSprite.bounds.size.x * _frontSprite.pixelsPerUnit, bodyRenderer.sortingLayerID);
    }

    // 프레임 선택만 한다. 공격 시계·발동은 CombatService 소유.
    public void ApplyAnimation(bool isMoving, float walkClock, MemberAttackMotion attack)
    {
        _motionSprite = null;
        if (!IsDowned && _motionData != null && HasArt)
        {
            if (attack.IsPlaying)
            {
                var frames = attack.Facing.IsBack ? _attackBackFrames : _attackFrontFrames;
                _motionSprite = frames[attack.GetFrame(frames.Length)];
            }
            else if (isMoving)
            {
                var frames = Facing.IsBack ? _walkBackFrames : _walkFrontFrames;
                var frame = Mathf.FloorToInt(walkClock * _motionData.Fps + 1e-5f) % frames.Length;
                _motionSprite = frames[frame];
            }
        }

        ApplySprite();
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

    // 쓰러짐이면 쓰러진 그림, 아니면 방향의 앞·뒤 그림. 좌우 반전은 늘 Facing(쓰러진 뒤에는 쓰러질 때 방향)
    private void ApplySprite()
    {
        var sprite = IsDowned ? _downedSprite : _motionSprite != null ? _motionSprite : Facing.IsBack ? _backSprite : _frontSprite;
        bodyRenderer.sprite = sprite;
        bodyRenderer.flipX = Facing.IsFlipped;
        flashRenderer.sprite = sprite;
        flashRenderer.flipX = Facing.IsFlipped;
    }
}
