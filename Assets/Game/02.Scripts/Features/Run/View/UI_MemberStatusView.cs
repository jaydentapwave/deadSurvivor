using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전투 하단 캐릭터별 칸(V2): 이름, HP 막대, 아이템 목록 틀(4열 최대 16칸). F1은 시작 아이템 이름 1칸.
public class UI_MemberStatusView : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text hpText;
    [SerializeField] private RectTransform hpFill;
    [SerializeField] private Text[] itemSlotTexts;
    [SerializeField] private Color aliveNameColor = Color.white;
    [SerializeField] private Color downedNameColor = new(0.5f, 0.5f, 0.5f, 1f);

    public void UpdateUI(string characterName, float hp, float maxHp, bool isAlive, IReadOnlyList<string> itemNameList)
    {
        nameText.text = characterName;
        nameText.color = isAlive ? aliveNameColor : downedNameColor;
        hpText.text = $"{Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
        hpFill.anchorMax = new Vector2(Mathf.Clamp01(hp / maxHp), 1f);
        for (var i = 0; i < itemSlotTexts.Length; i++)
        {
            var hasItem = i < itemNameList.Count;
            itemSlotTexts[i].transform.parent.gameObject.SetActive(hasItem);
            if (hasItem)
            {
                itemSlotTexts[i].text = itemNameList[i];
            }
        }
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);
}
