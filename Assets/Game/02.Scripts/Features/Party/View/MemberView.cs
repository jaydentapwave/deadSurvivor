using UnityEngine;

// 원정대원 그림(그림 넣기 기획서 E절) + 머리 위 HP 막대. 그림이 없으면 F1 임시 도형(원).
// 쓰러지면 쓰러진 그림(좌우 반전은 쓰러질 때 방향)으로 바꾸고 downedDimSeconds 동안 downedColor까지 어두워진다(아트 기준서 3.10). 부활하면 방향 그림으로 돌아온다.
// 몸 그림은 자식(Body)이라 대기·피격 오프셋은 몸 자식만 옮기고 HP 막대는 따라가지 않는다. 번쩍임은 몸 자식의 자식(같은 스프라이트·flipX, 흰 재질).
public class MemberView : MonoBehaviour
{
    private DQAttackFxLibrary _dqFx;
    private SpriteRenderer _dqFxRenderer;
    public SpriteRenderer DQAttackFxRenderer => _dqFxRenderer;
    public void ApplyProjection(PerspectiveFrame frame, TerrainHeightGrid terrain = null)
    {
        var upright = bodyRenderer.transform.parent;
        var pivot = bodyRenderer.sprite.pivot;
        if (bodyRenderer.flipX) pivot.x = bodyRenderer.sprite.rect.width - pivot.x;
        upright.position = frame.SnapCard(transform.position, pivot, bodyRenderer.sprite.pixelsPerUnit, terrain);
    }

    [SerializeField] private SpriteRenderer bodyRenderer;
    public SpriteRenderer BodyRenderer => bodyRenderer;
    [SerializeField] private Material shadowMaterial;
    [SerializeField] private Material shadowCasterMaterial;
    [SerializeField] private UprightShadowCaster shadowCaster;
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
    private static readonly Color HpBackColor = Color.white;
    private static readonly Color HpFillColor = new(86f / 255, 135f / 255, 87f / 255); // Art standard 3.13 B
    // 쓰러짐(아트 기준서 3.10 사망: 0.3초 어둡게)
    [SerializeField] private float downedDimSeconds = 0.3f;
    [SerializeField] private Color downedColor = new(0.45f, 0.45f, 0.45f, 1f);
    // 대기·피격·방향 값(기획서 E절, 아트 기준서 3.10, 가정 AI2·AI4)
    [SerializeField] private float idlePeriodSeconds = 0.8f;
    [SerializeField] private float idleStaggerSeconds = 0.2f;
    [SerializeField] private float hitFlashSeconds = 0.1f;
    [SerializeField] private float minMovePixels = 0.25f;
    [SerializeField] private float facingAxisRatio = 0.25f;

    private DQActorClips _dq;
    private Sprite _dqHitSprite;
    private float _dqIdleClock;
    private float _dqFallClock;
    private bool _dqWasMoving;
    public bool HasDQ => _dq != null;
    public string DQActorId => _dq?.Data.actorId;
    public int DQFrame { get; private set; }
    public string DQAction { get; private set; }
    public long DQHitAttackId { get; private set; }

    public void InitializeDQ(DQActorClips actor, DQAttackFxLibrary attackFx)
    {
        _dq = actor;
        _dqFx = attackFx;
        if (_dqFx != null)
        {
            if (_dqFxRenderer == null)
            {
                var child = new GameObject("DQ attack cue", typeof(SpriteRenderer));
                child.layer = bodyRenderer.gameObject.layer;
                child.transform.SetParent(bodyRenderer.transform, false);
                _dqFxRenderer = child.GetComponent<SpriteRenderer>();
                _dqFxRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _dqFxRenderer.receiveShadows = false;
            }
            _dqFxRenderer.sharedMaterial = actor.Data.actorId == "bruno" || actor.Data.actorId == "lena"
                ? _dqFx.bladeMaterial : _dqFx.magicMaterial;
            _dqFxRenderer.enabled = false;
        }
        if (dqLightProfiles != null)
        {
            bodyRenderer.sharedMaterial = dqLightProfiles.Find(actor.Data.actorId);
        }
        _dqHitSprite = null;
        _dqIdleClock = 0;
        _dqFallClock = 0;
        _dqWasMoving = false;
        _downedSprite = actor.Lying;
        var directions = new Sprite[5];
        for (var i = 0; i < 5; i++)
        {
            directions[i] = actor.Static((FacingDirection)i);
            foreach (var action in new[] { "walk", "idle", "attack" })
            {
                if (actor.Clip(action, (FacingDirection)i) == null)
                {
                    throw new System.InvalidOperationException("Missing DQ member direction: " + actor.Data.actorId);
                }
            }
        }
        InitializeDirections(directions);
        // Adopted DQ body height 56px. The existing bar size/colour remains unchanged.
        var bar = hpBackRenderer.transform.localPosition;
        bar.y = (56 + hpBarGapPixels) / (float)Const.PixelsPerUnit;
        hpBackRenderer.transform.localPosition = bar;
        hpFillRenderer.transform.localPosition = bar;
        FacingFallbackCount = 0;
    }

    public void ResetDQVisualState()
    {
        _dqHitSprite = null;
        _motionSprite = null;
        _dqIdleClock = _dqFallClock = 0;
        _dqWasMoving = false;
        DQAction = null;
        DQFrame = 0;
        DQHitAttackId = 0;
        if (_dqFxRenderer != null)
        {
            _dqFxRenderer.enabled = false;
        }
    }

    public void AdvanceDQClock(float delta)
    {
        _dqIdleClock += delta;
        _dqFallClock = IsDowned ? _dqFallClock + delta : 0;
    }

    public void ApplyDQHit(DQHitRecord hit)
    {
        if (_dq == null || IsDowned)
        {
            return;
        }
        _dqHitSprite = null;
        Facing = hit.Facing;
        var clip = _dq.Clip("attack", Facing.Direction);
        DQAction = "attack-hit";
        DQFrame = clip.eventFrame;
        DQHitAttackId = hit.AttackId;
        _motionSprite = _dq.Frame(clip, DQFrame);
        ApplySprite();
    }

    private Sprite _downedSprite;
    private Sprite _motionSprite;
    private Sprite[] _directionSprites;
    private Sprite[][] _walkFrameTable;
    private Sprite[][] _attackFrameTable;
    private int _spriteMask;
    private int _walkMask;
    private int _attackMask;
    public int FacingFallbackCount
    {
        get; private set;
    }
    private MemberMotionData _motionData;
    [SerializeField] private Material dqBlobShadowMaterial;
    [SerializeField] private DQCharacterLightProfiles dqLightProfiles;
    private DQBlobShadow _dqBlobShadow;
    private MaterialPropertyBlock _dqLightingProperties;
    private UprightContactShadow _contactShadow;

    public int BodyDiameterPixels => bodyDiameterPixels;
    public Vector2Int HpBarPixels => hpBarPixels;
    public HpLossBarView HpLossBarView => hpLossBarView;
    public float IdlePeriodSeconds => idlePeriodSeconds;
    public float IdleStaggerSeconds => idleStaggerSeconds;
    public float HitFlashSeconds => hitFlashSeconds;
    public float MinMovePixels => minMovePixels;
    public float FacingAxisRatio => facingAxisRatio;
    public bool HasArt
    {
        get; private set;
    }
    public bool IsDowned
    {
        get; private set;
    }
    public string BodySpriteName => bodyRenderer.sprite.name;
    public Color BodyColor => bodyRenderer.color;
    public SpriteFacing Facing { get; private set; } = SpriteFacing.Front;
    public bool IsFlashOn => flashRenderer.enabled;
    public Vector3 BodyOffset => bodyRenderer.transform.localPosition;

    // 그림이 없으면 frontSprite = backSprite = downedSprite = 임시 원, hasArt = false. HP 틀·흰 채움: 피벗 왼쪽 가운데
    public void Initialize(Sprite frontSprite, Sprite backSprite, Sprite downedSprite, Sprite hpBarSprite, Sprite hpFillSprite, bool hasArt)
    {
        _motionSprite = null;
        _dq = null;
        _dqHitSprite = null;
        _dqIdleClock = _dqFallClock = 0;
        _dqWasMoving = false;
        DQAction = null;
        DQFrame = 0;
        DQHitAttackId = 0;
        _motionData = null;
        FacingFallbackCount = 0;
        _directionSprites = new[] { null, frontSprite, null, backSprite, null };
        _spriteMask = SpriteMask(_directionSprites);
        _downedSprite = downedSprite;
        HasArt = hasArt;
        IsDowned = false;
        bodyRenderer.color = Color.white;
        var bodyHeightPixels = hasArt ? artHeightPixels : bodyDiameterPixels;
        var barY = (bodyHeightPixels + hpBarGapPixels) / (float)Const.PixelsPerUnit;
        var barX = -hpBarPixels.x * 0.5f / Const.PixelsPerUnit;
        UiBarUtils.Initialize(hpBackRenderer, hpFillRenderer, hpBarSprite, hpFillSprite,
            (Vector2)hpBarPixels / Const.PixelsPerUnit, new Vector2(barX, barY), HpBackColor, HpFillColor);
        ApplyMotion(SpriteFacing.Front, Vector2Int.zero, false);
        shadowCaster.Initialize(bodyRenderer, shadowCasterMaterial, HD2DBackgroundRig.BackgroundLayer);
    }

    // downedSeconds: 쓰러진 뒤 흐른 시간(산 대원이면 쓰지 않는다)
    public void UpdateView(Vector3 worldPosition, float hpRatio, bool isAlive, float downedSeconds)
    {
        transform.localPosition = worldPosition;
        hpFillRenderer.transform.localScale = new Vector3(Mathf.Clamp01(hpRatio), 1f, 1f);
        hpBackRenderer.enabled = isAlive;
        hpFillRenderer.enabled = isAlive;
        bodyRenderer.color = isAlive ? Color.white : Color.Lerp(Color.white, downedColor, downedSeconds / downedDimSeconds);
        if (IsDowned == isAlive)
        {
            IsDowned = !isAlive;
            _dqFallClock = 0;
            _dqIdleClock = 0;
            _dqHitSprite = null;
            _motionSprite = null;
            ApplySprite();
        }
    }

    public void HideHealthBar()
    {
        hpBackRenderer.enabled = false;
        hpFillRenderer.enabled = false;
    }

    // 방향(앞·뒤 스프라이트, 좌우 반전)·몸 오프셋(정수 픽셀)·흰 번쩍임을 한 번에 적용
    public void ApplyMotion(SpriteFacing facing, Vector2Int bodyOffsetPixels, bool isFlashOn)
    {
        if (_dq != null)
        {
            if (isFlashOn && !flashRenderer.enabled)
            {
                _dqHitSprite = bodyRenderer.sprite;
            }
            else if (!isFlashOn)
            {
                _dqHitSprite = null;
            }
        }
        Facing = facing;
        ApplySprite();
        bodyRenderer.transform.localPosition = SpriteMotion.ToUnits(bodyOffsetPixels);
        flashRenderer.enabled = isFlashOn;
    }

    public void InitializeDirections(Sprite[] sprites)
    {
        _directionSprites = sprites;
        _spriteMask = SpriteMask(sprites);
        ApplySprite();
    }

    public void InitializeClips(MemberMotionData data, Sprite[] walkFront, Sprite[] walkBack, Sprite[] attackFront, Sprite[] attackBack)
    {
        InitializeClips(data, new[] { null, walkFront, null, walkBack, null },
            new[] { null, attackFront, null, attackBack, null });
    }

    public void InitializeClips(MemberMotionData data, Sprite[][] walkFrameTable, Sprite[][] attackFrameTable)
    {
        _motionData = data;
        _walkFrameTable = walkFrameTable;
        _attackFrameTable = attackFrameTable;
        _walkMask = ClipMask(walkFrameTable);
        _attackMask = ClipMask(attackFrameTable);
        FacingFallbackCount = 0;
        for (var i = 0; i < 5; i++)
        {
            var bit = 1 << i;
            if ((_spriteMask & bit) == 0 || (_walkMask & bit) == 0 || (_attackMask & bit) == 0)
            {
                FacingFallbackCount++;
            }
        }
    }

    public void SetGround(TerrainHeightGrid terrain, Vector3 normal, Material terrainShadow)
    {
        if (_dqBlobShadow != null)
        {
            _dqBlobShadow.SetGround(terrain, terrainShadow);
            return;
        }
        if (!terrain.IsFlat)
        {
            _contactShadow.SetGround(normal, terrainShadow);
        }
    }

    public void InitializeShadow(float widthPixels = 28)
    {
        if (dqBlobShadowMaterial != null)
        {
            _dqBlobShadow = GetComponent<DQBlobShadow>() ?? gameObject.AddComponent<DQBlobShadow>();
            _dqBlobShadow.Initialize(bodyRenderer, dqBlobShadowMaterial, widthPixels / bodyRenderer.sprite.pixelsPerUnit);
            shadowCaster.SetCastingShadow(false);
            return;
        }
        if (_contactShadow == null)
        {
            _contactShadow = gameObject.AddComponent<UprightContactShadow>();
        }
        _contactShadow.Initialize(bodyRenderer, shadowMaterial);
    }

    // 프레임 선택만 한다. 공격 시계·발동은 CombatService 소유.
    public void ApplyAnimation(bool isMoving, float walkClock, MemberAttackMotion attack)
    {
        _motionSprite = null;
        if (_dq != null)
        {
            DQAction = IsDowned ? "fall" : attack.IsPlaying ? "attack" : isMoving ? "walk" : "idle";
            if (isMoving != _dqWasMoving)
            {
                _dqIdleClock = 0;
                _dqWasMoving = isMoving;
            }
            var clip = _dq.Clip(DQAction, Facing.Direction);
            if (DQAction == "attack")
            {
                DQFrame = DQClipMath.AttackFrame(clip, attack.Elapsed, attack.HitSeconds, attack.HasHit);
            }
            else
            {
                DQFrame = DQClipMath.FrameAt(clip, IsDowned ? _dqFallClock : isMoving ? walkClock : _dqIdleClock);
            }
            _motionSprite = IsDowned && _dqFallClock >= clip.Seconds ? _dq.Lying : _dq.Frame(clip, DQFrame);
            ApplySprite();
            return;
        }
        if (!IsDowned && _motionData != null && HasArt)
        {
            if (attack.IsPlaying)
            {
                var direction = SpriteFacing.ResolveAvailable(attack.Facing.Direction, _attackMask);
                var frames = _attackFrameTable[(int)direction];
                _motionSprite = frames[attack.GetFrame(frames.Length)];
            }
            else if (isMoving)
            {
                var direction = SpriteFacing.ResolveAvailable(Facing.Direction, _walkMask);
                var frames = _walkFrameTable[(int)direction];
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
        var sprite = !IsDowned && _dqHitSprite != null ? _dqHitSprite : _dq != null && _motionSprite != null ? _motionSprite : IsDowned ? _downedSprite : _motionSprite != null ? _motionSprite : _directionSprites[(int)SpriteFacing.ResolveAvailable(Facing.Direction, _spriteMask)];
        DQSpriteLighting.Apply(bodyRenderer, sprite, ref _dqLightingProperties);
        bodyRenderer.flipX = Facing.IsFlipped;
        if (_dqFxRenderer != null)
        {
            var cue = _dq != null && _dqFx != null && !IsDowned && (DQAction == "attack" || DQAction == "attack-hit")
                ? _dqFx.GetFrame(_dq.Data.actorId, Facing.Direction, DQFrame) : null;
            _dqFxRenderer.sprite = cue;
            _dqFxRenderer.flipX = Facing.IsFlipped;
            _dqFxRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            _dqFxRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
            _dqFxRenderer.enabled = cue != null;
        }
        flashRenderer.sprite = sprite;
        flashRenderer.flipX = Facing.IsFlipped;
    }
    private static int SpriteMask(Sprite[] sprites)
    {
        var mask = 0;
        for (var i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                mask |= 1 << i;
            }
        }
        return mask;
    }

    private static int ClipMask(Sprite[][] frames)
    {
        var mask = 0;
        for (var i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null && frames[i].Length > 0)
            {
                mask |= 1 << i;
            }
        }
        return mask;
    }

}
