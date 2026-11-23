using UnityEngine;

// 몬스터 그림(풀에서 꺼낸다): 몸 = 아틀라스 그림(그림 넣기 기획서 E절, 배율 1) 또는 F1 임시 마름모(색·크기 = 몬스터 데이터) + 상태 표시 둘.
// 돌진 준비(Windup) 중 돌진 방향 반투명 선, 자폭 점화(Fuse) 중 폭발 반경 반투명 원(F4).
// 몸은 자식(Body)이라 이동 움직임(정수 픽셀 위아래)·예고 흔들림(정수 픽셀 좌우)·좌우 반전은 몸 자식만 바꾼다.
// 예고 그림이 있는 몬스터는 Windup·Fuse 동안 몸을 예고 그림으로 바꾼다(아트 기준서 3.10 돌진·자폭 예고). 다른 상태에서는 평소 그림.
public class MonsterView : MonoBehaviour
{
    private float _dqDashRadius;
    private float _dqWindupSeconds;
    public bool HasDQDash => _dqDashRadius > 0 && _effectSpriteCache.HasDQTelegraph;

    public void InitializeDash(float hitRadius, float windupSeconds)
    {
        _dqDashRadius = hitRadius;
        _dqWindupSeconds = windupSeconds;
    }

    private void OnDisable()
    {
        // A pooled prefab can be disabled before InitializeEffects has supplied its optional cache.
        if (_effectSpriteCache != null)
        {
            _effectSpriteCache.ReleaseDQDash(GetInstanceID());
        }
    }
    public void ApplyProjection(PerspectiveFrame frame, TerrainHeightGrid terrain = null)
    {
        var upright = bodyRenderer.transform.parent;
        var pivot = bodyRenderer.sprite.pivot;
        if (bodyRenderer.flipX) pivot.x = bodyRenderer.sprite.rect.width - pivot.x;
        upright.position = frame.SnapCard(transform.position, pivot, bodyRenderer.sprite.pixelsPerUnit, terrain);
    }

    private DQActorClips _dq;
    private MonsterPhase _dqPhase = MonsterPhase.Approach;
    private float _dqPhaseClock;
    private float _dqMoveClock;
    private string _dqShotAction;
    private float _dqShotClock;
    private bool _dqJustHit;
    private bool _dqDead;
    private int _dqLastFrame = -1;
    private int _dqFinalShownFrame = -1;
    public int DQFrame { get; private set; }
    public string DQAction { get; private set; }
    public float DQPhaseClock => _dqPhaseClock;
    public bool HasDQ => _dq != null;
    public string DQActorId => _dq?.Data.actorId;
    public bool DQFallFinished => _dqDead && _dqFinalShownFrame >= 0 && Time.frameCount > _dqFinalShownFrame;

    public void InitializeDQ(DQActorClips actor)
    {
        _dq = actor;
        _dqPhase = MonsterPhase.Approach;
        _dqPhaseClock = 0;
        _dqMoveClock = 0;
        _dqShotAction = null;
        _dqShotClock = 0;
        _dqJustHit = false;
        _dqDead = false;
        _dqLastFrame = -1;
        _dqFinalShownFrame = -1;
        var sprites = new Sprite[5];
        for (var i = 0; i < 5; i++)
        {
            sprites[i] = actor.Static((FacingDirection)i);
        }
        InitializeDirections(sprites, sprites);
        FacingFallbackCount = 0;
    }

    public void BeginDQAttack(SpriteFacing facing)
    {
        if (_dq == null || _dqDead)
        {
            return;
        }
        Facing = facing;
        _dqShotAction = _dq.Data.actorId == "mushroom" ? "shoot" : "contact";
        var clip = _dq.Clip(_dqShotAction, facing.Direction);
        if (clip == null)
        {
            _dqShotAction = null;
            return;
        }
        _dqShotClock = 0;
        for (var i = 0; i < clip.eventFrame; i++)
        {
            _dqShotClock += clip.durationsMs[i] / 1000f;
        }
        _dqJustHit = true;
    }

    public void BeginDQFall()
    {
        _dqDead = true;
        _dqPhaseClock = 0;
        _dqMoveClock = 0;
        _dqShotAction = null;
        dashLineRenderer.enabled = false;
        blastAreaRenderer.enabled = false;
        if (_dashDecal != null)
        {
            _dashDecal.SetVisible(false);
        }
        if (_blastDecal != null)
        {
            _blastDecal.SetVisible(false);
        }
        if (_effectSpriteCache != null)
        {
            _effectSpriteCache.ReleaseDQDash(GetInstanceID());
        }
    }

    public void AcknowledgeDQRender()
    {
        if (_dqDead && _dqPhaseClock >= _dq.Clip("fall", Facing.Direction).Seconds && _dqFinalShownFrame < 0)
        {
            _dqFinalShownFrame = Time.frameCount;
        }
    }

    public void ApplyDQAnimation(float delta, bool isMoving, float moveSeconds = 0, float phaseSeconds = -1)
    {
        if (_dq == null)
        {
            return;
        }
        if (phaseSeconds >= 0 && !_dqDead)
        {
            _dqPhaseClock = phaseSeconds;
        }
        if (_dqLastFrame != Time.frameCount)
        {
            _dqLastFrame = Time.frameCount;
            if (phaseSeconds < 0 || _dqDead)
            {
                _dqPhaseClock += delta;
            }
            if (isMoving)
            {
                _dqMoveClock += moveSeconds;
            }
            else
            {
                _dqMoveClock = 0;
            }
            if (!_dqJustHit)
            {
                _dqShotClock += delta;
            }
        }
        _dqJustHit = false;
        var shot = _dqShotAction == null ? null : _dq.Clip(_dqShotAction, Facing.Direction);
        if (shot != null && _dqShotClock >= shot.Seconds)
        {
            _dqShotAction = null;
            shot = null;
        }
        DQAction = _dqDead ? "fall" : shot != null ? _dqShotAction
            : _dqPhase == MonsterPhase.Approach || _dqPhase == MonsterPhase.Swarm
                ? isMoving ? _dq.PhaseAction(_dqPhase) : "static" : _dq.PhaseAction(_dqPhase);
        if (DQAction == "static")
        {
            DQSpriteLighting.Apply(bodyRenderer, _dq.Static(Facing.Direction), ref _dqLightingProperties);
            DQFrame = 0;
        }
        else
        {
            var clip = _dq.Clip(DQAction, Facing.Direction);
            var clock = shot != null && !_dqDead ? _dqShotClock
                : !_dqDead && (DQAction == "move" || DQAction == "walk") ? _dqMoveClock : _dqPhaseClock;
            DQSpriteLighting.Apply(bodyRenderer, _dq.At(DQAction, Facing, clock, out var frame), ref _dqLightingProperties);
            DQFrame = frame;
        }
        bodyRenderer.flipX = Facing.IsFlipped;
        bodyRenderer.transform.localPosition = Vector3.zero;
        UpdateShadowWidth();
    }

    private float _appearanceElapsed = SpawnAppearanceMath.Seconds;
    private bool _appearanceJustStarted;
    private MaterialPropertyBlock _appearanceBlock;
    public float Appearance => SpawnAppearanceMath.Fraction(_appearanceElapsed);
    public float AppearanceElapsed => _appearanceElapsed;
    public void BeginAppearance(bool isInside)
    {
        _appearanceElapsed = isInside ? 0 : SpawnAppearanceMath.Seconds;
        _appearanceJustStarted = true;
    }
    public void AdvanceAppearance(float delta)
    {
        if (!_appearanceJustStarted)
        {
            _appearanceElapsed += delta;
        }
        _appearanceJustStarted = false;
        _appearanceBlock ??= new MaterialPropertyBlock();
        bodyRenderer.GetPropertyBlock(_appearanceBlock);
        _appearanceBlock.SetFloat("_Appearance", Appearance);
        bodyRenderer.SetPropertyBlock(_appearanceBlock);
    }

    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Material shadowMaterial;
    [SerializeField] private Material terrainWarningMaterial;
    private TerrainHeightGrid _terrain = TerrainHeightGrid.Flat;
    private ConformedDecal _dashDecal;
    private ConformedDecal _blastDecal;
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
    [SerializeField] private Material dqBlobShadowMaterial;
    private DQBlobShadow _dqBlobShadow;
    private MaterialPropertyBlock _dqLightingProperties;
    private FootShadowView _shadow;
    private Sprite _shadowSprite;
    public float ShadowWidthPixels => _dqBlobShadow != null ? _dqBlobShadow.Width * 48 : _shadow == null ? 0f : _shadow.WidthPixels;
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


    public bool IsDashLineVisible => _terrain.IsFlat ? dashLineRenderer.enabled : _dashDecal != null && _dashDecal.ActiveRenderer.enabled;
    public bool IsBlastAreaVisible => _terrain.IsFlat ? blastAreaRenderer.enabled : _blastDecal != null && _blastDecal.ActiveRenderer.enabled;
    private IEffectFrameSource _dashFrameSource;
    private IEffectFrameSource _blastFrameSource;
    public IEffectFrameSource DashFrameSource => _dashFrameSource ??= new WarningFrameSource(this, true);
    public IEffectFrameSource BlastFrameSource => _blastFrameSource ??= new WarningFrameSource(this, false);

    // Each warning has its own sprite/frame and its own active flat or conformed renderer.
    private sealed class WarningFrameSource : IEffectFrameSource
    {
        private readonly MonsterView _owner;
        private readonly bool _isDash;
        public WarningFrameSource(MonsterView owner, bool isDash) { _owner = owner; _isDash = isDash; }
        private SpriteRenderer Source => _isDash ? _owner.dashLineRenderer : _owner.blastAreaRenderer;
        public Renderer ActiveRenderer => _owner._terrain.IsFlat ? Source : (_isDash ? _owner._dashDecal : _owner._blastDecal)?.ActiveRenderer;
        public Sprite SourceSprite => Source.sprite;
        public float Alpha => Source.color.a;
        public bool IsShown => _owner.gameObject.activeInHierarchy && ActiveRenderer != null && ActiveRenderer.enabled;
        public int FrameNumber
        {
            get
            {
                if (SourceSprite == null) { return 0; }
                var name = SourceSprite.name.Replace("(Clone)", string.Empty);
                return int.TryParse(name.Substring(name.LastIndexOf('_') + 1), out var frame) ? frame : 1;
            }
        }
    }

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
    public SpriteRenderer BodyRenderer => bodyRenderer;
    public bool IsFlipped => bodyRenderer.flipX;
    public Vector3 BodyOffset => bodyRenderer.transform.localPosition;

    // hasArt = 몸이 아틀라스 그림(아니면 F1 마름모). warningSprite = 예고 그림(없는 몬스터는 null). 풀에서 다시 꺼내도 반전 없음·오프셋 0·평소 그림으로 시작한다.
    // dashLength·blastRadius는 바닥 단위(0이면 그 표시를 쓰지 않는 몬스터). lineSprite 피벗 = 왼쪽 가운데, areaSprite 피벗 = 아래 가운데
    public void Initialize(Sprite bodySprite, Sprite warningSprite, bool hasArt, Sprite lineSprite, Sprite areaSprite, float dashLength,
        float blastRadius, float unitScale)
    {
        _dq = null;
        BeginAppearance(false);
        AdvanceAppearance(0);
        _bodySprite = bodySprite;
        _warningSprite = warningSprite;
        IsWarning = false;
        Facing = SpriteFacing.Front;
        _directionSprites = new[] { null, bodySprite, null, null, null };
        _warningSprites = new[] { null, warningSprite, null, null, null };
        _spriteMask = 1 << (int)FacingDirection.DownRight;
        _warningMask = warningSprite == null ? 0 : _spriteMask;
        DQSpriteLighting.Apply(bodyRenderer, bodySprite, ref _dqLightingProperties);
        HasArt = hasArt;
        ApplyMotion(false, Vector2Int.zero);
        dashLineRenderer.sprite = lineSprite;
        blastAreaRenderer.sprite = areaSprite;
        _dashLength = dashLength;
        _dqDashRadius = 0;
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
        if (dqBlobShadowMaterial != null)
        {
            _dqBlobShadow = GetComponent<DQBlobShadow>() ?? gameObject.AddComponent<DQBlobShadow>();
            _dqBlobShadow.Initialize(bodyRenderer, dqBlobShadowMaterial, bodyWidthPixels / bodyRenderer.sprite.pixelsPerUnit);
            return;
        }
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

    public void SetTerrain(TerrainHeightGrid terrain, Vector3 normal, Material groundShadow)
    {
        _terrain = terrain;
        if (_dqBlobShadow != null)
        {
            _dqBlobShadow.SetGround(terrain, groundShadow);
            return;
        }
        if (!terrain.IsFlat)
        {
            _shadow.SetGround(normal, groundShadow);
        }
    }

    private void UpdateDecal(ref ConformedDecal decal, SpriteRenderer source, bool isVisible)
    {
        source.enabled = false;
        if (!isVisible)
        {
            if (decal != null)
            {
                decal.SetVisible(false);
            }
            return;
        }
        if (decal == null)
        {
            var child = new GameObject("Terrain " + source.name);
            child.transform.SetParent(transform, false);
            decal = child.AddComponent<ConformedDecal>();
        }
        decal.UpdateView(_terrain, transform.position, source.sprite, terrainWarningMaterial, new Vector2(1, 2));
    }

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
        if (_dq != null)
        {
            return;
        }
        var mask = IsWarning ? _warningMask : _spriteMask;
        var sprites = IsWarning ? _warningSprites : _directionSprites;
        DQSpriteLighting.Apply(bodyRenderer, sprites[(int)SpriteFacing.ResolveAvailable(Facing.Direction, mask)], ref _dqLightingProperties);
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
    public void UpdateState(MonsterPhase phase, Vector2 dashDirection, float phaseSeconds = -1)
    {
        var wasWindup = _lastPhase == MonsterPhase.Windup;
        if (_dqDead)
        {
            return;
        }
        if (_dq != null && phase != _dqPhase)
        {
            _dqPhase = phase;
            _dqPhaseClock = 0;
        }
        if (_dq != null && phaseSeconds >= 0)
        {
            _dqPhaseClock = phaseSeconds;
        }
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
        var elapsed = _dq != null ? _dqPhaseClock : Time.time - _warningStarted;
        if (phase == MonsterPhase.Fuse && _blastRadius > 0)
        {
            blastAreaRenderer.sprite = _effectSpriteCache.Ring("blast", _blastRadius * _unitScale, EffectFrameMath.FrameAt(elapsed, 2, 8, true));
        }
        if (isWindup && _dashLength > 0)
        {
            var angle = Mathf.Atan2(_dashDirection.y, _dashDirection.x) * Mathf.Rad2Deg;
            dashLineRenderer.sprite = HasDQDash
                ? _effectSpriteCache.DQDash(_dashLength * _unitScale, _dqDashRadius * _unitScale, angle, DQTelegraphTiles.StageAt(elapsed, _dqWindupSeconds), GetInstanceID())
                : _effectSpriteCache.Dash(_dashLength * _unitScale, angle, EffectFrameMath.FrameAt(elapsed, 3, 12, true));
        }
        if (!_terrain.IsFlat)
        {
            UpdateDecal(ref _dashDecal, dashLineRenderer, isWindup);
            UpdateDecal(ref _blastDecal, blastAreaRenderer, phase == MonsterPhase.Fuse);
        }
        if (wasWindup && !isWindup)
        {
            _effectSpriteCache.ReleaseDQDash(GetInstanceID());
        }
    }
}
