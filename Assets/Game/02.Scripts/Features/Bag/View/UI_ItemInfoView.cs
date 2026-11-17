using UnityEngine;
using UnityEngine.UI;

// 아이템 설명과 수치 변화 미리보기(기획서 C절 V3). 미리보기는 간략 3줄 + "추가 변화 n개", 상세 보기 버튼으로 전체(스크롤)
public class UI_ItemInfoView : MonoBehaviour
{
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text previewText;
    [SerializeField] private Button detailButton;
    [SerializeField] private Text detailButtonText;
    private UI_ItemIconView _iconView;

    public Button DetailButton => detailButton;

    private void InitializeIcon()
    {
        _iconView = ItemIconUtils.Create(transform);
        _iconView.rectTransform.anchorMin = Vector2.one;
        _iconView.rectTransform.anchorMax = Vector2.one;
        _iconView.rectTransform.pivot = Vector2.one;
        _iconView.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
        _iconView.rectTransform.sizeDelta = new Vector2(32f, 32f);
        var descriptionRect = descriptionText.rectTransform;
        descriptionRect.offsetMax = new Vector2(descriptionRect.offsetMax.x - 36f, descriptionRect.offsetMax.y);
    }

    // hasMoreLines = 간략 보기에서 숨긴 줄이 있다(상세 보기 버튼을 보인다)
    public void UpdateUI(string description, string preview, bool hasMoreLines, bool isDetailShown,
        Sprite icon = null, int rotation = 0)
    {
        if (_iconView == null)
        {
            InitializeIcon();
        }

        descriptionText.text = description;
        previewText.text = preview;
        detailButton.gameObject.SetActive(hasMoreLines || isDetailShown);
        detailButtonText.text = isDetailShown ? "간략 보기" : "상세 보기";
        _iconView.UpdateUI(icon, rotation, new Rect(0f, 0f, 1f, 1f), Color.white);
    }
}
