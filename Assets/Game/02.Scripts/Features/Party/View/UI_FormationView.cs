using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 정비 화면 진형 판넬(V3, F2 기획서 F절): 진형 버튼(이름, 대기 진형 강조)과 대기 진형 자리 미리보기(대원 4명 이름 표지).
// 버튼은 장면에 둔 것 중 진형 수만큼 켠다. 미리보기는 전투와 같은 모양(세로 절반 압축).
public class UI_FormationView : MonoBehaviour
{
    // 아이소 바닥의 세로 압축(IsoProjection과 같은 모양)
    private const float PreviewVerticalCompress = 0.5f;

    [SerializeField] private Sprite normalRow;
    [SerializeField] private Sprite selectedRow;
    [SerializeField] private Image[] selectionMarkers;
    [SerializeField] private Image[] portraitFaces;
    [SerializeField] private Sprite aliveFrame;
    [SerializeField] private Sprite deadFrame;
    [SerializeField] private Button[] formationButtons;
    [SerializeField] private Text[] formationButtonTexts;
    [SerializeField] private RectTransform[] memberMarkers;
    [SerializeField] private Text[] memberMarkerTexts;
    [SerializeField] private Image[] memberMarkerImages;
    // 미리보기 캔버스 단위 / 바닥 단위
    [SerializeField] private float previewScale = 0.6f;
    [SerializeField] private Color selectedButtonColor = new(0.95f, 0.75f, 0.3f, 1f);
    [SerializeField] private Color normalButtonColor = new(0.3f, 0.3f, 0.35f, 1f);
    [SerializeField] private Color aliveMarkerColor = new(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color deadMarkerColor = new(0.4f, 0.4f, 0.4f, 1f);

    public IReadOnlyList<Button> FormationButtons => formationButtons;

    public void UpdateUI(IReadOnlyList<FormationData> formationDataList, string pendingId, IReadOnlyList<Vector2> pendingSlots,
        IReadOnlyList<string> memberNameList, IReadOnlyList<bool> memberAliveList, IReadOnlyList<Sprite> portraitList)
    {
        for (var i = 0; i < formationButtons.Length; i++)
        {
            var hasFormation = i < formationDataList.Count;
            formationButtons[i].gameObject.SetActive(hasFormation);
            if (!hasFormation)
            {
                continue;
            }

            var data = formationDataList[i];
            formationButtonTexts[i].text = data.Name;
            var selected = data.Id == pendingId;
            formationButtons[i].image.sprite = selected ? selectedRow : normalRow;
            formationButtons[i].image.color = Color.white;
            formationButtonTexts[i].color = selected ? new Color(236f / 255, 236f / 255, 236f / 255) : new Color(187f / 255, 187f / 255, 186f / 255);
            selectionMarkers[i].enabled = selected;
        }

        for (var i = 0; i < memberMarkers.Length; i++)
        {
            var hasMember = i < memberNameList.Count && i < pendingSlots.Count;
            memberMarkers[i].gameObject.SetActive(hasMember);
            if (!hasMember)
            {
                continue;
            }

            var slot = pendingSlots[i];
            memberMarkers[i].anchoredPosition = new Vector2(slot.x, slot.y * PreviewVerticalCompress) * previewScale;
            memberMarkerTexts[i].text = "";
            memberMarkerImages[i].sprite = memberAliveList[i] ? aliveFrame : deadFrame;
            memberMarkerImages[i].color = Color.white;
            var face = portraitFaces[i];
            face.sprite = portraitList[i];
            var scale = UiDotScale.CanvasPerTexel(1f, GetComponentInParent<Canvas>().scaleFactor);
            var spriteSize = portraitList[i].rect.size;
            face.rectTransform.sizeDelta = spriteSize * scale;
            face.rectTransform.anchoredPosition = (spriteSize * 0.5f - new Vector2(34, spriteSize.y - 32)) * scale;
        }
    }
}
