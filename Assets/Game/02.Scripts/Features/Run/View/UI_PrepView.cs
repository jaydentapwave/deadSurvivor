using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 정비 화면(V3): "웨이브 n 완료", 캐릭터 4명 자리(이름·HP), 다음 웨이브 버튼
public class UI_PrepView : MonoBehaviour
{
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

    public Button NextWaveButton => nextWaveButton;
    // 대원 자리 순서(F3 RevivePresenter가 그린다)
    public IReadOnlyList<UI_ReviveView> ReviveViews => reviveViews;
    // 대원 자리 순서
    public IReadOnlyList<UI_BagView> BagViews => bagViews;
    public UI_StashView StashView => stashView;
    public UI_ItemInfoView ItemInfoView => itemInfoView;
    public Button RotateButton => rotateButton;
    public Button CancelButton => cancelButton;

    public void UpdateUI(int completedWave, IReadOnlyList<string> memberLineList)
    {
        titleText.text = $"웨이브 {completedWave} 완료";
        for (var i = 0; i < memberTexts.Length; i++)
        {
            var hasMember = i < memberLineList.Count;
            memberTexts[i].gameObject.SetActive(hasMember);
            if (hasMember)
            {
                memberTexts[i].text = memberLineList[i];
            }
        }
    }
}
