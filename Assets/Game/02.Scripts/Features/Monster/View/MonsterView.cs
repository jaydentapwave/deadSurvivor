using UnityEngine;

// 몬스터 그림(풀에서 꺼낸다): 몸 = 아틀라스 그림(그림 넣기 기획서 E절, 배율 1) 또는 F1 임시 마름모(색·크기 = 몬스터 데이터) + 상태 표시 둘.
// 돌진 준비(Windup) 중 돌진 방향 반투명 선, 자폭 점화(Fuse) 중 폭발 반경 반투명 원(F4).
// 몸은 자식(Body)이라 이동 움직임(정수 픽셀 위아래)·예고 흔들림(정수 픽셀 좌우)·좌우 반전은 몸 자식만 바꾼다.
// 예고 그림이 있는 몬스터는 Windup·Fuse 동안 몸을 예고 그림으로 바꾼다(아트 기준서 3.10 돌진·자폭 예고). 다른 상태에서는 평소 그림.
public class MonsterView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
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
    private float _unitScale;
    private Sprite _bodySprite;
    private Sprite _warningSprite;

    public bool IsDashLineVisible => dashLineRenderer.enabled;
    public bool IsBlastAreaVisible => blastAreaRenderer.enabled;
    public float MinMovePixels => minMovePixels;
    public float FacingAxisRatio => facingAxisRatio;
    public float MovePeriodSeconds => movePeriodSeconds;
    public float StaggerSeconds => staggerSeconds;
    public float ShakePeriodSeconds => shakePeriodSeconds;
    public bool HasArt { get; private set; }
    // 예고 그림을 보이는 중
    public bool IsWarning { get; private set; }
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
        bodyRenderer.sprite = bodySprite;
        HasArt = hasArt;
        ApplyMotion(false, Vector2Int.zero);
        dashLineRenderer.sprite = lineSprite;
        blastAreaRenderer.sprite = areaSprite;
        _dashLength = dashLength;
        _unitScale = unitScale;

        // 바닥의 원 → 아이소 타원. 원 스프라이트 피벗이 아래라 반높이만큼 내려 중심을 발 위치에 맞춘다
        var areaSize = IsoProjection.FloorToWorld(new Vector2(blastRadius * 2f, blastRadius * 2f), unitScale);
        var spriteSize = areaSprite.bounds.size;
        var areaTransform = blastAreaRenderer.transform;
        areaTransform.localScale = new Vector3(areaSize.x / spriteSize.x, areaSize.y / spriteSize.y, 1f);
        areaTransform.localPosition = new Vector3(0f, -areaSize.y * 0.5f, 0f);
        UpdateState(MonsterPhase.Approach, Vector2.zero);
    }

    public void UpdateView(Vector2 worldPosition) => transform.localPosition = worldPosition;

    // 좌우 반전과 몸 오프셋(정수 픽셀)
    public void ApplyMotion(bool isFlipped, Vector2Int bodyOffsetPixels)
    {
        bodyRenderer.flipX = isFlipped;
        bodyRenderer.transform.localPosition = SpriteMotion.ToUnits(bodyOffsetPixels);
    }

    // dashDirection: 바닥 평면 단위 벡터(Windup일 때만 쓴다)
    public void UpdateState(MonsterPhase phase, Vector2 dashDirection)
    {
        var isWarning = _warningSprite != null && (phase == MonsterPhase.Windup || phase == MonsterPhase.Fuse);
        if (isWarning != IsWarning)
        {
            IsWarning = isWarning;
            bodyRenderer.sprite = isWarning ? _warningSprite : _bodySprite;
        }

        var isWindup = phase == MonsterPhase.Windup;
        dashLineRenderer.enabled = isWindup;
        blastAreaRenderer.enabled = phase == MonsterPhase.Fuse;
        if (!isWindup)
        {
            return;
        }

        var line = IsoProjection.FloorToWorld(dashDirection * _dashLength, _unitScale);
        var lineTransform = dashLineRenderer.transform;
        lineTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg);
        lineTransform.localScale = new Vector3(line.magnitude / dashLineRenderer.sprite.bounds.size.x, 1f, 1f);
    }
}
