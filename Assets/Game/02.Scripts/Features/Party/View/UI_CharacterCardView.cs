using UnityEngine;
using UnityEngine.UI;

// 로비 캐릭터 카드(V1): 임시 도형 색, 이름, 직업, HP, 공격 배수, 방어, 시작 아이템 이름
public class UI_CharacterCardView : MonoBehaviour
{
    [SerializeField] private Image colorImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text roleText;
    [SerializeField] private Text hpText;
    [SerializeField] private Text atkText;
    [SerializeField] private Text defText;
    [SerializeField] private Text itemText;

    public void UpdateUI(string characterName, string role, int hp, float atk, float def, string startItemName, Color color)
    {
        colorImage.color = color;
        nameText.text = characterName;
        roleText.text = role;
        hpText.text = $"HP {hp}";
        atkText.text = $"공격 ×{atk:0.0#}";
        defText.text = $"방어 {Mathf.RoundToInt(def * 100f)}%";
        itemText.text = $"시작: {startItemName}";
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);
}
