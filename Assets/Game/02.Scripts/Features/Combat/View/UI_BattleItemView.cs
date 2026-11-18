using UnityEngine;
using UnityEngine.UI;

// 전투 하단 아이템 칸 하나(기획서 F절 V2): 아이콘 또는 이름, 상태 글, 쿨타임 막대
public class UI_BattleItemView : MonoBehaviour
{
    [SerializeField] private Sprite normalBackground;
    [SerializeField] private Sprite disabledBackground;
    [SerializeField] private Image background;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text labelText;
    [SerializeField] private Text stateText;
    [SerializeField] private GameObject cooldownBar;
    [SerializeField] private RectTransform cooldownFill;
    [SerializeField] private Image cooldownFillImage;
    [SerializeField] private Color backgroundColor = new(0.83f, 0.74f, 0.55f, 0.92f);
    [SerializeField] private Color disabledBackgroundColor = new(0.3f, 0.27f, 0.22f, 0.92f);
    [SerializeField] private Color textColor = new(0.2f, 0.12f, 0.06f, 1f);
    [SerializeField] private Color disabledTextColor = new(0.6f, 0.56f, 0.5f, 1f);
    [SerializeField] private Color readyBarColor = new(0.36f, 0.82f, 0.36f, 1f);
    [SerializeField] private Color coolingBarColor = new(0.9f, 0.55f, 0.15f, 1f);
    [SerializeField] private Color firedTextColor = new(0.75f, 0.2f, 0.05f, 1f);

    // icon이 null이면 이름(label)을 보인다. barRatio = 남은 쿨 ÷ 마지막 주기(0~1)
    public void UpdateUI(string label, Sprite icon, BattleItemState state, float remainingSeconds, float barRatio)
    {
        var isDisabled = state == BattleItemState.Disabled;
        var hasIcon = icon != null;
        background.sprite = isDisabled ? disabledBackground : normalBackground;
        background.color = Color.white;
        iconImage.gameObject.SetActive(hasIcon);
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.color = isDisabled ? disabledTextColor : Color.white;
        labelText.gameObject.SetActive(false);
        stateText.gameObject.SetActive(false);
        labelText.text = label;
        labelText.color = isDisabled ? disabledTextColor : textColor;
        stateText.text = GetStateText(state, remainingSeconds);
        stateText.color = isDisabled ? disabledTextColor : state == BattleItemState.Fired ? firedTextColor : textColor;
        var hasBar = state != BattleItemState.Disabled && state != BattleItemState.Passive;
        cooldownBar.SetActive(hasBar);
        cooldownFill.anchorMax = new Vector2(Mathf.Clamp01(barRatio), 1f);
        cooldownFillImage.color = state == BattleItemState.Cooling ? coolingBarColor : readyBarColor;
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

    private static string GetStateText(BattleItemState state, float remainingSeconds)
    {
        switch (state)
        {
            case BattleItemState.Disabled:
                return "비활성";
            case BattleItemState.Passive:
                return "지속 효과";
            case BattleItemState.Fired:
                return "발동!";
            case BattleItemState.Cooling:
                return $"{remainingSeconds:0.0}초";
            default:
                return "준비";
        }
    }
}
