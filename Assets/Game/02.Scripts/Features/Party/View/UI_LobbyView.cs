using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 로비(V1): 캐릭터 카드 4장과 출발 버튼
public class UI_LobbyView : MonoBehaviour
{
    [SerializeField] private UI_CharacterCardView[] cardViews;
    [SerializeField] private Button departButton;

    public Button DepartButton => departButton;
    public int CardSlotCount => cardViews.Length;
    public bool IsDepartInteractable => departButton.interactable && departButton.gameObject.activeInHierarchy;

    // 초상이 그림인 보이는 카드 수
    public int PortraitCount
    {
        get
        {
            var count = 0;
            foreach (var cardView in cardViews)
            {
                if (cardView.gameObject.activeSelf && cardView.HasPortrait)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // 카드 칸보다 데이터가 적으면 남는 칸을 숨긴다
    public void UpdateUI(IReadOnlyList<CharacterData> characterDataList, IReadOnlyList<string> startItemNameList,
        IReadOnlyList<Color> colorList, IReadOnlyList<Sprite> portraitSpriteList)
    {
        for (var i = 0; i < cardViews.Length; i++)
        {
            var hasCharacter = i < characterDataList.Count;
            cardViews[i].SetVisible(hasCharacter);
            if (!hasCharacter)
            {
                continue;
            }

            var data = characterDataList[i];
            cardViews[i].UpdateUI(data.Name, data.Role, data.Hp, data.Atk, data.Def, startItemNameList[i], colorList[i],
                portraitSpriteList[i]);
        }
    }
}
