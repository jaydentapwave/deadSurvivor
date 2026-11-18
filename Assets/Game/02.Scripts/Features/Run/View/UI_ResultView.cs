using UnityEngine;
using UnityEngine.UI;

// 결과 화면(V4): 승리·패배, 버틴 웨이브, 처치 수, 전투 시간, 로비로 버튼
public class UI_ResultView : MonoBehaviour
{
    [SerializeField] private Text outcomeText;
    [SerializeField] private Text wavesText;
    [SerializeField] private Text killsText;
    [SerializeField] private Text timeText;
    [SerializeField] private Button toLobbyButton;
    [SerializeField] private Color winColor = new(1f, 0.85f, 0.4f, 1f);
    [SerializeField] private Color loseColor = new(0.9f, 0.35f, 0.3f, 1f);

    public Button ToLobbyButton => toLobbyButton;

    public void UpdateUI(RunOutcome outcome, int wavesSurvived, int waveCount, int kills, float battleSeconds)
    {
        var isWin = outcome == RunOutcome.Win;
        outcomeText.text = isWin ? "승리" : "패배";
        outcomeText.color = isWin ? winColor : loseColor;
        wavesText.text = $"{wavesSurvived}/{waveCount}";
        killsText.text = $"{kills}";
        var seconds = Mathf.RoundToInt(battleSeconds);
        timeText.text = $"{seconds / 60}:{seconds % 60:00}";
    }
}
