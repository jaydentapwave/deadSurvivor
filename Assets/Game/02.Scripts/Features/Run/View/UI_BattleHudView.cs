using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전투 화면(V2) 표시줄: 상단(웨이브·처치·위협·남은 시간·버닝 타임·배속·일시정지), 하단 캐릭터별 칸
public class UI_BattleHudView : MonoBehaviour
{
    [SerializeField] private Image[] waveNodes;
    [SerializeField] private Sprite passedNode;
    [SerializeField] private Sprite currentNode;
    [SerializeField] private Sprite remainingNode;
    [SerializeField] private Image pauseIcon;
    [SerializeField] private Sprite pauseSprite;
    [SerializeField] private Sprite resumeSprite;
    [SerializeField] private Text waveText;
    [SerializeField] private Text killsText;
    [SerializeField] private Text threatText;
    [SerializeField] private Text remainingText;
    [SerializeField] private GameObject burningLabel;
    [SerializeField] private Button speedButton;
    [SerializeField] private Text speedText;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Text pauseText;
    [SerializeField] private UI_MemberStatusView[] memberStatusViews;

    public Button SpeedButton => speedButton;
    public Button PauseButton => pauseButton;
    public IReadOnlyList<UI_MemberStatusView> MemberStatusViews => memberStatusViews;

    public void UpdateUI(int wave, int waveCount, int kills, int threat, float remainingSeconds, bool isBurningTime,
        float speed, bool isPaused)
    {
        waveText.text = $"{wave}/{waveCount}";
        for (var i = 0; i < waveNodes.Length; i++)
        {
            var state = WaveProgressMath.Resolve(i, wave, waveCount);
            waveNodes[i].sprite = state == WaveNodeState.Passed ? passedNode : state == WaveNodeState.Current ? currentNode : remainingNode;
            waveNodes[i].rectTransform.sizeDelta = Vector2.one * (state == WaveNodeState.Current ? 12 : 8);
        }
        pauseIcon.sprite = isPaused ? resumeSprite : pauseSprite;
        killsText.text = $"처치 {kills}";
        threatText.text = $"위협 {threat}";
        var seconds = Mathf.CeilToInt(remainingSeconds);
        remainingText.text = $"{seconds / 60}:{seconds % 60:00}";
        burningLabel.SetActive(isBurningTime);
        speedText.text = $"×{speed:0.#}";
        pauseText.text = "";
    }
}
