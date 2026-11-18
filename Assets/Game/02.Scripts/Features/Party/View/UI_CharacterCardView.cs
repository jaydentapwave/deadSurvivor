using UnityEngine;
using UnityEngine.UI;

// 로비 캐릭터 카드(V1): 초상(앞 그림에서 머리~허리를 자른 창, 그림 넣기 기획서 F절) 또는 F1 임시 도형 색, 이름, 직업, HP, 공격 배수, 방어,
// 시작 아이템 이름
public class UI_CharacterCardView : MonoBehaviour
{
    [SerializeField] private Image colorImage;
    // 초상 칸(카드 세로 배치의 한 줄) → 가운데 창(RectMask2D) → 앞 그림
    [SerializeField] private LayoutElement portraitSlot;
    [SerializeField] private RectTransform portraitWindow;
    [SerializeField] private Image portraitImage;
    // 보일 영역(그림 픽셀, 위 왼쪽 원점): x 14~50, y 16~52 = 머리~허리 36×36(가정 AI6)
    [SerializeField] private RectInt portraitArea = new(14, 16, 36, 36);
    [SerializeField] private Text nameText;
    [SerializeField] private Text roleText;
    [SerializeField] private Text hpText;
    [SerializeField] private Text atkText;
    [SerializeField] private Text defText;
    [SerializeField] private Text itemText;

    public bool HasPortrait => portraitSlot.gameObject.activeSelf && portraitImage.sprite != null;

    // portraitSprite가 없으면(키가 비거나 그림이 없음) F1 색 칸
    public void UpdateUI(string characterName, string role, int hp, float atk, float def, string startItemName, Color color,
        Sprite portraitSprite)
    {
        var hasPortrait = portraitSprite != null;
        colorImage.gameObject.SetActive(!hasPortrait);
        portraitSlot.gameObject.SetActive(hasPortrait);
        colorImage.color = color;
        if (hasPortrait)
        {
            UpdatePortrait(portraitSprite);
        }

        nameText.text = characterName;
        roleText.text = role;
        hpText.text = $"{hp}";
        atkText.text = $"×{atk:0.0#}";
        defText.text = $"{Mathf.RoundToInt(def * 100f)}%";
        itemText.text = startItemName;
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

    // 배율 = 칸 높이 ÷ 창 높이의 정수(1 이상, 도트 굵기를 고르게). 창 = 영역 × 배율, 그림은 영역 가운데가 창 가운데에 오게 둔다
    private void UpdatePortrait(Sprite sprite)
    {
        var scale = UiDotScale.CanvasPerTexel(4f / 3, GetComponentInParent<Canvas>().scaleFactor);
        portraitWindow.sizeDelta = new Vector2(portraitArea.width * scale, portraitArea.height * scale);
        var spriteSize = sprite.rect.size;
        var areaCenter = new Vector2(portraitArea.x + portraitArea.width * 0.5f, spriteSize.y - (portraitArea.y + portraitArea.height * 0.5f));
        portraitImage.sprite = sprite;
        portraitImage.preserveAspect = true;
        var imageRect = portraitImage.rectTransform;
        imageRect.sizeDelta = spriteSize * scale;
        imageRect.anchoredPosition = (spriteSize * 0.5f - areaCenter) * scale;
    }
}
