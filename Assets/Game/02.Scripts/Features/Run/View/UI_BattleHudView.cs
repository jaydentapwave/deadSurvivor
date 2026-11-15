using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전투 화면(V2) 표시줄: 상단(웨이브·처치·위협·남은 시간·버닝 타임·배속·일시정지), 하단 캐릭터별 칸
public class UI_BattleHudView : MonoBehaviour
{
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
        waveText.text = $"웨이브 {wave}/{waveCount}";
        killsText.text = $"처치 {kills}";
        threatText.text = $"위협 {threat}";
        var seconds = Mathf.CeilToInt(remainingSeconds);
        remainingText.text = $"{seconds / 60}:{seconds % 60:00}";
        burningLabel.SetActive(isBurningTime);
        speedText.text = $"×{speed:0.#}";
        pauseText.text = isPaused ? "계속" : "일시정지";
    }
}
