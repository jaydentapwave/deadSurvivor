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

    public Button NextWaveButton => nextWaveButton;
    // 대원 자리 순서(F3 RevivePresenter가 그린다)
    public IReadOnlyList<UI_ReviveView> ReviveViews => reviveViews;

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
