using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 보관함 항목 하나(기획서 C절): 이름, 등급 색, 칸 모양 작은 그림(4×4), 새 전리품 "새" 표시
public class UI_StashItemView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private Text nameText;
    [SerializeField] private GameObject newMark;
    // 칸 모양 작은 그림, 줄 순서(index = y × 4 + x)
    [SerializeField] private Image[] shapeCells;
    [SerializeField] private Color itemColor = new(0.85f, 0.76f, 0.58f, 1f);
    [SerializeField] private Color pickedColor = new(1f, 0.93f, 0.6f, 1f);
    [SerializeField] private Color shapeOnColor = new(0.35f, 0.25f, 0.15f, 1f);
    [SerializeField] private Color shapeOffColor = new(0.35f, 0.25f, 0.15f, 0.15f);

    private UI_StashView _owner;

    public int Uid { get; private set; } = -1;

    public void Bind(UI_StashView owner) => _owner = owner;

    public void UpdateUI(StashItemDisplay display)
    {
        Uid = display.Uid;
        background.color = display.IsPicked ? pickedColor : itemColor;
        border.color = display.GradeColor;
        nameText.text = display.Name;
        newMark.SetActive(display.IsNew);
        for (var i = 0; i < shapeCells.Length; i++)
        {
            var cell = new Vector2Int(i % 4, i / 4);
            shapeCells[i].color = display.CellList.Contains(cell) ? shapeOnColor : shapeOffColor;
        }
    }

    public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

    public void OnPointerDown(PointerEventData eventData) => _owner.ReportPressed(Uid);

    public void OnDrag(PointerEventData eventData) => _owner.ReportDragged(BagPointerUtils.FindTarget(eventData));

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.dragging)
        {
            _owner.ReportDropped(BagPointerUtils.FindTarget(eventData));
            return;
        }

        _owner.ReportTapped(BagTarget.StashItem(Uid));
    }

    // 항목 클릭이 부모 보관함의 빈 곳 탭으로 올라가지 않게 여기서 받는다(탭은 OnPointerUp이 보고했다)
    public void OnPointerClick(PointerEventData eventData)
    {
    }
}

// 보관함 항목 하나의 표시 값. CellList = 회전 0 칸 모양
public struct StashItemDisplay
{
    public int Uid;
    public string Name;
    public Color GradeColor;
    public List<Vector2Int> CellList;
    public bool IsNew;
    public bool IsPicked;
}
