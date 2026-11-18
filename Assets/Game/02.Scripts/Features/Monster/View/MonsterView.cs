using UnityEngine;

// 몬스터 그림(풀에서 꺼낸다): 몸 = 아틀라스 그림(그림 넣기 기획서 E절, 배율 1) 또는 F1 임시 마름모(색·크기 = 몬스터 데이터) + 상태 표시 둘.
// 돌진 준비(Windup) 중 돌진 방향 반투명 선, 자폭 점화(Fuse) 중 폭발 반경 반투명 원(F4).
// 몸은 자식(Body)이라 이동 움직임(정수 픽셀 위아래)·예고 흔들림(정수 픽셀 좌우)·좌우 반전은 몸 자식만 바꾼다.
// 예고 그림이 있는 몬스터는 Windup·Fuse 동안 몸을 예고 그림으로 바꾼다(아트 기준서 3.10 돌진·자폭 예고). 다른 상태에서는 평소 그림.
public class MonsterView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Material shadowMaterial;
    [SerializeField] private SpriteRenderer dashLineRenderer;
    [SerializeField] private SpriteRenderer blastAreaRenderer;
    // 방향·이동 움직임 값(기획서 E절, 가정 AI2·AI4)
    [SerializeField] private float minMovePixels = 0.25f;
    [SerializeField] private float facingAxisRatio = 0.25f;
    [SerializeField] private float movePeriodSeconds = 0.4f;
    [SerializeField] private float staggerSeconds = 0.37f;
    // 예고 흔들림 한 주기(앞 절반 0px, 뒤 절반 +1px)
    [SerializeField] private float shakePeriodSeconds = 0.1f;

    private float _dashLength;
    private float _blastRadius;
    private EffectSpriteCache _effectSpriteCache;
    private MonsterPhase _lastPhase;
    private float _warningStarted;
    private Vector2 _dashDirection;
    private float _unitScale;
    private Sprite _bodySprite;
    private Sprite _warningSprite;
    private FootShadowView _shadow;
    private Sprite _shadowSprite;
    public float ShadowWidthPixels => _shadow == null ? 0f : _shadow.WidthPixels;
    private Sprite[] _directionSprites;
    private Sprite[] _warningSprites;
    private int _spriteMask;
    private int _warningMask;
    public SpriteFacing Facing { get; private set; } = SpriteFacing.Front;
    public string WarningSpriteName => _warningSprite.name;
    public int FacingFallbackCount
    {
        get; private set;
    }


    public bool IsDashLineVisible => dashLineRenderer.enabled;
    public bool IsBlastAreaVisible => blastAreaRenderer.enabled;
    public float MinMovePixels => minMovePixels;
    public float FacingAxisRatio => facingAxisRatio;
    public float MovePeriodSeconds => movePeriodSeconds;
    public float StaggerSeconds => staggerSeconds;
    public float ShakePeriodSeconds => shakePeriodSeconds;
    public bool HasArt
    {
        get; private set;
    }
    // 예고 그림을 보이는 중
    public bool IsWarning
    {
        get; private set;
    }
    public string BodySpriteName => bodyRenderer.sprite.name;
    public bool IsFlipped => bodyRenderer.flipX;
    public Vector3 BodyOffset => bodyRenderer.transform.localPosition;

    // hasArt = 몸이 아틀라스 그림(아니면 F1 마름모). warningSprite = 예고 그림(없는 몬스터는 null). 풀에서 다시 꺼내도 반전 없음·오프셋 0·평소 그림으로 시작한다.
    // dashLength·blastRadius는 바닥 단위(0이면 그 표시를 쓰지 않는 몬스터). lineSprite 피벗 = 왼쪽 가운데, areaSprite 피벗 = 아래 가운데
    public void Initialize(Sprite bodySprite, Sprite warningSprite, bool hasArt, Sprite lineSprite, Sprite areaSprite, float dashLength,
        float blastRadius, float unitScale)
    {
        _bodySprite = bodySprite;
        _warningSprite = warningSprite;
        IsWarning = false;
        Facing = SpriteFacing.Front;
        _directionSprites = new[] { null, bodySprite, null, null, null };
        _warningSprites = new[] { null, warningSprite, null, null, null };
        _spriteMask = 1 << (int)FacingDirection.DownRight;
        _warningMask = warningSprite == null ? 0 : _spriteMask;
        bodyRenderer.sprite = bodySprite;
        HasArt = hasArt;
        ApplyMotion(false, Vector2Int.zero);
        dashLineRenderer.sprite = lineSprite;
        blastAreaRenderer.sprite = areaSprite;
        _dashLength = dashLength;
        _unitScale = unitScale;

        _blastRadius = blastRadius;
        _lastPhase = MonsterPhase.Approach;
        foreach (var renderer in new[] { dashLineRenderer, blastAreaRenderer })
        {
            renderer.transform.localRotation = Quaternion.Euler(90, 0, 0);
            renderer.transform.localScale = new Vector3(1, 2, 1);
            renderer.transform.localPosition = new Vector3(0, .001f, 0);
        }
        UpdateState(MonsterPhase.Approach, Vector2.zero);
    }

    public void InitializeEffects(EffectSpriteCache cache)
    {
        _effectSpriteCache = cache;
        if (_blastRadius > 0)
        {
            blastAreaRenderer.sprite = cache.Ring("blast", _blastRadius * _unitScale, 0);
        }
    }

    public void InitializeShadow(Sprite sprite, float bodyWidthPixels)
    {
        if (_shadow == null)
        {
            var child = new GameObject("Shadow");
            child.transform.SetParent(transform, false);
            child.layer = HD2DBackgroundRig.BackgroundLayer;
            _shadow = child.AddComponent<FootShadowView>();
        }

        _shadow.Initialize(sprite, bodyWidthPixels, bodyRenderer.sortingLayerID, shadowMaterial);
        _shadowSprite = null;
        UpdateShadowWidth();
    }

    public void UpdateView(Vector3 worldPosition) => transform.localPosition = worldPosition;

    public void InitializeDirections(Sprite[] bodySprites, Sprite[] warningSprites)
    {
        _directionSprites = bodySprites;
        _warningSprites = warningSprites;
        _spriteMask = 0;
        _warningMask = 0;
        FacingFallbackCount = 0;
        for (var i = 0; i < 5; i++)
        {
            if (bodySprites[i] != null)
            {
                _spriteMask |= 1 << i;
            }
            if (warningSprites[i] != null)
            {
                _warningMask |= 1 << i;
            }
            if (bodySprites[i] == null || (_warningSprite != null && warningSprites[i] == null))
            {
                FacingFallbackCount++;
            }
        }
        ApplySprite();
    }

    public void ApplyMotion(bool isFlipped, Vector2Int bodyOffsetPixels) =>
        ApplyMotion(new SpriteFacing(FacingDirection.DownRight, isFlipped), bodyOffsetPixels);

    public void ApplyMotion(SpriteFacing facing, Vector2Int bodyOffsetPixels)
    {
        Facing = facing;
        ApplySprite();
        bodyRenderer.flipX = facing.IsFlipped;
        bodyRenderer.transform.localPosition = SpriteMotion.ToUnits(bodyOffsetPixels);
    }

    private void ApplySprite()
    {
        var mask = IsWarning ? _warningMask : _spriteMask;
        var sprites = IsWarning ? _warningSprites : _directionSprites;
        bodyRenderer.sprite = sprites[(int)SpriteFacing.ResolveAvailable(Facing.Direction, mask)];
        UpdateShadowWidth();
    }

    private void UpdateShadowWidth()
    {
        // Shadow is attached after the body is initialized by the factory.
        if (_shadow == null || _shadowSprite == bodyRenderer.sprite)
        {
            return;
        }
        _shadowSprite = bodyRenderer.sprite;
        var vertices = _shadowSprite.vertices;
        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        foreach (var vertex in vertices)
        {
            min = Mathf.Min(min, vertex.x);
            max = Mathf.Max(max, vertex.x);
        }
        _shadow.SetWidth((max - min) * _shadowSprite.pixelsPerUnit);
    }

    // dashDirection: 바닥 평면 단위 벡터(Windup일 때만 쓴다)
    public void UpdateState(MonsterPhase phase, Vector2 dashDirection)
    {
        var isWarning = _warningSprite != null && (phase == MonsterPhase.Windup || phase == MonsterPhase.Fuse);
        if (isWarning != IsWarning)
        {
            IsWarning = isWarning;
            ApplySprite();
        }

        var isWindup = phase == MonsterPhase.Windup;
        if (phase != _lastPhase)
        {
            _warningStarted = Time.time;
            _lastPhase = phase;
            if (isWindup)
            {
                _dashDirection = dashDirection;
            }
        }
        dashLineRenderer.enabled = isWindup;
        blastAreaRenderer.enabled = phase == MonsterPhase.Fuse;
        var elapsed = Time.time - _warningStarted;
        if (phase == MonsterPhase.Fuse && _blastRadius > 0)
        {
            blastAreaRenderer.sprite = _effectSpriteCache.Ring("blast", _blastRadius * _unitScale, EffectFrameMath.FrameAt(elapsed, 2, 8, true));
        }
        if (isWindup && _dashLength > 0)
        {
            var angle = Mathf.Atan2(_dashDirection.y, _dashDirection.x) * Mathf.Rad2Deg;
            dashLineRenderer.sprite = _effectSpriteCache.Dash(_dashLength * _unitScale, angle, EffectFrameMath.FrameAt(elapsed, 3, 12, true));
        }
    }
}
