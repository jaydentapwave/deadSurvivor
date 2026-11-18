using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 정비 화면(V3): "웨이브 n 완료", 캐릭터 4명 자리(이름·HP), 다음 웨이브 버튼
public class UI_PrepView : MonoBehaviour
{
    [SerializeField] private RectTransform[] memberHpFills;
    [SerializeField] private Text titleText;
    [SerializeField] private Text[] memberTexts;
    [SerializeField] private Button nextWaveButton;
    [SerializeField] private UI_ReviveView[] reviveViews;
    // F5 가방·보관함·설명, 회전·선택 취소(BagPresenter가 그린다)
    [SerializeField] private UI_BagView[] bagViews;
    [SerializeField] private UI_StashView stashView;
    [SerializeField] private UI_ItemInfoView itemInfoView;
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button cancelButton;

    private Vector2 _layoutSize;

    public Button NextWaveButton => nextWaveButton;
    // 대원 자리 순서(F3 RevivePresenter가 그린다)
    public IReadOnlyList<UI_ReviveView> ReviveViews => reviveViews;
    // 대원 자리 순서
    public IReadOnlyList<UI_BagView> BagViews => bagViews;
    public UI_StashView StashView => stashView;
    public UI_ItemInfoView ItemInfoView => itemInfoView;
    public Button RotateButton => rotateButton;
    public Button CancelButton => cancelButton;

    private void LateUpdate()
    {
        UpdateButtonLabel(rotateButton);
        UpdateButtonLabel(cancelButton);
        var size = ((RectTransform)transform).rect.size;
        if (size == _layoutSize)
        {
            return;
        }
        _layoutSize = size;
        // A landscape notch reduces usable height. Wide headers can share the
        // next-wave and member rows horizontally while retaining 44px hit areas.
        var compact = size.y < 354 && size.x >= 580;
        var header = (RectTransform)titleText.transform.parent;
        header.sizeDelta = new Vector2(0, compact ? 64 : 90);
        var anchor = new Vector2(compact ? 0 : 0.5f, 1);
        for (var i = 0; i < memberTexts.Length; i++)
        {
            var x = (compact ? 65 : -168) + i * 112;
            var member = memberTexts[i].rectTransform;
            member.anchorMin = member.anchorMax = anchor;
            member.anchoredPosition = new Vector2(x - 22, compact ? -42 : -58);
            var bar = (RectTransform)memberHpFills[i].parent;
            bar.anchorMin = bar.anchorMax = anchor;
            bar.anchoredPosition = new Vector2(x - 22, compact ? -60 : -79);
            var revive = (RectTransform)reviveViews[i].transform;
            revive.anchorMin = revive.anchorMax = anchor;
            revive.anchoredPosition = new Vector2(x + 36, compact ? -42 : -68);
        }
        foreach (var bag in bagViews)
        {
            var rect = (RectTransform)bag.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, compact ? -70 : -96);
        }
    }

    private static void UpdateButtonLabel(Button button)
    {
        button.GetComponentInChildren<Text>(true).color = button.IsInteractable()
            ? new Color(236f / 255, 236f / 255, 236f / 255)
            : new Color(168f / 255, 172f / 255, 175f / 255);
    }

    public void UpdateUI(int completedWave, IReadOnlyList<string> memberLineList, IReadOnlyList<float> memberHpRatioList)
    {
        titleText.text = $"웨이브 {completedWave} 완료";
        for (var i = 0; i < memberTexts.Length; i++)
        {
            var hasMember = i < memberLineList.Count;
            memberTexts[i].gameObject.SetActive(hasMember);
            if (hasMember)
            {
                memberTexts[i].text = memberLineList[i];
                memberHpFills[i].anchorMax = new Vector2(Mathf.Clamp01(memberHpRatioList[i]), 1);
            }
        }
    }
}
