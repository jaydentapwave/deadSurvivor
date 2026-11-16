using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전투 하단 캐릭터별 칸(V2): 이름, HP 막대, 아이템 칸(4열 최대 16칸, F3 BattleItemListPresenter가 그린다).
public class UI_MemberStatusView : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text hpText;
    [SerializeField] private RectTransform hpFill;
    // HP 막대 안 채움 뒤의 빨간 구간(F6)
    [SerializeField] private UI_HpLossView hpLossView;
    [SerializeField] private UI_BattleItemView[] battleItemViews;
    [SerializeField] private Color aliveNameColor = Color.white;
    [SerializeField] private Color downedNameColor = new(0.5f, 0.5f, 0.5f, 1f);

    public IReadOnlyList<UI_BattleItemView> BattleItemViews => battleItemViews;
    public UI_HpLossView HpLossView => hpLossView;

    public void UpdateUI(string characterName, float hp, float maxHp, bool isAlive)
    {
        nameText.text = characterName;
        nameText.color = isAlive ? aliveNameColor : downedNameColor;
        hpText.text = $"{Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
        hpFill.anchorMax = new Vector2(Mathf.Clamp01(hp / maxHp), 1f);
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);
}
