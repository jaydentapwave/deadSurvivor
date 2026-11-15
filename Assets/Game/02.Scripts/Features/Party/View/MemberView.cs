using UnityEngine;

// 원정대원 임시 도형(원) + 머리 위 HP 막대. 크기·색은 그림 단계가 바꿀 임시 값이라 장면에 둔다.
public class MemberView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer hpBackRenderer;
    [SerializeField] private SpriteRenderer hpFillRenderer;
    [SerializeField] private int bodyDiameterPixels = 14;
    [SerializeField] private Vector2Int hpBarPixels = new(14, 2);
    [SerializeField] private int hpBarGapPixels = 2;
    [SerializeField] private Color hpBackColor = new(0.15f, 0.08f, 0.05f, 1f);
    [SerializeField] private Color hpFillColor = new(0.36f, 0.82f, 0.36f, 1f);
    [SerializeField] private float downedAlpha = 0.35f;

    public int BodyDiameterPixels => bodyDiameterPixels;
    public Vector2Int HpBarPixels => hpBarPixels;

    // hpBarSprite: 피벗이 왼쪽 가운데인 흰 상자
    public void Initialize(Sprite bodySprite, Sprite hpBarSprite)
    {
        bodyRenderer.sprite = bodySprite;
        hpBackRenderer.sprite = hpBarSprite;
        hpFillRenderer.sprite = hpBarSprite;
        hpBackRenderer.color = hpBackColor;
        hpFillRenderer.color = hpFillColor;
        var barY = (bodyDiameterPixels + hpBarGapPixels) / (float)Const.PixelsPerUnit;
        var barX = -hpBarPixels.x * 0.5f / Const.PixelsPerUnit;
        hpBackRenderer.transform.localPosition = new Vector3(barX, barY, 0f);
        hpFillRenderer.transform.localPosition = new Vector3(barX, barY, 0f);
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

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);
}
