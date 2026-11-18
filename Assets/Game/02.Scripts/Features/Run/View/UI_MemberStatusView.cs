using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전투 하단 캐릭터별 칸(V2): 이름, HP 막대, 아이템 칸(4열 최대 16칸, F3 BattleItemListPresenter가 그린다).
public class UI_MemberStatusView : MonoBehaviour
{
    [SerializeField] private Image portraitFrame;
    [SerializeField] private Sprite alivePortraitFrame;
    [SerializeField] private Sprite downedPortraitFrame;
    [SerializeField] private RectTransform portraitWindow;
    [SerializeField] private Image portraitImage;
    [SerializeField] private RectInt portraitArea = new(14, 16, 36, 36);
    [SerializeField] private Text nameText;
    [SerializeField] private Text hpText;
    [SerializeField] private RectTransform hpFill;
    // HP 막대 안 채움 뒤의 빨간 구간(F6)
    [SerializeField] private UI_HpLossView hpLossView;
    [SerializeField] private UI_BattleItemView[] battleItemViews;
    [SerializeField] private Color aliveNameColor = Color.white;
    [SerializeField] private Color downedNameColor = new(0.5f, 0.5f, 0.5f, 1f);

    public IReadOnlyList<UI_BattleItemView> BattleItemViews => battleItemViews;
    public UI_HpLossView HpLossView => hpLossView;

    public void UpdateUI(string characterName, float hp, float maxHp, bool isAlive, Sprite portraitSprite)
    {
        portraitFrame.sprite = isAlive ? alivePortraitFrame : downedPortraitFrame;
        var hasPortrait = portraitSprite != null;
        portraitWindow.gameObject.SetActive(hasPortrait);
        if (hasPortrait)
        {
            // 로비와 같은 36×36 창. 하단은 고정 1배로 판넬 크기에 따른 확대·축소 없이 표시한다.
            var scale = UiDotScale.CanvasPerTexel(1f, GetComponentInParent<Canvas>().scaleFactor);
            portraitWindow.sizeDelta = (Vector2)portraitArea.size * scale;
            var spriteSize = portraitSprite.rect.size;
            var areaCenter = new Vector2(portraitArea.x + portraitArea.width * 0.5f,
                spriteSize.y - (portraitArea.y + portraitArea.height * 0.5f));
            portraitImage.sprite = portraitSprite;
            portraitImage.preserveAspect = true;
            portraitImage.rectTransform.sizeDelta = spriteSize * scale;
            portraitImage.rectTransform.anchoredPosition = (spriteSize * 0.5f - areaCenter) * scale;
        }

        nameText.text = characterName;
        nameText.color = isAlive ? aliveNameColor : downedNameColor;
        hpText.text = $"{Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
        hpFill.anchorMax = new Vector2(Mathf.Clamp01(hp / maxHp), 1f);
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);
}
